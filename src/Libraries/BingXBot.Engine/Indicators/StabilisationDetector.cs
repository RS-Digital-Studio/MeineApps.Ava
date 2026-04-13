using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// SK-System Stabilisierungs-Erkennung: BB-Squeeze, Range-Kontraktion, Dreieck, Volumen-Trocknung.
/// Score 0-4 (Anzahl positiver Methoden). Ab Score 2 = valide Stabilisierung für Confluence-Bonus.
/// </summary>
public static class StabilisationDetector
{
    /// <summary>
    /// Erkennt Stabilisierung über 4 unabhängige Methoden.
    /// Score 0-4: Je mehr Methoden positiv, desto stärker die Stabilisierung.
    /// </summary>
    /// <param name="candles">Candle-Daten (mindestens lookback + 5 Kerzen).</param>
    /// <param name="lookback">Analysefenster (Default: 20 Kerzen).</param>
    /// <returns>Score 0-4 (Anzahl positiver Stabilisierungs-Signale).</returns>
    public static int DetectStabilisation(IReadOnlyList<Candle> candles, int lookback = 20)
    {
        if (candles.Count < lookback + 5) return 0;
        var recent = candles.Skip(candles.Count - lookback).ToList();
        int s = 0;

        // 1. BB-Squeeze: Bollinger-Bandbreite verengt sich (aktuelle Hälfte < 70% der vollen Periode)
        var bb20 = BBWidth(recent.Select(c => c.Close).ToList());
        var bb10 = BBWidth(recent.Skip(10).Select(c => c.Close).ToList());
        if (bb10 > 0 && bb20 > 0 && bb10 < bb20 * 0.7m) s++;

        // 2. Range-Kontraktion: Letzte 5 Kerzen haben < 60% der Range der vorherigen 5
        var rNew = recent.TakeLast(5).Average(c => (double)(c.High - c.Low));
        var rOld = recent.Skip(lookback - 10).Take(5).Average(c => (double)(c.High - c.Low));
        if (rOld > 0 && rNew < rOld * 0.6) s++;

        // 3. Dreieck: Konvergierende Highs (fallend) + Lows (steigend) in den letzten 10 Kerzen
        var l10 = recent.TakeLast(10).ToList();
        if (l10.Count >= 2)
        {
            var hF = l10.Zip(l10.Skip(1), (a, b) => b.High < a.High).Count(x => x);
            var lR = l10.Zip(l10.Skip(1), (a, b) => b.Low > a.Low).Count(x => x);
            if (hF >= 6 && lR >= 6) s++;
        }

        // 4. Volumen-Trocknung: Letzte 5 Kerzen haben < 50% des Volumens der vorherigen 5
        var vNew = recent.TakeLast(5).Average(c => (double)c.Volume);
        var vOld = recent.Skip(lookback - 10).Take(5).Average(c => (double)c.Volume);
        if (vOld > 0 && vNew < vOld * 0.5) s++;

        return s;
    }

    /// <summary>Bollinger-Bandbreite in % des Mittelwerts (2× Standardabweichung / Mittelwert × 100).</summary>
    private static decimal BBWidth(IList<decimal> c)
    {
        if (c.Count < 2) return 0;
        var a = c.Average();
        var sd = (decimal)Math.Sqrt(c.Average(x => (double)((x - a) * (x - a))));
        return a > 0 ? sd * 2 / a * 100 : 0;
    }
}
