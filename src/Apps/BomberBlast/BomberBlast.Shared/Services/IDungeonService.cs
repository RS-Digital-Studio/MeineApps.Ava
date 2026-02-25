using BomberBlast.Models.Dungeon;

namespace BomberBlast.Services;

/// <summary>
/// Service für den Dungeon-Run Roguelike-Modus.
/// Verwaltet Run-State, Floor-Belohnungen, Buff-Auswahl und Statistiken.
/// </summary>
public interface IDungeonService
{
    /// <summary>Aktueller Run-State (null wenn kein aktiver Run)</summary>
    DungeonRunState? RunState { get; }

    /// <summary>Persistente Dungeon-Statistiken</summary>
    DungeonStats Stats { get; }

    /// <summary>Ob ein Gratis-Run verfügbar ist (1x pro Tag)</summary>
    bool CanStartFreeRun { get; }

    /// <summary>Ob ein Ad-Run verfügbar ist (1x pro Tag, zusätzlich zum Gratis-Run)</summary>
    bool CanStartAdRun { get; }

    /// <summary>Kosten für einen bezahlten Run in Coins</summary>
    int PaidRunCoinCost { get; }

    /// <summary>Kosten für einen bezahlten Run in Gems</summary>
    int PaidRunGemCost { get; }

    /// <summary>Ob ein aktiver Run läuft</summary>
    bool IsRunActive { get; }

    /// <summary>
    /// Startet einen neuen Dungeon-Run.
    /// </summary>
    /// <param name="entryType">Art des Eintritts (Free/Coins/Gems/Ad)</param>
    /// <returns>true wenn erfolgreich gestartet</returns>
    bool StartRun(DungeonEntryType entryType);

    /// <summary>
    /// Berechnet Belohnungen für den aktuellen Floor und geht zum nächsten weiter.
    /// </summary>
    /// <returns>Belohnungen des abgeschlossenen Floors</returns>
    DungeonFloorReward CompleteFloor();

    /// <summary>
    /// Generiert 3 zufällige Buff-Optionen für die Auswahl nach einem Floor.
    /// </summary>
    List<DungeonBuffDefinition> GenerateBuffChoices();

    /// <summary>
    /// Wendet einen gewählten Buff auf den aktuellen Run an.
    /// </summary>
    void ApplyBuff(DungeonBuffType buffType);

    /// <summary>
    /// Beendet den Run (Tod oder Aufgabe). Zahlt gesammelte Belohnungen aus.
    /// </summary>
    /// <returns>Zusammenfassung des Runs</returns>
    DungeonRunSummary EndRun();

    /// <summary>
    /// Prüft ob nach dem aktuellen Floor eine Buff-Auswahl stattfindet.
    /// </summary>
    bool IsBuffFloorNext { get; }

    /// <summary>
    /// Prüft ob der aktuelle Floor ein Boss-Floor ist.
    /// </summary>
    bool IsCurrentFloorBoss { get; }

    /// <summary>Persistiert den aktuellen Run-State (z.B. nach Reroll-Zähler-Änderung)</summary>
    void PersistRunState();

    /// <summary>
    /// Wählt einen Node auf der Map aus und setzt RoomType/Modifier für den nächsten Floor.
    /// </summary>
    void SelectMapNode(int floor, int column);

    /// <summary>
    /// Markiert den aktuellen Floor-Node als abgeschlossen.
    /// </summary>
    void CompleteMapNode(int floor);

    /// <summary>Aktuelles Ascension-Level (0-5)</summary>
    int CurrentAscension { get; }

    /// <summary>Coin-Multiplikator basierend auf Ascension-Level</summary>
    float AscensionCoinMultiplier { get; }

    /// <summary>Event wenn sich der Run-State ändert</summary>
    event Action? RunStateChanged;
}

/// <summary>
/// Art des Dungeon-Eintritts
/// </summary>
public enum DungeonEntryType
{
    Free,
    Coins,
    Gems,
    Ad
}

/// <summary>
/// Zusammenfassung eines beendeten Dungeon-Runs
/// </summary>
public class DungeonRunSummary
{
    public int FloorsCompleted { get; init; }
    public int TotalCoins { get; init; }
    public int TotalGems { get; init; }
    public int TotalCards { get; init; }
    public int TotalDungeonCoins { get; init; }
    public bool IsNewBestFloor { get; init; }
    public List<DungeonBuffType> UsedBuffs { get; init; } = [];

    /// <summary>Ob durch diesen Run ein neues Ascension-Level freigeschaltet wurde</summary>
    public bool AscensionLevelUp { get; init; }

    /// <summary>Neues Ascension-Level (nur relevant wenn AscensionLevelUp == true)</summary>
    public int NewAscensionLevel { get; init; }
}
