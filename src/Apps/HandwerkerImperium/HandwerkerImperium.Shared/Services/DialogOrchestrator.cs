using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Koordiniert die Back-Press-Dismiss-Kaskade (Schritt 6 aus velvety-booping-peacock-Plan).
/// Die Reihenfolge ist 1:1 identisch mit der alten MainViewModel.HandleBackPressed-Methode
/// (Zeilen 244-262 aus der alten Navigation.cs). Unit-testbar via Mock-Host.
/// </summary>
public sealed class DialogOrchestrator : IDialogOrchestrator
{
    private INavigationHost? _host;

    public void AttachHost(INavigationHost host) => _host = host;

    /// <summary>
    /// Reihenfolge (strikt!):
    /// Hint → LuckySpin → CombinedWelcome → WelcomeOffer → Confirm → PrestigeSummary → Alert
    /// → Achievement → LevelUp → OfflineEarnings → DailyReward → Story → WorkerProfile.
    /// </summary>
    public bool TryDismissTopmost()
    {
        if (_host == null) return false;
        var dlg = _host.DialogVM;
        var missions = _host.MissionsVM;

        if (dlg.IsHintVisible) { dlg.DismissHintCommand.Execute(null); return true; }
        if (_host.IsLuckySpinVisible) { _host.HideLuckySpinOverlay(); return true; }
        if (_host.IsCombinedWelcomeDialogVisible) { _host.DismissCombinedDialog(); return true; }
        if (missions.IsWelcomeOfferVisible) { missions.DismissWelcomeOfferCommand.Execute(null); return true; }
        if (dlg.IsConfirmDialogVisible) { dlg.ConfirmDialogCancelCommand.Execute(null); return true; }
        if (dlg.IsPrestigeSummaryVisible) { dlg.DismissPrestigeSummaryCommand.Execute(null); return true; }
        if (dlg.IsAlertDialogVisible) { dlg.DismissAlertDialogCommand.Execute(null); return true; }
        if (dlg.IsAchievementDialogVisible) { dlg.DismissAchievementDialogCommand.Execute(null); return true; }
        if (dlg.IsLevelUpDialogVisible) { dlg.DismissLevelUpDialogCommand.Execute(null); return true; }
        if (_host.IsOfflineEarningsDialogVisible) { _host.CollectOfflineEarningsNormal(); return true; }
        if (_host.IsDailyRewardDialogVisible) { _host.IsDailyRewardDialogVisible = false; _host.CheckDeferredDialogs(); return true; }
        if (dlg.IsStoryDialogVisible) { dlg.DismissStoryDialogCommand.Execute(null); return true; }

        // Overlay schliessen (ActivePage bleibt erhalten)
        if (_host.IsWorkerProfileActive)
        {
            _host.IsWorkerProfileActive = false;
            return true;
        }

        return false;
    }
}
