namespace FinanzRechner.Services;

/// <summary>
/// Service für lokale Benachrichtigungen
/// </summary>
public interface INotificationService
{
    Task SendBudgetAlertAsync(string categoryName, decimal percentageUsed, decimal spent, decimal limit);
    Task<bool> AreNotificationsAllowedAsync();
    Task<bool> RequestNotificationPermissionAsync();
}
