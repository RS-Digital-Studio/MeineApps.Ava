namespace BomberBlast.Models;

/// <summary>
/// Ein Achievement/Badge im Spiel
/// </summary>
public class Achievement
{
    /// <summary>Eindeutige ID</summary>
    public string Id { get; init; } = "";

    /// <summary>RESX-Key für den Namen</summary>
    public string NameKey { get; init; } = "";

    /// <summary>RESX-Key für die Beschreibung</summary>
    public string DescriptionKey { get; init; } = "";

    /// <summary>Kategorie</summary>
    public AchievementCategory Category { get; init; }

    /// <summary>Ob freigeschaltet</summary>
    public bool IsUnlocked { get; set; }

    /// <summary>Aktueller Fortschritt (z.B. 50 von 100 Gegnern)</summary>
    public int Progress { get; set; }

    /// <summary>Ziel-Wert für Fortschritt</summary>
    public int Target { get; init; }

    /// <summary>Material.Icons Name für das Badge</summary>
    public string IconName { get; init; } = "Trophy";
}

/// <summary>
/// Achievement-Kategorien
/// </summary>
public enum AchievementCategory
{
    /// <summary>Story-Fortschritt (Welten abschließen)</summary>
    Progress,
    /// <summary>Meisterschaft (alle Sterne)</summary>
    Mastery,
    /// <summary>Kampf (Gegner besiegen)</summary>
    Combat,
    /// <summary>Geschick (Effizienz/Speed)</summary>
    Skill,
    /// <summary>Arcade-Modus</summary>
    Arcade
}
