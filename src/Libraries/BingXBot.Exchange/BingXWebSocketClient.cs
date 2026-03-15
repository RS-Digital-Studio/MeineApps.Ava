using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace BingXBot.Exchange;

/// <summary>
/// BingX WebSocket Client mit Auto-Reconnect und Ping/Pong Handling.
/// Unterstützt Kline- und Ticker-Streams für Perpetual Futures.
/// </summary>
public class BingXWebSocketClient : IAsyncDisposable
{
    private const string WsBaseUrl = "wss://open-api-swap.bingx.com/swap-market";
    private const int MaxReconnectAttempts = 5;
    private const int ReceiveBufferSize = 8192;

    private ClientWebSocket? _ws;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly ILogger<BingXWebSocketClient> _logger;
    private int _reconnectAttempts;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, Action<string>> _handlers = new();
    // ListenKey wird erst beim Live-Trading via REST API gesetzt
#pragma warning disable CS0649
    private string? _listenKey;
#pragma warning restore CS0649
    private PeriodicTimer? _listenKeyTimer;
    private Task? _receiveTask;

    public event EventHandler<bool>? ConnectionStateChanged;
    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public BingXWebSocketClient(
        string apiKey,
        string apiSecret,
        ILogger<BingXWebSocketClient> logger)
    {
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        _logger = logger;
    }

    /// <summary>
    /// Verbindung zum BingX WebSocket herstellen und Receive-Loop starten.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _ws = new ClientWebSocket();
        _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

        var uri = new Uri(WsBaseUrl);
        _logger.LogInformation("Verbinde mit BingX WebSocket: {Uri}", uri);

        try
        {
            await _ws.ConnectAsync(uri, _cts.Token);
            _reconnectAttempts = 0;
            ConnectionStateChanged?.Invoke(this, true);
            _logger.LogInformation("BingX WebSocket verbunden");

            // Receive-Loop in Background starten
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

            // ListenKey für User-Data-Streams erneuern (alle 30 Minuten)
            _listenKeyTimer = new PeriodicTimer(TimeSpan.FromMinutes(30));
            _ = Task.Run(async () =>
            {
                while (await _listenKeyTimer.WaitForNextTickAsync(_cts.Token))
                {
                    try
                    {
                        await RenewListenKeyAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ListenKey-Erneuerung fehlgeschlagen");
                    }
                }
            }, _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket-Verbindung fehlgeschlagen");
            ConnectionStateChanged?.Invoke(this, false);
            throw;
        }
    }

    /// <summary>
    /// Channel abonnieren und Handler registrieren.
    /// </summary>
    public async Task SubscribeAsync(string channel, Action<string> handler)
    {
        _handlers[channel] = handler;

        if (!IsConnected)
        {
            _logger.LogWarning("WebSocket nicht verbunden, Channel {Channel} wird bei Reconnect abonniert", channel);
            return;
        }

        var subscribeMsg = JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid().ToString("N"),
            reqType = "sub",
            dataType = channel
        });

        var bytes = Encoding.UTF8.GetBytes(subscribeMsg);
        await _ws!.SendAsync(bytes, WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);

        _logger.LogInformation("Abonniert: {Channel}", channel);
    }

    /// <summary>
    /// Channel-Abo aufheben.
    /// </summary>
    public async Task UnsubscribeAsync(string channel)
    {
        _handlers.TryRemove(channel, out _);

        if (!IsConnected) return;

        var unsubMsg = JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid().ToString("N"),
            reqType = "unsub",
            dataType = channel
        });

        var bytes = Encoding.UTF8.GetBytes(unsubMsg);
        await _ws!.SendAsync(bytes, WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);

        _logger.LogInformation("Abbestellt: {Channel}", channel);
    }

    /// <summary>
    /// Empfangs-Loop: liest Nachrichten, handelt Ping/Pong, leitet Daten an Handler weiter.
    /// BingX sendet gzip-komprimierte Daten.
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[ReceiveBufferSize];

        try
        {
            while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await _ws.ReceiveAsync(buffer, ct);
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogWarning("WebSocket Close empfangen");
                    break;
                }

                // BingX sendet gzip-komprimierte Nachrichten
                string message;
                try
                {
                    ms.Position = 0;
                    using var decompressed = new MemoryStream();
                    using (var gzip = new GZipStream(ms, CompressionMode.Decompress))
                    {
                        await gzip.CopyToAsync(decompressed, ct);
                    }
                    message = Encoding.UTF8.GetString(decompressed.ToArray());
                }
                catch (InvalidDataException)
                {
                    // Nicht gzip-komprimiert - direkt als Text lesen
                    message = Encoding.UTF8.GetString(ms.ToArray());
                }

                // Ping/Pong Handling - BingX sendet "Ping", erwartet "Pong"
                if (message == "Ping" || message.Contains("\"ping\""))
                {
                    var pongBytes = Encoding.UTF8.GetBytes("Pong");
                    await _ws.SendAsync(pongBytes, WebSocketMessageType.Text, true, ct);
                    continue;
                }

                // Nachricht an registrierte Handler weiterleiten
                try
                {
                    using var doc = JsonDocument.Parse(message);
                    var root = doc.RootElement;

                    // Channel aus "dataType" oder "e" Feld extrahieren
                    string? dataType = null;
                    if (root.TryGetProperty("dataType", out var dtProp))
                        dataType = dtProp.GetString();
                    else if (root.TryGetProperty("e", out var eProp))
                        dataType = eProp.GetString();

                    if (dataType != null && _handlers.TryGetValue(dataType, out var handler))
                    {
                        handler.Invoke(message);
                    }
                }
                catch (JsonException)
                {
                    _logger.LogDebug("Nicht-JSON Nachricht: {Message}", message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Receive-Loop abgebrochen");
        }
        catch (WebSocketException ex)
        {
            _logger.LogError(ex, "WebSocket-Fehler in Receive-Loop");
        }

        ConnectionStateChanged?.Invoke(this, false);

        // Auto-Reconnect falls nicht explizit abgebrochen
        if (!ct.IsCancellationRequested)
            await ReconnectAsync();
    }

    /// <summary>
    /// Automatischer Reconnect mit exponentiellem Backoff (iterativ, kein Stack-Overflow-Risiko).
    /// Re-subscribt alle vorherigen Channels.
    /// </summary>
    private async Task ReconnectAsync()
    {
        while (_reconnectAttempts < MaxReconnectAttempts)
        {
            _reconnectAttempts++;
            var backoff = GetBackoff();
            _logger.LogWarning("Reconnect Versuch {Attempt}/{Max} in {Backoff}s...",
                _reconnectAttempts, MaxReconnectAttempts, backoff.TotalSeconds);

            await Task.Delay(backoff);

            try
            {
                // Alte WebSocket-Instanz entsorgen
                _ws?.Dispose();
                _ws = new ClientWebSocket();
                _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

                await _ws.ConnectAsync(new Uri(WsBaseUrl), _cts?.Token ?? CancellationToken.None);
                _reconnectAttempts = 0;
                ConnectionStateChanged?.Invoke(this, true);
                _logger.LogInformation("Reconnect erfolgreich");

                // Alle Channels re-subscriben
                foreach (var channel in _handlers.Keys)
                {
                    await SubscribeAsync(channel, _handlers[channel]);
                }

                // Receive-Loop neu starten
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts?.Token ?? CancellationToken.None));
                return; // Erfolgreich verbunden
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reconnect fehlgeschlagen");
            }
        }

        _logger.LogError("Max Reconnect-Versuche ({Max}) erreicht", MaxReconnectAttempts);
        ConnectionStateChanged?.Invoke(this, false);
    }

    /// <summary>
    /// Exponentieller Backoff: 2^Versuch Sekunden (2s, 4s, 8s, 16s, 32s).
    /// </summary>
    private TimeSpan GetBackoff() => TimeSpan.FromSeconds(Math.Pow(2, _reconnectAttempts));

    /// <summary>
    /// ListenKey für User-Data-Streams erneuern.
    /// </summary>
    private async Task RenewListenKeyAsync()
    {
        if (string.IsNullOrEmpty(_listenKey)) return;

        _logger.LogDebug("Erneuere ListenKey");
        // ListenKey-Erneuerung erfolgt via REST API - wird in BingXDataFeed koordiniert
    }

    /// <summary>
    /// Ressourcen freigeben, WebSocket schließen.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _listenKeyTimer?.Dispose();

        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
        }

        if (_ws is not null)
        {
            if (_ws.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Dispose",
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Fehler beim sauberen Schließen der WebSocket-Verbindung");
                }
            }
            _ws.Dispose();
            _ws = null;
        }

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask;
            }
            catch (OperationCanceledException) { }
        }

        _logger.LogInformation("BingX WebSocket disposed");
    }
}
