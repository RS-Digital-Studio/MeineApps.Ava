using BingXBot.Backtest;
using BingXBot.Backtest.Reports;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Backtest.Simulation;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using BingXBot.Trading;
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
    private readonly ScannerSettings _scannerSettings;
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

    // Multi-TF Standalone: Backtest pro TF sequenziell
    [ObservableProperty] private bool _backtestAllTimeframes;
    [ObservableProperty] private bool _tfD1InBacktest = true;
    [ObservableProperty] private bool _tfH4InBacktest = true;
    [ObservableProperty] private bool _tfH1InBacktest = true;
    [ObservableProperty] private bool _tfM15InBacktest = false;
    [ObservableProperty] private string _perTfSummary = "";

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

    // === v1.5.3 Phase 6 / v1.6.0 Phase 10C — Walk-Forward-Backtest ===
    [ObservableProperty] private bool _enableWalkForward;
    [ObservableProperty] private int _walkForwardWindowDays = 30;
    [ObservableProperty] private int _walkForwardStepDays = 7;
    [ObservableProperty] private bool _hasWalkForwardResult;
    [ObservableProperty] private decimal _walkForwardAvgWinRate;
    [ObservableProperty] private decimal _walkForwardRobustnessScore;
    [ObservableProperty] private decimal _walkForwardTotalPnl;
    [ObservableProperty] private decimal _walkForwardMaxDrawdown;
    [ObservableProperty] private int _walkForwardWindowCount;
    public ObservableCollection<WalkForwardWindowResult> WalkForwardWindows { get; } = new();

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

    public BacktestViewModel(RiskSettings riskSettings, ScannerSettings scannerSettings, BotEventBus eventBus,
        IPublicMarketDataClient? publicClient = null, BotDatabaseService? dbService = null)
    {
        // WICHTIG: scannerSettings bewusst als NICHT-optionaler Parameter.
        // Microsoft.Extensions.DependencyInjection füllt optionale Parameter mit Default-Wert
        // NICHT aus dem Container — der Default (null) gewinnt. Als required-Parameter wird
        // die Singleton-Instanz aus App.axaml.cs (Z.327) garantiert injiziert.
        _riskSettings = riskSettings;
        _scannerSettings = scannerSettings;
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
    /// Erstellt die passende IStrategy-Instanz (Multi-TF Standalone: kein Preset nötig).
    /// </summary>
    private IStrategy CreateStrategy() => StrategyFactory.Create(SelectedStrategy);

    /// <summary>
    /// Multi-TF Standalone: Backtest pro ausgewählter Navigator-TF sequenziell durchführen.
    /// Aggregiert die Ergebnisse + speichert eine Per-TF-Zusammenfassung in <see cref="PerTfSummary"/>.
    /// </summary>
    private async Task RunBacktestMultiTfAsync()
    {
        var tfsToTest = new List<TimeFrame>();
        if (TfD1InBacktest) tfsToTest.Add(TimeFrame.D1);
        if (TfH4InBacktest) tfsToTest.Add(TimeFrame.H4);
        if (TfH1InBacktest) tfsToTest.Add(TimeFrame.H1);
        if (TfM15InBacktest) tfsToTest.Add(TimeFrame.M15);

        if (tfsToTest.Count == 0)
        {
            StatusText = "Multi-TF-Test: Mindestens eine TF muss ausgewählt sein";
            IsRunning = false;
            return;
        }

        var allTrades = new List<CompletedTrade>();
        decimal totalPnl = 0;
        int totalTrades = 0;
        var perTfLines = new List<string>();

        var from = StartDate?.UtcDateTime ?? DateTime.UtcNow.AddDays(-30);
        var to = EndDate?.UtcDateTime ?? DateTime.UtcNow;

        // W1/D1 einmal vor der TF-Schleife laden — in den nachfolgenden RunAsync-Aufrufen
        // als preloaded*-Parameter durchgereicht (spart n × identische Kline-Requests).
        List<Candle>? preloadedWeekly = null;
        List<Candle>? preloadedDaily = null;
        if (_publicClient != null && tfsToTest.Count > 1)
        {
            var preloadEngine = new BacktestEngine(_publicClient, NullLogger<BacktestEngine>.Instance);
            try
            {
                preloadedWeekly = await preloadEngine.LoadCandlesAsync(Symbol, TimeFrame.W1, from.AddDays(-365), to).ConfigureAwait(false);
            }
            catch { /* W1-Preload optional */ }
            try
            {
                preloadedDaily = await preloadEngine.LoadCandlesAsync(Symbol, TimeFrame.D1, from.AddDays(-120), to).ConfigureAwait(false);
            }
            catch { /* D1-Preload optional */ }
        }

        for (int i = 0; i < tfsToTest.Count; i++)
        {
            var tf = tfsToTest[i];
            if (_backtestCts?.Token.IsCancellationRequested ?? true) break;

            StatusText = $"Teste {tf} ({i + 1}/{tfsToTest.Count})...";
            var strategy = CreateStrategy();
            var riskSettings = BuildRiskSettings();
            var riskManager = new RiskManager(riskSettings, NullLogger<RiskManager>.Instance);
            var backtestSettings = new BacktestSettings
            {
                InitialBalance = InitialBalance,
                Tp1CloseRatio = _riskSettings.Tp1CloseRatio,
                Tp2CloseRatio = _riskSettings.Tp2CloseRatio,
                MinRiskRewardRatio = _riskSettings.MinRiskRewardRatio,
                HtfTimeFrame = Engine.Strategies.SequenzKonzeptStrategy.GetFilterTimeframe(tf),
            };

            BacktestEngine engine = _publicClient != null
                ? new BacktestEngine(_publicClient, NullLogger<BacktestEngine>.Instance)
                : new BacktestEngine(new SimulatedExchange(backtestSettings), NullLogger<BacktestEngine>.Instance);

            // Bei D1-Navigator kein Preload-D1 durchreichen (Strategy lädt eigenes D1 nicht — wäre Rekursion)
            // Bei W1-Navigator analog kein Preload-W1.
            var wForTf = tf == TimeFrame.W1 ? null : preloadedWeekly;
            var dForTf = tf == TimeFrame.D1 ? null : preloadedDaily;

            var report = await engine.RunAsync(
                strategy, riskManager, Symbol, tf, from, to, backtestSettings,
                new Progress<int>(p => Progress = (i * 100 + p) / tfsToTest.Count),
                _backtestCts.Token,
                scannerSettings: _scannerSettings,
                riskSettings: riskSettings,
                preloadedWeekly: wForTf,
                preloadedDaily: dForTf).ConfigureAwait(false);

            // Trades mit TF-Tag versehen
            foreach (var trade in report.Trades)
                allTrades.Add(trade with { NavigatorTimeframe = tf });
            totalPnl += report.TotalPnl;
            totalTrades += report.TotalTrades;
            perTfLines.Add($"{tf}: {report.TotalTrades} Trades, PnL {report.TotalPnl:+0.00;-0.00} USDT, WinRate {report.WinRate:P0}");

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Backtest",
                $"{tf}: {report.TotalTrades} Trades, Gesamt-PnL {report.TotalPnl:+0.00;-0.00} USDT"));
        }

        // Aggregierte Metriken ins UI
        TotalPnl = totalPnl;
        TotalTrades = totalTrades;
        WinRate = totalTrades > 0
            ? (decimal)allTrades.Count(t => t.Pnl > 0) / totalTrades * 100m
            : 0m;
        PerTfSummary = string.Join("\n", perTfLines);

        // Trades in UI-Collection (neueste zuerst)
        Trades.Clear();
        foreach (var trade in allTrades.OrderByDescending(t => t.ExitTime).Take(500))
            Trades.Add(new BacktestTradeItem(
                trade.Symbol,
                trade.Side.ToString(),
                trade.EntryPrice,
                trade.ExitPrice,
                trade.Pnl,
                trade.Pnl > 0));

        HasResult = true;
        StatusText = $"Multi-TF-Test abgeschlossen: {totalTrades} Trades über {tfsToTest.Count} TFs";
        IsRunning = false;

        _eventBus.PublishBacktestCompleted(new BacktestCompletedArgs
        {
            Trades = allTrades,
            StrategyName = SelectedStrategy,
            Symbol = Symbol,
        });
    }

    private RiskSettings BuildRiskSettings() => new()
    {
        MaxDailyDrawdownPercent = _riskSettings.MaxDailyDrawdownPercent,
        MaxTotalDrawdownPercent = _riskSettings.MaxTotalDrawdownPercent,
        MaxOpenPositions = _riskSettings.MaxOpenPositions,
        MaxOpenPositionsPerSymbol = _riskSettings.MaxOpenPositionsPerSymbol,
        MaxLeverage = Leverage,
        MaxPositionSizePercent = _riskSettings.MaxPositionSizePercent,
        MaxMarginPerTradePercent = _riskSettings.MaxMarginPerTradePercent,
        MaxRiskPercentPerTrade = _riskSettings.MaxRiskPercentPerTrade,
        MaxTotalMarginPercent = _riskSettings.MaxTotalMarginPercent,
        LossStreakHalveAtCount = _riskSettings.LossStreakHalveAtCount,
        LossStreakPauseAtCount = _riskSettings.LossStreakPauseAtCount,
        EnableLossStreakDampening = _riskSettings.EnableLossStreakDampening,
        MinPositionSizeRetentionPercent = _riskSettings.MinPositionSizeRetentionPercent,
        Tp1CloseRatio = _riskSettings.Tp1CloseRatio,
        Tp2CloseRatio = _riskSettings.Tp2CloseRatio,
        MinRiskRewardRatio = _riskSettings.MinRiskRewardRatio,
        PipScalingByTf = _riskSettings.PipScalingByTf,
    };

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
        HasWalkForwardResult = false;
        StatusText = "Lade historische Daten von BingX...";
        Trades.Clear();
        WalkForwardWindows.Clear();

        _backtestCts?.Cancel();
        _backtestCts?.Dispose();
        _backtestCts = new CancellationTokenSource();

        // Log: Backtest gestartet
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Backtest",
            $"Starte Backtest: {SelectedStrategy} auf {Symbol} ({SelectedTimeFrame}), Kapital: {InitialBalance} USDT, Hebel: {Leverage}x"));

        try
        {
            // Phase 10C: Wenn Walk-Forward aktiv → eigener Pfad (Single-TF, ueberlappende Fenster)
            if (EnableWalkForward)
            {
                await RunWalkForwardBacktestAsync(_backtestCts.Token).ConfigureAwait(false);
                return;
            }

            // Multi-TF Standalone: Wenn BacktestAllTimeframes aktiv → Schleife über alle ausgewählten TFs
            if (BacktestAllTimeframes)
            {
                await RunBacktestMultiTfAsync().ConfigureAwait(false);
                return;
            }

            // Timeframe zuerst parsen (wird für Preset + HTF-Konfiguration gebraucht)
            var timeFrame = ParseTimeFrame(SelectedTimeFrame);

            // Echte Strategie erstellen
            var strategy = CreateStrategy();

            // Multi-TF Standalone: RiskSettings direkt aus dem globalen Settings-Snapshot verwenden.
            var riskSettings = BuildRiskSettings();

            var riskManager = new RiskManager(riskSettings, NullLogger<RiskManager>.Instance);

            var backtestSettings = new BacktestSettings
            {
                InitialBalance = InitialBalance,
                Tp1CloseRatio = _riskSettings.Tp1CloseRatio,
                Tp2CloseRatio = _riskSettings.Tp2CloseRatio,
                MinRiskRewardRatio = _riskSettings.MinRiskRewardRatio,
                // Filter-TF gemäß Live-Mapping: D1→H4, H4→H1, H1→M15, M15→M5.
                // BacktestEngine lädt diese als FilterTimeframeCandles (= dasselbe wie Live).
                HtfTimeFrame = Engine.Strategies.SequenzKonzeptStrategy.GetFilterTimeframe(timeFrame),
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
                _backtestCts.Token,
                scannerSettings: _scannerSettings,
                riskSettings: riskSettings);

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

    /// <summary>
    /// v1.6.0 Phase 10C — Walk-Forward-Backtest: Ueberlappende Fenster (WindowDays + StepDays)
    /// um Robustheit ueber unterschiedliche Markt-Regimes zu testen. Robustness-Score = StdDev
    /// der WinRate ueber alle Fenster — niedrige Werte = konsistent, hohe = Overfitting-Verdacht.
    /// </summary>
    private async Task RunWalkForwardBacktestAsync(CancellationToken ct)
    {
        if (_publicClient == null)
        {
            StatusText = "Walk-Forward braucht den BingX-Public-Client (Symbol-Daten).";
            return;
        }
        if (WalkForwardWindowDays < 1 || WalkForwardStepDays < 1)
        {
            StatusText = "Walk-Forward: Window/Step muss > 0 sein.";
            return;
        }

        try
        {
            var timeFrame = ParseTimeFrame(SelectedTimeFrame);
            var from = StartDate?.UtcDateTime ?? DateTime.UtcNow.AddDays(-180);
            var to = EndDate?.UtcDateTime ?? DateTime.UtcNow;
            var windowSize = TimeSpan.FromDays(WalkForwardWindowDays);
            var stepSize = TimeSpan.FromDays(WalkForwardStepDays);

            // Vorab Window-Anzahl pruefen, damit User aussagekraeftige Fehlermeldung erhaelt.
            var windows = WalkForwardRunner.GenerateWindows(from, to, windowSize, stepSize);
            if (windows.Count < 2)
            {
                StatusText = $"Walk-Forward braucht ≥ 2 Fenster — Range ergibt nur {windows.Count}. Range erweitern oder Window verkleinern.";
                return;
            }

            StatusText = $"Walk-Forward: {windows.Count} Fenster × {WalkForwardWindowDays} Tage...";

            var engine = new BacktestEngine(_publicClient, NullLogger<BacktestEngine>.Instance);
            var runner = new WalkForwardRunner(engine, NullLogger<WalkForwardRunner>.Instance);

            var backtestSettings = new BacktestSettings
            {
                InitialBalance = InitialBalance,
                Tp1CloseRatio = _riskSettings.Tp1CloseRatio,
                Tp2CloseRatio = _riskSettings.Tp2CloseRatio,
                MinRiskRewardRatio = _riskSettings.MinRiskRewardRatio,
                HtfTimeFrame = SequenzKonzeptStrategy.GetFilterTimeframe(timeFrame),
            };

            var progressReporter = new Progress<(int Window, int Total)>(p =>
            {
                Progress = (int)((double)p.Window / p.Total * 100);
                StatusText = $"Walk-Forward: Fenster {p.Window}/{p.Total}";
            });

            var report = await runner.RunAsync(
                Symbol, timeFrame, from, to, windowSize, stepSize,
                backtestSettings,
                strategyFactory: CreateStrategy,
                riskManagerFactory: () => new RiskManager(BuildRiskSettings(), NullLogger<RiskManager>.Instance),
                scannerSettings: _scannerSettings,
                riskSettings: BuildRiskSettings(),
                progress: progressReporter,
                ct: ct).ConfigureAwait(false);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                WalkForwardWindows.Clear();
                foreach (var w in report.Windows)
                    WalkForwardWindows.Add(w);

                WalkForwardWindowCount = report.WindowCount;
                WalkForwardAvgWinRate = report.AvgWinRate;
                WalkForwardRobustnessScore = report.RobustnessScore;
                WalkForwardTotalPnl = report.TotalNetPnl;
                WalkForwardMaxDrawdown = report.MaxDrawdownAcrossWindows;
                HasWalkForwardResult = true;

                StatusText = $"Walk-Forward fertig — {report.WindowCount} Fenster, AvgWinRate={report.AvgWinRate:P0}, Robustheit (StdDev WinRate)={report.RobustnessScore:F4}";
            });

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Backtest",
                $"Walk-Forward abgeschlossen: {report.WindowCount} Fenster, AvgWinRate={report.AvgWinRate:P1}, Robustheit={report.RobustnessScore:F4}, TotalPnl={report.TotalNetPnl:F2}"));
        }
        catch (OperationCanceledException)
        {
            StatusText = "Walk-Forward abgebrochen";
        }
        catch (Exception ex)
        {
            StatusText = $"Walk-Forward Fehler: {ex.Message}";
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Backtest",
                $"Walk-Forward fehlgeschlagen: {ex.Message}"));
        }
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
