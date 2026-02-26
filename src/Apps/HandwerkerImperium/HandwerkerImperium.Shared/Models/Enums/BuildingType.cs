namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Types of support buildings that provide passive bonuses.
/// Each building can be upgraded from level 1 to 5.
/// </summary>
public enum BuildingType
{
    /// <summary>Improves worker mood recovery and reduces rest time</summary>
    Canteen = 0,

    /// <summary>Reduces material costs</summary>
    Storage = 1,

    /// <summary>Increases available order slots</summary>
    Office = 2,

    /// <summary>Passively increases reputation</summary>
    Showroom = 3,

    /// <summary>Speeds up worker training</summary>
    TrainingCenter = 4,

    /// <summary>Increases order rewards</summary>
    VehicleFleet = 5,

    /// <summary>Adds extra worker slots per workshop</summary>
    WorkshopExtension = 6
}

public static class BuildingTypeExtensions
{
    /// <summary>
    /// Base purchase cost for this building type (level 1).
    /// Each additional level costs BaseCost * 3^(Level-1).
    /// </summary>
    public static decimal GetBaseCost(this BuildingType type) => type switch
    {
        BuildingType.Canteen => 10_000m,
        BuildingType.Storage => 15_000m,
        BuildingType.Office => 20_000m,
        BuildingType.Showroom => 25_000m,
        BuildingType.TrainingCenter => 50_000m,
        BuildingType.VehicleFleet => 75_000m,
        BuildingType.WorkshopExtension => 100_000m,
        _ => 10_000m
    };

    /// <summary>
    /// Player level required to unlock this building.
    /// </summary>
    public static int GetUnlockLevel(this BuildingType type) => type switch
    {
        BuildingType.Canteen => 5,
        BuildingType.Storage => 8,
        BuildingType.Office => 10,
        BuildingType.Showroom => 15,
        BuildingType.TrainingCenter => 20,
        BuildingType.VehicleFleet => 25,
        BuildingType.WorkshopExtension => 30,
        _ => 5
    };

    /// <summary>
    /// Maximum level for this building type.
    /// </summary>
    public static int GetMaxLevel(this BuildingType type) => 5;

    /// <summary>
    /// Icon for this building type.
    /// </summary>
    public static string GetIcon(this BuildingType type) => type switch
    {
        BuildingType.Canteen => "Silverware",
        BuildingType.Storage => "PackageVariantClosed",
        BuildingType.Office => "OfficeBuildingOutline",
        BuildingType.Showroom => "StorefrontOutline",
        BuildingType.TrainingCenter => "SchoolOutline",
        BuildingType.VehicleFleet => "TruckOutline",
        BuildingType.WorkshopExtension => "HammerWrench",
        _ => "OfficeBuildingOutline"
    };

    /// <summary>
    /// Localization key for building name.
    /// </summary>
    public static string GetLocalizationKey(this BuildingType type) => type.ToString();

    /// <summary>
    /// Localization key for building description.
    /// </summary>
    public static string GetDescriptionKey(this BuildingType type) => $"{type}Desc";

    /// <summary>
    /// Localization key for building effect description.
    /// </summary>
    public static string GetEffectKey(this BuildingType type) => $"{type}Effect";
}
