using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Fähigkeit eines Vorarbeiters/Managers.
/// </summary>
public enum ManagerAbility
{
    AutoCollectOrders,
    EfficiencyBoost,
    FatigueReduction,
    MoodBoost,
    IncomeBoost,
    TrainingSpeedUp
}

/// <summary>
/// Statische Definition eines Managers (Template).
/// </summary>
public record ManagerDefinition(
    string Id,
    string NameKey,
    WorkshopType? Workshop,  // null = gilt für alle
    ManagerAbility Ability,
    int RequiredLevel,
    int RequiredPrestige,
    int RequiredPerfectRatings
);

/// <summary>
/// Ein freigeschalteter Manager mit Level.
/// </summary>
public class Manager
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("isUnlocked")]
    public bool IsUnlocked { get; set; }

    /// <summary>
    /// Max-Level ist 5.
    /// </summary>
    [JsonIgnore]
    public bool IsMaxLevel => Level >= 5;

    /// <summary>
    /// Upgrade-Kosten in Goldschrauben.
    /// </summary>
    [JsonIgnore]
    public int UpgradeCost => Level * 10;

    /// <summary>
    /// Alle 14 Manager-Definitionen (gecacht, keine Allokation pro Aufruf).
    /// </summary>
    private static readonly List<ManagerDefinition> _allDefinitions =
    [
        new("mgr_hans", "ManagerHans", WorkshopType.Carpenter, ManagerAbility.EfficiencyBoost, 10, 0, 0),
        new("mgr_fritz", "ManagerFritz", WorkshopType.Plumber, ManagerAbility.FatigueReduction, 20, 0, 0),
        new("mgr_kurt", "ManagerKurt", WorkshopType.Electrician, ManagerAbility.IncomeBoost, 30, 0, 0),
        new("mgr_lisa", "ManagerLisa", WorkshopType.Painter, ManagerAbility.MoodBoost, 40, 0, 0),
        new("mgr_karl", "ManagerKarl", WorkshopType.Roofer, ManagerAbility.EfficiencyBoost, 60, 0, 0),
        new("mgr_otto", "ManagerOtto", WorkshopType.Contractor, ManagerAbility.IncomeBoost, 80, 0, 0),
        new("mgr_anna", "ManagerAnna", WorkshopType.Architect, ManagerAbility.FatigueReduction, 0, 0, 25),
        new("mgr_max", "ManagerMax", WorkshopType.GeneralContractor, ManagerAbility.IncomeBoost, 100, 0, 0),
        new("mgr_schmied", "ManagerSchmied", WorkshopType.MasterSmith, ManagerAbility.EfficiencyBoost, 120, 0, 0),
        new("mgr_erfinder", "ManagerErfinder", WorkshopType.InnovationLab, ManagerAbility.IncomeBoost, 140, 0, 0),
        new("mgr_schmidt", "ManagerSchmidt", null, ManagerAbility.TrainingSpeedUp, 0, 1, 0),
        new("mgr_weber", "ManagerWeber", null, ManagerAbility.AutoCollectOrders, 0, 2, 0),
        new("mgr_mueller", "ManagerMueller", null, ManagerAbility.EfficiencyBoost, 0, 3, 0),
        new("mgr_kaiser", "ManagerKaiser", null, ManagerAbility.IncomeBoost, 0, 4, 0),
    ];

    /// <summary>
    /// Dictionary-Lookup für O(1) Zugriff per ID (vermeidet FirstOrDefault pro GetBonus-Aufruf).
    /// </summary>
    private static readonly Dictionary<string, ManagerDefinition> _definitionsById =
        _allDefinitions.ToDictionary(d => d.Id, d => d);

    public static List<ManagerDefinition> GetAllDefinitions() => _allDefinitions;

    /// <summary>
    /// Findet eine Manager-Definition per ID (O(1) Dictionary-Lookup).
    /// </summary>
    public static ManagerDefinition? GetDefinitionById(string id) =>
        _definitionsById.GetValueOrDefault(id);

    /// <summary>
    /// Berechnet den Bonus basierend auf Manager-Level und Fähigkeit.
    /// </summary>
    public decimal GetBonus(ManagerAbility ability)
    {
        if (!IsUnlocked) return 0m;
        var def = GetDefinitionById(Id);
        if (def == null || def.Ability != ability) return 0m;

        return ability switch
        {
            ManagerAbility.EfficiencyBoost => 0.05m * Level,   // +5% pro Level
            ManagerAbility.FatigueReduction => 0.03m * Level,  // -3% pro Level
            ManagerAbility.MoodBoost => 0.04m * Level,         // +4% pro Level
            ManagerAbility.IncomeBoost => 0.05m * Level,       // +5% pro Level
            ManagerAbility.TrainingSpeedUp => 0.10m * Level,   // +10% pro Level
            ManagerAbility.AutoCollectOrders => Level,          // Anzahl pro Check
            _ => 0m
        };
    }
}
