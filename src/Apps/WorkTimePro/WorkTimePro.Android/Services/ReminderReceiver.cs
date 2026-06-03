using Android.App;
using Android.Content;
using AndroidX.Core.App;

namespace WorkTimePro.Android.Services;

/// <summary>
/// BroadcastReceiver für geplante Reminder-Notifications.
/// Wird von AlarmManager gefeuert (auch wenn App geschlossen).
/// </summary>
[BroadcastReceiver(Exported = false)]
public class ReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;

        var title = intent.GetStringExtra("title") ?? "WorkTimePro";
        var body = intent.GetStringExtra("body") ?? "";
        var id = intent.GetStringExtra("id") ?? "reminder";

        ShowNotification(context, title, body, id);
    }

    private static void ShowNotification(Context context, string title, string body, string id)
    {
        var manager = NotificationManagerCompat.From(context);
        if (manager == null || !manager.AreNotificationsEnabled())
            return;

        // Tap öffnet die App
        var tapIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? "");
        PendingIntent? pendingTapIntent = null;
        if (tapIntent != null)
        {
            tapIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            pendingTapIntent = PendingIntent.GetActivity(
                context, 0, tapIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        }

        // Setter werden einzeln auf der nicht-null Builder-Instanz aufgerufen (statt Fluent-Chain),
        // weil die AndroidX-Bindings für SetXxx() einen nullable Builder zurückgeben (CS8602 im Chain).
        var builder = new NotificationCompat.Builder(context, "worktimepro_reminder");
        builder.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo);
        builder.SetContentTitle(title);
        builder.SetContentText(body);
        builder.SetPriority(NotificationCompat.PriorityHigh);
        builder.SetAutoCancel(true);
        builder.SetCategory(NotificationCompat.CategoryReminder);

        if (pendingTapIntent != null)
            builder.SetContentIntent(pendingTapIntent);

        var notification = builder.Build();
        if (notification != null)
            manager.Notify(AndroidNotificationService.StableHash(id), notification);
    }
}
