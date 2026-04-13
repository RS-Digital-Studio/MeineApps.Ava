using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für Risk-Management (Buch-konform: nur SK-Buch-relevante Parameter).
/// Max Drawdown, Position Sizing, Leverage, Korrelation, Cooldown.
/// Nicht-Buch-konforme Parameter (Trailing, Funding-Filter, Equity-Curve, Momentum) wurden entfernt.
/// </summary>
public partial class RiskSettingsViewModel : ViewModelBase
{
    private readonly RiskSettings _riskSettings;
    private readonly BotEventBus _eventBus;
    private readonly BotDatabaseService? _dbService;

    [ObservableProperty] private decimal _maxPositionSizePercent;
    [ObservableProperty] private decimal _maxMarginPerTradePercent;
    [ObservableProperty] private decimal _maxDailyDrawdownPercent;
    [ObservableProperty] private decimal _maxTotalDrawdownPercent;
    [ObservableProperty] private int _maxOpenPositions;
    [ObservableProperty] private int _maxOpenPositionsPerSymbol;
    [ObservableProperty] private decimal _maxLeverage;
    [ObservableProperty] private bool _checkCorrelation;
    [ObservableProperty] private decimal _maxCorrelation;
    [ObservableProperty] private decimal _minLiquidationDistancePercent;
    [ObservableProperty] private int _cooldownHours;
    [ObservableProperty] private int _maxTradesPerDay;
    [ObservableProperty] private int _maxHoldHours;
    [ObservableProperty] private decimal _tp1CloseRatio;
    [ObservableProperty] private decimal _tp2CloseRatio;
    [ObservableProperty] private decimal _minRiskRewardRatio;

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
        BotDatabaseService? dbService = null)
    {
        _riskSettings = riskSettings;
        _eventBus = eventBus;
        _botSettings = botSettings;
        _scannerSettings = scannerSettings;
        _dbService = dbService;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        MaxPositionSizePercent = _riskSettings.MaxPositionSizePercent;
        MaxMarginPerTradePercent = _riskSettings.MaxMarginPerTradePercent;
        MaxDailyDrawdownPercent = _riskSettings.MaxDailyDrawdownPercent;
        MaxTotalDrawdownPercent = _riskSettings.MaxTotalDrawdownPercent;
        MaxOpenPositions = _riskSettings.MaxOpenPositions;
        MaxOpenPositionsPerSymbol = _riskSettings.MaxOpenPositionsPerSymbol;
        MaxLeverage = _riskSettings.MaxLeverage;
        CheckCorrelation = _riskSettings.CheckCorrelation;
        MaxCorrelation = _riskSettings.MaxCorrelation;
        MinLiquidationDistancePercent = _riskSettings.MinLiquidationDistancePercent;
        CooldownHours = _riskSettings.CooldownHours;
        MaxTradesPerDay = _riskSettings.MaxTradesPerDay;
        MaxHoldHours = _riskSettings.MaxHoldHours;
        Tp1CloseRatio = _riskSettings.Tp1CloseRatio;
        Tp2CloseRatio = _riskSettings.Tp2CloseRatio;
        MinRiskRewardRatio = _riskSettings.MinRiskRewardRatio;
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
        _riskSettings.CheckCorrelation = CheckCorrelation;
        _riskSettings.MaxCorrelation = MaxCorrelation;
        _riskSettings.MinLiquidationDistancePercent = MinLiquidationDistancePercent;
        _riskSettings.CooldownHours = CooldownHours;
        _riskSettings.MaxTradesPerDay = MaxTradesPerDay;
        _riskSettings.MaxHoldHours = MaxHoldHours;
        _riskSettings.Tp1CloseRatio = Tp1CloseRatio;
        _riskSettings.Tp2CloseRatio = Tp2CloseRatio;
        _riskSettings.MinRiskRewardRatio = MinRiskRewardRatio;

        SaveStatus = "Gespeichert";
        HasUnsavedChanges = false;
        _ = App.SaveAllSettingsAsync();

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
        CheckCorrelation = defaults.CheckCorrelation;
        MaxCorrelation = defaults.MaxCorrelation;
        MinLiquidationDistancePercent = defaults.MinLiquidationDistancePercent;
        CooldownHours = defaults.CooldownHours;
        MaxTradesPerDay = defaults.MaxTradesPerDay;
        MaxHoldHours = defaults.MaxHoldHours;
        Tp1CloseRatio = defaults.Tp1CloseRatio;
        Tp2CloseRatio = defaults.Tp2CloseRatio;
        MinRiskRewardRatio = defaults.MinRiskRewardRatio;
        _ = SaveAsync();
        SaveStatus = "Zurückgesetzt";
    }

    private void MarkDirty()
    {
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
    partial void OnCheckCorrelationChanged(bool value) => MarkDirty();
    partial void OnMaxCorrelationChanged(decimal value) => MarkDirty();
    partial void OnMinLiquidationDistancePercentChanged(decimal value) => MarkDirty();
    partial void OnCooldownHoursChanged(int value) => MarkDirty();
    partial void OnMaxTradesPerDayChanged(int value) => MarkDirty();
    partial void OnMaxHoldHoursChanged(int value) => MarkDirty();
    partial void OnTp1CloseRatioChanged(decimal value) => MarkDirty();
    partial void OnTp2CloseRatioChanged(decimal value) => MarkDirty();
    partial void OnMinRiskRewardRatioChanged(decimal value) => MarkDirty();
}
