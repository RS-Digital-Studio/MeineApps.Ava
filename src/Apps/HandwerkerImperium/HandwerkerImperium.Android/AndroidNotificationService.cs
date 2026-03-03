using Android.App;
using Android.Content;
using Android.OS;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Android;

/// <summary>
/// Android-Implementierung für lokale Push-Benachrichtigungen.
/// Nutzt AlarmManager + BroadcastReceiver für zeitgesteuerte Notifications.
/// Persistiert geplante Alarme in SharedPreferences für Boot-Recovery.
/// </summary>
public class AndroidNotificationService : INotificationService
{
    private readonly Context _context;
    private const string ChannelId = "handwerker_game";
    private const string ChannelName = "HandwerkerImperium";
    private const string PrefsName = "notification_schedule";

    // Notification IDs
    internal const int ResearchCompleteId = 1001;
    internal const int DeliveryReminderId = 1002;
    internal const int RushAvailableId = 1003;
    internal const int DailyRewardId = 1004;

    // Alle bekannten Notification-IDs für Iteration
    internal static readonly int[] AllNotificationIds =
        [ResearchCompleteId, DeliveryReminderId, RushAvailableId, DailyRewardId];

    public AndroidNotificationService(Context context)
    {
        _context = context;
        CreateNotificationChannel();
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.Default)
        {
            Description = "Spielbenachrichtigungen"
        };

        var manager = (NotificationManager?)_context.GetSystemService(Context.NotificationService);
        manager?.CreateNotificationChannel(channel);
    }

    public void ScheduleGameNotifications(Models.GameState state)
    {
        if (!state.NotificationsEnabled) return;

        CancelAllNotifications();

        // 1. Forschung abgeschlossen
        if (state.ActiveResearchId != null)
        {
            var activeResearch = state.Researches?.FirstOrDefault(r => r.Id == state.ActiveResearchId);
            if (activeResearch?.StartedAt != null)
            {
                var endTime = activeResearch.StartedAt.Value + activeResearch.Duration;
                if (endTime > DateTime.UtcNow)
                {
                    var delay = endTime - DateTime.UtcNow;
                    ScheduleNotification(ResearchCompleteId, "ResearchDoneNotif", (long)delay.TotalMilliseconds);
                }
            }
        }

        // 2. Lieferant wartet (3 Minuten nach App-Close)
        ScheduleNotification(DeliveryReminderId, "DeliveryWaitingNotif", 3 * 60 * 1000);

        // 3. Tägliche Belohnung (nächster Tag 10:00 Uhr)
        var now = DateTime.UtcNow;
        var nextReward = now.Date.AddDays(1).AddHours(10);
        if (nextReward > now)
        {
            var delay = nextReward - now;
            ScheduleNotification(DailyRewardId, "DailyRewardNotif", (long)delay.TotalMilliseconds);
        }

        // 4. Feierabend-Rush (18:00 UTC → wird lokal angezeigt)
        var rushNow = DateTime.UtcNow;
        var rushTime = rushNow.Date.AddHours(18);
        if (rushTime <= rushNow) rushTime = rushTime.AddDays(1);
        var rushDelay = rushTime - rushNow;
        ScheduleNotification(RushAvailableId, "RushAvailableNotif", (long)rushDelay.TotalMilliseconds);
    }

    public void CancelAllNotifications()
    {
        var alarmManager = (AlarmManager?)_context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        foreach (var id in AllNotificationIds)
            CancelAlarm(alarmManager, id);

        // Persistierte Daten löschen
        var prefs = _context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
        prefs?.Edit()?.Clear()?.Apply();
    }

    private void ScheduleNotification(int notificationId, string messageKey, long delayMs)
    {
        var alarmManager = (AlarmManager?)_context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(_context, typeof(NotificationReceiver));
        intent.PutExtra("notification_id", notificationId);
        intent.PutExtra("message_key", messageKey);
        intent.PutExtra("channel_id", ChannelId);

        var pendingIntent = PendingIntent.GetBroadcast(
            _context,
            notificationId,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var triggerTime = Java.Lang.JavaSystem.CurrentTimeMillis() + delayMs;

        if (pendingIntent != null)
        {
            alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerTime, pendingIntent);

            // Alarm-Daten persistieren für Boot-Recovery
            PersistAlarm(notificationId, messageKey, triggerTime);
        }
    }

    /// <summary>
    /// Speichert Alarm-Daten in SharedPreferences, damit der BootReceiver
    /// sie nach einem Geräte-Neustart neu planen kann.
    /// </summary>
    private void PersistAlarm(int notificationId, string messageKey, long triggerTimeMs)
    {
        var prefs = _context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
        var editor = prefs?.Edit();
        if (editor == null) return;

        editor.PutString($"msg_{notificationId}", messageKey);
        editor.PutLong($"trigger_{notificationId}", triggerTimeMs);
        editor.Apply();
    }

    private void CancelAlarm(AlarmManager alarmManager, int notificationId)
    {
        var intent = new Intent(_context, typeof(NotificationReceiver));
        var pendingIntent = PendingIntent.GetBroadcast(
            _context,
            notificationId,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        if (pendingIntent != null)
        {
            alarmManager.Cancel(pendingIntent);
        }
    }

    /// <summary>
    /// Statische Methode für den BootReceiver: Liest persistierte Alarme
    /// aus SharedPreferences und plant sie erneut.
    /// </summary>
    internal static void RescheduleFromPreferences(Context context)
    {
        var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
        if (prefs == null) return;

        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var now = Java.Lang.JavaSystem.CurrentTimeMillis();

        foreach (var id in AllNotificationIds)
        {
            var messageKey = prefs.GetString($"msg_{id}", null);
            var triggerTime = prefs.GetLong($"trigger_{id}", 0);

            // Nur noch gültige (zukünftige) Alarme neu planen
            if (messageKey == null || triggerTime <= now) continue;

            var intent = new Intent(context, typeof(NotificationReceiver));
            intent.PutExtra("notification_id", id);
            intent.PutExtra("message_key", messageKey);
            intent.PutExtra("channel_id", ChannelId);

            var pendingIntent = PendingIntent.GetBroadcast(
                context, id, intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            if (pendingIntent != null)
            {
                alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerTime, pendingIntent);
            }
        }
    }
}
