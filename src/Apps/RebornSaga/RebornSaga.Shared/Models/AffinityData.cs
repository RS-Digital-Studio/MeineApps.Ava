namespace RebornSaga.Models;

/// <summary>
/// Affinitäts-Daten für einen NPC (Bond-System, 5 Stufen).
/// </summary>
public class AffinityData
{
    /// <summary>NPC-ID (z.B. "aria", "aldric", "kael", "luna", "vex").</summary>
    public string NpcId { get; set; } = "";

    /// <summary>Aktuelle Affinitäts-Punkte.</summary>
    public int Points { get; set; }

    /// <summary>Höchste erreichte Bond-Stufe (1-5).</summary>
    public int BondLevel { get; set; } = 1;

    /// <summary>Welche Bond-Szenen bereits gesehen wurden.</summary>
    public HashSet<int> SeenScenes { get; set; } = new();

    /// <summary>
    /// Berechnet die aktuelle Bond-Stufe basierend auf Punkten.
    /// </summary>
    public static int CalculateLevel(int points) => points switch
    {
        >= 500 => 5, // Seelenverwandt
        >= 300 => 4, // Vertrauter
        >= 150 => 3, // Freund
        >= 50 => 2,  // Verbündeter
        _ => 1       // Bekannter
    };

    /// <summary>
    /// Punkte die für die nächste Stufe nötig sind.
    /// </summary>
    public static int PointsForLevel(int level) => level switch
    {
        2 => 50,
        3 => 150,
        4 => 300,
        5 => 500,
        _ => 0
    };
}

/// <summary>
/// Einzelner Eintrag im Entscheidungs-Log des Fate-Tracking-Systems.
/// </summary>
public class FateDecision
{
    /// <summary>Kapitel-ID in dem die Entscheidung getroffen wurde.</summary>
    public string ChapterId { get; set; } = "";

    /// <summary>Knoten-ID der Entscheidung.</summary>
    public string NodeId { get; set; } = "";

    /// <summary>Gewählte Option (0-basiert).</summary>
    public int ChoiceIndex { get; set; }

    /// <summary>Karma-Änderung durch diese Entscheidung.</summary>
    public int KarmaChange { get; set; }

    /// <summary>Kurze Beschreibung (für den Kodex).</summary>
    public string DescriptionKey { get; set; } = "";
}

/// <summary>
/// Kodex-Eintrag (Bestiary, Lore, Charakter-Profile).
/// </summary>
public class CodexEntry
{
    public string Id { get; set; } = "";
    public string CategoryKey { get; set; } = "";
    public string TitleKey { get; set; } = "";
    public string ContentKey { get; set; } = "";
    public bool IsUnlocked { get; set; }
}
