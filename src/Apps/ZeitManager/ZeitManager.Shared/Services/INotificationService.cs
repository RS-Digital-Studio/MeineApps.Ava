namespace ZeitManager.Services;

public interface INotificationService
{
    Task ShowNotificationAsync(string title, string body, string? actionId = null);
    Task ScheduleNotificationAsync(string id, string title, string body, DateTime triggerAt);
    Task CancelNotificationAsync(string id);
    bool CanScheduleExactAlarms();
}
