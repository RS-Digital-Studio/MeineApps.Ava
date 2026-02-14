using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Droid;
using MeineApps.Core.Premium.Ava.Services;
using WorkTimePro.Android.Services;

namespace WorkTimePro.Android;

[Activity(
    Label = "WorkTimePro",
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
        // Factories MUESSEN vor base.OnCreate (DI) registriert werden
        App.FileShareServiceFactory = () => new AndroidFileShareService(this);
        App.NotificationServiceFactory = () => new AndroidNotificationService();
        App.HapticServiceFactory = () => new AndroidHapticService();

        _rewardedAdHelper = new RewardedAdHelper();
        App.RewardedAdServiceFactory = sp =>
            new MeineApps.Core.Premium.Ava.Droid.AndroidRewardedAdService(
                _rewardedAdHelper!, sp.GetRequiredService<IPurchaseService>(), "WorkTimePro");

        base.OnCreate(savedInstanceState);

        // POST_NOTIFICATIONS Permission (Android 13+)
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            RequestPermissions([Manifest.Permission.PostNotifications], 100);
        }

        // Google Mobile Ads initialisieren - Ads erst nach SDK-Callback laden
        AdMobHelper.Initialize(this, () =>
        {
            // Banner-Ad Layout vorbereiten und laden
            _adMobHelper = new AdMobHelper();
            var adService = App.Services.GetRequiredService<IAdService>();
            var purchaseService = App.Services.GetRequiredService<IPurchaseService>();
            _adMobHelper.AttachToActivity(this, AdConfig.GetBannerAdUnitId("WorkTimePro"), adService, purchaseService, 56);

            // Rewarded Ad vorladen
            _rewardedAdHelper!.Load(this, AdConfig.GetRewardedAdUnitId("WorkTimePro"));

            // GDPR Consent-Form anzeigen falls noetig (EU)
            AdMobHelper.RequestConsent(this);
        });
    }

    // === Zurück-Taste: Navigation oder Double-Back-to-Exit ===

#pragma warning disable CS0672 // OnBackPressed ist deprecated ab API 33, aber Avalonia nutzt es intern
    public override void OnBackPressed()
    {
        try
        {
            var mainVm = App.Services.GetRequiredService<WorkTimePro.ViewModels.MainViewModel>();
            if (!mainVm.HandleBackPressed())
            {
                // Zweimal gedrückt → App in den Hintergrund (nicht destroyen)
                MoveTaskToBack(true);
            }
        }
        catch
        {
            base.OnBackPressed();
        }
    }
#pragma warning restore CS0672

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
