using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the workshop detail page.
/// Shows upgrade options, workers, and statistics.
/// </summary>
public partial class WorkshopViewModel : ObservableObject, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localizationService;
    private readonly IPurchaseService _purchaseService;
    private bool _disposed;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private WorkshopType _workshopType;

    [ObservableProperty]
    private string _workshopIcon = "";

    [ObservableProperty]
    private string _workshopName = "";

    [ObservableProperty]
    private int _level = 1;

    [ObservableProperty]
    private int _maxLevel = 10;

    [ObservableProperty]
    private double _levelProgress;

    [ObservableProperty]
    private decimal _incomePerSecond;

    [ObservableProperty]
    private string _incomeDisplay = "0 €/s";

    [ObservableProperty]
    private decimal _totalEarned;

    [ObservableProperty]
    private int _ordersCompleted;

    [ObservableProperty]
    private ObservableCollection<Worker> _workers = [];

    [ObservableProperty]
    private int _workerCount;

    [ObservableProperty]
    private int _maxWorkers = 1;

    [ObservableProperty]
    private decimal _upgradeCost;

    [ObservableProperty]
    private string _upgradeCostDisplay = "";

    [ObservableProperty]
    private decimal _hireWorkerCost;

    [ObservableProperty]
    private string _hireCostDisplay = "";

    [ObservableProperty]
    private bool _canUpgrade;

    [ObservableProperty]
    private bool _canHireWorker;

    [ObservableProperty]
    private bool _canAffordUpgrade;

    [ObservableProperty]
    private bool _canAffordHire;

    /// <summary>
    /// Whether there are no workers in this workshop.
    /// </summary>
    public bool HasNoWorkers => WorkerCount == 0;

    partial void OnWorkerCountChanged(int value) => OnPropertyChanged(nameof(HasNoWorkers));

    /// <summary>
    /// Indicates whether ads should be shown (not premium).
    /// </summary>
    public bool ShowAds => !_purchaseService.IsPremium;

    public WorkshopViewModel(
        IGameStateService gameStateService,
        IAudioService audioService,
        ILocalizationService localizationService,
        IPurchaseService purchaseService)
    {
        _gameStateService = gameStateService;
        _audioService = audioService;
        _localizationService = localizationService;
        _purchaseService = purchaseService;

        _gameStateService.MoneyChanged += OnMoneyChanged;
        _gameStateService.WorkshopUpgraded += OnWorkshopUpgraded;
        _gameStateService.WorkerHired += OnWorkerHired;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION (replaces IQueryAttributable)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Set the workshop type and load data.
    /// </summary>
    public void SetWorkshopType(WorkshopType type)
    {
        WorkshopType = type;
        LoadWorkshop();
    }

    /// <summary>
    /// Set the workshop type from an integer value and load data.
    /// </summary>
    public void SetWorkshopType(int typeInt)
    {
        WorkshopType = (WorkshopType)typeInt;
        LoadWorkshop();
    }

    private void LoadWorkshop()
    {
        var workshop = _gameStateService.State.GetOrCreateWorkshop(WorkshopType);

        WorkshopIcon = WorkshopType.GetIcon();
        WorkshopName = _localizationService.GetString(WorkshopType.GetLocalizationKey());
        Level = workshop.Level;
        LevelProgress = Level / 10.0;
        IncomePerSecond = workshop.IncomePerSecond;
        IncomeDisplay = $"{IncomePerSecond:N0} €/s";
        TotalEarned = workshop.TotalEarned;
        OrdersCompleted = workshop.OrdersCompleted;

        Workers.Clear();
        foreach (var worker in workshop.Workers)
        {
            Workers.Add(worker);
        }
        WorkerCount = workshop.Workers.Count;
        MaxWorkers = workshop.MaxWorkers;

        UpgradeCost = workshop.UpgradeCost;
        UpgradeCostDisplay = $"{UpgradeCost:N0} €";
        HireWorkerCost = workshop.HireWorkerCost;
        HireCostDisplay = $"{HireWorkerCost:N0} €";

        CanUpgrade = workshop.CanUpgrade;
        CanHireWorker = workshop.CanHireWorker;
        CanAffordUpgrade = _gameStateService.CanAfford(UpgradeCost);
        CanAffordHire = _gameStateService.CanAfford(HireWorkerCost);
    }

    [RelayCommand]
    private async Task UpgradeAsync()
    {
        if (!CanUpgrade || !CanAffordUpgrade)
            return;

        if (_gameStateService.TryUpgradeWorkshop(WorkshopType))
        {
            await _audioService.PlaySoundAsync(GameSound.Upgrade);
            LoadWorkshop();
        }
    }

    [RelayCommand]
    private void HireWorkerFromMarket()
    {
        // Bug 2 Fix: Zum Arbeitermarkt navigieren statt zufaelligen Worker erstellen
        NavigationRequested?.Invoke("workers");
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    private void OnMoneyChanged(object? sender, MoneyChangedEventArgs e)
    {
        CanAffordUpgrade = e.NewAmount >= UpgradeCost;
        CanAffordHire = e.NewAmount >= HireWorkerCost;
    }

    private void OnWorkshopUpgraded(object? sender, WorkshopUpgradedEventArgs e)
    {
        if (e.WorkshopType == WorkshopType)
        {
            LoadWorkshop();
        }
    }

    private void OnWorkerHired(object? sender, WorkerHiredEventArgs e)
    {
        if (e.WorkshopType == WorkshopType)
        {
            LoadWorkshop();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _gameStateService.MoneyChanged -= OnMoneyChanged;
        _gameStateService.WorkshopUpgraded -= OnWorkshopUpgraded;
        _gameStateService.WorkerHired -= OnWorkerHired;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
