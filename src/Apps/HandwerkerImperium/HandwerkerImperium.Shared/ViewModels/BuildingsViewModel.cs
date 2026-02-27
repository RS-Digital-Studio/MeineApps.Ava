using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel für die Gebäude-Verwaltung.
/// Zeigt alle 7 Gebäude-Typen mit Level, Kosten und Effekten.
/// Erlaubt direktes Bauen (Lv.0→1) und Upgraden (Lv.1→5).
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
    /// Event für Alert-Dialog. Parameter: title, message, buttonText.
    /// </summary>
    public event Action<string, string, string>? AlertRequested;

    /// <summary>
    /// Event für FloatingText (leichtgewichtiges Feedback). Parameter: text, style.
    /// </summary>
    public event Action<string, string>? FloatingTextRequested;

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
    // BERECHNETE PROPERTIES (für ImperiumView)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Freigeschaltete Gebäude (für Full-Width Karten in der ImperiumView).
    /// </summary>
    public IEnumerable<BuildingDisplayItem> UnlockedBuildings =>
        Buildings.Where(b => !b.IsLocked);

    /// <summary>
    /// Gesperrte Gebäude (für kompaktes Grid in der ImperiumView).
    /// </summary>
    public IEnumerable<BuildingDisplayItem> LockedBuildings =>
        Buildings.Where(b => b.IsLocked);

    /// <summary>
    /// "3/7 Gebäude" Anzeige.
    /// </summary>
    public string BuildingsCountDisplay =>
        $"{Buildings.Count(b => b.IsBuilt)}/{Buildings.Count}";

    /// <summary>
    /// True wenn es gesperrte Gebäude gibt.
    /// </summary>
    public bool HasLockedBuildings => Buildings.Any(b => b.IsLocked);

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
    /// Lädt alle 7 Gebäude-Typen und aktualisiert die Anzeige-Listen.
    /// </summary>
    public void LoadBuildings()
    {
        var state = _gameStateService.State;
        CurrentBalance = MoneyFormatter.Format(state.Money, 2);

        var items = new List<BuildingDisplayItem>();
        var buildLabel = _localizationService.GetString("Build") ?? "Bauen";
        var upgradeLabel = _localizationService.GetString("Upgrade") ?? "Upgrade";
        var maxLevelLabel = _localizationService.GetString("MaxLevel") ?? "Max";

        foreach (var type in Enum.GetValues<BuildingType>())
        {
            var building = _buildingService.GetBuilding(type);
            var unlockLevel = type.GetUnlockLevel();
            var isLocked = state.PlayerLevel < unlockLevel;
            var maxLevel = type.GetMaxLevel();
            var isBuilt = building?.IsBuilt ?? false;
            var isMaxLevel = isBuilt && (building?.Level ?? 0) >= maxLevel;

            decimal cost;
            if (!isBuilt)
                cost = type.GetBaseCost();
            else if (!isMaxLevel)
                cost = building!.NextLevelCost;
            else
                cost = 0m;

            var canAfford = cost > 0 && _gameStateService.CanAfford(cost) && !isLocked;

            items.Add(new BuildingDisplayItem
            {
                Type = type,
                Name = _localizationService.GetString(type.GetLocalizationKey()),
                Icon = type.GetIcon(),
                Level = building?.Level ?? 0,
                MaxLevel = maxLevel,
                IsBuilt = isBuilt,
                IsLocked = isLocked,
                UnlockLevel = unlockLevel,
                Cost = cost,
                CostDisplay = cost > 0 ? MoneyFormatter.Format(cost, 0) : maxLevelLabel,
                CanAfford = canAfford,
                EffectDescription = GetEffectDescription(type, building),
                Description = _localizationService.GetString(type.GetDescriptionKey()),
                // Neue Properties für ImperiumView
                ActionText = isMaxLevel ? maxLevelLabel : (isBuilt ? upgradeLabel : buildLabel),
                ShowActionButton = !isLocked && !isMaxLevel,
                CostForeground = canAfford ? "#22C55E" : "#6B7280",
                DisplayText = isBuilt
                    ? GetEffectDescription(type, building)
                    : _localizationService.GetString(type.GetDescriptionKey())
            });
        }

        Buildings = items;

        // Berechnete Properties benachrichtigen
        OnPropertyChanged(nameof(UnlockedBuildings));
        OnPropertyChanged(nameof(LockedBuildings));
        OnPropertyChanged(nameof(BuildingsCountDisplay));
        OnPropertyChanged(nameof(HasLockedBuildings));
    }

    /// <summary>
    /// Aktualisiert lokalisierte Texte nach Sprachwechsel.
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
            FloatingTextRequested?.Invoke(
                string.Format(
                    _localizationService.GetString("BuildingBuiltFormat"),
                    _localizationService.GetString(type.GetLocalizationKey())),
                "Success");
        }
        else
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("NotEnoughMoney"),
                _localizationService.GetString("NotEnoughMoneyDesc"),
                _localizationService.GetString("OK") ?? "OK");
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
            FloatingTextRequested?.Invoke(
                string.Format(
                    _localizationService.GetString("BuildingUpgradedFormat"),
                    _localizationService.GetString(type.GetLocalizationKey()),
                    building?.Level ?? 0),
                "Success");
        }
        else
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("NotEnoughMoney"),
                _localizationService.GetString("NotEnoughMoneyDesc"),
                _localizationService.GetString("OK") ?? "OK");
        }
    }

    /// <summary>
    /// Intelligenter Build-oder-Upgrade Command für inline ImperiumView.
    /// </summary>
    [RelayCommand]
    private void BuildOrUpgrade(BuildingType type)
    {
        var building = _buildingService.GetBuilding(type);
        if (building?.IsBuilt == true)
            Upgrade(type);
        else
            Build(type);
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
/// Anzeige-Element für ein Gebäude.
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

    // ═══════════════════════════════════════════════════════════════════════
    // Neue Properties für ImperiumView Inline-Karten
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Button-Text: "Bauen", "Upgrade" oder "Max".
    /// </summary>
    public string ActionText { get; set; } = string.Empty;

    /// <summary>
    /// Ob der Build/Upgrade-Button sichtbar sein soll.
    /// False bei gesperrten und Max-Level Gebäuden.
    /// </summary>
    public bool ShowActionButton { get; set; }

    /// <summary>
    /// Farbe des Preises: Grün wenn leistbar, Grau wenn nicht.
    /// </summary>
    public string CostForeground { get; set; } = "#6B7280";

    /// <summary>
    /// Anzeigetext: Beschreibung (nicht gebaut) oder Effekt (gebaut).
    /// </summary>
    public string DisplayText { get; set; } = string.Empty;

    /// <summary>
    /// Opacity: locked = 0.4, unlocked = 1.0.
    /// </summary>
    public double DisplayOpacity => IsLocked ? 0.4 : 1.0;

    /// <summary>
    /// Max-Level erreicht.
    /// </summary>
    public bool IsMaxLevel => IsBuilt && Level >= MaxLevel;

    /// <summary>
    /// Level-Anzeige (z.B. "Lv.3/5").
    /// </summary>
    public string LevelDisplay => IsBuilt ? $"Lv.{Level}/{MaxLevel}" : string.Empty;
}
