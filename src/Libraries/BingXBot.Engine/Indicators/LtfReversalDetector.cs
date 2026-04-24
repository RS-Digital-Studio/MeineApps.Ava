using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// Task 4.3 — LTF-Reversal-Detector für konservativen Entry-Modus (SK-Buch Masterclass).
///
/// Buch-Zitat: "Konservativ (Reaktion abwarten): Du wartest, bis der Preis in die 50-66%
/// Box eintaucht. Erst wenn sich auf einer kleineren Zeiteinheit eine eigene kleine
/// 0-A-B-C Umkehrsequenz bildet oder eine starke Umkehrkerze (Pinbar/Engulfing)
/// auftritt, steigst du manuell ein."
///
/// Drei Reversal-Trigger:
/// 1. Micro-Sequence: Auf der Filter-TF (H4→H1, H1→M15, M15→M5) bildet sich eine
///    eigenständige aktivierte 0-A-B-C-Sequenz in die gewünschte Trade-Richtung.
/// 2. Pinbar: Untere Docht-Länge &gt;= 2× Body bei Long, Schlusskurs oberes Drittel.
/// 3. Engulfing: Aktuelle Kerze umschließt die vorherige vollständig (Body).
/// </summary>
public sealed record LtfReversalHit(
    LtfReversalType Type,
    string Reason);

public enum LtfReversalType
{
    None,
    Pinbar,
    Engulfing,
    MicroSequence
}

public static class LtfReversalDetector
{
    /// <summary>
    /// Prüft Filter-TF-Kerzen auf Reversal-Pattern. Gibt Hit zurück wenn mindestens ein Pattern trifft.
    /// </summary>
    /// <param name="filterCandles">Kerzen der Filter-TF (nächst-kleinere TF relativ zum Navigator).</param>
    /// <param name="bullish">True = Long-Reversal-Suche (Pinbar-Low, Engulfing-Bull), False = Short.</param>
    public static LtfReversalHit? Detect(IReadOnlyList<Candle>? filterCandles, bool bullish)
        => Detect(filterCandles, bullish, correctionBoxLower: null, correctionBoxUpper: null,
            enforceBoxClose: false, requirePinbarOrEngulfingOnly: false);

    /// <summary>
    /// Spec §4 + Strukturpunkte-Doku §5C — erweiterte Prüfung mit Box-Close- und Wick-Rejection-Pflicht.
    /// </summary>
    /// <param name="filterCandles">Kerzen der Filter-TF.</param>
    /// <param name="bullish">True = Long-Reversal-Suche, False = Short.</param>
    /// <param name="correctionBoxLower">
    /// Untere Kante der Korrekturzone (bei Long die 66.7%-Schwelle, bei Short die 50%-Schwelle).
    /// Nur ausgewertet wenn <paramref name="enforceBoxClose"/> true ist.
    /// </param>
    /// <param name="correctionBoxUpper">
    /// Obere Kante der Korrekturzone (bei Long die 50%-Schwelle, bei Short die 66.7%-Schwelle).
    /// </param>
    /// <param name="enforceBoxClose">
    /// Spec §4 Confirmation-Mode: Body der Trigger-Kerze muss in bzw. oberhalb (Long) / unterhalb (Short) der Box schließen.
    /// Docht darf rausstehen. Bei false wird keine Box-Regel geprüft.
    /// </param>
    /// <param name="requirePinbarOrEngulfingOnly">
    /// Strukturpunkte-Doku §5C: Wenn true gibt Micro-Sequence KEINEN Reversal-Hit mehr — nur Pinbar oder Engulfing
    /// gelten als "mathematisch nachweisbare Docht-Rejection" im Sinne der Spec.
    /// </param>
    public static LtfReversalHit? Detect(
        IReadOnlyList<Candle>? filterCandles,
        bool bullish,
        decimal? correctionBoxLower,
        decimal? correctionBoxUpper,
        bool enforceBoxClose,
        bool requirePinbarOrEngulfingOnly)
    {
        if (filterCandles == null || filterCandles.Count < 3) return null;

        var last = filterCandles[^1];
        var prev = filterCandles[^2];

        // Spec §4: Trigger-Kerzenkörper muss innerhalb bzw. oberhalb/unterhalb der Korrekturbox schließen.
        // Body-Grenze = min(Open,Close) bei Long (Body-Unterkante darf nicht unter Box-Unterkante),
        // max(Open,Close) bei Short (Body-Oberkante darf nicht über Box-Oberkante).
        // Der Docht darf rausstehen — das ist die ganze Idee der Rejection.
        if (enforceBoxClose)
        {
            if (bullish && correctionBoxLower.HasValue)
            {
                var bodyLow = Math.Min(last.Open, last.Close);
                if (bodyLow < correctionBoxLower.Value) return null;
            }
            if (!bullish && correctionBoxUpper.HasValue)
            {
                var bodyHigh = Math.Max(last.Open, last.Close);
                if (bodyHigh > correctionBoxUpper.Value) return null;
            }
        }

        // 1. Pinbar in der aktuellen Kerze
        if (CandlePatternDetector.IsPinbar(last, bullish))
            return new LtfReversalHit(LtfReversalType.Pinbar,
                bullish ? "Bullish Pinbar" : "Bearish Pinbar");

        // 2. Engulfing (aktuelle umschließt vorherige)
        if (CandlePatternDetector.IsEngulfing(last, prev, bullish))
            return new LtfReversalHit(LtfReversalType.Engulfing,
                bullish ? "Bullish Engulfing" : "Bearish Engulfing");

        // Strukturpunkte-Doku §5C: Wenn Pinbar/Engulfing verlangt ist, reicht Micro-Sequence NICHT — return null.
        if (requirePinbarOrEngulfingOnly) return null;

        // 3. Micro-Sequence: LTF-Sequenz in gewünschter Richtung aktiv
        if (filterCandles.Count >= 20)
        {
            // Strukturpunkte §3: BOS-Anker auch auf Filter-TF — sonst zählt der LTF-Reversal-Bonus ohne Strukturbruch.
            // Hardcoded `3` (statt aus ScannerSettings): LTF-Reversal blickt bewusst kurz zurück
            // (Micro-Reversal-Detektion, 20-Kerzen-Fenster). Ein User-konfigurierter Wert würde hier
            // nur Rauschen oder Look-Ahead-Probleme verursachen. ScannerSettings wird bewusst nicht
            // injiziert, um die Detector-Signatur klein zu halten (nur Candles+Parameter).
            var (primary, longM, shortM) = SequenceStateMachine.FromCandlesBoth(
                filterCandles, minImpulsePercent: 0.3m, correctionThreshold: 0.2m, minPoint0Candles: 2,
                bosAnchorSwingStrength: 3);
            var target = bullish ? longM : shortM;
            if (target.State >= SmState.Aktiviert)
                return new LtfReversalHit(LtfReversalType.MicroSequence,
                    bullish ? "LTF Long-Seq Aktiviert" : "LTF Short-Seq Aktiviert");
        }

        return null;
    }
}
