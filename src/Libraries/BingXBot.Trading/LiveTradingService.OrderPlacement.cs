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

            // TrendFollow nutzt ausschliesslich Market-Entry (kein Limit-Pullback-Entry mehr).

            // v1.6.2 Phase 12 — SlippageGuard fuer Market-Orders.
            // Pragmatisch ohne dediziertes OrderBook-API: nutzen Ticker.AskPrice/BidPrice als
            // Best-Level-Proxy. Spread (Ask - Last) / Last ist eine konservative Slippage-Schaetzung.
            // Schwelle per-Kategorie (Forex 0.05 %, Crypto 0.10 %, Stock 0.30 %), Fallback global.
            if (_scannerSettings.SlippageGuardEnabled
                && ticker.LastPrice > 0 && ticker.AskPrice > 0 && ticker.BidPrice > 0)
            {
                var refPrice = ticker.LastPrice;
                var spreadPct = side == Side.Buy
                    ? (ticker.AskPrice - refPrice) / refPrice * 100m
                    : (refPrice - ticker.BidPrice) / refPrice * 100m;
                var slippageThreshold = _scannerSettings.GetMaxSlippagePercent(category);
                if (spreadPct > slippageThreshold)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                        $"{ticker.Symbol} [{category}]: Order geblockt — Slippage-Estimate {spreadPct:F3}% > Threshold {slippageThreshold:F3}% (SlippageGuard)",
                        ticker.Symbol));
                    return false;
                }
            }
            const OrderType orderType = OrderType.Market;
            decimal? limitPrice = null;

            // TP wird NICHT im Haupt-Order gesetzt — stattdessen separate TP-Market-Orders
            // mit spezifischer Quantity (TP1 30% bei 161.8%, TP2 Rest bei 200%)
            // Nativer TP auf Haupt-Order würde 100% schließen und Partial-Close überschreiben
            //
            // v1.5.5 Phase 8 (Finding Order-Retry) — Wrapped in OrderRetryPolicy: HTTP 429 / 5xx /
            // Timeouts / spezifische BingX-Error-Codes (109400, 100410) werden mit Exp-Backoff
            // (100/300/1000/3000 ms) bis zu 4× wiederholt. Vor jedem Retry pruefen wir per
            // Idempotency-Check (GetPositionsAsync), ob die Order beim ersten Versuch in Wahrheit
            // doch durchging und nur die Response timeoutete — sonst Doppel-Place.
            Order order = null!;
            order = await Resilience.OrderRetryPolicy.ExecuteAsync(async () =>
            {
                // Idempotency-Pre-Check vor jedem Retry-Versuch (ausser dem ersten):
                // Position oder pending Limit-Order schon da → skippen, gefakte "neue" Order zurueckgeben.
                if (order != null) // Marker: vorheriger Versuch lief und wirft jetzt Retry
                {
                    try
                    {
                        var existing = await _restClient.GetPositionsAsync().ConfigureAwait(false);
                        var match = existing.FirstOrDefault(p => p.Symbol == ticker.Symbol && p.Side == side);
                        if (match != null && match.Quantity > 0)
                        {
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                                $"{ticker.Symbol}: Idempotency-Treffer — Position existiert bereits, kein Doppel-Place",
                                ticker.Symbol));
                            return order;
                        }
                    }
                    catch { /* Idempotency-Check Best-Effort */ }
                }
                var placed = await _restClient.PlaceOrderAsync(new OrderRequest(
                    ticker.Symbol, side, orderType, quantity,
                    Price: limitPrice,
                    StopLoss: signal?.StopLoss,
                    TakeProfit: null),
                    lastPrice: ticker.LastPrice)
                    .ConfigureAwait(false);
                if (placed.Status == OrderStatus.Rejected)
                    return placed; // Nicht retryen — strukturelle Ablehnung.
                return placed;
            }, onRetry: (attempt, ex) =>
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                    $"{ticker.Symbol}: Order-Retry #{attempt} nach Fehler ({ex.GetType().Name}: {ex.Message}) — Backoff {Resilience.OrderRetryPolicy.GetBackoffMs(attempt + 1)} ms",
                    ticker.Symbol));
            }).ConfigureAwait(false);

            if (order.Status == OrderStatus.Rejected)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                    $"LIVE ORDER ABGELEHNT: {ticker.Symbol} {side}", ticker.Symbol));
                return false;
            }

            // TP1 + TP2 als LIMIT Reduce-Only Orders auf BingX (stackbar, Maker-Fee 0.02%)
            // Reguläre LIMIT-Orders mit reduceOnly=true: BingX erlaubt beliebig viele pro Position.
            // Market-Entry → Position existiert sofort, TP wird direkt nach dem Fill platziert.
            if (signal?.TakeProfit.HasValue == true && signal.TakeProfit.Value > 0)
            {
                await PlaceTpLimitOrdersAfterFillAsync(ticker.Symbol, side, quantity, signal).ConfigureAwait(false);
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
    ///
    /// v1.4.0 Phase 0.6 (Finding 0.6) — 2-Stage-Retry:
    /// - Stage 1: Position-Read mit 3× 1 s Retry (heutige Logik). Wenn sichtbar → TP mit echter Qty.
    /// - Stage 2 (NEU): Position nach 3 s nicht da → TP-Place mit <paramref name="fallbackQty"/>.
    ///   BingX kann die Order ggf. wegen "no position" rejecten. Bei Reject: ExitState.PendingTpRetry=true,
    ///   Stage 3 (in OnBeforePriceTickerIteration) versucht nach max 30 s erneut.
    /// </summary>
    private async Task PlaceTpLimitOrdersAfterFillAsync(string symbol, Side side, decimal fallbackQty, SignalResult signal)
    {
        try
        {
            // Stage 1: Position mit Retry lesen — BingX braucht bei Market-Orders manchmal 1-3 s
            // bis die Position in GetPositionsAsync auftaucht.
            Position? actualPos = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var posAfterOrder = await _restClient.GetPositionsAsync().ConfigureAwait(false);
                actualPos = posAfterOrder.FirstOrDefault(p => p.Symbol == symbol && p.Side == side);
                if (actualPos != null && actualPos.Quantity > 0) break;
                if (attempt < 3)
                    await Task.Delay(1000).ConfigureAwait(false);
            }

            // Stage 2 (Phase 0.6): Position nicht sichtbar → mit fallbackQty trotzdem versuchen.
            // Lieber ein gelogtes Reject ("no position") als eine ungeschuetzte Position bis SL-Hit.
            // Bei Reject: ExitState.PendingTpRetry=true → Stage 3 retried in OnBeforePriceTickerIteration.
            if (actualPos == null || actualPos.Quantity <= 0)
            {
                var posKeyStage2 = $"{symbol}_{side}";
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                    $"LIVE: {symbol} Position nach 3s nicht bei BingX registriert — Stage-2-TP-Place mit fallbackQty={fallbackQty:F8} versuchen",
                    symbol));

                // Min-Qty-aware Split (siehe SplitTpQuantity): winzige Positionen → ein Full-TP bei TP1.
                var s2WantsTp2 = signal.TakeProfit2.HasValue && signal.TakeProfit2.Value > 0
                                 && signal.TakeProfit2.Value != signal.TakeProfit!.Value;
                var (stage2Tp1Qty, stage2Tp2Qty, _) = SplitTpQuantity(
                    symbol, fallbackQty, s2WantsTp2, signal.TakeProfit ?? 0m, signal.TakeProfit2 ?? signal.TakeProfit ?? 0m);

                string? s2Tp1 = null, s2Tp2 = null;
                if (signal.TakeProfit.HasValue && stage2Tp1Qty > 0)
                    s2Tp1 = await PlaceTpWithRetryAsync(symbol, side, stage2Tp1Qty, signal.TakeProfit!.Value, "TP1 Stage2").ConfigureAwait(false);
                if (s2WantsTp2 && stage2Tp2Qty > 0)
                    s2Tp2 = await PlaceTpWithRetryAsync(symbol, side, stage2Tp2Qty, signal.TakeProfit2!.Value, "TP2 Stage2").ConfigureAwait(false);

                // Phase 0.2/0.3 — auch Stage-2-Erfolge ins ExitState schreiben, sonst greift der
                // Skip-Block im PriceTickerLoop nicht und der Doppel-Close-Race entsteht hier.
                if (_exitStates.TryGetValue(posKeyStage2, out var esStage2))
                {
                    var stage2Mutated = false;
                    if (!string.IsNullOrEmpty(s2Tp1)) { esStage2.Tp1LimitOrderId = s2Tp1; stage2Mutated = true; }
                    if (!string.IsNullOrEmpty(s2Tp2)) { esStage2.Tp2LimitOrderId = s2Tp2; stage2Mutated = true; }
                    // F10 Fix — TP-OrderId synchron persistieren, damit ein Hot-Crash zwischen
                    // hier und naechstem StopAsync die Zuordnung nicht verliert.
                    if (stage2Mutated)
                        await PersistExitStatesAsync().ConfigureAwait(false);
                }

                // Stage 3 vorbereiten: nur retry-Marker, wenn beide Stage-2-Versuche fehlschlugen.
                if (string.IsNullOrEmpty(s2Tp1) && string.IsNullOrEmpty(s2Tp2))
                {
                    if (_exitStates.TryGetValue(posKeyStage2, out var es2))
                    {
                        es2.PendingTpRetry = true;
                        if (es2.PendingTpFirstAttemptUtc == default)
                            es2.PendingTpFirstAttemptUtc = DateTime.UtcNow;
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                            $"LIVE: {symbol} Stage-2-TP-Place fehlgeschlagen — Stage 3 (PendingTpRetry) aktiviert", symbol));
                    }
                }
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

            // Min-Qty-Guard (02.06.2026): Bei winzigen Positionen, deren Teilmenge unter die Min-Order
            // faellt, kein Split — ein Full-TP bei TP1 (verhindert BingX-Reject, z.B. ETH 0.01/Min-Qty 0.01).
            if (hasTp2 && (tp2Qty <= 0m
                || !_restClient.MeetsMinimumOrder(symbol, tp1Qty, signal.TakeProfit!.Value)
                || !_restClient.MeetsMinimumOrder(symbol, tp2Qty, signal.TakeProfit2!.Value)))
            {
                hasTp2 = false;
                tp1Qty = Math.Round(actualQty, 6);
                tp2Qty = 0m;
            }

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

            // v1.4.0 Phase 0.2/0.3 (Findings 0.2/0.3) — OrderIds ins ExitState schreiben.
            // Solange Tp1LimitOrderId / Tp2LimitOrderId gesetzt sind, skippt der PriceTickerLoop
            // den Bot-seitigen TP-Hit-Check (BingX fuellt nativ + WebSocket-Fill-Event triggert Phase-Transition).
            // Verhindert Doppel-Close-Race: Bot Market-closed mit pos.Quantity*0.5 nach BingX-Limit-Partial-Fill
            // → falsche Mengen, kaputte CompletedTrade-Buchhaltung.
            var posKeyExit = $"{symbol}_{side}";
            if (_exitStates.TryGetValue(posKeyExit, out var esTp))
            {
                var stage1Mutated = false;
                if (!string.IsNullOrEmpty(tp1OrderId)) { esTp.Tp1LimitOrderId = tp1OrderId; stage1Mutated = true; }
                if (!string.IsNullOrEmpty(tp2OrderId)) { esTp.Tp2LimitOrderId = tp2OrderId; stage1Mutated = true; }
                // F10 Fix — TP-OrderIds synchron persistieren (Crash-Recovery).
                if (stage1Mutated)
                    await PersistExitStatesAsync().ConfigureAwait(false);
            }

            // v1.4.0 Phase 0.6 (Finding 0.6): Erfolgreicher Stage-1-Place setzt PendingTpRetry=false
            // (Reset eines moeglichen Stage-3-Markers aus vorherigem Versuch).
            var posKeyAfterPlace = $"{symbol}_{side}";
            if (_exitStates.TryGetValue(posKeyAfterPlace, out var esAfter))
            {
                if (esAfter.PendingTpRetry)
                {
                    esAfter.PendingTpRetry = false;
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                        $"LIVE: {symbol} Stage-3-Retry erfolgreich abgeschlossen — TPs platziert nach {esAfter.PendingTpRetryCount} Versuch(en)",
                        symbol));
                }
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
    ///
    /// v1.5.5 Phase 8 — nutzt jetzt <see cref="Resilience.OrderRetryPolicy"/> fuer Exception-basierte
    /// Retries (HTTP 429/5xx/Timeout/BingX 109400/100410) PLUS einen separaten Reject-basierten
    /// Retry-Loop (BingX-Reject ist KEINE Exception, sondern <c>OrderStatus.Rejected</c>).
    /// </summary>
    private async Task<string?> PlaceTpWithRetryAsync(string symbol, Side side, decimal quantity, decimal price, string tpLabel)
    {
        // Reject-basierte Retries: 3 Versuche mit 1.5 s Pause — historisches Verhalten beibehalten.
        // Exception-basierte Retries (Timeout/429/5xx) laufen INNERHALB jedes Versuchs durch
        // OrderRetryPolicy.ExecuteAsync, sodass kurze Netz-Bumps kein Reject-Versuch verbrauchen.
        // Phase 18 / A2 — IdempotencyCheck via GetOpenOrdersAsync: vor jedem inneren Retry
        // pruefen, ob der vorherige Place-Versuch (z.B. nach TaskCanceledException) in Wahrheit
        // doch ankam — sonst Doppel-TP-Place mit reduceOnly-Konflikt.
        var closeSide = side == Side.Buy ? Side.Sell : Side.Buy;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            Order order;
            try
            {
                order = await Resilience.OrderRetryPolicy.ExecuteAsync(() =>
                    _restClient.PlaceTpReduceOnlyLimitAsync(symbol, side, quantity, price),
                    onRetry: (retry, ex) =>
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                            $"LIVE: {symbol} {tpLabel} Inner-Retry #{retry} ({ex.GetType().Name}: {ex.Message})",
                            symbol));
                    },
                    idempotencyCheck: () => ProbeExistingTpOrderAsync(symbol, closeSide, quantity, price, tpLabel)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Exception-Retries erschoepft → wie Reject behandeln und ggf. weiter retryen.
                order = new Order(string.Empty, symbol, side, OrderType.Limit, price, quantity,
                    null, DateTime.UtcNow, OrderStatus.Rejected, RejectionReason: ex.Message,
                    ReduceOnly: true);
            }

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

    /// <summary>
    /// Phase 18 / A2 — IdempotencyCheck-Probe fuer TP-Reduce-Only-Limits.
    /// Sucht in den offenen Orders eine TP-Limit, die zur erwarteten Zielorder passt
    /// (gleicher Symbol/Side/Qty/Price ± Toleranzfenster, ReduceOnly + Limit). Findet das Probe
    /// einen Treffer, gilt der vorherige Place-Versuch als erfolgreich gelaufen — wir vermeiden
    /// den Doppel-Place. Wird nur vor inneren Retry-Versuchen aufgerufen.
    /// Toleranz: Qty 0.5 % (BingX truncated nach Precision-Cache), Price 0.05 % (Tick-Round).
    /// </summary>
    private async Task<Order?> ProbeExistingTpOrderAsync(
        string symbol, Side closeSide, decimal expectedQuantity, decimal expectedPrice, string tpLabel)
    {
        // Phase 18 / G7 (Teil-Extraktion) — Match-Logik in pure-function-Helper TpOrderMatcher.
        // Hier nur noch der I/O-Part (REST-Call) + Logging.
        var openOrders = await _restClient.GetOpenOrdersAsync(symbol).ConfigureAwait(false);
        var match = Reconciliation.TpOrderMatcher.FindMatchingTpOrder(
            openOrders, symbol, closeSide, expectedQuantity, expectedPrice);
        if (match != null)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                $"LIVE: {symbol} {tpLabel} Idempotency-Treffer — TP existiert bereits (OrderId={match.OrderId}, Qty={match.Quantity:F8} @ {match.Price:F8})",
                symbol));
        }
        return match;
    }

    /// <summary>
    /// v1.4.0 Phase 0.2/0.3 (Findings 0.2/0.3) — verarbeitet ein Order-Filled-Event vom
    /// User-Data-Stream fuer Bot-platzierte TP1/TP2-Reduce-Only-Limits.
    ///
    /// Flow:
    /// 1) Suche ExitState mit passender <see cref="PositionExitState.Tp1LimitOrderId"/> /
    ///    <see cref="PositionExitState.Tp2LimitOrderId"/>.
    /// 2) Bei TP1-Fill: Phase Initial → Tp1Hit, PartialClosed=true, Signal-TP auf Tp2 patchen,
    ///    nativen SL/TP auf BingX aktualisieren (BE / neuer TP). CompletedTrade mit Maker-Fee.
    /// 3) Bei TP2-Fill: Position vollstaendig zu — CompletedTrade publishen, Signal entfernen,
    ///    native Orders aufraeumen.
    /// 4) Idempotent: nach Verarbeitung wird die Tp1/Tp2-OrderId genullt → Duplicate-Events
    ///    werden ignoriert.
    /// </summary>
    /// <returns>True wenn das Event eine Bot-TP-Order war und verarbeitet wurde.</returns>
    internal async Task<bool> ProcessTpLimitFillAsync(string symbol, string orderId)
    {
        if (string.IsNullOrEmpty(orderId)) return false;

        // ExitState-Lookup: finde den Eintrag dessen Tp1/Tp2 OrderId matcht.
        string? matchedKey = null;
        PositionExitState? es = null;
        foreach (var kvp in _exitStates)
        {
            var v = kvp.Value;
            if (v.Symbol != symbol) continue;
            if (v.Tp1LimitOrderId == orderId || v.Tp2LimitOrderId == orderId)
            {
                matchedKey = kvp.Key;
                es = v;
                break;
            }
        }
        if (matchedKey == null || es == null) return false;

        var isTp1 = es.Tp1LimitOrderId == orderId;
        var isTp2 = es.Tp2LimitOrderId == orderId;

        // Position nach dem Limit-Fill abrufen — gibt Aufschluss ueber tatsaechliche Restmenge.
        var positions = await _restClient.GetPositionsAsync().ConfigureAwait(false);
        var posAfter = positions.FirstOrDefault(p => p.Symbol == symbol && p.Side == es.Side);

        if (isTp1 && es.Phase == ExitPhase.Initial)
        {
            // Geschlossene Menge: tatsaechliche Restmenge-Differenz, Fallback Tp1CloseRatio.
            var expectedClosedQty = es.OriginalQuantity * _riskSettings.Tp1CloseRatio;
            var closedQty = posAfter != null
                ? Math.Max(0m, es.OriginalQuantity - posAfter.Quantity)
                : expectedClosedQty;
            if (closedQty <= 0m) closedQty = expectedClosedQty;

            var fillPrice = es.Signal.TakeProfit ?? 0m;
            // Maker-Fee — Bot-platzierte Reduce-Only-LIMIT ist Maker. Anteilige Entry-Fee
            // (proportional zur geschlossenen Menge) gegen Doppel-Buchung beim TP2-Fill.
            // Market-Entry ist TAKER; die Exit-Fee bleibt Maker (Bot-Reduce-Only-Limit).
            var entryFee = closedQty * es.EntryPrice * _takerFeeRate;
            var exitFee = closedQty * fillPrice * _makerFeeRate;
            var totalFee = entryFee + exitFee;
            var rawPnl = es.Side == Side.Buy
                ? (fillPrice - es.EntryPrice) * closedQty
                : (es.EntryPrice - fillPrice) * closedQty;
            var navTf = GetNavigatorTimeframeForKey(matchedKey);
            var trade = new CompletedTrade(symbol, es.Side, es.EntryPrice, fillPrice,
                closedQty, rawPnl - totalFee, totalFee, es.EntryTime, DateTime.UtcNow,
                "TP1 (Limit-Fill via WebSocket)", TradingMode.Live, navTf);
            ProcessCompletedTrade(trade);
            _eventBus.PublishTrade(trade);

            // Phase-Transition: Initial -> Tp1Hit.
            es.PartialClosed = true;
            es.Tp1LimitOrderId = null;  // Idempotent: Duplicate-WS-Event triggert nichts mehr.
            if (es.Tp2.HasValue && _positionSignals.TryGetValue(matchedKey, out var sigOld))
            {
                var sigNew = sigOld with { TakeProfit = es.Tp2 };
                _positionSignals[matchedKey] = sigNew;
                es.Signal = sigNew;
                es.Phase = ExitPhase.Tp1Hit;
            }

            // SL/TP auf BingX aktualisieren (BE-Trigger / neuer TP).
            try
            {
                await _restClient.SetPositionSlTpAsync(
                    symbol, es.Side,
                    es.Signal.StopLoss,
                    es.Signal.TakeProfit).ConfigureAwait(false);
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                    $"LIVE: {symbol} TP1 Limit-Fill via WebSocket erkannt → Phase Tp1Hit, SL/TP aktualisiert",
                    symbol));
            }
            catch (Exception slEx)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                    $"LIVE: {symbol} SL/TP-Update nach TP1-Fill fehlgeschlagen: {slEx.Message}", symbol));
            }
            // F10 Fix — Phase-Transition Initial→Tp1Hit + Tp1LimitOrderId=null synchron persistieren.
            await PersistExitStatesAsync().ConfigureAwait(false);
            return true;
        }

        if (isTp2 && (es.Phase == ExitPhase.Tp1Hit || es.Phase == ExitPhase.Initial))
        {
            // Tp2 oder Tp-Single (Phase=Initial mit nur einem TP der per LIMIT lief): Position komplett zu.
            var totalEntryQty = es.OriginalQuantity > 0 ? es.OriginalQuantity : (posAfter?.Quantity ?? 0m);
            var alreadyClosedTp1 = es.Phase == ExitPhase.Tp1Hit
                ? totalEntryQty * _riskSettings.Tp1CloseRatio
                : 0m;
            var closedQty = Math.Max(0m, totalEntryQty - alreadyClosedTp1 - (posAfter?.Quantity ?? 0m));
            if (closedQty <= 0m && posAfter == null)
                closedQty = totalEntryQty - alreadyClosedTp1;
            if (closedQty <= 0m) closedQty = Math.Max(0m, totalEntryQty - alreadyClosedTp1);

            var fillPrice = es.Signal.TakeProfit ?? 0m;
            // Market-Entry ist TAKER; die Exit-Fee bleibt Maker (Bot-Reduce-Only-Limit).
            var entryFee = closedQty * es.EntryPrice * _takerFeeRate;
            var exitFee = closedQty * fillPrice * _makerFeeRate;
            var totalFee = entryFee + exitFee;
            var rawPnl = es.Side == Side.Buy
                ? (fillPrice - es.EntryPrice) * closedQty
                : (es.EntryPrice - fillPrice) * closedQty;
            var navTf = GetNavigatorTimeframeForKey(matchedKey);
            var trade = new CompletedTrade(symbol, es.Side, es.EntryPrice, fillPrice,
                closedQty, rawPnl - totalFee, totalFee, es.EntryTime, DateTime.UtcNow,
                es.Phase == ExitPhase.Tp1Hit ? "TP2 (Limit-Fill via WebSocket)" : "TP (Limit-Fill via WebSocket)",
                TradingMode.Live, navTf);
            ProcessCompletedTrade(trade);
            _eventBus.PublishTrade(trade);

            // Cleanup: Signal/ExitState entfernen, native Orders auf Symbol+Side aufraeumen.
            es.Tp2LimitOrderId = null;
            es.Tp1LimitOrderId = null;
            RemoveSignalByKey(matchedKey);
            try { await CancelNativeSlTpOrdersAsync(symbol, es.Side).ConfigureAwait(false); }
            catch { /* best-effort */ }

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                $"LIVE: {symbol} TP{(es.Phase == ExitPhase.Tp1Hit ? "2" : "")} Limit-Fill via WebSocket erkannt → Position vollstaendig geschlossen",
                symbol));
            // F10 Fix — Vollstaendiger Close synchron persistieren, damit DB-Snapshot den Remove kennt.
            await PersistExitStatesAsync().ConfigureAwait(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Platziert TP-Orders für eine bestehende Position (Recovery nach App-Neustart).
    /// Wird vom LiveTradingManager aufgerufen wenn eine Position ohne TP-Orders erkannt wird.
    /// </summary>
    public Task RecoverTpOrdersAsync(string symbol, Side side, decimal quantity, SignalResult signal) =>
        PlaceTpLimitOrdersAfterFillAsync(symbol, side, quantity, signal);
}
