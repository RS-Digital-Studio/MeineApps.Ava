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
public partial class BacktestViewModel : ViewModelBase
{
    private readonly RiskSettings _riskSettings;
    private readonly IPublicMarketDataClient? _publicClient;
    private readonly BotEventBus _eventBus;
    private readonly BotDatabaseService? _dbService;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _symbol = "BTC-USDT";
    [ObservableProperty] private string _selectedStrategy = "CryptoTrendPro";
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
    /// Erstellt die passende IStrategy-Instanz basierend auf SelectedStrategy.
    /// </summary>
    private IStrategy CreateStrategy() => StrategyFactory.Create(SelectedStrategy);

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

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        // Log: Backtest gestartet
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Backtest",
            $"Starte Backtest: {SelectedStrategy} auf {Symbol} ({SelectedTimeFrame}), Kapital: {InitialBalance} USDT, Hebel: {Leverage}x"));

        try
        {
            // Echte Strategie erstellen
            var strategy = CreateStrategy();

            // RiskSettings mit konfiguriertem Leverage übernehmen
            var riskSettings = new RiskSettings
            {
                MaxPositionSizePercent = _riskSettings.MaxPositionSizePercent,
                MaxDailyDrawdownPercent = _riskSettings.MaxDailyDrawdownPercent,
                MaxTotalDrawdownPercent = _riskSettings.MaxTotalDrawdownPercent,
                MaxOpenPositions = _riskSettings.MaxOpenPositions,
                MaxOpenPositionsPerSymbol = _riskSettings.MaxOpenPositionsPerSymbol,
                MaxLeverage = Leverage,
                CheckCorrelation = _riskSettings.CheckCorrelation,
                MaxCorrelation = _riskSettings.MaxCorrelation,
                EnableTrailingStop = _riskSettings.EnableTrailingStop,
                TrailingStopPercent = _riskSettings.TrailingStopPercent
            };

            var riskManager = new RiskManager(riskSettings, NullLogger<RiskManager>.Instance);

            // BacktestSettings mit konfiguriertem Startkapital
            var backtestSettings = new BacktestSettings { InitialBalance = InitialBalance };

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

            var timeFrame = ParseTimeFrame(SelectedTimeFrame);
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
                _cts.Token);

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

            // Regime-Breakdown als Text formatieren
            if (report.RegimeBreakdown.Count > 0)
            {
                var lines = report.RegimeBreakdown
                    .OrderByDescending(r => r.Value.TradeCount)
                    .Select(r => $"{r.Key}: {r.Value.TradeCount} Trades, WR {r.Value.WinRate:P0}, PnL {r.Value.TotalPnl:F2}, PF {r.Value.ProfitFactor:F2}");
                RegimeBreakdownText = string.Join("\n", lines);
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
        _cts?.Cancel();
    }

    // === Walk-Forward-Optimierung ===

    [ObservableProperty] private bool _isWalkForwardRunning;
    [ObservableProperty] private string _walkForwardResult = "";

    [RelayCommand]
    private async Task RunWalkForward()
    {
        if (_publicClient == null)
        {
            WalkForwardResult = "Kein Public Client verfügbar (Offline-Modus)";
            return;
        }

        IsWalkForwardRunning = true;
        WalkForwardResult = "Lade historische Daten für Walk-Forward-Optimierung...";

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            var strategy = CreateStrategy();
            var timeFrame = ParseTimeFrame(SelectedTimeFrame);
            var from = StartDate?.UtcDateTime ?? DateTime.UtcNow.AddDays(-90);
            var to = EndDate?.UtcDateTime ?? DateTime.UtcNow;

            // Historische Daten laden
            var candles = await _publicClient.GetKlinesAsync(Symbol, timeFrame, from, to, _cts.Token).ConfigureAwait(false);
            if (candles.Count < 200)
            {
                WalkForwardResult = $"Zu wenige Daten: {candles.Count} Candles (min. 200 benötigt)";
                return;
            }

            WalkForwardResult = $"{candles.Count} Candles geladen, starte Walk-Forward...";

            // WalkForward im ThreadPool ausführen (ist CPU-intensiv wegen GA)
            var wfo = new BingXBot.Engine.ATI.WalkForwardOptimizer
            {
                PopulationSize = 30,
                MaxGenerations = 20,
                TrainTestRatio = 2
            };

            // Fitness-Funktion: Mini-Backtest pro Fenster (Sharpe-Ratio als Ziel)
            var backtestSettings = new BacktestSettings { InitialBalance = InitialBalance };
            var riskSettings = new RiskSettings { MaxLeverage = Leverage, MinRiskRewardRatio = 0 }; // RRR deaktiviert für Optimierung

            Engine.ATI.WalkForwardOptimizer.WalkForwardResult result = null!;
            await Task.Run(() =>
            {
                result = wfo.Optimize(strategy, candles, candles.Count / 6,
                    (strat, windowCandles) =>
                    {
                        var simExchange = new SimulatedExchange(backtestSettings);
                        var riskManager = new RiskManager(riskSettings, NullLogger<RiskManager>.Instance);
                        var engine = new BacktestEngine(simExchange, NullLogger<BacktestEngine>.Instance);

                        var report = engine.RunAsync(strat, riskManager, Symbol, timeFrame,
                            windowCandles[0].OpenTime, windowCandles[^1].CloseTime,
                            backtestSettings).GetAwaiter().GetResult();

                        // Sharpe als Fitness
                        return report.SharpeRatio;
                    });
            }, _cts.Token).ConfigureAwait(false);

            // Ergebnis anzeigen
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Walk-Forward abgeschlossen: {result.Windows.Count} Fenster");
            sb.AppendLine($"Aggregierter Sharpe: {result.AggregatedSharpe:F3}");
            sb.AppendLine($"OOS-WinRate: {result.AggregatedWinRate:P0}");
            sb.AppendLine("Optimierte Parameter:");
            foreach (var (name, value) in result.BestParameters)
                sb.AppendLine($"  {name} = {value}");

            WalkForwardResult = sb.ToString();

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "WFO",
                $"Walk-Forward auf {Symbol}: {result.Windows.Count} Fenster, Sharpe={result.AggregatedSharpe:F3}"));
        }
        catch (OperationCanceledException)
        {
            WalkForwardResult = "Walk-Forward abgebrochen";
        }
        catch (Exception ex)
        {
            WalkForwardResult = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsWalkForwardRunning = false;
        }
    }
}

/// <summary>
/// Ein einzelner Backtest-Trade.
/// </summary>
public record BacktestTradeItem(string Symbol, string Side, decimal Entry, decimal Exit, decimal Pnl, bool IsWin);
