using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Avalonia;
using Avalonia.Android;
using SmartMeasure.Android.Ar;
using SmartMeasure.Shared;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Android;

[Activity(
    Label = "SmartMeasure",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private MainViewModel? _mainVm;
    private AndroidArCaptureService? _arCaptureService;

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // BLE-Service fuer Verbindung zum Vermessungsstab
        App.BleServiceFactory = _ => new Services.AndroidBleService(this);

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
        if (_mainVm != null)
        {
            _mainVm.ExitHintRequested += msg =>
                RunOnUiThread(() => Toast.MakeText(this, msg, ToastLength.Short)?.Show());
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        _arCaptureService?.HandleActivityResult(requestCode, resultCode, data);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        _arCaptureService?.HandlePermissionResult(requestCode, grantResults);
    }

    public override void OnBackPressed()
    {
        if (_mainVm != null && _mainVm.HandleBackPressed()) return;
        base.OnBackPressed();
    }
}
