using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für Risk-Management-Einstellungen (Max Drawdown, Position Sizing, Stop-Loss).
/// Direkt verbunden mit dem echten RiskSettings-Objekt aus dem DI-Container.
/// Publiziert Änderungen über den BotEventBus an die Log-Ansicht.
/// </summary>
public partial class RiskSettingsViewModel : ViewModelBase
{
    private readonly RiskSettings _riskSettings;
    private readonly BotEventBus _eventBus;
    private readonly BotDatabaseService? _dbService;

    [ObservableProperty] private decimal _maxPositionSizePercent;
    [ObservableProperty] private decimal _maxDailyDrawdownPercent;
    [ObservableProperty] private decimal _maxTotalDrawdownPercent;
    [ObservableProperty] private int _maxOpenPositions;
    [ObservableProperty] private int _maxOpenPositionsPerSymbol;
    [ObservableProperty] private decimal _maxLeverage;
    [ObservableProperty] private bool _checkCorrelation;
    [ObservableProperty] private decimal _maxCorrelation;
    [ObservableProperty] private bool _enableTrailingStop;
    [ObservableProperty] private decimal _trailingStopPercent;
    [ObservableProperty] private decimal _minLiquidationDistancePercent;
    [ObservableProperty] private decimal _maxNetExposurePercent;
    [ObservableProperty] private bool _considerFundingRate;
    [ObservableProperty] private decimal _maxAdverseFundingRatePercent;

    // Cooldown-Eskalation
    [ObservableProperty] private bool _enableCooldownEscalation;
    [ObservableProperty] private int _maxCooldownHours;

    // Equity-Curve-Trading
    [ObservableProperty] private bool _enableEquityCurveTrading;
    [ObservableProperty] private int _equityCurvePeriod;

    // Momentum-Decay
    [ObservableProperty] private bool _enableMomentumDecay;

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
        // Settings sind bereits beim App-Start aus DB geladen (App.RestoreSettingsFromDb)
        // UI nur noch von den DI-Singletons befüllen
        LoadFromSettings();
    }


    /// <summary>
    /// Lädt alle Werte aus dem echten RiskSettings-Objekt.
    /// </summary>
    private void LoadFromSettings()
    {
        MaxPositionSizePercent = _riskSettings.MaxPositionSizePercent;
        MaxDailyDrawdownPercent = _riskSettings.MaxDailyDrawdownPercent;
        MaxTotalDrawdownPercent = _riskSettings.MaxTotalDrawdownPercent;
        MaxOpenPositions = _riskSettings.MaxOpenPositions;
        MaxOpenPositionsPerSymbol = _riskSettings.MaxOpenPositionsPerSymbol;
        MaxLeverage = _riskSettings.MaxLeverage;
        CheckCorrelation = _riskSettings.CheckCorrelation;
        MaxCorrelation = _riskSettings.MaxCorrelation;
        EnableTrailingStop = _riskSettings.EnableTrailingStop;
        TrailingStopPercent = _riskSettings.TrailingStopPercent;
        MinLiquidationDistancePercent = _riskSettings.MinLiquidationDistancePercent;
        MaxNetExposurePercent = _riskSettings.MaxNetExposurePercent;
        ConsiderFundingRate = _riskSettings.ConsiderFundingRate;
        MaxAdverseFundingRatePercent = _riskSettings.MaxAdverseFundingRatePercent;
        EnableCooldownEscalation = _riskSettings.EnableCooldownEscalation;
        MaxCooldownHours = _riskSettings.MaxCooldownHours;
        EnableEquityCurveTrading = _riskSettings.EnableEquityCurveTrading;
        EquityCurvePeriod = _riskSettings.EquityCurvePeriod;
        EnableMomentumDecay = _riskSettings.EnableMomentumDecay;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        // Werte zurück ins echte RiskSettings-Objekt schreiben
        _riskSettings.MaxPositionSizePercent = MaxPositionSizePercent;
        _riskSettings.MaxDailyDrawdownPercent = MaxDailyDrawdownPercent;
        _riskSettings.MaxTotalDrawdownPercent = MaxTotalDrawdownPercent;
        _riskSettings.MaxOpenPositions = MaxOpenPositions;
        _riskSettings.MaxOpenPositionsPerSymbol = MaxOpenPositionsPerSymbol;
        _riskSettings.MaxLeverage = MaxLeverage;
        _riskSettings.CheckCorrelation = CheckCorrelation;
        _riskSettings.MaxCorrelation = MaxCorrelation;
        _riskSettings.EnableTrailingStop = EnableTrailingStop;
        _riskSettings.TrailingStopPercent = TrailingStopPercent;
        _riskSettings.MinLiquidationDistancePercent = MinLiquidationDistancePercent;
        _riskSettings.MaxNetExposurePercent = MaxNetExposurePercent;
        _riskSettings.ConsiderFundingRate = ConsiderFundingRate;
        _riskSettings.MaxAdverseFundingRatePercent = MaxAdverseFundingRatePercent;
        _riskSettings.EnableCooldownEscalation = EnableCooldownEscalation;
        _riskSettings.MaxCooldownHours = MaxCooldownHours;
        _riskSettings.EnableEquityCurveTrading = EnableEquityCurveTrading;
        _riskSettings.EquityCurvePeriod = EquityCurvePeriod;
        _riskSettings.EnableMomentumDecay = EnableMomentumDecay;

        SaveStatus = "Gespeichert";
        HasUnsavedChanges = false;

        // Zentral alle Settings persistieren (Risk + Scanner + Bot)
        _ = App.SaveAllSettingsAsync();

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Risk",
            $"Risiko-Einstellungen gespeichert: MaxPos={MaxPositionSizePercent}%, MaxDD={MaxTotalDrawdownPercent}%, Hebel={MaxLeverage}x"));
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        // Standard-RiskSettings als Referenz
        var defaults = new RiskSettings();

        MaxPositionSizePercent = defaults.MaxPositionSizePercent;
        MaxDailyDrawdownPercent = defaults.MaxDailyDrawdownPercent;
        MaxTotalDrawdownPercent = defaults.MaxTotalDrawdownPercent;
        MaxOpenPositions = defaults.MaxOpenPositions;
        MaxOpenPositionsPerSymbol = defaults.MaxOpenPositionsPerSymbol;
        MaxLeverage = defaults.MaxLeverage;
        CheckCorrelation = defaults.CheckCorrelation;
        MaxCorrelation = defaults.MaxCorrelation;
        EnableTrailingStop = defaults.EnableTrailingStop;
        TrailingStopPercent = defaults.TrailingStopPercent;
        MinLiquidationDistancePercent = defaults.MinLiquidationDistancePercent;
        MaxNetExposurePercent = defaults.MaxNetExposurePercent;
        ConsiderFundingRate = defaults.ConsiderFundingRate;
        MaxAdverseFundingRatePercent = defaults.MaxAdverseFundingRatePercent;
        EnableCooldownEscalation = defaults.EnableCooldownEscalation;
        MaxCooldownHours = defaults.MaxCooldownHours;
        EnableEquityCurveTrading = defaults.EnableEquityCurveTrading;
        EquityCurvePeriod = defaults.EquityCurvePeriod;
        EnableMomentumDecay = defaults.EnableMomentumDecay;

        // Auch ins echte Settings-Objekt schreiben
        _ = SaveAsync();
        SaveStatus = "Zurückgesetzt";

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Risk",
            "Risiko-Einstellungen auf Standardwerte zurückgesetzt"));
    }

    // Dirty-State bei jeder Änderung markieren
    private void MarkDirty()
    {
        HasUnsavedChanges = true;
        SaveStatus = "";
    }

    partial void OnMaxPositionSizePercentChanged(decimal value) => MarkDirty();
    partial void OnMaxDailyDrawdownPercentChanged(decimal value) => MarkDirty();
    partial void OnMaxTotalDrawdownPercentChanged(decimal value) => MarkDirty();
    partial void OnMaxOpenPositionsChanged(int value) => MarkDirty();
    partial void OnMaxOpenPositionsPerSymbolChanged(int value) => MarkDirty();
    partial void OnMaxLeverageChanged(decimal value) => MarkDirty();
    partial void OnCheckCorrelationChanged(bool value) => MarkDirty();
    partial void OnMaxCorrelationChanged(decimal value) => MarkDirty();
    partial void OnEnableTrailingStopChanged(bool value) => MarkDirty();
    partial void OnTrailingStopPercentChanged(decimal value) => MarkDirty();
    partial void OnMinLiquidationDistancePercentChanged(decimal value) => MarkDirty();
    partial void OnMaxNetExposurePercentChanged(decimal value) => MarkDirty();
    partial void OnConsiderFundingRateChanged(bool value) => MarkDirty();
    partial void OnMaxAdverseFundingRatePercentChanged(decimal value) => MarkDirty();
    partial void OnEnableCooldownEscalationChanged(bool value) => MarkDirty();
    partial void OnMaxCooldownHoursChanged(int value) => MarkDirty();
    partial void OnEnableEquityCurveTradingChanged(bool value) => MarkDirty();
    partial void OnEquityCurvePeriodChanged(int value) => MarkDirty();
    partial void OnEnableMomentumDecayChanged(bool value) => MarkDirty();
}
