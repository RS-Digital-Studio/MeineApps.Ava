namespace BomberBlast.Models.Collection;

/// <summary>
/// Kategorie eines Sammlungs-Eintrags
/// </summary>
public enum CollectionCategory
{
    /// <summary>12 Gegner-Typen</summary>
    Enemies,
    /// <summary>5 Boss-Typen</summary>
    Bosses,
    /// <summary>12 PowerUp-Typen</summary>
    PowerUps,
    /// <summary>14 Bomben-Karten</summary>
    BombCards,
    /// <summary>Kosmetik-Items (Skins, Trails, etc.)</summary>
    Cosmetics
}

/// <summary>
/// Ein Eintrag im Sammlungs-Album. Beschreibt ein entdeckbares/sammelbares Element.
/// </summary>
public class CollectionEntry
{
    /// <summary>Eindeutige ID (z.B. "enemy_ballom", "boss_stone_golem", "powerup_bombup", "card_ice")</summary>
    public string Id { get; init; } = "";

    /// <summary>Kategorie (Gegner, Boss, PowerUp, Karte, Kosmetik)</summary>
    public CollectionCategory Category { get; init; }

    /// <summary>RESX-Key für den Namen</summary>
    public string NameKey { get; init; } = "";

    /// <summary>RESX-Key für den Lore-Text / Beschreibung</summary>
    public string LoreKey { get; init; } = "";

    /// <summary>Ob der Eintrag vom Spieler entdeckt/gesehen wurde</summary>
    public bool IsDiscovered { get; set; }

    /// <summary>Statistik: Wie oft gesehen/angetroffen</summary>
    public int TimesEncountered { get; set; }

    /// <summary>Statistik: Wie oft besiegt (nur Gegner/Bosse)</summary>
    public int TimesDefeated { get; set; }

    /// <summary>Statistik: Wie oft eingesammelt (nur PowerUps)</summary>
    public int TimesCollected { get; set; }

    /// <summary>Ob der Eintrag besessen wird (Karten, Kosmetik)</summary>
    public bool IsOwned { get; set; }

    /// <summary>Material Icon Name für die Anzeige</summary>
    public string IconName { get; init; } = "HelpCircleOutline";
}

/// <summary>
/// Meilenstein-Definition für das Sammlungs-Album
/// </summary>
public class CollectionMilestone
{
    /// <summary>Benötigter Fortschritt in Prozent (25, 50, 75, 100)</summary>
    public int PercentRequired { get; init; }

    /// <summary>Coin-Belohnung</summary>
    public int CoinReward { get; init; }

    /// <summary>Gem-Belohnung</summary>
    public int GemReward { get; init; }

    /// <summary>Ob der Meilenstein bereits beansprucht wurde</summary>
    public bool IsClaimed { get; set; }

    /// <summary>Ob der Meilenstein erreicht ist (Fortschritt >= PercentRequired)</summary>
    public bool IsReached { get; set; }

    /// <summary>Alle Meilenstein-Stufen</summary>
    public static readonly CollectionMilestone[] All =
    [
        new() { PercentRequired = 25, CoinReward = 2_000, GemReward = 0 },
        new() { PercentRequired = 50, CoinReward = 5_000, GemReward = 10 },
        new() { PercentRequired = 75, CoinReward = 10_000, GemReward = 20 },
        new() { PercentRequired = 100, CoinReward = 25_000, GemReward = 50 }
    ];
}

/// <summary>
/// Persistierte Sammlungs-Daten (JSON)
/// </summary>
public class CollectionData
{
    /// <summary>Encounter/Defeat-Tracking pro Gegner-ID</summary>
    public Dictionary<string, EnemyCollectionStats> EnemyStats { get; set; } = new();

    /// <summary>Encounter/Defeat-Tracking pro Boss-ID</summary>
    public Dictionary<string, BossCollectionStats> BossStats { get; set; } = new();

    /// <summary>Einsammel-Tracking pro PowerUp-ID</summary>
    public Dictionary<string, int> PowerUpCollected { get; set; } = new();

    /// <summary>Beanspruchte Meilensteine (Prozent-Werte)</summary>
    public List<int> ClaimedMilestones { get; set; } = [];
}

/// <summary>
/// Statistik für einen Gegner-Typ
/// </summary>
public class EnemyCollectionStats
{
    public int TimesEncountered { get; set; }
    public int TimesDefeated { get; set; }
}

/// <summary>
/// Statistik für einen Boss-Typ
/// </summary>
public class BossCollectionStats
{
    public int TimesEncountered { get; set; }
    public int TimesDefeated { get; set; }
    public float BestTimeSeconds { get; set; }
}
