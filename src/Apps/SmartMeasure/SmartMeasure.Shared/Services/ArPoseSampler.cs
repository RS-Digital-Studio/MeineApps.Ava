namespace SmartMeasure.Shared.Services;

/// <summary>
/// Sammelt HitTest-Samples über mehrere Frames für Multi-Frame-Averaging.
/// Reduziert Hand-Wackler-Effekt beim Tap.
///
/// Liegt in <see cref="SmartMeasure.Shared.Services"/> (kein Android-API noetig),
/// damit Unit-Tests die Klasse direkt referenzieren koennen statt eine Mini-Kopie
/// zu pflegen.
/// </summary>
public sealed class ArPoseSampler
{
    public readonly struct Sample
    {
        public readonly float X, Y, Z;
        public readonly int HitQuality;
        public Sample(float x, float y, float z, int hq) { X = x; Y = y; Z = z; HitQuality = hq; }
    }

    private readonly List<Sample> _samples = [];
    public int Count => _samples.Count;

    public void Add(float x, float y, float z, int hitQuality)
    {
        _samples.Add(new Sample(x, y, z, hitQuality));
    }

    public void Clear() => _samples.Clear();

    /// <summary>
    /// Berechnet eine robuste Mittel-Position aller Samples in zwei Schritten:
    /// 1. Komponentenweiser Median (X, Y, Z) — unempfindlich gegen Ausreißer
    /// 2. Outlier-Filter: Samples die &gt;3×StdDev vom Median entfernt sind werden verworfen
    /// 3. Auf den gefilterten Samples wird das arithmetische Mittel berechnet
    ///
    /// Median im Schritt 1 + Mittel im Schritt 3 ist bewusst — der Median liefert
    /// einen Ausreißer-resistenten Ankerpunkt, das anschließende Mittel auf bereinigten
    /// Samples reduziert die Restvarianz (Median ist nicht der MLE für Gauß-Verteilungen,
    /// das Mittel auf gefilterten Daten schon).
    ///
    /// Liefert null wenn weniger als 3 Samples oder alle zu divergent.
    /// </summary>
    public (float x, float y, float z, float stdDev, int validCount, int maxHitQuality)? ComputeRobustMedian()
    {
        if (_samples.Count < 3) return null;

        // Median berechnen
        var mx = Median(_samples.Select(s => s.X).ToArray());
        var my = Median(_samples.Select(s => s.Y).ToArray());
        var mz = Median(_samples.Select(s => s.Z).ToArray());

        // StdDev vom Median
        var variances = _samples
            .Select(s => (s.X - mx) * (s.X - mx) + (s.Y - my) * (s.Y - my) + (s.Z - mz) * (s.Z - mz))
            .ToArray();
        var meanVar = variances.Average();
        var stdDev = MathF.Sqrt(meanVar);

        // Outlier filtern: >3σ entfernt → raus
        var threshold = 3f * stdDev;
        var kept = _samples
            .Where(s =>
            {
                var d = MathF.Sqrt((s.X - mx) * (s.X - mx) + (s.Y - my) * (s.Y - my) + (s.Z - mz) * (s.Z - mz));
                return d <= threshold;
            })
            .ToList();

        if (kept.Count < 3) return null;

        // Erneut mitteln (arithmetisches Mittel über gefilterte Samples)
        var finalX = kept.Average(s => s.X);
        var finalY = kept.Average(s => s.Y);
        var finalZ = kept.Average(s => s.Z);

        // Finales StdDev
        var finalVar = kept.Average(s =>
            (s.X - finalX) * (s.X - finalX) +
            (s.Y - finalY) * (s.Y - finalY) +
            (s.Z - finalZ) * (s.Z - finalZ));
        var finalStdDev = MathF.Sqrt(finalVar);

        var maxQuality = kept.Max(s => s.HitQuality);

        return (finalX, finalY, finalZ, finalStdDev, kept.Count, maxQuality);
    }

    private static float Median(float[] values)
    {
        Array.Sort(values);
        var n = values.Length;
        return n % 2 == 0
            ? (values[n / 2 - 1] + values[n / 2]) / 2f
            : values[n / 2];
    }
}
