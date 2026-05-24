namespace FinanzRechner.Services;

public sealed class NotificationService : INotificationService
{
    public Task SendBudgetAlertAsync(string categoryName, decimal percentageUsed, decimal spent, decimal limit)
    {
        // Desktop: Keine Aktion, kann mit plattformspezifischen Benachrichtigungen erweitert werden
        return Task.CompletedTask;
    }

    public Task<bool> AreNotificationsAllowedAsync()
    {
        return Task.FromResult(true);
    }

    public Task<bool> RequestNotificationPermissionAsync()
    {
        return Task.FromResult(true);
    }
}
