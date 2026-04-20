using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Avalonia;
using Avalonia.Android;
using SmartMeasure.Android.Ar;
using SmartMeasure.Android.Services;
using SmartMeasure.Shared;
using SmartMeasure.Shared.Services;
using SmartMeasure.Shared.ViewModels;
using AndroidBleService = SmartMeasure.Android.Services.AndroidBleService;

namespace SmartMeasure.Android;

[Activity(
    Label = "SmartMeasure",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private const int BleRuntimePermissionRequestCode = 9001;

    private MainViewModel? _mainVm;
    private AndroidArCaptureService? _arCaptureService;
    private IBleService? _bleService;

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // IAppPaths MUSS vor dem DI-Build gesetzt werden — ProjectService-Ctor hängt davon ab.
        // Context.FilesDir garantiert sandbox-sicheren Pfad auf allen Android-ROMs.
        App.AppPathsFactory = () => new AndroidAppPaths(this);

        // BLE-Service für Verbindung zum Vermessungsstab
        App.BleServiceFactory = _ => new AndroidBleService(this);

        // AR-Capture-Service (ARCore)
        _arCaptureService = new AndroidArCaptureService(this);
        App.ArCaptureServiceFactory = _ => _arCaptureService;

        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _mainVm = App.Services?.GetService(typeof(MainViewModel)) as MainViewModel;
        _bleService = App.Services?.GetService(typeof(IBleService)) as IBleService;

        if (_mainVm != null)
        {
            _mainVm.ExitHintRequested += msg =>
                RunOnUiThread(() => Toast.MakeText(this, msg, ToastLength.Short)?.Show());

            // Error-UX: MessageRequested aus VM → Toast
            _mainVm.MessageRequested += (title, body) =>
                RunOnUiThread(() => Toast.MakeText(this, $"{title}: {body}", ToastLength.Long)?.Show());

            // Foreground-Service automatisch mit BLE-Status koppeln
            _mainVm.ForegroundServiceRequested += active =>
            {
                if (active) MeasurementForegroundService.Start(this);
                else MeasurementForegroundService.Stop(this);
            };
        }

        // Permissions proaktiv anfordern — User kann nicht scannen/verbinden ohne diese
        RequestBlePermissionsIfNeeded();
    }

    /// <summary>
    /// Android 12+ (API 31) verlangt BLUETOOTH_SCAN + BLUETOOTH_CONNECT als Runtime-Permissions.
    /// ACCESS_FINE_LOCATION ist für BLE-Scans auf Android 6-11 nötig (wir haben es als Fallback).
    /// CAMERA wird separat von ArCaptureActivity gehandhabt.
    /// </summary>
    private void RequestBlePermissionsIfNeeded()
    {
        var permissions = new List<string>();

        // Android 12+ (API 31): BLUETOOTH_SCAN + BLUETOOTH_CONNECT sind Runtime-Permissions
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            if (ContextCompat.CheckSelfPermission(this, global::Android.Manifest.Permission.BluetoothScan)
                != Permission.Granted)
                permissions.Add(global::Android.Manifest.Permission.BluetoothScan);

            if (ContextCompat.CheckSelfPermission(this, global::Android.Manifest.Permission.BluetoothConnect)
                != Permission.Granted)
                permissions.Add(global::Android.Manifest.Permission.BluetoothConnect);
        }

        // Location bleibt für Mapsui + AR-Georeferenzierung nötig, unabhängig von API-Level
        if (ContextCompat.CheckSelfPermission(this, global::Android.Manifest.Permission.AccessFineLocation)
            != Permission.Granted)
            permissions.Add(global::Android.Manifest.Permission.AccessFineLocation);

        if (permissions.Count == 0) return;

        ActivityCompat.RequestPermissions(this, permissions.ToArray(), BleRuntimePermissionRequestCode);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        _arCaptureService?.HandleActivityResult(requestCode, resultCode, data);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        // AR-Capture hat eigenes Permission-Handling (Camera)
        _arCaptureService?.HandlePermissionResult(requestCode, grantResults);

        if (requestCode == BleRuntimePermissionRequestCode)
        {
            var allGranted = grantResults.Length > 0 && grantResults.All(r => r == Permission.Granted);
            if (!allGranted)
            {
                RunOnUiThread(() => Toast.MakeText(this,
                    "Ohne Bluetooth-Berechtigung kann der Vermessungsstab nicht verbunden werden",
                    ToastLength.Long)?.Show());
            }
        }
    }

    public override void OnBackPressed()
    {
        if (_mainVm != null && _mainVm.HandleBackPressed()) return;
        base.OnBackPressed();
    }

    protected override void OnDestroy()
    {
        // FG-Service stoppen wenn Activity stirbt — sonst läuft er weiter ohne UI
        MeasurementForegroundService.Stop(this);
        base.OnDestroy();
    }
}
