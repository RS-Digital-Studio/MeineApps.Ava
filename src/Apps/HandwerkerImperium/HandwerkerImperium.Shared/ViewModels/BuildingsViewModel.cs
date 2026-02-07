using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the building management screen.
/// Shows all 7 building types with their current level, cost, and effects.
/// Allows building (level 0->1) and upgrading (level 1->5).
/// </summary>
public partial class BuildingsViewModel : ObservableObject
{
    private readonly IBuildingService _buildingService;
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event EventHandler<string>? NavigationRequested;

    /// <summary>
    /// Event to show an alert dialog. Parameters: title, message, buttonText.
    /// </summary>
    public event Action<string, string, string>? AlertRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private List<BuildingDisplayItem> _buildings = [];

    [ObservableProperty]
    private string _currentBalance = "0 \u20AC";

    [ObservableProperty]
    private string _title = string.Empty;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public BuildingsViewModel(
        IBuildingService buildingService,
        IGameStateService gameStateService,
        ILocalizationService localizationService)
    {
        _buildingService = buildingService;
        _gameStateService = gameStateService;
        _localizationService = localizationService;

        UpdateLocalizedTexts();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Loads all 7 building types and populates the display list.
    /// </summary>
    public void LoadBuildings()
    {
        var state = _gameStateService.State;
        CurrentBalance = MoneyFormatter.Format(state.Money, 2);

        var items = new List<BuildingDisplayItem>();

        foreach (var type in Enum.GetValues<BuildingType>())
        {
            var building = _buildingService.GetBuilding(type);
            var unlockLevel = type.GetUnlockLevel();
            var isLocked = state.PlayerLevel < unlockLevel;
            var maxLevel = type.GetMaxLevel();

            decimal cost;
            if (building == null || !building.IsBuilt)
            {
                cost = type.GetBaseCost();
            }
            else if (building.Level < maxLevel)
            {
                cost = building.NextLevelCost;
            }
            else
            {
                cost = 0m;
            }

            items.Add(new BuildingDisplayItem
            {
                Type = type,
                Name = _localizationService.GetString(type.GetLocalizationKey()),
                Icon = type.GetIcon(),
                Level = building?.Level ?? 0,
                MaxLevel = maxLevel,
                IsBuilt = building?.IsBuilt ?? false,
                IsLocked = isLocked,
                UnlockLevel = unlockLevel,
                Cost = cost,
                CostDisplay = cost > 0 ? MoneyFormatter.Format(cost, 0) : _localizationService.GetString("MaxLevel"),
                CanAfford = cost > 0 && _gameStateService.CanAfford(cost) && !isLocked,
                EffectDescription = GetEffectDescription(type, building),
                Description = _localizationService.GetString(type.GetDescriptionKey())
            });
        }

        Buildings = items;
    }

    /// <summary>
    /// Updates localized texts after language change.
    /// </summary>
    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("Buildings");
        LoadBuildings();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void Build(BuildingType type)
    {
        bool success = _buildingService.TryBuildBuilding(type);
        if (success)
        {
            LoadBuildings();
            AlertRequested?.Invoke(
                _localizationService.GetString("BuildingBuilt"),
                string.Format(
                    _localizationService.GetString("BuildingBuiltFormat"),
                    _localizationService.GetString(type.GetLocalizationKey())),
                _localizationService.GetString("Great"));
        }
        else
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("NotEnoughMoney"),
                _localizationService.GetString("NotEnoughMoneyDesc"),
                "OK");
        }
    }

    [RelayCommand]
    private void Upgrade(BuildingType type)
    {
        bool success = _buildingService.TryUpgradeBuilding(type);
        if (success)
        {
            LoadBuildings();
            var building = _buildingService.GetBuilding(type);
            AlertRequested?.Invoke(
                _localizationService.GetString("BuildingUpgraded"),
                string.Format(
                    _localizationService.GetString("BuildingUpgradedFormat"),
                    _localizationService.GetString(type.GetLocalizationKey()),
                    building?.Level ?? 0),
                _localizationService.GetString("Great"));
        }
        else
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("NotEnoughMoney"),
                _localizationService.GetString("NotEnoughMoneyDesc"),
                "OK");
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke(this, "..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private string GetEffectDescription(BuildingType type, Building? building)
    {
        if (building == null || !building.IsBuilt)
        {
            return _localizationService.GetString(type.GetEffectKey());
        }

        return type switch
        {
            BuildingType.Canteen => string.Format(
                _localizationService.GetString("CanteenEffectFormat"),
                building.MoodRecoveryPerHour,
                building.RestTimeReduction * 100),
            BuildingType.Storage => string.Format(
                _localizationService.GetString("StorageEffectFormat"),
                building.MaterialCostReduction * 100),
            BuildingType.Office => string.Format(
                _localizationService.GetString("OfficeEffectFormat"),
                building.ExtraOrderSlots),
            BuildingType.Showroom => string.Format(
                _localizationService.GetString("ShowroomEffectFormat"),
                building.DailyReputationGain),
            BuildingType.TrainingCenter => string.Format(
                _localizationService.GetString("TrainingCenterEffectFormat"),
                building.TrainingSpeedMultiplier),
            BuildingType.VehicleFleet => string.Format(
                _localizationService.GetString("VehicleFleetEffectFormat"),
                building.OrderRewardBonus * 100),
            BuildingType.WorkshopExtension => string.Format(
                _localizationService.GetString("WorkshopExtensionEffectFormat"),
                building.ExtraWorkerSlots),
            _ => string.Empty
        };
    }
}

/// <summary>
/// Display item for a building in the buildings list.
/// </summary>
public class BuildingDisplayItem
{
    public BuildingType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int Level { get; set; }
    public int MaxLevel { get; set; }
    public bool IsBuilt { get; set; }
    public bool IsLocked { get; set; }
    public int UnlockLevel { get; set; }
    public decimal Cost { get; set; }
    public string CostDisplay { get; set; } = string.Empty;
    public bool CanAfford { get; set; }
    public string EffectDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Opacity: locked = 0.4, unlocked = 1.0.
    /// </summary>
    public double DisplayOpacity => IsLocked ? 0.4 : 1.0;

    /// <summary>
    /// Whether the building is at max level and cannot be upgraded further.
    /// </summary>
    public bool IsMaxLevel => IsBuilt && Level >= MaxLevel;

    /// <summary>
    /// Level display string (e.g. "Lv.3/5").
    /// </summary>
    public string LevelDisplay => IsBuilt ? $"Lv.{Level}/{MaxLevel}" : string.Empty;
}
