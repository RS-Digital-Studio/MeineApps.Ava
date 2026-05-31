using BingXBot.Backtest;
using BingXBot.Backtest.Reports;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BingXBot.Tests.Backtest;

public class BacktestEngineTests
{
    private static List<Candle> GenerateTrendingCandles(int count, decimal startPrice = 100m, int trend = 1)
    {
        var candles = new List<Candle>();
        var rng = new Random(42);
        var price = startPrice;
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < count; i++)
        {
            var trendComponent = trend * 0.5m;
            var noise = (decimal)(rng.NextDouble() - 0.5) * 2m;
            var change = trendComponent + noise;
            var open = price;
            var close = price + change;
            var high = Math.Max(open, close) + (decimal)rng.NextDouble() * 1m;
            var low = Math.Min(open, close) - (decimal)rng.NextDouble() * 1m;
            low = Math.Max(low, 0.01m);

            candles.Add(new Candle(baseTime.AddHours(i), open, high, low, close,
                1000m + (decimal)rng.NextDouble() * 500m, baseTime.AddHours(i + 1)));
            price = Math.Max(close, 0.01m);
        }
        return candles;
    }

    [Fact]
    public async Task Run_WithData_ShouldReturnReport()
    {
        var candles = GenerateTrendingCandles(200, 100m, 1);
        var exchangeClient = Substitute.For<IExchangeClient>();
        exchangeClient.GetKlinesAsync(Arg.Any<string>(), Arg.Any<TimeFrame>(), Arg.Any<int>())
            .Returns(candles);

        var strategy = Substitute.For<IStrategy>();
        strategy.Evaluate(Arg.Any<MarketContext>()).Returns(
            new SignalResult(Signal.None, 0m, null, null, null, "Kein Signal"));

        var riskManager = Substitute.For<IRiskManager>();

        var engine = new BacktestEngine(exchangeClient, NullLogger<BacktestEngine>.Instance);
        var report = await engine.RunAsync(strategy, riskManager, "BTC-USDT", TimeFrame.H1,
            DateTime.MinValue, DateTime.MaxValue, new BacktestSettings());

        report.Should().NotBeNull();
        report.EquityCurve.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Run_EmptyData_WithRealDataSource_ShouldReturnEmptyReportNotDemo()
    {
        // Fix C: Bei einer echten Datenquelle (hier IExchangeClient-Mock) und leeren Klines
        // wird der Run als "keine Daten" uebersprungen — KEINE synthetischen Demo-Candles.
        // (Demo-Candles wuerden einen Sinus-Random-Walk erzeugen, den ein Trendfolger trivial
        // gewinnt → genau das machte den OOS-2024-Lauf mit ZEC-Phantom-Trades wertlos.)
        var exchangeClient = Substitute.For<IExchangeClient>();
        exchangeClient.GetKlinesAsync(Arg.Any<string>(), Arg.Any<TimeFrame>(), Arg.Any<int>())
            .Returns(new List<Candle>());

        var strategy = Substitute.For<IStrategy>();
        strategy.Evaluate(Arg.Any<MarketContext>()).Returns(
            new SignalResult(Signal.None, 0m, null, null, null, "Kein Signal"));

        var engine = new BacktestEngine(exchangeClient, NullLogger<BacktestEngine>.Instance);
        var report = await engine.RunAsync(
            strategy,
            Substitute.For<IRiskManager>(),
            "BTC-USDT", TimeFrame.H1,
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, new BacktestSettings());

        // Leerer Report (keine Trades, keine Equity-Kurve) statt Demo-Candle-Phantom-Statistik.
        report.Should().NotBeNull();
        report.TotalTrades.Should().Be(0);
        report.EquityCurve.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_WithScannerSettings_ShouldPassThroughToStrategyContext()
    {
        // Verifiziert, dass ScannerSettings an jedes Strategy.Evaluate-Call weitergereicht wird.
        var candles = GenerateTrendingCandles(200, 100m, 1);
        var exchangeClient = Substitute.For<IExchangeClient>();
        exchangeClient.GetKlinesAsync(Arg.Any<string>(), Arg.Any<TimeFrame>(), Arg.Any<int>())
            .Returns(candles);

        MarketContext? capturedContext = null;
        var strategy = Substitute.For<IStrategy>();
        strategy.Evaluate(Arg.Do<MarketContext>(c => capturedContext ??= c))
            .Returns(new SignalResult(Signal.None, 0m, null, null, null, ""));

        var scannerSettings = new ScannerSettings();
        var riskSettings = new RiskSettings();

        var engine = new BacktestEngine(exchangeClient, NullLogger<BacktestEngine>.Instance);
        await engine.RunAsync(strategy, Substitute.For<IRiskManager>(),
            "BTC-USDT", TimeFrame.H1, DateTime.MinValue, DateTime.MaxValue, new BacktestSettings(),
            scannerSettings: scannerSettings, riskSettings: riskSettings);

        capturedContext.Should().NotBeNull();
        capturedContext!.ScannerSettings.Should().BeSameAs(scannerSettings);
        capturedContext.RiskSettings.Should().BeSameAs(riskSettings);
        capturedContext.NavigatorTimeframe.Should().Be(TimeFrame.H1, "NavigatorTF muss der übergebenen Backtest-TF entsprechen");
    }

    [Fact]
    public async Task Run_WithoutScannerSettings_ShouldLeaveContextScannerSettingsNull()
    {
        var candles = GenerateTrendingCandles(200, 100m, 1);
        var exchangeClient = Substitute.For<IExchangeClient>();
        exchangeClient.GetKlinesAsync(Arg.Any<string>(), Arg.Any<TimeFrame>(), Arg.Any<int>())
            .Returns(candles);

        MarketContext? capturedContext = null;
        var strategy = Substitute.For<IStrategy>();
        strategy.Evaluate(Arg.Do<MarketContext>(c => capturedContext ??= c))
            .Returns(new SignalResult(Signal.None, 0m, null, null, null, ""));

        var engine = new BacktestEngine(exchangeClient, NullLogger<BacktestEngine>.Instance);
        await engine.RunAsync(strategy, Substitute.For<IRiskManager>(),
            "BTC-USDT", TimeFrame.H1, DateTime.MinValue, DateTime.MaxValue, new BacktestSettings());

        capturedContext.Should().NotBeNull();
        capturedContext!.ScannerSettings.Should().BeNull();
        capturedContext.RiskSettings.Should().BeNull();
    }

    [Fact]
    public async Task Run_WithPreloadedWeeklyDaily_ShouldSkipInternalLoad()
    {
        // Preloaded-Parameter sollen die internen W1/D1-Loads vermeiden — bei IExchangeClient
        // würde ein zusätzlicher GetKlinesAsync-Call erfolgen. Wir verifizieren, dass für W1/D1
        // nichts angefragt wird, wenn die Kerzen bereits übergeben wurden.
        var navCandles = GenerateTrendingCandles(200, 100m, 1);
        var exchangeClient = Substitute.For<IExchangeClient>();
        // Nav-TF (H1) liefert Daten, W1/D1 würde der Engine im internen Load anfragen.
        exchangeClient.GetKlinesAsync(Arg.Any<string>(), Arg.Any<TimeFrame>(), Arg.Any<int>())
            .Returns(navCandles);

        var strategy = Substitute.For<IStrategy>();
        strategy.Evaluate(Arg.Any<MarketContext>())
            .Returns(new SignalResult(Signal.None, 0m, null, null, null, ""));

        var preW = new List<Candle> { new(DateTime.UtcNow.AddDays(-7), 100m, 101m, 99m, 100m, 1000m, DateTime.UtcNow) };
        var preD = new List<Candle> { new(DateTime.UtcNow.AddDays(-1), 100m, 101m, 99m, 100m, 1000m, DateTime.UtcNow) };

        var engine = new BacktestEngine(exchangeClient, NullLogger<BacktestEngine>.Instance);
        await engine.RunAsync(strategy, Substitute.For<IRiskManager>(),
            "BTC-USDT", TimeFrame.H1, DateTime.MinValue, DateTime.MaxValue, new BacktestSettings(),
            preloadedWeekly: preW, preloadedDaily: preD);

        // Der Substitute soll W1/D1 NICHT angefragt haben
        await exchangeClient.DidNotReceive().GetKlinesAsync("BTC-USDT", TimeFrame.W1, Arg.Any<int>());
        await exchangeClient.DidNotReceive().GetKlinesAsync("BTC-USDT", TimeFrame.D1, Arg.Any<int>());
    }

    [Fact]
    public void GenerateDemoCandles_ShouldCreateValidCandles()
    {
        var candles = BacktestEngine.GenerateDemoCandles(100, 50000m, TimeFrame.H1);

        candles.Should().HaveCount(100);
        candles.Should().AllSatisfy(c =>
        {
            c.High.Should().BeGreaterThanOrEqualTo(c.Low);
            c.Open.Should().BeGreaterThan(0);
            c.Close.Should().BeGreaterThan(0);
            c.Volume.Should().BeGreaterThan(0);
        });

        // Zeitlich aufsteigend sortiert
        for (int i = 1; i < candles.Count; i++)
            candles[i].OpenTime.Should().BeAfter(candles[i - 1].OpenTime);
    }

    [Fact]
    public void PerformanceReport_FromTrades_ShouldCalculateMetrics()
    {
        var trades = new List<CompletedTrade>
        {
            new("BTC", Side.Buy, 100m, 110m, 1m, 10m, 0.5m, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), "Win", TradingMode.Backtest),
            new("BTC", Side.Buy, 100m, 95m, 1m, -5m, 0.5m, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, "Loss", TradingMode.Backtest),
            new("BTC", Side.Sell, 100m, 90m, 1m, 10m, 0.5m, DateTime.UtcNow.AddHours(-3), DateTime.UtcNow.AddHours(-2), "Win", TradingMode.Backtest),
        };
        var equity = new List<EquityPoint>
        {
            new(DateTime.UtcNow.AddHours(-3), 1000m),
            new(DateTime.UtcNow.AddHours(-2), 1010m),
            new(DateTime.UtcNow.AddHours(-1), 1005m),
            new(DateTime.UtcNow, 1015m),
        };

        var report = PerformanceReport.FromTrades(trades, equity, 1000m);
        report.TotalTrades.Should().Be(3);
        report.WinningTrades.Should().Be(2);
        report.LosingTrades.Should().Be(1);
        report.WinRate.Should().BeApproximately(66.67m, 0.1m);
        report.TotalPnl.Should().Be(15m);
        report.ProfitFactor.Should().BeGreaterThan(1m);
        report.AverageWin.Should().Be(10m);
        report.AverageLoss.Should().Be(5m);
        report.AverageRrr.Should().Be(2m);
    }

    [Fact]
    public void PerformanceReport_MaxDrawdown_ShouldCalculate()
    {
        var equity = new List<EquityPoint>
        {
            new(DateTime.UtcNow.AddHours(-4), 1000m),
            new(DateTime.UtcNow.AddHours(-3), 1100m),  // Peak
            new(DateTime.UtcNow.AddHours(-2), 900m),   // Drawdown: 200 (18.18%)
            new(DateTime.UtcNow.AddHours(-1), 1050m),
            new(DateTime.UtcNow, 1200m),
        };

        var report = PerformanceReport.FromTrades(new(), equity, 1000m);
        report.MaxDrawdown.Should().Be(200m);
        report.MaxDrawdownPercent.Should().BeApproximately(18.18m, 0.1m);
    }
}
