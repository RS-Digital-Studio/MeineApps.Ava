using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Tracks all prestige-related data across resets.
/// </summary>
public class PrestigeData
{
    /// <summary>
    /// Current highest tier reached.
    /// </summary>
    [JsonPropertyName("currentTier")]
    public PrestigeTier CurrentTier { get; set; } = PrestigeTier.None;

    [JsonPropertyName("bronzeCount")]
    public int BronzeCount { get; set; }

    [JsonPropertyName("silverCount")]
    public int SilverCount { get; set; }

    [JsonPropertyName("goldCount")]
    public int GoldCount { get; set; }

    [JsonPropertyName("platinCount")]
    public int PlatinCount { get; set; }

    [JsonPropertyName("diamantCount")]
    public int DiamantCount { get; set; }

    [JsonPropertyName("meisterCount")]
    public int MeisterCount { get; set; }

    [JsonPropertyName("legendeCount")]
    public int LegendeCount { get; set; }

    /// <summary>
    /// Spendable prestige points.
    /// </summary>
    [JsonPropertyName("prestigePoints")]
    public int PrestigePoints { get; set; }

    /// <summary>
    /// Lifetime prestige points earned.
    /// </summary>
    [JsonPropertyName("totalPrestigePoints")]
    public int TotalPrestigePoints { get; set; }

    /// <summary>
    /// IDs of purchased prestige shop items.
    /// </summary>
    [JsonPropertyName("purchasedShopItems")]
    public List<string> PurchasedShopItems { get; set; } = [];

    /// <summary>
    /// Cumulative permanent income multiplier from all prestiges.
    /// Starts at 1.0 (no bonus).
    /// </summary>
    [JsonPropertyName("permanentMultiplier")]
    public decimal PermanentMultiplier { get; set; } = 1.0m;

    /// <summary>
    /// Total number of prestiges across all tiers.
    /// </summary>
    [JsonIgnore]
    public int TotalPrestigeCount => BronzeCount + SilverCount + GoldCount
        + PlatinCount + DiamantCount + MeisterCount + LegendeCount;

    /// <summary>
    /// Calculates prestige points from total money earned.
    /// Formula: floor(sqrt(TotalMoneyEarned / 100_000))
    /// </summary>
    public static int CalculatePrestigePoints(decimal totalMoneyEarned)
    {
        if (totalMoneyEarned <= 0) return 0;
        return (int)Math.Floor(Math.Sqrt((double)(totalMoneyEarned / 100_000m)));
    }

    /// <summary>
    /// Checks if a specific prestige tier is available.
    /// </summary>
    public bool CanPrestige(PrestigeTier tier, int playerLevel)
    {
        if (playerLevel < tier.GetRequiredLevel()) return false;

        return tier switch
        {
            PrestigeTier.Bronze => true,
            PrestigeTier.Silver => BronzeCount >= tier.GetRequiredPreviousTierCount(),
            PrestigeTier.Gold => SilverCount >= tier.GetRequiredPreviousTierCount(),
            PrestigeTier.Platin => GoldCount >= tier.GetRequiredPreviousTierCount(),
            PrestigeTier.Diamant => PlatinCount >= tier.GetRequiredPreviousTierCount(),
            PrestigeTier.Meister => DiamantCount >= tier.GetRequiredPreviousTierCount(),
            PrestigeTier.Legende => MeisterCount >= tier.GetRequiredPreviousTierCount(),
            _ => false
        };
    }

    /// <summary>
    /// Gets the highest available tier for prestige.
    /// </summary>
    public PrestigeTier GetHighestAvailableTier(int playerLevel)
    {
        if (CanPrestige(PrestigeTier.Legende, playerLevel)) return PrestigeTier.Legende;
        if (CanPrestige(PrestigeTier.Meister, playerLevel)) return PrestigeTier.Meister;
        if (CanPrestige(PrestigeTier.Diamant, playerLevel)) return PrestigeTier.Diamant;
        if (CanPrestige(PrestigeTier.Platin, playerLevel)) return PrestigeTier.Platin;
        if (CanPrestige(PrestigeTier.Gold, playerLevel)) return PrestigeTier.Gold;
        if (CanPrestige(PrestigeTier.Silver, playerLevel)) return PrestigeTier.Silver;
        if (CanPrestige(PrestigeTier.Bronze, playerLevel)) return PrestigeTier.Bronze;
        return PrestigeTier.None;
    }
}
