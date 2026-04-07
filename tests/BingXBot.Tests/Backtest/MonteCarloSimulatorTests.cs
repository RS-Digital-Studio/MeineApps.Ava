using BingXBot.Backtest.Reports;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Backtest;

public class MonteCarloSimulatorTests
{
    private static CompletedTrade MakeTrade(decimal pnl) => new(
        "BTC-USDT", Side.Buy, 60000m, pnl > 0 ? 61000m : 59000m, 0.1m, pnl, 0.5m,
        DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, "Test", TradingMode.Backtest);

    [Fact]
    public void ZuWenigTrades_GibtEmptyResult()
    {
        var trades = Enumerable.Range(0, 3).Select(_ => MakeTrade(10m)).ToList();
        var result = MonteCarloSimulator.Simulate(trades, 1000m);
        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void NurGewinne_PositiveReturns()
    {
        var trades = Enumerable.Range(0, 20).Select(_ => MakeTrade(50m)).ToList();
        var result = MonteCarloSimulator.Simulate(trades, 1000m, iterations: 100);

        result.IsEmpty.Should().BeFalse();
        result.Return50.Should().BeGreaterThan(0);
        result.RuinProbability.Should().Be(0);
    }

    [Fact]
    public void GemischteTrades_PlausibleKonfidenzIntervalle()
    {
        var trades = new List<CompletedTrade>();
        for (int i = 0; i < 30; i++)
            trades.Add(MakeTrade(i % 3 == 0 ? -30m : 20m)); // 2/3 Gewinner

        var result = MonteCarloSimulator.Simulate(trades, 1000m, iterations: 500);

        result.Iterations.Should().Be(500);
        result.TradeCount.Should().Be(30);
        result.MaxDrawdown95.Should().BeGreaterThanOrEqualTo(result.MaxDrawdown50);
        result.Return5.Should().BeLessThanOrEqualTo(result.Return50);
        result.Return50.Should().BeLessThanOrEqualTo(result.Return95);
    }

    [Fact]
    public void NurVerluste_HoheRuinWahrscheinlichkeit()
    {
        var trades = Enumerable.Range(0, 20).Select(_ => MakeTrade(-100m)).ToList();
        var result = MonteCarloSimulator.Simulate(trades, 1000m, iterations: 100);

        result.RuinProbability.Should().BeGreaterThan(50); // > 50% Ruin bei reinen Verlusten
        result.Return50.Should().BeLessThan(0);
    }
}
