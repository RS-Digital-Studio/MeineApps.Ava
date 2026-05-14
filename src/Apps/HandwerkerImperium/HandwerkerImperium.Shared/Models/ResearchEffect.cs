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
    /// Reduziert den Workshop-Level-Anforderungsmalus auf Worker-Effizienz (0.0-0.5).
    /// Stapelt mit der Tier-eigenen Resistenz.
    /// </summary>
    [JsonPropertyName("levelResistanceBonus")]
    public decimal LevelResistanceBonus { get; set; }

    // --- Endgame-Effekte (Level 16-20) ---

    /// <summary>
    /// Bonus-Ascension-Punkte bei Ascension (z.B. 0.15 = +15%).
    /// </summary>
    [JsonPropertyName("ascensionPointBonus")]
    public decimal AscensionPointBonus { get; set; }

    /// <summary>
    /// Synergie-Bonus pro zusätzlichem aktivem Workshop (z.B. 0.02 = +2% pro Workshop).
    /// </summary>
    [JsonPropertyName("workshopSynergyBonus")]
    public decimal WorkshopSynergyBonus { get; set; }

    /// <summary>
    /// Schaltet automatisches Worker-Training frei.
    /// </summary>
    [JsonPropertyName("unlocksAutoTraining")]
    public bool UnlocksAutoTraining { get; set; }

    /// <summary>
    /// Schaltet Masseneinstellung frei (mehrere Worker gleichzeitig anstellen).
    /// </summary>
    [JsonPropertyName("unlocksMassHiring")]
    public bool UnlocksMassHiring { get; set; }

    /// <summary>
    /// Bonus auf Reputations-Gewinn (z.B. 0.10 = +10%).
    /// </summary>
    [JsonPropertyName("reputationBonus")]
    public decimal ReputationBonus { get; set; }

    /// <summary>
    /// Erhöhte Chance auf Premium-Auftragstypen (Large/Weekly/Cooperation).
    /// </summary>
    [JsonPropertyName("premiumOrderChance")]
    public decimal PremiumOrderChance { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // V7 (): Logistik-Branch
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>V7: Zusaetzliche Lager-Slots (additiv zu Default 20 + Geld-Upgrades).</summary>
    [JsonPropertyName("bonusWarehouseSlots")]
    public int BonusWarehouseSlots { get; set; }

    /// <summary>V7: Multiplikator auf Stack-Limit (z.B. 2.0 = doppelt so viel pro Slot).</summary>
    [JsonPropertyName("stackLimitMultiplier")]
    public decimal StackLimitMultiplier { get; set; }

    /// <summary>V7: Markt-Verfuegbarkeit (logi_05).</summary>
    [JsonPropertyName("unlocksMarket")]
    public bool UnlocksMarket { get; set; }

    /// <summary>V7: Auto-Verkaufs-Regeln (Min/Max pro Slot, logi_07).</summary>
    [JsonPropertyName("unlocksAutoSellRules")]
    public bool UnlocksAutoSellRules { get; set; }

    /// <summary>V7: Tier-4-Rezepte ( Trigger, logi_09).</summary>
    [JsonPropertyName("unlocksTier4")]
    public bool UnlocksTier4 { get; set; }

    /// <summary>V7: Crafting-Speed-Bonus (logi_10, additiv mit Prestige-Speed-Bonus).</summary>
    [JsonPropertyName("craftingSpeedBonus")]
    public decimal CraftingSpeedBonus { get; set; }

    /// <summary>V7: Lieferanten-Material-Lieferung Bonus (logi_08).</summary>
    [JsonPropertyName("supplierMaterialBonus")]
    public decimal SupplierMaterialBonus { get; set; }

    /// <summary>V7: Erbstuecke ueberleben Prestige ( Trigger, logi_12).</summary>
    [JsonPropertyName("unlocksHeirloomSurvival")]
    public bool UnlocksHeirloomSurvival { get; set; }

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
            UnlocksAutoAssign = a.UnlocksAutoAssign || b.UnlocksAutoAssign,
            LevelResistanceBonus = a.LevelResistanceBonus + b.LevelResistanceBonus,
            AscensionPointBonus = a.AscensionPointBonus + b.AscensionPointBonus,
            WorkshopSynergyBonus = a.WorkshopSynergyBonus + b.WorkshopSynergyBonus,
            UnlocksAutoTraining = a.UnlocksAutoTraining || b.UnlocksAutoTraining,
            UnlocksMassHiring = a.UnlocksMassHiring || b.UnlocksMassHiring,
            ReputationBonus = a.ReputationBonus + b.ReputationBonus,
            PremiumOrderChance = a.PremiumOrderChance + b.PremiumOrderChance,
            // V7 ()
            BonusWarehouseSlots = a.BonusWarehouseSlots + b.BonusWarehouseSlots,
            StackLimitMultiplier = Math.Max(a.StackLimitMultiplier, b.StackLimitMultiplier),
            UnlocksMarket = a.UnlocksMarket || b.UnlocksMarket,
            UnlocksAutoSellRules = a.UnlocksAutoSellRules || b.UnlocksAutoSellRules,
            UnlocksTier4 = a.UnlocksTier4 || b.UnlocksTier4,
            CraftingSpeedBonus = a.CraftingSpeedBonus + b.CraftingSpeedBonus,
            SupplierMaterialBonus = a.SupplierMaterialBonus + b.SupplierMaterialBonus,
            UnlocksHeirloomSurvival = a.UnlocksHeirloomSurvival || b.UnlocksHeirloomSurvival
        };
    }
}
