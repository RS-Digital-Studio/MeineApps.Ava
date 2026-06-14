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
    private async Task StartTickerStreamAsync(CancellationToken ct)
    {
        if (_wsClient == null) return;
        try
        {
            _tickerPriceHandler = (symbol, price) => _wsTickerPrices[symbol] = price;
            _wsClient.TickerPriceReceived += _tickerPriceHandler;
            // Haupt-WS explizit verbinden — SubscribeAsync schickt das Abo sonst ins Leere
            // (loggt nur "nicht verbunden" und merkt den Handler fuer einen Reconnect vor,
            // der nie kommt) und IsWsTickerActive waere eine Fehlanzeige.
            if (!_wsClient.IsConnected)
                await _wsClient.ConnectAsync(ct).ConfigureAwait(false);
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

                            // Native SL-/TP-Market-Fills (STOP_MARKET / TAKE_PROFIT_MARKET) buchen.
                            // Diese Orders schliessen die Position exchange-seitig — ohne diesen Pfad
                            // hatte ein nativer SL-Fill KEINEN Buchungsweg (Position verschwand vor dem
                            // naechsten 5-s-Tick, Orphan-Cleanup loeschte still, der 30-min-Income-
                            // Backfill buchte einen synthetischen Null-Record).
                            var orderType = orderData.TryGetProperty("o", out var otProp) ? otProp.GetString() : null;
                            if (orderType is "STOP_MARKET" or "TAKE_PROFIT_MARKET")
                            {
                                var ps = orderData.TryGetProperty("ps", out var psProp) ? psProp.GetString() : null;
                                var avgPrice = ReadWsDecimal(orderData, "ap");
                                var filledQty = ReadWsDecimal(orderData, "z");
                                var commission = ReadWsDecimal(orderData, "n");
                                var realizedPnl = ReadWsDecimal(orderData, "rp");
                                _ = SafeProcessNativeCloseFillAsync(symbol!, orderType!, ps, oSide,
                                    avgPrice, filledQty, commission, realizedPnl);
                            }
                            else if (!string.IsNullOrEmpty(orderId))
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
    /// Liest ein decimal-Feld aus dem ORDER_TRADE_UPDATE-Payload (String oder Number).
    /// Liefert null wenn das Feld FEHLT oder unparsbar ist — der Unterschied zu einem echten
    /// 0-Wert ist entscheidend: realizedPnl == 0 ist ein valider Break-Even-Close (kein
    /// Fallback auf die lokale Preis-Rechnung), commission == 0 eine valide Maker-Rabatt-Fee.
    /// </summary>
    private static decimal? ReadWsDecimal(System.Text.Json.JsonElement orderData, string name)
    {
        if (!orderData.TryGetProperty(name, out var prop)) return null;
        if (prop.ValueKind == System.Text.Json.JsonValueKind.Number && prop.TryGetDecimal(out var num))
            return num;
        if (prop.ValueKind == System.Text.Json.JsonValueKind.String &&
            decimal.TryParse(prop.GetString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    /// <summary>Exception-sicherer Wrapper um <see cref="ProcessNativeCloseFillAsync"/>.</summary>
    private async Task SafeProcessNativeCloseFillAsync(string symbol, string orderType, string? positionSide,
        string? orderSide, decimal? avgPrice, decimal? filledQty, decimal? commission, decimal? realizedPnl)
    {
        try
        {
            await ProcessNativeCloseFillAsync(symbol, orderType, positionSide, orderSide,
                avgPrice, filledQty, commission, realizedPnl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "WebSocket",
                $"Native-Close-Fill-Verarbeitung fuer {symbol} ({orderType}) fehlgeschlagen: {ex.Message}", symbol));
        }
    }

    /// <summary>
    /// Bucht einen exchange-seitigen Market-Close (nativer STOP_MARKET-SL oder TAKE_PROFIT_MARKET)
    /// als echten CompletedTrade mit den Fill-Daten aus dem ORDER_TRADE_UPDATE-Event
    /// (avgPrice = ap, Menge = z, Kommission = n, realisierter PnL = rp).
    /// Das Buchungs-Gate (<see cref="TryClaimNativeCloseBooking"/>) verhindert Doppel-Buchung
    /// gegen den Ticker-Mikro-Race-Zweig in OnSlTpHitAsync und die Orphan-Rekonstruktion.
    /// </summary>
    internal async Task<bool> ProcessNativeCloseFillAsync(string symbol, string orderType,
        string? positionSide, string? orderSide, decimal? avgPrice, decimal? filledQty,
        decimal? commission, decimal? realizedPnl)
    {
        // Positions-Seite: bevorzugt "ps" (LONG/SHORT, Hedge-Mode), Fallback Order-Side-Inversion
        // (der SL eines Longs verkauft). NIE reduceOnly nutzen — im Hedge-Mode immer false.
        Side side;
        if (string.Equals(positionSide, "LONG", StringComparison.OrdinalIgnoreCase)) side = Side.Buy;
        else if (string.Equals(positionSide, "SHORT", StringComparison.OrdinalIgnoreCase)) side = Side.Sell;
        else if (string.Equals(orderSide, "SELL", StringComparison.OrdinalIgnoreCase)) side = Side.Buy;
        else if (string.Equals(orderSide, "BUY", StringComparison.OrdinalIgnoreCase)) side = Side.Sell;
        else return false;

        var key = $"{symbol}_{side}";

        // Entry-Kontext: ohne ExitState keine belastbaren Entry-Daten → Orphan-/Backfill-Pfad
        // uebernimmt. (Gate hier noch NICHT claimen, sonst blockieren wir die Fallback-Buchung.)
        if (!_exitStates.TryGetValue(key, out var es) || es.EntryPrice <= 0m)
            return false;

        if (!TryClaimNativeCloseBooking(key)) return false;

        // Menge: echte Fill-Menge vom Event; Fallback Rest nach TP1-Teilschliessung.
        var qty = filledQty is > 0m
            ? filledQty.Value
            : (es.PartialClosed
                ? Math.Max(0m, es.OriginalQuantity * (1m - _riskSettings.Tp1CloseRatio))
                : es.OriginalQuantity);
        if (qty <= 0m) qty = es.OriginalQuantity;

        var exitPrice = avgPrice is > 0m ? avgPrice.Value : (es.Signal.StopLoss ?? 0m);
        var entryFee = qty * es.EntryPrice * _takerFeeRate;
        // Feld-Praesenz statt !=0: commission == 0 ist eine valide Maker-Rabatt-Fee,
        // realizedPnl == 0 ein valider Break-Even-Close (kein Fallback auf die lokale Rechnung).
        var exitFee = commission.HasValue ? Math.Abs(commission.Value) : qty * exitPrice * _takerFeeRate;
        var rawPnl = side == Side.Buy
            ? (exitPrice - es.EntryPrice) * qty
            : (es.EntryPrice - exitPrice) * qty;
        // BingX "rp" ist der realisierte Brutto-PnL des Fills (Kommission separat in "n").
        var grossPnl = realizedPnl ?? rawPnl;

        var reason = orderType == "STOP_MARKET"
            ? "Native Stop-Loss (WebSocket)"
            : "Native Take-Profit (WebSocket)";
        var navTf = GetNavigatorTimeframeForKey(key);
        var trade = new CompletedTrade(symbol, side, es.EntryPrice, exitPrice, qty,
            grossPnl - entryFee - exitFee, entryFee + exitFee,
            es.EntryTime, DateTime.UtcNow, reason, TradingMode.Live, navTf);

        // Signal + ExitState entfernen, uebrige native Orders (TP-Limits) aufraeumen.
        RemoveSignalByKey(key);
        try { await CancelNativeSlTpOrdersAsync(symbol, side).ConfigureAwait(false); }
        catch { /* Verwaiste Orders sind ungefaehrlich */ }

        ProcessCompletedTrade(trade);
        _eventBus.PublishTrade(trade);

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
            $"LIVE: {symbol} {side}: {reason} @ {exitPrice} (qty={qty:F8}, PnL={grossPnl - entryFee - exitFee:F4})",
            symbol));

        try { await PersistExitStatesAsync().ConfigureAwait(false); }
        catch { /* best-effort */ }
        return true;
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
