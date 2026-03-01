using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Avalonia;
using Avalonia.Android;
using HandwerkerImperium.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Droid;
using MeineApps.Core.Premium.Ava.Services;
using Android.Gms.Games;
using Xamarin.Google.Android.Play.Core.Review;
using HandwerkerImperium.Android;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

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
                _rewardedAdHelper!, sp.GetRequiredService<IPurchaseService>(), "HandwerkerImperium");

        // Google Play Billing (echte In-App-Käufe statt Desktop-Stub)
        App.PurchaseServiceFactory = sp =>
            new MeineApps.Core.Premium.Ava.Droid.AndroidPurchaseService(
                this, sp.GetRequiredService<IPreferencesService>(), sp.GetRequiredService<IAdService>());

        // Audio-Service für Sounds und Haptik
        App.AudioServiceFactory = sp => new AndroidAudioService(this, sp.GetRequiredService<IGameStateService>());

        // Benachrichtigungs-Service für lokale Push-Benachrichtigungen
        App.NotificationServiceFactory = _ => new AndroidNotificationService(this);

        // Google Play Games SDK initialisieren (MUSS vor erstem Client-Aufruf)
        PlayGamesSdk.Initialize(this);

        // Google Play Games für Leaderboards, Cloud Save und Gilden
        App.PlayGamesServiceFactory = _ => new AndroidPlayGamesService(this);

        // In-App Review-Prompt über Google Play Review API
        App.ReviewPromptRequested = () => LaunchReviewFlow();

        // Share-Text via natives Android Share-Sheet (Intent.ActionSend)
        UriLauncher.PlatformShareText = (text, title) =>
        {
            try
            {
                var intent = new Intent(Intent.ActionSend);
                intent.SetType("text/plain");
                intent.PutExtra(Intent.ExtraText, text);
                StartActivity(Intent.CreateChooser(intent, title));
            }
            catch
            {
                // Share-Sheet nicht verfügbar
            }
        };

        base.OnCreate(savedInstanceState);

        // ViewModel holen und ExitHint-Event verdrahten
        _mainVm = App.Services.GetService<MainViewModel>();
        if (_mainVm != null)
        {
            _mainVm.ExitHintRequested += msg =>
                RunOnUiThread(() => Toast.MakeText(this, msg, ToastLength.Short)?.Show());
        }

        // Immersive Fullscreen aktivieren
        EnableImmersiveMode();

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
        _mainVm?.ResumeGameLoop();

        // Immersive Mode nach Resume wiederherstellen (z.B. nach Ad-Anzeige)
        EnableImmersiveMode();
    }

    protected override void OnPause()
    {
        _mainVm?.PauseGameLoop();
        _adMobHelper?.Pause();
        base.OnPause();
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

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus) EnableImmersiveMode();
    }

#pragma warning disable CA1422 // OnBackPressed ab API 33 veraltet, aber notwendig für ältere API-Level
    public override void OnBackPressed()
    {
        if (_mainVm != null && _mainVm.HandleBackPressed())
            return;
        base.OnBackPressed();
    }
#pragma warning restore CA1422

    /// <summary>
    /// Startet den Google In-App Review Flow.
    /// Zeigt dem Nutzer den nativen Play Store Bewertungsdialog.
    /// </summary>
    private void LaunchReviewFlow()
    {
        try
        {
            var manager = ReviewManagerFactory.Create(this);
            var requestTask = manager.RequestReviewFlow();
            requestTask.AddOnCompleteListener(new ReviewRequestListener(this, manager));
        }
        catch
        {
            // Review API nicht verfügbar (z.B. kein Play Store auf Gerät)
        }
    }

    /// <summary>
    /// Java-Callback für den asynchronen Review-Request.
    /// Startet den eigentlichen Review-Dialog wenn der Request erfolgreich war.
    /// </summary>
    private class ReviewRequestListener(Activity activity, IReviewManager manager)
        : Java.Lang.Object, global::Android.Gms.Tasks.IOnCompleteListener
    {
        public void OnComplete(global::Android.Gms.Tasks.Task task)
        {
            if (task.IsSuccessful && task.Result is ReviewInfo reviewInfo)
            {
                try
                {
                    manager.LaunchReviewFlow(activity, reviewInfo);
                }
                catch
                {
                    // Review-Dialog konnte nicht angezeigt werden
                }
            }
        }
    }

    protected override void OnDestroy()
    {
        _rewardedAdHelper?.Dispose();
        _adMobHelper?.Dispose();
        base.OnDestroy();
    }
}
