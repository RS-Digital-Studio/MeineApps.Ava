using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Trading;

// Partial fuer Order-Placement (Entry + TP Limit-Orders).
// Split-Teil von LiveTradingService (24.04.2026, P1-1 Gott-Klasse-Split).
//
// Enthaelt:
// - PlaceOrderOnExchangeAsync: Entry-Order (Market oder Limit, SL inline, Leverage, MarginType)
// - PlaceTpLimitOrdersAfterFillAsync: TP1/TP2 als Reduce-Only-LIMIT nach Fill (Maker-Fee)
// - PlaceTpWithRetryAsync: 3-Versuchs-Retry fuer einzelne TP-Order
// - OnOrderPlacedAsync: Entry-Fee-Logging
public partial class LiveTradingService
{
    protected override async Task<bool> PlaceOrderOnExchangeAsync(Ticker ticker, Side side, decimal quantity, SignalResult? signal = null, int adaptiveLeverage = 0)
    {
        try
        {
            // SK-VERIFY: [6.3] Isolated Margin VOR jeder Order sicherstellen
            // Ohne Isolated Margin kann ein einzelner Trade das gesamte Konto liquidieren
            try
            {
                await _restClient.SetMarginTypeAsync(ticker.Symbol, MarginType.Isolated)
                    .ConfigureAwait(false);
            }
            catch (Exception marginEx)
            {
                // SK-VERIFY: [6.3] Erwartete Fehler: Position offen (MarginType nicht änderbar)
                // oder bereits Isolated. Unerwartete Fehler loggen.
                var msg = marginEx.Message;
                if (!msg.Contains("isolated", StringComparison.OrdinalIgnoreCase)
                    && !msg.Contains("position", StringComparison.OrdinalIgnoreCase)
                    && !msg.Contains("margin type", StringComparison.OrdinalIgnoreCase))
                {
                    _eventBus.PublishLog(new Core.Models.LogEntry(DateTime.UtcNow,
                        Core.Enums.LogLevel.Warning, "Exchange",
                        $"{ModePrefix}{ticker.Symbol}: SetMarginType(Isolated) fehlgeschlagen: {msg}",
                        ticker.Symbol));
                }
            }

            // Leverage setzen (adaptiv oder kategoriespezifisch)
            var category = Core.Helpers.SymbolClassifier.Classify(ticker.Symbol);
            var catMaxLev = (int)_riskSettings.GetCategorySettings(category).MaxLeverage;
            var leverage = adaptiveLeverage > 0
                ? Math.Min(adaptiveLeverage, catMaxLev)
                : catMaxLev;
            await _restClient.SetLeverageAsync(ticker.Symbol, leverage, side)
                .ConfigureAwait(false);

            // Order platzieren: Limit wenn bevorzugt und Entry-Preis vorhanden, sonst Market
            var useLimit = signal?.PreferLimitOrder == true && signal.EntryPrice.HasValue && signal.EntryPrice.Value > 0;
            var orderType = useLimit ? OrderType.Limit : OrderType.Market;
            var limitPrice = useLimit ? signal!.EntryPrice : null;

            if (useLimit)
            {
                // Stale-Sequence-Cleanup (19.04.2026): pending Orders auf veralteten Sequenzen
                // fuer (Symbol, Seite) cancelln bevor die neue Limit-Order platziert wird.
                // Schuetzt vor Fills auf alten Fib-Levels nach PointA-Shift.
                await CancelStaleSequencePendingAsync(ticker.Symbol, side, signal?.SequenceId).ConfigureAwait(false);

                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                    $"{ticker.Symbol}: Limit-Order bei {limitPrice:F8} (Pullback-Entry, Maker-Fee)", ticker.Symbol));
            }

            // TP wird NICHT im Haupt-Order gesetzt — stattdessen separate TP-Market-Orders
            // mit spezifischer Quantity (TP1 30% bei 161.8%, TP2 Rest bei 200%)
            // Nativer TP auf Haupt-Order würde 100% schließen und Partial-Close überschreiben
            var order = await _restClient.PlaceOrderAsync(new OrderRequest(
                ticker.Symbol, side, orderType, quantity,
                Price: limitPrice,
                StopLoss: signal?.StopLoss,
                TakeProfit: null),
                lastPrice: ticker.LastPrice)
                .ConfigureAwait(false);

            if (order.Status == OrderStatus.Rejected)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                    $"LIVE ORDER ABGELEHNT: {ticker.Symbol} {side}", ticker.Symbol));
                return false;
            }

            // SK-Buch Workflow 5.3: Limit-Order bleibt valid bis Sequenz invalid wird.
            // Invalidation-Level = SignalResult.StopLoss (78.6er gecappt, ≈ Point0).
            // Key enthält SequenceId-Suffix (_Prim/_Add seit v1.2.5, Legacy _L500/_L618/_L667 vor dem
            // Strip Phase 2) — damit Sibling-Entries separat getrackt + bei Invalidierung gemeinsam
            // entfernt werden. Single/Dual-Entry nach Buch (Triple/Quad/Hex sind entfernt).
            if (useLimit && order.OrderId != null && signal?.StopLoss.HasValue == true)
            {
                var isLong = side == Side.Buy;
                var pendingKey = BuildPendingKey(ticker.Symbol, signal.SequenceId);
                _pendingLimitOrders[pendingKey] = (order.OrderId, DateTime.UtcNow, signal.StopLoss.Value,
                    isLong, ticker.Symbol, signal.SequenceId,
                    signal.TakeProfit, signal.TakeProfit2);

                // Periodisches Save (18.04.2026 v1.2.4): Pending-Liste sofort persistieren, damit
                // ein Crash zwischen Order-Platzierung und naechstem Stop den State nicht verliert.
                // Fire-and-forget — Save ist best-effort, darf den Order-Flow nicht blockieren.
                _ = PersistPendingLimitOrdersAsync();
            }

            // TP1 + TP2 als LIMIT Reduce-Only Orders auf BingX (stackbar, Maker-Fee 0.02%)
            // Reguläre LIMIT-Orders mit reduceOnly=true: BingX erlaubt beliebig viele pro Position.
            // Bei Entry-Limit-Orders: Überspringen — Position existiert noch nicht (pending).
            // TP wird im PriceTickerLoop nachgeholt sobald die Limit-Order gefüllt ist.
            if (!useLimit && signal?.TakeProfit.HasValue == true && signal.TakeProfit.Value > 0)
            {
                await PlaceTpLimitOrdersAfterFillAsync(ticker.Symbol, side, quantity, signal).ConfigureAwait(false);
            }
            else if (useLimit && signal?.TakeProfit.HasValue == true)
            {
                // Explizit zeigen dass TP bei Limit-Pending noch NICHT auf BingX ist — erst nach Fill.
                // Ohne diesen Hinweis interpretieren User das Trade-Log ("TP1=... | TP2=...") fälschlich
                // als "TP ist gesetzt" und suchen sie vergeblich im BingX-Orderbuch.
                var tp1Str = signal.TakeProfit.Value.ToString("F8");
                var tp2Str = signal.TakeProfit2.HasValue ? signal.TakeProfit2.Value.ToString("F8") : "---";
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                    $"LIVE: {ticker.Symbol} Limit-Order pending @ {limitPrice:F8} — TP1={tp1Str}, TP2={tp2Str} werden erst NACH Fill auf BingX platziert (Maker-Fee, nicht jetzt sichtbar im Orderbuch)",
                    ticker.Symbol));
            }

            return true;
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                $"LIVE ORDER FEHLGESCHLAGEN: {ticker.Symbol} {side} - {ex.Message}", ticker.Symbol));
            return false;
        }
    }

    /// <summary>
    /// Platziert TP1/TP2 als LIMIT Reduce-Only-Orders nach einem Market-Fill.
    /// Liest echte Position + Qty von BingX (3 Retry-Versuche wegen BingX-Position-Lag)
    /// und platziert dann die TP-Orders gestaffelt gemaess SK-Tp1CloseRatio.
    /// </summary>
    private async Task PlaceTpLimitOrdersAfterFillAsync(string symbol, Side side, decimal fallbackQty, SignalResult signal)
    {
        try
        {
            // Position mit Retry lesen: BingX braucht bei Market-Orders manchmal 1-3s bis die Position
            // in GetPositionsAsync auftaucht. Ohne Retry würde TP-Order mit fallbackQty platziert und
            // könnte als "keine Position" rejected werden (Hedge-Mode mit positionSide=LONG).
            Position? actualPos = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var posAfterOrder = await _restClient.GetPositionsAsync().ConfigureAwait(false);
                actualPos = posAfterOrder.FirstOrDefault(p => p.Symbol == symbol && p.Side == side);
                if (actualPos != null && actualPos.Quantity > 0) break;
                if (attempt < 3)
                    await Task.Delay(1000).ConfigureAwait(false);
            }

            if (actualPos == null || actualPos.Quantity <= 0)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                    $"LIVE: {symbol} TP-Platzierung übersprungen — Position nach 3s noch nicht bei BingX registriert (Fallback: Bot-seitig via PriceTickerLoop)", symbol));
                return;
            }

            var actualQty = actualPos.Quantity;

            // ExitState mit echten Fill-Werten korrigieren (v1.2.5) — deckt Market-Entries ab,
            // die in ScanAndTradeAsync mit ticker.LastPrice als Proxy ausgefuellt wurden.
            // BingX rundet die Qty auf Step-Size, Market-Fills haben Slippage → die echte
            // Position weicht ab. Stufen-Logik (SL halbieren/BE) muss auf den Fill-Preis rechnen.
            var posKeyFill = $"{symbol}_{side}";
            if (_exitStates.TryGetValue(posKeyFill, out var exFillState))
            {
                var qtyBefore = exFillState.OriginalQuantity;
                var epBefore = exFillState.EntryPrice;
                exFillState.EntryPrice = actualPos.EntryPrice;
                exFillState.OriginalQuantity = actualQty;
                // Signal-EntryPrice mit tatsaechlichem Fill-Preis patchen (A-Bruch-BE nutzt EntryPrice).
                if (_positionSignals.TryGetValue(posKeyFill, out var sigFill))
                {
                    var patched = sigFill with { EntryPrice = actualPos.EntryPrice };
                    _positionSignals[posKeyFill] = patched;
                    exFillState.Signal = patched;
                }

                if (_eventBus.HasLogSubscribers
                    && (Math.Abs(qtyBefore - actualQty) > qtyBefore * 0.01m
                        || (epBefore > 0 && Math.Abs(epBefore - actualPos.EntryPrice) > epBefore * 0.001m)))
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
                        $"LIVE: {symbol} Fill-Korrektur Qty={qtyBefore:F4}→{actualQty:F4}, Entry={epBefore:F8}→{actualPos.EntryPrice:F8}",
                        symbol));
                }
            }

            // SK-Buch: Tp1CloseRatio aus RiskSettings (Default 0.5 = 50%)
            var tp1Ratio = _riskSettings.Tp1CloseRatio;
            var tp2Qty = 0m;
            var hasTp2 = signal.TakeProfit2.HasValue && signal.TakeProfit2.Value > 0
                         && signal.TakeProfit2.Value != signal.TakeProfit!.Value;

            // Wenn TP1 == TP2 (BCKL-Fallback / tp1 auf falscher Seite):
            // TP1 deckt die ganze Position ab, damit keine 50% ungeschützt bleiben.
            var tp1Qty = hasTp2
                ? Math.Round(actualQty * tp1Ratio, 6)
                : Math.Round(actualQty, 6);

            if (hasTp2)
            {
                tp2Qty = signal.DisableSmartBreakeven
                    ? Math.Round(actualQty - tp1Qty, 6)  // SK: Rest (Sequenz abgearbeitet bei TP2)
                    : Math.Round(actualQty * _riskSettings.Tp2CloseRatio, 6);
            }

            // Over-Close Guard: TP1+TP2 darf nie > Position
            if (tp1Qty + tp2Qty > actualQty)
                tp2Qty = Math.Round(actualQty - tp1Qty, 6);

            // TP1 als LIMIT Reduce-Only (mit Retry bei Fehler)
            string? tp1OrderId = null;
            if (tp1Qty > 0)
            {
                tp1OrderId = await PlaceTpWithRetryAsync(symbol, side, tp1Qty, signal.TakeProfit!.Value, "TP1").ConfigureAwait(false);
            }

            // TP2 als LIMIT (stackbar — überschreibt TP1 nicht, mit Retry bei Fehler)
            string? tp2OrderId = null;
            if (hasTp2 && tp2Qty > 0)
            {
                tp2OrderId = await PlaceTpWithRetryAsync(symbol, side, tp2Qty, signal.TakeProfit2!.Value, "TP2").ConfigureAwait(false);
            }

            // Verifizieren dass die platzierten TP-Orders tatsächlich im BingX-Orderbuch stehen
            // (Schutz gegen "stumme" API-Erfolge wo Order zurückgegeben wird aber nicht existiert)
            if (!string.IsNullOrEmpty(tp1OrderId) || !string.IsNullOrEmpty(tp2OrderId))
            {
                try
                {
                    var openOrders = await _restClient.GetOpenOrdersAsync(symbol).ConfigureAwait(false);
                    var liveIds = new HashSet<string>(openOrders.Select(o => o.OrderId));

                    if (!string.IsNullOrEmpty(tp1OrderId) && !liveIds.Contains(tp1OrderId))
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                            $"LIVE: {symbol} TP1 Verify fehlgeschlagen — Order {tp1OrderId} nicht im BingX-Orderbuch! (Fallback: Bot-seitig via PriceTickerLoop)", symbol));
                    }
                    if (!string.IsNullOrEmpty(tp2OrderId) && !liveIds.Contains(tp2OrderId))
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                            $"LIVE: {symbol} TP2 Verify fehlgeschlagen — Order {tp2OrderId} nicht im BingX-Orderbuch! (Fallback: Bot-seitig via PriceTickerLoop)", symbol));
                    }
                }
                catch (Exception verifyEx)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
                        $"LIVE: {symbol} TP-Verify übersprungen: {verifyEx.Message}", symbol));
                }
            }
        }
        catch (Exception ex)
        {
            // TP-Orders fehlgeschlagen → Bot-seitiger PriceTickerLoop übernimmt als Fallback
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                $"LIVE: {symbol} TP Limit-Orders fehlgeschlagen (Fallback: Bot-seitig): {ex.Message}", symbol));
        }
    }

    /// <summary>
    /// Platziert eine einzelne TP-Limit-Order mit Retry bei Rejection.
    /// Gibt die OrderId zurück wenn erfolgreich, null bei endgültigem Fehler.
    /// </summary>
    private async Task<string?> PlaceTpWithRetryAsync(string symbol, Side side, decimal quantity, decimal price, string tpLabel)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var order = await _restClient.PlaceTpReduceOnlyLimitAsync(symbol, side, quantity, price).ConfigureAwait(false);

            if (order.Status != OrderStatus.Rejected && !string.IsNullOrEmpty(order.OrderId))
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                    $"LIVE: {symbol} {tpLabel} Limit platziert: {quantity:F8} @ {price:F8} (OrderId={order.OrderId}, Maker-Fee)",
                    symbol));
                return order.OrderId;
            }

            var reason = order.RejectionReason ?? "unbekannt";
            if (attempt < 3)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                    $"LIVE: {symbol} {tpLabel} Versuch {attempt}/3 abgelehnt: {reason} (Qty={quantity:F8}, Preis={price:F8}) — retry in 1.5s",
                    symbol));
                await Task.Delay(1500).ConfigureAwait(false);
            }
            else
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                    $"LIVE: {symbol} {tpLabel} ENDGÜLTIG ABGELEHNT nach 3 Versuchen: {reason} (Qty={quantity:F8}, Preis={price:F8})",
                    symbol));
            }
        }
        return null;
    }

    protected override Task OnOrderPlacedAsync(Ticker ticker, Side side, decimal quantity)
    {
        // Entry-Fee nur loggen, KEINEN CompletedTrade publizieren
        // (Ghost-Trade + doppelte Fee-Zählung vermeiden - Fee wird beim Close eingerechnet)
        var entryNotional = quantity * ticker.LastPrice;
        var entryFee = entryNotional * _takerFeeRate;
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
            $"LIVE: {ticker.Symbol} Entry-Fee: {entryFee:N4} USDT", ticker.Symbol));
        return Task.CompletedTask;
    }
}
