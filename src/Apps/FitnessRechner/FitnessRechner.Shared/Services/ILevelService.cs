namespace FitnessRechner.Services;

/// <summary>
/// Verwaltet das XP/Level-System für Gamification.
/// </summary>
public interface ILevelService
{
    /// <summary>
    /// Wird ausgelöst bei Level-Up. Parameter: neues Level.
    /// </summary>
    event Action<int>? LevelUp;

    /// <summary>
    /// Aktuelles Level (1-50).
    /// </summary>
    int CurrentLevel { get; }

    /// <summary>
    /// Aktuelle XP insgesamt.
    /// </summary>
    int TotalXp { get; }

    /// <summary>
    /// XP-Fortschritt im aktuellen Level (0.0-1.0).
    /// </summary>
    double LevelProgress { get; }

    /// <summary>
    /// XP im aktuellen Level / XP benötigt für nächstes Level.
    /// </summary>
    string XpDisplay { get; }

    /// <summary>
    /// XP hinzufügen (z.B. für Logging, Achievements).
    /// </summary>
    void AddXp(int amount);
}
