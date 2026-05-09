using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Avalonia.Android;
using BingXBot.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BingXBot;

/// <summary>
/// Android-Einstiegspunkt fuer BingXBot. Minimalistisch — die App verbindet sich
/// zum Pi-Server und steuert ihn ferngesteuert. Keine Trading-Engine auf dem Handy.
///
/// Avalonia 12: AvaloniaMainActivity (non-generic). Application-Bootstrap ist nun in
/// <see cref="AndroidApp"/> ausgelagert — diese Activity macht nur noch UI/Lifecycle.
/// </summary>
[Activity(
    Label = "BingXBot",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    Exported = true,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    private MainViewModel? _mainVm;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Android-spezifische Pfade registrieren BEVOR DI-Container gebaut wird.
        // OnFrameworkInitializationCompleted ruft AppPathsFactory ab — muss vor base.OnCreate gesetzt sein.
        // Hier in der Activity (nicht in AndroidApp.OnCreate), weil AndroidAppPaths einen Context braucht.
        App.AppPathsFactory = () => new AndroidAppPaths(this);

        base.OnCreate(savedInstanceState);

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
