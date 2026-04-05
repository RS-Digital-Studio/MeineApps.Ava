using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// The complete game state, persisted between sessions.
/// Version 5: Boosts, DailyProgress und Cosmetics in Sub-Objekte extrahiert.
/// </summary>
public class GameState
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 5;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastSavedAt")]
    public DateTime LastSavedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastPlayedAt")]
    public DateTime LastPlayedAt { get; set; } = DateTime.UtcNow;

    // ═══════════════════════════════════════════════════════════════════════
    // PLAYER PROGRESS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Stabile Spieler-ID (GUID). Backup für Preferences-Verlust.
    /// Wird für alle Firebase-Daten-Pfade verwendet (statt Firebase-UID).
    /// </summary>
    [JsonPropertyName("playerGuid")]
    public string? PlayerGuid { get; set; }

    [JsonPropertyName("playerLevel")]
    public int PlayerLevel { get; set; } = 1;

    [JsonPropertyName("currentXp")]
    public int CurrentXp { get; set; }

    [JsonPropertyName("totalXp")]
    public int TotalXp { get; set; }

    [JsonPropertyName("money")]
    public decimal Money { get; set; } = 1000m;

    [JsonPropertyName("totalMoneyEarned")]
    public decimal TotalMoneyEarned { get; set; }

    /// <summary>
    /// Geld verdient seit dem letzten Prestige. Wird bei Prestige zurückgesetzt.
    /// Basis für die Prestige-Punkte-Berechnung (statt TotalMoneyEarned).
    /// </summary>
    [JsonPropertyName("currentRunMoney")]
    public decimal CurrentRunMoney { get; set; }

    [JsonPropertyName("totalMoneySpent")]
    public decimal TotalMoneySpent { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // GOLDSCHRAUBEN (Premium-Waehrung)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("goldenScrews")]
    public int GoldenScrews { get; set; }

    [JsonPropertyName("totalGoldenScrewsEarned")]
    public long TotalGoldenScrewsEarned { get; set; }

    [JsonPropertyName("totalGoldenScrewsSpent")]
    public long TotalGoldenScrewsSpent { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // PREMIUM AD-REWARDS & COOLDOWNS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Anzahl der heute genutzten Premium-Ad-Rewards (max 3/Tag ohne Video).
    /// </summary>
    [JsonPropertyName("premiumAdRewardsUsedToday")]
    public int PremiumAdRewardsUsedToday { get; set; }

    /// <summary>
    /// Letzter Reset-Zeitpunkt der Premium-Ad-Rewards (Tages-Reset).
    /// </summary>
    [JsonPropertyName("lastPremiumAdRewardReset")]
    public DateTime LastPremiumAdRewardReset { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Letzte Einlösung eines Shop-Ad-Rewards (3h Cooldown für Free-User).
    /// </summary>
    [JsonPropertyName("lastShopAdRewardTime")]
    public DateTime LastShopAdRewardTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Letzte Einlösung der Goldschrauben-Ad im Shop (eigener 4h-Cooldown).
    /// Getrennt von LastShopAdRewardTime, damit GS-Ad nicht mit Cash-Ads konkurriert.
    /// </summary>
    [JsonPropertyName("lastGoldenScrewsAdTime")]
    public DateTime LastGoldenScrewsAdTime { get; set; } = DateTime.MinValue;

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

    /// <summary>
    /// EVENT-5: Pity-Counter für negative Events. Zählt aufeinanderfolgende negative Events.
    /// Nach 2 negativen in Folge werden negative Events für das nächste Event ausgeschlossen.
    /// </summary>
    [JsonPropertyName("consecutiveNegativeEvents")]
    public int ConsecutiveNegativeEvents { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // STATISTICS (Sub-Objekt seit V4)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("statistics")]
    public StatisticsData Statistics { get; set; } = new();

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE (3-Tier System)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// New prestige data (3-tier system with shop).
    /// </summary>
    [JsonPropertyName("prestige")]
    public PrestigeData Prestige { get; set; } = new();

    /// <summary>
    /// Ascension-Daten (Meta-Prestige). Reserviert für zukünftige Ascension-Funktionalität.
    /// Property wird persistiert (JSON) - nicht löschen wegen Save-Kompatibilität.
    /// </summary>
    [JsonPropertyName("ascension")]
    public AscensionData Ascension { get; set; } = new();

    // Legacy fields for v1 save compatibility
    [JsonPropertyName("prestigeLevel")]
    public int PrestigeLevel { get; set; }

    [JsonPropertyName("prestigeMultiplier")]
    public decimal PrestigeMultiplier { get; set; } = 1.0m;

    // ═══════════════════════════════════════════════════════════════════════
    // WORKSHOP REBIRTH (Late-Game Prestige pro Workshop)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rebirth-Sterne pro Workshop (Key = WorkshopType.ToString(), Value = 0-5).
    /// Permanent: überlebt Prestige + Ascension. Nur über RebirthService änderbar.
    /// </summary>
    [JsonPropertyName("workshopStars")]
    public Dictionary<string, int> WorkshopStars { get; set; } = new();

    // ═══════════════════════════════════════════════════════════════════════
    // SETTINGS (Sub-Objekt seit V4)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("settings")]
    public SettingsData Settings { get; set; } = new();

    // ═══════════════════════════════════════════════════════════════════════
    // PREMIUM STATUS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("isPremium")]
    public bool IsPremium { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // DAILY PROGRESS (Sub-Objekt seit V5)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("dailyProgress")]
    public DailyProgressData DailyProgress { get; set; } = new();

    // --- Legacy-Weiterleitungen (Backward-Kompatibilität für V4-Saves) ---

    [JsonPropertyName("lastDailyRewardClaim")]
    public DateTime LastDailyRewardClaim { get => DailyProgress.LastDailyRewardClaim; set => DailyProgress.LastDailyRewardClaim = value; }

    [JsonPropertyName("dailyRewardStreak")]
    public int DailyRewardStreak { get => DailyProgress.DailyRewardStreak; set => DailyProgress.DailyRewardStreak = value; }

    // ═══════════════════════════════════════════════════════════════════════
    // BOOSTS (Sub-Objekt seit V5)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("boosts")]
    public BoostData Boosts { get; set; } = new();

    // --- Legacy-Weiterleitungen (Backward-Kompatibilität für V4-Saves) ---
    // V4-Saves haben diese Properties flach. System.Text.Json deserialisiert sie hierher,
    // MigrateFromV4() kopiert sie in das Boosts-Sub-Objekt.

    [JsonPropertyName("speedBoostEndTime")]
    public DateTime SpeedBoostEndTime { get => Boosts.SpeedBoostEndTime; set => Boosts.SpeedBoostEndTime = value; }

    [JsonPropertyName("xpBoostEndTime")]
    public DateTime XpBoostEndTime { get => Boosts.XpBoostEndTime; set => Boosts.XpBoostEndTime = value; }

    [JsonPropertyName("rushBoostEndTime")]
    public DateTime RushBoostEndTime { get => Boosts.RushBoostEndTime; set => Boosts.RushBoostEndTime = value; }

    [JsonPropertyName("lastFreeRushUsed")]
    public DateTime LastFreeRushUsed { get => Boosts.LastFreeRushUsed; set => Boosts.LastFreeRushUsed = value; }

    [JsonIgnore]
    public bool IsSpeedBoostActive => Boosts.IsSpeedBoostActive;

    [JsonIgnore]
    public bool IsXpBoostActive => Boosts.IsXpBoostActive;

    [JsonIgnore]
    public bool IsRushBoostActive => Boosts.IsRushBoostActive;

    [JsonIgnore]
    public bool IsSoftCapActive { get => Boosts.IsSoftCapActive; set => Boosts.IsSoftCapActive = value; }

    [JsonIgnore]
    public int SoftCapReductionPercent { get => Boosts.SoftCapReductionPercent; set => Boosts.SoftCapReductionPercent = value; }

    [JsonIgnore]
    public bool IsFreeRushAvailable => Boosts.IsFreeRushAvailable;

    // ═══════════════════════════════════════════════════════════════════════
    // ACHIEVEMENTS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("unlockedAchievements")]
    public List<string> UnlockedAchievements { get; set; } = [];

    // --- Quick Jobs Legacy-Weiterleitungen (Backward-Kompatibilität für V4-Saves) ---

    [JsonPropertyName("quickJobs")]
    public List<QuickJob> QuickJobs { get => DailyProgress.QuickJobs; set => DailyProgress.QuickJobs = value; }

    [JsonPropertyName("lastQuickJobRotation")]
    public DateTime LastQuickJobRotation { get => DailyProgress.LastQuickJobRotation; set => DailyProgress.LastQuickJobRotation = value; }

    [JsonPropertyName("totalQuickJobsCompleted")]
    public int TotalQuickJobsCompleted { get => DailyProgress.TotalQuickJobsCompleted; set => DailyProgress.TotalQuickJobsCompleted = value; }

    [JsonPropertyName("quickJobsCompletedToday")]
    public int QuickJobsCompletedToday { get => DailyProgress.QuickJobsCompletedToday; set => DailyProgress.QuickJobsCompletedToday = value; }

    [JsonPropertyName("lastQuickJobDailyReset")]
    public DateTime LastQuickJobDailyReset { get => DailyProgress.LastQuickJobDailyReset; set => DailyProgress.LastQuickJobDailyReset = value; }

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

    // ═══════════════════════════════════════════════════════════════════════
    // TUTORIAL (Sub-Objekt seit V4)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("tutorial")]
    public TutorialState Tutorial { get; set; } = new();

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

    // --- Weekly Missions / Welcome Back / Streak Legacy-Weiterleitungen ---

    [JsonPropertyName("weeklyMissionState")]
    public WeeklyMissionState WeeklyMissionState { get => DailyProgress.WeeklyMissionState; set => DailyProgress.WeeklyMissionState = value; }

    [JsonPropertyName("activeWelcomeBackOffer")]
    public WelcomeBackOffer? ActiveWelcomeBackOffer { get => DailyProgress.ActiveWelcomeBackOffer; set => DailyProgress.ActiveWelcomeBackOffer = value; }

    [JsonPropertyName("claimedStarterPack")]
    public bool ClaimedStarterPack { get => DailyProgress.ClaimedStarterPack; set => DailyProgress.ClaimedStarterPack = value; }

    [JsonPropertyName("streakBeforeBreak")]
    public int StreakBeforeBreak { get => DailyProgress.StreakBeforeBreak; set => DailyProgress.StreakBeforeBreak = value; }

    [JsonPropertyName("streakRescueUsed")]
    public bool StreakRescueUsed { get => DailyProgress.StreakRescueUsed; set => DailyProgress.StreakRescueUsed = value; }

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

    /// <summary>Datum des letzten Lieferauftrags-Resets.</summary>
    [JsonPropertyName("lastMaterialOrderReset")]
    public string LastMaterialOrderReset { get; set; } = "";

    /// <summary>Set der abgeschlossenen Crafting-Rezept-IDs.</summary>
    [JsonPropertyName("completedRecipeIds")]
    public List<string> CompletedRecipeIds { get; set; } = [];

    /// <summary>Set der MiniGame-Typen mit mindestens einem Perfect Rating.</summary>
    [JsonPropertyName("perfectMiniGameTypes")]
    public List<string> PerfectMiniGameTypes { get; set; } = [];

    /// <summary>
    /// Zähler für Perfect-Ratings pro MiniGame-Typ (Key = MiniGameType als int).
    /// Wird für Auto-Complete-Feature verwendet (30x Perfect → Auto-Ergebnis, Premium 15x).
    /// </summary>
    [JsonPropertyName("perfectRatingCounts")]
    public Dictionary<int, int> PerfectRatingCounts { get; set; } = new();

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

    /// <summary>
    /// Ob der Prestige-Pass für den aktuellen Durchlauf aktiv ist.
    /// Wird bei jedem Prestige zurückgesetzt.
    /// </summary>
    [JsonPropertyName("isPrestigePassActive")]
    public bool IsPrestigePassActive { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // CONTEXTUAL OFFERS (Welle 5)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Level-Meilensteine bei denen bereits ein Angebot gezeigt wurde.
    /// </summary>
    [JsonPropertyName("claimedLevelOffers")]
    public List<int> ClaimedLevelOffers { get; set; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // STARTER OFFER (einmaliges zeitlich begrenztes Premium-Angebot)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ob das Starter-Offer bereits angezeigt wurde (nur 1x pro Spieler).
    /// </summary>
    [JsonPropertyName("starterOfferShown")]
    public bool StarterOfferShown { get; set; }

    /// <summary>
    /// Zeitpunkt an dem das Starter-Offer aktiviert wurde (für 24h-Countdown).
    /// </summary>
    [JsonPropertyName("starterOfferTimestamp")]
    public DateTime? StarterOfferTimestamp { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // GUILD (Welle 6)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("guildMembership")]
    public GuildMembership? GuildMembership { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // INTEGRITY (Manipulationsschutz fuer Gilden-relevante Werte)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// HMAC-SHA256-Signatur ueber Gilden-relevante Werte (Level, Prestige, Geld, Goldschrauben, Auftraege).
    /// Wird bei jedem Save berechnet und vor Firebase-Updates geprueft.
    /// </summary>
    [JsonPropertyName("integritySignature")]
    public string? IntegritySignature { get; set; }

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

    /// <summary>
    /// Letztes Datum an dem ein Geschenk an einen echten Freund gesendet wurde (1x/Tag).
    /// </summary>
    [JsonPropertyName("lastGiftSentDate")]
    public DateTime LastGiftSentDate { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Spielername für Firebase-Profile (Gilden, Leaderboards, Chat).
    /// </summary>
    [JsonPropertyName("playerName")]
    public string? PlayerName { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // DAILY SHOP OFFER (Welle 8)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("dailyShopOffer")]
    public ShopOffer? DailyShopOffer { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // COSMETICS (Sub-Objekt seit V5)
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("cosmetics")]
    public CosmeticData Cosmetics { get; set; } = new();

    // --- Legacy-Weiterleitungen (Backward-Kompatibilität für V4-Saves) ---

    [JsonPropertyName("unlockedCosmetics")]
    public List<string> UnlockedCosmetics { get => Cosmetics.UnlockedCosmetics; set => Cosmetics.UnlockedCosmetics = value; }

    [JsonPropertyName("activeCityThemeId")]
    public string ActiveCityThemeId { get => Cosmetics.ActiveCityThemeId; set => Cosmetics.ActiveCityThemeId = value; }

    [JsonPropertyName("activeWorkshopSkins")]
    public Dictionary<string, string> ActiveWorkshopSkins { get => Cosmetics.ActiveWorkshopSkins; set => Cosmetics.ActiveWorkshopSkins = value; }

    // ═══════════════════════════════════════════════════════════════════════
    // UI/UX STATE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bisheriger Rekord fuer Offline-Einnahmen (fuer "Neuer Rekord!" Anzeige).
    /// </summary>
    [JsonPropertyName("maxOfflineEarnings")]
    public decimal MaxOfflineEarnings { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // OFFLINE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Base offline hours (always 4).
    /// </summary>
    [JsonIgnore]
    public int BaseOfflineHours => 4;

    // Gecachter MaxOfflineHours-Wert (invalidieren bei Prestige-Shop-Kauf oder Premium-Wechsel)
    [JsonIgnore] private int _cachedMaxOfflineHours = -1;

    /// <summary>
    /// Cache invalidieren (nach Prestige-Shop-Kauf oder Premium-Status-Änderung).
    /// </summary>
    public void InvalidateMaxOfflineHoursCache() => _cachedMaxOfflineHours = -1;

    [JsonIgnore]
    public int MaxOfflineHours
    {
        get
        {
            if (_cachedMaxOfflineHours >= 0) return _cachedMaxOfflineHours;

            int baseHours = IsPremium ? 16 : OfflineVideoExtended ? 8 : 4;

            // Prestige-Shop OfflineHoursBonus addieren (pp_offline_hours: +4h)
            var purchased = Prestige.PurchasedShopItems;
            if (purchased.Count > 0)
            {
                foreach (var item in PrestigeShop.GetAllItems())
                {
                    if (!item.IsRepeatable && purchased.Contains(item.Id) && item.Effect.OfflineHoursBonus > 0)
                        baseHours += item.Effect.OfflineHoursBonus;
                }
            }

            _cachedMaxOfflineHours = baseHours;
            return baseHours;
        }
    }

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

    // Gecachte Einkommens-/Kosten-Werte (vermeidet LINQ .Sum() pro Tick)
    [JsonIgnore] private decimal _cachedIncome;
    [JsonIgnore] private decimal _cachedCosts;
    [JsonIgnore] private bool _incomeCacheDirty = true;

    /// <summary>
    /// Brutto-Einkommen pro Sekunde aus allen Workshops (mit Prestige-Multiplikator, gekappt bei 250x).
    /// Shop-Income-Boni werden separat im GameLoop angewendet.
    /// </summary>
    [JsonIgnore]
    public decimal TotalIncomePerSecond
    {
        get
        {
            if (_incomeCacheDirty) RecalculateIncomeCache();
            return _cachedIncome;
        }
    }

    /// <summary>
    /// Total running costs per second from all workshops.
    /// </summary>
    [JsonIgnore]
    public decimal TotalCostsPerSecond
    {
        get
        {
            if (_incomeCacheDirty) RecalculateIncomeCache();
            return _cachedCosts;
        }
    }

    /// <summary>
    /// Invalidiert den Einkommens-/Kosten-Cache.
    /// Aufrufen bei: Workshop-Level-Up, Worker-Aenderung, Research-Abschluss.
    /// </summary>
    public void InvalidateIncomeCache() => _incomeCacheDirty = true;

    private void RecalculateIncomeCache()
    {
        decimal totalIncome = 0m;
        decimal totalCosts = 0m;
        for (int i = 0; i < Workshops.Count; i++)
        {
            totalIncome += Workshops[i].GrossIncomePerSecond;
            totalCosts += Workshops[i].TotalCostsPerHour;
        }
        // BAL-37: Konsistent mit PrestigeService.MaxPermanentMultiplier (20x)
        decimal multiplier = Math.Min(Prestige.PermanentMultiplier, 20.0m);
        _cachedIncome = totalIncome * multiplier;
        _cachedCosts = totalCosts / 3600m;
        _incomeCacheDirty = false;
    }

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

    public Workshop GetOrCreateWorkshop(WorkshopType type)
    {
        var workshop = Workshops.FirstOrDefault(w => w.Type == type);
        if (workshop == null)
        {
            workshop = Workshop.Create(type);

            // Legende-Prestige: Gesicherte Worker wiederverwenden (bis zu 3 pro Workshop, MaxWorkers beachten)
            var typeKey = type.ToString();
            if (Prestige.KeptWorkers.TryGetValue(typeKey, out var keptWorker))
            {
                workshop.Workers.Add(keptWorker);
                Prestige.KeptWorkers.Remove(typeKey);
            }
            // Indizierte Keys (neue Saves: _1, _2)
            for (int idx = 1; idx <= 2; idx++)
            {
                var indexedKey = $"{typeKey}_{idx}";
                if (Prestige.KeptWorkers.TryGetValue(indexedKey, out var extraWorker)
                    && workshop.Workers.Count < workshop.MaxWorkers)
                {
                    workshop.Workers.Add(extraWorker);
                    Prestige.KeptWorkers.Remove(indexedKey);
                }
            }

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
    /// Gecachtes Dictionary für Building-Lookups (vermeidet FirstOrDefault pro Tick).
    /// </summary>
    [JsonIgnore]
    private Dictionary<BuildingType, Building>? _buildingCache;

    /// <summary>
    /// Gets a building by type, returns null if not built.
    /// Nutzt Dictionary-Cache statt FirstOrDefault.
    /// </summary>
    public Building? GetBuilding(BuildingType type)
    {
        if (_buildingCache == null)
            RebuildBuildingCache();

        return _buildingCache!.GetValueOrDefault(type);
    }

    /// <summary>
    /// Cache invalidieren nach Gebäude-Kauf/Upgrade.
    /// </summary>
    public void InvalidateBuildingCache() => _buildingCache = null;

    private void RebuildBuildingCache()
    {
        _buildingCache = new Dictionary<BuildingType, Building>();
        for (int i = 0; i < Buildings.Count; i++)
        {
            var b = Buildings[i];
            if (b.IsBuilt)
                _buildingCache[b.Type] = b;
        }
    }

    /// <summary>
    /// Creates a new game state with default values.
    /// </summary>
    public static GameState CreateNew()
    {
        var state = new GameState();

        // Startwerkstatt (Schreiner) mit 2 Arbeitern für schnelleren Einstieg
        var carpenter = Workshop.Create(WorkshopType.Carpenter);
        carpenter.IsUnlocked = true;
        var worker1 = Worker.CreateRandom();
        worker1.AssignedWorkshop = WorkshopType.Carpenter;
        var worker2 = Worker.CreateRandom();
        worker2.AssignedWorkshop = WorkshopType.Carpenter;
        carpenter.Workers.Add(worker1);
        carpenter.Workers.Add(worker2);
        state.Workshops.Add(carpenter);

        // Initialize research tree
        state.Researches = ResearchTree.CreateAll();

        // Initialize tools
        state.Tools = Tool.CreateDefaults();

        return state;
    }

}
