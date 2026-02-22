namespace BomberBlast.Models.Dungeon;

/// <summary>
/// Buff-Typ der nach bestimmten Dungeon-Floors gewählt werden kann
/// </summary>
public enum DungeonBuffType
{
    ExtraBomb,
    ExtraFire,
    SpeedBoost,
    Shield,
    CoinBonus,
    ReloadSpecialBombs,
    EnemySlow,
    ExtraLife,
    FireImmunity,
    BlastRadius,
    BombTimer,
    PowerUpMagnet
}

/// <summary>
/// Seltenheit eines Dungeon-Buffs (beeinflusst Auswahl-Wahrscheinlichkeit)
/// </summary>
public enum DungeonBuffRarity
{
    Common,
    Rare,
    Epic
}

/// <summary>
/// Definition eines Dungeon-Buffs mit Name, Beschreibung und Effekt
/// </summary>
public class DungeonBuffDefinition
{
    public DungeonBuffType Type { get; init; }
    public string NameKey { get; init; } = "";
    public string DescKey { get; init; } = "";
    public string IconName { get; init; } = "";
    public DungeonBuffRarity Rarity { get; init; }

    /// <summary>Gewichtung für zufällige Auswahl (höher = häufiger)</summary>
    public int Weight { get; init; }
}

/// <summary>
/// Statischer Katalog aller verfügbaren Dungeon-Buffs
/// </summary>
public static class DungeonBuffCatalog
{
    public static readonly DungeonBuffDefinition[] All =
    [
        // Common (Gewicht 20-25)
        new()
        {
            Type = DungeonBuffType.ExtraBomb, NameKey = "DungeonBuffExtraBomb",
            DescKey = "DungeonBuffExtraBombDesc", IconName = "Bomb",
            Rarity = DungeonBuffRarity.Common, Weight = 25
        },
        new()
        {
            Type = DungeonBuffType.ExtraFire, NameKey = "DungeonBuffExtraFire",
            DescKey = "DungeonBuffExtraFireDesc", IconName = "Fire",
            Rarity = DungeonBuffRarity.Common, Weight = 25
        },
        new()
        {
            Type = DungeonBuffType.SpeedBoost, NameKey = "DungeonBuffSpeed",
            DescKey = "DungeonBuffSpeedDesc", IconName = "Run",
            Rarity = DungeonBuffRarity.Common, Weight = 20
        },
        new()
        {
            Type = DungeonBuffType.CoinBonus, NameKey = "DungeonBuffCoins",
            DescKey = "DungeonBuffCoinsDesc", IconName = "CurrencyUsd",
            Rarity = DungeonBuffRarity.Common, Weight = 20
        },
        new()
        {
            Type = DungeonBuffType.BombTimer, NameKey = "DungeonBuffBombTimer",
            DescKey = "DungeonBuffBombTimerDesc", IconName = "TimerOutline",
            Rarity = DungeonBuffRarity.Common, Weight = 20
        },

        // Rare (Gewicht 12-15)
        new()
        {
            Type = DungeonBuffType.Shield, NameKey = "DungeonBuffShield",
            DescKey = "DungeonBuffShieldDesc", IconName = "Shield",
            Rarity = DungeonBuffRarity.Rare, Weight = 15
        },
        new()
        {
            Type = DungeonBuffType.ReloadSpecialBombs, NameKey = "DungeonBuffReload",
            DescKey = "DungeonBuffReloadDesc", IconName = "Reload",
            Rarity = DungeonBuffRarity.Rare, Weight = 12
        },
        new()
        {
            Type = DungeonBuffType.EnemySlow, NameKey = "DungeonBuffSlow",
            DescKey = "DungeonBuffSlowDesc", IconName = "Tortoise",
            Rarity = DungeonBuffRarity.Rare, Weight = 15
        },
        new()
        {
            Type = DungeonBuffType.BlastRadius, NameKey = "DungeonBuffBlast",
            DescKey = "DungeonBuffBlastDesc", IconName = "Flare",
            Rarity = DungeonBuffRarity.Rare, Weight = 12
        },
        new()
        {
            Type = DungeonBuffType.PowerUpMagnet, NameKey = "DungeonBuffMagnet",
            DescKey = "DungeonBuffMagnetDesc", IconName = "Magnet",
            Rarity = DungeonBuffRarity.Rare, Weight = 12
        },

        // Epic (Gewicht 5-8)
        new()
        {
            Type = DungeonBuffType.ExtraLife, NameKey = "DungeonBuffLife",
            DescKey = "DungeonBuffLifeDesc", IconName = "HeartPlus",
            Rarity = DungeonBuffRarity.Epic, Weight = 5
        },
        new()
        {
            Type = DungeonBuffType.FireImmunity, NameKey = "DungeonBuffFireImmunity",
            DescKey = "DungeonBuffFireImmunityDesc", IconName = "ShieldFire",
            Rarity = DungeonBuffRarity.Epic, Weight = 8
        }
    ];

    /// <summary>
    /// Findet eine Buff-Definition anhand des Typs
    /// </summary>
    public static DungeonBuffDefinition? Find(DungeonBuffType type)
    {
        foreach (var buff in All)
            if (buff.Type == type) return buff;
        return null;
    }

    /// <summary>
    /// Buff-Floors: nach welchen Floors eine Buff-Auswahl stattfindet
    /// </summary>
    public static readonly int[] BuffFloors = [2, 4, 5, 7, 9];

    /// <summary>
    /// Prüft ob nach diesem Floor eine Buff-Auswahl stattfindet
    /// </summary>
    public static bool IsBuffFloor(int floor)
    {
        foreach (var f in BuffFloors)
            if (f == floor) return true;
        return false;
    }

    /// <summary>
    /// Prüft ob dieser Floor ein Boss-Floor ist
    /// </summary>
    public static bool IsBossFloor(int floor) => floor % 5 == 0;
}
