using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Cumulative effects from completed research.
/// All values are additive bonuses.
/// </summary>
public class ResearchEffect
{
    [JsonPropertyName("efficiencyBonus")]
    public decimal EfficiencyBonus { get; set; }

    [JsonPropertyName("costReduction")]
    public decimal CostReduction { get; set; }

    [JsonPropertyName("miniGameZoneBonus")]
    public decimal MiniGameZoneBonus { get; set; }

    [JsonPropertyName("wageReduction")]
    public decimal WageReduction { get; set; }

    [JsonPropertyName("extraWorkerSlots")]
    public int ExtraWorkerSlots { get; set; }

    [JsonPropertyName("extraOrderSlots")]
    public int ExtraOrderSlots { get; set; }

    [JsonPropertyName("trainingSpeedMultiplier")]
    public decimal TrainingSpeedMultiplier { get; set; }

    [JsonPropertyName("rewardMultiplier")]
    public decimal RewardMultiplier { get; set; }

    [JsonPropertyName("unlocksAutoMaterial")]
    public bool UnlocksAutoMaterial { get; set; }

    [JsonPropertyName("unlocksHeadhunter")]
    public bool UnlocksHeadhunter { get; set; }

    [JsonPropertyName("unlocksSTierWorkers")]
    public bool UnlocksSTierWorkers { get; set; }

    [JsonPropertyName("unlocksAutoAssign")]
    public bool UnlocksAutoAssign { get; set; }

    /// <summary>
    /// Combines two research effects additively.
    /// </summary>
    public static ResearchEffect Combine(ResearchEffect a, ResearchEffect b)
    {
        return new ResearchEffect
        {
            EfficiencyBonus = a.EfficiencyBonus + b.EfficiencyBonus,
            CostReduction = a.CostReduction + b.CostReduction,
            MiniGameZoneBonus = a.MiniGameZoneBonus + b.MiniGameZoneBonus,
            WageReduction = a.WageReduction + b.WageReduction,
            ExtraWorkerSlots = a.ExtraWorkerSlots + b.ExtraWorkerSlots,
            ExtraOrderSlots = a.ExtraOrderSlots + b.ExtraOrderSlots,
            TrainingSpeedMultiplier = a.TrainingSpeedMultiplier + b.TrainingSpeedMultiplier,
            RewardMultiplier = a.RewardMultiplier + b.RewardMultiplier,
            UnlocksAutoMaterial = a.UnlocksAutoMaterial || b.UnlocksAutoMaterial,
            UnlocksHeadhunter = a.UnlocksHeadhunter || b.UnlocksHeadhunter,
            UnlocksSTierWorkers = a.UnlocksSTierWorkers || b.UnlocksSTierWorkers,
            UnlocksAutoAssign = a.UnlocksAutoAssign || b.UnlocksAutoAssign
        };
    }
}
