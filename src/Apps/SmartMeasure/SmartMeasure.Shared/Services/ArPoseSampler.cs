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
    /// Berechnet eine robuste Mittel-Position aller Samples:
    /// 1. Komponentenweiser Median (X, Y, Z) — ausreißer-resistenter Anker.
    /// 2. Outlier-Filter gegen den Median: Samples jenseits des 3-fachen RMS-Abstands raus.
    /// 3. Arithmetisches Mittel der gefilterten Samples (MLE-näher als der Median).
    /// 4. <b>Zweiter Filter-Pass gegen das Mittel</b> — fängt moderate Ausreißer, die beim
    ///    ersten Pass (RMS um den Median) noch durchrutschten und das Mittel zogen; danach
    ///    erneut mitteln. Ohne diesen Pass entkommen mehrere gleichseitige Ausreißer.
    ///
    /// <para>Hinweis zur Schwelle: <c>stdDev</c> ist hier der <b>RMS-Abstand</b> vom Zentrum
    /// (Streuradius), kein komponentenweises σ. Der Filter <c>d ≤ 3·RMS</c> ist für eng
    /// gestreute Cluster ausreichend streng und trennt grobe Fehlhits klar ab.</para>
    ///
    /// Liefert null wenn weniger als 3 Samples oder nach Filterung zu wenige übrig.
    /// </summary>
    public (float x, float y, float z, float stdDev, int validCount, int hitQuality)? ComputeRobustMedian()
    {
        if (_samples.Count < 3) return null;

        // Schritt 1: komponentenweiser Median als ausreißer-resistenter Anker
        var mx = Median(_samples.Select(s => s.X).ToArray());
        var my = Median(_samples.Select(s => s.Y).ToArray());
        var mz = Median(_samples.Select(s => s.Z).ToArray());

        // Schritt 2: Outlier gegen den Median filtern (3× RMS-Abstand)
        var kept = FilterWithinRms(_samples, mx, my, mz, 3f);
        if (kept.Count < 3) return null;

        // Schritt 3: arithmetisches Mittel der gefilterten Samples
        var (ax, ay, az) = Mean(kept);

        // Schritt 4: zweiter Filter-Pass gegen das Mittel (entfernt nachgezogene Ausreißer)
        var kept2 = FilterWithinRms(kept, ax, ay, az, 3f);
        if (kept2.Count >= 3)
        {
            kept = kept2;
            (ax, ay, az) = Mean(kept);
        }

        // Finaler RMS-Abstand um das Mittel
        var finalVar = kept.Average(s =>
            (s.X - ax) * (s.X - ax) + (s.Y - ay) * (s.Y - ay) + (s.Z - az) * (s.Z - az));
        var finalStdDev = MathF.Sqrt(finalVar);

        // Repräsentative Hit-Quality = Median der gefilterten Samples. NICHT das Maximum:
        // ein einzelner Plane-Hit unter lauter Instant-Placement-Samples würde sonst die
        // Confidence-Hit-Komponente hochziehen, obwohl die Position von schwachen Hits stammt.
        var hitQuality = MedianInt(kept.Select(s => s.HitQuality).ToArray());

        return (ax, ay, az, finalStdDev, kept.Count, hitQuality);
    }

    /// <summary>Behält die Samples, deren Abstand zum Zentrum (cx,cy,cz) ≤ <paramref name="factor"/>×RMS
    /// liegt. Bei RMS = 0 (alle identisch) bleiben alle erhalten.</summary>
    private static List<Sample> FilterWithinRms(List<Sample> samples, float cx, float cy, float cz, float factor)
    {
        var meanVar = samples.Average(s =>
            (s.X - cx) * (s.X - cx) + (s.Y - cy) * (s.Y - cy) + (s.Z - cz) * (s.Z - cz));
        var threshold = factor * MathF.Sqrt(meanVar);
        return samples
            .Where(s =>
            {
                var d = MathF.Sqrt((s.X - cx) * (s.X - cx) + (s.Y - cy) * (s.Y - cy) + (s.Z - cz) * (s.Z - cz));
                return d <= threshold;
            })
            .ToList();
    }

    private static (float x, float y, float z) Mean(List<Sample> samples)
        => (samples.Average(s => s.X), samples.Average(s => s.Y), samples.Average(s => s.Z));

    private static float Median(float[] values)
    {
        Array.Sort(values);
        var n = values.Length;
        return n % 2 == 0
            ? (values[n / 2 - 1] + values[n / 2]) / 2f
            : values[n / 2];
    }

    private static int MedianInt(int[] values)
    {
        Array.Sort(values);
        var n = values.Length;
        // Bei gerader Anzahl den unteren der beiden mittleren Werte nehmen (konservativ).
        return n % 2 == 0 ? values[n / 2 - 1] : values[n / 2];
    }
}
