using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace ZeitManager.Android.Services;

/// <summary>
/// BroadcastReceiver fuer geplante Alarme.
/// Wird von AlarmManager gefeuert (auch wenn App komplett geschlossen).
/// Wenn App im Vordergrund → AlarmSchedulerService übernimmt (In-App Overlay).
/// Wenn App im Hintergrund → AlarmActivity für Fullscreen-Anzeige ueber Lockscreen.
/// </summary>
[BroadcastReceiver(Exported = false)]
public class AlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;

        var title = intent.GetStringExtra("title") ?? "ZeitManager";
        var body = intent.GetStringExtra("body") ?? "Alarm!";
        var id = intent.GetStringExtra("id") ?? "alarm";
        var snoozeDuration = intent.GetIntExtra("snooze_duration", 5);
        var alarmTone = intent.GetStringExtra("alarm_tone");

        // Wenn App im Vordergrund → AlarmSchedulerService zeigt In-App Overlay
        // AlarmActivity wird dann nicht gestartet (verhindert Doppel-Alarm)
        if (MainActivity.IsAppInForeground)
            return;

        // WakeLock damit das Geraet aufwacht
        AcquireWakeLock(context);

        // AlarmActivity direkt starten (zeigt Fullscreen ueber Lockscreen)
        var activityIntent = new Intent(context, typeof(AlarmActivity));
        activityIntent.AddFlags(ActivityFlags.NewTask
                              | ActivityFlags.ClearTop
                              | ActivityFlags.NoAnimation);
        activityIntent.PutExtra("title", title);
        activityIntent.PutExtra("body", body);
        activityIntent.PutExtra("id", id);
        activityIntent.PutExtra("snooze_duration", snoozeDuration);
        if (!string.IsNullOrEmpty(alarmTone))
            activityIntent.PutExtra("alarm_tone", alarmTone);
        context.StartActivity(activityIntent);

        // Backup-Notification erstellen (Heads-Up + Notification-Leiste)
        ShowBackupNotification(context, title, body, id, snoozeDuration, alarmTone);
    }

    /// <summary>
    /// Erstellt eine High-Priority Notification als Backup.
    /// Die eigentliche Alarm-Anzeige laeuft ueber AlarmActivity.
    /// </summary>
    private static void ShowBackupNotification(Context context, string title, string body, string id,
        int snoozeDuration, string? alarmTone)
    {
        var notificationManager = NotificationManagerCompat.From(context);
        if (notificationManager is null || !notificationManager.AreNotificationsEnabled())
            return;

        var tapIntent = new Intent(context, typeof(AlarmActivity));
        tapIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        tapIntent.PutExtra("title", title);
        tapIntent.PutExtra("body", body);
        tapIntent.PutExtra("id", id);
        tapIntent.PutExtra("snooze_duration", snoozeDuration);
        if (!string.IsNullOrEmpty(alarmTone))
            tapIntent.PutExtra("alarm_tone", alarmTone);

        var stableId = AndroidNotificationService.StableHash(id);
        var pendingTapIntent = PendingIntent.GetActivity(
            context, stableId, tapIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(context, AndroidNotificationService.AlarmChannelIdV2);
        builder.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo);
        builder.SetContentTitle(title);
        builder.SetContentText(body);
        builder.SetPriority(NotificationCompat.PriorityMax);
        builder.SetAutoCancel(true);
        builder.SetCategory(NotificationCompat.CategoryAlarm);
        builder.SetContentIntent(pendingTapIntent);
        builder.SetFullScreenIntent(pendingTapIntent, true);
        builder.SetOngoing(true); // Nicht wegwischbar solange Alarm aktiv

        var notification = builder.Build();
        if (notification is null) return;
        notificationManager.Notify(stableId, notification);
    }

    private static void AcquireWakeLock(Context context)
    {
        try
        {
            var powerManager = (PowerManager?)context.GetSystemService(Context.PowerService);
            var wakeLock = powerManager?.NewWakeLock(WakeLockFlags.Partial | WakeLockFlags.AcquireCausesWakeup,
                "ZeitManager::AlarmWakeLock");
            wakeLock?.Acquire(10_000); // 10 Sekunden reichen fuer Activity-Start
        }
        catch
        {
            // WakeLock ist optional
        }
    }
}
