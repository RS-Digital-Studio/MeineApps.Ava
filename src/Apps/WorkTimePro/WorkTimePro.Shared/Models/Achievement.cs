using SQLite;

namespace WorkTimePro.Models;

/// <summary>
/// Repräsentiert ein freigeschaltetes oder noch zu erreichendes Achievement/Badge.
/// Wird in der SQLite-Datenbank persistiert.
/// </summary>
[Table("Achievements")]
public class Achievement
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Eindeutiger Schlüssel (z.B. "hours_100", "streak_30", "perfect_week")
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// Ob das Achievement freigeschaltet wurde
    /// </summary>
    public bool IsUnlocked { get; set; }

    /// <summary>
    /// Zeitpunkt der Freischaltung (UTC)
    /// </summary>
    public DateTime? UnlockedAt { get; set; }

    /// <summary>
    /// Aktueller Fortschritt (z.B. 4500 Minuten gearbeitet)
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Zielwert zum Freischalten (z.B. 6000 Minuten für 100h)
    /// </summary>
    public int Target { get; set; }

    // === Berechnete Properties (nicht in DB) ===

    /// <summary>
    /// Fortschritt in Prozent (0-100)
    /// </summary>
    [Ignore]
    public double ProgressPercent => Target > 0 ? Math.Min(100, (double)Progress / Target * 100) : 0;

    /// <summary>
    /// Lokalisierter Name (wird vom AchievementService gesetzt)
    /// </summary>
    [Ignore]
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Lokalisierte Beschreibung (wird vom AchievementService gesetzt)
    /// </summary>
    [Ignore]
    public string Description { get; set; } = "";

    /// <summary>
    /// Fortschritts-Anzeige als Text (z.B. "45/100h" oder "7/30 Tage")
    /// </summary>
    [Ignore]
    public string ProgressDisplay { get; set; } = "";

    /// <summary>
    /// Material-Icon-Name für die Anzeige
    /// </summary>
    [Ignore]
    public string IconName { get; set; } = "";
}
