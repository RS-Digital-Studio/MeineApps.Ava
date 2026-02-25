namespace BomberBlast.Models.Dungeon;

/// <summary>
/// Definition eines permanenten Dungeon-Upgrades (Meta-Progression à la "Hades Mirror of Night").
/// Upgrades werden mit DungeonCoins gekauft und bleiben über Runs hinweg bestehen.
/// </summary>
public class DungeonUpgradeDefinition
{
    public string Id { get; init; } = "";
    public string NameKey { get; init; } = "";
    public string DescKey { get; init; } = "";
    public string IconName { get; init; } = "";
    public int MaxLevel { get; init; }

    /// <summary>Kosten pro Level (Index 0 = Level 1, Index 1 = Level 2, ...)</summary>
    public int[] CostsPerLevel { get; init; } = [];
}

/// <summary>
/// Zustand eines gekauften Dungeon-Upgrades (persistiert)
/// </summary>
public class DungeonUpgradeState
{
    public string Id { get; set; } = "";
    public int Level { get; set; }
}

/// <summary>
/// Statischer Katalog aller permanenten Dungeon-Upgrades
/// </summary>
public static class DungeonUpgradeCatalog
{
    public const string StartingBombs = "starting_bombs";
    public const string StartingFire = "starting_fire";
    public const string StartingSpeed = "starting_speed";
    public const string ExtraBuffChoice = "extra_buff_choice";
    public const string BossGoldBonus = "boss_gold_bonus";
    public const string StartingShield = "starting_shield";
    public const string CardDropBoost = "card_drop_boost";
    public const string ReviveCostReduction = "revive_cost_reduction";

    public static readonly DungeonUpgradeDefinition[] All =
    [
        new()
        {
            Id = StartingBombs, NameKey = "DungeonUpgradeStartBombs",
            DescKey = "DungeonUpgradeStartBombsDesc", IconName = "Bomb",
            MaxLevel = 2, CostsPerLevel = [50, 150]
        },
        new()
        {
            Id = StartingFire, NameKey = "DungeonUpgradeStartFire",
            DescKey = "DungeonUpgradeStartFireDesc", IconName = "Fire",
            MaxLevel = 2, CostsPerLevel = [50, 150]
        },
        new()
        {
            Id = StartingSpeed, NameKey = "DungeonUpgradeStartSpeed",
            DescKey = "DungeonUpgradeStartSpeedDesc", IconName = "Run",
            MaxLevel = 1, CostsPerLevel = [100]
        },
        new()
        {
            Id = ExtraBuffChoice, NameKey = "DungeonUpgradeExtraBuff",
            DescKey = "DungeonUpgradeExtraBuffDesc", IconName = "CardsPlaying",
            MaxLevel = 1, CostsPerLevel = [200]
        },
        new()
        {
            Id = BossGoldBonus, NameKey = "DungeonUpgradeBossGold",
            DescKey = "DungeonUpgradeBossGoldDesc", IconName = "TreasureChest",
            MaxLevel = 2, CostsPerLevel = [75, 200]
        },
        new()
        {
            Id = StartingShield, NameKey = "DungeonUpgradeStartShield",
            DescKey = "DungeonUpgradeStartShieldDesc", IconName = "Shield",
            MaxLevel = 1, CostsPerLevel = [250]
        },
        new()
        {
            Id = CardDropBoost, NameKey = "DungeonUpgradeCardDrop",
            DescKey = "DungeonUpgradeCardDropDesc", IconName = "Cards",
            MaxLevel = 2, CostsPerLevel = [100, 250]
        },
        new()
        {
            Id = ReviveCostReduction, NameKey = "DungeonUpgradeReviveCost",
            DescKey = "DungeonUpgradeReviveCostDesc", IconName = "HeartPlus",
            MaxLevel = 1, CostsPerLevel = [300]
        }
    ];

    /// <summary>Findet eine Upgrade-Definition anhand der ID</summary>
    public static DungeonUpgradeDefinition? Find(string id)
    {
        foreach (var def in All)
            if (def.Id == id) return def;
        return null;
    }
}
