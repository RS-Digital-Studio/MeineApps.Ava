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
        // NotificationChannel-API erst ab API 26 (Oreo) verfügbar. OperatingSystem-Guard,
        // damit der CA1416-Analyzer den geschützten Zweig erkennt.
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;

        var context = Application.Context;
        if (context == null) return;
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
        if (context == null) return Task.CompletedTask;

        var manager = NotificationManagerCompat.From(context);
        if (manager == null || !manager.AreNotificationsEnabled())
            return Task.CompletedTask;

        // Setter werden einzeln auf der nicht-null Builder-Instanz aufgerufen (statt Fluent-Chain),
        // weil die AndroidX-Bindings für SetXxx() einen nullable Builder zurückgeben (CS8602 im Chain).
        var builder = new NotificationCompat.Builder(context, ChannelId);
        builder.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo);
        builder.SetContentTitle(title);
        builder.SetContentText(body);
        builder.SetPriority(NotificationCompat.PriorityHigh);
        builder.SetAutoCancel(true);
        builder.SetCategory(NotificationCompat.CategoryReminder);

        var notification = builder.Build();
        if (notification != null)
            manager.Notify(StableHash(actionId ?? "default"), notification);

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
        var canExact = !OperatingSystem.IsAndroidVersionAtLeast(31) || alarmManager.CanScheduleExactAlarms();

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
        if (!OperatingSystem.IsAndroidVersionAtLeast(31)) return true;
        var context = Application.Context;
        if (context == null) return false;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        return alarmManager?.CanScheduleExactAlarms() ?? false;
    }

    public bool AreNotificationsEnabled()
    {
        var context = Application.Context;
        if (context == null) return false;
        var manager = NotificationManagerCompat.From(context);
        return manager != null && manager.AreNotificationsEnabled();
    }

    public Task CancelNotificationAsync(string id)
    {
        var context = Application.Context;
        if (context == null) return Task.CompletedTask;
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
        manager?.Cancel(StableHash(id));

        return Task.CompletedTask;
    }
}
