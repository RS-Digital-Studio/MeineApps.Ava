using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the workshop detail page.
/// Shows upgrade options, workers, and statistics.
/// </summary>
public sealed partial class WorkshopViewModel : ViewModelBase, INavigable, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localizationService;
    private readonly IPurchaseService _purchaseService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IRebirthService _rebirthService;
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
    private string _totalEarnedDisplay = "";

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
    /// Anzahl der Rebirth-Sterne dieses Workshops (0-5).
    /// </summary>
    [ObservableProperty]
    private int _rebirthStars;

    /// <summary>
    /// Ob mindestens ein Rebirth-Stern vorhanden ist (fuer UI-Sichtbarkeit).
    /// </summary>
    public bool HasRebirthStars => RebirthStars > 0;

    partial void OnRebirthStarsChanged(int value) => OnPropertyChanged(nameof(HasRebirthStars));

    /// <summary>
    /// Ob der Workshop wiedergeboren werden kann (Level 1000, weniger als 5 Sterne).
    /// </summary>
    [ObservableProperty]
    private bool _canRebirth;

    /// <summary>
    /// Formatierte Kosten fuer den naechsten Rebirth (z.B. "100 GS + 10%").
    /// </summary>
    [ObservableProperty]
    private string _rebirthCostDisplay = "";

    // ═══════════════════════════════════════════════════════════════════════
    // SPEZIALISIERUNG (ab Level 100)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Ob die Spezialisierung freigeschaltet ist (Level >= 100).</summary>
    [ObservableProperty]
    private bool _isSpecializationUnlocked;

    /// <summary>Aktuell gewählte Spezialisierung (null = keine).</summary>
    [ObservableProperty]
    private string _currentSpecializationName = "";

    /// <summary>Farbe der aktuellen Spezialisierung.</summary>
    [ObservableProperty]
    private string _currentSpecializationColor = "#808080";

    /// <summary>Beschreibung der aktuellen Spezialisierung.</summary>
    [ObservableProperty]
    private string _currentSpecializationDesc = "";

    /// <summary>Ob aktuell eine Spezialisierung gewählt ist.</summary>
    [ObservableProperty]
    private bool _hasSpecialization;

    /// <summary>
    /// Ob ein Extra-Worker-Slot per Ad verfügbar ist (Workshop voll + Werbung aktiv).
    /// </summary>
    [ObservableProperty]
    private bool _canWatchSlotAd;

    /// <summary>Idle Worker automatisch zuweisen (ab Level 50).</summary>
    public bool AutoAssignWorkers
    {
        get => _gameStateService.Automation.AutoAssignWorkers;
        set
        {
            if (_gameStateService.Automation.AutoAssignWorkers == value) return;
            _gameStateService.Automation.AutoAssignWorkers = value;
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
        IRewardedAdService rewardedAdService,
        IRebirthService rebirthService)
    {
        _gameStateService = gameStateService;
        _audioService = audioService;
        _localizationService = localizationService;
        _purchaseService = purchaseService;
        _rewardedAdService = rewardedAdService;
        _rebirthService = rebirthService;

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

        WorkshopIcon = WorkshopType.GetIconKind();
        WorkshopName = _localizationService.GetString(WorkshopType.GetLocalizationKey());
        Level = workshop.Level;
        LevelDisplay = $"{Level}/{Workshop.MaxLevel}";
        LevelProgress = Level / (double)Workshop.MaxLevel;
        IncomePerSecond = workshop.IncomePerSecond;
        IncomeDisplay = MoneyFormatter.FormatPerSecond(IncomePerSecond, 1);
        TotalEarned = workshop.TotalEarned;
        TotalEarnedDisplay = MoneyFormatter.FormatCompact(workshop.TotalEarned);
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
        // Extra-Slot per Ad: nur wenn Workshop voll, Werbung aktiv UND Cap nicht erreicht
        CanWatchSlotAd = !CanHireWorker && ShowAds && workshop.Workers.Count > 0
            && workshop.AdBonusWorkerSlots < Workshop.MaxAdBonusWorkerSlots;

        // Rebirth-Sterne
        RebirthStars = _rebirthService.GetStars(WorkshopType);
        CanRebirth = _rebirthService.CanRebirth(WorkshopType);
        if (CanRebirth)
        {
            var cost = _rebirthService.GetRebirthCost(WorkshopType);
            RebirthCostDisplay = $"{cost.goldenScrews} GS + {cost.moneyPercent * 100:F0}%";
        }
        else
        {
            RebirthCostDisplay = "";
        }

        // Spezialisierung
        IsSpecializationUnlocked = workshop.Level >= GameBalanceConstants.SpecializationUnlockLevel;
        HasSpecialization = workshop.WorkshopSpecialization != null;
        if (HasSpecialization)
        {
            var spec = workshop.WorkshopSpecialization!;
            CurrentSpecializationName = _localizationService.GetString(spec.NameKey)
                ?? spec.Type.ToString();
            CurrentSpecializationColor = spec.Color;
            CurrentSpecializationDesc = FormatSpecializationEffects(spec);
        }
        else
        {
            CurrentSpecializationName = _localizationService.GetString("NoSpecialization") ?? "-";
            CurrentSpecializationColor = "#808080";
            CurrentSpecializationDesc = "";
        }
    }

    /// <summary>
    /// Formatiert die Spezialisierungs-Effekte als kompakten Übersichtstext.
    /// </summary>
    private string FormatSpecializationEffects(WorkshopSpecialization spec)
    {
        var parts = new List<string>();
        string incomeLabel = _localizationService.GetString("Income") ?? "Income";
        string efficiencyLabel = _localizationService.GetString("Efficiency") ?? "Efficiency";
        string costsLabel = _localizationService.GetString("Costs") ?? "Costs";

        if (spec.IncomeModifier > 0) parts.Add($"+{spec.IncomeModifier:P0} {incomeLabel}");
        else if (spec.IncomeModifier < 0) parts.Add($"{spec.IncomeModifier:P0} {incomeLabel}");
        if (spec.EfficiencyModifier > 0) parts.Add($"+{spec.EfficiencyModifier:P0} {efficiencyLabel}");
        else if (spec.EfficiencyModifier < 0) parts.Add($"{spec.EfficiencyModifier:P0} {efficiencyLabel}");
        if (spec.CostModifier > 0) parts.Add($"+{spec.CostModifier:P0} {costsLabel}");
        else if (spec.CostModifier < 0) parts.Add($"{spec.CostModifier:P0} {costsLabel}");
        string workerLabel = _localizationService.GetString("Workers") ?? "Worker";
        if (spec.WorkerCapacityModifier != 0) parts.Add($"{spec.WorkerCapacityModifier} {workerLabel}");
        return string.Join(" | ", parts);
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
        var earnings = workshop.GrossIncomePerSecond * 7200; // BAL-AD-3: 2h Ertrag (von 30min erhöht, bei mehreren WS sonst zu schwach)

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
            // Cap prüfen: Maximal MaxAdBonusWorkerSlots (3) Extra-Slots per Werbung
            if (workshop.AdBonusWorkerSlots >= Workshop.MaxAdBonusWorkerSlots)
                return;
            workshop.AdBonusWorkerSlots += 1;
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
    private void SetSpecialization(string? typeString)
    {
        if (string.IsNullOrEmpty(typeString)) return;
        if (!Enum.TryParse<SpecializationType>(typeString, out var specType)) return;

        var workshop = _gameStateService.State.GetOrCreateWorkshop(WorkshopType);
        if (workshop.Level < GameBalanceConstants.SpecializationUnlockLevel) return;

        workshop.WorkshopSpecialization = new WorkshopSpecialization { Type = specType };
        _gameStateService.State.InvalidateIncomeCache();
        LoadWorkshop();
    }

    [RelayCommand]
    private void RemoveSpecialization()
    {
        var workshop = _gameStateService.State.GetOrCreateWorkshop(WorkshopType);
        workshop.WorkshopSpecialization = null;
        _gameStateService.State.InvalidateIncomeCache();
        LoadWorkshop();
    }

    [RelayCommand]
    private void DoRebirth()
    {
        if (!_rebirthService.CanRebirth(WorkshopType)) return;

        if (_rebirthService.DoRebirth(WorkshopType))
        {
            LoadWorkshop();
            UpgradeEffectRequested?.Invoke(this, EventArgs.Empty);
        }
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
