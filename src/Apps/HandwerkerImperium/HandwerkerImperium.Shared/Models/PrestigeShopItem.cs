using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Prestige-Shop Kategorien für die gruppierte Anzeige.
/// </summary>
public enum PrestigeShopCategory
{
    /// <summary>Einkommen-Boosts und Kostenreduzierung</summary>
    IncomeAndCosts,

    /// <summary>Arbeiter-Upgrades und Stimmung</summary>
    WorkerAndMood,

    /// <summary>Rush, Lieferant, Crafting, Offline, QuickJobs</summary>
    SpeedAndAutomation,

    /// <summary>Startkapital, Goldschrauben, XP</summary>
    CurrencyAndStart
}

/// <summary>
/// An item purchasable with prestige points.
/// </summary>
public class PrestigeShopItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nameKey")]
    public string NameKey { get; set; } = string.Empty;

    [JsonPropertyName("descriptionKey")]
    public string DescriptionKey { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Cost in prestige points.
    /// </summary>
    [JsonPropertyName("cost")]
    public int Cost { get; set; }

    [JsonPropertyName("isPurchased")]
    public bool IsPurchased { get; set; }

    [JsonPropertyName("effect")]
    public PrestigeEffect Effect { get; set; } = new();

    /// <summary>
    /// Kategorie für die gruppierte Shop-Anzeige.
    /// </summary>
    [JsonIgnore]
    public PrestigeShopCategory Category { get; set; }

    /// <summary>
    /// Wiederholbar kaufbar (steigende Kosten: Basis * 2^(Kaufanzahl)).
    /// </summary>
    [JsonIgnore]
    public bool IsRepeatable { get; set; }

    /// <summary>
    /// Aktuelle Kaufanzahl (nur für wiederholbare Items, aus PrestigeData geladen).
    /// </summary>
    [JsonIgnore]
    public int PurchaseCount { get; set; }

    /// <summary>
    /// Mindest-Prestige-Tier um dieses Item im Shop zu sehen.
    /// None = immer sichtbar. Bereits gekaufte Items bleiben unabhängig vom Tier wirksam.
    /// </summary>
    [JsonIgnore]
    public PrestigeTier RequiredTier { get; set; } = PrestigeTier.None;
}

/// <summary>
/// Permanent bonuses from prestige shop items.
/// </summary>
public class PrestigeEffect
{
    [JsonPropertyName("incomeMultiplier")]
    public decimal IncomeMultiplier { get; set; }

    [JsonPropertyName("xpMultiplier")]
    public decimal XpMultiplier { get; set; }

    [JsonPropertyName("moodDecayReduction")]
    public decimal MoodDecayReduction { get; set; }

    [JsonPropertyName("costReduction")]
    public decimal CostReduction { get; set; }

    [JsonPropertyName("extraStartMoney")]
    public decimal ExtraStartMoney { get; set; }

    [JsonPropertyName("startingWorkerTier")]
    public string? StartingWorkerTier { get; set; }

    /// <summary>
    /// Rush-Verstärker: Bonus-Multiplikator auf Rush-Boost (z.B. 0.5 = Rush gibt 3x statt 2x).
    /// </summary>
    [JsonPropertyName("rushMultiplierBonus")]
    public decimal RushMultiplierBonus { get; set; }

    /// <summary>
    /// Lieferant-Turbo: Reduzierung des Lieferintervalls (z.B. 0.3 = 30% schneller).
    /// </summary>
    [JsonPropertyName("deliverySpeedBonus")]
    public decimal DeliverySpeedBonus { get; set; }

    /// <summary>
    /// Goldschrauben-Boost: Bonus auf alle Goldschrauben-Quellen (z.B. 0.25 = +25%).
    /// </summary>
    [JsonPropertyName("goldenScrewBonus")]
    public decimal GoldenScrewBonus { get; set; }

    /// <summary>
    /// Extra Offline-Stunden (z.B. 4 = +4h max. Offline-Berechnung).
    /// </summary>
    [JsonPropertyName("offlineHoursBonus")]
    public int OfflineHoursBonus { get; set; }

    /// <summary>
    /// Extra QuickJob-Limit pro Tag (z.B. 10 = +10 QuickJobs/Tag).
    /// </summary>
    [JsonPropertyName("extraQuickJobLimit")]
    public int ExtraQuickJobLimit { get; set; }

    /// <summary>
    /// Crafting-Geschwindigkeitsbonus (z.B. 0.25 = 25% schneller).
    /// </summary>
    [JsonPropertyName("craftingSpeedBonus")]
    public decimal CraftingSpeedBonus { get; set; }

    /// <summary>
    /// Rabatt auf Workshop-Upgrade-Kosten (z.B. 0.15 = -15%).
    /// </summary>
    [JsonPropertyName("upgradeDiscount")]
    public decimal UpgradeDiscount { get; set; }

    /// <summary>
    /// Bonus auf Auftragsbelohnungen (z.B. 0.05 = +5%).
    /// Angewendet in GameStateService.GetOrderRewardMultiplier().
    /// </summary>
    [JsonPropertyName("orderRewardBonus")]
    public decimal OrderRewardBonus { get; set; }

    /// <summary>
    /// Forschungs-Geschwindigkeitsbonus (z.B. 0.25 = -25% Forschungszeit).
    /// Angewendet in ResearchService.CalculateResearchDuration().
    /// </summary>
    [JsonPropertyName("researchSpeedBonus")]
    public decimal ResearchSpeedBonus { get; set; }
}
