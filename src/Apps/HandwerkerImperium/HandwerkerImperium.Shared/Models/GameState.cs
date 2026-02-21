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

    /// <summary>
    /// Temporär gesetzter QuickJob während MiniGame läuft (nicht persistiert).
    /// Wird von MainViewModel gesetzt/geleert, von MiniGame-VMs für Belohnungs-Anzeige gelesen.
    /// </summary>
    [JsonIgnore]
    public QuickJob? ActiveQuickJob { get; set; }

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

    /// <summary>
    /// Letzter Zeitpunkt des täglichen Reputation-Decay (persistiert, damit App-Neustart nicht resettet).
    /// </summary>
    [JsonPropertyName("lastReputationDecay")]
    public DateTime LastReputationDecay { get; set; } = DateTime.UtcNow;

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

    [JsonPropertyName("notificationsEnabled")]
    public bool NotificationsEnabled { get; set; } = true;

    [JsonPropertyName("cloudSaveEnabled")]
    public bool CloudSaveEnabled { get; set; } = true;

    [JsonPropertyName("lastCloudSaveTime")]
    public DateTime LastCloudSaveTime { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

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

    /// <summary>
    /// Feierabend-Rush: 2h 2x-Boost, einmal täglich gratis.
    /// </summary>
    [JsonPropertyName("rushBoostEndTime")]
    public DateTime RushBoostEndTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Letztes Datum an dem der gratis Rush verwendet wurde.
    /// </summary>
    [JsonPropertyName("lastFreeRushUsed")]
    public DateTime LastFreeRushUsed { get; set; } = DateTime.MinValue;

    [JsonIgnore]
    public bool IsSpeedBoostActive => SpeedBoostEndTime > DateTime.UtcNow;

    [JsonIgnore]
    public bool IsXpBoostActive => XpBoostEndTime > DateTime.UtcNow;

    [JsonIgnore]
    public bool IsRushBoostActive => RushBoostEndTime > DateTime.UtcNow;

    /// <summary>
    /// Ob der tägliche Gratis-Rush verfügbar ist (noch nicht heute verwendet).
    /// Zeitmanipulations-sicher: Wenn LastFreeRushUsed in der Zukunft liegt, ist future &lt; today false → blockiert.
    /// </summary>
    [JsonIgnore]
    public bool IsFreeRushAvailable => LastFreeRushUsed.Date < DateTime.UtcNow.Date;

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

    [JsonPropertyName("quickJobsCompletedToday")]
    public int QuickJobsCompletedToday { get; set; }

    [JsonPropertyName("lastQuickJobDailyReset")]
    public DateTime LastQuickJobDailyReset { get; set; } = DateTime.MinValue;

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
    // MEISTERWERKZEUGE (Sammelbare Artefakte)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// IDs der gesammelten Meisterwerkzeuge.
    /// </summary>
    [JsonPropertyName("collectedMasterTools")]
    public List<string> CollectedMasterTools { get; set; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // LIEFERANT (Variable Rewards)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Nächster Zeitpunkt für eine Lieferung.
    /// </summary>
    [JsonPropertyName("nextDeliveryTime")]
    public DateTime NextDeliveryTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Aktuell wartende Lieferung (null = keine).
    /// </summary>
    [JsonPropertyName("pendingDelivery")]
    public SupplierDelivery? PendingDelivery { get; set; }

    /// <summary>
    /// Gesamtanzahl abgeholter Lieferungen.
    /// </summary>
    [JsonPropertyName("totalDeliveriesClaimed")]
    public int TotalDeliveriesClaimed { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // TUTORIAL
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("tutorialCompleted")]
    public bool TutorialCompleted { get; set; }

    [JsonPropertyName("tutorialStep")]
    public int TutorialStep { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // STORY-SYSTEM
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// IDs der bereits freigeschalteten und gesehenen Story-Kapitel.
    /// </summary>
    [JsonPropertyName("viewedStoryIds")]
    public List<string> ViewedStoryIds { get; set; } = [];

    /// <summary>
    /// ID des nächsten ungesehenen Story-Kapitels (für Badge-Anzeige).
    /// </summary>
    [JsonPropertyName("pendingStoryId")]
    public string? PendingStoryId { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // AUTOMATION (Welle 1)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("automation")]
    public AutomationSettings Automation { get; set; } = new();

    // ═══════════════════════════════════════════════════════════════════════
    // WEEKLY MISSIONS (Welle 1)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("weeklyMissionState")]
    public WeeklyMissionState WeeklyMissionState { get; set; } = new();

    // ═══════════════════════════════════════════════════════════════════════
    // WELCOME BACK (Welle 1)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("activeWelcomeBackOffer")]
    public WelcomeBackOffer? ActiveWelcomeBackOffer { get; set; }

    [JsonPropertyName("claimedStarterPack")]
    public bool ClaimedStarterPack { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // STREAK-ERWEITERUNG (Welle 1)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Streak-Wert vor dem letzten Unterbruch (für Streak-Rettung).
    /// </summary>
    [JsonPropertyName("streakBeforeBreak")]
    public int StreakBeforeBreak { get; set; }

    /// <summary>
    /// Ob die Streak-Rettung bereits verwendet wurde (nur 1x pro Unterbrechung).
    /// </summary>
    [JsonPropertyName("streakRescueUsed")]
    public bool StreakRescueUsed { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // LUCKY SPIN (Welle 2)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("luckySpin")]
    public LuckySpinState LuckySpin { get; set; } = new();

    // ═══════════════════════════════════════════════════════════════════════
    // EQUIPMENT (Welle 2)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("equipmentInventory")]
    public List<Equipment> EquipmentInventory { get; set; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // TOURNAMENTS (Welle 2)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("currentTournament")]
    public Tournament? CurrentTournament { get; set; }

    [JsonPropertyName("totalTournamentsPlayed")]
    public int TotalTournamentsPlayed { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // MANAGERS (Welle 3)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("managers")]
    public List<Manager> Managers { get; set; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // SEASONAL EVENTS (Welle 4)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("currentSeasonalEvent")]
    public SeasonalEvent? CurrentSeasonalEvent { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // BATTLE PASS (Welle 4)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("battlePass")]
    public BattlePass BattlePass { get; set; } = new();

    // ═══════════════════════════════════════════════════════════════════════
    // CONTEXTUAL OFFERS (Welle 5)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Level-Meilensteine bei denen bereits ein Angebot gezeigt wurde.
    /// </summary>
    [JsonPropertyName("claimedLevelOffers")]
    public List<int> ClaimedLevelOffers { get; set; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // GUILD (Welle 6)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("guild")]
    public Guild? Guild { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // CRAFTING (Welle 7)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("craftingInventory")]
    public Dictionary<string, int> CraftingInventory { get; set; } = new();

    [JsonPropertyName("activeCraftingJobs")]
    public List<CraftingJob> ActiveCraftingJobs { get; set; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // VIP (Welle 7)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("totalPurchaseAmount")]
    public decimal TotalPurchaseAmount { get; set; }

    [JsonPropertyName("vipLevel")]
    public VipTier VipLevel { get; set; } = VipTier.None;

    // ═══════════════════════════════════════════════════════════════════════
    // STARTER PACK (Welle 8)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("hasPurchasedStarterPack")]
    public bool HasPurchasedStarterPack { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // BOSS ORDERS (Welle 8)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("lastBossOrderDate")]
    public DateTime LastBossOrderDate { get; set; } = DateTime.MinValue;

    [JsonPropertyName("bossOrdersCompleted")]
    public int BossOrdersCompleted { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // FRIENDS (Welle 8)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("friends")]
    public List<Friend> Friends { get; set; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // DAILY SHOP OFFER (Welle 8)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("dailyShopOffer")]
    public ShopOffer? DailyShopOffer { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // UI/UX STATE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bisheriger Rekord fuer Offline-Einnahmen (fuer "Neuer Rekord!" Anzeige).
    /// </summary>
    [JsonPropertyName("maxOfflineEarnings")]
    public decimal MaxOfflineEarnings { get; set; }

    /// <summary>
    /// Ob der Tutorial-Hint (pulsierende Umrandung bei erstem Upgrade) bereits gesehen wurde.
    /// </summary>
    [JsonPropertyName("hasSeenTutorialHint")]
    public bool HasSeenTutorialHint { get; set; }

    /// <summary>
    /// MiniGame-Typen, für die das Tutorial bereits angezeigt wurde.
    /// </summary>
    [JsonPropertyName("seenMiniGameTutorials")]
    public List<MiniGameType> SeenMiniGameTutorials { get; set; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // OFFLINE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Base offline hours (always 4).
    /// </summary>
    [JsonIgnore]
    public int BaseOfflineHours => 4;

    [JsonIgnore]
    public int MaxOfflineHours => IsPremium ? 16 : OfflineVideoExtended ? 8 : 4;

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
    /// Brutto-Einkommen pro Sekunde aus allen Workshops (mit Prestige-Multiplikator, gekappt bei 20x).
    /// Shop-Income-Boni werden separat im GameLoop angewendet.
    /// </summary>
    [JsonIgnore]
    public decimal TotalIncomePerSecond
    {
        get
        {
            decimal total = Workshops.Sum(w => w.GrossIncomePerSecond);
            // Cap bei 50x für alte Spielstände die vor dem DoPrestige-Cap gespeichert wurden
            decimal multiplier = Math.Min(Prestige.PermanentMultiplier, 50.0m);
            return total * multiplier;
        }
    }

    /// <summary>
    /// Total running costs per second from all workshops.
    /// </summary>
    [JsonIgnore]
    public decimal TotalCostsPerSecond => Workshops.Sum(w => w.TotalCostsPerHour) / 3600m;

    /// <summary>
    /// Roher Netto-Einkommenswert (Brutto - Kosten) OHNE Research/Prestige/Building-Modifikatoren.
    /// Die tatsächliche Berechnung mit allen Modifikatoren erfolgt im GameLoopService.
    /// Nur für Display-Zwecke (Dashboard, DailyChallengeService).
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

    /// <summary>
    /// Fügt XP hinzu. Wendet XP-Boost (2x) und Prestige-Shop-XP-Bonus an.
    /// </summary>
    public int AddXp(int amount)
    {
        // XP-Boost aus DailyReward (2x)
        if (IsXpBoostActive)
            amount *= 2;

        // Prestige-Shop XP-Multiplikator
        var xpBonus = GetPrestigeXpBonus();
        if (xpBonus > 0)
            amount = (int)(amount * (1m + xpBonus));

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

    /// <summary>
    /// Berechnet den XP-Multiplikator-Bonus aus gekauften Prestige-Shop-Items.
    /// </summary>
    private decimal GetPrestigeXpBonus()
    {
        var purchased = Prestige.PurchasedShopItems;
        if (purchased.Count == 0) return 0m;

        var allItems = PrestigeShop.GetAllItems();
        decimal bonus = 0m;
        foreach (var item in allItems)
        {
            if (purchased.Contains(item.Id) && item.Effect.XpMultiplier > 0)
                bonus += item.Effect.XpMultiplier;
        }
        return bonus;
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
        {
            old.Researches = ResearchTree.CreateAll();
        }
        else
        {
            // Prerequisites aus der aktuellen ResearchTree-Definition synchronisieren
            // (damit Änderungen am Baum-Layout auch bei bestehenden Spielständen wirken)
            var template = ResearchTree.CreateAll();
            foreach (var tmpl in template)
            {
                var existing = old.Researches.FirstOrDefault(r => r.Id == tmpl.Id);
                if (existing != null)
                    existing.Prerequisites = tmpl.Prerequisites;
            }
        }

        return old;
    }
}
