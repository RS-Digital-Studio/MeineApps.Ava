using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Avalonia.Android;
using GardenControl.Shared;
using GardenControl.Shared.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GardenControl.Android;

[Activity(
    Label = "GardenControl",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    private MainViewModel? _mainVm;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // VM-Referenz fuer Back-Button-Handling + Exit-Toast
        _mainVm = App.Services?.GetService<MainViewModel>();
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
