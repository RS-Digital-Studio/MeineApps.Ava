using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Avalonia;
using Avalonia.Android;
using BomberBlast.Droid;
using BomberBlast.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Premium.Ava.Droid;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast;

[Activity(
    Label = "BomberBlast",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    Exported = true,
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private AdMobHelper? _adMobHelper;
    private RewardedAdHelper? _rewardedAdHelper;
    private MainViewModel? _mainVm;

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Rewarded Ad Helper + Factory MUSS vor base.OnCreate (DI) registriert werden
        _rewardedAdHelper = new RewardedAdHelper();
        App.RewardedAdServiceFactory = sp =>
            new MeineApps.Core.Premium.Ava.Droid.AndroidRewardedAdService(
                _rewardedAdHelper!, sp.GetRequiredService<IPurchaseService>(), "BomberBlast");

        // Sound Service Factory: Android-SoundPool/MediaPlayer statt NullSoundService
        App.SoundServiceFactory = _ => new AndroidSoundService(this);

        base.OnCreate(savedInstanceState);

        // Fullscreen/Immersive Mode (Landscape-Spiel, System-Bars ausblenden)
        if (Window != null)
        {
            AndroidX.Core.View.WindowCompat.SetDecorFitsSystemWindows(Window, false);
            var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(Window, Window.DecorView);
            controller.Hide(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars());
            controller.SystemBarsBehavior = AndroidX.Core.View.WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
        }

        // Back-Navigation: ViewModel holen + Toast-Event verdrahten
        _mainVm = App.Services.GetService<MainViewModel>();
        if (_mainVm != null)
        {
            _mainVm.ExitHintRequested += msg =>
                RunOnUiThread(() =>
                    Toast.MakeText(this, msg, ToastLength.Short)?.Show());
        }

        // Google Mobile Ads initialisieren - Ads erst nach SDK-Callback laden
        AdMobHelper.Initialize(this, () =>
        {
            // Banner-Ad Layout vorbereiten und laden
            _adMobHelper = new AdMobHelper();
            var adService = App.Services.GetRequiredService<IAdService>();
            var purchaseService = App.Services.GetRequiredService<IPurchaseService>();
            _adMobHelper.AttachToActivity(this, AdConfig.GetBannerAdUnitId("BomberBlast"), adService, purchaseService);

            // Rewarded Ad vorladen
            _rewardedAdHelper!.Load(this, AdConfig.GetRewardedAdUnitId("BomberBlast"));

            // GDPR Consent-Form anzeigen falls noetig (EU)
            AdMobHelper.RequestConsent(this);
        });
    }

    protected override void OnResume()
    {
        base.OnResume();

        // Fullscreen/Immersive Mode erneut setzen (kann bei Alt-Tab etc. verloren gehen)
        if (Window != null)
        {
            AndroidX.Core.View.WindowCompat.SetDecorFitsSystemWindows(Window, false);
            var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(Window, Window.DecorView);
            controller.Hide(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars());
            controller.SystemBarsBehavior = AndroidX.Core.View.WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
        }

        _adMobHelper?.Resume();
    }

    protected override void OnPause()
    {
        _adMobHelper?.Pause();
        base.OnPause();
    }

    [System.Obsolete("Avalonia nutzt OnBackPressed")]
    public override void OnBackPressed()
    {
        if (_mainVm != null && _mainVm.HandleBackPressed())
            return;

        base.OnBackPressed();
    }

    protected override void OnDestroy()
    {
        _rewardedAdHelper?.Dispose();
        _adMobHelper?.Dispose();
        base.OnDestroy();
    }
}
