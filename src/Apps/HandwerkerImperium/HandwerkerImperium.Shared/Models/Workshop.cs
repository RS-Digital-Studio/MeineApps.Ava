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
    /// Extra Worker-Slots durch Rewarded Ads (persistent).
    /// </summary>
    [JsonPropertyName("adBonusWorkerSlots")]
    public int AdBonusWorkerSlots { get; set; }

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
    /// </summary>
    [JsonIgnore]
    public decimal GrossIncomePerSecond
    {
        get
        {
            if (Workers.Count == 0) return 0;
            return Workers.Sum(w => BaseIncomePerWorker * w.EffectiveEfficiency);
        }
    }

    /// <summary>
    /// Rent cost per hour (scales with level).
    /// </summary>
    [JsonIgnore]
    public decimal RentPerHour => 10m * Level;

    /// <summary>
    /// Material-Kosten pro Stunde (skaliert mit Level, nicht mit Einkommen).
    /// Formel: 5 * Level * TypeMultiplier - so profitiert man von Worker-Effizienz und Prestige-Boni.
    /// </summary>
    [JsonIgnore]
    public decimal MaterialCostPerHour => 5m * Level * Type.GetBaseIncomeMultiplier();

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
    /// Net income per second (gross - costs/3600).
    /// Can be negative!
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
    /// Formel: 200 * 1.035^(Level-1)
    /// Moderat-exponentiell, skaliert sicher bis Level 1000.
    /// </summary>
    [JsonIgnore]
    public decimal UpgradeCost
    {
        get
        {
            if (Level >= MaxLevel) return 0;
            if (Level == 1) return 100m; // Erstes Upgrade guenstig
            return 200m * (decimal)Math.Pow(1.035, Level - 1);
        }
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
