using Android.App;
using Android.Content;
using Android.OS;
using MeineApps.Core.Ava.Services;
using FitnessRechner.Services;

namespace FitnessRechner.Android;

/// <summary>
/// Android-Implementierung für Erinnerungs-Notifications.
/// Nutzt AlarmManager + NotificationChannel.
/// </summary>
public class AndroidReminderService : ReminderService
{
    private readonly Context _context;
    private const string ChannelId = "fitness_reminders";
    private const int WaterReminderId = 1001;
    private const int WeightReminderId = 1002;
    private const int EveningSummaryId = 1003;

    public AndroidReminderService(Context context, IPreferencesService preferences) : base(preferences)
    {
        _context = context;
        CreateNotificationChannel();
    }

#pragma warning disable CA1416 // API-Level via SdkInt-Check abgesichert
    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        var channel = new NotificationChannel(ChannelId, "Fitness Erinnerungen", NotificationImportance.Default)
        {
            Description = "Erinnerungen für Wasser, Gewicht und Tages-Zusammenfassung"
        };
        channel.EnableVibration(true);

        var manager = _context.GetSystemService(Context.NotificationService) as NotificationManager;
        manager?.CreateNotificationChannel(channel);
    }
#pragma warning restore CA1416

    public override void UpdateSchedule()
    {
        var alarmManager = _context.GetSystemService(Context.AlarmService) as AlarmManager;
        if (alarmManager == null) return;

        // Alle bestehenden Alarme abbrechen
        CancelAlarm(alarmManager, WaterReminderId);
        CancelAlarm(alarmManager, WeightReminderId);
        CancelAlarm(alarmManager, EveningSummaryId);

        // Wasser-Erinnerung (wiederholend, alle X Stunden, 8-20 Uhr)
        if (IsWaterReminderEnabled)
        {
            var intervalMs = WaterReminderIntervalHours * 60 * 60 * 1000L;
            var nextTrigger = GetNextWaterReminderTime();
            ScheduleRepeating(alarmManager, WaterReminderId, nextTrigger, intervalMs,
                "Wasser trinken!", "Zeit für ein Glas Wasser 💧");
        }

        // Gewicht-Erinnerung (täglich)
        if (IsWeightReminderEnabled)
        {
            var nextTrigger = GetNextDailyTime(WeightReminderTime);
            ScheduleRepeating(alarmManager, WeightReminderId, nextTrigger, AlarmManager.IntervalDay,
                "Gewicht loggen", "Vergiss nicht, dich heute zu wiegen!");
        }

        // Abend-Zusammenfassung (täglich)
        if (IsEveningSummaryEnabled)
        {
            var nextTrigger = GetNextDailyTime(EveningSummaryTime);
            ScheduleRepeating(alarmManager, EveningSummaryId, nextTrigger, AlarmManager.IntervalDay,
                "Tages-Zusammenfassung", "Schau dir deinen Fortschritt von heute an!");
        }
    }

    private long GetNextWaterReminderTime()
    {
        var now = DateTime.Now;
        var next = now.Date.AddHours(8); // Start: 8 Uhr morgens

        // Nächsten Slot finden der in der Zukunft liegt
        while (next <= now || next.Hour >= 20)
        {
            next = next.AddHours(WaterReminderIntervalHours);
            if (next.Hour >= 20) // Nach 20 Uhr → nächster Tag 8 Uhr
                next = next.Date.AddDays(1).AddHours(8);
        }

        return DateTimeToMillis(next);
    }

    private long GetNextDailyTime(TimeSpan time)
    {
        var now = DateTime.Now;
        var next = now.Date.Add(time);
        if (next <= now) next = next.AddDays(1);
        return DateTimeToMillis(next);
    }

    private static long DateTimeToMillis(DateTime dt)
    {
        return new DateTimeOffset(dt).ToUnixTimeMilliseconds();
    }

    private void ScheduleRepeating(AlarmManager manager, int id, long triggerAtMillis, long intervalMillis,
        string title, string message)
    {
        var intent = new Intent(_context, typeof(ReminderReceiver));
        intent.PutExtra("title", title);
        intent.PutExtra("message", message);
        intent.PutExtra("id", id);

        var pendingIntent = PendingIntent.GetBroadcast(
            _context, id, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        if (pendingIntent == null) return;

        manager.SetRepeating(AlarmType.RtcWakeup, triggerAtMillis, intervalMillis, pendingIntent);
    }

    private void CancelAlarm(AlarmManager manager, int id)
    {
        var intent = new Intent(_context, typeof(ReminderReceiver));
        var pendingIntent = PendingIntent.GetBroadcast(
            _context, id, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        if (pendingIntent != null)
            manager.Cancel(pendingIntent);
    }
}

/// <summary>
/// BroadcastReceiver der Reminder-Notifications anzeigt.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public class ReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;

        var title = intent.GetStringExtra("title") ?? "FitnessRechner";
        var message = intent.GetStringExtra("message") ?? "";
        var id = intent.GetIntExtra("id", 0);

        try
        {
#pragma warning disable CA1416 // NotificationChannel erfordert API 26+, App Target ist 34+
            var builder = new Notification.Builder(context, "fitness_reminders")
                .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetAutoCancel(true);

            // Klick öffnet die App
            var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? "");
            if (launchIntent != null)
            {
                var pendingIntent = PendingIntent.GetActivity(
                    context, 0, launchIntent,
                    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
                builder.SetContentIntent(pendingIntent);
            }

            var manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
            manager?.Notify(id, builder.Build());
#pragma warning restore CA1416
        }
        catch
        {
            // Notification fehlgeschlagen - ignorieren
        }
    }
}
