using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Widget;
using Avalonia;
using Avalonia.Android;
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
    }

    protected override void OnResume()
    {
        base.OnResume();
        IsAppInForeground = true;
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

    private DateTime _lastBackPress = DateTime.MinValue;
    private const int BackPressIntervalMs = 2000;

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
        var localization = App.Services?.GetService<MeineApps.Core.Ava.Localization.ILocalizationService>();
        var msg = localization?.GetString("PressBackAgainToExit") ?? "Erneut drücken zum Beenden";
        Toast.MakeText(this, msg, ToastLength.Short)?.Show();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Register Android-specific services before app initialization
        App.ConfigurePlatformServices = services =>
        {
            services.AddSingleton<INotificationService, AndroidNotificationService>();
            services.AddSingleton<IAudioService, AndroidAudioService>();
            services.AddSingleton<IShakeDetectionService, AndroidShakeDetectionService>();
        };

        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
