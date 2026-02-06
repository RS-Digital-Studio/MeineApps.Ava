using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using ZeitManager.Android.Services;
using ZeitManager.Services;

namespace ZeitManager.Android;

[Activity(
    Label = "ZeitManager",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Register Android-specific services before app initialization
        App.ConfigurePlatformServices = services =>
        {
            services.AddSingleton<INotificationService, AndroidNotificationService>();
            services.AddSingleton<IAudioService, AndroidAudioService>();
        };

        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
