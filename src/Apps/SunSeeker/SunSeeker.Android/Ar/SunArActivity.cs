using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.Content;
using MeineApps.Core.Ava.Localization;
using Microsoft.Extensions.DependencyInjection;
using SunSeeker.Shared;
using SunSeeker.Shared.Models;
using SunSeeker.Shared.Services;

namespace SunSeeker.Android.Ar;

/// <summary>
/// AR-Sonnenbahn-Overlay: blendet die Tagesbahn der Sonne + die aktuelle Sonnenposition über das
/// Live-Kamerabild ein. Kein ARCore-Tracking — die Sonnenrichtung kommt aus
/// <see cref="ISolarPositionService"/> (Ort + Zeit), die Bildposition aus dem Rotationsvektor-Sensor
/// (Kamera-Blickrichtung) + Missweisung. CameraX-Vorschau (Muster wie FitnessRechner-Barcode).
/// </summary>
[Activity(
    Theme = "@style/MyTheme.Fullscreen",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class SunArActivity : AndroidX.AppCompat.App.AppCompatActivity, ISensorEventListener
{
    private const int CameraPermissionRequestCode = 9201;

    private PreviewView? _previewView;
    private SunArOverlayView? _overlay;
    private SensorManager? _sensorManager;
    private Sensor? _rotationSensor;
    private System.Timers.Timer? _sunTimer;

    private GeoLocation _location = new(52.52, 13.405, 38);
    private float _declination;
    private ISolarPositionService? _solar;

    private readonly float[] _rotationMatrix = new float[9];
    private readonly float[] _remapped = new float[9];
    private readonly float[] _orientation = new float[3];

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var root = new FrameLayout(this)
        {
            LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent),
        };

        _previewView = new PreviewView(this)
        {
            LayoutParameters = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent),
        };
        root.AddView(_previewView);

        _overlay = new SunArOverlayView(this)
        {
            LayoutParameters = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent),
        };
        root.AddView(_overlay);

        AddCloseButton(root);
        SetContentView(root);

        ResolveData();
        _sensorManager = (SensorManager?)GetSystemService(SensorService);
        _rotationSensor = _sensorManager?.GetDefaultSensor(SensorType.RotationVector);

        if (ContextCompat.CheckSelfPermission(this, global::Android.Manifest.Permission.Camera) == Permission.Granted)
            StartCamera();
        else
            RequestPermissions([global::Android.Manifest.Permission.Camera], CameraPermissionRequestCode);
    }

    private void AddCloseButton(FrameLayout root)
    {
        var density = Resources!.DisplayMetrics!.Density;
        var close = new ImageButton(this);
        close.SetImageResource(global::Android.Resource.Drawable.IcMenuCloseClearCancel);
        close.SetBackgroundColor(Color.Transparent);
        close.SetColorFilter(Color.White);
        var lp = new FrameLayout.LayoutParams((int)(48 * density), (int)(48 * density))
        {
            Gravity = GravityFlags.Top | GravityFlags.Start,
            TopMargin = (int)(16 * density),
            LeftMargin = (int)(16 * density),
        };
        close.LayoutParameters = lp;
        close.Click += (_, _) => Finish();
        root.AddView(close);
    }

    /// <summary>Holt Ort + Sonnendaten aus dem Shared-DI und füllt das Overlay (Tagesbahn, Marker, Hinweis).</summary>
    private void ResolveData()
    {
        var services = App.Services;
        _solar = services?.GetService<ISolarPositionService>();
        var location = services?.GetService<ILocationService>()?.Current;
        if (location is { } loc) _location = loc;
        var localization = services?.GetService<ILocalizationService>();

        _declination = new GeomagneticField(
            (float)_location.Latitude, (float)_location.Longitude, (float)_location.AltitudeMeters,
            Java.Lang.JavaSystem.CurrentTimeMillis()).Declination;

        if (_solar is null || _overlay is null) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var arc = _solar.GetDayArc(_location, today)
            .Where(p => p.Elevation > -1)
            .Select(p => (p.Azimuth, p.Elevation))
            .ToList();
        _overlay.ArcPoints = arc;

        var times = _solar.GetSunTimes(_location, today);
        if (times.SunriseUtc is { } sr)
        {
            var pos = _solar.GetPosition(_location, sr);
            var label = $"{localization?.GetString("ArSunrise") ?? "Sunrise"} {sr.ToLocalTime():HH:mm}";
            _overlay.Sunrise = (pos.Azimuth, pos.Elevation, label);
        }
        if (times.SunsetUtc is { } ss)
        {
            var pos = _solar.GetPosition(_location, ss);
            var label = $"{localization?.GetString("ArSunset") ?? "Sunset"} {ss.ToLocalTime():HH:mm}";
            _overlay.Sunset = (pos.Azimuth, pos.Elevation, label);
        }

        _overlay.HintText = localization?.GetString("ArHint") ?? "Point the camera at the sky";
        UpdateCurrentSun();
    }

    private void UpdateCurrentSun()
    {
        if (_solar is null || _overlay is null) return;
        var sun = _solar.GetPosition(_location, DateTime.UtcNow);
        _overlay.CurrentSun = (sun.Azimuth, sun.Elevation);
        _overlay.PostInvalidate();
    }

    private void StartCamera()
    {
        try
        {
            var future = ProcessCameraProvider.GetInstance(this);
            future!.AddListener(new Java.Lang.Runnable(() =>
            {
                try
                {
                    var provider = (ProcessCameraProvider)future.Get()!;
                    var preview = new AndroidX.Camera.Core.Preview.Builder()!.Build()!;
                    var executor = ContextCompat.GetMainExecutor(this)!;
                    preview.SetSurfaceProvider(executor, _previewView!.SurfaceProvider);
                    provider.UnbindAll();
                    provider.BindToLifecycle(this, CameraSelector.DefaultBackCamera!, preview);
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Warn("SunAr", $"Kamera-Start fehlgeschlagen: {ex.Message}");
                }
            }), ContextCompat.GetMainExecutor(this)!);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("SunAr", $"CameraProvider fehlgeschlagen: {ex.Message}");
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == CameraPermissionRequestCode && grantResults.Length > 0 && grantResults[0] == Permission.Granted)
            StartCamera();
        else if (requestCode == CameraPermissionRequestCode)
            Finish();
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (_rotationSensor != null)
            _sensorManager?.RegisterListener(this, _rotationSensor, SensorDelay.Game);
        _sunTimer = new System.Timers.Timer(5000) { AutoReset = true };
        _sunTimer.Elapsed += (_, _) => RunOnUiThread(UpdateCurrentSun);
        _sunTimer.Start();
    }

    protected override void OnPause()
    {
        base.OnPause();
        _sensorManager?.UnregisterListener(this);
        _sunTimer?.Stop();
        _sunTimer?.Dispose();
        _sunTimer = null;
    }

    public void OnSensorChanged(SensorEvent? e)
    {
        if (e?.Sensor?.Type != SensorType.RotationVector || e.Values is null || _overlay is null) return;

        SensorManager.GetRotationMatrixFromVector(_rotationMatrix, [.. e.Values]);
        // Kamera zeigt nach vorn → Achsen für die AR-Blickrichtung remappen.
        SensorManager.RemapCoordinateSystem(_rotationMatrix, global::Android.Hardware.Axis.X, global::Android.Hardware.Axis.Z, _remapped);
        SensorManager.GetOrientation(_remapped, _orientation);

        var azimuth = _orientation[0] * 180.0 / Math.PI;  // Gier = Kamera-Himmelsrichtung (magnetisch)
        var pitch = _orientation[1] * 180.0 / Math.PI;
        var roll = _orientation[2] * 180.0 / Math.PI;

        _overlay.CameraAzimuth = Normalize360(azimuth + _declination); // magnetisch → geografisch (true north)
        _overlay.CameraElevation = -pitch;                              // Kamera nach oben → positive Elevation
        _overlay.CameraRoll = roll;
        _overlay.PostInvalidate();
    }

    public void OnAccuracyChanged(Sensor? sensor, [GeneratedEnum] SensorStatus accuracy) { }

    private static double Normalize360(double deg)
    {
        deg %= 360.0;
        return deg < 0 ? deg + 360.0 : deg;
    }
}
