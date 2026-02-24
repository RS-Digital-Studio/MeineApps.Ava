using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Statische Definition aller 60 Research-Nodes (3 Branches x 20 Level).
/// Level 1-15: Basis-Forschungen. Level 16-20: Endgame-Forschungen (5B-100B Kosten, 96-168h).
/// </summary>
public static class ResearchTree
{
    public static List<Research> CreateAll()
    {
        var all = new List<Research>();
        all.AddRange(CreateToolsBranch());
        all.AddRange(CreateManagementBranch());
        all.AddRange(CreateMarketingBranch());
        return all;
    }

    private static List<Research> CreateToolsBranch()
    {
        // Baum-Layout: Zeile 0=1 zentriert, Zeile 1=2 nebeneinander, Zeile 2=1 zentriert, ...
        // Prerequisites passen zum Layout: 2er-Reihen hängen vom vorherigen zentrierten Node ab,
        // zentrierte Nodes hängen von BEIDEN Nodes der vorherigen 2er-Reihe ab.
        return
        [
            // Zeile 0: [1] zentriert
            Create("tools_01", ResearchBranch.Tools, 1, "ResearchBetterSaws", 500m, TimeSpan.FromMinutes(10),
                new ResearchEffect { EfficiencyBonus = 0.05m }),
            // Zeile 1: [2] [3] - beide hängen von [1] ab
            Create("tools_02", ResearchBranch.Tools, 2, "ResearchPrecisionTools", 2_000m, TimeSpan.FromMinutes(30),
                new ResearchEffect { MiniGameZoneBonus = 0.02m }, ["tools_01"]),
            Create("tools_03", ResearchBranch.Tools, 3, "ResearchPowerTools", 8_000m, TimeSpan.FromHours(1),
                new ResearchEffect { EfficiencyBonus = 0.05m }, ["tools_01"]),
            // Zeile 2: [4] zentriert - hängt von [2] UND [3] ab
            Create("tools_04", ResearchBranch.Tools, 4, "ResearchAutoMaterial", 25_000m, TimeSpan.FromHours(2),
                new ResearchEffect { UnlocksAutoMaterial = true }, ["tools_02", "tools_03"]),
            // Zeile 3: [5] [6] - beide hängen von [4] ab
            Create("tools_05", ResearchBranch.Tools, 5, "ResearchAdvancedMachinery", 80_000m, TimeSpan.FromHours(4),
                new ResearchEffect { EfficiencyBonus = 0.08m }, ["tools_04"]),
            Create("tools_06", ResearchBranch.Tools, 6, "ResearchQualityControl", 200_000m, TimeSpan.FromHours(6),
                new ResearchEffect { MiniGameZoneBonus = 0.03m }, ["tools_04"]),
            // Zeile 4: [7] zentriert - hängt von [5] UND [6] ab
            Create("tools_07", ResearchBranch.Tools, 7, "ResearchCncMachines", 500_000m, TimeSpan.FromHours(8),
                new ResearchEffect { EfficiencyBonus = 0.10m }, ["tools_05", "tools_06"]),
            // Zeile 5: [8] [9] - beide hängen von [7] ab
            Create("tools_08", ResearchBranch.Tools, 8, "ResearchLaserCutting", 1_000_000m, TimeSpan.FromHours(12),
                new ResearchEffect { MiniGameZoneBonus = 0.03m }, ["tools_07"]),
            Create("tools_09", ResearchBranch.Tools, 9, "ResearchRobotics", 3_000_000m, TimeSpan.FromHours(16),
                new ResearchEffect { EfficiencyBonus = 0.10m }, ["tools_07"]),
            // Zeile 6: [10] zentriert - hängt von [8] UND [9] ab
            Create("tools_10", ResearchBranch.Tools, 10, "Research3dPrinting", 8_000_000m, TimeSpan.FromHours(24),
                new ResearchEffect { CostReduction = 0.10m }, ["tools_08", "tools_09"]),
            // Zeile 7: [11] [12] - beide hängen von [10] ab
            Create("tools_11", ResearchBranch.Tools, 11, "ResearchSmartFactory", 20_000_000m, TimeSpan.FromHours(32),
                new ResearchEffect { EfficiencyBonus = 0.12m }, ["tools_10"]),
            Create("tools_12", ResearchBranch.Tools, 12, "ResearchNanotech", 50_000_000m, TimeSpan.FromHours(40),
                new ResearchEffect { MiniGameZoneBonus = 0.04m }, ["tools_10"]),
            // Zeile 8: [13] zentriert - hängt von [11] UND [12] ab
            Create("tools_13", ResearchBranch.Tools, 13, "ResearchQuantumMeasure", 100_000_000m, TimeSpan.FromHours(48),
                new ResearchEffect { EfficiencyBonus = 0.15m }, ["tools_11", "tools_12"]),
            // Zeile 9: [14] [15] - beide hängen von [13] ab
            Create("tools_14", ResearchBranch.Tools, 14, "ResearchAiAssisted", 300_000_000m, TimeSpan.FromHours(60),
                new ResearchEffect { CostReduction = 0.15m }, ["tools_13"]),
            Create("tools_15", ResearchBranch.Tools, 15, "ResearchMasterCraftsman", 1_000_000_000m, TimeSpan.FromHours(72),
                new ResearchEffect { EfficiencyBonus = 0.20m, MiniGameZoneBonus = 0.05m }, ["tools_13"]),

            // === Endgame (Level 16-20) - Ascension-Boni + Workshop-Synergie ===
            // Zeile 10: [16] zentriert - hängt von [14] UND [15] ab
            Create("tools_16", ResearchBranch.Tools, 16, "ResearchDimensionalForge", 5_000_000_000m, TimeSpan.FromHours(96),
                new ResearchEffect { EfficiencyBonus = 0.20m, CostReduction = 0.10m }, ["tools_14", "tools_15"]),
            // Zeile 11: [17] [18] - beide hängen von [16] ab
            Create("tools_17", ResearchBranch.Tools, 17, "ResearchAscensionCatalyst", 10_000_000_000m, TimeSpan.FromHours(108),
                new ResearchEffect { AscensionPointBonus = 0.15m }, ["tools_16"]),
            Create("tools_18", ResearchBranch.Tools, 18, "ResearchMolecularAssembly", 20_000_000_000m, TimeSpan.FromHours(120),
                new ResearchEffect { EfficiencyBonus = 0.25m, MiniGameZoneBonus = 0.06m }, ["tools_16"]),
            // Zeile 12: [19] zentriert - hängt von [17] UND [18] ab
            Create("tools_19", ResearchBranch.Tools, 19, "ResearchWorkshopSynergy", 50_000_000_000m, TimeSpan.FromHours(144),
                new ResearchEffect { WorkshopSynergyBonus = 0.02m }, ["tools_17", "tools_18"]),
            // Zeile 13: [20] Krönungs-Node - hängt von [19] ab
            Create("tools_20", ResearchBranch.Tools, 20, "ResearchEternalForge", 100_000_000_000m, TimeSpan.FromHours(168),
                new ResearchEffect { EfficiencyBonus = 0.30m, AscensionPointBonus = 0.25m, MiniGameZoneBonus = 0.08m, WorkshopSynergyBonus = 0.03m }, ["tools_19"]),
        ];
    }

    private static List<Research> CreateManagementBranch()
    {
        // Baum-Layout: Zeile 0=1 zentriert, Zeile 1=2 nebeneinander, Zeile 2=1 zentriert, ...
        return
        [
            // Zeile 0: [1] zentriert
            Create("mgmt_01", ResearchBranch.Management, 1, "ResearchHrBasics", 500m, TimeSpan.FromMinutes(10),
                new ResearchEffect { WageReduction = 0.05m }),
            // Zeile 1: [2] [3] - beide hängen von [1] ab
            Create("mgmt_02", ResearchBranch.Management, 2, "ResearchTeamBuilding", 2_000m, TimeSpan.FromMinutes(30),
                new ResearchEffect { ExtraWorkerSlots = 1 }, ["mgmt_01"]),
            Create("mgmt_03", ResearchBranch.Management, 3, "ResearchMotivation", 8_000m, TimeSpan.FromHours(1),
                new ResearchEffect { WageReduction = 0.05m }, ["mgmt_01"]),
            // Zeile 2: [4] zentriert - hängt von [2] UND [3] ab
            Create("mgmt_04", ResearchBranch.Management, 4, "ResearchHeadhunter", 25_000m, TimeSpan.FromHours(2),
                new ResearchEffect { UnlocksHeadhunter = true }, ["mgmt_02", "mgmt_03"]),
            // Zeile 3: [5] [6] - beide hängen von [4] ab
            Create("mgmt_05", ResearchBranch.Management, 5, "ResearchTrainingProgram", 80_000m, TimeSpan.FromHours(4),
                new ResearchEffect { TrainingSpeedMultiplier = 0.5m, LevelResistanceBonus = 0.05m }, ["mgmt_04"]),
            Create("mgmt_06", ResearchBranch.Management, 6, "ResearchWorkLifeBalance", 200_000m, TimeSpan.FromHours(6),
                new ResearchEffect { WageReduction = 0.08m }, ["mgmt_04"]),
            // Zeile 4: [7] zentriert - hängt von [5] UND [6] ab
            Create("mgmt_07", ResearchBranch.Management, 7, "ResearchAutoAssign", 500_000m, TimeSpan.FromHours(8),
                new ResearchEffect { UnlocksAutoAssign = true }, ["mgmt_05", "mgmt_06"]),
            // Zeile 5: [8] [9] - beide hängen von [7] ab
            Create("mgmt_08", ResearchBranch.Management, 8, "ResearchTalentScout", 1_000_000m, TimeSpan.FromHours(12),
                new ResearchEffect { ExtraWorkerSlots = 1 }, ["mgmt_07"]),
            Create("mgmt_09", ResearchBranch.Management, 9, "ResearchLeadership", 3_000_000m, TimeSpan.FromHours(16),
                new ResearchEffect { WageReduction = 0.10m, LevelResistanceBonus = 0.08m }, ["mgmt_07"]),
            // Zeile 6: [10] zentriert - hängt von [8] UND [9] ab
            Create("mgmt_10", ResearchBranch.Management, 10, "ResearchEliteRecruitment", 8_000_000m, TimeSpan.FromHours(24),
                new ResearchEffect { UnlocksSTierWorkers = true }, ["mgmt_08", "mgmt_09"]),
            // Zeile 7: [11] [12] - beide hängen von [10] ab
            Create("mgmt_11", ResearchBranch.Management, 11, "ResearchMentorship", 20_000_000m, TimeSpan.FromHours(32),
                new ResearchEffect { TrainingSpeedMultiplier = 0.5m, LevelResistanceBonus = 0.07m }, ["mgmt_10"]),
            Create("mgmt_12", ResearchBranch.Management, 12, "ResearchCorporateCulture", 50_000_000m, TimeSpan.FromHours(40),
                new ResearchEffect { WageReduction = 0.10m }, ["mgmt_10"]),
            // Zeile 8: [13] zentriert - hängt von [11] UND [12] ab
            Create("mgmt_13", ResearchBranch.Management, 13, "ResearchGlobalTalent", 100_000_000m, TimeSpan.FromHours(48),
                new ResearchEffect { ExtraWorkerSlots = 2 }, ["mgmt_11", "mgmt_12"]),
            // Zeile 9: [14] [15] - beide hängen von [13] ab
            Create("mgmt_14", ResearchBranch.Management, 14, "ResearchAiManagement", 300_000_000m, TimeSpan.FromHours(60),
                new ResearchEffect { WageReduction = 0.12m, LevelResistanceBonus = 0.10m }, ["mgmt_13"]),
            Create("mgmt_15", ResearchBranch.Management, 15, "ResearchMasterManager", 1_000_000_000m, TimeSpan.FromHours(72),
                new ResearchEffect { ExtraWorkerSlots = 2, WageReduction = 0.15m, LevelResistanceBonus = 0.10m }, ["mgmt_13"]),

            // === Endgame (Level 16-20) - Auto-Training + Masseneinstellung ===
            // Zeile 10: [16] zentriert - hängt von [14] UND [15] ab
            Create("mgmt_16", ResearchBranch.Management, 16, "ResearchTalentAcademy", 5_000_000_000m, TimeSpan.FromHours(96),
                new ResearchEffect { WageReduction = 0.15m, ExtraWorkerSlots = 2 }, ["mgmt_14", "mgmt_15"]),
            // Zeile 11: [17] [18] - beide hängen von [16] ab
            Create("mgmt_17", ResearchBranch.Management, 17, "ResearchAutoTraining", 10_000_000_000m, TimeSpan.FromHours(108),
                new ResearchEffect { UnlocksAutoTraining = true, TrainingSpeedMultiplier = 0.5m }, ["mgmt_16"]),
            Create("mgmt_18", ResearchBranch.Management, 18, "ResearchMasterMentor", 20_000_000_000m, TimeSpan.FromHours(120),
                new ResearchEffect { LevelResistanceBonus = 0.15m, TrainingSpeedMultiplier = 1.0m }, ["mgmt_16"]),
            // Zeile 12: [19] zentriert - hängt von [17] UND [18] ab
            Create("mgmt_19", ResearchBranch.Management, 19, "ResearchMassHiring", 50_000_000_000m, TimeSpan.FromHours(144),
                new ResearchEffect { UnlocksMassHiring = true, ExtraWorkerSlots = 3 }, ["mgmt_17", "mgmt_18"]),
            // Zeile 13: [20] Krönungs-Node - hängt von [19] ab
            Create("mgmt_20", ResearchBranch.Management, 20, "ResearchLegendaryLeader", 100_000_000_000m, TimeSpan.FromHours(168),
                new ResearchEffect { ExtraWorkerSlots = 3, WageReduction = 0.20m, TrainingSpeedMultiplier = 1.0m, LevelResistanceBonus = 0.15m }, ["mgmt_19"]),
        ];
    }

    private static List<Research> CreateMarketingBranch()
    {
        // Baum-Layout: Zeile 0=1 zentriert, Zeile 1=2 nebeneinander, Zeile 2=1 zentriert, ...
        return
        [
            // Zeile 0: [1] zentriert
            Create("mkt_01", ResearchBranch.Marketing, 1, "ResearchLocalAds", 500m, TimeSpan.FromMinutes(10),
                new ResearchEffect { RewardMultiplier = 0.05m }),
            // Zeile 1: [2] [3] - beide hängen von [1] ab
            Create("mkt_02", ResearchBranch.Marketing, 2, "ResearchOnlinePresence", 2_000m, TimeSpan.FromMinutes(30),
                new ResearchEffect { ExtraOrderSlots = 1 }, ["mkt_01"]),
            Create("mkt_03", ResearchBranch.Marketing, 3, "ResearchBranding", 8_000m, TimeSpan.FromHours(1),
                new ResearchEffect { RewardMultiplier = 0.05m }, ["mkt_01"]),
            // Zeile 2: [4] zentriert - hängt von [2] UND [3] ab
            Create("mkt_04", ResearchBranch.Marketing, 4, "ResearchReferralProgram", 25_000m, TimeSpan.FromHours(2),
                new ResearchEffect { RewardMultiplier = 0.08m }, ["mkt_02", "mkt_03"]),
            // Zeile 3: [5] [6] - beide hängen von [4] ab
            Create("mkt_05", ResearchBranch.Marketing, 5, "ResearchPremiumBrand", 80_000m, TimeSpan.FromHours(4),
                new ResearchEffect { ExtraOrderSlots = 1 }, ["mkt_04"]),
            Create("mkt_06", ResearchBranch.Marketing, 6, "ResearchSocialMedia", 200_000m, TimeSpan.FromHours(6),
                new ResearchEffect { RewardMultiplier = 0.08m }, ["mkt_04"]),
            // Zeile 4: [7] zentriert - hängt von [5] UND [6] ab
            Create("mkt_07", ResearchBranch.Marketing, 7, "ResearchPublicRelations", 500_000m, TimeSpan.FromHours(8),
                new ResearchEffect { RewardMultiplier = 0.10m }, ["mkt_05", "mkt_06"]),
            // Zeile 5: [8] [9] - beide hängen von [7] ab
            Create("mkt_08", ResearchBranch.Marketing, 8, "ResearchTvCampaign", 1_000_000m, TimeSpan.FromHours(12),
                new ResearchEffect { ExtraOrderSlots = 1 }, ["mkt_07"]),
            Create("mkt_09", ResearchBranch.Marketing, 9, "ResearchInternational", 3_000_000m, TimeSpan.FromHours(16),
                new ResearchEffect { RewardMultiplier = 0.10m }, ["mkt_07"]),
            // Zeile 6: [10] zentriert - hängt von [8] UND [9] ab
            Create("mkt_10", ResearchBranch.Marketing, 10, "ResearchLuxuryBrand", 8_000_000m, TimeSpan.FromHours(24),
                new ResearchEffect { RewardMultiplier = 0.12m }, ["mkt_08", "mkt_09"]),
            // Zeile 7: [11] [12] - beide hängen von [10] ab
            Create("mkt_11", ResearchBranch.Marketing, 11, "ResearchFranchise", 20_000_000m, TimeSpan.FromHours(32),
                new ResearchEffect { ExtraOrderSlots = 2 }, ["mkt_10"]),
            Create("mkt_12", ResearchBranch.Marketing, 12, "ResearchGlobalBrand", 50_000_000m, TimeSpan.FromHours(40),
                new ResearchEffect { RewardMultiplier = 0.12m }, ["mkt_10"]),
            // Zeile 8: [13] zentriert - hängt von [11] UND [12] ab
            Create("mkt_13", ResearchBranch.Marketing, 13, "ResearchCelebEndorsement", 100_000_000m, TimeSpan.FromHours(48),
                new ResearchEffect { RewardMultiplier = 0.15m }, ["mkt_11", "mkt_12"]),
            // Zeile 9: [14] [15] - beide hängen von [13] ab
            Create("mkt_14", ResearchBranch.Marketing, 14, "ResearchMonopoly", 300_000_000m, TimeSpan.FromHours(60),
                new ResearchEffect { ExtraOrderSlots = 2 }, ["mkt_13"]),
            Create("mkt_15", ResearchBranch.Marketing, 15, "ResearchMarketDomination", 1_000_000_000m, TimeSpan.FromHours(72),
                new ResearchEffect { RewardMultiplier = 0.20m, ExtraOrderSlots = 2 }, ["mkt_13"]),

            // === Endgame (Level 16-20) - Premium-Aufträge + Reputation ===
            // Zeile 10: [16] zentriert - hängt von [14] UND [15] ab
            Create("mkt_16", ResearchBranch.Marketing, 16, "ResearchImperialBrand", 5_000_000_000m, TimeSpan.FromHours(96),
                new ResearchEffect { RewardMultiplier = 0.20m, ExtraOrderSlots = 2 }, ["mkt_14", "mkt_15"]),
            // Zeile 11: [17] [18] - beide hängen von [16] ab
            Create("mkt_17", ResearchBranch.Marketing, 17, "ResearchReputationEngine", 10_000_000_000m, TimeSpan.FromHours(108),
                new ResearchEffect { ReputationBonus = 0.10m, RewardMultiplier = 0.10m }, ["mkt_16"]),
            Create("mkt_18", ResearchBranch.Marketing, 18, "ResearchPremiumContracts", 20_000_000_000m, TimeSpan.FromHours(120),
                new ResearchEffect { PremiumOrderChance = 0.15m, RewardMultiplier = 0.15m }, ["mkt_16"]),
            // Zeile 12: [19] zentriert - hängt von [17] UND [18] ab
            Create("mkt_19", ResearchBranch.Marketing, 19, "ResearchWorldRenown", 50_000_000_000m, TimeSpan.FromHours(144),
                new ResearchEffect { ReputationBonus = 0.15m, PremiumOrderChance = 0.10m, ExtraOrderSlots = 3 }, ["mkt_17", "mkt_18"]),
            // Zeile 13: [20] Krönungs-Node - hängt von [19] ab
            Create("mkt_20", ResearchBranch.Marketing, 20, "ResearchEternalLegacy", 100_000_000_000m, TimeSpan.FromHours(168),
                new ResearchEffect { RewardMultiplier = 0.30m, ExtraOrderSlots = 3, ReputationBonus = 0.20m, PremiumOrderChance = 0.15m }, ["mkt_19"]),
        ];
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
            Prerequisites = prerequisites?.ToList() ?? []
        };
    }
}
