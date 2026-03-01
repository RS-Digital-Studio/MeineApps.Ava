using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Service für das Achievement/Badge-System.
/// Verwaltet Freischaltungen, Fortschritte und Benachrichtigungen.
/// </summary>
public interface IAchievementService
{
    /// <summary>
    /// Initialisiert die Achievement-Tabelle und lädt initiale Definitionen.
    /// Muss nach DB-Init aufgerufen werden.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Prüft alle Achievements gegen aktuelle Daten und aktualisiert Fortschritte.
    /// Feuert AchievementUnlocked für neu freigeschaltete Achievements.
    /// </summary>
    Task CheckAchievementsAsync();

    /// <summary>
    /// Gibt alle Achievements zurück (mit lokalisierten Namen und Fortschritt).
    /// </summary>
    Task<List<Achievement>> GetAllAsync();

    /// <summary>
    /// Gibt nur freigeschaltete Achievements zurück.
    /// </summary>
    Task<List<Achievement>> GetUnlockedAsync();

    /// <summary>
    /// Berechnet die aktuelle Streak (aufeinanderfolgende Arbeitstage ohne Luecke).
    /// Wochenenden werden uebersprungen. Nutzt Batch-Query statt N+1.
    /// </summary>
    Task<int> GetCurrentStreakAsync();

    /// <summary>
    /// Event das gefeuert wird wenn ein Achievement neu freigeschaltet wird.
    /// Für UI-Celebration und FloatingText.
    /// </summary>
    event EventHandler<Achievement>? AchievementUnlocked;
}
