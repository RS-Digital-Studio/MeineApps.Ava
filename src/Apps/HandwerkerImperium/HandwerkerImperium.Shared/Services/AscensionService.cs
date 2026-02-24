using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Implementiert das Ascension-System (Meta-Prestige nach 3x Legende).
/// Ascension setzt alle Prestige-Fortschritte zurück und vergibt Ascension-Punkte
/// für permanente Perks die über Prestiges hinweg bestehen bleiben.
/// </summary>
public class AscensionService : IAscensionService
{
    private readonly IGameStateService _gameStateService;
    private readonly ISaveGameService _saveGameService;

    public AscensionService(IGameStateService gameStateService, ISaveGameService saveGameService)
    {
        _gameStateService = gameStateService;
        _saveGameService = saveGameService;
    }

    public bool CanAscend()
    {
        var state = _gameStateService.State;
        return state.Prestige.LegendeCount >= 3;
    }

    public async Task<bool> DoAscension()
    {
        if (!CanAscend()) return false;

        var state = _gameStateService.State;
        var ascension = state.Ascension;

        // Ascension-Punkte berechnen: Mindestens 1, steigend mit jedem Ascension
        int points = 1 + (ascension.AscensionLevel / 2);

        // Ascension-Level und Punkte erhöhen
        ascension.AscensionLevel++;
        ascension.AscensionPoints += points;
        ascension.TotalAscensionPoints += points;

        // === PRESTIGE-DATEN ZURÜCKSETZEN ===
        var prestige = state.Prestige;
        prestige.BronzeCount = 0;
        prestige.SilverCount = 0;
        prestige.GoldCount = 0;
        prestige.PlatinCount = 0;
        prestige.DiamantCount = 0;
        prestige.MeisterCount = 0;
        prestige.LegendeCount = 0;
        prestige.PermanentMultiplier = 1.0m;
        prestige.PrestigePoints = 0;
        // TotalPrestigePoints bleibt erhalten (Lifetime-Statistik)
        prestige.CurrentTier = PrestigeTier.None;
        prestige.PurchasedShopItems.Clear();

        // Legacy-Felder synchron halten
        state.PrestigeLevel = 0;
        state.PrestigeMultiplier = 1.0m;

        // === VOLLER PROGRESS-RESET (wie Bronze-Prestige) ===
        ResetProgressForAscension(state);

        await _saveGameService.SaveAsync();

        return true;
    }

    public bool BuyPerk(string perkId)
    {
        var state = _gameStateService.State;
        var ascension = state.Ascension;

        var allPerks = AscensionPerk.GetAll();
        var perk = allPerks.Find(p => p.Id == perkId);
        if (perk == null) return false;

        int currentLevel = ascension.GetPerkLevel(perkId);

        // Maximales Level erreicht?
        if (currentLevel >= perk.MaxLevel) return false;

        // Kosten für nächstes Level ermitteln
        int cost = perk.CostsPerLevel[currentLevel];

        // Genug Punkte?
        if (ascension.AscensionPoints < cost) return false;

        // Kaufen: Punkte abziehen, Level erhöhen
        ascension.AscensionPoints -= cost;
        ascension.Perks[perkId] = currentLevel + 1;

        return true;
    }

    public List<(AscensionPerk Perk, int CurrentLevel)> GetPerksWithLevels()
    {
        var ascension = _gameStateService.State.Ascension;
        var allPerks = AscensionPerk.GetAll();

        return allPerks
            .Select(p => (p, ascension.GetPerkLevel(p.Id)))
            .ToList();
    }

    public decimal GetStartCapitalMultiplier()
    {
        var level = _gameStateService.State.Ascension.GetPerkLevel("asc_start_capital");
        if (level <= 0) return 0m;

        var perk = AscensionPerk.GetAll().Find(p => p.Id == "asc_start_capital");
        return perk?.ValuesPerLevel[level - 1] ?? 0m;
    }

    public decimal GetResearchDurationReduction()
    {
        var level = _gameStateService.State.Ascension.GetPerkLevel("asc_timeless_research");
        if (level <= 0) return 0m;

        var perk = AscensionPerk.GetAll().Find(p => p.Id == "asc_timeless_research");
        return perk?.ValuesPerLevel[level - 1] ?? 0m;
    }

    public decimal GetGoldenScrewBonus()
    {
        var level = _gameStateService.State.Ascension.GetPerkLevel("asc_golden_era");
        if (level <= 0) return 0m;

        var perk = AscensionPerk.GetAll().Find(p => p.Id == "asc_golden_era");
        return perk?.ValuesPerLevel[level - 1] ?? 0m;
    }

    public int GetStartReputation()
    {
        var level = _gameStateService.State.Ascension.GetPerkLevel("asc_legendary_reputation");
        if (level <= 0) return 50; // Standard-Startwert

        var perk = AscensionPerk.GetAll().Find(p => p.Id == "asc_legendary_reputation");
        return perk != null ? (int)perk.ValuesPerLevel[level - 1] : 50;
    }

    public int GetStartWorkshopCount()
    {
        var level = _gameStateService.State.Ascension.GetPerkLevel("asc_quick_start");
        if (level <= 0) return 1; // Standard: nur Carpenter

        var perk = AscensionPerk.GetAll().Find(p => p.Id == "asc_quick_start");
        return perk != null ? (int)perk.ValuesPerLevel[level - 1] : 1;
    }

    /// <summary>
    /// Voller Progress-Reset für Ascension (härter als Bronze-Prestige,
    /// da auch alle Prestige-Daten zurückgesetzt werden).
    /// Behält: Achievements, Premium, Settings, Tutorial, TotalMoneyEarned,
    /// TotalPlayTimeSeconds, Ascension-Daten, CreatedAt.
    /// </summary>
    private static void ResetProgressForAscension(GameState state)
    {
        // === RESET: Player Progress ===
        state.PlayerLevel = 1;
        state.CurrentXp = 0;
        state.TotalXp = 0;

        // === RESET: Money (TotalMoneyEarned bleibt!) ===
        state.Money = 100m;
        state.TotalMoneySpent = 0m;

        // === RESET: Goldschrauben (TotalGoldenScrewsEarned bleibt als Statistik) ===
        // Goldschrauben behalten - sind Premium-Währung und werden nicht durch Ascension entfernt

        // === RESET: Workshops → nur Carpenter Level 1 mit 1 Worker ===
        state.Workshops.Clear();
        state.UnlockedWorkshopTypes.Clear();
        state.UnlockedWorkshopTypes.Add(WorkshopType.Carpenter);

        var carpenter = Workshop.Create(WorkshopType.Carpenter);
        carpenter.IsUnlocked = true;
        carpenter.Workers.Add(Worker.CreateRandom());
        state.Workshops.Add(carpenter);

        // === RESET: Workers ===
        state.WorkerMarket = null;
        state.TotalWorkersHired = 0;
        state.TotalWorkersFired = 0;

        // === RESET: Orders ===
        state.AvailableOrders.Clear();
        state.ActiveOrder = null;
        state.TotalOrdersCompleted = 0;
        state.OrdersCompletedToday = 0;
        state.OrdersCompletedThisWeek = 0;
        state.LastOrderCooldownStart = DateTime.MinValue;
        state.WeeklyOrderReset = DateTime.UtcNow;

        // === RESET: Reputation ===
        state.Reputation = new CustomerReputation();
        state.LastReputationDecay = DateTime.UtcNow;

        // === RESET: Buildings ===
        state.Buildings.Clear();

        // === RESET: Research ===
        state.Researches = ResearchTree.CreateAll();
        state.ActiveResearchId = null;

        // === RESET: Events ===
        state.ActiveEvent = null;
        state.LastEventCheck = DateTime.UtcNow;
        state.EventHistory.Clear();

        // === RESET: Statistics (TotalPlayTimeSeconds bleibt!) ===
        state.TotalMiniGamesPlayed = 0;
        state.PerfectRatings = 0;
        state.PerfectStreak = 0;
        state.BestPerfectStreak = 0;

        // === RESET: Boosts ===
        state.SpeedBoostEndTime = DateTime.MinValue;
        state.XpBoostEndTime = DateTime.MinValue;
        state.RushBoostEndTime = DateTime.MinValue;
        state.LastFreeRushUsed = DateTime.MinValue;

        // === RESET: Daily Rewards ===
        state.DailyRewardStreak = 0;
        state.LastDailyRewardClaim = DateTime.MinValue;

        // === RESET: Lieferant ===
        state.PendingDelivery = null;
        state.NextDeliveryTime = DateTime.MinValue;
        state.TotalDeliveriesClaimed = 0;

        // === RESET: Quick Jobs ===
        state.QuickJobs.Clear();
        state.LastQuickJobRotation = DateTime.MinValue;
        state.TotalQuickJobsCompleted = 0;
        state.QuickJobsCompletedToday = 0;
        state.LastQuickJobDailyReset = DateTime.MinValue;

        // === RESET: Daily Challenges ===
        state.DailyChallengeState = new DailyChallengeState();

        // === RESET: Story (pending Story leeren, viewed bleiben erhalten) ===
        state.PendingStoryId = null;

        // === RESET: Meisterwerkzeuge ===
        state.CollectedMasterTools.Clear();

        // === RESET: Lucky Spin ===
        state.LuckySpin = new LuckySpinState();

        // === RESET: Weekly Missions ===
        state.WeeklyMissionState = new WeeklyMissionState();

        // === RESET: Welcome Back Offer ===
        state.ActiveWelcomeBackOffer = null;

        // === RESET: Tournament ===
        state.CurrentTournament = null;

        // === RESET: Crafting ===
        state.CraftingInventory.Clear();
        state.ActiveCraftingJobs.Clear();

        // === RESET: Daily Shop Offer ===
        state.DailyShopOffer = null;

        // === RESET: Equipment ===
        state.EquipmentInventory.Clear();

        // === RESET: Manager ===
        state.Managers.Clear();

        // === RESET: Tools ===
        state.Tools = Tool.CreateDefaults();

        // === ERHALTEN: ===
        // - state.Ascension (AscensionData mit Perks und Punkten)
        // - state.UnlockedAchievements
        // - state.IsPremium
        // - state.TutorialCompleted, state.TutorialStep
        // - state.TotalMoneyEarned
        // - state.TotalPlayTimeSeconds
        // - state.GoldenScrews, state.TotalGoldenScrewsEarned, state.TotalGoldenScrewsSpent
        // - state.SoundEnabled, state.MusicEnabled, state.HapticsEnabled, state.Language
        // - state.CreatedAt
        // - state.BattlePass (zeitbasiert + bezahlt)
        // - state.CurrentSeasonalEvent (zeitbasiert)
        // - state.HasPurchasedStarterPack
        // - state.VipLevel, state.TotalPurchaseAmount
        // - state.Friends
        // - state.ViewedStoryIds
    }
}
