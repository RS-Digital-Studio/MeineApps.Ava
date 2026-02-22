using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Models.Dungeon;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für den Dungeon-Run Roguelike-Modus.
/// 3 Zustände: PreRun (Start-Screen), BuffSelection (nach Floor-Abschluss), PostRun (Zusammenfassung).
/// </summary>
public partial class DungeonViewModel : ObservableObject
{
    private readonly IDungeonService _dungeonService;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ILocalizationService _localizationService;

    public event Action<string>? NavigationRequested;
    public event EventHandler<(string type, string text)>? FloatingTextRequested;
    public event EventHandler? CelebrationRequested;

    /// <summary>Event für Ad-basierte Runs (ViewModel weiß nicht ob Ad erfolgreich)</summary>
    public event Action? AdRunRequested;

    /// <summary>Event für Dungeon-Floor-Start (floor, seed)</summary>
    public event Action<int, int>? StartDungeonFloorRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty] private string _title = "Dungeon Run";
    [ObservableProperty] private string _currentFloorText = "Floor 1";
    [ObservableProperty] private string _livesText = "1";
    [ObservableProperty] private string _totalRunsText = "0";
    [ObservableProperty] private string _bestFloorText = "0";
    [ObservableProperty] private string _startButtonText = "Starten (Gratis)";
    [ObservableProperty] private string _coinEntryText = "500 Coins";
    [ObservableProperty] private string _gemEntryText = "10 Gems";
    [ObservableProperty] private string _adEntryText = "Werbung";
    [ObservableProperty] private bool _canStartFree;
    [ObservableProperty] private bool _canStartPaid = true;
    [ObservableProperty] private bool _canStartAd = true;
    [ObservableProperty] private bool _isPreRun = true;
    [ObservableProperty] private bool _isBuffSelection;
    [ObservableProperty] private bool _isPostRun;
    [ObservableProperty] private List<DungeonBuffDisplayItem> _buffChoices = [];
    [ObservableProperty] private string _summaryFloorsText = "";
    [ObservableProperty] private string _summaryCoinsText = "";
    [ObservableProperty] private string _summaryGemsText = "";
    [ObservableProperty] private string _summaryCardsText = "";
    [ObservableProperty] private string _summaryNewBestText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public DungeonViewModel(
        IDungeonService dungeonService,
        ICoinService coinService,
        IGemService gemService,
        ILocalizationService localizationService)
    {
        _dungeonService = dungeonService;
        _coinService = coinService;
        _gemService = gemService;
        _localizationService = localizationService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    public void OnAppearing()
    {
        RefreshState();
        UpdateLocalizedTexts();
    }

    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("DungeonTitle") ?? "Dungeon Run";
        StartButtonText = _localizationService.GetString("DungeonStartFree") ?? "Starten (Gratis)";
        CoinEntryText = string.Format(
            _localizationService.GetString("DungeonStartCoins") ?? "{0} Coins",
            _dungeonService.PaidRunCoinCost);
        GemEntryText = string.Format(
            _localizationService.GetString("DungeonStartGems") ?? "{0} Gems",
            _dungeonService.PaidRunGemCost);
        AdEntryText = _localizationService.GetString("DungeonStartAd") ?? "Werbung";

        // Stats-Texte aktualisieren
        var stats = _dungeonService.Stats;
        TotalRunsText = string.Format(
            _localizationService.GetString("DungeonTotalRuns") ?? "Runs: {0}", stats.TotalRuns);
        BestFloorText = string.Format(
            _localizationService.GetString("DungeonBestFloor") ?? "Bester Floor: {0}", stats.BestFloor);

        // Aktiver Run-Zustand
        var runState = _dungeonService.RunState;
        if (runState is { IsActive: true })
        {
            CurrentFloorText = string.Format(
                _localizationService.GetString("DungeonFloor") ?? "Floor {0}", runState.CurrentFloor);
            LivesText = string.Format(
                _localizationService.GetString("DungeonLives") ?? "Leben: {0}", runState.Lives);
        }
        else
        {
            CurrentFloorText = string.Format(
                _localizationService.GetString("DungeonFloor") ?? "Floor {0}", 1);
            LivesText = string.Format(
                _localizationService.GetString("DungeonLives") ?? "Leben: {0}", 1);
        }
    }

    public void RefreshState()
    {
        // CanStart-Flags aktualisieren
        CanStartFree = _dungeonService.CanStartFreeRun;
        CanStartPaid = _coinService.CanAfford(_dungeonService.PaidRunCoinCost)
                       || _gemService.CanAfford(_dungeonService.PaidRunGemCost);
        CanStartAd = _dungeonService.CanStartAdRun;

        // Stats
        var stats = _dungeonService.Stats;
        TotalRunsText = string.Format(
            _localizationService.GetString("DungeonTotalRuns") ?? "Runs: {0}", stats.TotalRuns);
        BestFloorText = string.Format(
            _localizationService.GetString("DungeonBestFloor") ?? "Bester Floor: {0}", stats.BestFloor);

        // Zustand zurücksetzen auf PreRun wenn kein aktiver Run
        if (!_dungeonService.IsRunActive)
        {
            IsPreRun = true;
            IsBuffSelection = false;
            IsPostRun = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void StartFreeRun()
    {
        if (!_dungeonService.StartRun(DungeonEntryType.Free)) return;

        var runState = _dungeonService.RunState!;
        NavigateToFloor(runState.CurrentFloor, runState.RunSeed);
    }

    [RelayCommand]
    private void StartCoinRun()
    {
        if (!_dungeonService.StartRun(DungeonEntryType.Coins)) return;

        var runState = _dungeonService.RunState!;
        NavigateToFloor(runState.CurrentFloor, runState.RunSeed);
    }

    [RelayCommand]
    private void StartGemRun()
    {
        if (!_dungeonService.StartRun(DungeonEntryType.Gems)) return;

        var runState = _dungeonService.RunState!;
        NavigateToFloor(runState.CurrentFloor, runState.RunSeed);
    }

    [RelayCommand]
    private void StartAdRun()
    {
        // ViewModel weiß nicht ob Ad erfolgreich - Aufruf delegieren
        AdRunRequested?.Invoke();
    }

    /// <summary>
    /// Wird extern aufgerufen wenn Ad erfolgreich war (z.B. aus MainViewModel)
    /// </summary>
    public void OnAdRunRewarded()
    {
        if (!_dungeonService.StartRun(DungeonEntryType.Ad)) return;

        var runState = _dungeonService.RunState!;
        NavigateToFloor(runState.CurrentFloor, runState.RunSeed);
    }

    [RelayCommand]
    private void SelectBuff(DungeonBuffType type)
    {
        _dungeonService.ApplyBuff(type);

        // Buff-Name für Feedback-Text suchen
        var buffDef = DungeonBuffCatalog.Find(type);
        if (buffDef != null)
        {
            var buffName = _localizationService.GetString(buffDef.NameKey) ?? buffDef.NameKey;
            FloatingTextRequested?.Invoke(this, ("success", $"+{buffName}"));
        }

        // Weiter zum nächsten Floor
        ContinueDungeon();
    }

    [RelayCommand]
    private void ContinueDungeon()
    {
        var runState = _dungeonService.RunState;
        if (runState is not { IsActive: true }) return;

        IsBuffSelection = false;
        NavigateToFloor(runState.CurrentFloor, runState.RunSeed);
    }

    [RelayCommand]
    private void Back() => NavigationRequested?.Invoke("..");

    // ═══════════════════════════════════════════════════════════════════════
    // DUNGEON-FLOOR CALLBACKS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wird von MainViewModel aufgerufen wenn ein Dungeon-Floor abgeschlossen wurde.
    /// Prüft ob Buff-Auswahl nötig ist oder direkt weiter.
    /// </summary>
    public void OnDungeonFloorComplete()
    {
        // Floor-Belohnung berechnen und zum nächsten Floor vorrücken
        var reward = _dungeonService.CompleteFloor();

        // Belohnungs-Feedback
        if (reward.Coins > 0)
            FloatingTextRequested?.Invoke(this, ("coin", $"+{reward.Coins} Coins"));
        if (reward.Gems > 0)
            FloatingTextRequested?.Invoke(this, ("gem", $"+{reward.Gems} Gems"));
        if (reward.WasBossFloor)
            CelebrationRequested?.Invoke(this, EventArgs.Empty);

        // Prüfen ob Buff-Floor
        if (_dungeonService.IsBuffFloorNext)
        {
            var choices = _dungeonService.GenerateBuffChoices();
            ShowBuffSelection(choices);
        }
        else
        {
            // Direkt zum nächsten Floor weiter
            ContinueDungeon();
        }
    }

    /// <summary>
    /// Wird von MainViewModel aufgerufen wenn der Spieler im Dungeon stirbt/aufgibt.
    /// </summary>
    public void OnDungeonRunEnded()
    {
        var summary = _dungeonService.EndRun();
        ShowPostRun(summary);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STATE-MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeigt die Buff-Auswahl nach einem Floor-Abschluss an
    /// </summary>
    public void ShowBuffSelection(List<DungeonBuffDefinition> choices)
    {
        IsPreRun = false;
        IsBuffSelection = true;
        IsPostRun = false;

        // DungeonBuffDefinition → DungeonBuffDisplayItem konvertieren
        var items = new List<DungeonBuffDisplayItem>(choices.Count);
        foreach (var choice in choices)
        {
            items.Add(new DungeonBuffDisplayItem
            {
                Type = choice.Type,
                Name = _localizationService.GetString(choice.NameKey) ?? choice.NameKey,
                Description = _localizationService.GetString(choice.DescKey) ?? choice.DescKey,
                IconName = choice.IconName,
                RarityText = GetLocalizedRarityText(choice.Rarity),
                RarityColor = GetRarityColor(choice.Rarity)
            });
        }

        BuffChoices = items;
    }

    /// <summary>
    /// Zeigt die Run-Zusammenfassung nach Tod oder Aufgabe
    /// </summary>
    public void ShowPostRun(DungeonRunSummary summary)
    {
        IsPreRun = false;
        IsBuffSelection = false;
        IsPostRun = true;

        SummaryFloorsText = string.Format(
            _localizationService.GetString("DungeonSummaryFloors") ?? "Floors: {0}", summary.FloorsCompleted);
        SummaryCoinsText = string.Format(
            _localizationService.GetString("DungeonSummaryCoins") ?? "Coins: {0}", summary.TotalCoins);
        SummaryGemsText = string.Format(
            _localizationService.GetString("DungeonSummaryGems") ?? "Gems: {0}", summary.TotalGems);
        SummaryCardsText = string.Format(
            _localizationService.GetString("DungeonSummaryCards") ?? "Karten: {0}", summary.TotalCards);
        SummaryNewBestText = summary.IsNewBestFloor
            ? _localizationService.GetString("DungeonNewBest") ?? "Neuer Rekord!"
            : "";

        if (summary.IsNewBestFloor)
            CelebrationRequested?.Invoke(this, EventArgs.Empty);

        // Stats aktualisieren
        RefreshState();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void NavigateToFloor(int floor, int seed)
    {
        StartDungeonFloorRequested?.Invoke(floor, seed);
        NavigationRequested?.Invoke($"Game?mode=dungeon&floor={floor}&seed={seed}");
    }

    private string GetLocalizedRarityText(DungeonBuffRarity rarity) => rarity switch
    {
        DungeonBuffRarity.Common => _localizationService.GetString("RarityCommon") ?? "Common",
        DungeonBuffRarity.Rare => _localizationService.GetString("RarityRare") ?? "Rare",
        DungeonBuffRarity.Epic => _localizationService.GetString("RarityEpic") ?? "Epic",
        _ => ""
    };

    private static Avalonia.Media.Color GetRarityColor(DungeonBuffRarity rarity) => rarity switch
    {
        DungeonBuffRarity.Common => Avalonia.Media.Color.Parse("#FFFFFF"),
        DungeonBuffRarity.Rare => Avalonia.Media.Color.Parse("#2196F3"),
        DungeonBuffRarity.Epic => Avalonia.Media.Color.Parse("#9C27B0"),
        _ => Avalonia.Media.Color.Parse("#FFFFFF")
    };
}

// ═══════════════════════════════════════════════════════════════════════
// HILFSKLASSE
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Display-Item für einen Dungeon-Buff in der Auswahl-Ansicht
/// </summary>
public class DungeonBuffDisplayItem
{
    public DungeonBuffType Type { get; init; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconName { get; init; } = "";
    public string RarityText { get; set; } = "";
    public Avalonia.Media.Color RarityColor { get; init; }
}
