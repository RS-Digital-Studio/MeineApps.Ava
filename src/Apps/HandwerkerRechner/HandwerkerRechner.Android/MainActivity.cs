using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Avalonia;
using Avalonia.Android;
using HandwerkerRechner.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Droid;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerRechner.Android;

[Activity(
    Label = "HandwerkerRechner",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    Exported = true,
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

        // Factories MUESSEN vor base.OnCreate (DI) registriert werden
        App.FileShareServiceFactory = () => new AndroidFileShareService(this);

        _rewardedAdHelper = new RewardedAdHelper();
        App.RewardedAdServiceFactory = sp =>
            new MeineApps.Core.Premium.Ava.Droid.AndroidRewardedAdService(
                _rewardedAdHelper!, sp.GetRequiredService<IPurchaseService>(), "HandwerkerRechner");

        // Google Play Billing (echte In-App-Käufe statt Desktop-Stub)
        App.PurchaseServiceFactory = sp =>
            new MeineApps.Core.Premium.Ava.Droid.AndroidPurchaseService(
                this, sp.GetRequiredService<IPreferencesService>(), sp.GetRequiredService<IAdService>());

        base.OnCreate(savedInstanceState);

        // Back-Navigation: Toast bei "Nochmal drücken zum Beenden"
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
            _adMobHelper.AttachToActivity(this, AdConfig.GetBannerAdUnitId("HandwerkerRechner"), adService, purchaseService, 56);

            // Rewarded Ad vorladen
            _rewardedAdHelper!.Load(this, AdConfig.GetRewardedAdUnitId("HandwerkerRechner"));

            // GDPR Consent-Form anzeigen falls noetig (EU)
            AdMobHelper.RequestConsent(this);
        });
    }

    // OnBackPressed ist ab API 33 deprecated, funktioniert aber weiterhin
    // solange enableOnBackInvokedCallback nicht in AndroidManifest gesetzt ist.
#pragma warning disable CA1422
    public override void OnBackPressed()
    {
        if (_mainVm != null && _mainVm.HandleBackPressed())
            return;

        base.OnBackPressed();
    }
#pragma warning restore CA1422

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
