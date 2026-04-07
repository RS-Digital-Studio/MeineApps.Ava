using BingXBot.Backtest.Reports;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Backtest;

public class CpcvValidatorTests
{
    private static CompletedTrade MakeTrade(decimal pnl, int minutesAgo) => new(
        "BTC-USDT", Side.Buy, 60000m, pnl > 0 ? 61000m : 59000m, 0.1m, pnl, 0.5m,
        DateTime.UtcNow.AddMinutes(-minutesAgo), DateTime.UtcNow.AddMinutes(-minutesAgo + 30),
        "Test", TradingMode.Backtest);

    [Fact]
    public void ZuWenigTrades_GibtEmptyResult()
    {
        var trades = Enumerable.Range(0, 10).Select(i => MakeTrade(10m, i * 60)).ToList();
        var result = CpcvValidator.Validate(trades, 1000m);
        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void GenugTrades_GibtPlausiblesErgebnis()
    {
        // 60 Trades mit gemischten Ergebnissen (chronologisch geordnet)
        var trades = new List<CompletedTrade>();
        for (int i = 0; i < 60; i++)
            trades.Add(MakeTrade(i % 2 == 0 ? 20m : -15m, (60 - i) * 240));

        var result = CpcvValidator.Validate(trades, 1000m);

        result.IsEmpty.Should().BeFalse();
        result.Combinations.Should().Be(15); // C(6,2) = 15
        result.Blocks.Should().Be(6);
        result.ProbabilityOfOverfitting.Should().BeGreaterThanOrEqualTo(0);
        result.ProbabilityOfOverfitting.Should().BeLessThanOrEqualTo(100);
        result.Degradation.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void KonsistenteStrategie_NiedrigePBO()
    {
        // Konsistente Gewinne über die gesamte Periode → niedrige PBO
        var trades = new List<CompletedTrade>();
        for (int i = 0; i < 60; i++)
            trades.Add(MakeTrade(10m, (60 - i) * 240)); // Alle Gewinner

        var result = CpcvValidator.Validate(trades, 1000m);

        result.ProbabilityOfOverfitting.Should().Be(0); // Keine negative OOS-Returns
        result.AvgOutOfSampleReturn.Should().BeGreaterThan(0);
    }

    [Fact]
    public void OosReturnsEnthaeltAlleKombinationen()
    {
        var trades = new List<CompletedTrade>();
        for (int i = 0; i < 60; i++)
            trades.Add(MakeTrade(i % 3 == 0 ? -20m : 15m, (60 - i) * 240));

        var result = CpcvValidator.Validate(trades, 1000m);

        result.OosReturns.Should().HaveCount(15); // C(6,2) = 15 Kombinationen
    }
}
