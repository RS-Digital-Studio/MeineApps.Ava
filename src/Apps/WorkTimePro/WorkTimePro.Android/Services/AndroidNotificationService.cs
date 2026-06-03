using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using WorkTimePro.Services;

namespace WorkTimePro.Android.Services;

public sealed class AndroidNotificationService : INotificationService
{
    private const string ChannelId = "worktimepro_reminder";

    /// <summary>
    /// Deterministischer Hash (stabil zwischen Neustarts)
    /// </summary>
    internal static int StableHash(string input)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in input)
                hash = hash * 31 + c;
            return Math.Abs(hash);
        }
    }

    public AndroidNotificationService()
    {
        CreateNotificationChannel();
    }

    private static void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        var context = Application.Context;
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager == null) return;

        // Lokalisierter Channel-Name (in den Android-System-Einstellungen sichtbar).
        // CreateNotificationChannel ist idempotent — überschreibt Name/Description bei Sprachwechsel.
        var channelName = WorkTimePro.Resources.Strings.AppStrings.Reminders;
        var channel = new NotificationChannel(ChannelId, channelName, NotificationImportance.High)
        {
            Description = $"WorkTimePro — {channelName}"
        };
        channel.EnableVibration(true);

        manager.CreateNotificationChannel(channel);
    }

    public Task ShowNotificationAsync(string title, string body, string? actionId = null)
    {
        var context = Application.Context;

        if (!NotificationManagerCompat.From(context).AreNotificationsEnabled())
            return Task.CompletedTask;

        var builder = new NotificationCompat.Builder(context, ChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetAutoCancel(true)
            .SetCategory(NotificationCompat.CategoryReminder);

        var manager = NotificationManagerCompat.From(context);
        manager.Notify(StableHash(actionId ?? "default"), builder.Build());

        return Task.CompletedTask;
    }

    public Task ScheduleNotificationAsync(string id, string title, string body, DateTime triggerAt)
    {
        var context = Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return Task.CompletedTask;

        // Exact-Alarm-Permission prüfen (Android 12+). Fehlt sie (vom System/Nutzer entzogen),
        // NICHT still abbrechen — sonst feuert die Erinnerung gar nicht. Stattdessen auf den
        // inexakten, Doze-tauglichen Alarm zurückfallen (Reminder darf etwas ungenau sein).
        var canExact = Build.VERSION.SdkInt < BuildVersionCodes.S || alarmManager.CanScheduleExactAlarms();

        var intent = new Intent(context, typeof(ReminderReceiver));
        intent.PutExtra("title", title);
        intent.PutExtra("body", body);
        intent.PutExtra("id", id);

        var pendingIntent = PendingIntent.GetBroadcast(
            context, StableHash(id), intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var triggerMs = (long)(triggerAt.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        if (pendingIntent != null)
        {
            if (canExact)
                alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMs, pendingIntent);
            else
                // Fallback: inexakter Doze-tauglicher Alarm (feuert ggf. etwas verzögert, aber feuert)
                alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMs, pendingIntent);
        }

        return Task.CompletedTask;
    }

    public bool CanScheduleExactAlarms()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.S) return true;
        var alarmManager = (AlarmManager?)Application.Context.GetSystemService(Context.AlarmService);
        return alarmManager?.CanScheduleExactAlarms() ?? false;
    }

    public bool AreNotificationsEnabled()
        => NotificationManagerCompat.From(Application.Context).AreNotificationsEnabled();

    public Task CancelNotificationAsync(string id)
    {
        var context = Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return Task.CompletedTask;

        var intent = new Intent(context, typeof(ReminderReceiver));
        var pendingIntent = PendingIntent.GetBroadcast(
            context, StableHash(id), intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        if (pendingIntent != null)
        {
            alarmManager.Cancel(pendingIntent);
        }

        // Auch angezeigte Notification entfernen
        var manager = NotificationManagerCompat.From(context);
        manager.Cancel(StableHash(id));

        return Task.CompletedTask;
    }
}
