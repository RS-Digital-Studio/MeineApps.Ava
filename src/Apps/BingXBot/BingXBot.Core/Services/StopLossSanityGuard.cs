using BingXBot.Core.Enums;

namespace BingXBot.Core.Services;

/// <summary>
/// Snapshot-Report-Fix Befund 3 / A0.6 — Sanity-Guard fuer alle SL-Adjustments.
///
/// Hintergrund: Im Pi-Snapshot vom 2026-05-17 hatte die DOGE-USDT-Long-Position einen Stop-Loss
/// von 0.10936 — der Entry-Preis war 0.10882. SL also <b>oberhalb</b> des Entry-Preises bei einer
/// Long-Position, ohne dass <see cref="Models.PositionExitState.BreakevenSet"/> oder
/// <see cref="Models.PositionExitState.PartialClosed"/> gesetzt war. Sobald der Markt das Level
/// kurz beruehrt haette, waere die Position mit echtem Verlust zugemacht worden.
///
/// Pure-Function-Validator: kein State, keine Async-IO — eignet sich fuer Unit-Tests + alle SL-Push-
/// Pfade (NativeSlTpManager, Recovery in <see cref="LiveTradingManager"/>, Partial-Close-Hook,
/// Reconcile-Re-Place). Liefert <see cref="SlSanityResult"/> mit klarem Reject-Grund — Caller logt
/// und ueberspringt den Push, statt den Bot in einen "ich liquidiere mich selbst"-State zu bringen.
/// </summary>
public static class StopLossSanityGuard
{
    /// <summary>
    /// Max. erlaubter Abstand des neuen SL ueber Entry (Long) bzw. unter Entry (Short), wenn
    /// Break-Even ODER PartialClose aktiv sind. 0.5 % deckt SK-Buch-BE (Entry × 1.0015, also 0.15 %)
    /// + Tick-Rounding-Padding ab. Werte darueber sind Trail-/Pyramid-Gewinnsicherung — die DUERFEN
    /// hoeher liegen, brauchen aber den BreakevenSet/PartialClosed/RunnerActive-Marker.
    /// </summary>
    public const decimal MaxBreakevenBufferPercent = 0.005m;

    /// <summary>
    /// Toleranzfenster fuer Floating-Point/Tick-Rounding bei "SL == Entry"-Vergleich.
    /// Schuetzt davor dass nach Tick-Floor/Ceil ein um 0.0001 % verschobener SL als "ueber Entry"
    /// abgelehnt wird, obwohl er gerade an der BE-Linie steht.
    /// </summary>
    public const decimal EqualityTolerancePercent = 0.00001m;

    /// <summary>
    /// Prueft ob der neue SL fuer eine Position akzeptiert werden darf.
    /// </summary>
    /// <param name="side">Position-Seite.</param>
    /// <param name="entryPrice">Original-Entry-Preis (muss &gt; 0 sein, sonst wird OK zurueckgegeben — kein Entry, kein Bezug).</param>
    /// <param name="newStopLoss">Geplanter neuer SL-Wert.</param>
    /// <param name="breakevenSet">Position ist im BE-State.</param>
    /// <param name="partialClosed">TP1 wurde erreicht und Teil geschlossen.</param>
    /// <param name="runnerActive">Runner-Trailing-Phase aktiv (SL darf in Gewinn nachgezogen werden).</param>
    public static SlSanityResult Validate(
        Side side,
        decimal entryPrice,
        decimal newStopLoss,
        bool breakevenSet = false,
        bool partialClosed = false,
        bool runnerActive = false)
    {
        if (newStopLoss <= 0m)
            return SlSanityResult.Reject($"newStopLoss <= 0 ({newStopLoss}) — Sentinel-Wert nicht erlaubt");

        if (entryPrice <= 0m)
            return SlSanityResult.Accept(); // Kein Entry-Preis bekannt — keine sinnvolle Pruefung moeglich.

        var equalityTol = entryPrice * EqualityTolerancePercent;
        var gainAllowed = breakevenSet || partialClosed || runnerActive;

        if (side == Side.Buy)
        {
            // Long: SL muss UNTER Entry liegen.
            if (newStopLoss <= entryPrice + equalityTol)
                return SlSanityResult.Accept();

            // SL strikt > Entry — nur erlaubt wenn Position bereits in Gewinn gesichert wird.
            if (!gainAllowed)
                return SlSanityResult.Reject(
                    $"Long-SL {newStopLoss} > Entry {entryPrice} ohne BreakevenSet/PartialClosed/RunnerActive — wuerde Position sofort im Verlust schliessen");

            // Auch im BE/Trail-Mode: harter Cap, sonst Bug der SL kilometerweit ueber Entry zieht.
            var maxAllowed = entryPrice * (1m + MaxBreakevenBufferPercent);
            if (newStopLoss > maxAllowed && !runnerActive)
                return SlSanityResult.Reject(
                    $"Long-SL {newStopLoss} > Entry × (1 + {MaxBreakevenBufferPercent:P2}) = {maxAllowed} (BE/Partial aktiv, Runner=false) — Buffer ueberschritten");

            return SlSanityResult.Accept();
        }

        // Short: SL muss UEBER Entry liegen.
        if (newStopLoss >= entryPrice - equalityTol)
            return SlSanityResult.Accept();

        if (!gainAllowed)
            return SlSanityResult.Reject(
                $"Short-SL {newStopLoss} < Entry {entryPrice} ohne BreakevenSet/PartialClosed/RunnerActive — wuerde Position sofort im Verlust schliessen");

        var minAllowed = entryPrice * (1m - MaxBreakevenBufferPercent);
        if (newStopLoss < minAllowed && !runnerActive)
            return SlSanityResult.Reject(
                $"Short-SL {newStopLoss} < Entry × (1 - {MaxBreakevenBufferPercent:P2}) = {minAllowed} (BE/Partial aktiv, Runner=false) — Buffer ueberschritten");

        return SlSanityResult.Accept();
    }
}

/// <summary>
/// Ergebnis einer SL-Sanity-Pruefung. <see cref="IsAcceptable"/>=false → SL-Push muss verweigert
/// werden, <see cref="RejectReason"/> enthaelt den menschlich lesbaren Grund fuer das Log.
/// </summary>
public sealed record SlSanityResult(bool IsAcceptable, string? RejectReason)
{
    public static SlSanityResult Accept() => new(true, null);
    public static SlSanityResult Reject(string reason) => new(false, reason);
}
