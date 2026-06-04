using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Core.Services;
using BingXBot.Backtest.Simulation;

namespace BingXBot.Backtest.Portfolio;

/// <summary>
/// Extrahierte Exit-Logik (SL/TP/BE/Partial-Close/Runner-Trailing) aus <c>BacktestEngine.RunAsync</c>.
/// Reiner Refactor — wortgleicher Foreach-Block, damit Single- und Portfolio-Backtest dieselbe
/// Exit-Verarbeitung teilen. Keine Verhaltensaenderung.
/// </summary>
internal static class BacktestExitProcessor
{
    /// <summary>
    /// SL/TP-Check auf offene Positionen mit echten Werten aus dem Signal.
    /// Verarbeitet Runner-Trailing, Break-Even (via <see cref="BreakevenCalculator"/>),
    /// TP1-Partial-Close und finalen SL/TP-Hit (Adverse-Gap-Worst-Case + Candle-Richtungs-Heuristik).
    /// </summary>
    public static async Task ProcessExitsAsync(
        SimulatedExchange simExchange,
        IReadOnlyList<Position> positions,
        Dictionary<string, SignalResult> positionSignals,
        Dictionary<string, BacktestExitState> exitTracking,
        BacktestSettings settings,
        RiskSettings? riskSettings,
        string symbol,
        Candle currentCandle,
        ISymbolInfoProvider? symbolInfo = null)
    {
        // SL/TP-Check auf offene Positionen mit echten Werten aus dem Signal
        // positions ist bereits eine Kopie (IReadOnlyList aus SimulatedExchange), kein ToList() nötig
        foreach (var pos in positions)
        {
            var key = $"{pos.Symbol}_{pos.Side}";
            if (!positionSignals.TryGetValue(key, out var origSignal))
                continue;

            // --- SK Multi-Stage Exit: TP1 Partial Close (161.8%), TP2 Rest (200%+Buffer) ---
            // BE-Trigger via zentralem BreakevenCalculator (identisch zu LiveTradingService):
            //   1) A-Bruch (Buch, 0,5 % Puffer) 2) 2x SL-Distanz (User-Ausnahme, 0,2 % Puffer).
            // Candle-Extreme (High bei Long, Low bei Short) sind der Preis-Proxy pro Candle —
            // so feuert der Trigger beim ersten Wick-Touch, konsistent zum Live-Tick-Verhalten.
            if (exitTracking.TryGetValue(key, out var exitState))
            {
                // Runner-Trailing (EnableRunner): Nach TP1 laeuft der Rest mit ATR-Chandelier-Trailing-Stop
                // statt festem TP2. Spiegelt die Live-Runner-Mechanik (TradingServiceBase) im Backtest,
                // damit "fester TP2 vs Trailing" empirisch A/B-getestet werden kann.
                if (exitState.RunnerActive)
                {
                    var trailMul = riskSettings?.RunnerTrailingAtrMultiplier ?? 2.0m;
                    var trailDist = exitState.RunnerAtrBase > 0m
                        ? exitState.RunnerAtrBase * trailMul
                        : exitState.EntryPrice * 0.01m;
                    decimal trailSl;
                    bool trailHit;
                    if (pos.Side == Side.Buy)
                    {
                        if (currentCandle.High > exitState.RunnerTrailAnchor) exitState.RunnerTrailAnchor = currentCandle.High;
                        trailSl = exitState.RunnerTrailAnchor - trailDist;
                        trailHit = currentCandle.Low <= trailSl;
                    }
                    else
                    {
                        if (exitState.RunnerTrailAnchor <= 0m || currentCandle.Low < exitState.RunnerTrailAnchor) exitState.RunnerTrailAnchor = currentCandle.Low;
                        trailSl = exitState.RunnerTrailAnchor + trailDist;
                        trailHit = currentCandle.High >= trailSl;
                    }
                    if (trailHit)
                    {
                        simExchange.SetCurrentPrice(symbol, trailSl);
                        await simExchange.ClosePositionAsync(symbol, pos.Side, isMakerClose: false).ConfigureAwait(false);
                        positionSignals.Remove(key);
                        exitTracking.Remove(key);
                        simExchange.SetCurrentPrice(symbol, currentCandle.Close);
                    }
                    continue; // Runner-Position hat eigene Exit-Logik — kein BE/TP1/Standard-Check.
                }

                if (!exitState.BreakevenSet && origSignal.StopLoss.HasValue)
                {
                    var currentPrice = pos.Side == Side.Buy ? currentCandle.High : currentCandle.Low;
                    var decision = BreakevenCalculator.Evaluate(
                        pos.Side, currentPrice, exitState.EntryPrice,
                        origSignal.StopLoss.Value, exitState.NavPointA, riskSettings?.BreakevenTriggerRMultiple ?? 2.0m);

                    if (decision.HasValue)
                    {
                        positionSignals[key] = positionSignals[key] with { StopLoss = decision.Value.NewStopLoss };
                        exitState.BreakevenSet = true;
                    }
                }

                // TP1-Check: Partial Close (50% bei 161.8% Extension)
                var tp1Hit = false;
                if (!exitState.PartialClosed && origSignal.TakeProfit.HasValue
                    && settings.Tp1CloseRatio > 0 && settings.Tp1CloseRatio < 1m)
                {
                    tp1Hit = pos.Side == Side.Buy
                        ? currentCandle.High >= origSignal.TakeProfit.Value
                        : currentCandle.Low <= origSignal.TakeProfit.Value;
                }

                if (tp1Hit)
                {
                    var closeQty = Math.Round(exitState.OriginalQuantity * settings.Tp1CloseRatio, 6);

                    // Min-Qty-Guard (GAP 2): Spiegelt SplitTpQuantity/MeetsMinimumOrder aus
                    // LiveTradingService.OrderPlacement.cs. Faellt die TP1-Teilmenge oder der TP2-Rest
                    // unter die Min-Order, kein Split → Full-TP bei TP1 (verhindert BingX-Reject auf
                    // winzige Teilmengen, z.B. ETH 0.01 / Min-Qty 0.01). Nur aktiv mit Provider.
                    var tp1Price = origSignal.TakeProfit!.Value;
                    var remainderQty = Math.Round(exitState.OriginalQuantity - closeQty, 6);
                    var foldToFullTp = symbolInfo != null && closeQty > 0 &&
                        (remainderQty <= 0m
                         || !symbolInfo.MeetsMinimumOrder(symbol, closeQty, tp1Price)
                         || !symbolInfo.MeetsMinimumOrder(symbol, remainderQty, tp1Price));

                    if (foldToFullTp)
                    {
                        // Full-TP: gesamte verbleibende Position bei TP1 schliessen, kein TP2-Bein.
                        simExchange.SetCurrentPrice(symbol, tp1Price);
                        await simExchange.ClosePositionAsync(symbol, pos.Side, isMakerClose: true).ConfigureAwait(false);
                        simExchange.SetCurrentPrice(symbol, currentCandle.Close);
                        positionSignals.Remove(key);
                        exitTracking.Remove(key);
                        continue;
                    }

                    if (closeQty > 0)
                    {
                        simExchange.SetCurrentPrice(symbol, tp1Price);
                        // TP1 = Limit-Reduce-Only auf der echten Exchange → MakerFee
                        await simExchange.ReducePositionAsync(symbol, pos.Side, closeQty, isMakerClose: true).ConfigureAwait(false);
                        simExchange.SetCurrentPrice(symbol, currentCandle.Close);
                    }
                    exitState.PartialClosed = true;
                    if (riskSettings?.EnableRunner == true && exitState.RunnerAtrBase > 0m)
                    {
                        // Trailing-Variante: Rest laeuft mit ATR-Trailing-Stop statt festem TP2.
                        // Start-Anker = TP1-Preis; kein festes TP-Ziel mehr (TakeProfit=null), SL bleibt.
                        exitState.RunnerActive = true;
                        exitState.RunnerTrailAnchor = origSignal.TakeProfit!.Value;
                        positionSignals[key] = origSignal with { TakeProfit = null };
                    }
                    else
                    {
                        // Baseline: TP2 (200%+Buffer) als neues Ziel, SL bleibt (Buch 4.3).
                        positionSignals[key] = origSignal with { TakeProfit = exitState.Tp2 };
                    }
                    continue;
                }

                // BUCH-ONLY: Kein Time-Exit.
            }

            // --- Standard SL/TP-Check (Fallback und finale Prüfung) ---
            // Auch bei Multi-Stage: prüft den aktuellen SL/TP (ggf. bereits auf BE/TP2 verschoben)
            var currentSignal = positionSignals[key]; // Kann durch Multi-Stage modifiziert sein
            var slHit = false;
            var tpHit = false;

            // Wenn beide (SL+TP) in einer Candle getroffen werden:
            // Candle-Richtung entscheidet welcher zuerst erreicht wurde.
            // Bullish Candle (Close>Open) → Preis ging zuerst hoch → TP bei Long wahrscheinlicher.
            // Bearish Candle (Close<Open) → Preis ging zuerst runter → SL bei Long wahrscheinlicher.
            if (pos.Side == Side.Buy)
            {
                var slTriggered = currentSignal.StopLoss.HasValue && currentCandle.Low <= currentSignal.StopLoss.Value;
                var tpTriggered = currentSignal.TakeProfit.HasValue && currentCandle.High >= currentSignal.TakeProfit.Value;
                if (slTriggered && tpTriggered)
                {
                    if (currentCandle.Close > currentCandle.Open)
                        tpHit = true; // Bullish → TP zuerst
                    else
                        slHit = true; // Bearish → SL zuerst
                }
                else if (slTriggered) slHit = true;
                else if (tpTriggered) tpHit = true;
            }
            else // Short
            {
                var slTriggered = currentSignal.StopLoss.HasValue && currentCandle.High >= currentSignal.StopLoss.Value;
                var tpTriggered = currentSignal.TakeProfit.HasValue && currentCandle.Low <= currentSignal.TakeProfit.Value;
                if (slTriggered && tpTriggered)
                {
                    if (currentCandle.Close < currentCandle.Open)
                        tpHit = true; // Bearish → TP zuerst für Short
                    else
                        slHit = true; // Bullish → SL zuerst für Short
                }
                else if (slTriggered) slHit = true;
                else if (tpTriggered) tpHit = true;
            }

            if (slHit)
            {
                // SL = StopMarket auf der echten Exchange → TakerFee.
                // ADVERSE-GAP-MODELL: Schiesst die Kerze durch den SL (Gap/Wick-Through), ist der
                // reale Fill schlechter als der SL-Preis. Frueher fuellte der Backtest exakt am SL →
                // er unterschaetzte SL-Verluste (zu optimistisch). Jetzt den Worst-Case der Kerze
                // nehmen: Long → tiefstes Low, Short → hoechstes High.
                var slFill = pos.Side == Side.Buy
                    ? Math.Min(currentSignal.StopLoss!.Value, currentCandle.Low)
                    : Math.Max(currentSignal.StopLoss!.Value, currentCandle.High);
                simExchange.SetCurrentPrice(symbol, slFill);
                await simExchange.ClosePositionAsync(symbol, pos.Side, isMakerClose: false).ConfigureAwait(false);
                positionSignals.Remove(key);
                exitTracking.Remove(key);
                simExchange.SetCurrentPrice(symbol, currentCandle.Close);
            }
            else if (tpHit)
            {
                // TP2 = Limit-Reduce-Only auf der echten Exchange → MakerFee
                simExchange.SetCurrentPrice(symbol, currentSignal.TakeProfit!.Value);
                await simExchange.ClosePositionAsync(symbol, pos.Side, isMakerClose: true).ConfigureAwait(false);
                positionSignals.Remove(key);
                exitTracking.Remove(key);
                simExchange.SetCurrentPrice(symbol, currentCandle.Close);
            }
        }
    }
}
