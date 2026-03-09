namespace RebornSaga.Models;

using SQLite;
using System;

/// <summary>
/// SQLite-Entity: Save-Slot Metadaten. 3 Slots + Auto-Save.
/// </summary>
[Table("SaveSlots")]
public class SaveSlotEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Slot-Nummer (1-3) oder 0 für Auto-Save.</summary>
    public int SlotNumber { get; set; }

    public string SlotName { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string LastPlayedAt { get; set; } = "";
    public int PlayTimeSeconds { get; set; }
    public string ChapterId { get; set; } = "";
    public string ClassName { get; set; } = "";
    public int Level { get; set; }
}

/// <summary>
/// SQLite-Entity: Spieler-Daten (Stats, Gold, Karma).
/// </summary>
[Table("PlayerData")]
public class PlayerDataEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SaveSlotId { get; set; }

    public string Name { get; set; } = "Held";
    public string ClassName { get; set; } = "";
    public int Level { get; set; }
    public int Exp { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Mp { get; set; }
    public int MaxMp { get; set; }
    public int Atk { get; set; }
    public int Def { get; set; }
    public int Int { get; set; }
    public int Spd { get; set; }
    public int Luk { get; set; }
    public int Gold { get; set; }
    public int Karma { get; set; }
    public int FreeStatPoints { get; set; }
    public string CurrentChapterId { get; set; } = "";
    public string CurrentNodeId { get; set; } = "";

    /// <summary>Story-Flags als JSON-serialisierter String.</summary>
    public string FlagsJson { get; set; } = "[]";

    /// <summary>Abgeschlossene Kapitel als JSON-serialisierter String.</summary>
    public string CompletedChaptersJson { get; set; } = "[]";
}

/// <summary>
/// SQLite-Entity: Inventar-Eintrag (Item + Anzahl + Equipment-Status).
/// </summary>
[Table("Inventory")]
public class InventoryEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SaveSlotId { get; set; }

    public string ItemId { get; set; } = "";
    public int Quantity { get; set; }
    public bool IsEquipped { get; set; }

    /// <summary>Equipment-Slot (nur wenn IsEquipped, z.B. "Weapon", "Armor", "Accessory").</summary>
    public string? EquipSlot { get; set; }
}

/// <summary>
/// SQLite-Entity: Skill-Mastery-Daten.
/// </summary>
[Table("SkillData")]
public class SkillDataEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SaveSlotId { get; set; }

    public string SkillId { get; set; } = "";
    public int MasteryCount { get; set; }
    public bool IsUnlocked { get; set; }
}

/// <summary>
/// SQLite-Entity: NPC-Affinitäts-Daten.
/// </summary>
[Table("AffinityData")]
public class AffinityEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SaveSlotId { get; set; }

    public string NpcId { get; set; } = "";
    public int Points { get; set; }
    public int BondLevel { get; set; }

    /// <summary>Gesehene Bond-Szenen als komma-getrennte Level-Nummern.</summary>
    public string SeenScenesStr { get; set; } = "";
}

/// <summary>
/// SQLite-Entity: Story-Entscheidung (Fate-Tracking).
/// </summary>
[Table("StoryProgress")]
public class StoryProgressEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SaveSlotId { get; set; }

    public string ChapterId { get; set; } = "";
    public string NodeId { get; set; } = "";
    public int ChoiceIndex { get; set; }
    public int KarmaChange { get; set; }
    public string DescriptionKey { get; set; } = "";
}

/// <summary>
/// SQLite-Entity: Kodex-Eintrag (Bestiary, Lore etc.).
/// </summary>
[Table("CodexEntries")]
public class CodexEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SaveSlotId { get; set; }

    public string EntryId { get; set; } = "";
    public bool IsDiscovered { get; set; }
}

/// <summary>
/// SQLite-Entity: Kapitel-Freischaltung (gilt für ALLE Save-Slots).
/// </summary>
[Table("ChapterUnlocks")]
public class ChapterUnlockEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string ChapterId { get; set; } = "";
    public string UnlockMethod { get; set; } = ""; // "gold", "free", "story"
    public string UnlockedAt { get; set; } = "";
}
