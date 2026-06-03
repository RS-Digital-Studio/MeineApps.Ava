using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Avalonia.Android;
using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.DependencyInjection;
using SmartMeasure.Android.Ar;
using SmartMeasure.Android.Services;
using SmartMeasure.Shared;
using SmartMeasure.Shared.Services;
using SmartMeasure.Shared.ViewModels;
using AndroidBleService = SmartMeasure.Android.Services.AndroidBleService;
using AndroidUri = global::Android.Net.Uri;

namespace SmartMeasure.Android;

[Activity(
    Label = "SmartMeasure",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    private const int BleRuntimePermissionRequestCode = 9001;

    private MainViewModel? _mainVm;
    private AndroidArCaptureService? _arCaptureService;
    private IBleService? _bleService;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Avalonia 12 Android: OnFrameworkInitializationCompleted (DI-Build) laeuft bereits in
        // AvaloniaAndroidApplication.OnCreate (Application-Ebene) — also VOR diesem
        // MainActivity.OnCreate. Die Platform-Factories MUESSEN daher hier vor base.OnCreate
        // gesetzt werden: base.OnCreate (unten) ruft via AvaloniaActivity.InitializeAvaloniaView
        // die App.MainViewFactory auf, und ERST dort loest App.axaml.cs das MainViewModel auf.
        // Zusammen mit der Lazy-Service-Registrierung in App.axaml.cs liegt der erste Resolve
        // damit deterministisch NACH dieser Factory-Setzung → echte Android-Services statt
        // Mock-Fallbacks. Frueher lief dieser Code in CustomizeAppBuilder.

        // IAppPaths MUSS vor dem DI-Build gesetzt werden — ProjectService-Ctor hängt davon ab.
        // Context.FilesDir garantiert sandbox-sicheren Pfad auf allen Android-ROMs.
        App.AppPathsFactory = () => new AndroidAppPaths(this);

        // BLE-Service für Verbindung zum Vermessungsstab.
        // IGeoidService wird nach DI-Build aufgelöst — rechnet Ellipsoid → Geoid-Höhe (NN).
        App.BleServiceFactory = sp => new AndroidBleService(this, sp.GetRequiredService<IGeoidService>());

        // Share-Sheet + Öffnen via FileProvider (Authority: ${applicationId}.fileprovider)
        UriLauncher.PlatformShareFile = ShareFileViaIntent;
        UriLauncher.PlatformOpenFile = OpenFileViaIntent;

        // AR-Capture-Service (ARCore)
        _arCaptureService = new AndroidArCaptureService(this);
        App.ArCaptureServiceFactory = _ => _arCaptureService;

        // Plan-Kap. 5.12: Voice-Annotation via Android SpeechRecognizer
        App.VoiceAnnotationServiceFactory = _ => new AndroidVoiceAnnotationService(this);

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

    // OnBackPressed ist ab API 33 deprecated (OnBackInvokedCallback), wird aber von Avalonias
    // Single-Activity-Modell weiterhin genutzt — ein API-33-Umstieg waere ein eigenes Feature.
#pragma warning disable CA1422
    public override void OnBackPressed()
    {
        if (_mainVm != null && _mainVm.HandleBackPressed()) return;
        base.OnBackPressed();
    }
#pragma warning restore CA1422

    protected override void OnDestroy()
    {
        // FG-Service stoppen wenn Activity stirbt — sonst läuft er weiter ohne UI
        MeasurementForegroundService.Stop(this);
        base.OnDestroy();
    }

    /// <summary>Share-Sheet mit Intent.ActionSend. Braucht FileProvider + grantUriPermissions.</summary>
    private void ShareFileViaIntent(string filePath, string mimeType, string? title)
    {
        try
        {
            if (!System.IO.File.Exists(filePath))
            {
                RunOnUiThread(() => Toast.MakeText(this, $"Datei nicht gefunden: {filePath}",
                    ToastLength.Short)?.Show());
                return;
            }

            var file = new Java.IO.File(filePath);
            var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                this, PackageName + ".fileprovider", file);

            var intent = new Intent(Intent.ActionSend);
            intent.SetType(mimeType);
            intent.PutExtra(Intent.ExtraStream, uri);
            if (!string.IsNullOrEmpty(title))
                intent.PutExtra(Intent.ExtraSubject, title);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission);

            StartActivity(Intent.CreateChooser(intent, title ?? "Teilen"));
        }
        catch (Exception ex)
        {
            RunOnUiThread(() => Toast.MakeText(this, $"Share fehlgeschlagen: {ex.Message}",
                ToastLength.Long)?.Show());
        }
    }

    /// <summary>Datei mit Standard-Handler öffnen (z.B. PDF-Reader für .pdf).</summary>
    private void OpenFileViaIntent(string filePath, string mimeType)
    {
        try
        {
            if (!System.IO.File.Exists(filePath))
            {
                RunOnUiThread(() => Toast.MakeText(this, $"Datei nicht gefunden: {filePath}",
                    ToastLength.Short)?.Show());
                return;
            }

            var file = new Java.IO.File(filePath);
            var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                this, PackageName + ".fileprovider", file);

            var intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(uri, mimeType);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);

            StartActivity(intent);
        }
        catch (global::Android.Content.ActivityNotFoundException)
        {
            RunOnUiThread(() => Toast.MakeText(this,
                "Keine App zum Öffnen dieses Dateityps installiert", ToastLength.Long)?.Show());
        }
        catch (Exception ex)
        {
            RunOnUiThread(() => Toast.MakeText(this, $"Öffnen fehlgeschlagen: {ex.Message}",
                ToastLength.Long)?.Show());
        }
    }
}
