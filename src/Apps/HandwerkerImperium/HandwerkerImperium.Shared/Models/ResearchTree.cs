using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Static definition of all 45 research nodes (3 branches x 15 levels).
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
        return
        [
            Create("tools_01", ResearchBranch.Tools, 1, "ResearchBetterSaws", 500m, TimeSpan.FromMinutes(10),
                new ResearchEffect { EfficiencyBonus = 0.05m }),
            Create("tools_02", ResearchBranch.Tools, 2, "ResearchPrecisionTools", 2_000m, TimeSpan.FromMinutes(30),
                new ResearchEffect { MiniGameZoneBonus = 0.02m }, ["tools_01"]),
            Create("tools_03", ResearchBranch.Tools, 3, "ResearchPowerTools", 8_000m, TimeSpan.FromHours(1),
                new ResearchEffect { EfficiencyBonus = 0.05m }, ["tools_02"]),
            Create("tools_04", ResearchBranch.Tools, 4, "ResearchAutoMaterial", 25_000m, TimeSpan.FromHours(2),
                new ResearchEffect { UnlocksAutoMaterial = true }, ["tools_03"]),
            Create("tools_05", ResearchBranch.Tools, 5, "ResearchAdvancedMachinery", 80_000m, TimeSpan.FromHours(4),
                new ResearchEffect { EfficiencyBonus = 0.08m }, ["tools_04"]),
            Create("tools_06", ResearchBranch.Tools, 6, "ResearchQualityControl", 200_000m, TimeSpan.FromHours(6),
                new ResearchEffect { MiniGameZoneBonus = 0.03m }, ["tools_05"]),
            Create("tools_07", ResearchBranch.Tools, 7, "ResearchCncMachines", 500_000m, TimeSpan.FromHours(8),
                new ResearchEffect { EfficiencyBonus = 0.10m }, ["tools_06"]),
            Create("tools_08", ResearchBranch.Tools, 8, "ResearchLaserCutting", 1_000_000m, TimeSpan.FromHours(12),
                new ResearchEffect { MiniGameZoneBonus = 0.03m }, ["tools_07"]),
            Create("tools_09", ResearchBranch.Tools, 9, "ResearchRobotics", 3_000_000m, TimeSpan.FromHours(16),
                new ResearchEffect { EfficiencyBonus = 0.10m }, ["tools_08"]),
            Create("tools_10", ResearchBranch.Tools, 10, "Research3dPrinting", 8_000_000m, TimeSpan.FromHours(24),
                new ResearchEffect { CostReduction = 0.10m }, ["tools_09"]),
            Create("tools_11", ResearchBranch.Tools, 11, "ResearchSmartFactory", 20_000_000m, TimeSpan.FromHours(32),
                new ResearchEffect { EfficiencyBonus = 0.12m }, ["tools_10"]),
            Create("tools_12", ResearchBranch.Tools, 12, "ResearchNanotech", 50_000_000m, TimeSpan.FromHours(40),
                new ResearchEffect { MiniGameZoneBonus = 0.04m }, ["tools_11"]),
            Create("tools_13", ResearchBranch.Tools, 13, "ResearchQuantumMeasure", 100_000_000m, TimeSpan.FromHours(48),
                new ResearchEffect { EfficiencyBonus = 0.15m }, ["tools_12"]),
            Create("tools_14", ResearchBranch.Tools, 14, "ResearchAiAssisted", 300_000_000m, TimeSpan.FromHours(60),
                new ResearchEffect { CostReduction = 0.15m }, ["tools_13"]),
            Create("tools_15", ResearchBranch.Tools, 15, "ResearchMasterCraftsman", 1_000_000_000m, TimeSpan.FromHours(72),
                new ResearchEffect { EfficiencyBonus = 0.20m, MiniGameZoneBonus = 0.05m }, ["tools_14"]),
        ];
    }

    private static List<Research> CreateManagementBranch()
    {
        return
        [
            Create("mgmt_01", ResearchBranch.Management, 1, "ResearchHrBasics", 500m, TimeSpan.FromMinutes(10),
                new ResearchEffect { WageReduction = 0.05m }),
            Create("mgmt_02", ResearchBranch.Management, 2, "ResearchTeamBuilding", 2_000m, TimeSpan.FromMinutes(30),
                new ResearchEffect { ExtraWorkerSlots = 1 }, ["mgmt_01"]),
            Create("mgmt_03", ResearchBranch.Management, 3, "ResearchMotivation", 8_000m, TimeSpan.FromHours(1),
                new ResearchEffect { WageReduction = 0.05m }, ["mgmt_02"]),
            Create("mgmt_04", ResearchBranch.Management, 4, "ResearchHeadhunter", 25_000m, TimeSpan.FromHours(2),
                new ResearchEffect { UnlocksHeadhunter = true }, ["mgmt_03"]),
            Create("mgmt_05", ResearchBranch.Management, 5, "ResearchTrainingProgram", 80_000m, TimeSpan.FromHours(4),
                new ResearchEffect { TrainingSpeedMultiplier = 0.5m }, ["mgmt_04"]),
            Create("mgmt_06", ResearchBranch.Management, 6, "ResearchWorkLifeBalance", 200_000m, TimeSpan.FromHours(6),
                new ResearchEffect { WageReduction = 0.08m }, ["mgmt_05"]),
            Create("mgmt_07", ResearchBranch.Management, 7, "ResearchAutoAssign", 500_000m, TimeSpan.FromHours(8),
                new ResearchEffect { UnlocksAutoAssign = true }, ["mgmt_06"]),
            Create("mgmt_08", ResearchBranch.Management, 8, "ResearchTalentScout", 1_000_000m, TimeSpan.FromHours(12),
                new ResearchEffect { ExtraWorkerSlots = 1 }, ["mgmt_07"]),
            Create("mgmt_09", ResearchBranch.Management, 9, "ResearchLeadership", 3_000_000m, TimeSpan.FromHours(16),
                new ResearchEffect { WageReduction = 0.10m }, ["mgmt_08"]),
            Create("mgmt_10", ResearchBranch.Management, 10, "ResearchEliteRecruitment", 8_000_000m, TimeSpan.FromHours(24),
                new ResearchEffect { UnlocksSTierWorkers = true }, ["mgmt_09"]),
            Create("mgmt_11", ResearchBranch.Management, 11, "ResearchMentorship", 20_000_000m, TimeSpan.FromHours(32),
                new ResearchEffect { TrainingSpeedMultiplier = 0.5m }, ["mgmt_10"]),
            Create("mgmt_12", ResearchBranch.Management, 12, "ResearchCorporateCulture", 50_000_000m, TimeSpan.FromHours(40),
                new ResearchEffect { WageReduction = 0.10m }, ["mgmt_11"]),
            Create("mgmt_13", ResearchBranch.Management, 13, "ResearchGlobalTalent", 100_000_000m, TimeSpan.FromHours(48),
                new ResearchEffect { ExtraWorkerSlots = 2 }, ["mgmt_12"]),
            Create("mgmt_14", ResearchBranch.Management, 14, "ResearchAiManagement", 300_000_000m, TimeSpan.FromHours(60),
                new ResearchEffect { WageReduction = 0.12m }, ["mgmt_13"]),
            Create("mgmt_15", ResearchBranch.Management, 15, "ResearchMasterManager", 1_000_000_000m, TimeSpan.FromHours(72),
                new ResearchEffect { ExtraWorkerSlots = 2, WageReduction = 0.15m }, ["mgmt_14"]),
        ];
    }

    private static List<Research> CreateMarketingBranch()
    {
        return
        [
            Create("mkt_01", ResearchBranch.Marketing, 1, "ResearchLocalAds", 500m, TimeSpan.FromMinutes(10),
                new ResearchEffect { RewardMultiplier = 0.05m }),
            Create("mkt_02", ResearchBranch.Marketing, 2, "ResearchOnlinePresence", 2_000m, TimeSpan.FromMinutes(30),
                new ResearchEffect { ExtraOrderSlots = 1 }, ["mkt_01"]),
            Create("mkt_03", ResearchBranch.Marketing, 3, "ResearchBranding", 8_000m, TimeSpan.FromHours(1),
                new ResearchEffect { RewardMultiplier = 0.05m }, ["mkt_02"]),
            Create("mkt_04", ResearchBranch.Marketing, 4, "ResearchReferralProgram", 25_000m, TimeSpan.FromHours(2),
                new ResearchEffect { RewardMultiplier = 0.08m }, ["mkt_03"]),
            Create("mkt_05", ResearchBranch.Marketing, 5, "ResearchPremiumBrand", 80_000m, TimeSpan.FromHours(4),
                new ResearchEffect { ExtraOrderSlots = 1 }, ["mkt_04"]),
            Create("mkt_06", ResearchBranch.Marketing, 6, "ResearchSocialMedia", 200_000m, TimeSpan.FromHours(6),
                new ResearchEffect { RewardMultiplier = 0.08m }, ["mkt_05"]),
            Create("mkt_07", ResearchBranch.Marketing, 7, "ResearchPublicRelations", 500_000m, TimeSpan.FromHours(8),
                new ResearchEffect { RewardMultiplier = 0.10m }, ["mkt_06"]),
            Create("mkt_08", ResearchBranch.Marketing, 8, "ResearchTvCampaign", 1_000_000m, TimeSpan.FromHours(12),
                new ResearchEffect { ExtraOrderSlots = 1 }, ["mkt_07"]),
            Create("mkt_09", ResearchBranch.Marketing, 9, "ResearchInternational", 3_000_000m, TimeSpan.FromHours(16),
                new ResearchEffect { RewardMultiplier = 0.10m }, ["mkt_08"]),
            Create("mkt_10", ResearchBranch.Marketing, 10, "ResearchLuxuryBrand", 8_000_000m, TimeSpan.FromHours(24),
                new ResearchEffect { RewardMultiplier = 0.12m }, ["mkt_09"]),
            Create("mkt_11", ResearchBranch.Marketing, 11, "ResearchFranchise", 20_000_000m, TimeSpan.FromHours(32),
                new ResearchEffect { ExtraOrderSlots = 2 }, ["mkt_10"]),
            Create("mkt_12", ResearchBranch.Marketing, 12, "ResearchGlobalBrand", 50_000_000m, TimeSpan.FromHours(40),
                new ResearchEffect { RewardMultiplier = 0.12m }, ["mkt_11"]),
            Create("mkt_13", ResearchBranch.Marketing, 13, "ResearchCelebEndorsement", 100_000_000m, TimeSpan.FromHours(48),
                new ResearchEffect { RewardMultiplier = 0.15m }, ["mkt_12"]),
            Create("mkt_14", ResearchBranch.Marketing, 14, "ResearchMonopoly", 300_000_000m, TimeSpan.FromHours(60),
                new ResearchEffect { ExtraOrderSlots = 2 }, ["mkt_13"]),
            Create("mkt_15", ResearchBranch.Marketing, 15, "ResearchMarketDomination", 1_000_000_000m, TimeSpan.FromHours(72),
                new ResearchEffect { RewardMultiplier = 0.20m, ExtraOrderSlots = 2 }, ["mkt_14"]),
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
