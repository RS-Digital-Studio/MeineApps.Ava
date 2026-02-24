using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Implements the 3-tier prestige system (Bronze / Silver / Gold).
/// Each tier resets progress but grants permanent multipliers and prestige points.
/// Bronze: Basic reset. Silver: Preserves research. Gold: Preserves shop items.
/// </summary>
public class PrestigeService : IPrestigeService
{
    private readonly IGameStateService _gameStateService;
    private readonly ISaveGameService _saveGameService;

    public event EventHandler? PrestigeCompleted;

    public PrestigeService(IGameStateService gameStateService, ISaveGameService saveGameService)
    {
        _gameStateService = gameStateService;
        _saveGameService = saveGameService;
    }

    public bool CanPrestige(PrestigeTier tier)
    {
        if (tier == PrestigeTier.None) return false;

        var state = _gameStateService.State;
        return state.Prestige.CanPrestige(tier, state.PlayerLevel);
    }

    public int GetPrestigePoints(decimal totalMoneyEarned)
    {
        // Basis-Punkte aus PrestigeData (floor(sqrt(totalMoney / 100_000)))
        return PrestigeData.CalculatePrestigePoints(totalMoneyEarned);
    }

    public async Task<bool> DoPrestige(PrestigeTier tier)
    {
        if (!CanPrestige(tier)) return false;

        var state = _gameStateService.State;
        var prestige = state.Prestige;

        // Prestige-Punkte berechnen (Tier-Multiplikator anwenden)
        int basePoints = GetPrestigePoints(state.TotalMoneyEarned);
        int tierPoints = (int)(basePoints * tier.GetPointMultiplier());

        // Prestige-Pass: +50% Bonus auf Prestige-Punkte
        if (state.IsPrestigePassActive)
            tierPoints = (int)(tierPoints * 1.5m);

        // Gilden-Forschung: Prestige-Punkte-Bonus (+10%)
        if (state.GuildMembership?.ResearchPrestigePointBonus > 0)
            tierPoints = (int)(tierPoints * (1m + state.GuildMembership.ResearchPrestigePointBonus));

        prestige.PrestigePoints += tierPoints;
        prestige.TotalPrestigePoints += tierPoints;

        // Tier-Zähler erhöhen
        switch (tier)
        {
            case PrestigeTier.Bronze:
                prestige.BronzeCount++;
                break;
            case PrestigeTier.Silver:
                prestige.SilverCount++;
                break;
            case PrestigeTier.Gold:
                prestige.GoldCount++;
                break;
            case PrestigeTier.Platin:
                prestige.PlatinCount++;
                break;
            case PrestigeTier.Diamant:
                prestige.DiamantCount++;
                break;
            case PrestigeTier.Meister:
                prestige.MeisterCount++;
                break;
            case PrestigeTier.Legende:
                prestige.LegendeCount++;
                break;
        }

        // Hoechsten Tier tracken
        if (tier > prestige.CurrentTier)
            prestige.CurrentTier = tier;

        // Permanenten Multiplier erhoehen (Tier-Bonus), Cap bei 20x
        prestige.PermanentMultiplier += tier.GetPermanentMultiplierBonus();
        prestige.PermanentMultiplier = Math.Min(Math.Round(prestige.PermanentMultiplier, 3), MaxPermanentMultiplier);

        // Legacy-Felder synchron halten
        state.PrestigeLevel = prestige.TotalPrestigeCount;
        state.PrestigeMultiplier = prestige.PermanentMultiplier;

        // Reset durchfuehren
        ResetProgress(state, tier);

        await _saveGameService.SaveAsync();

        PrestigeCompleted?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>
    /// Prestige-Pass aktivieren (nach erfolgreichem IAP-Kauf).
    /// Setzt IsPrestigePassActive auf den aktuellen Durchlauf.
    /// </summary>
    public void ActivatePrestigePass()
    {
        _gameStateService.State.IsPrestigePassActive = true;
    }

    public List<PrestigeShopItem> GetShopItems()
    {
        var allItems = PrestigeShop.GetAllItems();
        var purchased = _gameStateService.State.Prestige.PurchasedShopItems;

        // Kaufstatus aus PrestigeData uebernehmen
        foreach (var item in allItems)
        {
            item.IsPurchased = purchased.Contains(item.Id);
        }

        return allItems;
    }

    public bool BuyShopItem(string itemId)
    {
        var prestige = _gameStateService.State.Prestige;
        var allItems = PrestigeShop.GetAllItems();

        var item = allItems.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return false;

        // Bereits gekauft?
        if (prestige.PurchasedShopItems.Contains(itemId)) return false;

        // Genug Punkte?
        if (prestige.PrestigePoints < item.Cost) return false;

        prestige.PrestigePoints -= item.Cost;
        prestige.PurchasedShopItems.Add(itemId);

        // Shop-Boni werden dynamisch in GetPermanentMultiplier() berechnet,
        // keine extra Neuberechnung nötig.

        return true;
    }

    /// <summary>
    /// Maximaler Prestige-Multiplikator (nur Tier-Boni, nicht Shop-Income-Boni).
    /// Shop-Income-Boni werden separat im GameLoop/OfflineProgress angewendet.
    /// </summary>
    private const decimal MaxPermanentMultiplier = 200.0m;

    public decimal GetPermanentMultiplier()
    {
        // Nur Tier-Multiplikator zurückgeben (bereits gekappt beim Schreiben in DoPrestige).
        // Shop-Income-Boni (pp_income_10/25/50) werden separat in GameLoop + OfflineProgress angewendet.
        return _gameStateService.State.Prestige.PermanentMultiplier;
    }

    public decimal GetCostReduction()
    {
        var purchased = _gameStateService.State.Prestige.PurchasedShopItems;
        var allItems = PrestigeShop.GetAllItems();
        decimal reduction = 0m;

        foreach (var item in allItems)
        {
            if (purchased.Contains(item.Id) && item.Effect.CostReduction > 0)
                reduction += item.Effect.CostReduction;
        }

        // Cap bei 50% Reduktion
        return Math.Min(reduction, 0.50m);
    }

    public decimal GetMoodDecayReduction()
    {
        var purchased = _gameStateService.State.Prestige.PurchasedShopItems;
        var allItems = PrestigeShop.GetAllItems();
        decimal reduction = 0m;

        foreach (var item in allItems)
        {
            if (purchased.Contains(item.Id) && item.Effect.MoodDecayReduction > 0)
                reduction += item.Effect.MoodDecayReduction;
        }

        // Cap bei 50% Reduktion
        return Math.Min(reduction, 0.50m);
    }

    public decimal GetXpMultiplier()
    {
        var purchased = _gameStateService.State.Prestige.PurchasedShopItems;
        var allItems = PrestigeShop.GetAllItems();
        decimal bonus = 0m;

        foreach (var item in allItems)
        {
            if (purchased.Contains(item.Id) && item.Effect.XpMultiplier > 0)
                bonus += item.Effect.XpMultiplier;
        }

        return bonus;
    }

    /// <summary>
    /// Resets game progress based on prestige tier.
    /// Bronze: Full reset (keep achievements, premium, settings, prestige, tutorial, TotalMoneyEarned, TotalPlayTimeSeconds).
    /// Silver: Additionally preserves research.
    /// Gold: Additionally preserves prestige shop items (already in PrestigeData, so no extra handling needed).
    /// </summary>
    private static void ResetProgress(GameState state, PrestigeTier tier)
    {
        // Startgeld berechnen (100 Basis + Shop-Boni)
        decimal startMoney = 100m;
        var purchased = state.Prestige.PurchasedShopItems;
        var allItems = PrestigeShop.GetAllItems();
        foreach (var item in allItems)
        {
            if (purchased.Contains(item.Id) && item.Effect.ExtraStartMoney > 0)
            {
                startMoney += item.Effect.ExtraStartMoney;
            }
        }

        // Start-Worker-Tier aus Shop bestimmen
        var startWorkerTier = WorkerTier.E;
        foreach (var item in allItems)
        {
            if (purchased.Contains(item.Id) && item.Effect.StartingWorkerTier != null)
            {
                if (Enum.TryParse<WorkerTier>(item.Effect.StartingWorkerTier, out var shopTier) && shopTier > startWorkerTier)
                {
                    startWorkerTier = shopTier;
                }
            }
        }

        // === RESET: Player Progress ===
        state.PlayerLevel = 1;
        state.CurrentXp = 0;
        state.TotalXp = 0;

        // === RESET: Money (TotalMoneyEarned bleibt!) ===
        state.Money = startMoney;
        state.TotalMoneySpent = 0m;

        // === RESET: Workshops -> nur Carpenter Level 1 mit 1 Worker ===
        state.Workshops.Clear();
        state.UnlockedWorkshopTypes.Clear();
        state.UnlockedWorkshopTypes.Add(WorkshopType.Carpenter);

        var carpenter = Workshop.Create(WorkshopType.Carpenter);
        carpenter.IsUnlocked = true;
        carpenter.Workers.Add(Worker.CreateForTier(startWorkerTier));
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

        // === RESET: Buildings (Diamant+ behält Gebäude, Level wird weiter unten auf 1 gesetzt) ===
        if (!tier.KeepsBuildings())
            state.Buildings.Clear();

        // === RESET: Research (Silver + Gold behalten Research!) ===
        if (!tier.KeepsResearch())
        {
            state.Researches = ResearchTree.CreateAll();
            state.ActiveResearchId = null;
        }

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

        // === RESET: Meisterwerkzeuge (Platin+ behält sie) ===
        if (!tier.KeepsMasterTools())
            state.CollectedMasterTools.Clear();

        // === RESET: Lucky Spin (immer zurücksetzen) ===
        state.LuckySpin = new LuckySpinState();

        // === RESET: Weekly Missions (immer zurücksetzen) ===
        state.WeeklyMissionState = new WeeklyMissionState();

        // === RESET: Welcome Back Offer (immer zurücksetzen) ===
        state.ActiveWelcomeBackOffer = null;

        // === RESET: Tournament (immer zurücksetzen) ===
        state.CurrentTournament = null;

        // === RESET: Crafting (immer zurücksetzen) ===
        state.CraftingInventory.Clear();
        state.ActiveCraftingJobs.Clear();

        // === RESET: Daily Shop Offer (immer zurücksetzen) ===
        state.DailyShopOffer = null;

        // === RESET: Workshop-Spezialisierung (immer zurücksetzen) ===
        foreach (var ws in state.Workshops)
        {
            ws.WorkshopSpecialization = null;
        }

        // === Gilden-Mitgliedschaft bleibt bestehen (Firebase kümmert sich um Weekly-Reset) ===

        // === BEDINGT: Equipment (Diamant+ behält es) ===
        if (!tier.KeepsEquipment())
        {
            state.EquipmentInventory.Clear();
            foreach (var ws in state.Workshops)
            {
                foreach (var worker in ws.Workers)
                {
                    worker.EquippedItem = null;
                }
            }
        }

        // === BEDINGT: Manager (Meister+ behält sie, Level→1) ===
        if (!tier.KeepsManagers())
        {
            state.Managers.Clear();
        }
        else
        {
            // Manager behalten, aber Level auf 1 zurücksetzen
            foreach (var mgr in state.Managers)
            {
                mgr.Level = 1;
            }
        }

        // === BEDINGT: Legende behält 1 besten Worker pro Workshop ===
        if (tier.KeepsBestWorkers())
        {
            // Pro Workshop nur den Worker mit der höchsten Efficiency behalten
            foreach (var ws in state.Workshops)
            {
                if (ws.Workers.Count > 1)
                {
                    var bestWorker = ws.Workers.OrderByDescending(w => w.Efficiency).First();
                    ws.Workers.Clear();
                    ws.Workers.Add(bestWorker);
                }
            }
        }

        // === RESET: Gebäude (Diamant+ behält sie mit Level→1) ===
        if (tier.KeepsBuildings())
        {
            foreach (var b in state.Buildings)
            {
                b.Level = 1;
            }
        }
        // (Unterhalb von Diamant werden Buildings bereits oben via .Clear() zurückgesetzt)

        // === PRESERVED (nicht angefasst): ===
        // - state.Prestige (PrestigeData mit Punkten, Shop-Items, Tier-Counts)
        // - state.UnlockedAchievements
        // - state.IsPremium
        // - state.TutorialCompleted, state.TutorialStep
        // - state.TotalMoneyEarned
        // - state.TotalPlayTimeSeconds
        // - state.SoundEnabled, state.MusicEnabled, state.HapticsEnabled, state.Language
        // - state.CreatedAt
        // - state.BattlePass (zeitbasiert + bezahlt)
        // - state.CurrentSeasonalEvent (zeitbasiert)
        // - state.ClaimedLevelOffers
        // - state.HasPurchasedStarterPack
        // - state.VipLevel, state.TotalPurchaseAmount
        // - state.Friends
        // - state.TotalTournamentsPlayed
        // - state.StreakRescueUsed

        // === RESET: Prestige-Pass (wird bei jedem Prestige verbraucht) ===
        state.IsPrestigePassActive = false;

        // Gold prestige preserves shop items (already in PrestigeData.PurchasedShopItems,
        // which is not touched by reset). Nothing extra needed here.
    }
}
