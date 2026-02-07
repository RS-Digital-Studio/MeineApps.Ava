using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// The complete game state, persisted between sessions.
/// Version 2: New worker system, buildings, research, events, prestige, reputation.
/// </summary>
public class GameState
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 2;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastSavedAt")]
    public DateTime LastSavedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastPlayedAt")]
    public DateTime LastPlayedAt { get; set; } = DateTime.UtcNow;

    // ═══════════════════════════════════════════════════════════════════════
    // PLAYER PROGRESS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("playerLevel")]
    public int PlayerLevel { get; set; } = 1;

    [JsonPropertyName("currentXp")]
    public int CurrentXp { get; set; }

    [JsonPropertyName("totalXp")]
    public int TotalXp { get; set; }

    [JsonPropertyName("money")]
    public decimal Money { get; set; } = 250m;

    [JsonPropertyName("totalMoneyEarned")]
    public decimal TotalMoneyEarned { get; set; }

    [JsonPropertyName("totalMoneySpent")]
    public decimal TotalMoneySpent { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // GOLDSCHRAUBEN (Premium-Waehrung)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("goldenScrews")]
    public int GoldenScrews { get; set; }

    [JsonPropertyName("totalGoldenScrewsEarned")]
    public int TotalGoldenScrewsEarned { get; set; }

    [JsonPropertyName("totalGoldenScrewsSpent")]
    public int TotalGoldenScrewsSpent { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // WORKSHOPS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("workshops")]
    public List<Workshop> Workshops { get; set; } = [];

    /// <summary>
    /// Workshop types that have been unlocked/purchased.
    /// </summary>
    [JsonPropertyName("unlockedWorkshopTypes")]
    public List<WorkshopType> UnlockedWorkshopTypes { get; set; } = [WorkshopType.Carpenter];

    // ═══════════════════════════════════════════════════════════════════════
    // WORKERS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("workerMarket")]
    public WorkerMarketPool? WorkerMarket { get; set; }

    [JsonPropertyName("totalWorkersHired")]
    public int TotalWorkersHired { get; set; }

    [JsonPropertyName("totalWorkersFired")]
    public int TotalWorkersFired { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // ORDERS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("availableOrders")]
    public List<Order> AvailableOrders { get; set; } = [];

    [JsonPropertyName("activeOrder")]
    public Order? ActiveOrder { get; set; }

    [JsonPropertyName("totalOrdersCompleted")]
    public int TotalOrdersCompleted { get; set; }

    /// <summary>
    /// Orders completed today (resets daily).
    /// </summary>
    [JsonPropertyName("ordersCompletedToday")]
    public int OrdersCompletedToday { get; set; }

    /// <summary>
    /// Orders completed this week (resets weekly).
    /// </summary>
    [JsonPropertyName("ordersCompletedThisWeek")]
    public int OrdersCompletedThisWeek { get; set; }

    [JsonPropertyName("lastOrderCooldownStart")]
    public DateTime LastOrderCooldownStart { get; set; } = DateTime.MinValue;

    [JsonPropertyName("weeklyOrderReset")]
    public DateTime WeeklyOrderReset { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Daily order threshold before cooldown kicks in.
    /// </summary>
    [JsonIgnore]
    public int OrderCooldownThreshold => 10;

    /// <summary>
    /// Weekly order limit.
    /// </summary>
    [JsonIgnore]
    public int WeeklyOrderLimit => 100;

    // ═══════════════════════════════════════════════════════════════════════
    // REPUTATION
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("reputation")]
    public CustomerReputation Reputation { get; set; } = new();

    // ═══════════════════════════════════════════════════════════════════════
    // BUILDINGS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("buildings")]
    public List<Building> Buildings { get; set; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // RESEARCH
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("researches")]
    public List<Research> Researches { get; set; } = [];

    [JsonPropertyName("activeResearchId")]
    public string? ActiveResearchId { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("activeEvent")]
    public GameEvent? ActiveEvent { get; set; }

    [JsonPropertyName("lastEventCheck")]
    public DateTime LastEventCheck { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("eventHistory")]
    public List<string> EventHistory { get; set; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // STATISTICS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("totalMiniGamesPlayed")]
    public int TotalMiniGamesPlayed { get; set; }

    [JsonPropertyName("perfectRatings")]
    public int PerfectRatings { get; set; }

    [JsonPropertyName("perfectStreak")]
    public int PerfectStreak { get; set; }

    [JsonPropertyName("bestPerfectStreak")]
    public int BestPerfectStreak { get; set; }

    [JsonPropertyName("totalPlayTimeSeconds")]
    public long TotalPlayTimeSeconds { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE (3-Tier System)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// New prestige data (3-tier system with shop).
    /// </summary>
    [JsonPropertyName("prestige")]
    public PrestigeData Prestige { get; set; } = new();

    // Legacy fields for v1 save compatibility
    [JsonPropertyName("prestigeLevel")]
    public int PrestigeLevel { get; set; }

    [JsonPropertyName("prestigeMultiplier")]
    public decimal PrestigeMultiplier { get; set; } = 1.0m;

    // ═══════════════════════════════════════════════════════════════════════
    // SETTINGS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("soundEnabled")]
    public bool SoundEnabled { get; set; } = true;

    [JsonPropertyName("musicEnabled")]
    public bool MusicEnabled { get; set; } = true;

    [JsonPropertyName("hapticsEnabled")]
    public bool HapticsEnabled { get; set; } = true;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    // ═══════════════════════════════════════════════════════════════════════
    // PREMIUM STATUS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("isPremium")]
    public bool IsPremium { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // DAILY REWARDS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("lastDailyRewardClaim")]
    public DateTime LastDailyRewardClaim { get; set; } = DateTime.MinValue;

    [JsonPropertyName("dailyRewardStreak")]
    public int DailyRewardStreak { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // BOOSTS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("speedBoostEndTime")]
    public DateTime SpeedBoostEndTime { get; set; } = DateTime.MinValue;

    [JsonPropertyName("xpBoostEndTime")]
    public DateTime XpBoostEndTime { get; set; } = DateTime.MinValue;

    [JsonIgnore]
    public bool IsSpeedBoostActive => SpeedBoostEndTime > DateTime.UtcNow;

    [JsonIgnore]
    public bool IsXpBoostActive => XpBoostEndTime > DateTime.UtcNow;

    // ═══════════════════════════════════════════════════════════════════════
    // ACHIEVEMENTS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("unlockedAchievements")]
    public List<string> UnlockedAchievements { get; set; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // QUICK JOBS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("quickJobs")]
    public List<QuickJob> QuickJobs { get; set; } = [];

    [JsonPropertyName("lastQuickJobRotation")]
    public DateTime LastQuickJobRotation { get; set; } = DateTime.MinValue;

    [JsonPropertyName("totalQuickJobsCompleted")]
    public int TotalQuickJobsCompleted { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // DAILY CHALLENGES
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("dailyChallengeState")]
    public DailyChallengeState DailyChallengeState { get; set; } = new();

    // ═══════════════════════════════════════════════════════════════════════
    // TOOLS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("tools")]
    public List<Tool> Tools { get; set; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // TUTORIAL
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("tutorialCompleted")]
    public bool TutorialCompleted { get; set; }

    [JsonPropertyName("tutorialStep")]
    public int TutorialStep { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // OFFLINE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Base offline hours (always 4).
    /// </summary>
    [JsonIgnore]
    public int BaseOfflineHours => 4;

    [JsonIgnore]
    public int MaxOfflineHours => IsPremium ? 12 : 4;

    /// <summary>
    /// Session flag: video extended offline duration.
    /// </summary>
    [JsonIgnore]
    public bool OfflineVideoExtended { get; set; }

    /// <summary>
    /// Session flag: video doubled offline earnings.
    /// </summary>
    [JsonIgnore]
    public bool OfflineVideoDoubled { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // CALCULATED PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [JsonIgnore]
    public int XpForNextLevel => CalculateXpForLevel(PlayerLevel + 1);

    [JsonIgnore]
    public double LevelProgress
    {
        get
        {
            int required = XpForNextLevel - CalculateXpForLevel(PlayerLevel);
            int current = CurrentXp - CalculateXpForLevel(PlayerLevel);
            return Math.Clamp((double)current / required, 0.0, 1.0);
        }
    }

    /// <summary>
    /// Total gross income per second from all workshops.
    /// </summary>
    [JsonIgnore]
    public decimal TotalIncomePerSecond
    {
        get
        {
            decimal total = Workshops.Sum(w => w.GrossIncomePerSecond);
            return total * Prestige.PermanentMultiplier;
        }
    }

    /// <summary>
    /// Total running costs per second from all workshops.
    /// </summary>
    [JsonIgnore]
    public decimal TotalCostsPerSecond => Workshops.Sum(w => w.TotalCostsPerHour) / 3600m;

    /// <summary>
    /// Net income per second (gross - costs).
    /// Can be negative if costs exceed income!
    /// </summary>
    [JsonIgnore]
    public decimal NetIncomePerSecond => TotalIncomePerSecond - TotalCostsPerSecond;

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    public static int CalculateXpForLevel(int level)
    {
        if (level <= 1) return 0;
        return (int)(100 * Math.Pow(level - 1, 1.2));
    }

    public int AddXp(int amount)
    {
        CurrentXp += amount;
        TotalXp += amount;

        int levelUps = 0;
        while (CurrentXp >= XpForNextLevel)
        {
            PlayerLevel++;
            levelUps++;
        }

        return levelUps;
    }

    public Workshop GetOrCreateWorkshop(WorkshopType type)
    {
        var workshop = Workshops.FirstOrDefault(w => w.Type == type);
        if (workshop == null)
        {
            workshop = Workshop.Create(type);
            Workshops.Add(workshop);
        }
        return workshop;
    }

    public bool IsWorkshopUnlocked(WorkshopType type)
    {
        // Must meet level requirement
        if (PlayerLevel < type.GetUnlockLevel()) return false;
        // Must meet prestige requirement
        if (type.GetRequiredPrestige() > Prestige.TotalPrestigeCount) return false;
        // Must be in unlocked list (purchased)
        return UnlockedWorkshopTypes.Contains(type);
    }

    /// <summary>
    /// Gets a building by type, returns null if not built.
    /// </summary>
    public Building? GetBuilding(BuildingType type)
    {
        return Buildings.FirstOrDefault(b => b.Type == type && b.IsBuilt);
    }

    /// <summary>
    /// Creates a new game state with default values.
    /// </summary>
    public static GameState CreateNew()
    {
        var state = new GameState();

        // Create the starting workshop (Carpenter) with 1 worker
        var carpenter = Workshop.Create(WorkshopType.Carpenter);
        carpenter.IsUnlocked = true;
        carpenter.Workers.Add(Worker.CreateRandom());
        state.Workshops.Add(carpenter);

        // Initialize research tree
        state.Researches = ResearchTree.CreateAll();

        // Initialize tools
        state.Tools = Tool.CreateDefaults();

        return state;
    }

    /// <summary>
    /// Migrates a v1 save to v2 format.
    /// </summary>
    public static GameState MigrateFromV1(GameState old)
    {
        if (old.Version >= 2) return old;

        old.Version = 2;

        // Migrate workers: old workers had flat 1.0 efficiency
        foreach (var ws in old.Workshops)
        {
            ws.IsUnlocked = true;
            foreach (var worker in ws.Workers)
            {
                worker.Tier = WorkerTier.E;
                worker.Talent = 3;
                worker.Personality = WorkerPersonality.Steady;
                worker.Mood = 80m;
                worker.Fatigue = 0m;
                worker.ExperienceLevel = Math.Min(10, worker.SkillLevel);
                worker.WagePerHour = WorkerTier.E.GetWagePerHour();
                worker.AssignedWorkshop = ws.Type;
            }

            if (!old.UnlockedWorkshopTypes.Contains(ws.Type))
                old.UnlockedWorkshopTypes.Add(ws.Type);
        }

        // Migrate prestige
        old.Prestige = new PrestigeData
        {
            BronzeCount = old.PrestigeLevel,
            PermanentMultiplier = old.PrestigeMultiplier,
            CurrentTier = old.PrestigeLevel > 0 ? Enums.PrestigeTier.Bronze : Enums.PrestigeTier.None
        };

        // Initialize reputation
        old.Reputation ??= new CustomerReputation();

        // Initialize empty collections
        old.Buildings ??= [];
        old.EventHistory ??= [];

        // Initialize research tree
        if (old.Researches == null || old.Researches.Count == 0)
            old.Researches = ResearchTree.CreateAll();

        return old;
    }
}
