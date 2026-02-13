using Android.App;
using Android.Content;
using Android.Hardware;
using ZeitManager.Services;

namespace ZeitManager.Android.Services;

/// <summary>
/// Android Shake-Erkennung via Accelerometer-Sensor.
/// Schwellwert: 12.0f, Cooldown: 500ms.
/// </summary>
public class AndroidShakeDetectionService : Java.Lang.Object, IShakeDetectionService, ISensorEventListener
{
    private SensorManager? _sensorManager;
    private Sensor? _accelerometer;
    private DateTime _lastShakeTime = DateTime.MinValue;
    private const float ShakeThreshold = 12.0f;
    private static readonly TimeSpan ShakeCooldown = TimeSpan.FromMilliseconds(500);

    public event EventHandler? ShakeDetected;

    public bool HasPhysicalSensor
    {
        get
        {
            EnsureSensor();
            return _accelerometer != null;
        }
    }

    public void StartListening()
    {
        EnsureSensor();
        if (_sensorManager != null && _accelerometer != null)
        {
            _sensorManager.RegisterListener(this, _accelerometer, SensorDelay.Game);
        }
    }

    public void StopListening()
    {
        _sensorManager?.UnregisterListener(this);
    }

    public void SimulateShake()
    {
        ShakeDetected?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureSensor()
    {
        if (_sensorManager != null) return;
        _sensorManager = (SensorManager?)Application.Context.GetSystemService(Context.SensorService);
        _accelerometer = _sensorManager?.GetDefaultSensor(SensorType.Accelerometer);
    }

    public void OnSensorChanged(SensorEvent? e)
    {
        if (e?.Values == null || e.Values.Count < 3) return;

        var x = e.Values[0];
        var y = e.Values[1];
        var z = e.Values[2];

        // Gravitationskraft abziehen und Gesamtbeschleunigung berechnen
        var acceleration = Math.Sqrt(x * x + y * y + z * z) - SensorManager.GravityEarth;

        if (acceleration > ShakeThreshold)
        {
            var now = DateTime.UtcNow;
            if (now - _lastShakeTime > ShakeCooldown)
            {
                _lastShakeTime = now;
                ShakeDetected?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy) { }
}
