using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Premium.Ava.Droid;
using MeineApps.Core.Premium.Ava.Services;

namespace FitnessRechner.Android;

[Activity(
    Label = "FitnessRechner",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private AdMobHelper? _adMobHelper;

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Global exception handlers for crash diagnostics
        AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
        {
            global::Android.Util.Log.Error("FitnessRechner", $"UNHANDLED: {args.Exception}");
        };
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            global::Android.Util.Log.Error("FitnessRechner", $"UNHANDLED: {args.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            global::Android.Util.Log.Error("FitnessRechner", $"UNOBSERVED: {args.Exception}");
        };

        try
        {
            base.OnCreate(savedInstanceState);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("FitnessRechner", $"ONCREATE BASE CRASH: {ex}");
            throw;
        }

        try
        {
            // Google Mobile Ads + GDPR consent
            AdMobHelper.Initialize(this);
            AdMobHelper.RequestConsent(this);

            // Banner ad above the tab bar (56dp tab bar height)
            _adMobHelper = new AdMobHelper();
            var adService = App.Services.GetRequiredService<IAdService>();
            var purchaseService = App.Services.GetRequiredService<IPurchaseService>();
            _adMobHelper.AttachToActivity(this, AdConfig.GetBannerAdUnitId("FitnessRechner"), adService, purchaseService, 56);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("FitnessRechner", $"ADMOB CRASH: {ex}");
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        _adMobHelper?.Resume();
    }

    protected override void OnPause()
    {
        _adMobHelper?.Pause();
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        _adMobHelper?.Dispose();
        base.OnDestroy();
    }
}
