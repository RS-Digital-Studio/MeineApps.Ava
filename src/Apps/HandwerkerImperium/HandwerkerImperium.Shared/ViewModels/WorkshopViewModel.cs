using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
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
    private readonly IRewardedAdService _rewardedAdService;
    private bool _disposed;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Wird nach erfolgreichem Upgrade gefeuert (fuer UI-Animation).
    /// </summary>
    public event EventHandler? UpgradeEffectRequested;

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

    /// <summary>
    /// Kosten pro Sekunde (formatiert für Anzeige, z.B. "3,20 €/s").
    /// </summary>
    [ObservableProperty]
    private string _costsDisplay = "";

    /// <summary>
    /// Nettoeinkommen pro Sekunde (Brutto - Kosten).
    /// </summary>
    [ObservableProperty]
    private string _netIncomeDisplay = "";

    /// <summary>
    /// Miete pro Stunde (formatiert).
    /// </summary>
    [ObservableProperty]
    private string _rentDisplay = "";

    /// <summary>
    /// Materialkosten pro Stunde (formatiert).
    /// </summary>
    [ObservableProperty]
    private string _materialCostDisplay = "";

    /// <summary>
    /// Löhne pro Stunde (formatiert).
    /// </summary>
    [ObservableProperty]
    private string _wagesDisplay = "";

    /// <summary>
    /// True wenn Nettoeinkommen negativ (für rote Farbe in UI).
    /// </summary>
    [ObservableProperty]
    private bool _isNetNegative;

    /// <summary>
    /// True wenn Kosten > 0 (für Sichtbarkeit der Kosten-Sektion).
    /// </summary>
    [ObservableProperty]
    private bool _hasCosts;

    /// <summary>
    /// Trend-Indikator: Pfeil hoch/runter gegenueber letztem Ladevorgang.
    /// </summary>
    [ObservableProperty]
    private string _trendIndicator = "";

    [ObservableProperty]
    private bool _hasTrendUp;

    [ObservableProperty]
    private bool _hasTrendDown;

    private decimal _lastNetIncome;

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
    private string _levelDisplay = "1/1000";

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
    /// Ob ein Extra-Worker-Slot per Ad verfügbar ist (Workshop voll + Werbung aktiv).
    /// </summary>
    [ObservableProperty]
    private bool _canWatchSlotAd;

    /// <summary>Idle Worker automatisch zuweisen (ab Level 50).</summary>
    public bool AutoAssignWorkers
    {
        get => _gameStateService.State.Automation.AutoAssignWorkers;
        set
        {
            if (_gameStateService.State.Automation.AutoAssignWorkers == value) return;
            _gameStateService.State.Automation.AutoAssignWorkers = value;
            _gameStateService.MarkDirty();
            OnPropertyChanged();
        }
    }

    /// <summary>Ob AutoAssign freigeschaltet ist (Level 50+).</summary>
    public bool IsAutoAssignUnlocked => _gameStateService.IsAutoAssignUnlocked;

    /// <summary>
    /// Whether there are no workers in this workshop.
    /// </summary>
    public bool HasNoWorkers => WorkerCount == 0;

    partial void OnWorkerCountChanged(int value) => OnPropertyChanged(nameof(HasNoWorkers));

    /// <summary>
    /// Indicates whether ads should be shown (not premium).
    /// </summary>
    public bool ShowAds => !_purchaseService.IsPremium;

    /// <summary>
    /// Ob der 2h-Speedup-Ad-Button angezeigt werden soll (Werbung aktiv + Workshop hat Einkommen).
    /// </summary>
    public bool CanWatchSpeedupAd => ShowAds && IncomePerSecond > 0;

    partial void OnIncomePerSecondChanged(decimal value) => OnPropertyChanged(nameof(CanWatchSpeedupAd));

    /// <summary>
    /// Gibt den aktuellen Workshop fuer SkiaSharp-Rendering zurueck.
    /// </summary>
    public Workshop? GetWorkshopForRendering()
    {
        return _gameStateService.State.Workshops.FirstOrDefault(w => w.Type == WorkshopType);
    }

    public WorkshopViewModel(
        IGameStateService gameStateService,
        IAudioService audioService,
        ILocalizationService localizationService,
        IPurchaseService purchaseService,
        IRewardedAdService rewardedAdService)
    {
        _gameStateService = gameStateService;
        _audioService = audioService;
        _localizationService = localizationService;
        _purchaseService = purchaseService;
        _rewardedAdService = rewardedAdService;

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
        LevelDisplay = $"{Level}/{Workshop.MaxLevel}";
        LevelProgress = Level / (double)Workshop.MaxLevel;
        IncomePerSecond = workshop.IncomePerSecond;
        IncomeDisplay = MoneyFormatter.FormatPerSecond(IncomePerSecond, 1);
        TotalEarned = workshop.TotalEarned;
        OrdersCompleted = workshop.OrdersCompleted;

        Workers.Clear();
        foreach (var worker in workshop.Workers)
        {
            // Einkommensbeitrag berechnen: BaseIncomePerWorker * EffectiveEfficiency
            worker.IncomeContribution = workshop.BaseIncomePerWorker * worker.EffectiveEfficiency;
            Workers.Add(worker);
        }
        WorkerCount = workshop.Workers.Count;
        MaxWorkers = workshop.MaxWorkers;

        // Kosten-Anzeige: Brutto, Kosten-Aufschlüsselung, Netto
        var totalCostsPerHour = workshop.TotalCostsPerHour;
        var costsPerSecond = totalCostsPerHour / 3600m;
        var netIncome = workshop.NetIncomePerSecond;

        CostsDisplay = MoneyFormatter.FormatPerSecond(costsPerSecond, 2);
        NetIncomeDisplay = MoneyFormatter.FormatPerSecond(Math.Abs(netIncome), 2);
        RentDisplay = MoneyFormatter.FormatPerHour(workshop.RentPerHour);
        MaterialCostDisplay = MoneyFormatter.FormatPerHour(workshop.MaterialCostPerHour);
        WagesDisplay = MoneyFormatter.FormatPerHour(workshop.TotalWagesPerHour);
        IsNetNegative = netIncome < 0;
        HasCosts = totalCostsPerHour > 0;

        // Trend-Indikator: Vergleich mit letztem Nettoeinkommen
        if (_lastNetIncome != 0 && netIncome != _lastNetIncome)
        {
            HasTrendUp = netIncome > _lastNetIncome;
            HasTrendDown = netIncome < _lastNetIncome;
        }
        else
        {
            HasTrendUp = false;
            HasTrendDown = false;
        }
        _lastNetIncome = netIncome;

        UpgradeCost = workshop.UpgradeCost;
        UpgradeCostDisplay = MoneyFormatter.FormatCompact(UpgradeCost);
        HireWorkerCost = workshop.HireWorkerCost;
        HireCostDisplay = MoneyFormatter.FormatCompact(HireWorkerCost);

        CanUpgrade = workshop.CanUpgrade;
        CanHireWorker = workshop.CanHireWorker;
        CanAffordUpgrade = _gameStateService.CanAfford(UpgradeCost);
        CanAffordHire = _gameStateService.CanAfford(HireWorkerCost);
        // Extra-Slot per Ad: nur wenn Workshop voll und Werbung aktiv
        CanWatchSlotAd = !CanHireWorker && ShowAds && workshop.Workers.Count > 0;
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
            UpgradeEffectRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private async Task WatchAdForSpeedupAsync()
    {
        if (!CanWatchSpeedupAd) return;

        var workshop = _gameStateService.State.GetOrCreateWorkshop(WorkshopType);
        var earnings = workshop.GrossIncomePerSecond * 7200; // 2h Ertrag

        var success = await _rewardedAdService.ShowAdAsync("workshop_speedup");
        if (success)
        {
            _gameStateService.AddMoney(earnings);
            LoadWorkshop();
        }
    }

    [RelayCommand]
    private async Task WatchAdForExtraSlotAsync()
    {
        if (!CanWatchSlotAd) return;

        var success = await _rewardedAdService.ShowAdAsync("worker_hire_bonus");
        if (success)
        {
            var workshop = _gameStateService.State.GetOrCreateWorkshop(WorkshopType);
            workshop.AdBonusWorkerSlots += 1;
            _gameStateService.MarkDirty();
            LoadWorkshop();
        }
    }

    [RelayCommand]
    private void SelectWorker(string? workerId)
    {
        if (string.IsNullOrEmpty(workerId)) return;
        NavigationRequested?.Invoke($"worker?id={workerId}");
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
