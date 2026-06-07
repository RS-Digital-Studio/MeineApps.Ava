using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using SunSeeker.Android.Ar;
using SunSeeker.Android.Services;
using SunSeeker.Shared;
using SunSeeker.Shared.ViewModels;

namespace SunSeeker.Android;

[Activity(
    Label = "SunSeeker",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    private const int LocationPermissionRequestCode = 9101;

    private AndroidLocationService? _locationService;
    private AndroidHeadingService? _headingService;
    private MainViewModel? _mainVm;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Platform-Factories MÜSSEN vor base.OnCreate gesetzt werden (Avalonia-12-Reihenfolge:
        // base.OnCreate löst via MainViewFactory das MainViewModel auf, das die Services zieht).
        _locationService = new AndroidLocationService(this);
        _headingService = new AndroidHeadingService(this);
        App.LocationServiceFactory = _ => _locationService;
        App.HeadingServiceFactory = _ => _headingService;
        App.LaunchSunAr = () => StartActivity(new Intent(this, typeof(SunArActivity)));
        // Anker-mTLS nativ (Androids SSLContext/KeyManager) — .NET-SslStream kann kein Client-Zertifikat.
        App.AnkerSecureStreamFactory = AndroidAnkerTls.ConnectAsync;

        base.OnCreate(savedInstanceState);

        // MainViewModel ist jetzt aufgelöst (via MainViewFactory in base.OnCreate) — Exit-Hinweis verdrahten.
        _mainVm = App.Services.GetService<MainViewModel>();
        if (_mainVm != null)
            _mainVm.ExitHintRequested += msg =>
                RunOnUiThread(() => Toast.MakeText(this, msg, ToastLength.Short)?.Show());

        RequestLocationPermissionIfNeeded();
    }

#pragma warning disable CA1422 // OnBackPressed ab API 33 veraltet, aber für ältere API-Level nötig
    public override void OnBackPressed()
    {
        // Erst die VM-Logik (Tab-Wechsel / Double-Back); sonst App in den Hintergrund.
        if (_mainVm != null && _mainVm.HandleBackPressed())
            return;
        MoveTaskToBack(true);
    }
#pragma warning restore CA1422

    protected override void OnResume()
    {
        base.OnResume();
        // GPS nur im Vordergrund betreiben (Akku). Start nur, wenn die Permission bereits erteilt ist;
        // direkt nach einem frischen Grant übernimmt OnRequestPermissionsResult den Start.
        if (CheckSelfPermission(global::Android.Manifest.Permission.AccessFineLocation) == Permission.Granted)
            _locationService?.Start();
    }

    protected override void OnPause()
    {
        // Im Hintergrund (oder während der AR-Activity) keine Standort-Updates — spart Akku.
        _locationService?.Stop();
        base.OnPause();
    }

    private void RequestLocationPermissionIfNeeded()
    {
        if (CheckSelfPermission(global::Android.Manifest.Permission.AccessFineLocation) != Permission.Granted)
            RequestPermissions([global::Android.Manifest.Permission.AccessFineLocation], LocationPermissionRequestCode);
        // Bei bereits erteilter Permission startet OnResume den Provider.
    }

    public override void OnRequestPermissionsResult(
        int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == LocationPermissionRequestCode
            && grantResults.Length > 0 && grantResults[0] == Permission.Granted)
        {
            _locationService?.Start();
        }
    }
}
