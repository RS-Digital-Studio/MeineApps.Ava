using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Support building that provides passive bonuses.
/// Can be upgraded from level 1 to 5.
/// </summary>
public class Building
{
    [JsonPropertyName("type")]
    public BuildingType Type { get; set; }

    /// <summary>
    /// Current level (1-5). 0 = not built.
    /// </summary>
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("isBuilt")]
    public bool IsBuilt { get; set; }

    [JsonIgnore]
    public string Name => Type.GetLocalizationKey();

    [JsonIgnore]
    public string Icon => Type.GetIcon();

    [JsonIgnore]
    public string Description => Type.GetDescriptionKey();

    /// <summary>
    /// Cost to build (level 1) or upgrade to next level.
    /// Formula: BaseCost * 3^(Level)
    /// </summary>
    [JsonIgnore]
    public decimal NextLevelCost
    {
        get
        {
            if (!IsBuilt) return Type.GetBaseCost();
            if (Level >= Type.GetMaxLevel()) return 0m;
            return Type.GetBaseCost() * (decimal)Math.Pow(3, Level);
        }
    }

    [JsonIgnore]
    public bool CanUpgrade => IsBuilt && Level < Type.GetMaxLevel();

    // ═══════════════════════════════════════════════════════════════════════
    // EFFECTS (vary by building type and level)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Canteen: Passive mood recovery per hour.
    /// Level 1-5: 1%, 2%, 3%, 4%, 5%
    /// </summary>
    [JsonIgnore]
    public decimal MoodRecoveryPerHour => Type == BuildingType.Canteen ? Level * 1.0m : 0m;

    /// <summary>
    /// Canteen: Rest time reduction multiplier.
    /// Level 1-5: 50%, 55%, 60%, 70%, 80%
    /// </summary>
    [JsonIgnore]
    public decimal RestTimeReduction => Type == BuildingType.Canteen ? Level switch
    {
        1 => 0.50m,
        2 => 0.55m,
        3 => 0.60m,
        4 => 0.70m,
        5 => 0.80m,
        _ => 0m
    } : 0m;

    /// <summary>
    /// Storage: Material cost reduction.
    /// Level 1-5: 15%, 25%, 35%, 45%, 50%
    /// </summary>
    [JsonIgnore]
    public decimal MaterialCostReduction => Type == BuildingType.Storage ? Level switch
    {
        1 => 0.15m,
        2 => 0.25m,
        3 => 0.35m,
        4 => 0.45m,
        5 => 0.50m,
        _ => 0m
    } : 0m;

    /// <summary>
    /// Office: Extra order slots.
    /// Level 1-5: 2, 3, 4, 5, 6
    /// </summary>
    [JsonIgnore]
    public int ExtraOrderSlots => Type == BuildingType.Office ? Level + 1 : 0;

    /// <summary>
    /// Showroom: Daily passive reputation gain.
    /// Level 1-5: 0.5, 1.0, 1.5, 2.0, 2.5
    /// </summary>
    [JsonIgnore]
    public decimal DailyReputationGain => Type == BuildingType.Showroom ? Level * 0.5m : 0m;

    /// <summary>
    /// TrainingCenter: Training speed multiplier.
    /// Level 1-5: 2x, 2.5x, 3x, 3.5x, 4x
    /// </summary>
    [JsonIgnore]
    public decimal TrainingSpeedMultiplier => Type == BuildingType.TrainingCenter ? 1.0m + Level * 0.5m + 0.5m : 1.0m;

    /// <summary>
    /// VehicleFleet: Order reward bonus.
    /// Level 1-5: 20%, 30%, 40%, 50%, 60%
    /// </summary>
    [JsonIgnore]
    public decimal OrderRewardBonus => Type == BuildingType.VehicleFleet ? Level switch
    {
        1 => 0.20m,
        2 => 0.30m,
        3 => 0.40m,
        4 => 0.50m,
        5 => 0.60m,
        _ => 0m
    } : 0m;

    /// <summary>
    /// WorkshopExtension: Extra worker slots per workshop.
    /// Level 1-5: 2, 3, 4, 5, 6
    /// </summary>
    [JsonIgnore]
    public int ExtraWorkerSlots => Type == BuildingType.WorkshopExtension ? Level + 1 : 0;

    public static Building Create(BuildingType type)
    {
        return new Building { Type = type };
    }
}
