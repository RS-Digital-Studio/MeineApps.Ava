using Android.App;
using Android.Content;
using Android.OS;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Android;

/// <summary>
/// Android-Implementierung für lokale Push-Benachrichtigungen.
/// Nutzt AlarmManager + BroadcastReceiver für zeitgesteuerte Notifications.
/// </summary>
public class AndroidNotificationService : INotificationService
{
    private readonly Activity _activity;
    private const string ChannelId = "handwerker_game";
    private const string ChannelName = "HandwerkerImperium";

    // Notification IDs
    private const int ResearchCompleteId = 1001;
    private const int DeliveryReminderId = 1002;
    private const int RushAvailableId = 1003;
    private const int DailyRewardId = 1004;

    public AndroidNotificationService(Activity activity)
    {
        _activity = activity;
        CreateNotificationChannel();
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.Default)
        {
            Description = "Spielbenachrichtigungen"
        };

        var manager = (NotificationManager?)_activity.GetSystemService(Context.NotificationService);
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

        // 4. Feierabend-Rush (18:00 lokale Zeit)
        var localNow = DateTime.Now;
        var rushTime = localNow.Date.AddHours(18);
        if (rushTime <= localNow) rushTime = rushTime.AddDays(1);
        var rushDelay = rushTime - localNow;
        ScheduleNotification(RushAvailableId, "RushAvailableNotif", (long)rushDelay.TotalMilliseconds);
    }

    public void CancelAllNotifications()
    {
        var alarmManager = (AlarmManager?)_activity.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        CancelAlarm(alarmManager, ResearchCompleteId);
        CancelAlarm(alarmManager, DeliveryReminderId);
        CancelAlarm(alarmManager, RushAvailableId);
        CancelAlarm(alarmManager, DailyRewardId);
    }

    private void ScheduleNotification(int notificationId, string messageKey, long delayMs)
    {
        var alarmManager = (AlarmManager?)_activity.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(_activity, typeof(NotificationReceiver));
        intent.PutExtra("notification_id", notificationId);
        intent.PutExtra("message_key", messageKey);
        intent.PutExtra("channel_id", ChannelId);

        var pendingIntent = PendingIntent.GetBroadcast(
            _activity,
            notificationId,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var triggerTime = Java.Lang.JavaSystem.CurrentTimeMillis() + delayMs;

        if (pendingIntent != null)
        {
            alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerTime, pendingIntent);
        }
    }

    private void CancelAlarm(AlarmManager alarmManager, int notificationId)
    {
        var intent = new Intent(_activity, typeof(NotificationReceiver));
        var pendingIntent = PendingIntent.GetBroadcast(
            _activity,
            notificationId,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        if (pendingIntent != null)
        {
            alarmManager.Cancel(pendingIntent);
        }
    }
}
