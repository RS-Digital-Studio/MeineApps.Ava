namespace WorkTimePro.Services;

public interface INotificationService
{
    Task ShowNotificationAsync(string title, string body, string? actionId = null);
    Task ScheduleNotificationAsync(string id, string title, string body, DateTime triggerAt);
    Task CancelNotificationAsync(string id);
    bool CanScheduleExactAlarms();

    /// <summary>
    /// Sind System-Benachrichtigungen für die App aktiviert?
    /// (Android: POST_NOTIFICATIONS / NotificationManagerCompat; Desktop: immer true.)
    /// </summary>
    bool AreNotificationsEnabled();
}
