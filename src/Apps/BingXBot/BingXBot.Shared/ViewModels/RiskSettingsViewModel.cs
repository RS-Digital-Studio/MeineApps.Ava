using BingXBot.Core.Configuration;
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
    [ObservableProperty] private string _saveStatus = "";
    [ObservableProperty] private bool _hasUnsavedChanges;

    // Task speichern damit Save() darauf warten kann (verhindert Race Condition bei schnellem Klick)
    private Task? _dbLoadTask;

    public RiskSettingsViewModel(RiskSettings riskSettings, BotEventBus eventBus, BotDatabaseService? dbService = null)
    {
        _riskSettings = riskSettings;
        _eventBus = eventBus;
        _dbService = dbService;
        LoadFromSettings();

        // Settings aus DB laden (überschreibt Defaults)
        _dbLoadTask = LoadSettingsFromDbAsync();
    }

    /// <summary>
    /// Lädt persistierte Settings aus der SQLite-Datenbank.
    /// </summary>
    private async Task LoadSettingsFromDbAsync()
    {
        if (_dbService == null) return;
        try
        {
            var settings = await _dbService.LoadSettingsAsync();
            // Risk-Settings aus DB auf das echte Objekt und UI übertragen
            _riskSettings.MaxPositionSizePercent = settings.Risk.MaxPositionSizePercent;
            _riskSettings.MaxDailyDrawdownPercent = settings.Risk.MaxDailyDrawdownPercent;
            _riskSettings.MaxTotalDrawdownPercent = settings.Risk.MaxTotalDrawdownPercent;
            _riskSettings.MaxOpenPositions = settings.Risk.MaxOpenPositions;
            _riskSettings.MaxOpenPositionsPerSymbol = settings.Risk.MaxOpenPositionsPerSymbol;
            _riskSettings.MaxLeverage = settings.Risk.MaxLeverage;
            _riskSettings.CheckCorrelation = settings.Risk.CheckCorrelation;
            _riskSettings.MaxCorrelation = settings.Risk.MaxCorrelation;
            _riskSettings.EnableTrailingStop = settings.Risk.EnableTrailingStop;
            _riskSettings.TrailingStopPercent = settings.Risk.TrailingStopPercent;
            _riskSettings.MinLiquidationDistancePercent = settings.Risk.MinLiquidationDistancePercent;
            _riskSettings.MaxNetExposurePercent = settings.Risk.MaxNetExposurePercent;
            _riskSettings.ConsiderFundingRate = settings.Risk.ConsiderFundingRate;
            _riskSettings.MaxAdverseFundingRatePercent = settings.Risk.MaxAdverseFundingRatePercent;

            // UI aktualisieren
            LoadFromSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Settings aus DB laden fehlgeschlagen: {ex.Message}");
        }
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
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        // Sicherstellen dass DB-Load abgeschlossen ist (Race Condition verhindern)
        if (_dbLoadTask != null)
        {
            try { await _dbLoadTask; } catch { /* Fehler beim Laden ignoriert */ }
            _dbLoadTask = null;
        }

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

        SaveStatus = "Gespeichert";
        HasUnsavedChanges = false;

        // In DB persistieren (fire-and-forget)
        if (_dbService != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var botSettings = new BotSettings { Risk = _riskSettings };
                    await _dbService.SaveSettingsAsync(botSettings);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Settings in DB speichern fehlgeschlagen: {ex.Message}");
                }
            });
        }

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
}
