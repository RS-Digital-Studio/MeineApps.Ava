using BingXBot.Core.Enums;

namespace BingXBot.Core.Services;

/// <summary>
/// Zentrale Berechnung der Break-Even-Trigger fuer SK-System. Wird sowohl von LiveTradingService
/// (Tick-Loop, Live-Preis) als auch vom BacktestEngine (Candle-Extreme als Preis-Proxy) genutzt —
/// damit beide Pfade exakt dasselbe Verhalten haben.
///
/// Zwei Trigger, OR-verknuepft, mit je eigenem BE-Puffer:
/// <list type="bullet">
///   <item><b>A-Bruch (Buch Masterclass, Workflow 4.2):</b> Preis erreicht <c>NavPointA</c> der
///     aktivierenden Sequenz → SL zieht auf <c>Entry ± 0,5 %</c>.</item>
///   <item><b>2x SL-Distanz (User-Ausnahme):</b> Preis erreicht <c>Entry ± 2 × |Entry − SL|</c>
///     → SL zieht auf <c>Entry ± 0,2 %</c>. Nicht im SK-Buch, bewusst behalten weil der A-Bruch
///     ohne validen NavPointA (z.B. Legacy-Signale) nicht feuert und der Trade sonst keinen BE
///     bekommt, obwohl er schon doppelt so weit gelaufen ist.</item>
/// </list>
/// A-Bruch hat Prioritaet — wenn beide im selben Tick triggern, gewinnt der buchtreuere
/// 0,5 %-Puffer. Idempotenz (einmal pro Position) wird vom Aufrufer via BreakevenSet-Flag geregelt.
/// </summary>
public static class BreakevenCalculator
{
    /// <summary>BE-Puffer beim A-Bruch (Prozent des Entry-Preises, 0,5 %).</summary>
    public const decimal ABreakBufferPct = 0.005m;

    /// <summary>BE-Puffer beim 2x-SL-Distanz-Trigger (Prozent des Entry-Preises, 0,2 %).</summary>
    public const decimal TwoXSlBufferPct = 0.002m;

    /// <summary>
    /// Entscheidung eines BE-Triggers: neuer SL-Preis + menschlich lesbarer Trigger-Name fuer Logs.
    /// </summary>
    public readonly record struct BreakevenDecision(decimal NewStopLoss, string TriggerName);

    /// <summary>
    /// Prueft beide BE-Trigger gegen den aktuellen Preis.
    /// </summary>
    /// <param name="side">Position-Seite (Buy = Long, Sell = Short).</param>
    /// <param name="price">Aktueller Preis (Live-Tick oder Candle-Extreme im Backtest).</param>
    /// <param name="entryPrice">Tatsaechlicher Fill-Preis der Position (&gt; 0).</param>
    /// <param name="originalStopLoss">Original-SL der Position vor BE (&gt; 0). Bei Long &lt; Entry, bei Short &gt; Entry.</param>
    /// <param name="navPointA">Navigator-Point-A der Aktivierungs-Sequenz (0 = unbekannt → nur 2x-SL-Trigger aktiv).</param>
    /// <returns>
    /// <see cref="BreakevenDecision"/> wenn ein Trigger greift, sonst <c>null</c>.
    /// Bei gleichzeitigem Feuer gewinnt A-Bruch (bucheigene Prio, 0,5 %-Puffer).
    /// </returns>
    /// <param name="triggerRMultiple">SL-Distanz-Vielfaches fuer den (vormals fixen 2x-)BE-Trigger.
    /// Default 2.0. 1.5 ≈ BE wenn TP1-Level erreicht. &lt;= 0 deaktiviert den Distanz-Trigger
    /// (nur A-Bruch bleibt aktiv). Steuerbar via <see cref="BingXBot.Core.Configuration.RiskSettings.BreakevenTriggerRMultiple"/>.</param>
    public static BreakevenDecision? Evaluate(
        Side side,
        decimal price,
        decimal entryPrice,
        decimal originalStopLoss,
        decimal navPointA,
        decimal triggerRMultiple = 2.0m)
    {
        if (entryPrice <= 0m || originalStopLoss <= 0m || price <= 0m)
            return null;

        var isLong = side == Side.Buy;

        // Prio 1: A-Bruch (SK-Buch Masterclass, 0,5 % Puffer)
        if (navPointA > 0m)
        {
            var aBreak = isLong ? price >= navPointA : price <= navPointA;
            if (aBreak)
            {
                var sl = isLong
                    ? entryPrice * (1m + ABreakBufferPct)
                    : entryPrice * (1m - ABreakBufferPct);
                return new BreakevenDecision(sl, $"A-Bruch (A={navPointA:F8})");
            }
        }

        // Prio 2: N x SL-Distanz (konfigurierbar, 0,2 % Puffer). triggerRMultiple<=0 → Trigger aus.
        var slDistance = Math.Abs(entryPrice - originalStopLoss);
        if (triggerRMultiple > 0m && slDistance > 0m)
        {
            var nxTarget = isLong
                ? entryPrice + triggerRMultiple * slDistance
                : entryPrice - triggerRMultiple * slDistance;
            var nxHit = isLong ? price >= nxTarget : price <= nxTarget;
            if (nxHit)
            {
                var sl = isLong
                    ? entryPrice * (1m + TwoXSlBufferPct)
                    : entryPrice * (1m - TwoXSlBufferPct);
                return new BreakevenDecision(sl, $"{triggerRMultiple:0.##}x SL-Distanz (Ziel={nxTarget:F8})");
            }
        }

        return null;
    }
}
