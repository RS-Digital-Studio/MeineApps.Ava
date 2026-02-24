using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the worker hiring market.
/// Shows available workers with tier badges, personality, talent stars, specialization, and wage.
/// Pool rotates every 4 hours with countdown timer.
/// </summary>
public partial class WorkerMarketViewModel : ObservableObject
{
    private readonly IWorkerService _workerService;
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;
    private readonly IRewardedAdService _rewardedAdService;

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
    private List<Worker> _availableWorkers = [];

    [ObservableProperty]
    private string _timeUntilRotation = "--:--:--";

    [ObservableProperty]
    private Worker? _selectedWorker;

    [ObservableProperty]
    private string _currentBalance = "0 €";

    [ObservableProperty]
    private string _goldenScrewsDisplay = "0";

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _hireButtonText = string.Empty;

    [ObservableProperty]
    private string _refreshButtonText = string.Empty;

    /// <summary>
    /// Ob der Gratis-Refresh noch verfügbar ist (1x pro Rotation).
    /// </summary>
    [ObservableProperty]
    private bool _hasFreeRefresh;

    [ObservableProperty]
    private string _nextRotationLabel = string.Empty;

    [ObservableProperty]
    private bool _canHire;

    [ObservableProperty]
    private bool _hasAvailableSlots;

    [ObservableProperty]
    private string _noSlotsMessage = string.Empty;

    /// <summary>
    /// Ob es volle Workshops gibt, denen ein Extra-Slot per Ad hinzugefuegt werden kann.
    /// </summary>
    public bool HasFullWorkshops => _gameStateService.State.Workshops
        .Any(w => _gameStateService.State.IsWorkshopUnlocked(w.Type) &&
                  w.Workers.Count >= w.MaxWorkers);

    /// <summary>
    /// Ob der Extra-Slot-Button sichtbar sein soll.
    /// </summary>
    public bool ShowExtraSlotButton => HasFullWorkshops && !HasAvailableSlots;

    // Workshop-Auswahl Properties (Bug 3: Spieler waehlt Workshop beim Einstellen)
    [ObservableProperty]
    private bool _showWorkshopSelection;

    [ObservableProperty]
    private Worker? _pendingWorker;

    [ObservableProperty]
    private List<WorkshopSelectionItem> _workshopSelections = [];

    [ObservableProperty]
    private string _selectWorkshopTitle = string.Empty;

    [ObservableProperty]
    private string _cancelText = string.Empty;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public WorkerMarketViewModel(
        IWorkerService workerService,
        IGameStateService gameStateService,
        ILocalizationService localizationService,
        IRewardedAdService rewardedAdService)
    {
        _workerService = workerService;
        _gameStateService = gameStateService;
        _localizationService = localizationService;
        _rewardedAdService = rewardedAdService;

        UpdateLocalizedTexts();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Laedt den aktuellen Arbeitermarkt-Pool und aktualisiert alle Anzeige-Properties.
    /// Zeigt IMMER Arbeiter an, auch wenn keine Workshops mit freien Plaetzen existieren.
    /// Der Hire-Button wird dann disabled (Bug 1 Fix).
    /// </summary>
    public void LoadMarket()
    {
        var market = _workerService.GetWorkerMarket();
        CurrentBalance = MoneyFormatter.Format(_gameStateService.State.Money, 2);
        GoldenScrewsDisplay = _gameStateService.State.GoldenScrews.ToString("N0");

        // Pruefen ob Workshops mit freien Plaetzen existieren
        var workshopsWithSlots = _gameStateService.State.Workshops
            .Where(w => _gameStateService.State.IsWorkshopUnlocked(w.Type) &&
                        w.Workers.Count < w.MaxWorkers)
            .ToList();

        HasAvailableSlots = workshopsWithSlots.Count > 0;

        // Durchschnittliche BaseIncomePerWorker berechnen fuer Mehrwert-Anzeige
        var unlockedWorkshops = _gameStateService.State.Workshops
            .Where(w => _gameStateService.State.IsWorkshopUnlocked(w.Type))
            .ToList();
        var avgBaseIncome = unlockedWorkshops.Count > 0
            ? unlockedWorkshops.Average(w => w.BaseIncomePerWorker)
            : 1m;

        // MarketRestriction: Während WorkerStrike höhere Tiers nicht verfügbar
        var activeEvent = _gameStateService.State.ActiveEvent;
        var marketRestriction = activeEvent?.IsActive == true ? activeEvent.Effect.MarketRestriction : null;

        // Markt IMMER anzeigen, unabhaengig von freien Plaetzen
        var playerLevel = _gameStateService.State.PlayerLevel;
        var netIncomePerSecond = Math.Max(0m, _gameStateService.State.NetIncomePerSecond);
        var workers = market.AvailableWorkers.ToList();

        // Bei MarketRestriction: Nur Worker bis zur erlaubten Tier-Stufe anzeigen
        if (marketRestriction != null)
            workers = workers.Where(w => w.Tier <= marketRestriction.Value).ToList();
        foreach (var worker in workers)
        {
            // Individuelle Anstellungskosten (Tier + Level + Talent + Persönlichkeit + Spezialisierung + Effizienz)
            var qualityPrice = worker.CalculateMarketPrice(playerLevel);

            // Einkommensbasierter Mindestpreis: ~3min Netto-Einkommen * Tier-Stufe
            // Verhindert dass Worker bei hohem Einkommen "geschenkt" werden
            var tierMultiplier = 1.0m + (int)worker.Tier * 0.5m;
            var incomeFloor = netIncomePerSecond * 180m * tierMultiplier;

            worker.HiringCost = Math.Max(qualityPrice, Math.Round(incomeFloor));

            // Geschaetzter Ertrag basierend auf Durchschnitt aller Workshops
            worker.IncomeContribution = avgBaseIncome * worker.Efficiency;

            // Lokalisierte Anzeige-Texte setzen
            worker.PersonalityDisplay = _localizationService.GetString(worker.Personality.GetLocalizationKey());
            worker.SpecializationDisplay = worker.Specialization != null
                ? _localizationService.GetString(worker.Specialization.Value.GetLocalizationKey())
                : "";
        }
        // Sortierung: Beste Worker zuerst (Tier absteigend, dann Effizienz absteigend)
        workers = workers
            .OrderByDescending(w => (int)w.Tier)
            .ThenByDescending(w => w.Efficiency)
            .ToList();
        AvailableWorkers = workers;

        if (!HasAvailableSlots)
        {
            NoSlotsMessage = _localizationService.GetString("NoFreeSlotDesc");
        }

        UpdateTimer();
        UpdateFreeRefreshState();
        UpdateCanHire();
        OnPropertyChanged(nameof(HasFullWorkshops));
        OnPropertyChanged(nameof(ShowExtraSlotButton));
    }

    /// <summary>
    /// Updates the rotation countdown timer. Called every second from the game loop.
    /// </summary>
    public void UpdateTimer()
    {
        var market = _workerService.GetWorkerMarket();
        var remaining = market.TimeUntilRotation;

        if (remaining <= TimeSpan.Zero)
        {
            // Markt rotiert automatisch beim naechsten GetWorkerMarket-Aufruf
            LoadMarket();
            return;
        }

        TimeUntilRotation = $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    /// <summary>
    /// Updates localized texts after language change.
    /// </summary>
    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("WorkerMarket");
        HireButtonText = _localizationService.GetString("HireWorker");
        NextRotationLabel = _localizationService.GetString("NextRotation");
        SelectWorkshopTitle = _localizationService.GetString("SelectWorkshop");
        CancelText = _localizationService.GetString("Cancel");
        UpdateFreeRefreshState();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task RefreshWithAdAsync()
    {
        var market = _workerService.GetWorkerMarket();

        // Gratis-Refresh verfügbar? Direkt aktualisieren ohne Ad
        if (!market.FreeRefreshUsedThisRotation)
        {
            market.FreeRefreshUsedThisRotation = true;
            _gameStateService.MarkDirty();
            DoRefreshMarket();
            return;
        }

        // Sonst: Video-Werbung anzeigen
        var adWatched = await _rewardedAdService.ShowAdAsync("market_refresh");
        if (adWatched)
        {
            DoRefreshMarket();
        }
        else
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("Info"),
                _localizationService.GetString("WatchAdToRefresh"),
                _localizationService.GetString("OK") ?? "OK");
        }
    }

    /// <summary>
    /// Führt den Markt-Refresh durch und aktualisiert alle UI-Properties.
    /// Ruft LoadMarket() auf, damit Worker-Daten (IncomeContribution, PersonalityDisplay etc.) korrekt gesetzt werden.
    /// </summary>
    private void DoRefreshMarket()
    {
        _workerService.RefreshMarket();
        SelectedWorker = null;
        LoadMarket();
    }

    [RelayCommand]
    private async Task WatchAdForWorkerSlotAsync()
    {
        // Erste volle Workshop finden
        var fullWorkshop = _gameStateService.State.Workshops
            .FirstOrDefault(w => _gameStateService.State.IsWorkshopUnlocked(w.Type) &&
                                 w.Workers.Count >= w.MaxWorkers);

        if (fullWorkshop == null) return;

        var success = await _rewardedAdService.ShowAdAsync("worker_hire_bonus");
        if (success)
        {
            // Cap bei MaxAdBonusWorkerSlots pro Workshop (Exploit-Schutz)
            if (fullWorkshop.AdBonusWorkerSlots >= Workshop.MaxAdBonusWorkerSlots)
            {
                AlertRequested?.Invoke(
                    _localizationService.GetString("Info"),
                    _localizationService.GetString("MaxSlotReached"),
                    _localizationService.GetString("OK") ?? "OK");
                return;
            }

            fullWorkshop.AdBonusWorkerSlots += 1;
            _gameStateService.MarkDirty();
            LoadMarket();

            AlertRequested?.Invoke(
                _localizationService.GetString("WorkerSlotBonusDesc"),
                _localizationService.GetString(fullWorkshop.Type.GetLocalizationKey()),
                _localizationService.GetString("Great"));
        }
    }

    [RelayCommand]
    private void HireWorker(Worker? worker)
    {
        if (worker == null) return;

        var hiringCost = worker.HiringCost;

        if (!_gameStateService.CanAfford(hiringCost))
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("NotEnoughMoney"),
                string.Format(_localizationService.GetString("HiringCostFormat"), MoneyFormatter.Format(hiringCost, 0)),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        // Goldschrauben-Kosten pruefen (Tier A + S)
        var hiringScrewCost = worker.Tier.GetHiringScrewCost();
        if (hiringScrewCost > 0 && !_gameStateService.CanAffordGoldenScrews(hiringScrewCost))
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("NotEnoughScrews"),
                string.Format(_localizationService.GetString("NotEnoughScrewsDesc"), hiringScrewCost),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        // Workshops mit freien Plaetzen ermitteln
        var workshopsWithSlots = _gameStateService.State.Workshops
            .Where(w => w.IsUnlocked && w.Workers.Count < w.MaxWorkers)
            .ToList();

        if (workshopsWithSlots.Count == 0)
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("NoFreeSlot"),
                _localizationService.GetString("NoFreeSlotDesc"),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        // Bug 3 Fix: Workshop-Auswahl-Overlay anzeigen statt automatisch zuzuweisen
        PendingWorker = worker;
        WorkshopSelections = workshopsWithSlots.Select(ws => new WorkshopSelectionItem
        {
            Type = ws.Type,
            Name = _localizationService.GetString(ws.Type.GetLocalizationKey()),
            WorkerInfo = $"{ws.Workers.Count}/{ws.MaxWorkers} {_localizationService.GetString("Workers")}",
            HasFreeSlots = true
        }).ToList();

        ShowWorkshopSelection = true;
    }

    [RelayCommand]
    private void ConfirmWorkshopSelection(WorkshopSelectionItem? item)
    {
        if (item == null || PendingWorker == null) return;

        var worker = PendingWorker;

        if (_workerService.HireWorker(worker, item.Type))
        {
            // Worker aus Markt-Liste entfernen
            var updated = AvailableWorkers.Where(w => w.Id != worker.Id).ToList();
            AvailableWorkers = updated;
            SelectedWorker = null;
            CurrentBalance = MoneyFormatter.Format(_gameStateService.State.Money, 2);
            GoldenScrewsDisplay = _gameStateService.State.GoldenScrews.ToString("N0");
            UpdateCanHire();

            // Workshop-Auswahl schliessen
            ShowWorkshopSelection = false;
            PendingWorker = null;

            AlertRequested?.Invoke(
                _localizationService.GetString("WorkerHired"),
                string.Format(_localizationService.GetString("WorkerHiredFormat"), worker.Name),
                _localizationService.GetString("Great"));

            // HasAvailableSlots neu berechnen
            HasAvailableSlots = _gameStateService.State.Workshops
                .Any(w => _gameStateService.State.IsWorkshopUnlocked(w.Type) &&
                          w.Workers.Count < w.MaxWorkers);
            if (!HasAvailableSlots)
            {
                NoSlotsMessage = _localizationService.GetString("NoFreeSlotDesc");
            }
        }
        else
        {
            ShowWorkshopSelection = false;
            PendingWorker = null;
            AlertRequested?.Invoke(
                _localizationService.GetString("NoFreeSlot"),
                _localizationService.GetString("NoFreeSlotDesc"),
                _localizationService.GetString("OK") ?? "OK");
        }
    }

    [RelayCommand]
    private void CancelWorkshopSelection()
    {
        ShowWorkshopSelection = false;
        PendingWorker = null;
    }

    [RelayCommand]
    private void SelectWorker(Worker? worker)
    {
        SelectedWorker = worker;
        UpdateCanHire();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke(this, "..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateCanHire()
    {
        if (SelectedWorker == null)
        {
            CanHire = false;
            return;
        }

        CanHire = _gameStateService.CanAfford(SelectedWorker.HiringCost) && HasAvailableSlots;
    }

    /// <summary>
    /// Aktualisiert den Gratis-Refresh-Status und den Button-Text/Icon.
    /// </summary>
    private void UpdateFreeRefreshState()
    {
        var market = _workerService.GetWorkerMarket();
        HasFreeRefresh = !market.FreeRefreshUsedThisRotation;
        RefreshButtonText = HasFreeRefresh
            ? _localizationService.GetString("RefreshMarketFree")
            : _localizationService.GetString("RefreshMarket");
    }
}

/// <summary>
/// Auswahl-Element fuer die Workshop-Zuweisung beim Einstellen eines Arbeiters.
/// </summary>
public class WorkshopSelectionItem
{
    public WorkshopType Type { get; set; }
    public string Name { get; set; } = "";
    public string WorkerInfo { get; set; } = "";
    public bool HasFreeSlots { get; set; }
}
