namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Prestige tiers with increasing requirements and rewards.
/// Each tier requires multiple completions of the previous tier.
/// </summary>
public enum PrestigeTier
{
    /// <summary>Kein Prestige</summary>
    None = 0,

    /// <summary>Erste Prestige-Stufe, erfordert Level 30</summary>
    Bronze = 1,

    /// <summary>Zweite Stufe, erfordert Level 100 + 1x Bronze</summary>
    Silver = 2,

    /// <summary>Dritte Stufe, erfordert Level 250 + 1x Silver</summary>
    Gold = 3,

    /// <summary>Vierte Stufe, erfordert Level 500 + 2x Gold</summary>
    Platin = 4,

    /// <summary>Fünfte Stufe, erfordert Level 750 + 2x Platin</summary>
    Diamant = 5,

    /// <summary>Sechste Stufe, erfordert Level 1000 + 2x Diamant</summary>
    Meister = 6,

    /// <summary>Höchste Stufe, erfordert Level 1200 + 3x Meister</summary>
    Legende = 7
}

public static class PrestigeTierExtensions
{
    /// <summary>
    /// Minimum player level required to prestige at this tier.
    /// </summary>
    public static int GetRequiredLevel(this PrestigeTier tier) => tier switch
    {
        PrestigeTier.Bronze => 30,
        PrestigeTier.Silver => 100,
        PrestigeTier.Gold => 250,
        PrestigeTier.Platin => 500,
        PrestigeTier.Diamant => 750,
        PrestigeTier.Meister => 1000,
        PrestigeTier.Legende => 1200,
        _ => int.MaxValue
    };

    /// <summary>
    /// Number of completions of the previous tier required.
    /// </summary>
    public static int GetRequiredPreviousTierCount(this PrestigeTier tier) => tier switch
    {
        PrestigeTier.Bronze => 0,
        PrestigeTier.Silver => 1,   // 1x Bronze
        PrestigeTier.Gold => 1,     // 1x Silver
        PrestigeTier.Platin => 2,   // 2x Gold
        PrestigeTier.Diamant => 2,  // 2x Platin
        PrestigeTier.Meister => 2,  // 2x Diamant
        PrestigeTier.Legende => 3,  // 3x Meister
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
        PrestigeTier.Platin => 8.0m,
        PrestigeTier.Diamant => 16.0m,
        PrestigeTier.Meister => 32.0m,
        PrestigeTier.Legende => 64.0m,
        _ => 0m
    };

    /// <summary>
    /// Permanent income multiplier bonus per prestige at this tier.
    /// </summary>
    public static decimal GetPermanentMultiplierBonus(this PrestigeTier tier) => tier switch
    {
        PrestigeTier.Bronze => 0.10m,   // +10% pro Bronze
        PrestigeTier.Silver => 0.25m,   // +25% pro Silver
        PrestigeTier.Gold => 0.50m,     // +50% pro Gold
        PrestigeTier.Platin => 1.00m,   // +100% pro Platin
        PrestigeTier.Diamant => 2.00m,  // +200% pro Diamant
        PrestigeTier.Meister => 4.00m,  // +400% pro Meister
        PrestigeTier.Legende => 8.00m,  // +800% pro Legende
        _ => 0m
    };

    /// <summary>
    /// Was bei Prestige erhalten bleibt:
    /// Bronze: Achievements, Premium, Settings, PrestigeData, Tutorial
    /// Silver+: + Research bleibt
    /// Gold+: + Prestige-Shop Items bleiben
    /// Platin+: + MasterTools bleiben
    /// Diamant+: + Gebäude (Level→1) + Equipment-Inventar
    /// Meister+: + Manager (Level→1)
    /// Legende: + 1 bester Worker pro Workshop
    /// </summary>
    public static bool KeepsResearch(this PrestigeTier tier) => tier >= PrestigeTier.Silver;
    public static bool KeepsShopItems(this PrestigeTier tier) => tier >= PrestigeTier.Gold;
    public static bool KeepsMasterTools(this PrestigeTier tier) => tier >= PrestigeTier.Platin;
    public static bool KeepsBuildings(this PrestigeTier tier) => tier >= PrestigeTier.Diamant;
    public static bool KeepsEquipment(this PrestigeTier tier) => tier >= PrestigeTier.Diamant;
    public static bool KeepsManagers(this PrestigeTier tier) => tier >= PrestigeTier.Meister;
    public static bool KeepsBestWorkers(this PrestigeTier tier) => tier >= PrestigeTier.Legende;

    /// <summary>
    /// Color key for this tier.
    /// </summary>
    public static string GetColorKey(this PrestigeTier tier) => tier switch
    {
        PrestigeTier.None => "#9E9E9E",      // Grey
        PrestigeTier.Bronze => "#CD7F32",    // Bronze
        PrestigeTier.Silver => "#C0C0C0",    // Silver
        PrestigeTier.Gold => "#FFD700",      // Gold
        PrestigeTier.Platin => "#E5E4E2",    // Platin
        PrestigeTier.Diamant => "#B9F2FF",   // Diamant (Hellblau)
        PrestigeTier.Meister => "#FF4500",   // Meister (Orangerot)
        PrestigeTier.Legende => "#FF69B4",   // Legende (Rainbow-Stellvertreter)
        _ => "#9E9E9E"
    };

    /// <summary>
    /// Icon for this tier.
    /// </summary>
    public static string GetIcon(this PrestigeTier tier) => tier switch
    {
        PrestigeTier.None => "",
        PrestigeTier.Bronze => "MedalOutline",
        PrestigeTier.Silver => "Medal",
        PrestigeTier.Gold => "TrophyAward",
        PrestigeTier.Platin => "DiamondStone",
        PrestigeTier.Diamant => "StarFourPoints",
        PrestigeTier.Meister => "Fire",
        PrestigeTier.Legende => "Crown",
        _ => ""
    };

    /// <summary>
    /// Localization key for tier name.
    /// </summary>
    public static string GetLocalizationKey(this PrestigeTier tier) => $"Prestige{tier}";
}
