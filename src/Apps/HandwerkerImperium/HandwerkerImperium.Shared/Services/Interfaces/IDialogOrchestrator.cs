namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Koordiniert die Dialog-/Overlay-Dismiss-Kaskade fuer die Back-Taste.
///  (Schritt 6 aus velvety-booping-peacock-Plan): Die Kaskade aus MainViewModel.HandleBackPressed
/// (Zeilen 244-262 der alten Navigation.cs) lebt jetzt hier. MainViewModel.HandleBackPressed delegiert
/// zuerst an <see cref="TryDismissTopmost"/> bevor es den Navigation-Stack oder Double-Back-Exit anfasst.
/// </summary>
public interface IDialogOrchestrator
{
    void AttachHost(INavigationHost host);

    /// <summary>
    /// Versucht, den obersten Dialog/Overlay in strikter Reihenfolge zu schliessen.
    /// Gibt true zurueck, wenn ein Element konsumiert wurde.
    /// Reihenfolge: Hint → LuckySpin → CombinedWelcome → WelcomeOffer → Confirm → PrestigeSummary
    /// → Alert → Achievement → LevelUp → OfflineEarnings → DailyReward → Story → WorkerProfile.
    /// </summary>
    bool TryDismissTopmost();
}
