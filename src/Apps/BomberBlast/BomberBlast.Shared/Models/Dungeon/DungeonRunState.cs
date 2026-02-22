namespace BomberBlast.Models.Dungeon;

/// <summary>
/// Persistenter Zustand eines Dungeon-Runs.
/// Wird als JSON in IPreferencesService gespeichert (Key: "DungeonRunData").
/// </summary>
public class DungeonRunState
{
    /// <summary>Aktueller Floor (1-basiert, 0 = kein aktiver Run)</summary>
    public int CurrentFloor { get; set; }

    /// <summary>Verbleibende Leben (Standard: 1, kann durch Buffs erhöht werden)</summary>
    public int Lives { get; set; } = 1;

    /// <summary>Aktive Buffs im aktuellen Run</summary>
    public List<DungeonBuffType> ActiveBuffs { get; set; } = [];

    /// <summary>Im Run gesammelte Coins (werden bei Run-Ende ausgezahlt)</summary>
    public int CollectedCoins { get; set; }

    /// <summary>Im Run gesammelte Gems</summary>
    public int CollectedGems { get; set; }

    /// <summary>Im Run erhaltene Karten-Drops (BombType-IDs als int)</summary>
    public List<int> CollectedCardDrops { get; set; } = [];

    /// <summary>Ob ein Run gerade aktiv ist</summary>
    public bool IsActive { get; set; }

    /// <summary>Seed für deterministische Level-Generierung</summary>
    public int RunSeed { get; set; }

    /// <summary>Datum des letzten Gratis-Runs (UTC, ISO 8601)</summary>
    public string LastFreeRunDate { get; set; } = "";

    /// <summary>Datum des letzten Ad-Runs (UTC, ISO 8601)</summary>
    public string LastAdRunDate { get; set; } = "";
}

/// <summary>
/// Persistente Dungeon-Statistiken (getrennt vom Run-State)
/// </summary>
public class DungeonStats
{
    /// <summary>Gesamtzahl abgeschlossener Runs</summary>
    public int TotalRuns { get; set; }

    /// <summary>Höchster erreichter Floor</summary>
    public int BestFloor { get; set; }

    /// <summary>Gesamt gesammelte Coins aus Dungeon-Runs</summary>
    public int TotalCoinsEarned { get; set; }

    /// <summary>Gesamt gesammelte Gems aus Dungeon-Runs</summary>
    public int TotalGemsEarned { get; set; }

    /// <summary>Gesamt gesammelte Karten-Drops aus Dungeon-Runs</summary>
    public int TotalCardsEarned { get; set; }
}

/// <summary>
/// Belohnungen für einen abgeschlossenen Dungeon-Floor
/// </summary>
public class DungeonFloorReward
{
    /// <summary>Coins für diesen Floor</summary>
    public int Coins { get; set; }

    /// <summary>Gems für diesen Floor (0 bei normalen Floors)</summary>
    public int Gems { get; set; }

    /// <summary>BombType-ID der gedropten Karte (-1 = kein Drop)</summary>
    public int CardDrop { get; set; } = -1;

    /// <summary>Ob dieser Floor ein Boss-Floor war</summary>
    public bool WasBossFloor { get; set; }

    /// <summary>Bonus-Coins aus der Truhe (nur Floor 10)</summary>
    public int ChestBonus { get; set; }
}
