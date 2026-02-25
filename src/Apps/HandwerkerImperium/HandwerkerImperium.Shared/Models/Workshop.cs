using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Represents a workshop/trade in the game.
/// Each workshop can be upgraded (1-1000), staffed with workers, and has running costs.
/// </summary>
public class Workshop
{
    [JsonPropertyName("type")]
    public WorkshopType Type { get; set; }

    /// <summary>
    /// Current level (1-1000). Higher = more income, more worker slots, higher costs.
    /// </summary>
    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("workers")]
    public List<Worker> Workers { get; set; } = [];

    [JsonPropertyName("totalEarned")]
    public decimal TotalEarned { get; set; }

    [JsonPropertyName("ordersCompleted")]
    public int OrdersCompleted { get; set; }

    /// <summary>
    /// Whether this workshop has been purchased/unlocked.
    /// </summary>
    [JsonPropertyName("isUnlocked")]
    public bool IsUnlocked { get; set; }

    /// <summary>
    /// Gewählte Spezialisierung (ab Level 100, null = keine).
    /// </summary>
    [JsonPropertyName("specialization")]
    public WorkshopSpecialization? WorkshopSpecialization { get; set; }

    /// <summary>
    /// Maximales Workshop-Level.
    /// </summary>
    public const int MaxLevel = 1000;

    /// <summary>
    /// Maximum workers allowed at current level.
    /// +1 every 50 levels (max 20 at level 1000).
    /// Note: BuildingType.WorkshopExtension adds extra slots.
    /// </summary>
    [JsonIgnore]
    public int BaseMaxWorkers => Math.Min(20, 1 + (Level - 1) / 50);

    /// <summary>
    /// Total max workers including building bonus + Ad-Bonus.
    /// Set by external systems that know about buildings.
    /// </summary>
    [JsonIgnore]
    public int MaxWorkers => BaseMaxWorkers + ExtraWorkerSlots + AdBonusWorkerSlots;

    /// <summary>
    /// Extra worker slots from buildings/research (set externally).
    /// </summary>
    [JsonIgnore]
    public int ExtraWorkerSlots { get; set; }

    /// <summary>
    /// Maximale Anzahl Ad-Bonus-Slots pro Workshop (Exploit-Schutz).
    /// </summary>
    public const int MaxAdBonusWorkerSlots = 3;

    /// <summary>
    /// Extra Worker-Slots durch Rewarded Ads (persistent, max 3).
    /// </summary>
    [JsonPropertyName("adBonusWorkerSlots")]
    public int AdBonusWorkerSlots { get; set; }

    /// <summary>
    /// Level-Resistenz-Bonus aus Forschung (0.0-0.5). Wird extern gesetzt.
    /// Reduziert den Workshop-Level-Anforderungsmalus auf Worker-Effizienz.
    /// </summary>
    [JsonIgnore]
    public decimal LevelResistanceBonus { get; set; }

    /// <summary>
    /// Upgrade-Kosten-Rabatt aus Prestige-Shop (0.0-1.0). Wird extern gesetzt.
    /// z.B. 0.15 = -15% auf Upgrade-Kosten.
    /// </summary>
    [JsonIgnore]
    public decimal UpgradeDiscount { get; set; }

    /// <summary>
    /// Base income per worker per second at current level.
    /// Formel: 1 * 1.025^(Level-1) * TypeMultiplier
    /// Moderat-exponentiell, skaliert sicher bis Level 1000.
    /// </summary>
    [JsonIgnore]
    public decimal BaseIncomePerWorker
    {
        get
        {
            decimal baseIncome = (decimal)Math.Pow(1.025, Level - 1);
            return baseIncome * Type.GetBaseIncomeMultiplier();
        }
    }

    /// <summary>
    /// Total gross income per second from all workers.
    /// Berücksichtigt den Level-Anforderungsmalus pro Worker (höhere Tiers sind resistenter)
    /// und Worker-Aura-Bonus (S-Tier+ geben passiven Einkommens-Bonus).
    /// </summary>
    [JsonIgnore]
    public decimal GrossIncomePerSecond
    {
        get
        {
            if (Workers.Count == 0) return 0;
            decimal baseIncome = Workers.Sum(w => BaseIncomePerWorker * w.EffectiveEfficiency * GetWorkerLevelFitFactor(w));

            // Worker-Aura: S-Tier+ Worker geben passiven Einkommens-Bonus
            decimal auraBonus = 0m;
            for (int i = 0; i < Workers.Count; i++)
                auraBonus += Workers[i].Tier.GetAuraBonus();

            if (auraBonus > 0)
                baseIncome *= (1m + auraBonus);

            return baseIncome;
        }
    }

    /// <summary>
    /// Berechnet den Level-Anforderungsfaktor für einen Worker.
    /// Höhere Workshop-Level sind anspruchsvoller → niedrige Tiers verlieren Effizienz.
    /// Basis: -2% alle 30 Level. Reduziert durch Tier-Resistenz + Forschung.
    /// </summary>
    public decimal GetWorkerLevelFitFactor(Worker worker)
    {
        if (Level <= 30) return 1.0m; // Kein Malus unter Level 30

        int steps = Level / 30;
        decimal basePenalty = steps * 0.02m;
        decimal tierResistance = worker.Tier.GetLevelResistance();
        decimal totalResistance = Math.Min(1.0m, tierResistance + LevelResistanceBonus);
        decimal adjustedPenalty = basePenalty * (1m - totalResistance);
        return Math.Max(0.20m, 1m - adjustedPenalty); // Min 20% (Worker wird nie komplett nutzlos)
    }

    /// <summary>
    /// Rent cost per hour (scales with level).
    /// </summary>
    [JsonIgnore]
    public decimal RentPerHour => Level <= 100
        ? 10m * Level
        : 1000m * (decimal)Math.Pow(1.008, Level - 100);

    /// <summary>
    /// Material-Kosten pro Stunde (hybrid: linear bis Lv.100, dann exponentiell).
    /// Bis Level 100 unverändet (bestehende Savegames safe).
    /// Ab 100 wachsen Kosten moderat exponentiell → spürbar bei hohen Leveln.
    /// </summary>
    [JsonIgnore]
    public decimal MaterialCostPerHour => Level <= 100
        ? 5m * Level * Type.GetBaseIncomeMultiplier()
        : 500m * (decimal)Math.Pow(1.008, Level - 100) * Type.GetBaseIncomeMultiplier();

    /// <summary>
    /// Total worker wages per hour.
    /// </summary>
    [JsonIgnore]
    public decimal TotalWagesPerHour => Workers.Where(w => !w.IsResting).Sum(w => w.WagePerHour);

    /// <summary>
    /// Total running costs per hour (rent + material + wages).
    /// </summary>
    [JsonIgnore]
    public decimal TotalCostsPerHour => RentPerHour + MaterialCostPerHour + TotalWagesPerHour;

    /// <summary>
    /// Roher Netto-Einkommenswert OHNE Prestige-Multiplikator und Research-Boni.
    /// Die tatsächliche Berechnung mit allen Modifikatoren erfolgt im GameLoopService.
    /// Nur für Display-Zwecke (WorkshopView).
    /// </summary>
    [JsonIgnore]
    public decimal NetIncomePerSecond => GrossIncomePerSecond - TotalCostsPerHour / 3600m;

    /// <summary>
    /// Legacy IncomePerSecond (now uses EffectiveEfficiency instead of raw Efficiency).
    /// </summary>
    [JsonIgnore]
    public decimal IncomePerSecond => GrossIncomePerSecond;

    /// <summary>
    /// Kosten fuer Upgrade auf naechstes Level.
    /// Formel: 200 * 1.035^(Level-1), reduziert durch Prestige-Shop UpgradeDiscount.
    /// Moderat-exponentiell, skaliert sicher bis Level 1000.
    /// </summary>
    [JsonIgnore]
    public decimal UpgradeCost
    {
        get
        {
            if (Level >= MaxLevel) return 0;
            decimal baseCost = Level == 1 ? 100m : 200m * (decimal)Math.Pow(1.035, Level - 1);

            // Prestige-Shop Upgrade-Rabatt anwenden
            if (UpgradeDiscount > 0)
                baseCost *= (1m - Math.Min(UpgradeDiscount, 0.50m));

            return baseCost;
        }
    }

    /// <summary>
    /// Berechnet die Gesamtkosten fuer N Upgrades ab dem aktuellen Level.
    /// Beruecksichtigt die exponentielle Kostensteigerung pro Level und Prestige-Shop-Rabatt.
    /// </summary>
    public decimal GetBulkUpgradeCost(int count)
    {
        if (count <= 0 || Level >= MaxLevel) return 0;
        decimal total = 0;
        decimal discountFactor = UpgradeDiscount > 0 ? (1m - Math.Min(UpgradeDiscount, 0.50m)) : 1m;
        int maxUpgrades = Math.Min(count, MaxLevel - Level);
        for (int i = 0; i < maxUpgrades; i++)
        {
            int lvl = Level + i;
            decimal cost = lvl == 1 ? 100m : 200m * (decimal)Math.Pow(1.035, lvl - 1);
            total += cost * discountFactor;
        }
        return total;
    }

    /// <summary>
    /// Berechnet wie viele Upgrades mit dem gegebenen Budget moeglich sind.
    /// </summary>
    public (int count, decimal cost) GetMaxAffordableUpgrades(decimal budget)
    {
        if (budget <= 0 || Level >= MaxLevel) return (0, 0);
        decimal total = 0;
        int count = 0;
        for (int i = 0; i < MaxLevel - Level; i++)
        {
            int lvl = Level + i;
            decimal lvlCost = lvl == 1 ? 100m : 200m * (decimal)Math.Pow(1.035, lvl - 1);
            if (total + lvlCost > budget) break;
            total += lvlCost;
            count++;
        }
        return (count, total);
    }

    /// <summary>
    /// Cost to unlock this workshop (one-time).
    /// </summary>
    [JsonIgnore]
    public decimal UnlockCost => Type.GetUnlockCost();

    [JsonIgnore]
    public bool CanUpgrade => Level < MaxLevel;

    [JsonIgnore]
    public bool CanHireWorker => Workers.Count < MaxWorkers;

    [JsonIgnore]
    public string Icon => Type.GetIcon();

    // Kosten fuer naechsten Worker (sanfter als 2^n)
    [JsonIgnore]
    public decimal HireWorkerCost => 50m * (decimal)Math.Pow(1.5, Workers.Count);

    public static Workshop Create(WorkshopType type)
    {
        return new Workshop
        {
            Type = type,
            IsUnlocked = type == WorkshopType.Carpenter // Carpenter is always unlocked
        };
    }
}
