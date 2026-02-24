using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel für die Vorarbeiter/Manager-Verwaltung.
/// Zeigt alle Manager (freigeschaltete und gesperrte) mit Upgrade-Möglichkeiten.
/// </summary>
public partial class ManagerViewModel : ObservableObject
{
    private readonly IGameStateService _gameStateService;
    private readonly IManagerService _managerService;
    private readonly ILocalizationService _localizationService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private ObservableCollection<ManagerDisplayModel> _managers = [];

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public ManagerViewModel(
        IGameStateService gameStateService,
        IManagerService managerService,
        ILocalizationService localizationService)
    {
        _gameStateService = gameStateService;
        _managerService = managerService;
        _localizationService = localizationService;

        UpdateLocalizedTexts();
        RefreshManagers();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void UpgradeManager(string? managerId)
    {
        if (string.IsNullOrEmpty(managerId)) return;

        _managerService.UpgradeManager(managerId);
        RefreshManagers();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert die Manager-Anzeigeliste aus dem State + allen Definitionen.
    /// </summary>
    public void RefreshManagers()
    {
        var state = _gameStateService.State;
        var definitions = Manager.GetAllDefinitions();
        var items = new ObservableCollection<ManagerDisplayModel>();

        foreach (var def in definitions)
        {
            var manager = state.Managers.FirstOrDefault(m => m.Id == def.Id);
            bool isUnlocked = manager?.IsUnlocked ?? false;
            int level = manager?.Level ?? 0;
            int upgradeCost = manager?.UpgradeCost ?? 10;
            bool canUpgrade = isUnlocked && !((manager?.IsMaxLevel) ?? false)
                              && _gameStateService.CanAffordGoldenScrews(upgradeCost);

            // Workshop-Name bestimmen
            string workshopName = def.Workshop.HasValue
                ? _localizationService.GetString(def.Workshop.Value.GetLocalizationKey())
                : _localizationService.GetString("AllWorkshops") ?? "Alle";

            // Fähigkeits-Name lokalisieren
            string abilityName = GetAbilityName(def.Ability);

            // Bonus-Anzeige
            string bonusDisplay = isUnlocked && manager != null
                ? FormatBonus(def.Ability, manager.GetBonus(def.Ability))
                : "-";

            items.Add(new ManagerDisplayModel
            {
                Id = def.Id,
                Name = _localizationService.GetString(def.NameKey) ?? def.NameKey,
                WorkshopName = workshopName,
                AbilityName = abilityName,
                Level = level,
                LevelDisplay = isUnlocked ? $"Lv.{level}" : "---",
                IsUnlocked = isUnlocked,
                CanUpgrade = canUpgrade,
                UpgradeCostDisplay = isUnlocked && !((manager?.IsMaxLevel) ?? false)
                    ? $"{upgradeCost}"
                    : "",
                BonusDisplay = bonusDisplay
            });
        }

        Managers = items;
    }

    /// <summary>
    /// Lokalisierte Texte aktualisieren (nach Sprachwechsel).
    /// </summary>
    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("Managers") ?? "Vorarbeiter";
        RefreshManagers();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private string GetAbilityName(ManagerAbility ability) => ability switch
    {
        ManagerAbility.AutoCollectOrders => _localizationService.GetString("AbilityAutoCollect") ?? "Auto-Aufträge",
        ManagerAbility.EfficiencyBoost => _localizationService.GetString("AbilityEfficiency") ?? "Effizienz",
        ManagerAbility.FatigueReduction => _localizationService.GetString("AbilityFatigue") ?? "Ermüdung",
        ManagerAbility.MoodBoost => _localizationService.GetString("AbilityMood") ?? "Stimmung",
        ManagerAbility.IncomeBoost => _localizationService.GetString("AbilityIncome") ?? "Einkommen",
        ManagerAbility.TrainingSpeedUp => _localizationService.GetString("AbilityTraining") ?? "Training",
        _ => ability.ToString()
    };

    private static string FormatBonus(ManagerAbility ability, decimal bonus) => ability switch
    {
        ManagerAbility.AutoCollectOrders => $"{bonus:F0}x",
        _ => $"+{bonus * 100:F0}%"
    };
}

// ═══════════════════════════════════════════════════════════════════════════════
// DISPLAY MODEL
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Anzeige-Modell für einen Manager im UI.
/// </summary>
public class ManagerDisplayModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string WorkshopName { get; set; } = "";
    public string AbilityName { get; set; } = "";
    public int Level { get; set; }
    public string LevelDisplay { get; set; } = "";
    public bool IsUnlocked { get; set; }
    public bool CanUpgrade { get; set; }
    public string UpgradeCostDisplay { get; set; } = "";
    public string BonusDisplay { get; set; } = "";

    /// <summary>
    /// Opacity: Gesperrte Manager leicht gedimmt.
    /// </summary>
    public double DisplayOpacity => IsUnlocked ? 1.0 : 0.5;
}
