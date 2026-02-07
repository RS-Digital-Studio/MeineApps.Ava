using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

public class BuildingService : IBuildingService
{
    private readonly IGameStateService _gameState;

    public BuildingService(IGameStateService gameState)
    {
        _gameState = gameState;
    }

    public bool TryBuildBuilding(BuildingType type)
    {
        var state = _gameState.State;
        if (state.PlayerLevel < type.GetUnlockLevel()) return false;

        var existing = state.Buildings.FirstOrDefault(b => b.Type == type);
        if (existing is { IsBuilt: true }) return false;

        var cost = type.GetBaseCost();
        if (!_gameState.CanAfford(cost)) return false;

        _gameState.TrySpendMoney(cost);

        if (existing != null)
        {
            existing.IsBuilt = true;
            existing.Level = 1;
        }
        else
        {
            var building = Building.Create(type);
            building.IsBuilt = true;
            building.Level = 1;
            state.Buildings.Add(building);
        }

        return true;
    }

    public bool TryUpgradeBuilding(BuildingType type)
    {
        var building = GetBuilding(type);
        if (building == null || !building.CanUpgrade) return false;

        var cost = building.NextLevelCost;
        if (!_gameState.CanAfford(cost)) return false;

        _gameState.TrySpendMoney(cost);
        building.Level++;
        return true;
    }

    public Building? GetBuilding(BuildingType type) =>
        _gameState.State.Buildings.FirstOrDefault(b => b.Type == type && b.IsBuilt);

    public List<Building> GetAllBuildings() => _gameState.State.Buildings;

    public decimal GetMoodRecoveryBonus() =>
        GetBuilding(BuildingType.Canteen)?.MoodRecoveryPerHour ?? 0m;

    public decimal GetRestTimeReduction() =>
        GetBuilding(BuildingType.Canteen)?.RestTimeReduction ?? 0m;

    public decimal GetMaterialCostReduction() =>
        GetBuilding(BuildingType.Storage)?.MaterialCostReduction ?? 0m;

    public int GetExtraOrderSlots() =>
        GetBuilding(BuildingType.Office)?.ExtraOrderSlots ?? 0;

    public decimal GetDailyReputationGain() =>
        GetBuilding(BuildingType.Showroom)?.DailyReputationGain ?? 0m;

    public decimal GetTrainingSpeedMultiplier() =>
        GetBuilding(BuildingType.TrainingCenter)?.TrainingSpeedMultiplier ?? 1.0m;

    public decimal GetOrderRewardBonus() =>
        GetBuilding(BuildingType.VehicleFleet)?.OrderRewardBonus ?? 0m;

    public int GetExtraWorkerSlots() =>
        GetBuilding(BuildingType.WorkshopExtension)?.ExtraWorkerSlots ?? 0;
}
