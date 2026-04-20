using Google.AR.Core;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Android.Ar;

/// <summary>
/// Verwaltet ARCore-Anchors für alle Messpunkte. Anchors kompensieren die ARCore-Drift:
/// ohne Anchor bleiben gesetzte Punkte bei ihrer ursprünglichen Weltposition, aber ARCore
/// korrigiert seine Session-Pose über die Zeit → Punkte driften gegen die Realität.
/// Anchors werden von ARCore pro Frame auf die korrigierte Welt-Position getrackt.
///
/// Limit: ARCore erlaubt ~1000 Anchors pro Session, praktisch sollte man &lt;150 halten.
/// Für Garten-Vermessung mit &lt;50 Punkten absolut genug.
///
/// Lifecycle:
/// - TryCreateAnchor: bei Punkt-Set, speichert Anchor + bindet an ArPoint.AnchorId
/// - RefreshAnchors: pro Frame, aktualisiert ArPoint.X/Y/Z aus Anchor.Pose
/// - Detach: beim Session-Ende, gibt alle Anchors frei
/// </summary>
public sealed class ArAnchorManager : IDisposable
{
    private readonly Dictionary<string, Anchor> _anchors = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>Aktuelle Anzahl aktiver Anchors.</summary>
    public int Count { get { lock (_lock) return _anchors.Count; } }

    /// <summary>ARCore-Soft-Limit — bei Annäherung sollte Session abgeschlossen werden.</summary>
    public const int SoftLimit = 150;

    /// <summary>
    /// Erstellt Anchor an gegebener Pose und bindet ihn an den ArPoint.
    /// Wenn Limit erreicht oder Pose null → false, Punkt bleibt unankiert.
    /// </summary>
    public bool TryCreateAnchor(Session? session, Pose? pose, ArPoint point)
    {
        if (_disposed || session == null || pose == null) return false;

        lock (_lock)
        {
            if (_anchors.Count >= SoftLimit * 2) return false; // Hart-Limit
            try
            {
                var anchor = session.CreateAnchor(pose);
                if (anchor == null) return false;

                var id = Guid.NewGuid().ToString("N");
                _anchors[id] = anchor;
                point.AnchorId = id;
                return true;
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Warn("ArAnchorManager",
                    $"CreateAnchor fehlgeschlagen: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Erstellt einen Earth-Anchor an gegebener Geo-Position.
    /// Stabiler als Session-Anchor über Zeit/Sessions hinweg, weil ARCore-VPS
    /// die Welt-Position kontinuierlich mit Street-View-Daten abgleicht.
    /// Setzt voraus dass Earth.TrackingState=Tracking ist.
    /// </summary>
    public bool TryCreateEarthAnchor(Earth? earth, double latitude, double longitude,
        double altitude, ArPoint point)
    {
        if (_disposed || earth == null) return false;

        lock (_lock)
        {
            if (_anchors.Count >= SoftLimit * 2) return false;
            try
            {
                // Identity-Quaternion für Heading — wir orientieren Punkte nicht
                // am Himmel, nur die Position zählt.
                var anchor = earth.CreateAnchor(latitude, longitude, altitude,
                    0f, 0f, 0f, 1f);
                if (anchor == null) return false;

                var id = Guid.NewGuid().ToString("N");
                _anchors[id] = anchor;
                point.AnchorId = id;
                return true;
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Warn("ArAnchorManager",
                    $"CreateEarthAnchor fehlgeschlagen: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Liest für jeden ArPoint mit AnchorId die aktualisierte Anchor-Pose
    /// und schreibt sie in X/Y/Z zurück. Drift-Kompensation in-place.
    /// Punkte mit Anchor im TrackingState != Tracking bleiben unverändert.
    /// </summary>
    public void RefreshAnchors(IEnumerable<ArPoint> points)
    {
        if (_disposed) return;

        lock (_lock)
        {
            foreach (var point in points)
            {
                if (string.IsNullOrEmpty(point.AnchorId)) continue;
                if (!_anchors.TryGetValue(point.AnchorId, out var anchor)) continue;

                try
                {
                    if (anchor.TrackingState != TrackingState.Tracking) continue;
                    var pose = anchor.Pose;
                    if (pose == null) continue;

                    point.X = pose.Tx();
                    point.Y = pose.Ty();
                    point.Z = pose.Tz();
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Warn("ArAnchorManager",
                        $"RefreshAnchors: Anchor {point.AnchorId} Update fehlgeschlagen: {ex.Message}");
                }
            }
        }
    }

    /// <summary>Anzahl Anchors mit gültigem Tracking (für Quality-Indikator).</summary>
    public int CountTracking()
    {
        if (_disposed) return 0;
        var count = 0;
        lock (_lock)
        {
            foreach (var a in _anchors.Values)
            {
                try { if (a.TrackingState == TrackingState.Tracking) count++; }
                catch { /* Anchor invalid — skip */ }
            }
        }
        return count;
    }

    /// <summary>Einzelnen Anchor freigeben (z.B. nach Undo).</summary>
    public void Detach(string? anchorId)
    {
        if (string.IsNullOrEmpty(anchorId) || _disposed) return;
        lock (_lock)
        {
            if (_anchors.TryGetValue(anchorId, out var anchor))
            {
                try { anchor.Detach(); } catch { /* OK */ }
                _anchors.Remove(anchorId);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            foreach (var anchor in _anchors.Values)
            {
                try { anchor.Detach(); } catch { /* OK */ }
            }
            _anchors.Clear();
        }
    }
}

/// <summary>
/// Sammelt HitTest-Samples über mehrere Frames für Multi-Frame-Averaging.
/// Reduziert Hand-Wackler-Effekt beim Tap.
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
    /// Berechnet robusten Median aller Samples. Outlier-Filter: Samples die >3×StdDev
    /// vom ersten Median entfernt sind werden verworfen, dann erneut gemittelt.
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

/// <summary>
/// Stabilitäts-Sensor: monitort Gyroscope + Accelerometer und liefert einen Stability-Score.
/// 1.0 = vollkommen still, 0.0 = starke Bewegung.
/// </summary>
public sealed class ArStabilityMonitor : Java.Lang.Object,
    global::Android.Hardware.ISensorEventListener, IDisposable
{
    private readonly global::Android.Hardware.SensorManager? _sensorManager;
    private readonly global::Android.Hardware.Sensor? _gyroscope;
    private readonly global::Android.Hardware.Sensor? _accelerometer;

    // Gleitender Durchschnitt der angular velocity (rad/s)
    private float _recentAngularSpeed;
    private float _recentLinearAccel;
    private long _lastSensorTimeNs;

    /// <summary>0.0 (stark bewegt) bis 1.0 (vollkommen still).</summary>
    public float StabilityScore { get; private set; } = 1.0f;

    /// <summary>Ist das Gerät "still genug" für präzise Messung? Threshold = 0.6.</summary>
    public bool IsStable => StabilityScore >= 0.6f;

    public ArStabilityMonitor(global::Android.Content.Context context)
    {
        _sensorManager = context.GetSystemService(global::Android.Content.Context.SensorService)
            as global::Android.Hardware.SensorManager;
        _gyroscope = _sensorManager?.GetDefaultSensor(global::Android.Hardware.SensorType.Gyroscope);
        _accelerometer = _sensorManager?.GetDefaultSensor(global::Android.Hardware.SensorType.LinearAcceleration);
    }

    public void Start()
    {
        if (_sensorManager == null) return;
        if (_gyroscope != null)
            _sensorManager.RegisterListener(this, _gyroscope, global::Android.Hardware.SensorDelay.Game);
        if (_accelerometer != null)
            _sensorManager.RegisterListener(this, _accelerometer, global::Android.Hardware.SensorDelay.Game);
    }

    public void Stop()
    {
        _sensorManager?.UnregisterListener(this);
    }

    public void OnAccuracyChanged(global::Android.Hardware.Sensor? sensor,
        global::Android.Hardware.SensorStatus accuracy) { }

    public void OnSensorChanged(global::Android.Hardware.SensorEvent? e)
    {
        if (e?.Values == null) return;

        var values = e.Values;
        if (e.Sensor?.Type == global::Android.Hardware.SensorType.Gyroscope && values.Count >= 3)
        {
            // Magnitude der angular velocity (rad/s)
            var gx = values[0];
            var gy = values[1];
            var gz = values[2];
            var speed = MathF.Sqrt(gx * gx + gy * gy + gz * gz);

            // Exponential Moving Average (schneller Response)
            _recentAngularSpeed = _recentAngularSpeed * 0.7f + speed * 0.3f;
        }
        else if (e.Sensor?.Type == global::Android.Hardware.SensorType.LinearAcceleration && values.Count >= 3)
        {
            // Magnitude der linear acceleration (m/s²), bereits gravity-compensated
            var ax = values[0];
            var ay = values[1];
            var az = values[2];
            var accel = MathF.Sqrt(ax * ax + ay * ay + az * az);
            _recentLinearAccel = _recentLinearAccel * 0.7f + accel * 0.3f;
        }

        // Stability = 1.0 wenn beide Threshold-Werte unterschritten
        // Gyro-Threshold: 0.15 rad/s (~8.6°/s = normale Handbewegung)
        // Accel-Threshold: 0.8 m/s² (langsame Bewegungen)
        var gyroScore = MathF.Max(0f, 1f - _recentAngularSpeed / 0.5f);
        var accelScore = MathF.Max(0f, 1f - _recentLinearAccel / 2.0f);
        StabilityScore = MathF.Min(gyroScore, accelScore);
    }

    public new void Dispose()
    {
        Stop();
        base.Dispose();
    }
}
