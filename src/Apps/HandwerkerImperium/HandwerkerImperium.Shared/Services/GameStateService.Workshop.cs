using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Workshop-Operationen: Upgrade, Bulk-Upgrade, Worker-Einstellung, Freischaltung.
/// </summary>
public sealed partial class GameStateService
{
    // ===================================================================
    // WERKSTATT-OPERATIONEN
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
}
