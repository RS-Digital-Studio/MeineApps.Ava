using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Represents an achievement in the game.
/// </summary>
public class Achievement
{
    /// <summary>
    /// Unique identifier for the achievement.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    /// Localization key for the title.
    /// </summary>
    [JsonPropertyName("titleKey")]
    public string TitleKey { get; set; } = "";

    /// <summary>
    /// Fallback title in English.
    /// </summary>
    [JsonPropertyName("titleFallback")]
    public string TitleFallback { get; set; } = "";

    /// <summary>
    /// Localization key for the description.
    /// </summary>
    [JsonPropertyName("descriptionKey")]
    public string DescriptionKey { get; set; } = "";

    /// <summary>
    /// Fallback description in English.
    /// </summary>
    [JsonPropertyName("descriptionFallback")]
    public string DescriptionFallback { get; set; } = "";

    /// <summary>
    /// Category of the achievement.
    /// </summary>
    [JsonPropertyName("category")]
    public AchievementCategory Category { get; set; }

    /// <summary>
    /// Icon/emoji for the achievement.
    /// </summary>
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "\ud83c\udfc6";

    /// <summary>
    /// Target value to unlock the achievement.
    /// </summary>
    [JsonPropertyName("targetValue")]
    public int TargetValue { get; set; } = 1;

    /// <summary>
    /// Current progress value.
    /// </summary>
    [JsonPropertyName("currentValue")]
    public int CurrentValue { get; set; }

    /// <summary>
    /// Money reward for unlocking.
    /// </summary>
    [JsonPropertyName("moneyReward")]
    public decimal MoneyReward { get; set; }

    /// <summary>
    /// XP reward for unlocking.
    /// </summary>
    [JsonPropertyName("xpReward")]
    public int XpReward { get; set; }

    /// <summary>
    /// Whether this achievement has been unlocked.
    /// </summary>
    [JsonPropertyName("isUnlocked")]
    public bool IsUnlocked { get; set; }

    /// <summary>
    /// When the achievement was unlocked.
    /// </summary>
    [JsonPropertyName("unlockedAt")]
    public DateTime? UnlockedAt { get; set; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    [JsonIgnore]
    public double Progress => TargetValue > 0 ? Math.Min(100.0, (double)CurrentValue / TargetValue * 100) : 0;

    /// <summary>
    /// Progress as a fraction (0.0-1.0).
    /// </summary>
    [JsonIgnore]
    public double ProgressFraction => Progress / 100.0;

    /// <summary>
    /// Whether the achievement is close to being unlocked (>75%).
    /// </summary>
    [JsonIgnore]
    public bool IsCloseToUnlock => !IsUnlocked && Progress >= 75;
}

/// <summary>
/// Predefined achievements for the game.
/// </summary>
public static class Achievements
{
    public static List<Achievement> GetAll()
    {
        return
        [
            // Orders Category
            new() { Id = "first_order", TitleKey = "AchFirstOrder", TitleFallback = "First Steps", DescriptionKey = "AchFirstOrderDesc", DescriptionFallback = "Complete your first order", Category = AchievementCategory.Orders, Icon = "\ud83d\udce6", TargetValue = 1, MoneyReward = 100, XpReward = 25 },
            new() { Id = "orders_10", TitleKey = "AchOrders10", TitleFallback = "Getting Started", DescriptionKey = "AchOrders10Desc", DescriptionFallback = "Complete 10 orders", Category = AchievementCategory.Orders, Icon = "\ud83d\udce6", TargetValue = 10, MoneyReward = 500, XpReward = 100 },
            new() { Id = "orders_50", TitleKey = "AchOrders50", TitleFallback = "Reliable Worker", DescriptionKey = "AchOrders50Desc", DescriptionFallback = "Complete 50 orders", Category = AchievementCategory.Orders, Icon = "\ud83d\udce6", TargetValue = 50, MoneyReward = 2500, XpReward = 300 },
            new() { Id = "orders_100", TitleKey = "AchOrders100", TitleFallback = "Master Craftsman", DescriptionKey = "AchOrders100Desc", DescriptionFallback = "Complete 100 orders", Category = AchievementCategory.Orders, Icon = "\ud83c\udfc6", TargetValue = 100, MoneyReward = 5000, XpReward = 500 },
            new() { Id = "orders_500", TitleKey = "AchOrders500", TitleFallback = "Industry Legend", DescriptionKey = "AchOrders500Desc", DescriptionFallback = "Complete 500 orders", Category = AchievementCategory.Orders, Icon = "\ud83d\udc51", TargetValue = 500, MoneyReward = 25000, XpReward = 1000 },

            // Mini-Games Category
            new() { Id = "perfect_first", TitleKey = "AchPerfectFirst", TitleFallback = "Perfection!", DescriptionKey = "AchPerfectFirstDesc", DescriptionFallback = "Get your first Perfect rating", Category = AchievementCategory.MiniGames, Icon = "\u2b50", TargetValue = 1, MoneyReward = 200, XpReward = 50 },
            new() { Id = "perfect_10", TitleKey = "AchPerfect10", TitleFallback = "Skilled Hands", DescriptionKey = "AchPerfect10Desc", DescriptionFallback = "Get 10 Perfect ratings", Category = AchievementCategory.MiniGames, Icon = "\u2b50", TargetValue = 10, MoneyReward = 1000, XpReward = 150 },
            new() { Id = "perfect_50", TitleKey = "AchPerfect50", TitleFallback = "Precision Master", DescriptionKey = "AchPerfect50Desc", DescriptionFallback = "Get 50 Perfect ratings", Category = AchievementCategory.MiniGames, Icon = "\u2b50", TargetValue = 50, MoneyReward = 5000, XpReward = 400 },
            new() { Id = "streak_5", TitleKey = "AchStreak5", TitleFallback = "On Fire!", DescriptionKey = "AchStreak5Desc", DescriptionFallback = "Get 5 Perfect ratings in a row", Category = AchievementCategory.MiniGames, Icon = "\ud83d\udd25", TargetValue = 5, MoneyReward = 500, XpReward = 100 },
            new() { Id = "streak_10", TitleKey = "AchStreak10", TitleFallback = "Unstoppable", DescriptionKey = "AchStreak10Desc", DescriptionFallback = "Get 10 Perfect ratings in a row", Category = AchievementCategory.MiniGames, Icon = "\ud83d\udd25", TargetValue = 10, MoneyReward = 2000, XpReward = 300 },
            new() { Id = "games_100", TitleKey = "AchGames100", TitleFallback = "Mini-Game Veteran", DescriptionKey = "AchGames100Desc", DescriptionFallback = "Play 100 mini-games", Category = AchievementCategory.MiniGames, Icon = "\ud83c\udfae", TargetValue = 100, MoneyReward = 2500, XpReward = 250 },

            // Workshops Category
            new() { Id = "workshop_level10", TitleKey = "AchWorkshop10", TitleFallback = "Upgraded", DescriptionKey = "AchWorkshop10Desc", DescriptionFallback = "Upgrade any workshop to level 10", Category = AchievementCategory.Workshops, Icon = "\ud83d\udd27", TargetValue = 10, MoneyReward = 1000, XpReward = 200 },
            new() { Id = "workshop_level25", TitleKey = "AchWorkshop25", TitleFallback = "Expert Facility", DescriptionKey = "AchWorkshop25Desc", DescriptionFallback = "Upgrade any workshop to level 25", Category = AchievementCategory.Workshops, Icon = "\ud83d\udd27", TargetValue = 25, MoneyReward = 5000, XpReward = 500 },
            new() { Id = "all_workshops", TitleKey = "AchAllWorkshops", TitleFallback = "Full House", DescriptionKey = "AchAllWorkshopsDesc", DescriptionFallback = "Unlock all 6 workshops", Category = AchievementCategory.Workshops, Icon = "\ud83c\udfed", TargetValue = 6, MoneyReward = 2500, XpReward = 400 },
            new() { Id = "worker_first", TitleKey = "AchWorkerFirst", TitleFallback = "Team Builder", DescriptionKey = "AchWorkerFirstDesc", DescriptionFallback = "Hire your first worker", Category = AchievementCategory.Workshops, Icon = "\ud83d\udc77", TargetValue = 1, MoneyReward = 100, XpReward = 25 },
            new() { Id = "workers_10", TitleKey = "AchWorkers10", TitleFallback = "Growing Team", DescriptionKey = "AchWorkers10Desc", DescriptionFallback = "Hire 10 workers total", Category = AchievementCategory.Workshops, Icon = "\ud83d\udc77", TargetValue = 10, MoneyReward = 1000, XpReward = 200 },
            new() { Id = "workers_25", TitleKey = "AchWorkers25", TitleFallback = "Big Business", DescriptionKey = "AchWorkers25Desc", DescriptionFallback = "Hire 25 workers total", Category = AchievementCategory.Workshops, Icon = "\ud83d\udc77", TargetValue = 25, MoneyReward = 5000, XpReward = 500 },
            new() { Id = "workshop_level50", TitleKey = "AchWorkshop50", TitleFallback = "Maximum Power", DescriptionKey = "AchWorkshop50Desc", DescriptionFallback = "Upgrade any workshop to level 50", Category = AchievementCategory.Workshops, Icon = "\ud83d\udd27", TargetValue = 50, MoneyReward = 50000, XpReward = 1000 },
            new() { Id = "all_workshops_8", TitleKey = "AchAllWorkshops8", TitleFallback = "Complete Empire", DescriptionKey = "AchAllWorkshops8Desc", DescriptionFallback = "Unlock all 8 workshops (including prestige)", Category = AchievementCategory.Workshops, Icon = "\ud83c\udfed", TargetValue = 8, MoneyReward = 100000, XpReward = 2000 },
            new() { Id = "events_survived_10", TitleKey = "AchEventsSurvived10", TitleFallback = "Weathered", DescriptionKey = "AchEventsSurvived10Desc", DescriptionFallback = "Survive 10 random events", Category = AchievementCategory.Workshops, Icon = "\u26c8", TargetValue = 10, MoneyReward = 5000, XpReward = 300 },

            // Money Category
            new() { Id = "money_1k", TitleKey = "AchMoney1k", TitleFallback = "First Thousand", DescriptionKey = "AchMoney1kDesc", DescriptionFallback = "Earn 1,000\u20ac total", Category = AchievementCategory.Money, Icon = "\ud83d\udcb0", TargetValue = 1000, MoneyReward = 100, XpReward = 25 },
            new() { Id = "money_10k", TitleKey = "AchMoney10k", TitleFallback = "Making Money", DescriptionKey = "AchMoney10kDesc", DescriptionFallback = "Earn 10,000\u20ac total", Category = AchievementCategory.Money, Icon = "\ud83d\udcb0", TargetValue = 10000, MoneyReward = 500, XpReward = 100 },
            new() { Id = "money_100k", TitleKey = "AchMoney100k", TitleFallback = "Wealthy", DescriptionKey = "AchMoney100kDesc", DescriptionFallback = "Earn 100,000\u20ac total", Category = AchievementCategory.Money, Icon = "\ud83d\udcb0", TargetValue = 100000, MoneyReward = 2500, XpReward = 300 },
            new() { Id = "money_1m", TitleKey = "AchMoney1m", TitleFallback = "Millionaire", DescriptionKey = "AchMoney1mDesc", DescriptionFallback = "Earn 1,000,000\u20ac total", Category = AchievementCategory.Money, Icon = "\ud83e\udd11", TargetValue = 1000000, MoneyReward = 10000, XpReward = 500 },
            new() { Id = "money_10m", TitleKey = "AchMoney10m", TitleFallback = "Multi-Millionaire", DescriptionKey = "AchMoney10mDesc", DescriptionFallback = "Earn 10,000,000\u20ac total", Category = AchievementCategory.Money, Icon = "\ud83d\udcb0", TargetValue = 10000000, MoneyReward = 25000, XpReward = 500 },
            new() { Id = "money_100m", TitleKey = "AchMoney100m", TitleFallback = "Mega Rich", DescriptionKey = "AchMoney100mDesc", DescriptionFallback = "Earn 100,000,000\u20ac total", Category = AchievementCategory.Money, Icon = "\ud83d\udcb0", TargetValue = 100000000, MoneyReward = 50000, XpReward = 800 },
            new() { Id = "money_1b", TitleKey = "AchMoney1b", TitleFallback = "Billionaire", DescriptionKey = "AchMoney1bDesc", DescriptionFallback = "Earn 1,000,000,000\u20ac total", Category = AchievementCategory.Money, Icon = "\ud83e\udd11", TargetValue = 1000000000, MoneyReward = 100000, XpReward = 1500 },

            // Time Category
            new() { Id = "play_1h", TitleKey = "AchPlay1h", TitleFallback = "Dedicated", DescriptionKey = "AchPlay1hDesc", DescriptionFallback = "Play for 1 hour total", Category = AchievementCategory.Time, Icon = "\u23f0", TargetValue = 3600, MoneyReward = 250, XpReward = 50 },
            new() { Id = "play_10h", TitleKey = "AchPlay10h", TitleFallback = "Committed", DescriptionKey = "AchPlay10hDesc", DescriptionFallback = "Play for 10 hours total", Category = AchievementCategory.Time, Icon = "\u23f0", TargetValue = 36000, MoneyReward = 2500, XpReward = 250 },
            new() { Id = "daily_7", TitleKey = "AchDaily7", TitleFallback = "Week Warrior", DescriptionKey = "AchDaily7Desc", DescriptionFallback = "Log in 7 days in a row", Category = AchievementCategory.Time, Icon = "\ud83d\udcc5", TargetValue = 7, MoneyReward = 1000, XpReward = 150 },

            // Special Category
            new() { Id = "level_10", TitleKey = "AchLevel10", TitleFallback = "Rising Star", DescriptionKey = "AchLevel10Desc", DescriptionFallback = "Reach level 10", Category = AchievementCategory.Special, Icon = "\ud83c\udf1f", TargetValue = 10, MoneyReward = 1000, XpReward = 200 },
            new() { Id = "level_25", TitleKey = "AchLevel25", TitleFallback = "Experienced", DescriptionKey = "AchLevel25Desc", DescriptionFallback = "Reach level 25", Category = AchievementCategory.Special, Icon = "\ud83c\udf1f", TargetValue = 25, MoneyReward = 5000, XpReward = 500 },
            new() { Id = "level_50", TitleKey = "AchLevel50", TitleFallback = "Veteran", DescriptionKey = "AchLevel50Desc", DescriptionFallback = "Reach level 50", Category = AchievementCategory.Special, Icon = "\ud83d\udc51", TargetValue = 50, MoneyReward = 10000, XpReward = 1000 },
            new() { Id = "prestige_1", TitleKey = "AchPrestige1", TitleFallback = "New Beginning", DescriptionKey = "AchPrestige1Desc", DescriptionFallback = "Prestige for the first time", Category = AchievementCategory.Prestige, Icon = "\u2728", TargetValue = 1, MoneyReward = 5000, XpReward = 500 },

            // Workers Category
            new() { Id = "worker_a_tier", TitleKey = "AchWorkerATier", TitleFallback = "Elite Recruitment", DescriptionKey = "AchWorkerATierDesc", DescriptionFallback = "Hire your first A-Tier worker", Category = AchievementCategory.Workers, Icon = "\u2b50", TargetValue = 1, MoneyReward = 10000, XpReward = 400 },
            new() { Id = "workers_max_level", TitleKey = "AchWorkersMaxLevel", TitleFallback = "Master Workers", DescriptionKey = "AchWorkersMaxLevelDesc", DescriptionFallback = "Train 10 workers to level 10", Category = AchievementCategory.Workers, Icon = "\ud83c\udf93", TargetValue = 10, MoneyReward = 25000, XpReward = 800 },
            new() { Id = "worker_loyal", TitleKey = "AchWorkerLoyal", TitleFallback = "Loyal Employee", DescriptionKey = "AchWorkerLoyalDesc", DescriptionFallback = "Keep a worker for 100 real-time days", Category = AchievementCategory.Workers, Icon = "\ud83e\udd1d", TargetValue = 100, MoneyReward = 15000, XpReward = 600 },
            new() { Id = "worker_specialist", TitleKey = "AchWorkerSpecialist", TitleFallback = "Perfect Match", DescriptionKey = "AchWorkerSpecialistDesc", DescriptionFallback = "Assign a worker to their specialization workshop", Category = AchievementCategory.Workers, Icon = "\ud83c\udfaf", TargetValue = 1, MoneyReward = 500, XpReward = 50 },
            new() { Id = "workers_total_50", TitleKey = "AchWorkersTotal50", TitleFallback = "HR Manager", DescriptionKey = "AchWorkersTotal50Desc", DescriptionFallback = "Hire 50 workers total (lifetime)", Category = AchievementCategory.Workers, Icon = "\ud83d\udc65", TargetValue = 50, MoneyReward = 20000, XpReward = 700 },
            new() { Id = "worker_s_tier", TitleKey = "AchWorkerSTier", TitleFallback = "Legend Found", DescriptionKey = "AchWorkerSTierDesc", DescriptionFallback = "Hire an S-Tier worker", Category = AchievementCategory.Workers, Icon = "\ud83d\udc8e", TargetValue = 1, MoneyReward = 50000, XpReward = 1000 },

            // Buildings Category
            new() { Id = "building_first", TitleKey = "AchBuildingFirst", TitleFallback = "Developer", DescriptionKey = "AchBuildingFirstDesc", DescriptionFallback = "Build your first building", Category = AchievementCategory.Buildings, Icon = "\ud83c\udfd7", TargetValue = 1, MoneyReward = 2000, XpReward = 100 },
            new() { Id = "building_all", TitleKey = "AchBuildingAll", TitleFallback = "Real Estate Mogul", DescriptionKey = "AchBuildingAllDesc", DescriptionFallback = "Build all 7 buildings", Category = AchievementCategory.Buildings, Icon = "\ud83c\udfd8", TargetValue = 7, MoneyReward = 50000, XpReward = 1000 },
            new() { Id = "building_max", TitleKey = "AchBuildingMax", TitleFallback = "Fully Upgraded", DescriptionKey = "AchBuildingMaxDesc", DescriptionFallback = "Upgrade any building to level 5", Category = AchievementCategory.Buildings, Icon = "\ud83c\udfe2", TargetValue = 5, MoneyReward = 20000, XpReward = 500 },
            new() { Id = "canteen_built", TitleKey = "AchCanteenBuilt", TitleFallback = "Happy Workers", DescriptionKey = "AchCanteenBuiltDesc", DescriptionFallback = "Build the Canteen", Category = AchievementCategory.Buildings, Icon = "\ud83c\udf7d", TargetValue = 1, MoneyReward = 5000, XpReward = 200 },
            new() { Id = "training_center", TitleKey = "AchTrainingCenter", TitleFallback = "Academy", DescriptionKey = "AchTrainingCenterDesc", DescriptionFallback = "Build the Training Center", Category = AchievementCategory.Buildings, Icon = "\ud83d\udcda", TargetValue = 1, MoneyReward = 5000, XpReward = 200 },

            // Research Category
            new() { Id = "research_first", TitleKey = "AchResearchFirst", TitleFallback = "Scientist", DescriptionKey = "AchResearchFirstDesc", DescriptionFallback = "Complete your first research", Category = AchievementCategory.Research, Icon = "\ud83d\udd2c", TargetValue = 1, MoneyReward = 2000, XpReward = 100 },
            new() { Id = "research_branch", TitleKey = "AchResearchBranch", TitleFallback = "Expert", DescriptionKey = "AchResearchBranchDesc", DescriptionFallback = "Complete an entire research branch (15/15)", Category = AchievementCategory.Research, Icon = "\ud83e\uddec", TargetValue = 15, MoneyReward = 100000, XpReward = 2000 },
            new() { Id = "research_all", TitleKey = "AchResearchAll", TitleFallback = "Genius", DescriptionKey = "AchResearchAllDesc", DescriptionFallback = "Complete all 45 researches", Category = AchievementCategory.Research, Icon = "\ud83c\udf93", TargetValue = 45, MoneyReward = 500000, XpReward = 5000 },
            new() { Id = "research_tools5", TitleKey = "AchResearchTools5", TitleFallback = "Tool Master", DescriptionKey = "AchResearchTools5Desc", DescriptionFallback = "Complete 5 Tools researches", Category = AchievementCategory.Research, Icon = "\ud83d\udd27", TargetValue = 5, MoneyReward = 10000, XpReward = 300 },
            new() { Id = "research_mgmt5", TitleKey = "AchResearchMgmt5", TitleFallback = "Manager", DescriptionKey = "AchResearchMgmt5Desc", DescriptionFallback = "Complete 5 Management researches", Category = AchievementCategory.Research, Icon = "\ud83d\udccb", TargetValue = 5, MoneyReward = 10000, XpReward = 300 },

            // Reputation Category
            new() { Id = "reputation_70", TitleKey = "AchReputation70", TitleFallback = "Well Known", DescriptionKey = "AchReputation70Desc", DescriptionFallback = "Reach 70 reputation", Category = AchievementCategory.Reputation, Icon = "\u2b50", TargetValue = 70, MoneyReward = 10000, XpReward = 300 },
            new() { Id = "reputation_90", TitleKey = "AchReputation90", TitleFallback = "Famous", DescriptionKey = "AchReputation90Desc", DescriptionFallback = "Reach 90 reputation", Category = AchievementCategory.Reputation, Icon = "\ud83c\udf1f", TargetValue = 90, MoneyReward = 25000, XpReward = 600 },
            new() { Id = "reputation_100", TitleKey = "AchReputation100", TitleFallback = "Legendary", DescriptionKey = "AchReputation100Desc", DescriptionFallback = "Reach 100 reputation", Category = AchievementCategory.Reputation, Icon = "\ud83d\udc51", TargetValue = 100, MoneyReward = 100000, XpReward = 1500 },
            new() { Id = "regular_10", TitleKey = "AchRegular10", TitleFallback = "Popular Choice", DescriptionKey = "AchRegular10Desc", DescriptionFallback = "Have 10 regular customers", Category = AchievementCategory.Reputation, Icon = "\ud83e\udd1d", TargetValue = 10, MoneyReward = 15000, XpReward = 500 },

            // Prestige Category
            new() { Id = "prestige_bronze", TitleKey = "AchPrestigeBronze", TitleFallback = "New Beginning", DescriptionKey = "AchPrestigeBronzeDesc", DescriptionFallback = "Complete your first Bronze prestige", Category = AchievementCategory.Prestige, Icon = "\ud83e\udd49", TargetValue = 1, MoneyReward = 10000, XpReward = 500 },
            new() { Id = "prestige_silver", TitleKey = "AchPrestigeSilver", TitleFallback = "Experienced Master", DescriptionKey = "AchPrestigeSilverDesc", DescriptionFallback = "Complete your first Silver prestige", Category = AchievementCategory.Prestige, Icon = "\ud83e\udd48", TargetValue = 1, MoneyReward = 50000, XpReward = 1500 },
            new() { Id = "prestige_gold", TitleKey = "AchPrestigeGold", TitleFallback = "Golden Legend", DescriptionKey = "AchPrestigeGoldDesc", DescriptionFallback = "Complete your first Gold prestige", Category = AchievementCategory.Prestige, Icon = "\ud83e\udd47", TargetValue = 1, MoneyReward = 200000, XpReward = 3000 },
            new() { Id = "prestige_points_100", TitleKey = "AchPrestigePoints100", TitleFallback = "Point Collector", DescriptionKey = "AchPrestigePoints100Desc", DescriptionFallback = "Spend 100 prestige points in the shop", Category = AchievementCategory.Prestige, Icon = "\ud83d\udcab", TargetValue = 100, MoneyReward = 50000, XpReward = 1000 }
        ];
    }
}
