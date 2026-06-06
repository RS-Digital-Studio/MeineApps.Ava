using Android.Content;
using Android.Hardware;
using Android.Runtime;
using Java.Lang;
using SunSeeker.Shared.Models;
using SunSeeker.Shared.Services;

namespace SunSeeker.Android.Services;

/// <summary>
/// Heading-Provider aus den nativen Bewegungssensoren. Nutzt <see cref="SensorType.RotationVector"/>
/// (Sensor-Fusion aus Accelerometer + Gyroskop + Magnetometer) für die Orientierung und
/// <see cref="SensorType.Gravity"/> ist implizit über den Rotationsvektor enthalten.
///
/// Konvention: Das Gerät wird flach an die geneigte Panel-Fläche gehalten. Die Geräte-Z-Achse
/// (Display-Normale) entspricht dann der Panel-Normale. Aus ihrer Welt-Orientierung (3. Spalte der
/// Rotationsmatrix; Welt = Ost/Nord/Hoch) folgt der Azimut (horizontale Projektion) und die Neigung
/// (Winkel gegen die Vertikale). Magnetischer Azimut wird per <see cref="GeomagneticField"/>
/// (Missweisung) auf geografisch Nord korrigiert.
///
/// Erbt von <see cref="Java.Lang.Object"/>, weil <see cref="ISensorEventListener"/> ein
/// Java-Interface ist.
/// </summary>
public sealed class AndroidHeadingService : Java.Lang.Object, IHeadingService, ISensorEventListener
{
    private readonly SensorManager? _sensorManager;
    private readonly Sensor? _rotationVector;
    private readonly float[] _rotationMatrix = new float[9];

    private double _latitude;
    private double _longitude;
    private double _altitude;
    private float _declination;
    private HeadingAccuracy _accuracy = HeadingAccuracy.Unreliable;
    private long _lastEmitTicks;

    public AndroidHeadingService(Context context)
    {
        _sensorManager = context.GetSystemService(Context.SensorService) as SensorManager;
        _rotationVector = _sensorManager?.GetDefaultSensor(SensorType.RotationVector);
    }

    public bool IsAvailable => _rotationVector != null;

    public HeadingReading Current { get; private set; }

    public event EventHandler<HeadingReading>? Changed;

    public void SetLocation(GeoLocation location)
    {
        _latitude = location.Latitude;
        _longitude = location.Longitude;
        _altitude = location.AltitudeMeters;
        UpdateDeclination();
    }

    public void Start()
    {
        if (_sensorManager == null || _rotationVector == null) return;
        _sensorManager.RegisterListener(this, _rotationVector, SensorDelay.Ui);
    }

    public void Stop() => _sensorManager?.UnregisterListener(this);

    public void OnSensorChanged(SensorEvent? e)
    {
        if (e?.Sensor == null || e.Values == null || e.Sensor.Type != SensorType.RotationVector)
            return;

        var vector = e.Values.ToArray();
        SensorManager.GetRotationMatrixFromVector(_rotationMatrix, vector);
        ComputeAndEmit();
    }

    public void OnAccuracyChanged(Sensor? sensor, [GeneratedEnum] SensorStatus accuracy)
    {
        if (sensor?.Type is SensorType.RotationVector or SensorType.MagneticField)
        {
            _accuracy = accuracy switch
            {
                SensorStatus.AccuracyHigh => HeadingAccuracy.High,
                SensorStatus.AccuracyMedium => HeadingAccuracy.Medium,
                SensorStatus.AccuracyLow => HeadingAccuracy.Low,
                _ => HeadingAccuracy.Unreliable,
            };
        }
    }

    private void ComputeAndEmit()
    {
        // Geräte-Z-Achse (Display-Normale) in Weltkoordinaten = 3. Spalte der Rotationsmatrix
        // (row-major 3x3). Welt: X = Ost, Y = Nord, Z = Hoch.
        float zx = _rotationMatrix[2];
        float zy = _rotationMatrix[5];
        float zz = _rotationMatrix[8];

        // Azimut der horizontalen Projektion (0 = Nord, 90 = Ost).
        var magneticAzimuth = System.Math.Atan2(zx, zy) * 180.0 / System.Math.PI;
        if (magneticAzimuth < 0) magneticAzimuth += 360.0;

        // Neigung der Fläche gegen die Horizontale: 0 = flach (Z senkrecht), 90 = senkrecht.
        var horizontal = System.Math.Sqrt(zx * zx + zy * zy);
        var tilt = System.Math.Atan2(horizontal, System.Math.Abs(zz)) * 180.0 / System.Math.PI;

        // Bei (fast) flacher Fläche ist die horizontale Projektion zu klein -> Azimut instabil.
        var reliable = horizontal > 0.12 && _accuracy >= HeadingAccuracy.Low;

        var trueAzimuth = magneticAzimuth + _declination;
        if (trueAzimuth < 0) trueAzimuth += 360.0;
        else if (trueAzimuth >= 360) trueAzimuth -= 360.0;

        Current = new HeadingReading(trueAzimuth, magneticAzimuth, _declination, tilt, reliable, _accuracy);

        // Auf ~20 Hz drosseln (Sensor liefert schneller, UI braucht das nicht).
        var now = JavaSystem.CurrentTimeMillis();
        if (now - _lastEmitTicks < 50) return;
        _lastEmitTicks = now;
        Changed?.Invoke(this, Current);
    }

    private void UpdateDeclination()
    {
        if (_latitude == 0 && _longitude == 0) return;
        try
        {
            var field = new GeomagneticField(
                (float)_latitude, (float)_longitude, (float)_altitude, JavaSystem.CurrentTimeMillis());
            _declination = field.Declination;
        }
        catch (System.Exception)
        {
            _declination = 0;
        }
    }
}
