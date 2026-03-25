using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Zentrale Sammlung aller Workshop-Berechnungsformeln.
/// Reine statische Methoden ohne Service-Abhängigkeiten — testbar und an einer Stelle änderbar.
/// Workshop-Model delegiert seine berechneten Properties an diese Klasse.
/// </summary>
public static class WorkshopFormulas
{
    /// <summary>
    /// Basis-Einkommen pro Worker pro Sekunde.
    /// Formel: 1 * 1.02^(Level-1) * TypeMultiplier * MilestoneMultiplier
    /// </summary>
    public static decimal CalculateBaseIncomePerWorker(int level, WorkshopType type)
    {
        decimal baseIncome = (decimal)Math.Pow(GameBalanceConstants.IncomeBaseMultiplier, level - 1);
        return baseIncome * type.GetBaseIncomeMultiplier() * CalculateMilestoneMultiplier(level);
    }

    /// <summary>
    /// Brutto-Einkommen pro Sekunde für einen Workshop (alle Worker, Aura, Rebirth).
    /// </summary>
    public static decimal CalculateGrossIncome(int level, WorkshopType type, List<Worker> workers,
        decimal levelResistanceBonus, decimal rebirthIncomeBonus)
    {
        if (workers.Count == 0) return 0;

        decimal baseIncomePerWorker = CalculateBaseIncomePerWorker(level, type);
        decimal totalIncome = 0m;
        for (int i = 0; i < workers.Count; i++)
        {
            var w = workers[i];
            totalIncome += baseIncomePerWorker * w.EffectiveEfficiency
                * CalculateLevelFitFactor(level, w.Tier.GetLevelResistance(), levelResistanceBonus);
        }

        // Worker-Aura: S-Tier+ Worker geben passiven Einkommens-Bonus
        decimal auraBonus = 0m;
        for (int i = 0; i < workers.Count; i++)
            auraBonus += workers[i].Tier.GetAuraBonus();
        if (auraBonus > 0)
            totalIncome *= (1m + auraBonus);

        // Rebirth-Sterne Einkommens-Bonus
        if (rebirthIncomeBonus > 0)
            totalIncome *= (1m + rebirthIncomeBonus);

        return totalIncome;
    }

    /// <summary>
    /// Level-Anforderungsfaktor: Höhere Workshop-Level → niedrige Tiers verlieren Effizienz.
    /// Basis: -2% alle 30 Level, reduziert durch Tier-Resistenz + Forschung.
    /// Minimum 20% (Worker wird nie komplett nutzlos).
    /// </summary>
    public static decimal CalculateLevelFitFactor(int workshopLevel, decimal tierResistance, decimal levelResistanceBonus)
    {
        if (workshopLevel <= 30) return 1.0m;

        int steps = workshopLevel / GameBalanceConstants.LevelPenaltyStep;
        decimal basePenalty = steps * GameBalanceConstants.LevelPenaltyPerStep;
        decimal totalResistance = Math.Min(1.0m, tierResistance + levelResistanceBonus);
        decimal adjustedPenalty = basePenalty * (1m - totalResistance);
        return Math.Max(GameBalanceConstants.MinLevelFitFactor, 1m - adjustedPenalty);
    }

    /// <summary>
    /// Multiplikator-Meilensteine (AdVenture-Capitalist "Bumpy Progression"-Pattern).
    /// Kumulativ: ~84.6x bei Level 1000.
    /// </summary>
    public static decimal CalculateMilestoneMultiplier(int level)
    {
        decimal mult = 1.0m;
        foreach (var (milestoneLevel, multiplier) in GameBalanceConstants.MilestoneMultipliers)
        {
            if (level >= milestoneLevel) mult *= multiplier;
        }
        return mult;
    }

    /// <summary>Gibt den Einzel-Multiplikator für ein bestimmtes Meilenstein-Level zurück.</summary>
    public static decimal GetMilestoneMultiplierForLevel(int level)
    {
        foreach (var (milestoneLevel, multiplier) in GameBalanceConstants.MilestoneMultipliers)
        {
            if (level == milestoneLevel) return multiplier;
        }
        return 1.0m;
    }

    /// <summary>Prüft ob ein Level ein Meilenstein ist.</summary>
    public static bool IsMilestoneLevel(int level)
    {
        foreach (var (milestoneLevel, _) in GameBalanceConstants.MilestoneMultipliers)
        {
            if (level == milestoneLevel) return true;
        }
        return false;
    }

    /// <summary>Roh-Kosten für ein einzelnes Level (ohne Rabatte).</summary>
    private static decimal CalculateRawLevelCost(int level) => level == 1
        ? GameBalanceConstants.UpgradeCostLevel1
        : GameBalanceConstants.UpgradeCostBase * (decimal)Math.Pow(GameBalanceConstants.UpgradeCostExponent, level - 1);

    /// <summary>
    /// Upgrade-Kosten mit allen Rabatten (Rebirth, Prestige-Shop, VIP).
    /// </summary>
    public static decimal CalculateUpgradeCost(int level, decimal rebirthDiscount, decimal prestigeDiscount, decimal vipReduction)
    {
        if (level >= Workshop.MaxLevel) return 0;
        decimal baseCost = CalculateRawLevelCost(level);

        if (rebirthDiscount > 0)
            baseCost *= (1m - rebirthDiscount);
        if (prestigeDiscount > 0)
            baseCost *= (1m - Math.Min(prestigeDiscount, GameBalanceConstants.PrestigeDiscountCap));
        if (vipReduction > 0)
            baseCost *= (1m - vipReduction);

        return baseCost;
    }

    /// <summary>
    /// Kombinierter Rabattfaktor aus Rebirth + Prestige + VIP.
    /// </summary>
    public static decimal CalculateDiscountFactor(decimal rebirthDiscount, decimal prestigeDiscount, decimal vipReduction)
    {
        decimal factor = 1m;
        if (rebirthDiscount > 0) factor *= (1m - rebirthDiscount);
        if (prestigeDiscount > 0) factor *= (1m - Math.Min(prestigeDiscount, GameBalanceConstants.PrestigeDiscountCap));
        if (vipReduction > 0) factor *= (1m - vipReduction);
        return factor;
    }

    /// <summary>
    /// Bulk-Upgrade Gesamtkosten für N Level ab currentLevel.
    /// </summary>
    public static decimal CalculateBulkUpgradeCost(int currentLevel, int count, decimal discountFactor)
    {
        if (count <= 0 || currentLevel >= Workshop.MaxLevel) return 0;
        decimal total = 0;
        int maxUpgrades = Math.Min(count, Workshop.MaxLevel - currentLevel);
        for (int i = 0; i < maxUpgrades; i++)
        {
            total += CalculateRawLevelCost(currentLevel + i) * discountFactor;
        }
        return total;
    }

    /// <summary>
    /// Maximale leistbare Upgrades bei gegebenem Budget.
    /// </summary>
    public static (int count, decimal cost) CalculateMaxAffordableUpgrades(int currentLevel, decimal budget, decimal discountFactor)
    {
        if (budget <= 0 || currentLevel >= Workshop.MaxLevel) return (0, 0);
        decimal total = 0;
        int count = 0;
        for (int i = 0; i < Workshop.MaxLevel - currentLevel; i++)
        {
            decimal lvlCost = CalculateRawLevelCost(currentLevel + i) * discountFactor;
            if (total + lvlCost > budget) break;
            total += lvlCost;
            count++;
        }
        return (count, total);
    }

    /// <summary>Miete pro Stunde (skaliert mit Level).</summary>
    public static decimal CalculateRentPerHour(int level) => level <= 100
        ? GameBalanceConstants.RentBaseLinear * level
        : GameBalanceConstants.RentBaseExponential * (decimal)Math.Pow(GameBalanceConstants.RentExponent, level - 100);

    /// <summary>Materialkosten pro Stunde (hybrid: linear bis Lv.100, dann exponentiell).</summary>
    public static decimal CalculateMaterialCostPerHour(int level, WorkshopType type) => level <= 100
        ? GameBalanceConstants.MaterialCostBaseLinear * level * type.GetBaseIncomeMultiplier()
        : GameBalanceConstants.MaterialCostBaseExponential * (decimal)Math.Pow(GameBalanceConstants.MaterialCostExponent, level - 100) * type.GetBaseIncomeMultiplier();

    /// <summary>Einstellungskosten für den nächsten Worker (sanfter als 2^n).</summary>
    public static decimal CalculateHireWorkerCost(int currentWorkerCount) =>
        GameBalanceConstants.HireWorkerCostBase * (decimal)Math.Pow(GameBalanceConstants.HireWorkerCostExponent, currentWorkerCount);
}
