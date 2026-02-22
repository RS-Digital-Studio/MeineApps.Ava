using System.Text.Json.Serialization;

namespace BomberBlast.Models;

/// <summary>
/// Persistenter Upgrade-Stand des Spielers
/// </summary>
public class PlayerUpgrades
{
    /// <summary>Upgrade-Level pro Typ (0 = nicht gekauft)</summary>
    [JsonInclude]
    public Dictionary<UpgradeType, int> Levels { get; set; } = new();

    // Maximale Level pro Upgrade-Typ
    private static readonly Dictionary<UpgradeType, int> MaxLevels = new()
    {
        { UpgradeType.StartBombs, 3 },
        { UpgradeType.StartFire, 3 },
        { UpgradeType.StartSpeed, 1 },
        { UpgradeType.ExtraLives, 2 },
        { UpgradeType.ScoreMultiplier, 3 },
        { UpgradeType.TimeBonus, 1 },
        { UpgradeType.ShieldStart, 1 },
        { UpgradeType.CoinBonus, 2 },
        { UpgradeType.PowerUpLuck, 2 },
        { UpgradeType.IceBomb, 1 },
        { UpgradeType.FireBomb, 1 },
        { UpgradeType.StickyBomb, 1 }
    };

    // Preise pro Level (Index 0 = Level 1, etc.) - ~50% reduziert fuer bessere Progression
    private static readonly Dictionary<UpgradeType, int[]> Prices = new()
    {
        { UpgradeType.StartBombs, [1500, 4000, 10000] },
        { UpgradeType.StartFire, [1500, 4000, 10000] },
        { UpgradeType.StartSpeed, [2500] },
        { UpgradeType.ExtraLives, [7500, 20000] },
        { UpgradeType.ScoreMultiplier, [4000, 10000, 20000] },
        { UpgradeType.TimeBonus, [6000] },
        { UpgradeType.ShieldStart, [8000] },
        { UpgradeType.CoinBonus, [8000, 25000] },
        { UpgradeType.PowerUpLuck, [5000, 15000] },
        { UpgradeType.IceBomb, [6000] },
        { UpgradeType.FireBomb, [8000] },
        { UpgradeType.StickyBomb, [10000] }
    };

    // Score-Multiplikatoren pro Level
    private static readonly float[] ScoreMultipliers = [1.0f, 1.25f, 1.5f, 2.0f];

    /// <summary>Aktuelles Level eines Upgrades (0 = nicht gekauft)</summary>
    public int GetLevel(UpgradeType type)
    {
        return Levels.GetValueOrDefault(type, 0);
    }

    /// <summary>Maximales Level eines Upgrades</summary>
    public static int GetMaxLevel(UpgradeType type)
    {
        return MaxLevels.GetValueOrDefault(type, 0);
    }

    /// <summary>Preis fuer das naechste Level (0 wenn bereits max)</summary>
    public int GetNextPrice(UpgradeType type)
    {
        int current = GetLevel(type);
        if (current >= GetMaxLevel(type))
            return 0;

        var prices = Prices.GetValueOrDefault(type);
        if (prices == null || current >= prices.Length)
            return 0;

        return prices[current];
    }

    /// <summary>Ob das Upgrade bereits auf Maximum ist</summary>
    public bool IsMaxed(UpgradeType type)
    {
        return GetLevel(type) >= GetMaxLevel(type);
    }

    /// <summary>Level erhoehen</summary>
    public void Upgrade(UpgradeType type)
    {
        int current = GetLevel(type);
        if (current < GetMaxLevel(type))
        {
            Levels[type] = current + 1;
        }
    }

    /// <summary>Score-Multiplikator basierend auf Upgrade-Level (1.0 / 1.25 / 1.5 / 2.0)</summary>
    public float GetScoreMultiplier()
    {
        int level = GetLevel(UpgradeType.ScoreMultiplier);
        return level < ScoreMultipliers.Length ? ScoreMultipliers[level] : ScoreMultipliers[^1];
    }

    /// <summary>Zeitbonus-Multiplikator (10 oder 20)</summary>
    public int GetTimeBonusMultiplier()
    {
        return GetLevel(UpgradeType.TimeBonus) >= 1 ? 20 : 10;
    }

    /// <summary>Start-Bomben (1 + Upgrade-Level)</summary>
    public int GetStartBombs()
    {
        return 1 + GetLevel(UpgradeType.StartBombs);
    }

    /// <summary>Start-Feuerreichweite (1 + Upgrade-Level)</summary>
    public int GetStartFire()
    {
        return 1 + GetLevel(UpgradeType.StartFire);
    }

    /// <summary>Ob Speed von Anfang an aktiv ist</summary>
    public bool HasStartSpeed()
    {
        return GetLevel(UpgradeType.StartSpeed) >= 1;
    }

    /// <summary>Start-Leben (3 + Upgrade-Level)</summary>
    public int GetStartLives()
    {
        return 3 + GetLevel(UpgradeType.ExtraLives);
    }

    /// <summary>Alle Upgrades zuruecksetzen</summary>
    public void Reset()
    {
        Levels.Clear();
    }
}
