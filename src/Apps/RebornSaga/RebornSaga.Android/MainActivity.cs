using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Avalonia.Android;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Droid;
using MeineApps.Core.Premium.Ava.Services;
using Microsoft.Extensions.DependencyInjection;
using RebornSaga.Android;
using RebornSaga.Services;
using RebornSaga.ViewModels;

namespace RebornSaga;

[Activity(
    Label = "Reborn Saga",
    Theme = "@android:style/Theme.Material.Light.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    Exported = true,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    private AdMobHelper? _adMobHelper;
    private RewardedAdHelper? _rewardedAdHelper;
    private MainViewModel? _mainVm;
    private Action<string>? _exitHintHandler;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Avalonia 12: Factory-Setups muessen VOR base.OnCreate gesetzt sein.
        // Frueher lief dieser Code in CustomizeAppBuilder.

        // Rewarded Ad Helper + Factory MUSS vor base.OnCreate (DI) registriert werden
        _rewardedAdHelper = new RewardedAdHelper();
        App.RewardedAdServiceFactory = sp =>
            new AndroidRewardedAdService(
                _rewardedAdHelper!, sp.GetRequiredService<IPurchaseService>(), "RebornSaga");

        // Google Play Billing (echte In-App-Käufe statt Desktop-Stub)
        App.PurchaseServiceFactory = sp =>
            new AndroidPurchaseService(
                this, sp.GetRequiredService<IPreferencesService>(), sp.GetRequiredService<IAdService>());

        // Audio-Factory: Android-Implementierung mit SoundPool + MediaPlayer
        App.AudioServiceFactory = sp =>
            new AndroidAudioService(this, sp.GetRequiredService<IPreferencesService>());

        base.OnCreate(savedInstanceState);

        // Immersive Fullscreen (Portrait-Spiel)
        EnableImmersiveMode();

        // Back-Navigation: ViewModel holen + Toast-Event verdrahten
        _mainVm = App.Services.GetService<MainViewModel>();
        if (_mainVm != null)
        {
            // Handler in Feld halten → in OnDestroy abmeldbar (die Singleton-VM wuerde sonst die
            // Activity ueber das capturing Lambda festhalten = Leak bei Activity-Recreation).
            _exitHintHandler = msg =>
                RunOnUiThread(() =>
                    Toast.MakeText(this, msg, ToastLength.Short)?.Show());
            _mainVm.ExitHintRequested += _exitHintHandler;
        }

        // Google Mobile Ads initialisieren - Ads erst nach SDK-Callback laden
        AdMobHelper.Initialize(this, () =>
        {
            // Banner-Ad (kein Banner für RebornSaga - Vollbild-SkiaSharp-Spiel)
            // Rewarded Ad vorladen
            _rewardedAdHelper!.Load(this, AdConfig.GetRewardedAdUnitId("RebornSaga"));

            // GDPR Consent-Form anzeigen falls nötig (EU)
            AdMobHelper.RequestConsent(this);
        });
    }

    protected override void OnResume()
    {
        base.OnResume();
        EnableImmersiveMode();
        _adMobHelper?.Resume();
    }

    protected override void OnPause()
    {
        _adMobHelper?.Pause();
        base.OnPause();
    }

    // OnBackPressed ist ab API 33 deprecated (OnBackInvokedCallback), wird aber von Avalonias
    // Single-Activity-Modell weiterhin genutzt — ein API-33-Umstieg waere ein eigenes Feature.
#pragma warning disable CA1422
    public override void OnBackPressed()
    {
        if (_mainVm != null && _mainVm.HandleBackPressed())
            return;

        base.OnBackPressed();
    }
#pragma warning restore CA1422

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus) EnableImmersiveMode();
    }

    /// <summary>
    /// Immersive Fullscreen: StatusBar + NavigationBar ausblenden.
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
            // Fallback für ältere API-Versionen (< 30) — SystemUiVisibility ist seit API 30 deprecated.
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
        if (_mainVm != null && _exitHintHandler != null)
            _mainVm.ExitHintRequested -= _exitHintHandler;
        App.DisposeServices();
        _rewardedAdHelper?.Dispose();
        _adMobHelper?.Dispose();
        base.OnDestroy();
    }
}
