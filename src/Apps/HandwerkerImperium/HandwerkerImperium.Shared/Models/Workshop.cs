using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Represents a workshop/trade in the game.
/// Each workshop can be upgraded (1-50), staffed with workers, and has running costs.
/// </summary>
public class Workshop
{
    [JsonPropertyName("type")]
    public WorkshopType Type { get; set; }

    /// <summary>
    /// Current level (1-50). Higher = more income, more worker slots, higher costs.
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
    /// Max Level
    /// </summary>
    public const int MaxLevel = 50;

    /// <summary>
    /// Maximum workers allowed at current level.
    /// +1 every 5 levels (max 10 at level 50).
    /// Note: BuildingType.WorkshopExtension adds extra slots.
    /// </summary>
    [JsonIgnore]
    public int BaseMaxWorkers => Math.Min(10, 1 + (Level - 1) / 5);

    /// <summary>
    /// Total max workers including building bonus.
    /// Set by external systems that know about buildings.
    /// </summary>
    [JsonIgnore]
    public int MaxWorkers => BaseMaxWorkers + ExtraWorkerSlots;

    /// <summary>
    /// Extra worker slots from buildings/research (set externally).
    /// </summary>
    [JsonIgnore]
    public int ExtraWorkerSlots { get; set; }

    /// <summary>
    /// Base income per worker per second at current level.
    /// Formula: 1 * 2^((Level-1)/3) * TypeMultiplier
    /// Slower scaling than before (was /2, now /3).
    /// </summary>
    [JsonIgnore]
    public decimal BaseIncomePerWorker
    {
        get
        {
            decimal baseIncome = 1m * (decimal)Math.Pow(2, (Level - 1) / 3.0);
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
    /// Material cost per hour (20% of gross income).
    /// </summary>
    [JsonIgnore]
    public decimal MaterialCostPerHour => GrossIncomePerSecond * 3600m * 0.20m;

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
    /// Cost to upgrade to next level.
    /// Formula: 200 * 2.2^(Level-1)
    /// </summary>
    [JsonIgnore]
    public decimal UpgradeCost
    {
        get
        {
            if (Level >= MaxLevel) return 0;
            return 200m * (decimal)Math.Pow(2.2, Level - 1);
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

    // Legacy property for backward compatibility
    [JsonIgnore]
    public decimal HireWorkerCost => 50m * (decimal)Math.Pow(2, Workers.Count);

    public static Workshop Create(WorkshopType type)
    {
        return new Workshop
        {
            Type = type,
            IsUnlocked = type == WorkshopType.Carpenter // Carpenter is always unlocked
        };
    }
}
