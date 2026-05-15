using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Trading;

// Partial fuer den WebSocket-Stream-Management (User-Data + Ticker).
// Split-Teil von LiveTradingService (24.04.2026, P1-1 Gott-Klasse-Split).
//
// Enthaelt: Ticker-Stream (SL/TP-Reaktion <100 ms), User-Data-Stream (Account/Order-Events),
// ListenKey-Lifecycle (Renew 30 min, Reconnect nach 2 Fehlern), Cleanup.
// Haupt-Datei LiveTradingService.cs haelt die Felder (_wsClient, _listenKey, _tickerPriceHandler,
// _wsTickerPrices, IsWsTickerActive, _listenKeyRenewTimer).
public partial class LiveTradingService
{
    /// <summary>
    /// Startet den WebSocket-Ticker-Stream für Echtzeit-Preise.
    /// Erlaubt schnellere SL/TP-Reaktion als 5s REST-Polling.
    /// </summary>
    private async Task StartTickerStreamAsync()
    {
        if (_wsClient == null) return;
        try
        {
            _tickerPriceHandler = (symbol, price) => _wsTickerPrices[symbol] = price;
            _wsClient.TickerPriceReceived += _tickerPriceHandler;
            await _wsClient.SubscribeAllTickersAsync().ConfigureAwait(false);
            IsWsTickerActive = true;
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "WebSocket",
                "Echtzeit-Ticker-Stream aktiv (sub-100ms Latenz)"));
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "WebSocket",
                $"Ticker-Stream nicht verfügbar: {ex.Message}. Fallback auf 5s REST-Polling."));
        }
    }

    /// <summary>Gibt den WebSocket-Preis für ein Symbol zurück, falls verfügbar.</summary>
    public decimal? GetWebSocketPrice(string symbol) =>
        _wsTickerPrices.TryGetValue(symbol, out var price) ? price : null;

    // ═══════════════════════════════════════════════════════════════
    // WebSocket User-Data-Stream (Live-spezifisch)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Startet den User-Data-Stream für Echtzeit-Account/Position-Updates.
    /// ListenKey wird alle 30 Minuten erneuert.
    /// </summary>
    private async Task StartUserDataStreamAsync(CancellationToken ct)
    {
        try
        {
            _listenKey = await _restClient.CreateListenKeyAsync().ConfigureAwait(false);

            // NF21 Fix — Refresher beim WS-Client verdrahten. Bei WS-Disconnect ruft der interne
            // Reconnect-Loop diesen Callback auf, um einen frischen ListenKey zu holen. Vorher
            // versuchte der Reconnect den alten Key zu verwenden — bei abgelaufenem Key blieb
            // der User-Data-Stream bis zum naechsten 30-min-Renewal-Tick tot.
            _wsClient!.ListenKeyRefresher = async (refreshCt) =>
                await _restClient.CreateListenKeyAsync().ConfigureAwait(false);

            await _wsClient.ConnectUserDataStreamAsync(_listenKey, ct).ConfigureAwait(false);

            _wsClient.UserDataReceived += OnUserDataReceived;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                "User-Data-Stream verbunden (Echtzeit-Updates aktiv)"));

            // ListenKey alle 30 Minuten erneuern, bei 2+ Fehlern Reconnect
            var renewFailures = 0;
            _listenKeyRenewTimer = new PeriodicTimer(TimeSpan.FromMinutes(30));
            while (await _listenKeyRenewTimer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await _restClient.RenewListenKeyAsync(_listenKey).ConfigureAwait(false);
                    renewFailures = 0;
                }
                catch (Exception ex)
                {
                    renewFailures++;
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                        $"ListenKey-Erneuerung fehlgeschlagen ({renewFailures}x): {ex.Message}"));

                    // Bei 2+ Fehlern: Neuen ListenKey erstellen und WS-Verbindung neu aufbauen
                    if (renewFailures >= 2)
                    {
                        try
                        {
                            if (_wsClient.IsUserDataConnected)
                                await _wsClient.DisconnectUserDataStreamAsync().ConfigureAwait(false);

                            _listenKey = await _restClient.CreateListenKeyAsync().ConfigureAwait(false);
                            await _wsClient.ConnectUserDataStreamAsync(_listenKey, ct).ConfigureAwait(false);
                            renewFailures = 0;

                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                                "User-Data-Stream neu verbunden (ListenKey erneuert)"));
                        }
                        catch (Exception reconnectEx)
                        {
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                                $"User-Data-Stream Reconnect fehlgeschlagen: {reconnectEx.Message}. Fallback: REST-Polling."));
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                $"User-Data-Stream konnte nicht gestartet werden: {ex.Message} (Fallback: REST-Polling)"));
        }
    }

    /// <summary>Verarbeitet User-Data-Stream Events (ACCOUNT_UPDATE, ORDER_TRADE_UPDATE).</summary>
    private void OnUserDataReceived(object? sender, string message)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(message);
            var root = doc.RootElement;

            var eventType = root.TryGetProperty("e", out var eProp) ? eProp.GetString() : null;

            switch (eventType)
            {
                case "ACCOUNT_UPDATE":
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "WebSocket",
                        "Account-Update empfangen (Balance/Position geändert)"));
                    break;

                case "ORDER_TRADE_UPDATE":
                    if (root.TryGetProperty("o", out var orderData))
                    {
                        var symbol = orderData.TryGetProperty("s", out var sProp) ? sProp.GetString() : "?";
                        var status = orderData.TryGetProperty("X", out var xProp) ? xProp.GetString() : "?";
                        var oSide = orderData.TryGetProperty("S", out var sideProp) ? sideProp.GetString() : "?";
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "WebSocket",
                            $"Order-Update: {symbol} {oSide} → {status}", symbol));

                        // v1.4.0 Phase 0.2/0.3 (Findings 0.2/0.3) — TP1/TP2 Limit-Fill-Detection.
                        // BingX User-Data-Stream feuert bei jedem Order-Status-Change. Bei FILLED auf
                        // einer unserer TP-Reduce-Only-Limits triggern wir die Phase-Transition
                        // (Signal patchen, BE setzen, CompletedTrade publishen) — der PriceTickerLoop
                        // hat den TP-Hit-Check bereits geskippt (siehe IsTpManagedByExchange).
                        // PARTIALLY_FILLED triggert NICHT — sonst frueher Phase-Wechsel bei niedriger Liquiditaet.
                        if (status == "FILLED" && !string.IsNullOrEmpty(symbol))
                        {
                            string? orderId = null;
                            if (orderData.TryGetProperty("i", out var iProp))
                            {
                                orderId = iProp.ValueKind == System.Text.Json.JsonValueKind.Number
                                    ? iProp.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture)
                                    : iProp.GetString();
                            }
                            if (!string.IsNullOrEmpty(orderId))
                            {
                                // Fire-and-forget — der WebSocket-Handler darf nicht blockieren.
                                // Exceptions im Helper werden dort geloggt.
                                _ = SafeProcessTpFillAsync(symbol!, orderId!);
                            }
                        }
                    }
                    break;
            }
        }
        catch
        {
            // Parse-Fehler ignorieren - User-Data ist optional
        }
    }

    /// <summary>
    /// Wrapper um <see cref="ProcessTpLimitFillAsync"/> mit Exception-Handling, damit ein
    /// Bug im Helper den Stream-Handler nicht killt.
    /// </summary>
    private async Task SafeProcessTpFillAsync(string symbol, string orderId)
    {
        try
        {
            await ProcessTpLimitFillAsync(symbol, orderId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "WebSocket",
                $"TP-Limit-Fill-Verarbeitung fuer {symbol}/{orderId} fehlgeschlagen: {ex.Message}", symbol));
        }
    }

    /// <summary>
    /// Räumt den User-Data-Stream sauber auf: Event-Handler abmelden, Timer stoppen,
    /// ListenKey löschen, WebSocket trennen.
    /// </summary>
    private async Task CleanupUserDataStreamAsync()
    {
        _listenKeyRenewTimer?.Dispose();
        _listenKeyRenewTimer = null;

        if (_wsClient != null)
            _wsClient.UserDataReceived -= OnUserDataReceived;

        if (_wsClient != null && _wsClient.IsUserDataConnected)
        {
            try { await _wsClient.DisconnectUserDataStreamAsync().ConfigureAwait(false); }
            catch { /* Best-effort beim Cleanup */ }
        }

        if (_listenKey != null)
        {
            try { await _restClient.DeleteListenKeyAsync(_listenKey).ConfigureAwait(false); }
            catch { /* Best-effort */ }
            _listenKey = null;
        }
    }
}
