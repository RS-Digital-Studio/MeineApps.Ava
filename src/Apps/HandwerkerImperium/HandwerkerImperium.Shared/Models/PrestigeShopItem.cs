using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

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
}
