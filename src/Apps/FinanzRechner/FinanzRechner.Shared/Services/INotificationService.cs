namespace FinanzRechner.Services;

/// <summary>
/// Service für lokale Benachrichtigungen
/// </summary>
public interface INotificationService
{
    Task SendBudgetAlertAsync(string categoryName, double percentageUsed, double spent, double limit);
    Task<bool> AreNotificationsAllowedAsync();
    Task<bool> RequestNotificationPermissionAsync();
}
