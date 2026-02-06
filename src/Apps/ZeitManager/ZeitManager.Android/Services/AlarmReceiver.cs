using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace ZeitManager.Android.Services;

/// <summary>
/// BroadcastReceiver fuer geplante Alarme.
/// Wird von AlarmManager gefeuert (auch wenn App komplett geschlossen).
/// Startet AlarmActivity direkt fuer Fullscreen-Anzeige ueber dem Lockscreen
/// und erstellt zusaetzlich eine Notification als Backup.
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

        // WakeLock damit das Geraet aufwacht
        AcquireWakeLock(context);

        // AlarmActivity direkt starten (zeigt Fullscreen ueber Lockscreen)
        // Das ist der Standard-Ansatz fuer Wecker-Apps auf Android
        var activityIntent = new Intent(context, typeof(AlarmActivity));
        activityIntent.AddFlags(ActivityFlags.NewTask
                              | ActivityFlags.ClearTop
                              | ActivityFlags.NoAnimation);
        activityIntent.PutExtra("title", title);
        activityIntent.PutExtra("body", body);
        activityIntent.PutExtra("id", id);
        context.StartActivity(activityIntent);

        // Backup-Notification erstellen (fuer Heads-Up wenn Bildschirm an,
        // und damit Nutzer den Alarm in der Notification-Leiste sehen)
        ShowBackupNotification(context, title, body, id);
    }

    /// <summary>
    /// Erstellt eine High-Priority Notification als Backup.
    /// Die eigentliche Alarm-Anzeige laeuft ueber AlarmActivity.
    /// </summary>
    private static void ShowBackupNotification(Context context, string title, string body, string id)
    {
        if (!NotificationManagerCompat.From(context).AreNotificationsEnabled())
            return;

        var tapIntent = new Intent(context, typeof(AlarmActivity));
        tapIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        tapIntent.PutExtra("title", title);
        tapIntent.PutExtra("body", body);
        tapIntent.PutExtra("id", id);
        var pendingTapIntent = PendingIntent.GetActivity(
            context, Math.Abs(id.GetHashCode()), tapIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(context, AndroidNotificationService.AlarmChannelIdV2)
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetPriority(NotificationCompat.PriorityMax)
            .SetAutoCancel(true)
            .SetCategory(NotificationCompat.CategoryAlarm)
            .SetContentIntent(pendingTapIntent)
            .SetFullScreenIntent(pendingTapIntent, true)
            .SetOngoing(true); // Nicht wegwischbar solange Alarm aktiv

        var manager = NotificationManagerCompat.From(context);
        manager.Notify(Math.Abs(id.GetHashCode()), builder.Build());
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
