namespace WorkTimePro.Services;

public interface INotificationService
{
    Task ShowNotificationAsync(string title, string body, string? actionId = null);
    Task ScheduleNotificationAsync(string id, string title, string body, DateTime triggerAt);
    Task CancelNotificationAsync(string id);
    bool CanScheduleExactAlarms();

    /// <summary>
    /// Öffnet die System-Einstellung, in der der Nutzer exakte Alarme für die App
    /// erlauben kann (Android 12+: "Wecker und Erinnerungen"). Desktop: No-Op.
    /// </summary>
    void RequestExactAlarmPermission();

    /// <summary>
    /// Sind System-Benachrichtigungen für die App aktiviert?
    /// (Android: POST_NOTIFICATIONS / NotificationManagerCompat; Desktop: immer true.)
    /// </summary>
    bool AreNotificationsEnabled();
}
