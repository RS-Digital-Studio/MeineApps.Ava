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

        prestige.PrestigePoints += tierPoints;
        prestige.TotalPrestigePoints += tierPoints;

        // Tier-Zaehler erhoehen
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
        }

        // Hoechsten Tier tracken
        if (tier > prestige.CurrentTier)
            prestige.CurrentTier = tier;

        // Permanenten Multiplier erhoehen (Tier-Bonus)
        prestige.PermanentMultiplier += tier.GetPermanentMultiplierBonus();
        prestige.PermanentMultiplier = Math.Round(prestige.PermanentMultiplier, 3);

        // Legacy-Felder synchron halten
        state.PrestigeLevel = prestige.TotalPrestigeCount;
        state.PrestigeMultiplier = prestige.PermanentMultiplier;

        // Reset durchfuehren
        ResetProgress(state, tier);

        await _saveGameService.SaveAsync();

        PrestigeCompleted?.Invoke(this, EventArgs.Empty);

        return true;
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

        // Multiplier neu berechnen (Shop-Income-Bonus wirkt auf PermanentMultiplier)
        RecalculatePermanentMultiplier();

        return true;
    }

    public decimal GetPermanentMultiplier()
    {
        var prestige = _gameStateService.State.Prestige;

        // Basis: gespeicherter PermanentMultiplier (enthaelt Tier-Boni)
        decimal multiplier = prestige.PermanentMultiplier;

        // Shop-Income-Boni addieren
        var purchased = prestige.PurchasedShopItems;
        var allItems = PrestigeShop.GetAllItems();

        foreach (var item in allItems)
        {
            if (purchased.Contains(item.Id) && item.Effect.IncomeMultiplier > 0)
            {
                multiplier += item.Effect.IncomeMultiplier;
            }
        }

        return multiplier;
    }

    /// <summary>
    /// Recalculates PermanentMultiplier in PrestigeData based on tier counts + shop bonuses.
    /// Called after buying shop items to keep the stored multiplier correct.
    /// Note: Shop income bonuses are NOT baked into PermanentMultiplier - they are added dynamically in GetPermanentMultiplier().
    /// </summary>
    private void RecalculatePermanentMultiplier()
    {
        // PermanentMultiplier bleibt wie es ist (Tier-Boni sind bereits eingerechnet).
        // Shop-Boni werden dynamisch in GetPermanentMultiplier() addiert.
        // Hier nichts zu tun - Methode existiert fuer zukuenftige Erweiterungen.
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

        // === RESET: Buildings ===
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

        // === RESET: Daily Rewards ===
        state.DailyRewardStreak = 0;
        state.LastDailyRewardClaim = DateTime.MinValue;

        // === PRESERVED (nicht angefasst): ===
        // - state.Prestige (PrestigeData mit Punkten, Shop-Items, Tier-Counts)
        // - state.UnlockedAchievements
        // - state.IsPremium
        // - state.TutorialCompleted, state.TutorialStep
        // - state.TotalMoneyEarned
        // - state.TotalPlayTimeSeconds
        // - state.SoundEnabled, state.MusicEnabled, state.HapticsEnabled, state.Language
        // - state.CreatedAt

        // Gold prestige preserves shop items (already in PrestigeData.PurchasedShopItems,
        // which is not touched by reset). Nothing extra needed here.
    }
}
