using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Avalonia.Android;
using BomberBlast.Droid;
using BomberBlast.Input;
using BomberBlast.Services;
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
public class MainActivity : AvaloniaMainActivity
{
    private AdMobHelper? _adMobHelper;
    private RewardedAdHelper? _rewardedAdHelper;
    private MainViewModel? _mainVm;
    // GameViewModel NICHT als Field cachen — MainViewModel.GameVm ist seit v2.0.36 lazy-resolved.
    // Direkter App.Services.GetService&lt;GameViewModel&gt;() in OnCreate wuerde den Lazy-Refactor
    // (PERF-6, ~100-200ms Startup-Ersparnis) komplett aushebeln. Stattdessen `_mainVm?.GameVm`
    // bei Bedarf in DispatchKeyEvent/DispatchGenericMotionEvent abfragen — null wenn Spiel
    // noch nie gestartet wurde, da werden Gamepad-Events sowieso nicht gebraucht.

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

        // Vibration Service Factory: Android Vibrator-API statt NullVibrationService.
        // Wichtig: GameEngine kann jetzt selbst _vibration.VibrateTick() bei Joystick-Richtungswechsel
        // aufrufen (siehe GameEngine.cs:558) — keine MainActivity-Event-Subscription mehr noetig.
        App.VibrationServiceFactory = _ => new AndroidVibrationService(this);

        // Google Play Games Services Factory
        App.PlayGamesServiceFactory = sp =>
            new MeineApps.Core.Premium.Ava.Droid.AndroidPlayGamesService(
                this, sp.GetRequiredService<IPreferencesService>());

        // Firebase Telemetrie/Analytics/Push (v2.0.56): Factories vor base.OnCreate setzen,
        // damit DI sie statt der Null-Implementierungen einbindet. AndroidPushNotificationService
        // braucht die Activity-Referenz fuer RequestPermissions auf Android 13+.
        App.TelemetryServiceFactory = _ => new AndroidTelemetryService(this);
        App.AnalyticsServiceFactory = _ => new AndroidAnalyticsService(this);
        App.PushNotificationServiceFactory = _ => new AndroidPushNotificationService(this);

        // AI-Asset-Loader: WebP-Bilder aus Android Assets laden
        GameAssetService.PlatformAssetLoader = path =>
        {
            try { return Assets?.Open($"visuals/{path}"); }
            catch (Java.IO.FileNotFoundException) { return null; }
        };

        base.OnCreate(savedInstanceState);

        // Fullscreen/Immersive Mode (Landscape-Spiel, System-Bars komplett ausblenden)
        EnableImmersiveMode();

        // Back-Navigation: ViewModel holen + Toast-Event verdrahten.
        // Wichtig: NUR MainViewModel hier eager aus DI ziehen — GameViewModel + GameEngine bleiben
        // lazy (siehe Field-Kommentar oben). Vibration wird seit v2.0.36 in GameEngine.cs:560 selbst
        // via IVibrationService.VibrateTick() ausgeloest.
        _mainVm = App.Services.GetService<MainViewModel>();
        if (_mainVm != null)
        {
            _mainVm.ExitHintRequested += msg =>
                RunOnUiThread(() =>
                    Toast.MakeText(this, msg, ToastLength.Short)?.Show());
        }

        // Google Play Games Services initialisieren + Auto-Sign-In
        var playGames = App.Services.GetService<BomberBlast.Services.IPlayGamesService>()
            as MeineApps.Core.Premium.Ava.Droid.AndroidPlayGamesService;
        playGames?.InitializeSdk();
        _ = playGames?.SignInAsync().ContinueWith(t =>
        {
            if (t.IsFaulted)
                Android.Util.Log.Warn("BomberBlast", $"GPGS SignIn: {t.Exception?.GetBaseException().Message}");
        }, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);

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

        // Sprint 2.3 AAA-Audit #3: Re-Engagement-Notifications stornieren — User ist aktiv,
        // Reminder waeren irritierend.
        try
        {
            App.Services?.GetService<IReEngagementScheduler>()?.CancelAll();
        }
        catch { /* Best-Effort — Service evtl. noch nicht initialisiert */ }
    }

    protected override void OnPause()
    {
        _adMobHelper?.Pause();

        // Sprint 2.3 AAA-Audit #3: Re-Engagement-Notifications planen.
        // App geht in den Hintergrund, plane D1/D3/D7-Reminders fuer inaktive Spieler.
        try
        {
            App.Services?.GetService<IReEngagementScheduler>()?.ScheduleAll();
        }
        catch { /* Best-Effort — Service evtl. noch nicht initialisiert */ }

        base.OnPause();
    }

    [System.Obsolete("Avalonia nutzt OnBackPressed")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member (Avalonia-Framework-Vorgabe)
#pragma warning disable CA1422 // Deprecated in Android 33+ (OnBackInvokedDispatcher ist Nachfolger, aber Avalonia leitet noch via OnBackPressed)
    public override void OnBackPressed()
    {
        if (_mainVm != null && _mainVm.HandleBackPressed())
            return;

        base.OnBackPressed();
    }
#pragma warning restore CA1422
#pragma warning restore CS0809

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus) EnableImmersiveMode();
    }

    /// <summary>
    /// Permission-Callback. Aktuell nur fuer POST_NOTIFICATIONS (Android 13+) relevant —
    /// das Resultat geht an den PushNotificationService, der seine TCS-Promise aufloest.
    /// </summary>
    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (App.Services?.GetService<IPushNotificationService>() is AndroidPushNotificationService pushService)
            pushService.OnPermissionResult(requestCode, grantResults);
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
            // SetDecorFitsSystemWindows in Android 35+ deprecated zugunsten WindowCompat.SetDecorFitsSystemWindows,
            // funktioniert aber weiterhin. Migration erfolgt mit AndroidX.Core-Integration.
#pragma warning disable CA1422
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
            // Fallback fuer aeltere API-Versionen (< 30). SystemUiVisibility ist seit API 30 deprecated,
            // aber der einzige Weg fuer API 24-29 Immersive-Mode.
#pragma warning disable CA1422, CS0618
            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                SystemUiFlags.ImmersiveSticky |
                SystemUiFlags.LayoutStable |
                SystemUiFlags.LayoutHideNavigation |
                SystemUiFlags.LayoutFullscreen |
                SystemUiFlags.HideNavigation |
                SystemUiFlags.Fullscreen);
#pragma warning restore CA1422, CS0618
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAMEPAD / CONTROLLER SUPPORT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gamepad Face-Buttons abfangen (BUTTON_A/B/X/Y/START/SELECT).
    /// Avalonia leitet diese nicht als Key-Events weiter, daher direkte Abfangung.
    /// </summary>
    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        // GameVm ist seit v2.0.36 lazy — bei IsGameActive=true ist er garantiert initialisiert.
        if (e != null && _mainVm is { IsGameActive: true, GameVm: { } gameVm })
        {
            var gamepadButton = MapKeyCodeToGamepadButton(e.KeyCode);
            if (gamepadButton.HasValue)
            {
                if (e.Action == KeyEventActions.Down)
                    gameVm.OnGamepadButtonDown(gamepadButton.Value);
                else if (e.Action == KeyEventActions.Up)
                    gameVm.OnGamepadButtonUp(gamepadButton.Value);
                return true; // Konsumiert, nicht an Avalonia weiterleiten
            }
        }

        return base.DispatchKeyEvent(e);
    }

    /// <summary>
    /// Analog-Stick Werte abfangen (MotionEvent mit Joystick-Source).
    /// </summary>
    public override bool DispatchGenericMotionEvent(MotionEvent? e)
    {
        if (e != null && _mainVm is { IsGameActive: true, GameVm: { } gameVm } &&
            e.Action == MotionEventActions.Move &&
            (e.Source & InputSourceType.Joystick) == InputSourceType.Joystick)
        {
            float x = e.GetAxisValue(Axis.X);
            float y = e.GetAxisValue(Axis.Y);
            gameVm.SetAnalogStick(x, y);
            return true;
        }

        return base.DispatchGenericMotionEvent(e);
    }

    /// <summary>
    /// Android Keycode → GamepadButton Mapping.
    /// Gibt null zurück wenn der Keycode kein Gamepad-Button ist.
    /// </summary>
    private static GamepadButton? MapKeyCodeToGamepadButton(Keycode keyCode) => keyCode switch
    {
        Keycode.ButtonA => GamepadButton.A,
        Keycode.ButtonB => GamepadButton.B,
        Keycode.ButtonX => GamepadButton.X,
        Keycode.ButtonY => GamepadButton.Y,
        Keycode.ButtonStart => GamepadButton.Start,
        Keycode.ButtonSelect => GamepadButton.Select,
        // Menu-Button als alternativer Start-Button (manche Controller)
        Keycode.Menu => GamepadButton.Start,
        _ => null
    };

    protected override void OnDestroy()
    {
        // SkiaSharp-Objekte in GameEngine/GameRenderer freigeben
        App.DisposeServices();

        _rewardedAdHelper?.Dispose();
        _adMobHelper?.Dispose();
        base.OnDestroy();
    }
}
