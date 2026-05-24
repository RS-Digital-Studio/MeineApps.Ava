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

    /// <summary>Detacht alle Anchors deren TrackingState <see cref="TrackingState.Stopped"/> ist.
    /// ARCore beendet ein Tracking dauerhaft (Stopped) wenn der zugehörige Trackable verloren ging.
    /// Solche Anchors halten nur noch native Ressourcen und sollten freigegeben werden, sonst
    /// wächst die Liste bei flackerndem Tracking unbegrenzt bis zum Hard-Limit.</summary>
    /// <returns>Anzahl freigegebener Anchors.</returns>
    public int PruneStopped()
    {
        if (_disposed) return 0;
        var pruned = 0;
        lock (_lock)
        {
            var toRemove = new List<string>();
            foreach (var (id, anchor) in _anchors)
            {
                try
                {
                    if (anchor.TrackingState == TrackingState.Stopped)
                        toRemove.Add(id);
                }
                catch { toRemove.Add(id); }
            }
            foreach (var id in toRemove)
            {
                if (_anchors.TryGetValue(id, out var anchor))
                {
                    try { anchor.Detach(); } catch { /* OK */ }
                    _anchors.Remove(id);
                    pruned++;
                }
            }
        }
        return pruned;
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

// ArPoseSampler lebt jetzt in SmartMeasure.Shared.Services (war reine Math-Klasse) —
// erlaubt Unit-Tests direkt auf der Klasse.

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
    private readonly object _emaLock = new();

    // Gleitender Durchschnitt der angular velocity (rad/s) / linear accel (m/s²).
    // Sensor-Thread schreibt unter _emaLock; Reader lesen StabilityScore (volatile).
    private float _recentAngularSpeed;
    private float _recentLinearAccel;
    private volatile bool _disposed;

    // Volatile-int-Bits eines float — sicherer Cross-Thread-Read ohne torn-read.
    private int _stabilityScoreBits = ToBits(1.0f);

    /// <summary>0.0 (stark bewegt) bis 1.0 (vollkommen still).</summary>
    public float StabilityScore
    {
        get => FromBits(System.Threading.Volatile.Read(ref _stabilityScoreBits));
        private set => System.Threading.Volatile.Write(ref _stabilityScoreBits, ToBits(value));
    }

    private static int ToBits(float f) => BitConverter.SingleToInt32Bits(f);
    private static float FromBits(int b) => BitConverter.Int32BitsToSingle(b);

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
        if (e?.Values == null || _disposed) return;

        float angular, accel;
        lock (_emaLock)
        {
            var values = e.Values;
            if (e.Sensor?.Type == global::Android.Hardware.SensorType.Gyroscope && values.Count >= 3)
            {
                // Magnitude der angular velocity (rad/s), EMA (schneller Response)
                var gx = values[0];
                var gy = values[1];
                var gz = values[2];
                var speed = MathF.Sqrt(gx * gx + gy * gy + gz * gz);
                _recentAngularSpeed = _recentAngularSpeed * 0.7f + speed * 0.3f;
            }
            else if (e.Sensor?.Type == global::Android.Hardware.SensorType.LinearAcceleration && values.Count >= 3)
            {
                // Magnitude der linear acceleration (m/s²), bereits gravity-compensated
                var ax = values[0];
                var ay = values[1];
                var az = values[2];
                var a = MathF.Sqrt(ax * ax + ay * ay + az * az);
                _recentLinearAccel = _recentLinearAccel * 0.7f + a * 0.3f;
            }
            angular = _recentAngularSpeed;
            accel = _recentLinearAccel;
        }

        // Stability = 1.0 wenn beide Threshold-Werte unterschritten
        // Gyro-Threshold: 0.5 rad/s (~28°/s), Accel-Threshold: 2.0 m/s²
        var gyroScore = MathF.Max(0f, 1f - angular / 0.5f);
        var accelScore = MathF.Max(0f, 1f - accel / 2.0f);
        StabilityScore = MathF.Min(gyroScore, accelScore);
    }

    public new void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        base.Dispose();
    }
}
