namespace BomberBlast.Services;

/// <summary>
/// FCM Push-Notifications + lokale Daily-Reminder.
/// Code-Hooks vorbereitet (v2.0.44 — AAA-Audit). FCM-Console-Setup macht Robert.
///
/// Topics werden serverseitig gesteuert (Saison-Ende, Daily-Race-Reset),
/// lokale Notifications kommen aus AlarmManager (Daily-Reward-Reminder).
/// </summary>
public interface IPushNotificationService
{
    /// <summary>Initialisierung beim App-Start. Erfragt POST_NOTIFICATIONS-Permission auf Android 13+.</summary>
    Task InitializeAsync();

    /// <summary>Ob Notifications grundsätzlich erlaubt sind (System-Setting).</summary>
    bool ArePermissionsGranted { get; }

    /// <summary>Permission anfragen (Android 13+ runtime-permission).</summary>
    Task<bool> RequestPermissionAsync();

    /// <summary>Topic-Subscription (Saison-Reminders etc.).</summary>
    Task SubscribeToTopicAsync(string topic);

    /// <summary>Topic-Subscription beenden.</summary>
    Task UnsubscribeFromTopicAsync(string topic);

    /// <summary>
    /// Lokale Daily-Notification planen (z.B. "Dein Daily-Reward wartet!").
    /// triggerDate UTC. Kanal steuert Sound/Vibration auf Android 8+.
    /// </summary>
    void ScheduleLocalNotification(string id, DateTime triggerUtc, string title, string body, NotificationChannel channel);

    /// <summary>Lokale Notification abbrechen (z.B. User hat Reward bereits gesammelt).</summary>
    void CancelLocalNotification(string id);

    /// <summary>FCM-Token für Server-seitige Push-Sends. Null wenn nicht verfügbar.</summary>
    string? FcmToken { get; }

    event EventHandler<string>? FcmTokenChanged;
}

/// <summary>Notification-Channels (Android 8+ Quality-of-Service).</summary>
public enum NotificationChannel
{
    /// <summary>Daily-Reminders, Stille mit Standard-Vibration.</summary>
    DailyRewards,
    /// <summary>Battle-Pass / Liga / Events. Mit Sound.</summary>
    LiveOps,
    /// <summary>Wichtige Events (Saison-Ende, Cloud-Save-Konflikt). Mit Sound + Vibration.</summary>
    Important
}

/// <summary>
/// Standard-Notification-Topics (Server-seitig pushbar).
/// </summary>
public static class NotificationTopics
{
    public const string AllUsers = "all";
    public const string SeasonReminders = "season_reminders";
    public const string DailyRaceReset = "daily_race_reset";
    public const string EventReminders = "event_reminders";
    public const string ComebackUsers = "comeback_users"; // >7 Tage inaktiv
}

/// <summary>No-Op für Desktop / nicht konfigurierte FCM-Setup.</summary>
public sealed class NullPushNotificationService : IPushNotificationService
{
    public Task InitializeAsync() => Task.CompletedTask;
    public bool ArePermissionsGranted => false;
    public Task<bool> RequestPermissionAsync() => Task.FromResult(false);
    public Task SubscribeToTopicAsync(string topic) => Task.CompletedTask;
    public Task UnsubscribeFromTopicAsync(string topic) => Task.CompletedTask;
    public void ScheduleLocalNotification(string id, DateTime triggerUtc, string title, string body, NotificationChannel channel) { }
    public void CancelLocalNotification(string id) { }
    public string? FcmToken => null;
#pragma warning disable CS0067 // Null-Service: Event wird nie gefeuert
    public event EventHandler<string>? FcmTokenChanged;
#pragma warning restore CS0067
}
