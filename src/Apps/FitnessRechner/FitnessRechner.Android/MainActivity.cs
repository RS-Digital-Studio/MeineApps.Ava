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
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private AdMobHelper? _adMobHelper;
    private RewardedAdHelper? _rewardedAdHelper;

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

        // Rewarded Ad Helper + Factory MUSS vor base.OnCreate (DI) registriert werden
        _rewardedAdHelper = new RewardedAdHelper();
        App.RewardedAdServiceFactory = sp =>
            new MeineApps.Core.Premium.Ava.Droid.AndroidRewardedAdService(
                _rewardedAdHelper!, sp.GetRequiredService<IPurchaseService>(), "FitnessRechner");

        // FileShareService Factory fuer Android (Share Intent mit FileProvider)
        App.FileShareServiceFactory = () =>
            new MeineApps.Core.Premium.Ava.Droid.AndroidFileShareService(this);

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
            // Google Mobile Ads initialisieren - Ads erst nach SDK-Callback laden
            AdMobHelper.Initialize(this, () =>
            {
                // Banner-Ad Layout vorbereiten und laden
                _adMobHelper = new AdMobHelper();
                var adService = App.Services.GetRequiredService<IAdService>();
                var purchaseService = App.Services.GetRequiredService<IPurchaseService>();
                _adMobHelper.AttachToActivity(this, AdConfig.GetBannerAdUnitId("FitnessRechner"), adService, purchaseService, 56);

                // Rewarded Ad vorladen
                _rewardedAdHelper!.Load(this, AdConfig.GetRewardedAdUnitId("FitnessRechner"));

                // GDPR Consent-Form anzeigen falls noetig (EU)
                AdMobHelper.RequestConsent(this);
            });
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
        _rewardedAdHelper?.Dispose();
        _adMobHelper?.Dispose();
        base.OnDestroy();
    }
}
