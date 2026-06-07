#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace HandwerkerImperium.Domain.Research
{
    /// <summary>
    /// Statische Definition aller Research-Nodes über 4 Branches: Tools / Management /
    /// Marketing / Logistics. 72 Knoten (3×20 + 12), Kosten und Dauer steigen
    /// exponentiell. Endgame-Forschungen erreichen 5B-100B Kosten und 96-168h Dauer.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/ResearchTree.cs).
    /// </summary>
    public static class ResearchTree
    {
        public static List<Research> CreateAll()
        {
            var all = new List<Research>();
            all.AddRange(CreateToolsBranch());
            all.AddRange(CreateManagementBranch());
            all.AddRange(CreateMarketingBranch());
            all.AddRange(CreateLogisticsBranch());
            return all;
        }

        /// <summary>
        /// 12 Logistik-Forschungen — Lager-Slots, Stack-Limit, Markt-Verfügbarkeit,
        /// Auto-Verkauf-Regeln, Crafting-Speed, T4-Trigger, Erbstück-Survival.
        /// </summary>
        private static List<Research> CreateLogisticsBranch()
        {
            return new List<Research>
            {
                // Zeile 0: Basis-Slots
                Create("logi_01", ResearchBranch.Logistics, 1, "ResearchLogiSlots1", 50_000m, TimeSpan.FromMinutes(30),
                    new ResearchEffect { BonusWarehouseSlots = 5 }),
                // Zeile 1: Stack-Verdoppelung
                Create("logi_02", ResearchBranch.Logistics, 2, "ResearchLogiStack2x", 200_000m, TimeSpan.FromHours(1),
                    new ResearchEffect { StackLimitMultiplier = 2.0m }, new[] { "logi_01" }),
                // Zeile 2: Markt freischalten
                Create("logi_05", ResearchBranch.Logistics, 3, "ResearchLogiMarket", 500_000m, TimeSpan.FromHours(2),
                    new ResearchEffect { UnlocksMarket = true }, new[] { "logi_02" }),
                // Zeile 3: Mehr Slots
                Create("logi_04", ResearchBranch.Logistics, 4, "ResearchLogiSlots2", 1_500_000m, TimeSpan.FromHours(3),
                    new ResearchEffect { BonusWarehouseSlots = 10 }, new[] { "logi_05" }),
                // Zeile 4: Lieferanten-Material-Bonus
                Create("logi_08", ResearchBranch.Logistics, 5, "ResearchLogiSupplier", 4_000_000m, TimeSpan.FromHours(6),
                    new ResearchEffect { SupplierMaterialBonus = 0.50m }, new[] { "logi_04" }),
                // Zeile 5: Auto-Verkauf-Regeln
                Create("logi_07", ResearchBranch.Logistics, 6, "ResearchLogiAutoSell", 10_000_000m, TimeSpan.FromHours(8),
                    new ResearchEffect { UnlocksAutoSellRules = true }, new[] { "logi_08" }),
                // Zeile 6: Crafting-Speed
                Create("logi_10", ResearchBranch.Logistics, 7, "ResearchLogiCraftSpeed", 25_000_000m, TimeSpan.FromHours(12),
                    new ResearchEffect { CraftingSpeedBonus = 0.20m }, new[] { "logi_07" }),
                // Zeile 7: Stack-Limit x5 (kombiniert mit logi_02 → x10)
                Create("logi_11", ResearchBranch.Logistics, 8, "ResearchLogiStack5x", 60_000_000m, TimeSpan.FromHours(16),
                    new ResearchEffect { StackLimitMultiplier = 5.0m }, new[] { "logi_10" }),
                // Zeile 8: T4-Rezepte (trigger)
                Create("logi_09", ResearchBranch.Logistics, 9, "ResearchLogiTier4", 150_000_000m, TimeSpan.FromHours(24),
                    new ResearchEffect { UnlocksTier4 = true }, new[] { "logi_11" }),
                // F-36: Top-3-Nodes auf max 24h gekappt (vorher 32h / 48h / 72h).
                // Zeile 9: Bonus-Slots Premium
                Create("logi_03", ResearchBranch.Logistics, 10, "ResearchLogiSlots3", 400_000_000m, TimeSpan.FromHours(24),
                    new ResearchEffect { BonusWarehouseSlots = 25 }, new[] { "logi_09" }),
                // Zeile 10: Erbstück-Survival
                Create("logi_12", ResearchBranch.Logistics, 11, "ResearchLogiHeirloom", 1_000_000_000m, TimeSpan.FromHours(24),
                    new ResearchEffect { UnlocksHeirloomSurvival = true }, new[] { "logi_03" }),
                // Zeile 11: Master-Logistik-Speedup
                Create("logi_06", ResearchBranch.Logistics, 12, "ResearchLogiMaster", 5_000_000_000m, TimeSpan.FromHours(24),
                    new ResearchEffect { CraftingSpeedBonus = 0.30m, BonusWarehouseSlots = 25 }, new[] { "logi_12" }),
            };
        }

        private static List<Research> CreateToolsBranch()
        {
            // Baum-Layout: Zeile 0=1 zentriert, Zeile 1=2 nebeneinander, Zeile 2=1 zentriert, ...
            return new List<Research>
            {
                // Zeile 0: [1] zentriert
                Create("tools_01", ResearchBranch.Tools, 1, "ResearchBetterSaws", 500m, TimeSpan.FromMinutes(10),
                    new ResearchEffect { EfficiencyBonus = 0.05m }),
                // Zeile 1: [2] [3] - beide hängen von [1] ab
                Create("tools_02", ResearchBranch.Tools, 2, "ResearchPrecisionTools", 2_000m, TimeSpan.FromMinutes(30),
                    new ResearchEffect { MiniGameZoneBonus = 0.02m }, new[] { "tools_01" }),
                Create("tools_03", ResearchBranch.Tools, 3, "ResearchPowerTools", 8_000m, TimeSpan.FromHours(1),
                    new ResearchEffect { EfficiencyBonus = 0.05m }, new[] { "tools_01" }),
                // Zeile 2: [4] zentriert - hängt von [2] UND [3] ab
                Create("tools_04", ResearchBranch.Tools, 4, "ResearchAutoMaterial", 25_000m, TimeSpan.FromHours(2),
                    new ResearchEffect { UnlocksAutoMaterial = true }, new[] { "tools_02", "tools_03" }),
                // Zeile 3: [5] [6] - beide hängen von [4] ab
                Create("tools_05", ResearchBranch.Tools, 5, "ResearchAdvancedMachinery", 80_000m, TimeSpan.FromHours(4),
                    new ResearchEffect { EfficiencyBonus = 0.08m }, new[] { "tools_04" }),
                Create("tools_06", ResearchBranch.Tools, 6, "ResearchQualityControl", 200_000m, TimeSpan.FromHours(6),
                    new ResearchEffect { MiniGameZoneBonus = 0.03m }, new[] { "tools_04" }),
                // Zeile 4: [7] zentriert - hängt von [5] UND [6] ab
                Create("tools_07", ResearchBranch.Tools, 7, "ResearchCncMachines", 500_000m, TimeSpan.FromHours(8),
                    new ResearchEffect { EfficiencyBonus = 0.10m }, new[] { "tools_05", "tools_06" }),
                // Zeile 5: [8] [9] - beide hängen von [7] ab
                Create("tools_08", ResearchBranch.Tools, 8, "ResearchLaserCutting", 1_000_000m, TimeSpan.FromHours(12),
                    new ResearchEffect { MiniGameZoneBonus = 0.03m }, new[] { "tools_07" }),
                Create("tools_09", ResearchBranch.Tools, 9, "ResearchRobotics", 3_000_000m, TimeSpan.FromHours(16),
                    new ResearchEffect { EfficiencyBonus = 0.10m }, new[] { "tools_07" }),
                // Zeile 6: [10] zentriert - hängt von [8] UND [9] ab
                Create("tools_10", ResearchBranch.Tools, 10, "Research3dPrinting", 8_000_000m, TimeSpan.FromHours(24),
                    new ResearchEffect { CostReduction = 0.10m }, new[] { "tools_08", "tools_09" }),
                // Zeile 7: [11] [12] - beide hängen von [10] ab
                Create("tools_11", ResearchBranch.Tools, 11, "ResearchSmartFactory", 20_000_000m, TimeSpan.FromHours(32),
                    new ResearchEffect { EfficiencyBonus = 0.12m }, new[] { "tools_10" }),
                Create("tools_12", ResearchBranch.Tools, 12, "ResearchNanotech", 50_000_000m, TimeSpan.FromHours(40),
                    new ResearchEffect { MiniGameZoneBonus = 0.04m }, new[] { "tools_10" }),
                // Zeile 8: [13] zentriert - hängt von [11] UND [12] ab
                Create("tools_13", ResearchBranch.Tools, 13, "ResearchQuantumMeasure", 100_000_000m, TimeSpan.FromHours(48),
                    new ResearchEffect { EfficiencyBonus = 0.15m }, new[] { "tools_11", "tools_12" }),
                // Zeile 9: [14] [15] - beide hängen von [13] ab
                Create("tools_14", ResearchBranch.Tools, 14, "ResearchAiAssisted", 300_000_000m, TimeSpan.FromHours(60),
                    new ResearchEffect { CostReduction = 0.15m }, new[] { "tools_13" }),
                Create("tools_15", ResearchBranch.Tools, 15, "ResearchMasterCraftsman", 1_000_000_000m, TimeSpan.FromHours(72),
                    new ResearchEffect { EfficiencyBonus = 0.20m, MiniGameZoneBonus = 0.05m }, new[] { "tools_13" }),

                // === Endgame (Level 16-20) - Ascension-Boni + Workshop-Synergie ===
                // Zeile 10: [16] zentriert - hängt von [14] UND [15] ab
                Create("tools_16", ResearchBranch.Tools, 16, "ResearchDimensionalForge", 5_000_000_000m, TimeSpan.FromHours(96),
                    new ResearchEffect { EfficiencyBonus = 0.20m, CostReduction = 0.10m }, new[] { "tools_14", "tools_15" }),
                // Zeile 11: [17] [18] - beide hängen von [16] ab
                Create("tools_17", ResearchBranch.Tools, 17, "ResearchAscensionCatalyst", 10_000_000_000m, TimeSpan.FromHours(108),
                    new ResearchEffect { AscensionPointBonus = 0.15m }, new[] { "tools_16" }),
                Create("tools_18", ResearchBranch.Tools, 18, "ResearchMolecularAssembly", 20_000_000_000m, TimeSpan.FromHours(120),
                    new ResearchEffect { EfficiencyBonus = 0.25m, MiniGameZoneBonus = 0.06m }, new[] { "tools_16" }),
                // Zeile 12: [19] zentriert - hängt von [17] UND [18] ab
                Create("tools_19", ResearchBranch.Tools, 19, "ResearchWorkshopSynergy", 50_000_000_000m, TimeSpan.FromHours(144),
                    new ResearchEffect { WorkshopSynergyBonus = 0.02m }, new[] { "tools_17", "tools_18" }),
                // Zeile 13: [20] Krönungs-Node - hängt von [19] ab
                Create("tools_20", ResearchBranch.Tools, 20, "ResearchEternalForge", 100_000_000_000m, TimeSpan.FromHours(168),
                    new ResearchEffect { EfficiencyBonus = 0.30m, AscensionPointBonus = 0.25m, MiniGameZoneBonus = 0.08m, WorkshopSynergyBonus = 0.03m }, new[] { "tools_19" }),
            };
        }

        private static List<Research> CreateManagementBranch()
        {
            // Baum-Layout: Zeile 0=1 zentriert, Zeile 1=2 nebeneinander, Zeile 2=1 zentriert, ...
            return new List<Research>
            {
                // Zeile 0: [1] zentriert
                Create("mgmt_01", ResearchBranch.Management, 1, "ResearchHrBasics", 500m, TimeSpan.FromMinutes(10),
                    new ResearchEffect { WageReduction = 0.05m }),
                // Zeile 1: [2] [3] - beide hängen von [1] ab
                Create("mgmt_02", ResearchBranch.Management, 2, "ResearchTeamBuilding", 2_000m, TimeSpan.FromMinutes(30),
                    new ResearchEffect { ExtraWorkerSlots = 1 }, new[] { "mgmt_01" }),
                Create("mgmt_03", ResearchBranch.Management, 3, "ResearchMotivation", 8_000m, TimeSpan.FromHours(1),
                    new ResearchEffect { WageReduction = 0.05m }, new[] { "mgmt_01" }),
                // Zeile 2: [4] zentriert - hängt von [2] UND [3] ab
                Create("mgmt_04", ResearchBranch.Management, 4, "ResearchHeadhunter", 25_000m, TimeSpan.FromHours(2),
                    new ResearchEffect { UnlocksHeadhunter = true }, new[] { "mgmt_02", "mgmt_03" }),
                // Zeile 3: [5] [6] - beide hängen von [4] ab
                Create("mgmt_05", ResearchBranch.Management, 5, "ResearchTrainingProgram", 80_000m, TimeSpan.FromHours(4),
                    new ResearchEffect { TrainingSpeedMultiplier = 0.5m, LevelResistanceBonus = 0.05m }, new[] { "mgmt_04" }),
                Create("mgmt_06", ResearchBranch.Management, 6, "ResearchWorkLifeBalance", 200_000m, TimeSpan.FromHours(6),
                    new ResearchEffect { WageReduction = 0.08m }, new[] { "mgmt_04" }),
                // Zeile 4: [7] zentriert - hängt von [5] UND [6] ab
                Create("mgmt_07", ResearchBranch.Management, 7, "ResearchAutoAssign", 500_000m, TimeSpan.FromHours(8),
                    new ResearchEffect { UnlocksAutoAssign = true }, new[] { "mgmt_05", "mgmt_06" }),
                // Zeile 5: [8] [9] - beide hängen von [7] ab
                Create("mgmt_08", ResearchBranch.Management, 8, "ResearchTalentScout", 1_000_000m, TimeSpan.FromHours(12),
                    new ResearchEffect { ExtraWorkerSlots = 1 }, new[] { "mgmt_07" }),
                Create("mgmt_09", ResearchBranch.Management, 9, "ResearchLeadership", 3_000_000m, TimeSpan.FromHours(16),
                    new ResearchEffect { WageReduction = 0.10m, LevelResistanceBonus = 0.08m }, new[] { "mgmt_07" }),
                // Zeile 6: [10] zentriert - hängt von [8] UND [9] ab
                Create("mgmt_10", ResearchBranch.Management, 10, "ResearchEliteRecruitment", 8_000_000m, TimeSpan.FromHours(24),
                    new ResearchEffect { UnlocksSTierWorkers = true }, new[] { "mgmt_08", "mgmt_09" }),
                // Zeile 7: [11] [12] - beide hängen von [10] ab
                Create("mgmt_11", ResearchBranch.Management, 11, "ResearchMentorship", 20_000_000m, TimeSpan.FromHours(32),
                    new ResearchEffect { TrainingSpeedMultiplier = 0.5m, LevelResistanceBonus = 0.07m }, new[] { "mgmt_10" }),
                Create("mgmt_12", ResearchBranch.Management, 12, "ResearchCorporateCulture", 50_000_000m, TimeSpan.FromHours(40),
                    new ResearchEffect { WageReduction = 0.10m }, new[] { "mgmt_10" }),
                // Zeile 8: [13] zentriert - hängt von [11] UND [12] ab
                Create("mgmt_13", ResearchBranch.Management, 13, "ResearchGlobalTalent", 100_000_000m, TimeSpan.FromHours(48),
                    new ResearchEffect { ExtraWorkerSlots = 2 }, new[] { "mgmt_11", "mgmt_12" }),
                // Zeile 9: [14] [15] - beide hängen von [13] ab
                Create("mgmt_14", ResearchBranch.Management, 14, "ResearchAiManagement", 300_000_000m, TimeSpan.FromHours(60),
                    new ResearchEffect { WageReduction = 0.12m, LevelResistanceBonus = 0.10m }, new[] { "mgmt_13" }),
                Create("mgmt_15", ResearchBranch.Management, 15, "ResearchMasterManager", 1_000_000_000m, TimeSpan.FromHours(72),
                    new ResearchEffect { ExtraWorkerSlots = 2, WageReduction = 0.15m, LevelResistanceBonus = 0.10m }, new[] { "mgmt_13" }),

                // === Endgame (Level 16-20) - Auto-Training + Masseneinstellung ===
                // Zeile 10: [16] zentriert - hängt von [14] UND [15] ab
                Create("mgmt_16", ResearchBranch.Management, 16, "ResearchTalentAcademy", 5_000_000_000m, TimeSpan.FromHours(96),
                    new ResearchEffect { WageReduction = 0.15m, ExtraWorkerSlots = 2 }, new[] { "mgmt_14", "mgmt_15" }),
                // Zeile 11: [17] [18] - beide hängen von [16] ab
                Create("mgmt_17", ResearchBranch.Management, 17, "ResearchAutoTraining", 10_000_000_000m, TimeSpan.FromHours(108),
                    new ResearchEffect { UnlocksAutoTraining = true, TrainingSpeedMultiplier = 0.5m }, new[] { "mgmt_16" }),
                Create("mgmt_18", ResearchBranch.Management, 18, "ResearchMasterMentor", 20_000_000_000m, TimeSpan.FromHours(120),
                    new ResearchEffect { LevelResistanceBonus = 0.15m, TrainingSpeedMultiplier = 1.0m }, new[] { "mgmt_16" }),
                // Zeile 12: [19] zentriert - hängt von [17] UND [18] ab
                Create("mgmt_19", ResearchBranch.Management, 19, "ResearchMassHiring", 50_000_000_000m, TimeSpan.FromHours(144),
                    new ResearchEffect { UnlocksMassHiring = true, ExtraWorkerSlots = 3 }, new[] { "mgmt_17", "mgmt_18" }),
                // Zeile 13: [20] Krönungs-Node - hängt von [19] ab
                Create("mgmt_20", ResearchBranch.Management, 20, "ResearchLegendaryLeader", 100_000_000_000m, TimeSpan.FromHours(168),
                    new ResearchEffect { ExtraWorkerSlots = 3, WageReduction = 0.20m, TrainingSpeedMultiplier = 1.0m, LevelResistanceBonus = 0.15m }, new[] { "mgmt_19" }),
            };
        }

        private static List<Research> CreateMarketingBranch()
        {
            // Baum-Layout: Zeile 0=1 zentriert, Zeile 1=2 nebeneinander, Zeile 2=1 zentriert, ...
            return new List<Research>
            {
                // Zeile 0: [1] zentriert
                Create("mkt_01", ResearchBranch.Marketing, 1, "ResearchLocalAds", 500m, TimeSpan.FromMinutes(10),
                    new ResearchEffect { RewardMultiplier = 0.05m }),
                // Zeile 1: [2] [3] - beide hängen von [1] ab
                Create("mkt_02", ResearchBranch.Marketing, 2, "ResearchOnlinePresence", 2_000m, TimeSpan.FromMinutes(30),
                    new ResearchEffect { ExtraOrderSlots = 1 }, new[] { "mkt_01" }),
                Create("mkt_03", ResearchBranch.Marketing, 3, "ResearchBranding", 8_000m, TimeSpan.FromHours(1),
                    new ResearchEffect { RewardMultiplier = 0.05m }, new[] { "mkt_01" }),
                // Zeile 2: [4] zentriert - hängt von [2] UND [3] ab
                Create("mkt_04", ResearchBranch.Marketing, 4, "ResearchReferralProgram", 25_000m, TimeSpan.FromHours(2),
                    new ResearchEffect { RewardMultiplier = 0.08m }, new[] { "mkt_02", "mkt_03" }),
                // Zeile 3: [5] [6] - beide hängen von [4] ab
                Create("mkt_05", ResearchBranch.Marketing, 5, "ResearchPremiumBrand", 80_000m, TimeSpan.FromHours(4),
                    new ResearchEffect { ExtraOrderSlots = 1 }, new[] { "mkt_04" }),
                Create("mkt_06", ResearchBranch.Marketing, 6, "ResearchSocialMedia", 200_000m, TimeSpan.FromHours(6),
                    new ResearchEffect { RewardMultiplier = 0.08m }, new[] { "mkt_04" }),
                // Zeile 4: [7] zentriert - hängt von [5] UND [6] ab
                Create("mkt_07", ResearchBranch.Marketing, 7, "ResearchPublicRelations", 500_000m, TimeSpan.FromHours(8),
                    new ResearchEffect { RewardMultiplier = 0.10m }, new[] { "mkt_05", "mkt_06" }),
                // Zeile 5: [8] [9] - beide hängen von [7] ab
                Create("mkt_08", ResearchBranch.Marketing, 8, "ResearchTvCampaign", 1_000_000m, TimeSpan.FromHours(12),
                    new ResearchEffect { ExtraOrderSlots = 1 }, new[] { "mkt_07" }),
                Create("mkt_09", ResearchBranch.Marketing, 9, "ResearchInternational", 3_000_000m, TimeSpan.FromHours(16),
                    new ResearchEffect { RewardMultiplier = 0.10m }, new[] { "mkt_07" }),
                // Zeile 6: [10] zentriert - hängt von [8] UND [9] ab
                Create("mkt_10", ResearchBranch.Marketing, 10, "ResearchLuxuryBrand", 8_000_000m, TimeSpan.FromHours(24),
                    new ResearchEffect { RewardMultiplier = 0.12m }, new[] { "mkt_08", "mkt_09" }),
                // Zeile 7: [11] [12] - beide hängen von [10] ab
                Create("mkt_11", ResearchBranch.Marketing, 11, "ResearchFranchise", 20_000_000m, TimeSpan.FromHours(32),
                    new ResearchEffect { ExtraOrderSlots = 2 }, new[] { "mkt_10" }),
                Create("mkt_12", ResearchBranch.Marketing, 12, "ResearchGlobalBrand", 50_000_000m, TimeSpan.FromHours(40),
                    new ResearchEffect { RewardMultiplier = 0.12m }, new[] { "mkt_10" }),
                // Zeile 8: [13] zentriert - hängt von [11] UND [12] ab
                Create("mkt_13", ResearchBranch.Marketing, 13, "ResearchCelebEndorsement", 100_000_000m, TimeSpan.FromHours(48),
                    new ResearchEffect { RewardMultiplier = 0.15m }, new[] { "mkt_11", "mkt_12" }),
                // Zeile 9: [14] [15] - beide hängen von [13] ab
                Create("mkt_14", ResearchBranch.Marketing, 14, "ResearchMonopoly", 300_000_000m, TimeSpan.FromHours(60),
                    new ResearchEffect { ExtraOrderSlots = 2 }, new[] { "mkt_13" }),
                Create("mkt_15", ResearchBranch.Marketing, 15, "ResearchMarketDomination", 1_000_000_000m, TimeSpan.FromHours(72),
                    new ResearchEffect { RewardMultiplier = 0.20m, ExtraOrderSlots = 2 }, new[] { "mkt_13" }),

                // === Endgame (Level 16-20) - Premium-Aufträge + Reputation ===
                // Zeile 10: [16] zentriert - hängt von [14] UND [15] ab
                Create("mkt_16", ResearchBranch.Marketing, 16, "ResearchImperialBrand", 5_000_000_000m, TimeSpan.FromHours(96),
                    new ResearchEffect { RewardMultiplier = 0.20m, ExtraOrderSlots = 2 }, new[] { "mkt_14", "mkt_15" }),
                // Zeile 11: [17] [18] - beide hängen von [16] ab
                Create("mkt_17", ResearchBranch.Marketing, 17, "ResearchReputationEngine", 10_000_000_000m, TimeSpan.FromHours(108),
                    new ResearchEffect { ReputationBonus = 0.10m, RewardMultiplier = 0.10m }, new[] { "mkt_16" }),
                Create("mkt_18", ResearchBranch.Marketing, 18, "ResearchPremiumContracts", 20_000_000_000m, TimeSpan.FromHours(120),
                    new ResearchEffect { PremiumOrderChance = 0.15m, RewardMultiplier = 0.15m }, new[] { "mkt_16" }),
                // Zeile 12: [19] zentriert - hängt von [17] UND [18] ab
                Create("mkt_19", ResearchBranch.Marketing, 19, "ResearchWorldRenown", 50_000_000_000m, TimeSpan.FromHours(144),
                    new ResearchEffect { ReputationBonus = 0.15m, PremiumOrderChance = 0.10m, ExtraOrderSlots = 3 }, new[] { "mkt_17", "mkt_18" }),
                // Zeile 13: [20] Krönungs-Node - hängt von [19] ab
                Create("mkt_20", ResearchBranch.Marketing, 20, "ResearchEternalLegacy", 100_000_000_000m, TimeSpan.FromHours(168),
                    new ResearchEffect { RewardMultiplier = 0.30m, ExtraOrderSlots = 3, ReputationBonus = 0.20m, PremiumOrderChance = 0.15m }, new[] { "mkt_19" }),
            };
        }

        private static Research Create(string id, ResearchBranch branch, int level, string nameKey,
            decimal cost, TimeSpan duration, ResearchEffect effect, string[]? prerequisites = null)
        {
            return new Research
            {
                Id = id,
                Branch = branch,
                Level = level,
                NameKey = nameKey,
                DescriptionKey = nameKey + "Desc",
                Cost = cost,
                DurationTicks = duration.Ticks,
                Effect = effect,
                Prerequisites = prerequisites?.ToList() ?? new List<string>()
            };
        }
    }
}
