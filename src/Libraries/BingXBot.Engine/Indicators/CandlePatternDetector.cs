using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// Task 4.3 — Candle-Pattern-Detector für konservativen Entry (SK-Buch Masterclass).
/// Buch: "Eine starke Umkehrkerze (Pinbar/Engulfing) in der 50-78.6%-Zone → manueller Entry."
/// </summary>
public static class CandlePatternDetector
{
    /// <summary>
    /// Pinbar: Docht in die Gegenrichtung >= 2× Körper, Schlusskurs in Trend-Richtung.
    /// Long-Pinbar: Unterer Wick >= 2× Body, Close im oberen Drittel.
    /// Short-Pinbar: Oberer Wick >= 2× Body, Close im unteren Drittel.
    /// Doji (body=0) wird BEWUSST nicht akzeptiert — Pinbar ist die strikte Reversal-Definition
    /// fuer LtfReversalDetector (Trigger-Bestaetigung); die lockerere HammerOrPin-Erkennung in
    /// <c>SequenceDetector.DetectEntryConfirmation</c> (ohne Close-Lokalisations-Pflicht) deckt
    /// Doji-Hammer als Confluence-Bonus ab — gewuenschte Schwellen-Differenz, kein Bug.
    /// </summary>
    public static bool IsPinbar(Candle candle, bool bullish)
    {
        var body = Math.Abs(candle.Close - candle.Open);
        var totalRange = candle.High - candle.Low;
        if (totalRange <= 0 || body <= 0) return false;

        var upperWick = candle.High - Math.Max(candle.Open, candle.Close);
        var lowerWick = Math.Min(candle.Open, candle.Close) - candle.Low;

        if (bullish)
        {
            // Lower wick >= 2× body, close oberes Drittel
            var closeInUpperThird = (candle.Close - candle.Low) / totalRange >= 0.6m;
            return lowerWick >= 2m * body && closeInUpperThird;
        }
        else
        {
            // Upper wick >= 2× body, close unteres Drittel
            var closeInLowerThird = (candle.High - candle.Close) / totalRange >= 0.6m;
            return upperWick >= 2m * body && closeInLowerThird;
        }
    }

    /// <summary>
    /// Bullish Engulfing: Aktuelle grüne Kerze umschließt den Body der vorherigen roten Kerze.
    /// Bearish analog.
    /// </summary>
    public static bool IsEngulfing(Candle current, Candle previous, bool bullish)
    {
        if (bullish)
        {
            var prevBearish = previous.Close < previous.Open;
            var curBullish = current.Close > current.Open;
            return prevBearish && curBullish
                && current.Close > previous.Open
                && current.Open < previous.Close;
        }
        else
        {
            var prevBullish = previous.Close > previous.Open;
            var curBearish = current.Close < current.Open;
            return prevBullish && curBearish
                && current.Close < previous.Open
                && current.Open > previous.Close;
        }
    }
}
