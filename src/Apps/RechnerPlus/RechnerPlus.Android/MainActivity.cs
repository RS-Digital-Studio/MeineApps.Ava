using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using MeineApps.Core.Ava.Services;

namespace RechnerPlus.Android;

[Activity(
    Label = "RechnerPlus",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // URI-Launcher für Android registrieren (mailto:, https:, etc.)
        UriLauncher.PlatformOpenUri = uri =>
        {
            try
            {
                var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(uri));
                intent.AddFlags(ActivityFlags.NewTask);
                StartActivity(intent);
            }
            catch
            {
                // Kein Handler für URI verfügbar
            }
        };

        base.OnCreate(savedInstanceState);
    }
}
