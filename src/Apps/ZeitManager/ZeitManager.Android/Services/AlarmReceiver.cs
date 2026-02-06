using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using AndroidX.Core.App;

namespace ZeitManager.Android.Services;

[BroadcastReceiver(Exported = false)]
public class AlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;

        var title = intent.GetStringExtra("title") ?? "ZeitManager";
        var body = intent.GetStringExtra("body") ?? "Alarm!";
        var id = intent.GetStringExtra("id") ?? "alarm";

        // Check notification permission
        if (!NotificationManagerCompat.From(context).AreNotificationsEnabled())
            return;

        // WakeLock damit das Geraet aufwacht
        AcquireWakeLock(context);

        // Intent um die App zu oeffnen wenn Notification angetippt wird
        var tapIntent = new Intent(context, typeof(MainActivity));
        tapIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
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
            .SetDefaults((int)NotificationDefaults.Vibrate);

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
            wakeLock?.Acquire(10_000); // 10 Sekunden reichen fuer Notification
        }
        catch
        {
            // WakeLock ist optional
        }
    }
}
