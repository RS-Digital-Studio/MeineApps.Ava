using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Avalonia;
using Avalonia.Android;
using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.DependencyInjection;
using RechnerPlus.ViewModels;

namespace RechnerPlus.Android;

[Activity(
    Label = "RechnerPlus",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
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

        // Haptic Feedback für Android registrieren
        App.HapticServiceFactory = _ => new AndroidHapticService(this);

        base.OnCreate(savedInstanceState);

        // Immersive Fullscreen aktivieren
        EnableImmersiveMode();

        // ViewModel holen und ExitHint-Event verdrahten
        _mainVm = App.Services.GetService<MainViewModel>();
        if (_mainVm != null)
        {
            _mainVm.ExitHintRequested += msg =>
                RunOnUiThread(() => Toast.MakeText(this, msg, ToastLength.Short)?.Show());
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        EnableImmersiveMode();
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

#pragma warning disable CA1422 // OnBackPressed ab API 33 veraltet, aber notwendig für ältere API-Level
    public override void OnBackPressed()
    {
        if (_mainVm != null && _mainVm.HandleBackPressed())
            return;
        MoveTaskToBack(true);
    }
#pragma warning restore CA1422
}

/// <summary>
/// Android-Implementierung für haptisches Feedback über den Vibrator-Service.
/// </summary>
public sealed class AndroidHapticService : IHapticService
{
    private readonly Activity _activity;
    private readonly Vibrator? _vibrator;

    public bool IsEnabled { get; set; } = true;

    public AndroidHapticService(Activity activity)
    {
        _activity = activity;
#pragma warning disable CA1422 // VibratorService veraltet ab API 31, Fallback ist HapticFeedback
        _vibrator = activity.GetSystemService(Context.VibratorService) as Vibrator;
#pragma warning restore CA1422
    }

#pragma warning disable CA1416 // API-Level bereits via SdkInt-Check abgesichert
    public void Tick()
    {
        if (!IsEnabled) return;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            PerformEffect(VibrationEffect.EffectTick);
        else
            PerformHapticFeedback(global::Android.Views.FeedbackConstants.KeyboardTap);
    }

    public void Click()
    {
        if (!IsEnabled) return;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            PerformEffect(VibrationEffect.EffectClick);
        else
            PerformHapticFeedback(global::Android.Views.FeedbackConstants.ContextClick);
    }

    public void HeavyClick()
    {
        if (!IsEnabled) return;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            PerformEffect(VibrationEffect.EffectHeavyClick);
        else
            PerformHapticFeedback(global::Android.Views.FeedbackConstants.LongPress);
    }

    private void PerformEffect(int effectId)
    {
        try
        {
            if (_vibrator?.HasVibrator == true)
                _vibrator.Vibrate(VibrationEffect.CreatePredefined(effectId));
        }
        catch
        {
            // Vibration nicht verfügbar
        }
    }
#pragma warning restore CA1416

    private void PerformHapticFeedback(global::Android.Views.FeedbackConstants constant)
    {
        try
        {
            _activity.Window?.DecorView?.PerformHapticFeedback(constant);
        }
        catch
        {
            // Haptic nicht verfügbar
        }
    }
}
