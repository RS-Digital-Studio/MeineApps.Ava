using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Avalonia.Android;
using SunSeeker.Android.Services;
using SunSeeker.Shared;

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

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Platform-Factories MUESSEN vor base.OnCreate gesetzt werden (Avalonia-12-Reihenfolge:
        // base.OnCreate loest via MainViewFactory das MainViewModel auf, das die Services zieht).
        _locationService = new AndroidLocationService(this);
        _headingService = new AndroidHeadingService(this);
        App.LocationServiceFactory = _ => _locationService;
        App.HeadingServiceFactory = _ => _headingService;

        base.OnCreate(savedInstanceState);

        RequestLocationPermissionIfNeeded();
    }

    private void RequestLocationPermissionIfNeeded()
    {
        if (CheckSelfPermission(global::Android.Manifest.Permission.AccessFineLocation) != Permission.Granted)
            RequestPermissions([global::Android.Manifest.Permission.AccessFineLocation], LocationPermissionRequestCode);
        else
            _locationService?.Start();
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
