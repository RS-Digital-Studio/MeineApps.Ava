using BingXBot.Backtest;
using BingXBot.Backtest.Reports;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Backtest.Simulation;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using BingXBot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für Backtesting (historische Daten, Strategie-Tests, Ergebnisse).
/// Nutzt echte BacktestEngine mit BingXPublicClient für echte Marktdaten (kein API-Key nötig).
/// Publiziert Ergebnisse über den BotEventBus an TradeHistory und Log.
/// </summary>
public partial class BacktestViewModel : ViewModelBase, IDisposable
{
    private readonly RiskSettings _riskSettings;
    private readonly IPublicMarketDataClient? _publicClient;
    private readonly BotEventBus _eventBus;
    private readonly BotDatabaseService? _dbService;
    private CancellationTokenSource? _backtestCts;

    [ObservableProperty] private string _symbol = "BTC-USDT";
    [ObservableProperty] private string _selectedStrategy = "SK-System";
    [ObservableProperty] private string _selectedTimeFrame = "H1";
    [ObservableProperty] private DateTimeOffset? _startDate = DateTimeOffset.UtcNow.AddDays(-30);
    [ObservableProperty] private DateTimeOffset? _endDate = DateTimeOffset.UtcNow;
    [ObservableProperty] private decimal _initialBalance = 1000m;
    [ObservableProperty] private decimal _leverage = 10m;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isLoadingSymbols;
    [ObservableProperty] private int _progress;
    [ObservableProperty] private string _statusText = "Wähle ein Symbol und eine Strategie, dann starte den Backtest";

    // Ergebnis-Werte
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private decimal _totalPnl;
    [ObservableProperty] private decimal _winRate;
    [ObservableProperty] private decimal _maxDrawdown;
    [ObservableProperty] private decimal _sharpeRatio;
    [ObservableProperty] private decimal _profitFactor;
    [ObservableProperty] private int _totalTrades;
    [ObservableProperty] private decimal _averageWin;
    [ObservableProperty] private decimal _averageLoss;

    // Erweiterte Metriken
    [ObservableProperty] private decimal _calmarRatio;
    [ObservableProperty] private decimal _sortinoRatio;
    [ObservableProperty] private decimal _recoveryFactor;
    [ObservableProperty] private int _maxConsecutiveLosses;

    // Monte Carlo
    [ObservableProperty] private bool _hasMonteCarloResult;
    [ObservableProperty] private decimal _mcMaxDrawdown95;
    [ObservableProperty] private decimal _mcReturn5;
    [ObservableProperty] private decimal _mcReturn50;
    [ObservableProperty] private decimal _mcRuinProbability;

    // CPCV
    [ObservableProperty] private bool _hasCpcvResult;
    [ObservableProperty] private decimal _cpcvPbo;
    [ObservableProperty] private decimal _cpcvDegradation;
    [ObservableProperty] private decimal _cpcvAvgOosReturn;

    // Regime-Breakdown (formatiert als Text-Zeilen)
    [ObservableProperty] private string _regimeBreakdownText = "";

    /// <summary>Ob das Gesamt-PnL positiv ist (für Farbsteuerung in der View).</summary>
    public bool IsPnlPositive => TotalPnl >= 0;

    /// <summary>Ob die Win-Rate über 50% ist (für Farbsteuerung in der View).</summary>
    public bool IsWinRateGood => WinRate >= 50m;

    partial void OnTotalPnlChanged(decimal value) => OnPropertyChanged(nameof(IsPnlPositive));
    partial void OnWinRateChanged(decimal value) => OnPropertyChanged(nameof(IsWinRateGood));

    public string[] Strategies => StrategyFactory.AvailableStrategies;
    public string[] TimeFrames => new[] { "M5", "M15", "M30", "H1", "H4", "D1" };

    public ObservableCollection<BacktestTradeItem> Trades { get; } = new();

    /// <summary>
    /// Verfügbare Symbole von BingX (nach Volumen sortiert).
    /// </summary>
    public ObservableCollection<string> AvailableSymbols { get; } = new();

    public BacktestViewModel(RiskSettings riskSettings, BotEventBus eventBus, IPublicMarketDataClient? publicClient = null, BotDatabaseService? dbService = null)
    {
        _riskSettings = riskSettings;
        _eventBus = eventBus;
        _publicClient = publicClient;
        _dbService = dbService;

        // Symbole im Hintergrund laden
        _ = LoadSymbolsAsync();
    }

    /// <summary>
    /// Lädt verfügbare Symbole von BingX (öffentlich, kein API-Key nötig).
    /// </summary>
    private async Task LoadSymbolsAsync()
    {
        if (_publicClient == null) return;

        IsLoadingSymbols = true;
        try
        {
            var symbols = await _publicClient.GetAllSymbolsAsync();

            AvailableSymbols.Clear();
            // Top 50 Symbole nach Volumen
            foreach (var s in symbols.Take(50))
                AvailableSymbols.Add(s);
        }
        catch (Exception ex)
        {
            // Fallback-Symbole bei Netzwerkfehler
            System.Diagnostics.Debug.WriteLine($"Symbol-Laden fehlgeschlagen: {ex.Message}");
            AvailableSymbols.Clear();
            AvailableSymbols.Add("BTC-USDT");
            AvailableSymbols.Add("ETH-USDT");
            AvailableSymbols.Add("SOL-USDT");
            AvailableSymbols.Add("XRP-USDT");
            AvailableSymbols.Add("DOGE-USDT");
        }
        finally
        {
            IsLoadingSymbols = false;
        }
    }

    /// <summary>
    /// Erstellt die passende IStrategy-Instanz und wendet den aktiven Trading-Modus-Preset an.
    /// </summary>
    private IStrategy CreateStrategy()
    {
        var strategy = StrategyFactory.Create(SelectedStrategy);
        if (strategy is SequenzKonzeptStrategy sk)
            sk.ApplyPreset(TradingModePreset.Swing);
        return strategy;
    }

    /// <summary>
    /// Parsed den TimeFrame-String in das entsprechende Enum.
    /// </summary>
    private static TimeFrame ParseTimeFrame(string tf) => tf switch
    {
        "M5" => TimeFrame.M5,
        "M15" => TimeFrame.M15,
        "M30" => TimeFrame.M30,
        "H1" => TimeFrame.H1,
        "H4" => TimeFrame.H4,
        "D1" => TimeFrame.D1,
        _ => TimeFrame.H1
    };

    [RelayCommand]
    private async Task RunBacktest()
    {
        IsRunning = true;
        HasResult = false;
        StatusText = "Lade historische Daten von BingX...";
        Trades.Clear();

        _backtestCts?.Cancel();
        _backtestCts?.Dispose();
        _backtestCts = new CancellationTokenSource();

        // Log: Backtest gestartet
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Backtest",
            $"Starte Backtest: {SelectedStrategy} auf {Symbol} ({SelectedTimeFrame}), Kapital: {InitialBalance} USDT, Hebel: {Leverage}x"));

        try
        {
            // Timeframe zuerst parsen (wird für Preset + HTF-Konfiguration gebraucht)
            var timeFrame = ParseTimeFrame(SelectedTimeFrame);

            // Echte Strategie erstellen
            var strategy = CreateStrategy();

            // Preset-spezifische RiskSettings: Alle Felder übernehmen damit Backtest
            // das gleiche Verhalten zeigt wie Paper/Live-Trading
            var preset = SelectedTimeFrame switch
            {
                "M5" or "M15" => TradingModePreset.Scalping,
                "M30" or "H1" => TradingModePreset.DayTrading,
                _ => TradingModePreset.Swing
            };
            var riskPreset = Core.Configuration.TradingModeDefaults.GetRiskPreset(preset);

            var riskSettings = new RiskSettings
            {
                MaxDailyDrawdownPercent = _riskSettings.MaxDailyDrawdownPercent,
                MaxTotalDrawdownPercent = _riskSettings.MaxTotalDrawdownPercent,
                MaxOpenPositions = _riskSettings.MaxOpenPositions,
                MaxOpenPositionsPerSymbol = _riskSettings.MaxOpenPositionsPerSymbol,
                MaxLeverage = Leverage,
                CheckCorrelation = _riskSettings.CheckCorrelation,
                MaxCorrelation = _riskSettings.MaxCorrelation,
                MinLiquidationDistancePercent = _riskSettings.MinLiquidationDistancePercent,
                MaxPositionSizePercent = riskPreset.MaxPositionSizePercent,
                MaxMarginPerTradePercent = riskPreset.MaxMarginPerTradePercent,
                CooldownHours = riskPreset.CooldownHours,
                MaxHoldHours = riskPreset.MaxHoldHours,
                Tp1CloseRatio = riskPreset.Tp1CloseRatio,
                Tp2CloseRatio = riskPreset.Tp2CloseRatio,
                MinRiskRewardRatio = riskPreset.MinRiskRewardRatio,
            };

            var riskManager = new RiskManager(riskSettings, NullLogger<RiskManager>.Instance);

            var backtestSettings = new BacktestSettings
            {
                InitialBalance = InitialBalance,
                Tp1CloseRatio = riskPreset.Tp1CloseRatio,
                Tp2CloseRatio = riskPreset.Tp2CloseRatio,
                MaxHoldHoursInitial = riskPreset.MaxHoldHours,
                MinRiskRewardRatio = riskPreset.MinRiskRewardRatio,
                HtfTimeFrame = timeFrame != Core.Enums.TimeFrame.H1 ? Core.Enums.TimeFrame.H1 : null,
            };

            // BacktestEngine: Echte Marktdaten wenn Public Client verfügbar, sonst Demo
            BacktestEngine engine;
            if (_publicClient != null)
            {
                engine = new BacktestEngine(_publicClient, NullLogger<BacktestEngine>.Instance);
            }
            else
            {
                // Fallback: SimulatedExchange als Datenquelle (Demo-Candles)
                var simExchange = new SimulatedExchange(backtestSettings);
                engine = new BacktestEngine(simExchange, NullLogger<BacktestEngine>.Instance);
            }

            var from = StartDate?.UtcDateTime ?? DateTime.UtcNow.AddDays(-30);
            var to = EndDate?.UtcDateTime ?? DateTime.UtcNow;

            StatusText = $"Teste {SelectedStrategy}-Strategie gegen historische Candles...";
            var progress = new Progress<int>(p =>
            {
                Progress = p;
                if (p > 0 && p < 100)
                    StatusText = $"Teste {SelectedStrategy}-Strategie... {p}%";
            });

            var report = await engine.RunAsync(
                strategy,
                riskManager,
                Symbol,
                timeFrame,
                from,
                to,
                backtestSettings,
                progress,
                _backtestCts.Token);

            // Report-Ergebnisse in Properties übertragen
            TotalPnl = report.TotalPnl;
            WinRate = report.WinRate;
            MaxDrawdown = report.MaxDrawdownPercent;
            SharpeRatio = report.SharpeRatio;
            ProfitFactor = report.ProfitFactor;
            TotalTrades = report.TotalTrades;
            AverageWin = report.AverageWin;
            AverageLoss = report.AverageLoss;

            // Erweiterte Metriken
            CalmarRatio = report.CalmarRatio;
            SortinoRatio = report.SortinoRatio;
            RecoveryFactor = report.RecoveryFactor;
            MaxConsecutiveLosses = report.MaxConsecutiveLosses;

            // Monte Carlo Ergebnisse
            if (report.MonteCarlo is { IsEmpty: false } mc)
            {
                HasMonteCarloResult = true;
                McMaxDrawdown95 = mc.MaxDrawdown95;
                McReturn5 = mc.Return5;
                McReturn50 = mc.Return50;
                McRuinProbability = mc.RuinProbability;
            }

            // CPCV Ergebnisse
            if (report.Cpcv is { IsEmpty: false } cpcv)
            {
                HasCpcvResult = true;
                CpcvPbo = cpcv.ProbabilityOfOverfitting;
                CpcvDegradation = cpcv.Degradation;
                CpcvAvgOosReturn = cpcv.AvgOutOfSampleReturn;
            }

            // Abgeschlossene Trades als BacktestTradeItems darstellen
            foreach (var trade in report.Trades)
            {
                Trades.Add(new BacktestTradeItem(
                    trade.Symbol,
                    trade.Side.ToString(),
                    trade.EntryPrice,
                    trade.ExitPrice,
                    trade.Pnl,
                    trade.Pnl > 0));
            }

            HasResult = true;
            var usedDemoData = _publicClient == null;
            var pnlSign = report.TotalPnl >= 0 ? "+" : "";
            StatusText = usedDemoData
                ? $"Abgeschlossen (Demo-Daten): {report.TotalTrades} Trades, P&L: {pnlSign}{report.TotalPnl:F2} USDT"
                : $"Abgeschlossen: {report.TotalTrades} Trades, P&L: {pnlSign}{report.TotalPnl:F2} USDT";

            // Ergebnisse an EventBus publizieren → TradeHistory + Log
            _eventBus.PublishBacktestCompleted(new BacktestCompletedArgs
            {
                Trades = report.Trades,
                StrategyName = SelectedStrategy,
                Symbol = Symbol
            });

            // Trades in DB persistieren
            if (_dbService != null)
            {
                foreach (var trade in report.Trades)
                    await _dbService.SaveTradeAsync(trade).ConfigureAwait(false);
            }

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Backtest",
                $"{SelectedStrategy} auf {Symbol}: {report.TotalTrades} Trades, P&L: {pnlSign}{report.TotalPnl:F2} USDT, WinRate: {report.WinRate:F1}%"));
        }
        catch (OperationCanceledException)
        {
            StatusText = "Abgebrochen";
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Backtest",
                "Backtest abgebrochen"));
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler: {ex.Message}";
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Backtest",
                $"Backtest fehlgeschlagen: {ex.Message}"));
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void CancelBacktest()
    {
        _backtestCts?.Cancel();
    }

    public void Dispose()
    {
        _backtestCts?.Cancel();
        _backtestCts?.Dispose();
        _backtestCts = null;
    }
}

/// <summary>
/// Ein einzelner Backtest-Trade.
/// </summary>
public record BacktestTradeItem(string Symbol, string Side, decimal Entry, decimal Exit, decimal Pnl, bool IsWin);
