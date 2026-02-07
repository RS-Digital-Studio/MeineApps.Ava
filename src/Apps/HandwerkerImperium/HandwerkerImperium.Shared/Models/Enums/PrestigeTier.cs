namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Prestige tiers with increasing requirements and rewards.
/// Each tier requires multiple completions of the previous tier.
/// </summary>
public enum PrestigeTier
{
    /// <summary>No prestige yet</summary>
    None = 0,

    /// <summary>First prestige tier, requires Level 30</summary>
    Bronze = 1,

    /// <summary>Second tier, requires Level 50 + 3x Bronze</summary>
    Silver = 2,

    /// <summary>Highest tier, requires Level 50 + 3x Silver</summary>
    Gold = 3
}

public static class PrestigeTierExtensions
{
    /// <summary>
    /// Minimum player level required to prestige at this tier.
    /// </summary>
    public static int GetRequiredLevel(this PrestigeTier tier) => tier switch
    {
        PrestigeTier.Bronze => 30,
        PrestigeTier.Silver => 50,
        PrestigeTier.Gold => 50,
        _ => int.MaxValue
    };

    /// <summary>
    /// Number of completions of the previous tier required.
    /// </summary>
    public static int GetRequiredPreviousTierCount(this PrestigeTier tier) => tier switch
    {
        PrestigeTier.Bronze => 0,
        PrestigeTier.Silver => 3,  // 3x Bronze required
        PrestigeTier.Gold => 3,    // 3x Silver required
        _ => 0
    };

    /// <summary>
    /// Base prestige point multiplier for this tier.
    /// </summary>
    public static decimal GetPointMultiplier(this PrestigeTier tier) => tier switch
    {
        PrestigeTier.Bronze => 1.0m,
        PrestigeTier.Silver => 2.0m,
        PrestigeTier.Gold => 4.0m,
        _ => 0m
    };

    /// <summary>
    /// Permanent income multiplier bonus per prestige at this tier.
    /// </summary>
    public static decimal GetPermanentMultiplierBonus(this PrestigeTier tier) => tier switch
    {
        PrestigeTier.Bronze => 0.10m,  // +10% per Bronze prestige
        PrestigeTier.Silver => 0.25m,  // +25% per Silver prestige
        PrestigeTier.Gold => 0.50m,    // +50% per Gold prestige
        _ => 0m
    };

    /// <summary>
    /// What is preserved during prestige at this tier.
    /// Bronze: Achievements, Premium, Settings, PrestigeData, Tutorial
    /// Silver: + Research stays
    /// Gold: + Prestige-Shop items stay
    /// </summary>
    public static bool KeepsResearch(this PrestigeTier tier) => tier >= PrestigeTier.Silver;
    public static bool KeepsShopItems(this PrestigeTier tier) => tier >= PrestigeTier.Gold;

    /// <summary>
    /// Color key for this tier.
    /// </summary>
    public static string GetColorKey(this PrestigeTier tier) => tier switch
    {
        PrestigeTier.None => "#9E9E9E",    // Grey
        PrestigeTier.Bronze => "#CD7F32",  // Bronze
        PrestigeTier.Silver => "#C0C0C0",  // Silver
        PrestigeTier.Gold => "#FFD700",    // Gold
        _ => "#9E9E9E"
    };

    /// <summary>
    /// Icon for this tier.
    /// </summary>
    public static string GetIcon(this PrestigeTier tier) => tier switch
    {
        PrestigeTier.None => "",
        PrestigeTier.Bronze => "\ud83e\udd49",  // Bronze medal
        PrestigeTier.Silver => "\ud83e\udd48",  // Silver medal
        PrestigeTier.Gold => "\ud83e\udd47",    // Gold medal
        _ => ""
    };

    /// <summary>
    /// Localization key for tier name.
    /// </summary>
    public static string GetLocalizationKey(this PrestigeTier tier) => $"Prestige{tier}";
}
