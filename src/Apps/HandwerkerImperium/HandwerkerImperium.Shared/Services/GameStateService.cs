using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Central service for managing the game state.
/// Thread-safe for access from UI thread and GameLoopService timer.
/// </summary>
public sealed class GameStateService : IGameStateService
{
    private GameState _state = new();
    private readonly object _stateLock = new();

    public GameState State => _state;
    public bool IsInitialized { get; private set; }

    // Lazy-Resolution für zirkuläre Dependencies (gesetzt in App.axaml.cs nach DI-Aufbau)
    public IChallengeConstraintService? ChallengeConstraints { get; set; }

    // Automation Level-Gates (zentral, vermeidet Duplikation in ViewModels)
    // Nach dem ersten Prestige sind alle Features permanent freigeschaltet
    private bool HasEverPrestiged => _state.Prestige.TotalPrestigeCount > 0;
    public bool IsAutoCollectUnlocked => HasEverPrestiged || _state.PlayerLevel >= LevelThresholds.AutoCollect;
    public bool IsAutoAcceptUnlocked => HasEverPrestiged || _state.PlayerLevel >= LevelThresholds.AutoAccept;
    public bool IsAutoAssignUnlocked => HasEverPrestiged || _state.PlayerLevel >= LevelThresholds.AutoAssign;

    // Gecachte Prestige-Shop-Boni (GS, XP, OrderReward) - werden bei Kauf/Prestige/StateLoad invalidiert
    private bool _prestigeBonusCacheDirty = true;
    private decimal _cachedGoldenScrewBonus;
    private decimal _cachedXpBonus;
    private decimal _cachedOrderRewardBonus;

    /// <summary>
    /// Prestige-Shop-Bonus-Cache invalidieren (nach Prestige-Shop-Kauf, Prestige-Reset oder State-Load).
    /// Feuert PrestigeShopPurchased Event für abhängige Services (z.B. CraftingService).
    /// </summary>
    public void InvalidatePrestigeBonusCache()
    {
        _prestigeBonusCacheDirty = true;
        PrestigeShopPurchased?.Invoke(this, EventArgs.Empty);
    }

    // Events
    public event EventHandler? PrestigeShopPurchased;
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
        lock (_stateLock)
        {
            _state = loadedState ?? GameState.CreateNew();
        }

        IsInitialized = true;
        _prestigeBonusCacheDirty = true;
        StateLoaded?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        lock (_stateLock)
        {
            _state = GameState.CreateNew();
        }
        _prestigeBonusCacheDirty = true;
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
            _state.CurrentRunMoney += amount;
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

    /// <summary>
    /// Externer GS-Bonus-Provider (z.B. Ascension Golden-Era-Perk).
    /// Wird nach DI-Auflösung gesetzt, um zirkuläre Dependencies zu vermeiden.
    /// Gibt den Bonus als Dezimalwert zurück (z.B. 0.2 = +20%).
    /// </summary>
    public Func<decimal>? ExternalGoldenScrewBonusProvider { get; set; }

    public void AddGoldenScrews(int amount, bool fromPurchase = false)
    {
        if (amount <= 0) return;

        // Prestige-Shop GoldenScrewBonus anwenden (z.B. +25%) - nur für Gameplay-Quellen
        if (!fromPurchase)
        {
            decimal bonus = GetGoldenScrewBonus();

            // Ascension Golden-Era-Perk: GS-Verdienst-Bonus (stackt additiv mit Prestige-Shop)
            decimal ascensionBonus = ExternalGoldenScrewBonusProvider?.Invoke() ?? 0m;
            bonus += ascensionBonus;

            if (bonus > 0)
                amount = (int)Math.Ceiling(amount * (1m + bonus));

            // Premium: +100% Goldschrauben aus Gameplay-Quellen (Mini-Games, Challenges, Meilensteine etc.)
            if (_state.IsPremium)
                amount *= 2;
        }

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
    /// Gibt den gecachten Goldschrauben-Bonus zurück (refresht bei Bedarf).
    /// </summary>
    private decimal GetGoldenScrewBonus()
    {
        RefreshPrestigeBonusCacheIfNeeded();
        return _cachedGoldenScrewBonus;
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
            // Bei Max-Level keine XP mehr addieren (verhindert int.MaxValue Overflow)
            if (_state.PlayerLevel >= LevelThresholds.MaxPlayerLevel)
                return;

            oldLevel = _state.PlayerLevel;

            // XP-Boost aus DailyReward (2x)
            if (_state.IsXpBoostActive)
                amount *= 2;

            // Prestige-Shop XP-Multiplikator
            var xpBonus = GetPrestigeXpBonus();
            if (xpBonus > 0)
                amount = (int)(amount * (1m + xpBonus));

            _state.CurrentXp += amount;
            _state.TotalXp += amount;

            levelUps = 0;
            while (_state.CurrentXp >= _state.XpForNextLevel && _state.PlayerLevel < LevelThresholds.MaxPlayerLevel)
            {
                _state.PlayerLevel++;
                levelUps++;
            }

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

    /// <summary>
    /// Gibt den gecachten XP-Bonus zurück (refresht bei Bedarf).
    /// </summary>
    private decimal GetPrestigeXpBonus()
    {
        RefreshPrestigeBonusCacheIfNeeded();
        return _cachedXpBonus;
    }

    /// <summary>
    /// Berechnet alle Prestige-Shop-Boni (GS, XP, OrderReward) in einem einzigen Durchlauf und cacht sie.
    /// Wird nur bei Dirty-Flag neu berechnet (nach Kauf, Prestige, StateLoad).
    /// </summary>
    private void RefreshPrestigeBonusCacheIfNeeded()
    {
        if (!_prestigeBonusCacheDirty) return;
        _prestigeBonusCacheDirty = false;

        _cachedGoldenScrewBonus = 0m;
        _cachedXpBonus = 0m;
        _cachedOrderRewardBonus = 0m;

        var purchased = _state.Prestige.PurchasedShopItems;
        var repeatableCounts = _state.Prestige.RepeatableItemCounts;
        if (purchased.Count == 0 && repeatableCounts.Count == 0) return;

        var allItems = PrestigeShop.GetAllItems();
        for (int i = 0; i < allItems.Count; i++)
        {
            var item = allItems[i];

            // Wiederholbare Items: Effekt * Kaufanzahl
            if (item.IsRepeatable)
            {
                if (repeatableCounts.TryGetValue(item.Id, out var count) && count > 0)
                {
                    if (item.Effect.OrderRewardBonus > 0)
                        _cachedOrderRewardBonus += item.Effect.OrderRewardBonus * count;
                }
                continue;
            }

            if (!purchased.Contains(item.Id)) continue;

            if (item.Effect.GoldenScrewBonus > 0)
                _cachedGoldenScrewBonus += item.Effect.GoldenScrewBonus;
            if (item.Effect.XpMultiplier > 0)
                _cachedXpBonus += item.Effect.XpMultiplier;
            if (item.Effect.OrderRewardBonus > 0)
                _cachedOrderRewardBonus += item.Effect.OrderRewardBonus;
        }
    }

    // ===================================================================
    // WORKSHOP OPERATIONS
    // ===================================================================

    public Workshop? GetWorkshop(WorkshopType type)
    {
        // For-Schleife statt LINQ FirstOrDefault (vermeidet Enumerator+Closure-Allokation)
        var workshops = _state.Workshops;
        for (int i = 0; i < workshops.Count; i++)
        {
            if (workshops[i].Type == type)
                return workshops[i];
        }
        return null;
    }

    public bool TryUpgradeWorkshop(WorkshopType type)
    {
        int oldLevel;
        int newLevel;
        decimal cost;
        decimal moneyBefore;
        decimal moneyAfter;

        lock (_stateLock)
        {
            var workshop = GetWorkshop(type);
            if (workshop == null || !workshop.CanUpgrade)
                return false;

            cost = workshop.UpgradeCost;

            // Challenge: Inflationszeit verdoppelt Upgrade-Kosten
            var challengeMultiplier = ChallengeConstraints?.GetUpgradeCostMultiplier() ?? 1.0m;
            if (challengeMultiplier > 1.0m)
                cost = Math.Round(cost * challengeMultiplier, 0);

            if (_state.Money < cost)
                return false;

            moneyBefore = _state.Money;
            _state.Money -= cost;
            _state.TotalMoneySpent += cost;
            moneyAfter = _state.Money;

            oldLevel = workshop.Level;
            workshop.Level++;
            newLevel = workshop.Level;
            _state.InvalidateIncomeCache();
        }

        MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(moneyBefore, moneyAfter));
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
        decimal moneyAfter = 0;

        lock (_stateLock)
        {
            var workshop = GetWorkshop(type);
            if (workshop == null) return 0;

            oldLevel = workshop.Level;
            moneyBefore = _state.Money;
            int maxUpgrades = count == 0 ? Workshop.MaxLevel - workshop.Level : count;

            // Challenge: Inflationszeit verdoppelt Upgrade-Kosten (identisch mit TryUpgradeWorkshop)
            var challengeMultiplier = ChallengeConstraints?.GetUpgradeCostMultiplier() ?? 1.0m;

            for (int i = 0; i < maxUpgrades; i++)
            {
                if (!workshop.CanUpgrade) break;

                var cost = workshop.UpgradeCost;
                if (challengeMultiplier > 1.0m)
                    cost = Math.Round(cost * challengeMultiplier, 0);
                if (_state.Money < cost) break;

                _state.Money -= cost;
                _state.TotalMoneySpent += cost;
                totalCost += cost;
                workshop.Level++;
                upgraded++;

                totalXp += 5 + workshop.Level / 10;
            }

            newLevel = workshop.Level;
            moneyAfter = _state.Money;
            _state.InvalidateIncomeCache();
        }

        if (upgraded > 0)
        {
            MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(moneyBefore, moneyAfter));
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
        decimal moneyBefore;
        decimal moneyAfter;

        lock (_stateLock)
        {
            var workshop = GetWorkshop(type);
            if (workshop == null || !workshop.CanHireWorker)
                return false;

            // Challenge: Spartaner begrenzt Worker auf 3
            int maxWorkers = ChallengeConstraints?.GetMaxWorkers(workshop.MaxWorkers) ?? workshop.MaxWorkers;
            if (workshop.Workers.Count >= maxWorkers)
                return false;

            cost = workshop.HireWorkerCost;
            if (_state.Money < cost)
                return false;

            moneyBefore = _state.Money;
            _state.Money -= cost;
            _state.TotalMoneySpent += cost;
            moneyAfter = _state.Money;

            worker = Worker.CreateRandom();
            workshop.Workers.Add(worker);
            workerCount = workshop.Workers.Count;
            _state.InvalidateIncomeCache();
        }

        MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(moneyBefore, moneyAfter));
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

            // Challenge: SoloMeister blockiert Workshop-Unlock wenn schon 1 vorhanden
            if (ChallengeConstraints?.IsWorkshopUnlockBlocked(_state.UnlockedWorkshopTypes.Count) == true)
                return false;

            return true;
        }
    }

    public bool TryPurchaseWorkshop(WorkshopType type, decimal costOverride = -1)
    {
        decimal cost;
        decimal moneyBefore;
        decimal moneyAfter;
        lock (_stateLock)
        {
            if (!CanPurchaseWorkshop(type)) return false;

            cost = costOverride >= 0 ? costOverride : type.GetUnlockCost();
            if (_state.Money < cost) return false;

            moneyBefore = _state.Money;
            _state.Money -= cost;
            _state.TotalMoneySpent += cost;
            moneyAfter = _state.Money;
            _state.UnlockedWorkshopTypes.Add(type);

            var workshop = _state.GetOrCreateWorkshop(type);
            workshop.IsUnlocked = true;
            _state.InvalidateIncomeCache();
        }

        if (cost > 0)
            MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(moneyBefore, moneyAfter));

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
            // Auftrags-spezifisch: Task-Ergebnis nur bei aktivem Auftrag (nicht bei QuickJobs)
            var order = _state.ActiveOrder;
            if (order != null)
                order.RecordTaskResult(rating);

            // Statistiken IMMER aktualisieren (auch bei QuickJobs)
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

        // Event IMMER feuern (DailyChallengeService, WeeklyMissions, QuickJob-Validierung)
        MiniGameResultRecorded?.Invoke(this, new MiniGameResultRecordedEventArgs(rating));
    }

    public decimal GetOrderRewardMultiplier(Order order)
    {
        lock (_stateLock)
        {
            return CalculateOrderRewardMultiplierUnlocked(order);
        }
    }

    /// <summary>
    /// Interne Berechnung ohne Lock - nur innerhalb bestehender lock(_stateLock)-Blöcke aufrufen.
    /// </summary>
    private decimal CalculateOrderRewardMultiplierUnlocked(Order order)
    {
        decimal multiplier = 1m;

        // Research-RewardMultiplier (For-Schleife statt LINQ Where+Sum)
        decimal researchRewardBonus = 0m;
        for (int i = 0; i < _state.Researches.Count; i++)
        {
            var r = _state.Researches[i];
            if (r.IsResearched && r.Effect.RewardMultiplier > 0)
                researchRewardBonus += r.Effect.RewardMultiplier;
        }
        if (researchRewardBonus > 0)
            multiplier *= (1m + researchRewardBonus);

        // VehicleFleet-Gebäude: Auftragsbelohnungs-Bonus
        var vehicleFleet = _state.GetBuilding(BuildingType.VehicleFleet);
        if (vehicleFleet != null && vehicleFleet.OrderRewardBonus > 0)
            multiplier *= (1m + vehicleFleet.OrderRewardBonus);

        // Reputation-Multiplikator: Höhere Reputation → bessere Belohnungen
        multiplier *= _state.Reputation.ReputationMultiplier;

        // Event-RewardMultiplier (HighDemand 1.5x, EconomicDownturn 0.7x)
        var activeEvent = _state.ActiveEvent;
        if (activeEvent?.IsActive == true && activeEvent.Effect.RewardMultiplier != 1.0m)
        {
            // AffectedWorkshop: Nur anwenden wenn Workshop-Typ passt oder kein spezifischer Typ gesetzt
            if (activeEvent.Effect.AffectedWorkshop == null ||
                activeEvent.Effect.AffectedWorkshop == order.WorkshopType)
            {
                multiplier *= activeEvent.Effect.RewardMultiplier;
            }
        }

        // Stammkunden-Bonus
        if (order.IsRegularCustomerOrder)
        {
            RegularCustomer? customer = null;
            for (int i = 0; i < _state.Reputation.RegularCustomers.Count; i++)
            {
                if (_state.Reputation.RegularCustomers[i].Id == order.CustomerId)
                {
                    customer = _state.Reputation.RegularCustomers[i];
                    break;
                }
            }
            if (customer != null)
                multiplier *= customer.BonusMultiplier;
        }

        // Prestige-Shop: Auftragsbelohnungs-Bonus (wiederholbar pp_order_reward_rep)
        decimal shopOrderBonus = GetPrestigeShopOrderRewardBonus();
        if (shopOrderBonus > 0)
            multiplier *= (1m + shopOrderBonus);

        // Soft-Cap: Diminishing Returns auf den Gesamt-Multiplikator
        // Verhindert Multiplikator-Explosion bei voll ausgebauten Spielern
        decimal cap = GameBalanceConstants.OrderRewardMultiplierSoftCap;
        if (multiplier > cap)
            multiplier = cap + (decimal)Math.Sqrt((double)(multiplier - cap));

        return multiplier;
    }

    /// <summary>
    /// Gibt den gecachten Auftragsbelohnungs-Bonus zurück (refresht bei Bedarf).
    /// Cap bei +100%.
    /// </summary>
    private decimal GetPrestigeShopOrderRewardBonus()
    {
        RefreshPrestigeBonusCacheIfNeeded();
        return Math.Min(_cachedOrderRewardBonus, 1.0m);
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

            // Prestige-Multiplikator ist bereits in BaseReward enthalten
            // (via NetIncomePerSecond in OrderGeneratorService), daher NICHT nochmal anwenden
            moneyReward = order.FinalReward * CalculateOrderRewardMultiplierUnlocked(order);
            xpReward = order.FinalXp;

            // Combo-Multiplikator (PaintingGame)
            if (order.ComboMultiplier > 1m)
            {
                moneyReward *= order.ComboMultiplier;
                xpReward = (int)(xpReward * order.ComboMultiplier);
            }

            // Rewarded-Ad-Verdopplung
            if (order.IsScoreDoubled)
            {
                moneyReward *= 2m;
                xpReward *= 2;
            }

            var workshop = GetWorkshop(order.WorkshopType);
            if (workshop != null)
            {
                workshop.TotalEarned += moneyReward;
                workshop.OrdersCompleted++;
            }

            _state.TotalOrdersCompleted++;

            if (order.TaskResults.Count > 0)
            {
                int ratingSum = 0;
                for (int i = 0; i < order.TaskResults.Count; i++)
                    ratingSum += (int)order.TaskResults[i];
                avgRating = (MiniGameRating)(int)Math.Round((double)ratingSum / order.TaskResults.Count);
            }
            else
            {
                avgRating = MiniGameRating.Ok;
            }

            // Reputation-System: Bewertung basierend auf MiniGame-Leistung
            int stars = avgRating switch
            {
                MiniGameRating.Perfect => 5,
                MiniGameRating.Good => 4,
                MiniGameRating.Ok => 3,
                _ => 2
            };
            _state.Reputation.AddRating(stars);

            // Stammkunden-Tracking bei Perfect Rating
            if (avgRating == MiniGameRating.Perfect && !string.IsNullOrEmpty(order.CustomerName))
            {
                RegularCustomer? existingCustomer = null;
                for (int i = 0; i < _state.Reputation.RegularCustomers.Count; i++)
                {
                    if (_state.Reputation.RegularCustomers[i].Name == order.CustomerName)
                    {
                        existingCustomer = _state.Reputation.RegularCustomers[i];
                        break;
                    }
                }
                if (existingCustomer != null)
                {
                    existingCustomer.PerfectOrderCount++;
                    existingCustomer.LastOrder = DateTime.UtcNow;
                    // BonusMultiplier: 1.1 Basis + 0.02 pro Perfect über 5 (Cap 1.5)
                    if (existingCustomer.PerfectOrderCount > 5)
                    {
                        existingCustomer.BonusMultiplier = Math.Min(1.5m,
                            1.1m + (existingCustomer.PerfectOrderCount - 5) * 0.02m);
                    }
                }
                else
                {
                    // Neuen Stammkunden anlegen
                    _state.Reputation.RegularCustomers.Add(new RegularCustomer
                    {
                        Name = order.CustomerName,
                        PreferredWorkshop = order.WorkshopType,
                        PerfectOrderCount = 1,
                        LastOrder = DateTime.UtcNow,
                        AvatarSeed = order.CustomerAvatarSeed
                    });
                    // Max 20 Stammkunden (älteste entfernen)
                    while (_state.Reputation.RegularCustomers.Count > 20)
                        _state.Reputation.RegularCustomers.RemoveAt(0);
                }
            }

            _state.ActiveOrder = null;
        }

        // Grant rewards (these have their own locks)
        AddMoney(moneyReward);
        AddXp(xpReward);

        OrderCompleted?.Invoke(this, new OrderCompletedEventArgs(
            order, moneyReward, xpReward, avgRating));
    }

    // ===================================================================
    // MINI-GAME AUTO-COMPLETE
    // ===================================================================

    public void RecordPerfectRating(MiniGameType type)
    {
        lock (_stateLock)
        {
            int key = (int)type;
            if (_state.PerfectRatingCounts.TryGetValue(key, out int count))
                _state.PerfectRatingCounts[key] = count + 1;
            else
                _state.PerfectRatingCounts[key] = 1;
        }
    }

    public bool CanAutoComplete(MiniGameType type, bool isPremium)
    {
        // Differenzierte Schwellen: Puzzle/Memory-Spiele sind schwerer → weniger Perfects nötig
        int baseThreshold = type switch
        {
            MiniGameType.PipePuzzle or MiniGameType.Blueprint or MiniGameType.InventGame
                or MiniGameType.DesignPuzzle or MiniGameType.Inspection => 20,
            _ => 30
        };
        int threshold = isPremium ? baseThreshold / 2 : baseThreshold;

        lock (_stateLock)
        {
            return _state.PerfectRatingCounts.TryGetValue((int)type, out int count) && count >= threshold;
        }
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

    public decimal CompleteMaterialOrder(Order order)
    {
        if (order.OrderType != OrderType.MaterialOrder || order.RequiredMaterials == null)
            return 0m;

        decimal reward;
        int xpReward;

        lock (_stateLock)
        {
            // Prüfen ob alle Items vorhanden
            foreach (var (productId, required) in order.RequiredMaterials)
            {
                int available = _state.CraftingInventory.GetValueOrDefault(productId, 0);
                if (available < required) return 0m;
            }

            // Items abziehen
            foreach (var (productId, required) in order.RequiredMaterials)
            {
                _state.CraftingInventory[productId] -= required;
                if (_state.CraftingInventory[productId] <= 0)
                    _state.CraftingInventory.Remove(productId);
            }

            // Belohnung berechnen (innerhalb Lock, da CalculateOrderRewardMultiplierUnlocked State liest)
            reward = order.EstimatedReward * CalculateOrderRewardMultiplierUnlocked(order);
            // MaterialOrders haben keine TaskResults → XP direkt aus BaseXp + Difficulty + OrderType
            xpReward = (int)(order.BaseXp * order.Difficulty.GetXpMultiplier() * OrderType.MaterialOrder.GetXpMultiplier());

            // Statistiken
            _state.TotalOrdersCompleted++;
            _state.MaterialOrdersCompletedToday++;
            _state.TotalMaterialOrdersCompleted++;
            var workshop = GetWorkshop(order.WorkshopType);
            if (workshop != null)
            {
                workshop.TotalEarned += reward;
                workshop.OrdersCompleted++;
            }

            // Order aus AvailableOrders entfernen
            _state.AvailableOrders.Remove(order);
        }

        // Geld + XP gutschreiben + Events AUSSERHALB des Locks
        // (AddMoney/AddXp nehmen eigene Locks und feuern Events die Event-Handler aufrufen)
        AddMoney(reward);
        AddXp(xpReward);

        OrderCompleted?.Invoke(this, new OrderCompletedEventArgs(order, reward, xpReward, MiniGameRating.Good));

        return reward;
    }
}
