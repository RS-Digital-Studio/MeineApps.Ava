using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Central service for managing the game state.
/// Thread-safe for access from UI thread and GameLoopService timer.
/// </summary>
public class GameStateService : IGameStateService
{
    private GameState _state = new();
    private readonly object _stateLock = new();

    public GameState State => _state;
    public bool IsInitialized { get; private set; }

    // Events
    public event EventHandler<MoneyChangedEventArgs>? MoneyChanged;
    public event EventHandler<LevelUpEventArgs>? LevelUp;
    public event EventHandler<XpGainedEventArgs>? XpGained;
    public event EventHandler<WorkshopUpgradedEventArgs>? WorkshopUpgraded;
    public event EventHandler<WorkerHiredEventArgs>? WorkerHired;
    public event EventHandler<OrderCompletedEventArgs>? OrderCompleted;
    public event EventHandler? StateLoaded;
    public event EventHandler<GoldenScrewsChangedEventArgs>? GoldenScrewsChanged;
    public event EventHandler<MiniGameResultRecordedEventArgs>? MiniGameResultRecorded;

    // ===================================================================
    // INITIALIZATION
    // ===================================================================

    public void Initialize(GameState? loadedState = null)
    {
        if (loadedState != null)
        {
            _state = loadedState;
        }
        else
        {
            _state = GameState.CreateNew();
        }

        IsInitialized = true;
        StateLoaded?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        lock (_stateLock)
        {
            _state = GameState.CreateNew();
        }
        StateLoaded?.Invoke(this, EventArgs.Empty);
    }

    public void MarkDirty()
    {
        // Intentional no-op kept for interface compatibility
    }

    // ===================================================================
    // MONEY OPERATIONS (Thread-safe)
    // ===================================================================

    public void AddMoney(decimal amount)
    {
        if (amount <= 0) return;

        decimal oldAmount;
        decimal newAmount;

        lock (_stateLock)
        {
            oldAmount = _state.Money;
            _state.Money += amount;
            _state.TotalMoneyEarned += amount;
            newAmount = _state.Money;
        }

        MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(oldAmount, newAmount));
    }

    public bool TrySpendMoney(decimal amount)
    {
        decimal oldAmount;
        decimal newAmount;

        lock (_stateLock)
        {
            if (amount <= 0 || _state.Money < amount)
                return false;

            oldAmount = _state.Money;
            _state.Money -= amount;
            _state.TotalMoneySpent += amount;
            newAmount = _state.Money;
        }

        MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(oldAmount, newAmount));
        return true;
    }

    public bool CanAfford(decimal amount)
    {
        lock (_stateLock)
        {
            return _state.Money >= amount;
        }
    }

    // ===================================================================
    // GOLDEN SCREWS OPERATIONS (Thread-safe)
    // ===================================================================

    public void AddGoldenScrews(int amount)
    {
        if (amount <= 0) return;

        // Prestige-Shop GoldenScrewBonus anwenden (z.B. +25%)
        decimal bonus = GetGoldenScrewBonus();
        if (bonus > 0)
            amount = (int)Math.Ceiling(amount * (1m + bonus));

        int oldAmount;
        int newAmount;

        lock (_stateLock)
        {
            oldAmount = _state.GoldenScrews;
            _state.GoldenScrews += amount;
            _state.TotalGoldenScrewsEarned += amount;
            newAmount = _state.GoldenScrews;
        }

        GoldenScrewsChanged?.Invoke(this, new GoldenScrewsChangedEventArgs(oldAmount, newAmount));
    }

    /// <summary>
    /// Berechnet den Goldschrauben-Bonus aus gekauften Prestige-Shop-Items.
    /// </summary>
    private decimal GetGoldenScrewBonus()
    {
        var purchased = _state.Prestige.PurchasedShopItems;
        if (purchased.Count == 0) return 0m;

        decimal bonus = 0m;
        foreach (var item in PrestigeShop.GetAllItems())
        {
            if (purchased.Contains(item.Id) && item.Effect.GoldenScrewBonus > 0)
                bonus += item.Effect.GoldenScrewBonus;
        }
        return bonus;
    }

    public bool TrySpendGoldenScrews(int amount)
    {
        int oldAmount;
        int newAmount;

        lock (_stateLock)
        {
            if (amount <= 0 || _state.GoldenScrews < amount)
                return false;

            oldAmount = _state.GoldenScrews;
            _state.GoldenScrews -= amount;
            _state.TotalGoldenScrewsSpent += amount;
            newAmount = _state.GoldenScrews;
        }

        GoldenScrewsChanged?.Invoke(this, new GoldenScrewsChangedEventArgs(oldAmount, newAmount));
        return true;
    }

    public bool CanAffordGoldenScrews(int amount)
    {
        lock (_stateLock)
        {
            return _state.GoldenScrews >= amount;
        }
    }

    // ===================================================================
    // XP/LEVEL OPERATIONS
    // ===================================================================

    public void AddXp(int amount)
    {
        if (amount <= 0) return;

        int oldLevel;
        int levelUps;
        int totalXp, currentXp, xpForNext, newLevel;

        lock (_stateLock)
        {
            oldLevel = _state.PlayerLevel;
            levelUps = _state.AddXp(amount);
            totalXp = _state.TotalXp;
            currentXp = _state.CurrentXp;
            xpForNext = _state.XpForNextLevel;
            newLevel = _state.PlayerLevel;
        }

        XpGained?.Invoke(this, new XpGainedEventArgs(amount, totalXp, currentXp, xpForNext));

        if (levelUps > 0)
        {
            var newlyUnlocked = new List<WorkshopType>();
            foreach (WorkshopType type in Enum.GetValues<WorkshopType>())
            {
                int unlockLevel = type.GetUnlockLevel();
                if (unlockLevel > oldLevel && unlockLevel <= newLevel)
                {
                    newlyUnlocked.Add(type);
                }
            }

            LevelUp?.Invoke(this, new LevelUpEventArgs(oldLevel, newLevel, newlyUnlocked));
        }
    }

    // ===================================================================
    // WORKSHOP OPERATIONS
    // ===================================================================

    public Workshop? GetWorkshop(WorkshopType type)
    {
        return _state.Workshops.FirstOrDefault(w => w.Type == type);
    }

    public bool TryUpgradeWorkshop(WorkshopType type)
    {
        int oldLevel;
        int newLevel;
        decimal cost;

        lock (_stateLock)
        {
            var workshop = GetWorkshop(type);
            if (workshop == null || !workshop.CanUpgrade)
                return false;

            cost = workshop.UpgradeCost;
            if (_state.Money < cost)
                return false;

            _state.Money -= cost;
            _state.TotalMoneySpent += cost;

            oldLevel = workshop.Level;
            workshop.Level++;
            newLevel = workshop.Level;
        }

        MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(_state.Money + cost, _state.Money));
        WorkshopUpgraded?.Invoke(this, new WorkshopUpgradedEventArgs(type, oldLevel, newLevel, cost));

        // XP für Workshop-Upgrade vergeben (5 + Level/10, skaliert mit Fortschritt)
        int xpReward = 5 + newLevel / 10;
        AddXp(xpReward);

        return true;
    }

    /// <summary>
    /// Upgradet einen Workshop mehrfach (Bulk Buy).
    /// Gibt die Anzahl der tatsächlich durchgeführten Upgrades zurück.
    /// Bei count=0 (Max): So viele Upgrades wie bezahlbar.
    /// </summary>
    public int TryUpgradeWorkshopBulk(WorkshopType type, int count)
    {
        int upgraded = 0;
        int totalXp = 0;
        int oldLevel = 0;
        int newLevel = 0;
        decimal totalCost = 0;
        decimal moneyBefore = 0;

        lock (_stateLock)
        {
            var workshop = GetWorkshop(type);
            if (workshop == null) return 0;

            oldLevel = workshop.Level;
            moneyBefore = _state.Money;
            int maxUpgrades = count == 0 ? Workshop.MaxLevel - workshop.Level : count;

            for (int i = 0; i < maxUpgrades; i++)
            {
                if (!workshop.CanUpgrade) break;

                var cost = workshop.UpgradeCost;
                if (_state.Money < cost) break;

                _state.Money -= cost;
                _state.TotalMoneySpent += cost;
                totalCost += cost;
                workshop.Level++;
                upgraded++;

                totalXp += 5 + workshop.Level / 10;
            }

            newLevel = workshop.Level;
        }

        if (upgraded > 0)
        {
            MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(moneyBefore, _state.Money));
            WorkshopUpgraded?.Invoke(this, new WorkshopUpgradedEventArgs(type, oldLevel, newLevel, totalCost));
            AddXp(totalXp);
        }

        return upgraded;
    }

    public bool TryHireWorker(WorkshopType type)
    {
        Worker worker;
        decimal cost;
        int workerCount;

        lock (_stateLock)
        {
            var workshop = GetWorkshop(type);
            if (workshop == null || !workshop.CanHireWorker)
                return false;

            cost = workshop.HireWorkerCost;
            if (_state.Money < cost)
                return false;

            _state.Money -= cost;
            _state.TotalMoneySpent += cost;

            worker = Worker.CreateRandom();
            workshop.Workers.Add(worker);
            workerCount = workshop.Workers.Count;
        }

        MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(_state.Money + cost, _state.Money));
        WorkerHired?.Invoke(this, new WorkerHiredEventArgs(type, worker, cost, workerCount));

        return true;
    }

    public bool IsWorkshopUnlocked(WorkshopType type)
    {
        return _state.IsWorkshopUnlocked(type);
    }

    public bool CanPurchaseWorkshop(WorkshopType type)
    {
        lock (_stateLock)
        {
            if (_state.UnlockedWorkshopTypes.Contains(type)) return false;
            if (_state.PlayerLevel < type.GetUnlockLevel()) return false;
            if (type.GetRequiredPrestige() > _state.Prestige.TotalPrestigeCount) return false;
            return true;
        }
    }

    public bool TryPurchaseWorkshop(WorkshopType type, decimal costOverride = -1)
    {
        decimal cost;
        lock (_stateLock)
        {
            if (!CanPurchaseWorkshop(type)) return false;

            cost = costOverride >= 0 ? costOverride : type.GetUnlockCost();
            if (_state.Money < cost) return false;

            _state.Money -= cost;
            _state.TotalMoneySpent += cost;
            _state.UnlockedWorkshopTypes.Add(type);

            var workshop = _state.GetOrCreateWorkshop(type);
            workshop.IsUnlocked = true;
        }

        if (cost > 0)
            MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(_state.Money + cost, _state.Money));

        // XP-Bonus für Workshop-Freischaltung
        AddXp(50);

        return true;
    }

    // ===================================================================
    // ORDER OPERATIONS
    // ===================================================================

    public void StartOrder(Order order)
    {
        lock (_stateLock)
        {
            _state.AvailableOrders.Remove(order);
            _state.ActiveOrder = order;
        }
    }

    public Order? GetActiveOrder()
    {
        return _state.ActiveOrder;
    }

    public void RecordMiniGameResult(MiniGameRating rating)
    {
        lock (_stateLock)
        {
            var order = _state.ActiveOrder;
            if (order == null) return;

            order.RecordTaskResult(rating);
            _state.TotalMiniGamesPlayed++;

            if (rating == MiniGameRating.Perfect)
            {
                _state.PerfectRatings++;
                _state.PerfectStreak++;
                if (_state.PerfectStreak > _state.BestPerfectStreak)
                {
                    _state.BestPerfectStreak = _state.PerfectStreak;
                }
            }
            else
            {
                _state.PerfectStreak = 0;
            }
        }

        // Event feuern damit DailyChallengeService MiniGame-Challenges tracken kann
        MiniGameResultRecorded?.Invoke(this, new MiniGameResultRecordedEventArgs(rating));
    }

    public void CompleteActiveOrder()
    {
        Order? order;
        decimal moneyReward;
        int xpReward;
        MiniGameRating avgRating;

        lock (_stateLock)
        {
            order = _state.ActiveOrder;
            if (order == null || !order.IsCompleted) return;

            moneyReward = order.FinalReward * _state.Prestige.PermanentMultiplier;

            // Research-RewardMultiplier anwenden
            decimal researchRewardBonus = _state.Researches
                .Where(r => r.IsResearched && r.Effect.RewardMultiplier > 0)
                .Sum(r => r.Effect.RewardMultiplier);
            if (researchRewardBonus > 0)
                moneyReward *= (1m + researchRewardBonus);

            // VehicleFleet-Gebäude: Auftragsbelohnungs-Bonus
            var vehicleFleet = _state.GetBuilding(BuildingType.VehicleFleet);
            if (vehicleFleet != null && vehicleFleet.OrderRewardBonus > 0)
                moneyReward *= (1m + vehicleFleet.OrderRewardBonus);

            // Reputation-Multiplikator: Höhere Reputation → bessere Belohnungen
            moneyReward *= _state.Reputation.ReputationMultiplier;

            xpReward = order.FinalXp;

            var workshop = GetWorkshop(order.WorkshopType);
            if (workshop != null)
            {
                workshop.TotalEarned += moneyReward;
                workshop.OrdersCompleted++;
            }

            _state.TotalOrdersCompleted++;

            avgRating = order.TaskResults.Count > 0
                ? (MiniGameRating)(int)Math.Round(order.TaskResults.Average(r => (int)r))
                : MiniGameRating.Ok;

            // Reputation-System: Bewertung basierend auf MiniGame-Leistung
            int stars = avgRating switch
            {
                MiniGameRating.Perfect => 5,
                MiniGameRating.Good => 4,
                MiniGameRating.Ok => 3,
                _ => 2
            };
            _state.Reputation.AddRating(stars);

            _state.ActiveOrder = null;
        }

        // Grant rewards (these have their own locks)
        AddMoney(moneyReward);
        AddXp(xpReward);

        OrderCompleted?.Invoke(this, new OrderCompletedEventArgs(
            order, moneyReward, xpReward, avgRating));
    }

    public void CancelActiveOrder()
    {
        lock (_stateLock)
        {
            if (_state.ActiveOrder == null) return;

            var order = _state.ActiveOrder;
            order.CurrentTaskIndex = 0;
            order.TaskResults.Clear();

            _state.AvailableOrders.Add(order);
            _state.ActiveOrder = null;
        }
    }
}
