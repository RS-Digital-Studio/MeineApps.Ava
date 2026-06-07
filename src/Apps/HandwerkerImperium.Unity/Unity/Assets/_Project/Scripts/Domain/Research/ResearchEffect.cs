using System;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Research
{
    /// <summary>
    /// Kumulative Effekte abgeschlossener Forschung. Alle Werte sind additive Boni.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/ResearchEffect.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public class ResearchEffect
    {
        [JsonProperty("efficiencyBonus")]
        public decimal EfficiencyBonus { get; set; }

        [JsonProperty("costReduction")]
        public decimal CostReduction { get; set; }

        [JsonProperty("miniGameZoneBonus")]
        public decimal MiniGameZoneBonus { get; set; }

        [JsonProperty("wageReduction")]
        public decimal WageReduction { get; set; }

        [JsonProperty("extraWorkerSlots")]
        public int ExtraWorkerSlots { get; set; }

        [JsonProperty("extraOrderSlots")]
        public int ExtraOrderSlots { get; set; }

        [JsonProperty("trainingSpeedMultiplier")]
        public decimal TrainingSpeedMultiplier { get; set; }

        [JsonProperty("rewardMultiplier")]
        public decimal RewardMultiplier { get; set; }

        [JsonProperty("unlocksAutoMaterial")]
        public bool UnlocksAutoMaterial { get; set; }

        [JsonProperty("unlocksHeadhunter")]
        public bool UnlocksHeadhunter { get; set; }

        [JsonProperty("unlocksSTierWorkers")]
        public bool UnlocksSTierWorkers { get; set; }

        [JsonProperty("unlocksAutoAssign")]
        public bool UnlocksAutoAssign { get; set; }

        /// <summary>
        /// Reduziert den Workshop-Level-Anforderungsmalus auf Worker-Effizienz (0.0-0.5).
        /// Stapelt mit der Tier-eigenen Resistenz.
        /// </summary>
        [JsonProperty("levelResistanceBonus")]
        public decimal LevelResistanceBonus { get; set; }

        // --- Endgame-Effekte (Level 16-20) ---

        /// <summary>Bonus-Ascension-Punkte bei Ascension (z.B. 0.15 = +15%).</summary>
        [JsonProperty("ascensionPointBonus")]
        public decimal AscensionPointBonus { get; set; }

        /// <summary>Synergie-Bonus pro zusätzlichem aktivem Workshop (z.B. 0.02 = +2% pro Workshop).</summary>
        [JsonProperty("workshopSynergyBonus")]
        public decimal WorkshopSynergyBonus { get; set; }

        /// <summary>Schaltet automatisches Worker-Training frei.</summary>
        [JsonProperty("unlocksAutoTraining")]
        public bool UnlocksAutoTraining { get; set; }

        /// <summary>Schaltet Masseneinstellung frei (mehrere Worker gleichzeitig anstellen).</summary>
        [JsonProperty("unlocksMassHiring")]
        public bool UnlocksMassHiring { get; set; }

        /// <summary>Bonus auf Reputations-Gewinn (z.B. 0.10 = +10%).</summary>
        [JsonProperty("reputationBonus")]
        public decimal ReputationBonus { get; set; }

        /// <summary>Erhöhte Chance auf Premium-Auftragstypen (Large/Weekly/Cooperation).</summary>
        [JsonProperty("premiumOrderChance")]
        public decimal PremiumOrderChance { get; set; }

        // ── Logistik-Branch ──

        /// <summary>Zusätzliche Lager-Slots (additiv zu Default 20 + Geld-Upgrades).</summary>
        [JsonProperty("bonusWarehouseSlots")]
        public int BonusWarehouseSlots { get; set; }

        /// <summary>Multiplikator auf Stack-Limit (z.B. 2.0 = doppelt so viel pro Slot).</summary>
        [JsonProperty("stackLimitMultiplier")]
        public decimal StackLimitMultiplier { get; set; }

        /// <summary>Markt-Verfügbarkeit (logi_05).</summary>
        [JsonProperty("unlocksMarket")]
        public bool UnlocksMarket { get; set; }

        /// <summary>Auto-Verkaufs-Regeln (Min/Max pro Slot, logi_07).</summary>
        [JsonProperty("unlocksAutoSellRules")]
        public bool UnlocksAutoSellRules { get; set; }

        /// <summary>Tier-4-Rezepte (Trigger, logi_09).</summary>
        [JsonProperty("unlocksTier4")]
        public bool UnlocksTier4 { get; set; }

        /// <summary>Crafting-Speed-Bonus (logi_10, additiv mit Prestige-Speed-Bonus).</summary>
        [JsonProperty("craftingSpeedBonus")]
        public decimal CraftingSpeedBonus { get; set; }

        /// <summary>Lieferanten-Material-Lieferung Bonus (logi_08).</summary>
        [JsonProperty("supplierMaterialBonus")]
        public decimal SupplierMaterialBonus { get; set; }

        /// <summary>Erbstücke überleben Prestige (Trigger, logi_12).</summary>
        [JsonProperty("unlocksHeirloomSurvival")]
        public bool UnlocksHeirloomSurvival { get; set; }

        /// <summary>Kombiniert zwei Forschungs-Effekte additiv.</summary>
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
}
