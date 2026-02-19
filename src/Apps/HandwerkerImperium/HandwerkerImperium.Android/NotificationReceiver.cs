using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace HandwerkerImperium.Android;

/// <summary>
/// BroadcastReceiver für geplante Benachrichtigungen.
/// Wird von AlarmManager aufgerufen und zeigt die Notification an.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public class NotificationReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;

        var notificationId = intent.GetIntExtra("notification_id", 0);
        var messageKey = intent.GetStringExtra("message_key") ?? "";
        var channelId = intent.GetStringExtra("channel_id") ?? "handwerker_game";

        // Lokalisierte Nachricht laden (Fallback auf Key)
        var message = GetLocalizedMessage(context, messageKey);

        var notificationIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? "");
        var pendingIntent = PendingIntent.GetActivity(
            context,
            0,
            notificationIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(context, channelId)
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle("Handwerker Imperium")
            .SetContentText(message)
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(true)
            .SetPriority(NotificationCompat.PriorityDefault);

        var manager = NotificationManagerCompat.From(context);
        manager.Notify(notificationId, builder.Build());
    }

    private static string GetLocalizedMessage(Context context, string messageKey)
    {
        // Einfaches Mapping - im Produktionscode würde man den ResourceManager nutzen
        return messageKey switch
        {
            "ResearchDoneNotif" => "Research complete! Come collect your results.",
            "DeliveryWaitingNotif" => "A supplier is waiting with a delivery!",
            "RushAvailableNotif" => "Rush hour is available! Double your income now.",
            "DailyRewardNotif" => "Your daily reward is waiting! Don't miss it.",
            _ => messageKey
        };
    }
}
