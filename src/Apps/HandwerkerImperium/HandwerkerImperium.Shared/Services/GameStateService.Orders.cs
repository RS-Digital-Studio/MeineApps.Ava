using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Auftrags-Operationen, Mini-Game-Ergebnisse, Lieferaufträge,
/// Lock-Zugriff und Event-Raiser für externe Services.
/// </summary>
public sealed partial class GameStateService
{
    // ===================================================================
    // AUFTRAGS-OPERATIONEN
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
            _state.Statistics.TotalMiniGamesPlayed++;

            if (rating == MiniGameRating.Perfect)
            {
                _state.Statistics.PerfectRatings++;
                _state.Statistics.PerfectStreak++;
                if (_state.Statistics.PerfectStreak > _state.Statistics.BestPerfectStreak)
                {
                    _state.Statistics.BestPerfectStreak = _state.Statistics.PerfectStreak;
                }
            }
            else
            {
                _state.Statistics.PerfectStreak = 0;
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

            _state.Statistics.TotalOrdersCompleted++;

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
            // Research ReputationBonus direkt aus State berechnen (GameStateService hat keinen IResearchService)
            decimal reputationBonus = 0m;
            for (int ri = 0; ri < _state.Researches.Count; ri++)
            {
                if (_state.Researches[ri].IsResearched && _state.Researches[ri].Effect?.ReputationBonus > 0)
                    reputationBonus += _state.Researches[ri].Effect!.ReputationBonus;
            }
            _state.Reputation.AddRating(stars, reputationBonus);

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

    // ===================================================================
    // LIEFERAUFTRÄGE (MaterialOrder)
    // ===================================================================

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
            _state.Statistics.TotalOrdersCompleted++;
            _state.Statistics.MaterialOrdersCompletedToday++;
            _state.Statistics.TotalMaterialOrdersCompleted++;
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

    // ===================================================================
    // LOCK-ZUGRIFF (für OperationServices)
    // ===================================================================

    /// <summary>
    /// Führt eine Aktion unter dem State-Lock aus.
    /// </summary>
    public void ExecuteWithLock(Action action)
    {
        lock (_stateLock)
        {
            action();
        }
    }

    /// <summary>
    /// Führt eine Funktion unter dem State-Lock aus und gibt das Ergebnis zurück.
    /// </summary>
    public T ExecuteWithLock<T>(Func<T> func)
    {
        lock (_stateLock)
        {
            return func();
        }
    }

    // ===================================================================
    // EVENT-RAISER (für OperationServices)
    // ===================================================================

    /// <summary>
    /// Feuert das WorkshopUpgraded-Event mit MoneyChanged.
    /// </summary>
    public void RaiseWorkshopUpgraded(WorkshopType type, int oldLevel, int newLevel, decimal cost, decimal moneyBefore, decimal moneyAfter)
    {
        MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(moneyBefore, moneyAfter));
        WorkshopUpgraded?.Invoke(this, new WorkshopUpgradedEventArgs(type, oldLevel, newLevel, cost));
    }

    /// <summary>
    /// Feuert das WorkerHired-Event mit MoneyChanged.
    /// </summary>
    public void RaiseWorkerHired(WorkshopType type, Worker worker, decimal cost, int workerCount, decimal moneyBefore, decimal moneyAfter)
    {
        MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(moneyBefore, moneyAfter));
        WorkerHired?.Invoke(this, new WorkerHiredEventArgs(type, worker, cost, workerCount));
    }

    /// <summary>
    /// Feuert das OrderCompleted-Event.
    /// </summary>
    public void RaiseOrderCompleted(Order order, decimal moneyReward, int xpReward, MiniGameRating avgRating)
    {
        OrderCompleted?.Invoke(this, new OrderCompletedEventArgs(order, moneyReward, xpReward, avgRating));
    }

    /// <summary>
    /// Feuert das MiniGameResultRecorded-Event.
    /// </summary>
    public void RaiseMiniGameResultRecorded(MiniGameRating rating)
    {
        MiniGameResultRecorded?.Invoke(this, new MiniGameResultRecordedEventArgs(rating));
    }

    /// <summary>
    /// Feuert das MoneyChanged-Event.
    /// </summary>
    public void RaiseMoneyChanged(decimal oldAmount, decimal newAmount)
    {
        MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(oldAmount, newAmount));
    }
}
