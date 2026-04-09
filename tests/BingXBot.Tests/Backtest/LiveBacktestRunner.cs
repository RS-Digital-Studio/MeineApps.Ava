using BingXBot.Backtest;
using BingXBot.Backtest.Reports;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using BingXBot.Exchange;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BingXBot.Tests.Backtest;

/// <summary>
/// Live-Backtest gegen echte BingX-Daten (Public API, kein Key nötig).
/// Testet CryptoTrendPro mit den neuen optimierten Parametern auf allen 3 Modi.
/// ACHTUNG: Braucht Internet-Verbindung! Kann 30-60s pro Test dauern.
/// </summary>
public class LiveBacktestRunner
{
    private readonly ITestOutputHelper _output;

    public LiveBacktestRunner(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("BTC-USDT", TradingModePreset.Scalping, 90)]
    [InlineData("BTC-USDT", TradingModePreset.DayTrading, 90)]
    [InlineData("BTC-USDT", TradingModePreset.Swing, 90)]
    [InlineData("ETH-USDT", TradingModePreset.DayTrading, 90)]
    public async Task Backtest_MitNeuenParametern(string symbol, TradingModePreset mode, int tage)
    {
        // BingX Public API Client (kein Key nötig für Klines)
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var rateLimiter = new RateLimiter();
        var publicClient = new BingXPublicClient(httpClient, rateLimiter, NullLogger<BingXPublicClient>.Instance);

        // SK-System mit Mode-Preset
        var strategy = StrategyFactory.Create("SK-System");
        if (strategy is SequenzKonzeptStrategy sk)
            sk.ApplyPreset(mode);

        // RiskSettings aus Preset
        var riskPreset = TradingModeDefaults.GetRiskPreset(mode);
        var scanPreset = TradingModeDefaults.GetScannerPreset(mode);
        var riskSettings = new RiskSettings
        {
            MaxPositionSizePercent = riskPreset.MaxPositionSizePercent,
            MaxMarginPerTradePercent = riskPreset.MaxMarginPerTradePercent,
            MaxLeverage = riskPreset.MaxLeverage,
            EnableMultiStageExit = true,
            Tp1CloseRatio = riskPreset.Tp1CloseRatio,
            Tp2CloseRatio = riskPreset.Tp2CloseRatio,
            SmartBreakevenAtrMultiplier = riskPreset.SmartBreakevenAtrMultiplier,
            MinRiskRewardRatio = 0m, // Deaktiviert für Backtest (Signalqualität messen)
            MaxHoldHours = riskPreset.MaxHoldHours,
            MaxHoldHoursAfterTp1 = riskPreset.MaxHoldHoursAfterTp1,
            EnableTrailingStop = true,
            TrailingStopPercent = 2.5m,
            MaxOpenPositions = 10,
            MinLiquidationDistancePercent = 0m,
            MaxNetExposurePercent = 99999m,
            ConsiderFundingRate = false,
            MaxDailyDrawdownPercent = 0m,       // 0 = deaktiviert
        };
        var riskManager = new RiskManager(riskSettings, NullLogger<RiskManager>.Instance);

        // BacktestSettings
        var backtestSettings = new BacktestSettings
        {
            InitialBalance = 10_000m,
            TakerFee = 0.0005m,
            MakerFee = 0.0002m,
            SpreadPercent = 0.08m,
            UseDynamicSlippage = true,
            SimulateMultiStageExit = true,
            Tp1CloseRatio = riskPreset.Tp1CloseRatio,
            Tp2CloseRatio = riskPreset.Tp2CloseRatio,
            TrailingAtrMultiplier = 2.5m,
            MaxHoldHoursInitial = riskPreset.MaxHoldHours,
            MaxHoldHoursAfterTp1 = riskPreset.MaxHoldHoursAfterTp1,
            SmartBreakevenAtrMultiplier = riskPreset.SmartBreakevenAtrMultiplier,
            HtfTimeFrame = scanPreset.ScanTimeFrame switch
            {
                TimeFrame.M15 => TimeFrame.H1,
                TimeFrame.H1 => TimeFrame.H4,
                _ => TimeFrame.D1
            }
        };

        // Zeitraum
        var to = DateTime.UtcNow;
        var from = to.AddDays(-tage);

        _output.WriteLine($"=== BACKTEST: {symbol} | {mode} ({scanPreset.ScanTimeFrame}) | {tage} Tage ===");
        _output.WriteLine($"Zeitraum: {from:dd.MM.yyyy} - {to:dd.MM.yyyy}");

        // Erst Klines manuell laden um zu prüfen ob API funktioniert
        var testCandles = await publicClient.GetKlinesAsync(symbol, scanPreset.ScanTimeFrame, from, to);
        _output.WriteLine($"Klines geladen: {testCandles.Count} Candles ({scanPreset.ScanTimeFrame})");
        if (testCandles.Count == 0)
        {
            _output.WriteLine("FEHLER: Keine Candles von BingX erhalten!");
            return;
        }

        // Direkte Strategie-Evaluation zum Debuggen (zählt Signale manuell)
        int signalCount = 0, noSignalCount = 0;
        string? lastNoSignalReason = null;
        for (int i = 200; i < testCandles.Count; i++)
        {
            var slice = testCandles.Skip(Math.Max(0, i - 200)).Take(Math.Min(200, i + 1)).ToList();
            var ticker2 = new BingXBot.Core.Models.Ticker(symbol, slice[^1].Close, slice[^1].Close, slice[^1].Close, slice[^1].Volume, 0m, slice[^1].CloseTime);
            var account2 = new BingXBot.Core.Models.AccountInfo(10000m, 10000m, 0m, 0m);
            var ctx = new BingXBot.Core.Models.MarketContext(symbol, slice, ticker2, new List<BingXBot.Core.Models.Position>(), account2);
            var sig = strategy.Evaluate(ctx);
            if (sig.Signal != BingXBot.Core.Enums.Signal.None)
                signalCount++;
            else
            {
                noSignalCount++;
                lastNoSignalReason = sig.Reason;
            }
        }
        _output.WriteLine($"Direkte Evaluation: {signalCount} Signale, {noSignalCount} NoSignal");
        _output.WriteLine($"Letzter NoSignal-Grund: {lastNoSignalReason}");

        // RiskManager-Check auf erstes Signal
        if (signalCount > 0)
        {
            for (int i = 200; i < testCandles.Count; i++)
            {
                var slice2 = testCandles.Skip(Math.Max(0, i - 200)).Take(Math.Min(200, i + 1)).ToList();
                var tk = new BingXBot.Core.Models.Ticker(symbol, slice2[^1].Close, slice2[^1].Close, slice2[^1].Close, slice2[^1].Volume, 0m, slice2[^1].CloseTime);
                var ac = new BingXBot.Core.Models.AccountInfo(10000m, 10000m, 0m, 0m);
                var cx = new BingXBot.Core.Models.MarketContext(symbol, slice2, tk, new List<BingXBot.Core.Models.Position>(), ac);
                var sg = strategy.Evaluate(cx);
                if (sg.Signal is BingXBot.Core.Enums.Signal.Long or BingXBot.Core.Enums.Signal.Short)
                {
                    var entry = slice2[^1].Close;
                    var slDist = Math.Abs(entry - (sg.StopLoss ?? 0));
                    var tpDist = Math.Abs((sg.TakeProfit ?? 0) - entry);
                    var rrr = slDist > 0 ? tpDist / slDist : 0;
                    _output.WriteLine($"Signal: {sg.Signal} | Entry={entry:F2} | SL={sg.StopLoss:F2} (dist={slDist:F2}) | TP={sg.TakeProfit:F2} (dist={tpDist:F2}) | RRR={rrr:F2}");
                    if (strategy is CryptoTrendProStrategy ctp2)
                        _output.WriteLine($"ActivePreset={ctp2.ActivePreset}");
                    break;
                }
            }
        }

        // Backtest ausführen
        var engine = new BacktestEngine(publicClient, NullLogger<BacktestEngine>.Instance);
        var report = await engine.RunAsync(strategy, riskManager, symbol, scanPreset.ScanTimeFrame,
            from, to, backtestSettings, ct: CancellationToken.None);

        // Ergebnisse ausgeben
        _output.WriteLine($"");
        _output.WriteLine($"Trades:         {report.TotalTrades}");
        _output.WriteLine($"Win-Rate:       {report.WinRate:P1}");
        _output.WriteLine($"Profit-Faktor:  {report.ProfitFactor:F2}");
        var returnPct = backtestSettings.InitialBalance > 0 ? report.TotalPnl / backtestSettings.InitialBalance * 100m : 0m;
        _output.WriteLine($"Gesamt-PnL:     {report.TotalPnl:+0.00;-0.00} USDT ({returnPct:+0.0;-0.0}%)");
        _output.WriteLine($"Max Drawdown:   {report.MaxDrawdownPercent:F1}%");
        _output.WriteLine($"Sharpe:         {report.SharpeRatio:F2}");
        _output.WriteLine($"Sortino:        {report.SortinoRatio:F2}");
        _output.WriteLine($"Avg Win:        {report.AverageWin:+0.00} USDT");
        _output.WriteLine($"Avg Loss:       {report.AverageLoss:+0.00;-0.00} USDT");
        _output.WriteLine($"Max ConsecLoss: {report.MaxConsecutiveLosses}");

        // Mindest-Erwartungen (nicht zu streng — Marktbedingungen variieren)
        if (report.TotalTrades >= 5)
        {
            _output.WriteLine($"");
            _output.WriteLine(report.WinRate >= 0.4m ? "WinRate >= 40%: OK" : "WinRate < 40%: WARNUNG");
            _output.WriteLine(report.ProfitFactor >= 1.0m ? "ProfitFactor >= 1.0: OK" : "ProfitFactor < 1.0: WARNUNG — Strategie verliert Geld");
            _output.WriteLine(report.MaxDrawdownPercent <= 15m ? "MaxDD <= 15%: OK" : "MaxDD > 15%: WARNUNG");
        }
        else
        {
            _output.WriteLine($"Zu wenig Trades ({report.TotalTrades}) für aussagekräftige Metriken.");
        }
    }
}
