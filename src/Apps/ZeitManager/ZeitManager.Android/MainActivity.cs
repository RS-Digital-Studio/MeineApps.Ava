using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Views;
using Android.Widget;
using Avalonia;
using Avalonia.Android;
using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.DependencyInjection;
using ZeitManager.Android.Services;
using ZeitManager.Services;
using ZeitManager.ViewModels;

namespace ZeitManager.Android;

[Activity(
    Label = "ZeitManager",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private const int RingtonePickerRequestCode = 9001;

    /// <summary>
    /// Statisches Flag: true wenn die App im Vordergrund ist.
    /// AlarmReceiver prüft dies, um Doppel-Auslösung zu vermeiden.
    /// </summary>
    public static bool IsAppInForeground { get; private set; }

    /// <summary>
    /// TaskCompletionSource für den Ringtone-Picker Result.
    /// </summary>
    private static TaskCompletionSource<string?>? _ringtonePickerTcs;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Immersive Fullscreen aktivieren
        EnableImmersiveMode();

        // ForegroundService-Callbacks registrieren sobald DI verfügbar ist
        if (App.Services?.GetService<ITimerService>() is TimerService timerService)
        {
            timerService.ForegroundNotificationCallback = (name, remaining) =>
                TimerForegroundService.UpdateNotification(this, name, remaining);
            timerService.StopForegroundCallback = () =>
                TimerForegroundService.StopService(this);
        }

        // Ringtone-Picker Callback für AndroidAudioService registrieren
        AndroidAudioService.PickRingtoneCallback = PickRingtoneAsync;

        // ViewModel holen und ExitHint-Event verdrahten
        _mainVm = App.Services?.GetService<MainViewModel>();
        if (_mainVm != null)
        {
            _mainVm.ExitHintRequested += msg =>
                RunOnUiThread(() => Toast.MakeText(this, msg, ToastLength.Short)?.Show());
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        IsAppInForeground = true;
        EnableImmersiveMode();
    }

    protected override void OnPause()
    {
        base.OnPause();
        IsAppInForeground = false;
    }

    /// <summary>
    /// Öffnet den Android Ringtone-Picker und wartet auf das Ergebnis.
    /// </summary>
    private Task<string?> PickRingtoneAsync()
    {
        _ringtonePickerTcs = new TaskCompletionSource<string?>();

        try
        {
            var intent = new Intent(RingtoneManager.ActionRingtonePicker);
            intent.PutExtra(RingtoneManager.ExtraRingtoneType,
                (int)(RingtoneType.Alarm | RingtoneType.Notification | RingtoneType.Ringtone));
            intent.PutExtra(RingtoneManager.ExtraRingtoneShowDefault, true);
            intent.PutExtra(RingtoneManager.ExtraRingtoneShowSilent, false);
            intent.PutExtra(RingtoneManager.ExtraRingtoneTitle, "Sound auswählen");

            StartActivityForResult(intent, RingtonePickerRequestCode);
        }
        catch
        {
            _ringtonePickerTcs.TrySetResult(null);
        }

        return _ringtonePickerTcs.Task;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == RingtonePickerRequestCode)
        {
            string? uri = null;
            if (resultCode == Result.Ok && data != null)
            {
                var ringtoneUri = data.GetParcelableExtra(RingtoneManager.ExtraRingtonePickedUri) as global::Android.Net.Uri;
                uri = ringtoneUri?.ToString();
            }

            _ringtonePickerTcs?.TrySetResult(uri);
            _ringtonePickerTcs = null;
        }
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

    private MainViewModel? _mainVm;

#pragma warning disable CA1422 // OnBackPressed ab API 33 veraltet, aber notwendig für ältere API-Level
    public override void OnBackPressed()
    {
        if (_mainVm != null && _mainVm.HandleBackPressed())
            return;
        MoveTaskToBack(true);
    }
#pragma warning restore CA1422

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Register Android-specific services before app initialization
        App.ConfigurePlatformServices = services =>
        {
            services.AddSingleton<INotificationService, AndroidNotificationService>();
            services.AddSingleton<IAudioService, AndroidAudioService>();
            services.AddSingleton<IShakeDetectionService, AndroidShakeDetectionService>();
            services.AddSingleton<IHapticService, AndroidHapticService>();
        };

        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
