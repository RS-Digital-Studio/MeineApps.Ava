using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Media;
using Android.OS;
using Android.Views;
using Android.Widget;
using MeineApps.Core.Ava.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace ZeitManager.Android.Services;

/// <summary>
/// Dedizierte Activity fuer Alarm-Anzeige ueber dem Lockscreen.
/// Wird von AlarmReceiver als FullScreenIntent gestartet.
/// Zeigt Alarm-UI mit Dismiss/Snooze Buttons und spielt Alarm-Sound.
/// </summary>
[Activity(
    Theme = "@android:style/Theme.DeviceDefault.NoActionBar",
    ShowWhenLocked = true,
    TurnScreenOn = true,
    ExcludeFromRecents = true,
    LaunchMode = LaunchMode.SingleInstance,
    TaskAffinity = "",
    Exported = false)]
public class AlarmActivity : Activity
{
    private Ringtone? _ringtone;
    private Vibrator? _vibrator;
    private string? _alarmId;
    private int _snoozeDurationMinutes = 5;
    private Handler? _timeHandler;
    private Java.Lang.Runnable? _timeRunnable;
    private Handler? _volumeHandler;
    private Java.Lang.Runnable? _volumeRunnable;
    private float _currentVolume = 0.1f;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // LockScreen-Flags fuer aeltere APIs (< API 27)
        if (Build.VERSION.SdkInt < BuildVersionCodes.OMr1)
        {
#pragma warning disable CA1422
            Window!.AddFlags(WindowManagerFlags.ShowWhenLocked
                           | WindowManagerFlags.TurnScreenOn
                           | WindowManagerFlags.DismissKeyguard);
#pragma warning restore CA1422
        }

        // Bildschirm anlassen waehrend Alarm aktiv
        Window!.AddFlags(WindowManagerFlags.KeepScreenOn);

        var title = Intent?.GetStringExtra("title") ?? "ZeitManager";
        var body = Intent?.GetStringExtra("body") ?? "Alarm!";
        _alarmId = Intent?.GetStringExtra("id") ?? "alarm";
        _snoozeDurationMinutes = Intent?.GetIntExtra("snooze_duration", 5) ?? 5;
        if (_snoozeDurationMinutes < 1) _snoozeDurationMinutes = 5;

        SetContentView(CreateAlarmLayout(title, body));

        PlayAlarmSound();
        StartVibration();
    }

    /// <summary>
    /// Erstellt das Alarm-Layout programmatisch (kein XML noetig).
    /// Dunkler Hintergrund, zentrierter Content mit Titel, Uhrzeit, Buttons.
    /// </summary>
    private FrameLayout CreateAlarmLayout(string title, string body)
    {
        var root = new FrameLayout(this)
        {
            LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };
        root.SetBackgroundColor(Color.ParseColor("#E0000000"));

        // Zentrierter Container
        var container = new LinearLayout(this)
        {
            Orientation = global::Android.Widget.Orientation.Vertical,
            LayoutParameters = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent,
                GravityFlags.Center)
        };
        container.SetGravity(GravityFlags.CenterHorizontal);
        container.SetPadding(DpToPx(32), DpToPx(32), DpToPx(32), DpToPx(32));

        // Alarm-Icon (grosser Kreis)
        var iconSize = DpToPx(120);
        var iconView = new TextView(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(iconSize, iconSize)
            {
                Gravity = GravityFlags.CenterHorizontal,
                BottomMargin = DpToPx(24)
            },
            Text = "\u23F0", // Alarm-Emoji als Fallback
            TextSize = 48,
            Gravity = GravityFlags.Center
        };
        var iconBg = new GradientDrawable();
        iconBg.SetShape(ShapeType.Oval);
        iconBg.SetColor(Color.ParseColor("#6366F1")); // Primary Indigo
        iconView.Background = iconBg;
        container.AddView(iconView);

        // Titel
        var titleView = new TextView(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.WrapContent)
            {
                Gravity = GravityFlags.CenterHorizontal,
                BottomMargin = DpToPx(8)
            },
            Text = title,
            TextSize = 28,
            Gravity = GravityFlags.Center
        };
        titleView.SetTextColor(Color.White);
        titleView.SetTypeface(null, TypefaceStyle.Bold);
        container.AddView(titleView);

        // Body
        var bodyView = new TextView(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.WrapContent)
            {
                Gravity = GravityFlags.CenterHorizontal,
                BottomMargin = DpToPx(24)
            },
            Text = body,
            TextSize = 16,
            Gravity = GravityFlags.Center
        };
        bodyView.SetTextColor(Color.ParseColor("#B0FFFFFF"));
        container.AddView(bodyView);

        // Aktuelle Uhrzeit (gross)
        var timeView = new TextView(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.WrapContent)
            {
                Gravity = GravityFlags.CenterHorizontal,
                BottomMargin = DpToPx(48)
            },
            Text = DateTime.Now.ToString("HH:mm"),
            TextSize = 56,
            Gravity = GravityFlags.Center
        };
        timeView.SetTextColor(Color.White);
        timeView.SetTypeface(null, TypefaceStyle.Normal);
        container.AddView(timeView);

        // Uhrzeit jede Sekunde aktualisieren
        _timeHandler = new Handler(Looper.MainLooper!);
        _timeRunnable = new Java.Lang.Runnable(() =>
        {
            timeView.Text = DateTime.Now.ToString("HH:mm");
            _timeHandler?.PostDelayed(_timeRunnable!, 1000);
        });
        _timeHandler.PostDelayed(_timeRunnable, 1000);

        // Dismiss-Button
        var dismissBtn = new Button(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(
                DpToPx(260),
                DpToPx(56))
            {
                Gravity = GravityFlags.CenterHorizontal,
                BottomMargin = DpToPx(12)
            },
            Text = GetLocalizedString("Dismiss", "Dismiss"),
            TextSize = 18
        };
        dismissBtn.SetTextColor(Color.White);
        var dismissBg = new GradientDrawable();
        dismissBg.SetCornerRadius(DpToPx(28));
        dismissBg.SetColor(Color.ParseColor("#6366F1"));
        dismissBtn.Background = dismissBg;
        dismissBtn.Click += (_, _) => DismissAlarm();
        container.AddView(dismissBtn);

        // Snooze-Button (Dauer aus Intent)
        var snoozeBtn = new Button(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(
                DpToPx(260),
                DpToPx(48))
            {
                Gravity = GravityFlags.CenterHorizontal
            },
            Text = $"{GetLocalizedString("Snooze", "Snooze")} ({_snoozeDurationMinutes} min)",
            TextSize = 16
        };
        snoozeBtn.SetTextColor(Color.White);
        var snoozeBg = new GradientDrawable();
        snoozeBg.SetCornerRadius(DpToPx(24));
        snoozeBg.SetStroke(DpToPx(2), Color.ParseColor("#80FFFFFF"));
        snoozeBg.SetColor(Color.Transparent);
        snoozeBtn.Background = snoozeBg;
        snoozeBtn.Click += (_, _) => SnoozeAlarm();
        container.AddView(snoozeBtn);

        root.AddView(container);
        return root;
    }

    private void DismissAlarm()
    {
        StopAlarmSound();
        StopVibration();
        CancelNotification();
        Finish();
    }

    private void SnoozeAlarm()
    {
        StopAlarmSound();
        StopVibration();
        CancelNotification();

        ScheduleSnooze();
        Finish();
    }

    private void ScheduleSnooze()
    {
        try
        {
            var alarmManager = (AlarmManager?)GetSystemService(AlarmService);
            if (alarmManager == null) return;

            // DateTime.Now verwenden (konsistent mit AlarmSchedulerService)
            var snoozeTime = DateTime.Now.AddMinutes(_snoozeDurationMinutes);
            var triggerMs = (long)(snoozeTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

            var title = Intent?.GetStringExtra("title") ?? "ZeitManager";
            var body = Intent?.GetStringExtra("body") ?? "Alarm!";

            var intent = new Intent(this, typeof(AlarmReceiver));
            intent.PutExtra("title", title);
            intent.PutExtra("body", body);
            // Original-Alarm-ID beibehalten (nicht "_snooze" anhängen)
            intent.PutExtra("id", _alarmId);
            intent.PutExtra("snooze_duration", _snoozeDurationMinutes);

            var pendingIntent = PendingIntent.GetBroadcast(
                this, AndroidNotificationService.StableHash(_alarmId ?? "alarm"), intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            if (pendingIntent != null)
            {
                // SetAlarmClock statt SetExactAndAllowWhileIdle (zuverlaessiger)
                var showIntent = PendingIntent.GetActivity(
                    this, 0, new Intent(this, typeof(MainActivity)),
                    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
                var alarmClock = new AlarmManager.AlarmClockInfo(triggerMs, showIntent);
                alarmManager.SetAlarmClock(alarmClock, pendingIntent);
            }
        }
        catch
        {
            // Snooze-Scheduling ist best-effort
        }
    }

    private void PlayAlarmSound()
    {
        try
        {
            // Benutzerdefinierten Alarm-Ton aus Intent lesen
            var alarmTone = Intent?.GetStringExtra("alarm_tone");
            global::Android.Net.Uri? uri = null;

            if (!string.IsNullOrEmpty(alarmTone) && alarmTone.StartsWith("content://"))
            {
                // Benutzerdefinierter System-Ringtone
                uri = global::Android.Net.Uri.Parse(alarmTone);
            }

            // Fallback: System-Default
            uri ??= RingtoneManager.GetDefaultUri(RingtoneType.Alarm)
                     ?? RingtoneManager.GetDefaultUri(RingtoneType.Notification);

            _ringtone = RingtoneManager.GetRingtone(this, uri);
            if (_ringtone != null)
            {
                // Audio-Attributes fuer Alarm-Stream setzen
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                {
                    _ringtone.AudioAttributes = new AudioAttributes.Builder()
                        .SetUsage(AudioUsageKind.Alarm)!
                        .SetContentType(AudioContentType.Sonification)!
                        .Build();
                }

                // Loop aktivieren (API 28+)
                if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
                {
                    _ringtone.Looping = true;
                }

                // Langsam ansteigende Lautstärke (API 28+)
                if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
                {
                    _currentVolume = 0.1f;
                    _ringtone.Volume = _currentVolume;
                    StartVolumeRamp();
                }

                _ringtone.Play();
            }
        }
        catch
        {
            // Sound ist optional
        }
    }

    /// <summary>
    /// Erhöht die Lautstärke alle 3 Sekunden um 10% bis 100%.
    /// </summary>
    private void StartVolumeRamp()
    {
        _volumeHandler = new Handler(Looper.MainLooper!);
        _volumeRunnable = new Java.Lang.Runnable(() =>
        {
            if (_ringtone == null || !_ringtone.IsPlaying) return;
            _currentVolume = Math.Min(1.0f, _currentVolume + 0.1f);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
            {
                _ringtone.Volume = _currentVolume;
            }
            if (_currentVolume < 1.0f)
            {
                _volumeHandler?.PostDelayed(_volumeRunnable!, 3000);
            }
        });
        _volumeHandler.PostDelayed(_volumeRunnable, 3000);
    }

    private void StopAlarmSound()
    {
        try
        {
            _volumeHandler?.RemoveCallbacks(_volumeRunnable!);
            _volumeHandler = null;
            _volumeRunnable = null;
            if (_ringtone?.IsPlaying == true)
                _ringtone.Stop();
            _ringtone = null;
        }
        catch
        {
            // Cleanup ignorieren
        }
    }

    private void StartVibration()
    {
        try
        {
            _vibrator = (Vibrator?)GetSystemService(VibratorService);
            if (_vibrator?.HasVibrator == true)
            {
                var pattern = new long[] { 0, 500, 200, 500, 200, 500, 1000 };
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    _vibrator.Vibrate(VibrationEffect.CreateWaveform(pattern, 0)!); // 0 = repeat from index 0
                }
                else
                {
#pragma warning disable CA1422
                    _vibrator.Vibrate(pattern, 0);
#pragma warning restore CA1422
                }
            }
        }
        catch
        {
            // Vibration ist optional
        }
    }

    private void StopVibration()
    {
        try
        {
            _vibrator?.Cancel();
            _vibrator = null;
        }
        catch
        {
            // Cleanup ignorieren
        }
    }

    private void CancelNotification()
    {
        if (_alarmId != null)
        {
            var manager = (NotificationManager?)GetSystemService(NotificationService);
            manager?.Cancel(AndroidNotificationService.StableHash(_alarmId));
        }
    }

    protected override void OnDestroy()
    {
        _timeHandler?.RemoveCallbacks(_timeRunnable!);
        _timeHandler = null;
        _timeRunnable = null;
        _volumeHandler?.RemoveCallbacks(_volumeRunnable!);
        _volumeHandler = null;
        _volumeRunnable = null;
        StopAlarmSound();
        StopVibration();
        base.OnDestroy();
    }

    public override void OnBackPressed()
    {
        // Hinweis anzeigen, dass Dismiss oder Snooze gedrückt werden muss
        Toast.MakeText(this, GetLocalizedString("DismissAlarmHint", "Please press Dismiss or Snooze"), ToastLength.Short)?.Show();
    }

    /// <summary>
    /// Lokalisierter String aus dem DI-Container (falls verfuegbar)
    /// </summary>
    private static string GetLocalizedString(string key, string fallback)
    {
        try
        {
            var localization = ZeitManager.App.Services?.GetService<ILocalizationService>();
            return localization?.GetString(key) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private int DpToPx(int dp)
    {
        return (int)(dp * Resources!.DisplayMetrics!.Density);
    }
}
