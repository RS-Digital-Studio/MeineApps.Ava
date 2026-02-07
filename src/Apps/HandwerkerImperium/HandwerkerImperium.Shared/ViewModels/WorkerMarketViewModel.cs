using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

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
    private string _currentBalance = "0 \u20AC";

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _hireButtonText = string.Empty;

    [ObservableProperty]
    private string _refreshButtonText = string.Empty;

    [ObservableProperty]
    private string _nextRotationLabel = string.Empty;

    [ObservableProperty]
    private bool _canHire;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public WorkerMarketViewModel(
        IWorkerService workerService,
        IGameStateService gameStateService,
        ILocalizationService localizationService)
    {
        _workerService = workerService;
        _gameStateService = gameStateService;
        _localizationService = localizationService;

        UpdateLocalizedTexts();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Loads the current worker market pool and updates all display properties.
    /// </summary>
    public void LoadMarket()
    {
        var market = _workerService.GetWorkerMarket();
        AvailableWorkers = market.AvailableWorkers.ToList();
        CurrentBalance = MoneyFormatter.Format(_gameStateService.State.Money, 2);
        UpdateTimer();
        UpdateCanHire();
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
        RefreshButtonText = _localizationService.GetString("RefreshMarket");
        NextRotationLabel = _localizationService.GetString("NextRotation");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void RefreshMarket()
    {
        var market = _workerService.RefreshMarket();
        AvailableWorkers = market.AvailableWorkers.ToList();
        UpdateTimer();
        SelectedWorker = null;
        UpdateCanHire();
    }

    [RelayCommand]
    private void HireWorker(Worker? worker)
    {
        if (worker == null) return;

        var hiringCost = worker.Tier.GetHiringCost();

        if (!_gameStateService.CanAfford(hiringCost))
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("NotEnoughMoney"),
                string.Format(_localizationService.GetString("HiringCostFormat"), MoneyFormatter.Format(hiringCost, 0)),
                "OK");
            return;
        }

        // Erster freier Workshop mit Platz
        var workshops = _gameStateService.State.Workshops
            .Where(w => w.IsUnlocked)
            .ToList();

        if (workshops.Count == 0)
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("NoWorkshop"),
                _localizationService.GetString("NoWorkshopDesc"),
                "OK");
            return;
        }

        // Versuche Worker zu einem Workshop zuzuordnen (erster mit freien Plaetzen)
        bool hired = false;
        foreach (var ws in workshops)
        {
            if (_workerService.HireWorker(worker, ws.Type))
            {
                hired = true;
                break;
            }
        }

        if (hired)
        {
            // Worker aus Markt-Liste entfernen
            var updated = AvailableWorkers.Where(w => w.Id != worker.Id).ToList();
            AvailableWorkers = updated;
            SelectedWorker = null;
            CurrentBalance = MoneyFormatter.Format(_gameStateService.State.Money, 2);
            UpdateCanHire();

            AlertRequested?.Invoke(
                _localizationService.GetString("WorkerHired"),
                string.Format(_localizationService.GetString("WorkerHiredFormat"), worker.Name),
                _localizationService.GetString("Great"));
        }
        else
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("NoFreeSlot"),
                _localizationService.GetString("NoFreeSlotDesc"),
                "OK");
        }
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

        var cost = SelectedWorker.Tier.GetHiringCost();
        CanHire = _gameStateService.CanAfford(cost);
    }
}
