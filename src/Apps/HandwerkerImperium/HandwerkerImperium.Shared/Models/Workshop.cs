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
    public const int MaxLevel = GameBalanceConstants.WorkshopMaxLevel;

    /// <summary>
    /// Maximum workers allowed at current level.
    /// +1 every 50 levels (max 20 at level 1000).
    /// Note: BuildingType.WorkshopExtension adds extra slots.
    /// </summary>
    [JsonIgnore]
    public int BaseMaxWorkers => Math.Min(20, 1 + (Level - 1) / 50);

    /// <summary>
    /// Total max workers including building bonus + Ad-Bonus + Rebirth-Sterne + Spezialisierung.
    /// Set by external systems that know about buildings.
    /// </summary>
    [JsonIgnore]
    public int MaxWorkers => Math.Max(1, BaseMaxWorkers + ExtraWorkerSlots + AdBonusWorkerSlots
        + RebirthExtraWorkers + (WorkshopSpecialization?.WorkerCapacityModifier ?? 0));

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

    // ═══════════════════════════════════════════════════════════════════════
    // REBIRTH (Late-Game Prestige pro Workshop, 0-5 Sterne)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Anzahl der Rebirth-Sterne (0-5). Permanent, überlebt Prestige + Ascension.
    /// Wird extern über RebirthService aus GameState.WorkshopStars gesetzt.
    /// </summary>
    [JsonIgnore]
    public int RebirthStars { get; set; }

    /// <summary>
    /// Einkommens-Bonus durch Rebirth-Sterne (0% bis +150%).
    /// </summary>
    [JsonIgnore]
    public decimal RebirthIncomeBonus => RebirthStars >= 1 && RebirthStars <= 5
        ? GameBalanceConstants.RebirthIncomeBonuses[RebirthStars - 1]
        : 0m;

    /// <summary>
    /// Upgrade-Kosten-Rabatt durch Rebirth-Sterne (0% bis -25%).
    /// </summary>
    [JsonIgnore]
    public decimal RebirthUpgradeDiscount => RebirthStars >= 1 && RebirthStars <= 5
        ? GameBalanceConstants.RebirthUpgradeDiscounts[RebirthStars - 1]
        : 0m;

    /// <summary>
    /// Extra Worker-Slots durch Rebirth-Sterne (0 bis +2).
    /// </summary>
    [JsonIgnore]
    public int RebirthExtraWorkers => RebirthStars < GameBalanceConstants.RebirthExtraWorkers.Length
        ? GameBalanceConstants.RebirthExtraWorkers[RebirthStars]
        : GameBalanceConstants.RebirthExtraWorkers[^1];

    // ═══════════════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════════════
    // BERECHNETE PROPERTIES (delegieren an WorkshopFormulas für Testbarkeit)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Basis-Einkommen pro Worker pro Sekunde.</summary>
    [JsonIgnore]
    public decimal BaseIncomePerWorker => WorkshopFormulas.CalculateBaseIncomePerWorker(Level, Type);

    /// <summary>Brutto-Einkommen pro Sekunde (alle Worker, Aura, Rebirth, Spezialisierung).</summary>
    [JsonIgnore]
    public decimal GrossIncomePerSecond
    {
        get
        {
            var gross = WorkshopFormulas.CalculateGrossIncome(Level, Type, Workers, LevelResistanceBonus, RebirthIncomeBonus);
            if (WorkshopSpecialization != null)
            {
                // Quality: +20% Worker-Effizienz als Einkommensboost
                if (WorkshopSpecialization.EfficiencyModifier != 0m)
                    gross *= (1m + WorkshopSpecialization.EfficiencyModifier);
                // Efficiency: +30% / Economy: -5%
                if (WorkshopSpecialization.IncomeModifier != 0m)
                    gross *= (1m + WorkshopSpecialization.IncomeModifier);
            }
            return gross;
        }
    }

    /// <summary>Level-Anforderungsfaktor für einen Worker (Tier-Resistenz + Forschung).</summary>
    public decimal GetWorkerLevelFitFactor(Worker worker) =>
        WorkshopFormulas.CalculateLevelFitFactor(Level, worker.Tier.GetLevelResistance(), LevelResistanceBonus);

    /// <summary>Kumulativer Meilenstein-Multiplikator für das aktuelle Level.</summary>
    public decimal GetMilestoneMultiplier() => WorkshopFormulas.CalculateMilestoneMultiplier(Level);

    /// <summary>Prüft ob ein Level ein Meilenstein ist.</summary>
    public static bool IsMilestoneLevel(int level) => WorkshopFormulas.IsMilestoneLevel(level);

    /// <summary>Einzel-Multiplikator für ein bestimmtes Meilenstein-Level.</summary>
    public static decimal GetMilestoneMultiplierForLevel(int level) => WorkshopFormulas.GetMilestoneMultiplierForLevel(level);

    /// <summary>Miete pro Stunde (skaliert mit Level).</summary>
    [JsonIgnore]
    public decimal RentPerHour => WorkshopFormulas.CalculateRentPerHour(Level);

    /// <summary>Materialkosten pro Stunde (hybrid: linear bis Lv.100, dann exponentiell).</summary>
    [JsonIgnore]
    public decimal MaterialCostPerHour => WorkshopFormulas.CalculateMaterialCostPerHour(Level, Type);

    /// <summary>
    /// Total worker wages per hour (For-Schleife, kein LINQ).
    /// </summary>
    [JsonIgnore]
    public decimal TotalWagesPerHour
    {
        get
        {
            decimal total = 0m;
            for (int i = 0; i < Workers.Count; i++)
            {
                if (!Workers[i].IsResting)
                    total += Workers[i].WagePerHour;
            }
            return total;
        }
    }

    /// <summary>
    /// Total running costs per hour (rent + material + wages + Spezialisierung).
    /// </summary>
    [JsonIgnore]
    public decimal TotalCostsPerHour
    {
        get
        {
            var costs = RentPerHour + MaterialCostPerHour + TotalWagesPerHour;
            // Quality: +15% Kosten / Economy: -25% Kosten
            if (WorkshopSpecialization?.CostModifier is > 0m or < 0m)
                costs *= (1m + WorkshopSpecialization!.CostModifier);
            return Math.Max(0m, costs);
        }
    }

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

    /// <summary>Upgrade-Kosten mit allen Rabatten (Rebirth, Prestige-Shop, VIP).</summary>
    [JsonIgnore]
    public decimal UpgradeCost =>
        WorkshopFormulas.CalculateUpgradeCost(Level, RebirthUpgradeDiscount, UpgradeDiscount, VipCostReduction);

    /// <summary>
    /// VIP-Kosten-Reduktion (0.0-0.10). Wird extern von GameLoopService gesetzt.
    /// </summary>
    [JsonIgnore]
    public decimal VipCostReduction { get; set; }

    /// <summary>Kombinierter Rabattfaktor aus Rebirth + Prestige-Shop + VIP.</summary>
    private decimal GetCombinedDiscountFactor() =>
        WorkshopFormulas.CalculateDiscountFactor(RebirthUpgradeDiscount, UpgradeDiscount, VipCostReduction);

    /// <summary>Bulk-Upgrade Gesamtkosten für N Level.</summary>
    public decimal GetBulkUpgradeCost(int count) =>
        WorkshopFormulas.CalculateBulkUpgradeCost(Level, count, GetCombinedDiscountFactor());

    /// <summary>Maximale leistbare Upgrades bei gegebenem Budget.</summary>
    public (int count, decimal cost) GetMaxAffordableUpgrades(decimal budget) =>
        WorkshopFormulas.CalculateMaxAffordableUpgrades(Level, budget, GetCombinedDiscountFactor());

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

    /// <summary>Einstellungskosten für den nächsten Worker.</summary>
    [JsonIgnore]
    public decimal HireWorkerCost => WorkshopFormulas.CalculateHireWorkerCost(Workers.Count);

    public static Workshop Create(WorkshopType type)
    {
        return new Workshop
        {
            Type = type,
            IsUnlocked = type == WorkshopType.Carpenter // Carpenter is always unlocked
        };
    }
}
