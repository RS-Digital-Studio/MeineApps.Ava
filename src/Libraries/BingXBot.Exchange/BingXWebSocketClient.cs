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
    // ListenKey für User-Data-Stream (Account/Position/Order Events)
    private string? _listenKey;
    private Task? _receiveTask;

    // User-Data-Stream: Separater WebSocket für Echtzeit-Account-Updates
    private ClientWebSocket? _userWs;
    private Task? _userReceiveTask;
    private CancellationTokenSource? _userCts;

    public event EventHandler<bool>? ConnectionStateChanged;
    /// <summary>Wird bei Account-Updates (Balance, Position, Order) aus dem User-Data-Stream ausgelöst.</summary>
    public event EventHandler<string>? UserDataReceived;
    public bool IsConnected => _ws?.State == WebSocketState.Open;
    public bool IsUserDataConnected => _userWs?.State == WebSocketState.Open;

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
            // ListenKey-Erneuerung wird extern vom LiveTradingService koordiniert
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
    /// Verbindet den User-Data-Stream (Account/Position/Order Events).
    /// Nutzt einen separaten WebSocket mit ListenKey.
    /// </summary>
    public async Task ConnectUserDataStreamAsync(string listenKey, CancellationToken ct)
    {
        _listenKey = listenKey;
        _userCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _userWs?.Dispose();
        _userWs = new ClientWebSocket();
        _userWs.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

        var uri = new Uri($"{WsBaseUrl}?listenKey={listenKey}");
        _logger.LogInformation("Verbinde User-Data-Stream: {Uri}", uri.Host);

        try
        {
            await _userWs.ConnectAsync(uri, _userCts.Token);
            _logger.LogInformation("User-Data-Stream verbunden");

            // Receive-Loop für User-Data
            _userReceiveTask = Task.Run(() => UserDataReceiveLoopAsync(_userCts.Token), _userCts.Token);
            // ListenKey-Erneuerung wird extern vom LiveTradingService koordiniert
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "User-Data-Stream Verbindung fehlgeschlagen");
            throw;
        }
    }

    /// <summary>Receive-Loop für User-Data-Stream Events.</summary>
    private async Task UserDataReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[ReceiveBufferSize];

        try
        {
            while (!ct.IsCancellationRequested && _userWs?.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await _userWs.ReceiveAsync(buffer, ct);
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogWarning("User-Data-Stream: Close empfangen");
                    break;
                }

                // BingX User-Data kann gzip-komprimiert sein
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
                    message = Encoding.UTF8.GetString(ms.ToArray());
                }

                // Ping/Pong
                if (message == "Ping" || message.Contains("\"ping\""))
                {
                    var pongBytes = Encoding.UTF8.GetBytes("Pong");
                    await _userWs.SendAsync(pongBytes, WebSocketMessageType.Text, true, ct);
                    continue;
                }

                // Event an Subscriber weiterleiten
                UserDataReceived?.Invoke(this, message);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex)
        {
            _logger.LogError(ex, "User-Data-Stream: WebSocket-Fehler");
        }

        _logger.LogWarning("User-Data-Stream: Verbindung beendet");
    }

    /// <summary>Trennt den User-Data-Stream.</summary>
    public async Task DisconnectUserDataStreamAsync()
    {
        if (_userCts != null)
        {
            await _userCts.CancelAsync();
            _userCts.Dispose();
            _userCts = null;
        }

        if (_userWs != null)
        {
            if (_userWs.State == WebSocketState.Open)
            {
                try
                {
                    await _userWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
                }
                catch { /* Best-effort Close */ }
            }
            _userWs.Dispose();
            _userWs = null;
        }

        _listenKey = null;
    }

    /// <summary>
    /// Ressourcen freigeben, WebSocket schließen.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // User-Data-Stream zuerst schließen
        await DisconnectUserDataStreamAsync();

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

        if (_userReceiveTask is not null)
        {
            try
            {
                await _userReceiveTask;
            }
            catch (OperationCanceledException) { }
        }

        _logger.LogInformation("BingX WebSocket disposed");
    }
}
