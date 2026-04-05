using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet Schnell-Auftraege (Quick Jobs) die alle 15 Minuten rotieren.
/// </summary>
public interface IQuickJobService
{
    List<QuickJob> GetAvailableJobs();
    void GenerateJobs(int count = 5);
    bool NeedsRotation();
    void RotateIfNeeded();
    TimeSpan TimeUntilNextRotation { get; }
    /// <summary>Maximale Quick Jobs pro Tag (skaliert mit Prestige).</summary>
    int MaxDailyJobs { get; }
    /// <summary>Prüft ob das tägliche Quick-Job-Limit erreicht ist.</summary>
    bool IsDailyLimitReached { get; }
    /// <summary>Verbleibende Quick Jobs heute.</summary>
    int RemainingJobsToday { get; }
    /// <summary>Wird aufgerufen wenn ein QuickJob abgeschlossen wird. Erhöht Tages-Counter.</summary>
    void NotifyJobCompleted(QuickJob job);
}
