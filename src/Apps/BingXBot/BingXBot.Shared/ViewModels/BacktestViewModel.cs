using BingXBot.Backtest;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Simulation;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für Backtesting (historische Daten, Strategie-Tests, Ergebnisse).
/// Nutzt echte BacktestEngine mit BingXPublicClient für echte Marktdaten (kein API-Key nötig).
/// </summary>
public partial class BacktestViewModel : ObservableObject
{
    private readonly RiskSettings _riskSettings;
    private readonly IPublicMarketDataClient? _publicClient;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _symbol = "BTC-USDT";
    [ObservableProperty] private string _selectedStrategy = "EMA Cross";
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

    public string[] Strategies => StrategyFactory.AvailableStrategies;
    public string[] TimeFrames => new[] { "M5", "M15", "M30", "H1", "H4", "D1" };

    public ObservableCollection<BacktestTradeItem> Trades { get; } = new();

    /// <summary>
    /// Verfügbare Symbole von BingX (nach Volumen sortiert).
    /// </summary>
    public ObservableCollection<string> AvailableSymbols { get; } = new();

    public BacktestViewModel(RiskSettings riskSettings, IPublicMarketDataClient? publicClient = null)
    {
        _riskSettings = riskSettings;
        _publicClient = publicClient;

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
        catch
        {
            // Fallback-Symbole
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
                UseKellyCriterion = _riskSettings.UseKellyCriterion,
                UseAtrSizing = _riskSettings.UseAtrSizing,
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
            var pnlSign = report.TotalPnl >= 0 ? "+" : "";
            StatusText = $"Abgeschlossen: {report.TotalTrades} Trades, P&L: {pnlSign}{report.TotalPnl:F2} USDT";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Abgebrochen";
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler: {ex.Message}";
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
}

/// <summary>
/// Ein einzelner Backtest-Trade.
/// </summary>
public record BacktestTradeItem(string Symbol, string Side, decimal Entry, decimal Exit, decimal Pnl, bool IsWin);
