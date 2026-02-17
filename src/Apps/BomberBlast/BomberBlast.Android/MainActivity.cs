using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Avalonia;
using Avalonia.Android;
using BomberBlast.Droid;
using BomberBlast.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Services;
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

        // Google Play Billing (echte In-App-Käufe statt Desktop-Stub)
        App.PurchaseServiceFactory = sp =>
            new MeineApps.Core.Premium.Ava.Droid.AndroidPurchaseService(
                this, sp.GetRequiredService<IPreferencesService>(), sp.GetRequiredService<IAdService>());

        // Sound Service Factory: Android-SoundPool/MediaPlayer statt NullSoundService
        App.SoundServiceFactory = _ => new AndroidSoundService(this);

        base.OnCreate(savedInstanceState);

        // Fullscreen/Immersive Mode (Landscape-Spiel, System-Bars komplett ausblenden)
        EnableImmersiveMode();

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
        EnableImmersiveMode();

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

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus) EnableImmersiveMode();
    }

    /// <summary>
    /// Immersive Fullscreen: StatusBar + NavigationBar ausblenden.
    /// Bars erscheinen bei Swipe vom Rand kurz und verschwinden automatisch wieder.
    /// </summary>
    private void EnableImmersiveMode()
    {
        if (Window == null) return;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.R) // API 30+
        {
            Window.SetDecorFitsSystemWindows(false);
            var controller = Window.InsetsController;
            if (controller != null)
            {
                controller.Hide(WindowInsets.Type.SystemBars());
                controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
            }
        }
        else
        {
            // Fallback fuer aeltere API-Versionen (< 30)
#pragma warning disable CA1422 // Deprecated API fuer Kompatibilitaet
            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                SystemUiFlags.ImmersiveSticky |
                SystemUiFlags.LayoutStable |
                SystemUiFlags.LayoutHideNavigation |
                SystemUiFlags.LayoutFullscreen |
                SystemUiFlags.HideNavigation |
                SystemUiFlags.Fullscreen);
#pragma warning restore CA1422
        }
    }

    protected override void OnDestroy()
    {
        _rewardedAdHelper?.Dispose();
        _adMobHelper?.Dispose();
        base.OnDestroy();
    }
}
