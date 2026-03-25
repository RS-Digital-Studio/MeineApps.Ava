using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using GardenControl.Shared;

namespace GardenControl.Android;

[Activity(
    Label = "GardenControl",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
