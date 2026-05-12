using Android.App;
using Android.Content;

namespace BomberBlast.Droid;

/// <summary>
/// Broadcast-Receiver fuer lokale AlarmManager-Notifications (Daily-Reward-Reminder usw.).
/// Wird vom AndroidPushNotificationService.ScheduleLocalNotification als PendingIntent-Target
/// registriert und feuert nach Ablauf des Alarms — baut dann die Notification und postet sie.
/// </summary>
[Android.Runtime.Register("org.rsdigital.bomberblast.NotificationReceiver")]
[BroadcastReceiver(Name = "org.rsdigital.bomberblast.NotificationReceiver", Exported = false)]
public sealed class NotificationReceiver : BroadcastReceiver
{
    public const string ActionShowLocal = "org.rsdigital.bomberblast.SHOW_LOCAL_NOTIFICATION";
    public const string ExtraId = "id";
    public const string ExtraTitle = "title";
    public const string ExtraBody = "body";
    public const string ExtraChannel = "channel";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;
        if (intent.Action != ActionShowLocal) return;

        var id = intent.GetStringExtra(ExtraId) ?? string.Empty;
        var title = intent.GetStringExtra(ExtraTitle) ?? "BomberBlast";
        var body = intent.GetStringExtra(ExtraBody) ?? string.Empty;
        var channelId = intent.GetStringExtra(ExtraChannel) ?? "bomberblast_liveops";

        try
        {
            ShowNotification(context, id, title, body, channelId);
        }
        catch
        {
            // Best-Effort: Local-Notification soll niemals den Receiver-Prozess crashen
        }
    }

    private static void ShowNotification(Context context, string id, string title, string body, string channelId)
    {
        // Tap-Action: MainActivity oeffnen, vorhandene Instanz wiederverwenden
        var openIntent = new Intent(context, typeof(MainActivity));
        openIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        var pendingIntent = PendingIntent.GetActivity(
            context,
            requestCode: id.GetHashCode(),
            openIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var iconResId = context.ApplicationInfo?.Icon ?? Android.Resource.Drawable.IcDialogInfo;

        Notification notification;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            notification = new Notification.Builder(context, channelId)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetStyle(new Notification.BigTextStyle().BigText(body))
                .SetSmallIcon(iconResId)
                .SetAutoCancel(true)
                .SetContentIntent(pendingIntent)
                .Build();
        }
        else
        {
#pragma warning disable CA1422 // API 24+25 brauchen den deprecated Constructor
            notification = new Notification.Builder(context)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetSmallIcon(iconResId)
                .SetAutoCancel(true)
                .SetContentIntent(pendingIntent)
                .Build();
#pragma warning restore CA1422
        }

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        // ID-Hash als Notification-ID, damit dieselbe Reminder-ID die alte Notification ersetzt
        manager?.Notify(id.GetHashCode(), notification);
    }
}
