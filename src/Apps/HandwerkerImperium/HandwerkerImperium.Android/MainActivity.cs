using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Avalonia;
using Avalonia.Android;
using HandwerkerImperium.ViewModels;
using MeineApps.Core.Ava.Localization;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Premium.Ava.Droid;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium;

[Activity(
    Label = "HandwerkerImperium",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private AdMobHelper? _adMobHelper;
    private RewardedAdHelper? _rewardedAdHelper;

    // Double-Back-Press zum Beenden
    private DateTime _lastBackPress = DateTime.MinValue;
    private const int BackPressIntervalMs = 2000;

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
                _rewardedAdHelper!, sp.GetRequiredService<IPurchaseService>(), "HandwerkerImperium");

        base.OnCreate(savedInstanceState);

        // Google Mobile Ads initialisieren - Ads erst nach SDK-Callback laden
        AdMobHelper.Initialize(this, () =>
        {
            // Banner-Ad Layout vorbereiten und laden
            _adMobHelper = new AdMobHelper();
            var adService = App.Services.GetRequiredService<IAdService>();
            var purchaseService = App.Services.GetRequiredService<IPurchaseService>();
            _adMobHelper.AttachToActivity(this, AdConfig.GetBannerAdUnitId("HandwerkerImperium"), adService, purchaseService, 64);

            // Rewarded Ad vorladen
            _rewardedAdHelper!.Load(this, AdConfig.GetRewardedAdUnitId("HandwerkerImperium"));

            // GDPR Consent-Form anzeigen falls noetig (EU)
            AdMobHelper.RequestConsent(this);
        });
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

    [System.Obsolete("Avalonia nutzt OnBackPressed")]
    public override void OnBackPressed()
    {
        // Zuerst im ViewModel prüfen ob eine Ebene zurücknavigiert werden kann
        var mainVm = App.Services?.GetService<MainViewModel>();
        if (mainVm != null && mainVm.TryGoBack())
            return;

        // Double-Back-Press zum Beenden
        var now = DateTime.UtcNow;
        if ((now - _lastBackPress).TotalMilliseconds < BackPressIntervalMs)
        {
            base.OnBackPressed();
            return;
        }

        _lastBackPress = now;
        var localization = App.Services?.GetService<ILocalizationService>();
        var msg = localization?.GetString("PressBackAgainToExit") ?? "Erneut drücken zum Beenden";
        Toast.MakeText(this, msg, ToastLength.Short)?.Show();
    }

    protected override void OnDestroy()
    {
        _rewardedAdHelper?.Dispose();
        _adMobHelper?.Dispose();
        base.OnDestroy();
    }
}
