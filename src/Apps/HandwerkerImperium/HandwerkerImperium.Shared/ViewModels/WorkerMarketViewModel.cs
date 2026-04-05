using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the worker hiring market.
/// Shows available workers with tier badges, personality, talent stars, specialization, and wage.
/// Pool rotates every 4 hours with countdown timer.
/// </summary>
public sealed partial class WorkerMarketViewModel : ViewModelBase, INavigable
{
    private readonly IWorkerService _workerService;
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IDialogService _dialogService;
    private bool _isBusy;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

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
    /// Ob keine Arbeiter im Markt verfügbar sind (für Empty-State-Anzeige).
    /// </summary>
    public bool HasNoAvailableWorkers => AvailableWorkers == null || AvailableWorkers.Count == 0;

    /// <summary>
    /// Ob es volle Workshops gibt, denen ein Extra-Slot per Ad hinzugefuegt werden kann.
    /// </summary>
    public bool HasFullWorkshops
    {
        get
        {
            var workshops = _gameStateService.State.Workshops;
            for (int i = 0; i < workshops.Count; i++)
            {
                var ws = workshops[i];
                if (_gameStateService.State.IsWorkshopUnlocked(ws.Type) && ws.Workers.Count >= ws.MaxWorkers)
                    return true;
            }
            return false;
        }
    }

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
        IRewardedAdService rewardedAdService,
        IDialogService dialogService)
    {
        _workerService = workerService;
        _gameStateService = gameStateService;
        _localizationService = localizationService;
        _rewardedAdService = rewardedAdService;
        _dialogService = dialogService;

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

        // Prüfen ob Workshops mit freien Plätzen existieren + Durchschnitts-Einkommen berechnen
        var allWorkshops = _gameStateService.State.Workshops;
        bool hasSlots = false;
        decimal totalBaseIncome = 0m;
        int unlockedCount = 0;

        for (int i = 0; i < allWorkshops.Count; i++)
        {
            var ws = allWorkshops[i];
            if (!_gameStateService.State.IsWorkshopUnlocked(ws.Type)) continue;
            unlockedCount++;
            totalBaseIncome += ws.BaseIncomePerWorker;
            if (ws.Workers.Count < ws.MaxWorkers)
                hasSlots = true;
        }

        HasAvailableSlots = hasSlots;
        var avgBaseIncome = unlockedCount > 0 ? totalBaseIncome / unlockedCount : 1m;

        // MarketRestriction: Während WorkerStrike höhere Tiers nicht verfügbar
        var activeEvent = _gameStateService.State.ActiveEvent;
        var marketRestriction = activeEvent?.IsActive == true ? activeEvent.Effect.MarketRestriction : null;

        // Markt IMMER anzeigen, unabhaengig von freien Plaetzen
        var playerLevel = _gameStateService.PlayerLevel;
        var netIncomePerSecond = Math.Max(0m, _gameStateService.State.NetIncomePerSecond);

        // Bei MarketRestriction: Nur Worker bis zur erlaubten Tier-Stufe (For-Schleife statt LINQ)
        var marketWorkers = market.AvailableWorkers;
        var workers = new List<Worker>(marketWorkers.Count);
        for (int i = 0; i < marketWorkers.Count; i++)
        {
            if (marketRestriction == null || marketWorkers[i].Tier <= marketRestriction.Value)
                workers.Add(marketWorkers[i]);
        }

        for (int i = 0; i < workers.Count; i++)
        {
            var worker = workers[i];

            // Individuelle Anstellungskosten (Tier + Level + Talent + Persönlichkeit + Spezialisierung + Effizienz)
            var qualityPrice = worker.CalculateMarketPrice(playerLevel);

            // Einkommensbasierter Mindestpreis: ~3min Netto-Einkommen * Tier-Stufe
            // Verhindert dass Worker bei hohem Einkommen "geschenkt" werden
            var tierMultiplier = 1.0m + (int)worker.Tier * 0.5m;
            var incomeFloor = netIncomePerSecond * 180m * tierMultiplier;

            // Level-basierter Mindestpreis als Sicherheitsnetz: Verhindert Exploit
            // wenn Spieler absichtlich Einkommen auf 0 senkt (alle Worker rasten lassen)
            var levelFloor = worker.Tier.GetBaseHiringCost() * (1m + playerLevel * 0.01m);

            worker.HiringCost = Math.Max(qualityPrice, Math.Max(Math.Round(incomeFloor), levelFloor));

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
        OnPropertyChanged(nameof(HasNoAvailableWorkers));

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
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            var market = _workerService.GetWorkerMarket();

            // Gratis-Refresh verfügbar? Direkt aktualisieren ohne Ad
            if (!market.FreeRefreshUsedThisRotation)
            {
                market.FreeRefreshUsedThisRotation = true;
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
                _dialogService.ShowAlertDialog(
                    _localizationService.GetString("Info"),
                    _localizationService.GetString("WatchAdToRefresh"),
                    _localizationService.GetString("OK") ?? "OK");
            }
        }
        finally
        {
            _isBusy = false;
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
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            // Erste volle Workshop finden (For-Schleife statt LINQ)
            Workshop? fullWorkshop = null;
            var workshops = _gameStateService.State.Workshops;
            for (int i = 0; i < workshops.Count; i++)
            {
                var ws = workshops[i];
                if (_gameStateService.State.IsWorkshopUnlocked(ws.Type) && ws.Workers.Count >= ws.MaxWorkers)
                {
                    fullWorkshop = ws;
                    break;
                }
            }

            if (fullWorkshop == null) return;

            var success = await _rewardedAdService.ShowAdAsync("worker_hire_bonus");
            if (success)
            {
                // Cap bei MaxAdBonusWorkerSlots pro Workshop (Exploit-Schutz)
                if (fullWorkshop.AdBonusWorkerSlots >= Workshop.MaxAdBonusWorkerSlots)
                {
                    _dialogService.ShowAlertDialog(
                        _localizationService.GetString("Info"),
                        _localizationService.GetString("MaxSlotReached"),
                        _localizationService.GetString("OK") ?? "OK");
                    return;
                }

                fullWorkshop.AdBonusWorkerSlots += 1;
                LoadMarket();

                _dialogService.ShowAlertDialog(
                    _localizationService.GetString("WorkerSlotBonusDesc"),
                    _localizationService.GetString(fullWorkshop.Type.GetLocalizationKey()),
                    _localizationService.GetString("Great"));
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private void HireWorker(Worker? worker)
    {
        if (worker == null) return;
        if (_isBusy) return;

        var hiringCost = worker.HiringCost;

        if (!_gameStateService.CanAfford(hiringCost))
        {
            _dialogService.ShowAlertDialog(
                _localizationService.GetString("NotEnoughMoney"),
                string.Format(_localizationService.GetString("HiringCostFormat"), MoneyFormatter.Format(hiringCost, 0)),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        // Goldschrauben-Kosten pruefen (Tier A + S)
        var hiringScrewCost = worker.Tier.GetHiringScrewCost();
        if (hiringScrewCost > 0 && !_gameStateService.CanAffordGoldenScrews(hiringScrewCost))
        {
            _dialogService.ShowAlertDialog(
                _localizationService.GetString("NotEnoughScrews"),
                string.Format(_localizationService.GetString("NotEnoughScrewsDesc"), hiringScrewCost),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        // Workshops mit freien Plätzen ermitteln (For-Schleife statt LINQ)
        var allWs = _gameStateService.State.Workshops;
        var workshopsWithSlots = new List<Workshop>();
        for (int i = 0; i < allWs.Count; i++)
        {
            if (allWs[i].IsUnlocked && allWs[i].Workers.Count < allWs[i].MaxWorkers)
                workshopsWithSlots.Add(allWs[i]);
        }

        if (workshopsWithSlots.Count == 0)
        {
            _dialogService.ShowAlertDialog(
                _localizationService.GetString("NoFreeSlot"),
                _localizationService.GetString("NoFreeSlotDesc"),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        // Bug 3 Fix: Workshop-Auswahl-Overlay anzeigen statt automatisch zuzuweisen
        PendingWorker = worker;
        var selections = new List<WorkshopSelectionItem>(workshopsWithSlots.Count);
        for (int i = 0; i < workshopsWithSlots.Count; i++)
        {
            var ws = workshopsWithSlots[i];
            selections.Add(new WorkshopSelectionItem
            {
                Type = ws.Type,
                Name = _localizationService.GetString(ws.Type.GetLocalizationKey()),
                WorkerInfo = $"{ws.Workers.Count}/{ws.MaxWorkers} {_localizationService.GetString("Workers")}",
                HasFreeSlots = true
            });
        }
        WorkshopSelections = selections;

        ShowWorkshopSelection = true;
    }

    [RelayCommand]
    private void ConfirmWorkshopSelection(WorkshopSelectionItem? item)
    {
        if (item == null || PendingWorker == null) return;

        var worker = PendingWorker;

        if (_workerService.HireWorker(worker, item.Type))
        {
            // Worker aus Markt-Liste entfernen (For-Schleife statt LINQ)
            var current = AvailableWorkers;
            var updated = new List<Worker>(current.Count);
            for (int i = 0; i < current.Count; i++)
            {
                if (current[i].Id != worker.Id)
                    updated.Add(current[i]);
            }
            AvailableWorkers = updated;
            OnPropertyChanged(nameof(HasNoAvailableWorkers));
            SelectedWorker = null;
            CurrentBalance = MoneyFormatter.Format(_gameStateService.State.Money, 2);
            GoldenScrewsDisplay = _gameStateService.State.GoldenScrews.ToString("N0");
            UpdateCanHire();

            // Workshop-Auswahl schliessen
            ShowWorkshopSelection = false;
            PendingWorker = null;

            _dialogService.ShowAlertDialog(
                _localizationService.GetString("WorkerHired"),
                string.Format(_localizationService.GetString("WorkerHiredFormat"), worker.Name),
                _localizationService.GetString("Great"));

            // HasAvailableSlots neu berechnen (For-Schleife statt LINQ)
            bool slotsAvailable = false;
            var wsList = _gameStateService.State.Workshops;
            for (int i = 0; i < wsList.Count; i++)
            {
                if (_gameStateService.State.IsWorkshopUnlocked(wsList[i].Type) && wsList[i].Workers.Count < wsList[i].MaxWorkers)
                {
                    slotsAvailable = true;
                    break;
                }
            }
            HasAvailableSlots = slotsAvailable;
            if (!HasAvailableSlots)
            {
                NoSlotsMessage = _localizationService.GetString("NoFreeSlotDesc");
            }
        }
        else
        {
            ShowWorkshopSelection = false;
            PendingWorker = null;
            _dialogService.ShowAlertDialog(
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
        NavigationRequested?.Invoke("..");
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
