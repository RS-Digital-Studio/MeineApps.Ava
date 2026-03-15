using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using HandwerkerImperium.Helpers;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Sub-ViewModel für das interaktive Gilden-Hauptquartier.
/// Zeigt 10 Gebäude mit Leveln, Upgrades und Effekten.
/// </summary>
public sealed partial class GuildHallViewModel : ViewModelBase
{
    private readonly IGuildHallService _hallService;
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;
    private bool _isBusy;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;
    public event Action? CelebrationRequested;
    public event EventHandler<(string Text, string Type)>? FloatingTextRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Hallen-Übersicht
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _hallLevel;

    [ObservableProperty]
    private string _hallLevelDisplay = "";

    [ObservableProperty]
    private string _hallEffectsSummary = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Gebäude
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<GuildBuildingDisplay> _buildings = [];

    [ObservableProperty]
    private bool _hasBuildings;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Status
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isLoading;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public GuildHallViewModel(
        IGuildHallService hallService,
        IGameStateService gameStateService,
        ILocalizationService localizationService)
    {
        _hallService = hallService;
        _gameStateService = gameStateService;
        _localizationService = localizationService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task LoadHallDataAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        IsLoading = true;
        try
        {
            // Gebäude-Upgrades prüfen
            await _hallService.CheckUpgradeCompletionAsync();

            // Gebäude laden
            var buildingList = await _hallService.GetBuildingsAsync();
            Buildings = new ObservableCollection<GuildBuildingDisplay>(buildingList);
            HasBuildings = Buildings.Count > 0;

            // Hallen-Level
            HallLevel = _hallService.GetHallLevel();
            var hallLabel = _localizationService.GetString("GuildHallLevel") ?? "Halle Lv.{0}";
            HallLevelDisplay = string.Format(hallLabel, HallLevel);

            // Effekte-Zusammenfassung
            var effects = _hallService.GetCachedEffects();
            var parts = new List<string>();
            if (effects.IncomeBonus > 0)
                parts.Add($"+{effects.IncomeBonus:P0} {_localizationService.GetString("Income") ?? "Einkommen"}");
            if (effects.CraftingSpeedBonus > 0)
                parts.Add($"+{effects.CraftingSpeedBonus:P0} {_localizationService.GetString("Crafting") ?? "Handwerk"}");
            if (effects.OrderRewardBonus > 0)
                parts.Add($"+{effects.OrderRewardBonus:P0} {_localizationService.GetString("OrderReward") ?? "Auftragsbelohnung"}");
            if (effects.EverythingBonus > 0)
                parts.Add($"+{effects.EverythingBonus:P0} {_localizationService.GetString("Everything") ?? "Alles"}");
            HallEffectsSummary = parts.Count > 0 ? string.Join(", ", parts) : "";
        }
        catch
        {
            MessageRequested?.Invoke(
                _localizationService.GetString("Error") ?? "Fehler",
                _localizationService.GetString("GuildHallLoadError") ?? "Hauptquartier konnte nicht geladen werden.");
        }
        finally
        {
            IsLoading = false;
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task UpgradeBuildingAsync(string buildingIdStr)
    {
        if (_isBusy) return;
        if (!Enum.TryParse<GuildBuildingId>(buildingIdStr, out var buildingId)) return;

        _isBusy = true;
        try
        {
            var success = await _hallService.UpgradeBuildingAsync(buildingId);
            if (success)
            {
                var buildingName = Buildings.FirstOrDefault(b => b.BuildingId == buildingId)?.Name ?? "";
                FloatingTextRequested?.Invoke(this,
                    ($"{buildingName} Upgrade!", "golden_screws"));
                CelebrationRequested?.Invoke();

                // Daten neu laden
                await LoadHallDataInternalAsync();
            }
            else
            {
                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Innung",
                    _localizationService.GetString("GuildHallUpgradeFailed") ?? "Upgrade nicht möglich (Kosten oder Voraussetzungen nicht erfüllt).");
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private void NavigateBack() => NavigationRequested?.Invoke("..");

    // ═══════════════════════════════════════════════════════════════════════
    // METHODEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Lädt die Hallen-Daten neu (für Hub-Aufruf).</summary>
    public void RefreshHall() => LoadHallDataAsync().SafeFireAndForget();

    /// <summary>Gibt den Quick-Status für den Guild-Hub zurück.</summary>
    public string GetQuickStatus()
    {
        var label = _localizationService.GetString("GuildHallShort") ?? "Halle";
        return $"{label} Lv.{HallLevel}";
    }

    public void UpdateLocalizedTexts()
    {
        if (HasBuildings)
            RefreshHall();
    }

    /// <summary>Interne Lade-Logik ohne isBusy-Guard.</summary>
    private async Task LoadHallDataInternalAsync()
    {
        var buildingList = await _hallService.GetBuildingsAsync();
        Buildings = new ObservableCollection<GuildBuildingDisplay>(buildingList);
        HasBuildings = Buildings.Count > 0;
        HallLevel = _hallService.GetHallLevel();
        var hallLabel = _localizationService.GetString("GuildHallLevel") ?? "Halle Lv.{0}";
        HallLevelDisplay = string.Format(hallLabel, HallLevel);
    }
}
