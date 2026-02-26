using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Avalonia;
using Avalonia.Android;
using BomberBlast.Core;
using BomberBlast.Droid;
using BomberBlast.Input;
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
    private GameViewModel? _gameVm;

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

        // Google Play Games Services Factory
        App.PlayGamesServiceFactory = sp =>
            new MeineApps.Core.Premium.Ava.Droid.AndroidPlayGamesService(
                this, sp.GetRequiredService<IPreferencesService>());

        base.OnCreate(savedInstanceState);

        // Fullscreen/Immersive Mode (Landscape-Spiel, System-Bars komplett ausblenden)
        EnableImmersiveMode();

        // Back-Navigation: ViewModel holen + Toast-Event verdrahten
        _mainVm = App.Services.GetService<MainViewModel>();
        _gameVm = App.Services.GetService<GameViewModel>();
        if (_mainVm != null)
        {
            _mainVm.ExitHintRequested += msg =>
                RunOnUiThread(() =>
                    Toast.MakeText(this, msg, ToastLength.Short)?.Show());
        }

        // Haptisches Feedback bei Joystick-Richtungswechsel (15ms Tick)
        var gameEngine = App.Services.GetService<GameEngine>();
        if (gameEngine != null)
        {
            var vibrator = (Vibrator?)GetSystemService(VibratorService);
            if (vibrator != null)
            {
                gameEngine.OnDirectionChanged += () =>
                {
                    try
                    {
                        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                            vibrator.Vibrate(VibrationEffect.CreateOneShot(15, VibrationEffect.DefaultAmplitude));
                    }
                    catch (Java.Lang.SecurityException)
                    {
                        // VIBRATE-Permission fehlt oder entzogen → still ignorieren
                    }
                };
            }
        }

        // Google Play Games Services initialisieren + Auto-Sign-In
        var playGames = App.Services.GetService<BomberBlast.Services.IPlayGamesService>()
            as MeineApps.Core.Premium.Ava.Droid.AndroidPlayGamesService;
        playGames?.InitializeSdk();
        _ = playGames?.SignInAsync(); // Fire-and-Forget Auto-Login (GPGS v2 Standard)

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

    // ═══════════════════════════════════════════════════════════════════════
    // GAMEPAD / CONTROLLER SUPPORT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gamepad Face-Buttons abfangen (BUTTON_A/B/X/Y/START/SELECT).
    /// Avalonia leitet diese nicht als Key-Events weiter, daher direkte Abfangung.
    /// </summary>
    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e != null && _mainVm?.IsGameActive == true && _gameVm != null)
        {
            var gamepadButton = MapKeyCodeToGamepadButton(e.KeyCode);
            if (gamepadButton.HasValue)
            {
                if (e.Action == KeyEventActions.Down)
                    _gameVm.OnGamepadButtonDown(gamepadButton.Value);
                else if (e.Action == KeyEventActions.Up)
                    _gameVm.OnGamepadButtonUp(gamepadButton.Value);
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
        if (e != null && _mainVm?.IsGameActive == true && _gameVm != null &&
            e.Action == MotionEventActions.Move &&
            (e.Source & InputSourceType.Joystick) == InputSourceType.Joystick)
        {
            float x = e.GetAxisValue(Axis.X);
            float y = e.GetAxisValue(Axis.Y);
            _gameVm.SetAnalogStick(x, y);
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
        _rewardedAdHelper?.Dispose();
        _adMobHelper?.Dispose();
        base.OnDestroy();
    }
}
