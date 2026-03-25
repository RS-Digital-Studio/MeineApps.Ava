using FitnessRechner.Models;

namespace FitnessRechner.Services;

/// <summary>
/// Service für Aktivitäts-/Sport-Tracking mit MET-basierter Kalorienberechnung.
/// </summary>
public interface IActivityService
{
    /// <summary>Wird ausgelöst wenn eine Aktivität hinzugefügt wurde.</summary>
    event Action? ActivityAdded;

    /// <summary>Fügt eine neue Aktivität hinzu.</summary>
    Task<ActivityEntry> AddActivityAsync(ActivityEntry entry);

    /// <summary>Gibt alle Aktivitäten für einen bestimmten Tag zurück.</summary>
    Task<IReadOnlyList<ActivityEntry>> GetActivitiesAsync(DateTime date);

    /// <summary>Gibt alle Aktivitäten in einem Zeitraum zurück.</summary>
    Task<IReadOnlyList<ActivityEntry>> GetActivitiesInRangeAsync(DateTime start, DateTime end);

    /// <summary>Löscht eine Aktivität anhand der ID.</summary>
    Task<bool> DeleteActivityAsync(string id);

    /// <summary>Berechnet die heute verbrannten Kalorien (Summe aller Aktivitäten).</summary>
    Task<double> GetTodayBurnedCaloriesAsync();

    /// <summary>Berechnet verbrannte Kalorien anhand von MET-Wert, Gewicht und Dauer.</summary>
    double CalculateCalories(double metValue, double weightKg, int durationMinutes);
}
