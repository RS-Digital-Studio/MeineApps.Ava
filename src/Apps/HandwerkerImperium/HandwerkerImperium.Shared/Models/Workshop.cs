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
    /// Formel: 1 * 1.02^(Level-1) * TypeMultiplier
    /// Langsameres Wachstum (1.02 statt 1.025) → Progression dauert länger,
    /// Spieler müssen mehr upgraden/optimieren bevor es "explodiert".
    /// </summary>
    [JsonIgnore]
    public decimal BaseIncomePerWorker
    {
        get
        {
            decimal baseIncome = (decimal)Math.Pow(1.02, Level - 1);
            return baseIncome * Type.GetBaseIncomeMultiplier() * GetMilestoneMultiplier();
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
    /// Multiplikator-Meilensteine bei bestimmten Workshop-Leveln.
    /// Erzeugt "Bumpy Progression" (AdVenture-Capitalist-Pattern):
    /// Vor einem Meilenstein verlangsamt es sich, danach explodiert das Einkommen.
    /// Meilensteine bei 25/50/75/100/150/200/225/250/350/500/1000.
    /// Meilenstein 200 schließt die Lücke zwischen 150 und 250.
    /// Meilenstein 225 (BAL-13) schließt die Grind-Wall 200→250.
    /// Meilenstein 350 schließt die Durststrecke 250→500 (BAL-1).
    /// </summary>
    public decimal GetMilestoneMultiplier()
    {
        decimal mult = 1.0m;
        if (Level >= 25) mult *= 1.25m;
        if (Level >= 50) mult *= 1.5m;
        if (Level >= 75) mult *= 1.5m;
        if (Level >= 100) mult *= 1.75m;
        if (Level >= 150) mult *= 2.0m;
        if (Level >= 200) mult *= 1.75m;
        if (Level >= 225) mult *= 1.5m;   // BAL-13: Neuer Meilenstein gegen Grind-Wall 200→250
        if (Level >= 250) mult *= 2.0m;
        if (Level >= 350) mult *= 2.0m;   // BAL-1: Neuer Meilenstein gegen Mid-Game-Durststrecke
        if (Level >= 500) mult *= 3.0m;
        if (Level >= 1000) mult *= 5.0m;
        return mult;
    }

    /// <summary>
    /// Prüft ob das aktuelle Level ein Multiplikator-Meilenstein ist.
    /// </summary>
    public static bool IsMilestoneLevel(int level) =>
        level is 25 or 50 or 75 or 100 or 150 or 200 or 225 or 250 or 350 or 500 or 1000;

    /// <summary>
    /// Gibt den Multiplikator für ein bestimmtes Meilenstein-Level zurück.
    /// </summary>
    public static decimal GetMilestoneMultiplierForLevel(int level) => level switch
    {
        25 => 1.25m,
        50 => 1.5m,
        75 => 1.5m,
        100 => 1.75m,
        150 => 2.0m,
        200 => 1.75m,
        225 => 1.5m,   // BAL-13: Neuer Meilenstein gegen Grind-Wall
        250 => 2.0m,
        350 => 2.0m,   // BAL-1: Neuer Meilenstein
        500 => 3.0m,
        1000 => 5.0m,
        _ => 1.0m
    };

    /// <summary>
    /// Rent cost per hour (scales with level).
    /// </summary>
    [JsonIgnore]
    public decimal RentPerHour => Level <= 100
        ? 10m * Level
        : 1000m * (decimal)Math.Pow(1.005, Level - 100);

    /// <summary>
    /// Material-Kosten pro Stunde (hybrid: linear bis Lv.100, dann exponentiell).
    /// Bis Level 100 unverändet (bestehende Savegames safe).
    /// Ab 100 wachsen Kosten moderat exponentiell → spürbar bei hohen Leveln.
    /// </summary>
    [JsonIgnore]
    public decimal MaterialCostPerHour => Level <= 100
        ? 5m * Level * Type.GetBaseIncomeMultiplier()
        : 500m * (decimal)Math.Pow(1.005, Level - 100) * Type.GetBaseIncomeMultiplier();

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
    /// Formel: 200 * 1.05^(Level-1), reduziert durch Prestige-Shop UpgradeDiscount.
    /// Steilere Kostenkurve (1.05 statt 1.035) → Upgrades werden schneller teuer,
    /// Spieler müssen länger sparen oder Ads schauen für Boost.
    /// </summary>
    [JsonIgnore]
    public decimal UpgradeCost
    {
        get
        {
            if (Level >= MaxLevel) return 0;
            decimal baseCost = Level == 1 ? 100m : 200m * (decimal)Math.Pow(1.05, Level - 1);

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
            decimal cost = lvl == 1 ? 100m : 200m * (decimal)Math.Pow(1.05, lvl - 1);
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
            decimal lvlCost = lvl == 1 ? 100m : 200m * (decimal)Math.Pow(1.05, lvl - 1);
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
