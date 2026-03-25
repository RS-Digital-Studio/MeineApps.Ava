using FitnessRechner.Models;

namespace FitnessRechner.Services;

/// <summary>
/// Interface für den Intervallfasten-Service.
/// Verwaltet Fasten-Pläne, Timer und History.
/// </summary>
public interface IFastingService
{
    /// <summary>Aktuell gewählter Fasten-Plan.</summary>
    FastingPlan SelectedPlan { get; set; }

    /// <summary>Fasten-Stunden des aktuellen Plans.</summary>
    int FastingHours { get; set; }

    /// <summary>Ess-Stunden des aktuellen Plans (24 - FastingHours).</summary>
    int EatingHours { get; }

    /// <summary>Ob gerade eine Fasten-Periode aktiv ist.</summary>
    bool IsActive { get; }

    /// <summary>Startzeit der aktuellen Fasten-Periode (UTC).</summary>
    DateTime? StartTime { get; }

    /// <summary>Geplante Endzeit der aktuellen Fasten-Periode (UTC).</summary>
    DateTime? EndTime { get; }

    /// <summary>Bereits vergangene Zeit seit Fasten-Start.</summary>
    TimeSpan ElapsedTime { get; }

    /// <summary>Verbleibende Zeit bis Fasten-Ende.</summary>
    TimeSpan RemainingTime { get; }

    /// <summary>Fortschritt 0.0 bis 1.0.</summary>
    double Progress { get; }

    /// <summary>
    /// Prüft ob die Fasten-Periode abgelaufen ist und schließt sie ggf. ab.
    /// Soll im Timer-Tick aufgerufen werden, nicht im Property-Getter.
    /// </summary>
    void CheckAndCompleteIfDone();

    /// <summary>Startet eine neue Fasten-Periode.</summary>
    void StartFasting();

    /// <summary>Beendet die aktuelle Fasten-Periode vorzeitig.</summary>
    void StopFasting();

    /// <summary>Gibt die letzten Fasten-Einträge zurück (max. 30).</summary>
    List<FastingRecord> GetHistory();

    /// <summary>Wird ausgelöst wenn eine Fasten-Periode gestartet wird.</summary>
    event Action? FastingStarted;

    /// <summary>Wird ausgelöst wenn eine Fasten-Periode (erfolgreich) abgeschlossen wird.</summary>
    event Action? FastingCompleted;
}
