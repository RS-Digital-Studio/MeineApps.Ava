using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Effects applied by an active game event.
/// Multipliers default to 1.0 (no change).
/// </summary>
public class GameEventEffect
{
    /// <summary>
    /// Income multiplier (1.0 = no change, 1.5 = +50%).
    /// </summary>
    [JsonPropertyName("incomeMultiplier")]
    public decimal IncomeMultiplier { get; set; } = 1.0m;

    /// <summary>
    /// Material/running cost multiplier (1.0 = no change, 2.0 = double).
    /// </summary>
    [JsonPropertyName("costMultiplier")]
    public decimal CostMultiplier { get; set; } = 1.0m;

    /// <summary>
    /// Order reward multiplier.
    /// </summary>
    [JsonPropertyName("rewardMultiplier")]
    public decimal RewardMultiplier { get; set; } = 1.0m;

    /// <summary>
    /// Reputation change (positive = gain, negative = loss).
    /// </summary>
    [JsonPropertyName("reputationChange")]
    public decimal ReputationChange { get; set; }

    /// <summary>
    /// Restrict worker market to certain tiers (null = no restriction).
    /// </summary>
    [JsonPropertyName("marketRestriction")]
    public WorkerTier? MarketRestriction { get; set; }

    /// <summary>
    /// Only affect a specific workshop type (null = all).
    /// </summary>
    [JsonPropertyName("affectedWorkshop")]
    public WorkshopType? AffectedWorkshop { get; set; }

    /// <summary>
    /// Special effect identifier for complex events.
    /// e.g. "tax_10_percent", "mood_drop_all_20"
    /// </summary>
    [JsonPropertyName("specialEffect")]
    public string? SpecialEffect { get; set; }
}
