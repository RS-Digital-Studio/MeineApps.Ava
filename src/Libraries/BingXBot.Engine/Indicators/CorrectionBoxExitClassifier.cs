using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// Task 4.2 — Schlusskurs-Regel für Korrekturbox-Exits (SK-Buch Masterclass).
///
/// Buch-Zitat: "Es ist völlig normal (und oft ein gutes Zeichen), wenn eine Kerze mit ihrem
/// Docht tief in die Korrekturbox einsticht oder sogar kurzfristig durch sie hindurch piekst.
/// Wichtig ist der Schlusskurs: Solange der Kerzenkörper innerhalb oder oberhalb der Box
/// schließt, ist das Setup valide. Schließt eine starke Kerze weit unterhalb (im Aufwärtstrend)
/// der Box, ist Vorsicht geboten."
///
/// Drei Exit-Klassen:
/// - <see cref="CorrectionBoxExit.WickOnly"/>: Docht bricht 78.6% oder Point 0, Body schließt in Box → Sequenz bleibt aktiv
/// - <see cref="CorrectionBoxExit.StrongClose"/>: Body-Close deutlich unterhalb der Box (>0.2%) → Reset empfohlen
/// - <see cref="CorrectionBoxExit.FullInvalidation"/>: Body-Close unter Point 0 → strikte Invalidierung
/// - <see cref="CorrectionBoxExit.InBox"/>: Alles normal, Preis innerhalb Korrekturbox
/// </summary>
public enum CorrectionBoxExit
{
    /// <summary>Preis innerhalb der Korrekturbox (50-78.6%) — Sequenz nominal aktiv.</summary>
    InBox,
    /// <summary>Wick-Pike unter Box oder Point 0, aber Body-Close oberhalb der Box-Untergrenze → weiter valide.</summary>
    WickOnly,
    /// <summary>Body-Close deutlich unter Box (>0.2%) — Vorsicht, Sequenz sollte zurückgesetzt werden.</summary>
    StrongClose,
    /// <summary>Body-Close unter Point 0 — strikte Invalidierung.</summary>
    FullInvalidation
}

public static class CorrectionBoxExitClassifier
{
    /// <summary>
    /// Klassifiziert die aktuelle Kerze relativ zur Korrekturbox (Ret500 → Ret786) und Point 0.
    /// </summary>
    /// <param name="isLong">True = Long-Sequenz (Low-Pike ist riskant), False = Short.</param>
    /// <param name="candle">Aktuelle Kerze (Body + Wicks).</param>
    /// <param name="boxUpper">Obere Grenze der Korrekturbox (Long: Ret500, Short: Ret500).</param>
    /// <param name="boxLower">Untere Grenze der Korrekturbox (Long: Ret786, Short: Ret786).</param>
    /// <param name="point0">Point 0 der Sequenz (absolute Verteidigung).</param>
    /// <param name="strongCloseThreshold">Prozent-Schwelle für StrongClose unter Box (Default 0.002 = 0.2%).</param>
    public static CorrectionBoxExit Classify(
        bool isLong,
        Candle candle,
        decimal boxUpper,
        decimal boxLower,
        decimal point0,
        decimal strongCloseThreshold = 0.002m)
    {
        // Box wird so umformuliert, dass "innen" = zwischen min und max liegt.
        var boxMin = Math.Min(boxUpper, boxLower);
        var boxMax = Math.Max(boxUpper, boxLower);

        // Long-Sicht: Preis unter Box = riskant. Short-Sicht: über Box = riskant.
        // close: Body-Schlusskurs; critical = Docht-Extrem das die Box-Grenze bricht.
        var close = candle.Close;
        var critical = isLong ? candle.Low : candle.High;

        // 1. Body unter/über Point 0 = strikte Invalidierung
        if (isLong && close < point0) return CorrectionBoxExit.FullInvalidation;
        if (!isLong && close > point0) return CorrectionBoxExit.FullInvalidation;

        // 2. Body "deutlich" jenseits der Box (0.2% = StrongClose)
        if (isLong)
        {
            var threshold = boxMin * (1m - strongCloseThreshold);
            if (close < threshold) return CorrectionBoxExit.StrongClose;
        }
        else
        {
            var threshold = boxMax * (1m + strongCloseThreshold);
            if (close > threshold) return CorrectionBoxExit.StrongClose;
        }

        // 3. Nur Docht jenseits der Box, Body innerhalb oder jenseits aber nicht stark → WickOnly
        if (isLong && critical < boxMin && close >= boxMin * (1m - strongCloseThreshold))
            return CorrectionBoxExit.WickOnly;
        if (!isLong && critical > boxMax && close <= boxMax * (1m + strongCloseThreshold))
            return CorrectionBoxExit.WickOnly;

        // 4. Sonst: im Box-Bereich
        return CorrectionBoxExit.InBox;
    }
}
