using BingXBot.Backtest.Reports;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Backtest;

/// <summary>
/// Dedizierte Tests für PerformanceReport.FromTrades - Metriken-Berechnung.
/// </summary>
public class PerformanceReportTests
{
    [Fact]
    public void FromTrades_EmptyTrades_ShouldReturnZeros()
    {
        var report = PerformanceReport.FromTrades(new(), new(), 1000m);
        report.TotalTrades.Should().Be(0);
        report.TotalPnl.Should().Be(0);
        report.WinRate.Should().Be(0);
    }

    [Fact]
    public void FromTrades_AllWinners_ShouldHave100PercentWinRate()
    {
        var trades = new List<CompletedTrade>
        {
            new("BTC", Side.Buy, 100m, 110m, 1m, 10m, 0.5m, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, "Win", TradingMode.Backtest),
            new("BTC", Side.Buy, 100m, 120m, 1m, 20m, 0.5m, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), "Win", TradingMode.Backtest),
        };
        var report = PerformanceReport.FromTrades(trades, new(), 1000m);
        report.WinRate.Should().Be(100m);
        report.ProfitFactor.Should().Be(decimal.MaxValue); // Kein Verlust
    }

    [Fact]
    public void FromTrades_MixedResults_ShouldCalculateCorrectly()
    {
        var trades = new List<CompletedTrade>
        {
            new("BTC", Side.Buy, 100m, 110m, 1m, 10m, 0.5m, DateTime.UtcNow.AddHours(-3), DateTime.UtcNow.AddHours(-2), "Win", TradingMode.Backtest),
            new("BTC", Side.Buy, 100m, 90m, 1m, -10m, 0.5m, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), "Loss", TradingMode.Backtest),
            new("BTC", Side.Buy, 100m, 115m, 1m, 15m, 0.5m, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, "Win", TradingMode.Backtest),
        };
        var report = PerformanceReport.FromTrades(trades, new(), 1000m);
        report.TotalTrades.Should().Be(3);
        report.WinningTrades.Should().Be(2);
        report.LosingTrades.Should().Be(1);
        report.WinRate.Should().BeApproximately(66.67m, 0.1m);
        report.TotalPnl.Should().Be(15m);
        report.AverageWin.Should().Be(12.5m);
        report.AverageLoss.Should().Be(10m);
        report.ProfitFactor.Should().BeGreaterThan(1m);
    }

    [Fact]
    public void FromTrades_MaxDrawdown_ShouldCalculateFromEquity()
    {
        var equity = new List<EquityPoint>
        {
            new(DateTime.UtcNow.AddHours(-3), 1000m),
            new(DateTime.UtcNow.AddHours(-2), 1200m), // Peak
            new(DateTime.UtcNow.AddHours(-1), 900m),  // DD = 300 (25%)
            new(DateTime.UtcNow, 1100m),
        };
        var report = PerformanceReport.FromTrades(new(), equity, 1000m);
        report.MaxDrawdown.Should().Be(300m);
        report.MaxDrawdownPercent.Should().Be(25m);
    }
}
