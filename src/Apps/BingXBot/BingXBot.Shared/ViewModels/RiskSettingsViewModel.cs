using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Trading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für Risk-Management (Buch-konform: nur SK-Buch-relevante Parameter).
/// Max Drawdown, Position Sizing, Leverage, Korrelation, Cooldown.
/// Nicht-Buch-konforme Parameter (Trailing, Funding-Filter, Equity-Curve, Momentum) wurden entfernt.
///
/// Subscribed auf <see cref="ISettingsService.SettingsChanged"/>, damit beim initialen Server-Sync
/// (Remote-Mode) und bei Multi-Client-Updates (anderer Client speichert via Hub-Push) die UI
/// automatisch refresht. Sonst bleiben die [ObservableProperty]-Felder auf den Defaults
/// hängen, die beim ersten Konstruktor-Lauf aus dem leeren Singleton kopiert wurden.
/// </summary>
public partial class RiskSettingsViewModel : ViewModelBase, IDisposable
{
    private readonly RiskSettings _riskSettings;
    private readonly BotEventBus _eventBus;
    private readonly BotDatabaseService? _dbService;
    private readonly ISettingsPersistenceService _settingsPersistence;
    private readonly ISettingsService? _settingsService;
    private bool _suppressDirty;
    private bool _disposed;

    [ObservableProperty] private decimal _maxPositionSizePercent;
    [ObservableProperty] private decimal _maxMarginPerTradePercent;
    [ObservableProperty] private decimal _maxDailyDrawdownPercent;
    [ObservableProperty] private decimal _maxTotalDrawdownPercent;
    [ObservableProperty] private int _maxOpenPositions;
    [ObservableProperty] private int _maxOpenPositionsPerSymbol;
    [ObservableProperty] private decimal _maxLeverage;
    [ObservableProperty] private decimal _tp1CloseRatio;
    [ObservableProperty] private decimal _tp2CloseRatio;
    [ObservableProperty] private decimal _minRiskRewardRatio;

    // === v1.5.x Optimization-Plan-2026-05 — opt-in Toggles ===
    [ObservableProperty] private bool _requireHtfConfluenceForEntry;   // Phase 1
    [ObservableProperty] private int _minConfluenceScore;              // Phase 1 quantitatives Gate
    [ObservableProperty] private bool _useAsymmetricCrv;               // Phase 2

    // === v1.7.0 Phase 16 — Cross-TF-Pyramiding (User-Ausnahme) ===
    [ObservableProperty] private bool _enableCrossTfPyramiding;
    [ObservableProperty] private int _pyramidMaxAddOns;
    [ObservableProperty] private decimal _pyramidScalePercent;

    // === Konfigurierbare Risk-Schwellen (vorher hardcoded) ===
    [ObservableProperty] private decimal _maxTotalMarginPercent;
    [ObservableProperty] private int _lossStreakHalveAtCount;
    [ObservableProperty] private int _lossStreakPauseAtCount;
    [ObservableProperty] private decimal _minPositionSizeRetentionPercent;

    // === Marktspezifische Hebel (mappen auf RiskSettings.CategorySettings) ===
    public decimal CryptoMaxLeverage
    {
        get => _riskSettings.GetCategorySettings(MarketCategory.Crypto).MaxLeverage;
        set { UpdateCategoryLeverage(MarketCategory.Crypto, value); OnPropertyChanged(); MarkDirty(); }
    }
    public decimal CommodityMaxLeverage
    {
        get => _riskSettings.GetCategorySettings(MarketCategory.Commodity).MaxLeverage;
        set { UpdateCategoryLeverage(MarketCategory.Commodity, value); OnPropertyChanged(); MarkDirty(); }
    }
    public decimal IndexMaxLeverage
    {
        get => _riskSettings.GetCategorySettings(MarketCategory.Index).MaxLeverage;
        set { UpdateCategoryLeverage(MarketCategory.Index, value); OnPropertyChanged(); MarkDirty(); }
    }
    public decimal ForexMaxLeverage
    {
        get => _riskSettings.GetCategorySettings(MarketCategory.Forex).MaxLeverage;
        set { UpdateCategoryLeverage(MarketCategory.Forex, value); OnPropertyChanged(); MarkDirty(); }
    }
    public decimal StockMaxLeverage
    {
        get => _riskSettings.GetCategorySettings(MarketCategory.Stock).MaxLeverage;
        set { UpdateCategoryLeverage(MarketCategory.Stock, value); OnPropertyChanged(); MarkDirty(); }
    }

    private void UpdateCategoryLeverage(MarketCategory cat, decimal value)
    {
        if (!_riskSettings.CategorySettings.ContainsKey(cat))
            _riskSettings.CategorySettings[cat] = new MarketCategorySettings();
        _riskSettings.CategorySettings[cat] = _riskSettings.CategorySettings[cat] with { MaxLeverage = value };
    }

    [ObservableProperty] private string _saveStatus = "";
    [ObservableProperty] private bool _hasUnsavedChanges;

    private readonly BotSettings _botSettings;
    private readonly ScannerSettings _scannerSettings;

    public RiskSettingsViewModel(RiskSettings riskSettings, BotEventBus eventBus,
        BotSettings botSettings, ScannerSettings scannerSettings,
        ISettingsPersistenceService settingsPersistence,
        ISettingsService? settingsService = null,
        BotDatabaseService? dbService = null)
    {
        _riskSettings = riskSettings;
        _eventBus = eventBus;
        _botSettings = botSettings;
        _scannerSettings = scannerSettings;
        _settingsPersistence = settingsPersistence;
        _settingsService = settingsService;
        _dbService = dbService;
        LoadFromSettings();

        // Subscribe auf Server-Pushes / Initial-Sync. Wichtig: Im Remote-Mode wird das VM
        // möglicherweise BEVOR der erste Server-Sync durch ist konstruiert (Lazy<T> + schneller
        // Tab-Klick). Bei Multi-Client-Saves feuert der Hub das Event ebenfalls hier.
        if (_settingsService != null)
            _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(FullSettingsDto snapshot)
    {
        // Feuert evtl. von SignalR-Background-Thread → auf UI-Thread marshalen, weil Setter
        // PropertyChanged-Notifications auf Bindings auslösen.
        Avalonia.Threading.Dispatcher.UIThread.Post(LoadFromSettings);
    }

    private void LoadFromSettings()
    {
        // Suppress-Flag verhindert dass die OnXxxChanged partial-Setter MarkDirty() triggern.
        // Sync vom Server ist KEINE User-Änderung — wir wollen kein "Ungespeicherte Änderungen"-Banner.
        _suppressDirty = true;
        try
        {
            MaxPositionSizePercent = _riskSettings.MaxPositionSizePercent;
            MaxMarginPerTradePercent = _riskSettings.MaxMarginPerTradePercent;
            MaxDailyDrawdownPercent = _riskSettings.MaxDailyDrawdownPercent;
            MaxTotalDrawdownPercent = _riskSettings.MaxTotalDrawdownPercent;
            MaxOpenPositions = _riskSettings.MaxOpenPositions;
            MaxOpenPositionsPerSymbol = _riskSettings.MaxOpenPositionsPerSymbol;
            MaxLeverage = _riskSettings.MaxLeverage;
            Tp1CloseRatio = _riskSettings.Tp1CloseRatio;
            Tp2CloseRatio = _riskSettings.Tp2CloseRatio;
            MinRiskRewardRatio = _riskSettings.MinRiskRewardRatio;

            // v1.5.x Optimization-Plan-Toggles
            RequireHtfConfluenceForEntry = _riskSettings.RequireHtfConfluenceForEntry;
            MinConfluenceScore = _riskSettings.MinConfluenceScore;
            UseAsymmetricCrv = _riskSettings.UseAsymmetricCrv;
            EnableCrossTfPyramiding = _riskSettings.EnableCrossTfPyramiding;
            PyramidMaxAddOns = _riskSettings.PyramidMaxAddOns;
            PyramidScalePercent = _riskSettings.PyramidScalePercent;

            // Konfigurierbare Risk-Schwellen
            MaxTotalMarginPercent = _riskSettings.MaxTotalMarginPercent;
            LossStreakHalveAtCount = _riskSettings.LossStreakHalveAtCount;
            LossStreakPauseAtCount = _riskSettings.LossStreakPauseAtCount;
            MinPositionSizeRetentionPercent = _riskSettings.MinPositionSizeRetentionPercent;

            // Marktspezifische Hebel: Diese Properties haben keinen Backing-Field-Setter (sie lesen
            // direkt aus _riskSettings.CategorySettings), brauchen also explizite Notification.
            OnPropertyChanged(nameof(CryptoMaxLeverage));
            OnPropertyChanged(nameof(CommodityMaxLeverage));
            OnPropertyChanged(nameof(IndexMaxLeverage));
            OnPropertyChanged(nameof(ForexMaxLeverage));
            OnPropertyChanged(nameof(StockMaxLeverage));

            HasUnsavedChanges = false;
        }
        finally
        {
            _suppressDirty = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        _riskSettings.MaxPositionSizePercent = MaxPositionSizePercent;
        _riskSettings.MaxMarginPerTradePercent = MaxMarginPerTradePercent;
        _riskSettings.MaxDailyDrawdownPercent = MaxDailyDrawdownPercent;
        _riskSettings.MaxTotalDrawdownPercent = MaxTotalDrawdownPercent;
        _riskSettings.MaxOpenPositions = MaxOpenPositions;
        _riskSettings.MaxOpenPositionsPerSymbol = MaxOpenPositionsPerSymbol;
        _riskSettings.MaxLeverage = MaxLeverage;
        _riskSettings.Tp1CloseRatio = Tp1CloseRatio;
        _riskSettings.Tp2CloseRatio = Tp2CloseRatio;
        _riskSettings.MinRiskRewardRatio = MinRiskRewardRatio;
        _riskSettings.RequireHtfConfluenceForEntry = RequireHtfConfluenceForEntry;
        _riskSettings.MinConfluenceScore = MinConfluenceScore;
        _riskSettings.UseAsymmetricCrv = UseAsymmetricCrv;
        _riskSettings.EnableCrossTfPyramiding = EnableCrossTfPyramiding;
        _riskSettings.PyramidMaxAddOns = PyramidMaxAddOns;
        _riskSettings.PyramidScalePercent = PyramidScalePercent;
        _riskSettings.MaxTotalMarginPercent = MaxTotalMarginPercent;
        _riskSettings.LossStreakHalveAtCount = LossStreakHalveAtCount;
        _riskSettings.LossStreakPauseAtCount = LossStreakPauseAtCount;
        _riskSettings.MinPositionSizeRetentionPercent = MinPositionSizeRetentionPercent;

        SaveStatus = "Gespeichert";
        HasUnsavedChanges = false;
        _ = _settingsPersistence.SaveAllAsync();

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Risk",
            $"Risiko-Einstellungen gespeichert: MaxPos={MaxPositionSizePercent}%, MaxDD={MaxTotalDrawdownPercent}%, Hebel={MaxLeverage}x"));
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        var defaults = new RiskSettings();
        MaxPositionSizePercent = defaults.MaxPositionSizePercent;
        MaxMarginPerTradePercent = defaults.MaxMarginPerTradePercent;
        MaxDailyDrawdownPercent = defaults.MaxDailyDrawdownPercent;
        MaxTotalDrawdownPercent = defaults.MaxTotalDrawdownPercent;
        MaxOpenPositions = defaults.MaxOpenPositions;
        MaxOpenPositionsPerSymbol = defaults.MaxOpenPositionsPerSymbol;
        MaxLeverage = defaults.MaxLeverage;
        Tp1CloseRatio = defaults.Tp1CloseRatio;
        Tp2CloseRatio = defaults.Tp2CloseRatio;
        MinRiskRewardRatio = defaults.MinRiskRewardRatio;
        MaxTotalMarginPercent = defaults.MaxTotalMarginPercent;
        LossStreakHalveAtCount = defaults.LossStreakHalveAtCount;
        LossStreakPauseAtCount = defaults.LossStreakPauseAtCount;
        MinPositionSizeRetentionPercent = defaults.MinPositionSizeRetentionPercent;
        _ = SaveAsync();
        SaveStatus = "Zurückgesetzt";
    }

    private void MarkDirty()
    {
        if (_suppressDirty) return;   // LoadFromSettings darf kein Dirty-Flag setzen
        HasUnsavedChanges = true;
        SaveStatus = "";
    }

    partial void OnMaxPositionSizePercentChanged(decimal value) => MarkDirty();
    partial void OnMaxMarginPerTradePercentChanged(decimal value) => MarkDirty();
    partial void OnMaxDailyDrawdownPercentChanged(decimal value) => MarkDirty();
    partial void OnMaxTotalDrawdownPercentChanged(decimal value) => MarkDirty();
    partial void OnMaxOpenPositionsChanged(int value) => MarkDirty();
    partial void OnMaxOpenPositionsPerSymbolChanged(int value) => MarkDirty();
    partial void OnMaxLeverageChanged(decimal value) => MarkDirty();
    partial void OnTp1CloseRatioChanged(decimal value) => MarkDirty();
    partial void OnTp2CloseRatioChanged(decimal value) => MarkDirty();
    partial void OnMinRiskRewardRatioChanged(decimal value) => MarkDirty();
    partial void OnRequireHtfConfluenceForEntryChanged(bool value) => MarkDirty();
    partial void OnMinConfluenceScoreChanged(int value) => MarkDirty();
    partial void OnUseAsymmetricCrvChanged(bool value) => MarkDirty();
    partial void OnEnableCrossTfPyramidingChanged(bool value) => MarkDirty();
    partial void OnPyramidMaxAddOnsChanged(int value) => MarkDirty();
    partial void OnPyramidScalePercentChanged(decimal value) => MarkDirty();
    partial void OnMaxTotalMarginPercentChanged(decimal value) => MarkDirty();
    partial void OnLossStreakHalveAtCountChanged(int value) => MarkDirty();
    partial void OnLossStreakPauseAtCountChanged(int value) => MarkDirty();
    partial void OnMinPositionSizeRetentionPercentChanged(decimal value) => MarkDirty();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_settingsService != null)
            _settingsService.SettingsChanged -= OnSettingsChanged;
    }
}
