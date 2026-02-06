using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Media;
using Android.OS;
using Android.Views;
using Android.Widget;

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
    private Handler? _timeHandler;
    private Java.Lang.Runnable? _timeRunnable;

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
            Text = "Dismiss", // Universell verstaendlich
            TextSize = 18
        };
        dismissBtn.SetTextColor(Color.White);
        var dismissBg = new GradientDrawable();
        dismissBg.SetCornerRadius(DpToPx(28));
        dismissBg.SetColor(Color.ParseColor("#6366F1"));
        dismissBtn.Background = dismissBg;
        dismissBtn.Click += (_, _) => DismissAlarm();
        container.AddView(dismissBtn);

        // Snooze-Button
        var snoozeBtn = new Button(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(
                DpToPx(260),
                DpToPx(48))
            {
                Gravity = GravityFlags.CenterHorizontal
            },
            Text = "Snooze (5 min)",
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

        // Neuen Alarm in 5 Minuten planen
        ScheduleSnooze();
        Finish();
    }

    private void ScheduleSnooze()
    {
        try
        {
            var alarmManager = (AlarmManager?)GetSystemService(AlarmService);
            if (alarmManager == null) return;

            var snoozeTime = DateTime.UtcNow.AddMinutes(5);
            var triggerMs = (long)(snoozeTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

            var title = Intent?.GetStringExtra("title") ?? "ZeitManager";
            var body = Intent?.GetStringExtra("body") ?? "Alarm!";

            var intent = new Intent(this, typeof(AlarmReceiver));
            intent.PutExtra("title", title);
            intent.PutExtra("body", body);
            intent.PutExtra("id", _alarmId + "_snooze");

            var pendingIntent = PendingIntent.GetBroadcast(
                this, Math.Abs((_alarmId + "_snooze").GetHashCode()), intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            if (pendingIntent != null)
            {
                alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMs, pendingIntent);
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
            var uri = RingtoneManager.GetDefaultUri(RingtoneType.Alarm)
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

                _ringtone.Play();
            }
        }
        catch
        {
            // Sound ist optional
        }
    }

    private void StopAlarmSound()
    {
        try
        {
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
            manager?.Cancel(Math.Abs(_alarmId.GetHashCode()));
        }
    }

    protected override void OnDestroy()
    {
        _timeHandler?.RemoveCallbacks(_timeRunnable!);
        _timeHandler = null;
        _timeRunnable = null;
        StopAlarmSound();
        StopVibration();
        base.OnDestroy();
    }

    public override void OnBackPressed()
    {
        // Back-Button soll den Alarm nicht versehentlich schliessen
        // Nutzer muss Dismiss oder Snooze druecken
    }

    private int DpToPx(int dp)
    {
        return (int)(dp * Resources!.DisplayMetrics!.Density);
    }
}
