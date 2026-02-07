using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Manages support buildings and their passive bonuses.
/// </summary>
public interface IBuildingService
{
    bool TryBuildBuilding(BuildingType type);
    bool TryUpgradeBuilding(BuildingType type);
    Building? GetBuilding(BuildingType type);
    List<Building> GetAllBuildings();

    /// <summary>
    /// Gets the aggregate effect values from all built buildings.
    /// </summary>
    decimal GetMoodRecoveryBonus();
    decimal GetRestTimeReduction();
    decimal GetMaterialCostReduction();
    int GetExtraOrderSlots();
    decimal GetDailyReputationGain();
    decimal GetTrainingSpeedMultiplier();
    decimal GetOrderRewardBonus();
    int GetExtraWorkerSlots();
}
