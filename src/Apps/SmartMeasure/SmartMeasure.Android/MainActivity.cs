using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Avalonia;
using Avalonia.Android;
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

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // TODO: BleServiceFactory fuer Android setzen wenn AndroidBleService implementiert ist
        // App.BleServiceFactory = sp => new AndroidBleService(this);

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

    public override void OnBackPressed()
    {
        if (_mainVm != null && _mainVm.HandleBackPressed()) return;
        base.OnBackPressed();
    }
}
