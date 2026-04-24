using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// Task 1.1 — Multi-TF GKL-Detector (Königsdisziplin, SK-Buch Masterclass).
///
/// Buch-Zitat: "Das Gesamtkorrekturlevel (GKL) ist die unangefochtene 'Königsdisziplin' im SK-System.
/// Es ist oft das mächtigste, stabilste und am stärksten verteidigte Level im gesamten Chartbild.
/// Das GKL (50% - 66.7% Zone der gigantischen Gesamtbewegung) ist der Bereich, in dem das 'große Geld'
/// (Institutionen, Banken, Hedgefonds) oft wieder massiv in den übergeordneten Trend einsteigt."
///
/// Im Unterschied zu <see cref="SequenceDetector.CalculateGKL"/> prüft dieser Detektor nicht nur
/// abgearbeitete Sequenzen, sondern auch LAUFENDE HTF-Sequenzen (W1 und D1), in deren
/// Korrekturzone der aktuelle Preis liegt. Das ist das wertvollste SK-Setup laut Buch.
/// </summary>
public sealed record GklHit(
    TimeFrame Tf,
    decimal Gkl500,
    decimal Gkl618,
    decimal Gkl667,
    bool IsUptrend,
    decimal SourcePoint0,
    decimal SourceEnd);

public static class MultiTfGklDetector
{
    /// <summary>
    /// Prüft ob <paramref name="currentPrice"/> in der 50-66.7%-Korrektur einer W1- oder D1-Sequenz liegt.
    /// Bevorzugt W1 (Master-Zone), fallback auf D1.
    /// </summary>
    /// <param name="weekly">W1-Kerzen (ideal 52+ Kerzen für stabile Swing-Erkennung).</param>
    /// <param name="daily">D1-Kerzen (ideal 120+ Kerzen).</param>
    public static GklHit? Detect(decimal currentPrice, IReadOnlyList<Candle>? weekly, IReadOnlyList<Candle>? daily, bool requireMatchDirection = true, bool? preferredLong = null)
    {
        var w1Hit = TryDetectOnTf(currentPrice, weekly, TimeFrame.W1, swingStrength: 5);
        if (w1Hit != null && (!requireMatchDirection || preferredLong == null || w1Hit.IsUptrend == preferredLong))
            return w1Hit;

        var d1Hit = TryDetectOnTf(currentPrice, daily, TimeFrame.D1, swingStrength: 7);
        if (d1Hit != null && (!requireMatchDirection || preferredLong == null || d1Hit.IsUptrend == preferredLong))
            return d1Hit;

        return null;
    }

    /// <summary>
    /// Task 1.1 — Direkter Single-TF-Check. Nützlich für Confluence-Scoring wenn nur W1 oder nur D1 relevant ist.
    /// </summary>
    public static GklHit? TryDetectOnTf(decimal currentPrice, IReadOnlyList<Candle>? candles, TimeFrame tf, int swingStrength = 7)
    {
        if (candles == null || candles.Count < swingStrength * 2 + 10) return null;

        var gkl = SequenceDetector.CalculateGKL(candles, swingStrength);
        if (gkl == null) return null;

        var (gkl500, _, gkl667, isUptrend, swingHigh, swingLow) = gkl.Value;

        if (!SequenceDetector.IsInGKL(currentPrice, gkl500, gkl667))
            return null;

        // 61.8% interpoliert (ideal zwischen 50 und 66.7%)
        var gkl618 = gkl500 + (gkl667 - gkl500) * 0.356m; // 50→66.7 linear mapping: 0.356 ≈ (0.618-0.500)/(0.667-0.500)
        var sourceP0 = isUptrend ? swingLow : swingHigh;
        var sourceEnd = isUptrend ? swingHigh : swingLow;

        return new GklHit(tf, gkl500, gkl618, gkl667, isUptrend, sourceP0, sourceEnd);
    }
}
