using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
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
    // SingleTask: Der Stempel-QR-Deep-Link holt die laufende Instanz nach vorne
    // (OnNewIntent) statt eine zweite Activity zu erzeugen (Avalonia = Single-Activity).
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
// Stempel-QR-Deep-Link: worktimepro://stamp (siehe QrStampRenderer.StampUri).
// Kamera-Scan öffnet die App und stempelt automatisch ein/aus.
[IntentFilter(
    [global::Android.Content.Intent.ActionView],
    Categories = [global::Android.Content.Intent.CategoryDefault, global::Android.Content.Intent.CategoryBrowsable],
    DataScheme = "worktimepro",
    DataHost = "stamp")]
public class MainActivity : AvaloniaMainActivity
{
    private AdMobHelper? _adMobHelper;
    private RewardedAdHelper? _rewardedAdHelper;

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

        // Google Play Billing (echte In-App-Käufe statt Desktop-Stub)
        App.PurchaseServiceFactory = sp =>
            new MeineApps.Core.Premium.Ava.Droid.AndroidPurchaseService(
                this, sp.GetRequiredService<IPreferencesService>(), sp.GetRequiredService<IAdService>());

        base.OnCreate(savedInstanceState);

        // Immersive Fullscreen aktivieren
        EnableImmersiveMode();

        // POST_NOTIFICATIONS Permission (Android 13+ / API 33). OperatingSystem-Guard,
        // damit der CA1416-Analyzer den API-33-Konstantenzugriff als geschützt erkennt.
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
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

        // Kaltstart über den Stempel-QR-Deep-Link (App war geschlossen)
        HandleStampIntent(Intent);
    }

    protected override void OnNewIntent(global::Android.Content.Intent? intent)
    {
        base.OnNewIntent(intent);
        // App lief bereits (SingleTask): Deep-Link kommt über OnNewIntent
        Intent = intent;
        HandleStampIntent(intent);
    }

    /// <summary>
    /// Verarbeitet den Stempel-QR-Deep-Link (worktimepro://stamp): stempelt ein/aus.
    /// Intent-Data wird danach genullt, damit Rotation/Recreate nicht erneut stempelt.
    /// </summary>
    private void HandleStampIntent(global::Android.Content.Intent? intent)
    {
        if (intent?.Data is not { Scheme: "worktimepro", Host: "stamp" })
            return;

        intent.SetData(null);

        try
        {
            var mainVm = App.Services.GetRequiredService<WorkTimePro.ViewModels.MainViewModel>();
            // Fire-and-forget: HandleStampScanAsync wartet intern auf die App-Initialisierung
            _ = mainVm.HandleStampScanAsync();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MainActivity", $"Stempel-Deep-Link fehlgeschlagen: {ex.Message}");
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        // POST_NOTIFICATIONS gewährt → Reminder neu planen (sie wurden ggf. vor der
        // Permission geplant und wären sonst bis zur nächsten Settings-Änderung stumm).
        if (requestCode == 100 && grantResults.Length > 0 && grantResults[0] == Permission.Granted)
        {
            try
            {
                var reminderService = App.Services.GetRequiredService<WorkTimePro.Services.IReminderService>();
                _ = reminderService.RescheduleAsync();
            }
            catch
            {
                // App.Services ggf. noch nicht bereit — Reminder werden beim nächsten
                // Settings-Save ohnehin neu geplant.
            }
        }
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
            MoveTaskToBack(true);
        }
    }
#pragma warning restore CS0672

    protected override void OnResume()
    {
        base.OnResume();
        _adMobHelper?.Resume();
        EnableImmersiveMode();
    }

    protected override void OnPause()
    {
        _adMobHelper?.Pause();
        base.OnPause();
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

        if (OperatingSystem.IsAndroidVersionAtLeast(30)) // API 30+
        {
#pragma warning disable CA1422 // SetDecorFitsSystemWindows ist ab API 35 deprecated, hier API 30-34 korrekt
            Window.SetDecorFitsSystemWindows(false);
#pragma warning restore CA1422
            var controller = Window.InsetsController;
            if (controller != null)
            {
                controller.Hide(WindowInsets.Type.SystemBars());
                controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
            }
        }
        else
        {
            // Fallback fuer aeltere API-Versionen (< 30) — SystemUiVisibility ist seit API 30 deprecated.
#pragma warning disable CA1422
#pragma warning disable CS0618
            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                SystemUiFlags.ImmersiveSticky |
                SystemUiFlags.LayoutStable |
                SystemUiFlags.LayoutHideNavigation |
                SystemUiFlags.LayoutFullscreen |
                SystemUiFlags.HideNavigation |
                SystemUiFlags.Fullscreen);
#pragma warning restore CS0618
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
