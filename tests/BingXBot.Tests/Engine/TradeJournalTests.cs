using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Analysis;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Tests für TradeJournal - Thread-sichere Trade-Aufzeichnung und Metriken.
/// </summary>
public class TradeJournalTests
{
    private static CompletedTrade MakeTrade(decimal pnl) => new(
        "BTC-USDT", Side.Buy, 50000m, 50000m + pnl * 100, 0.1m, pnl, 1m,
        DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow, "Test", TradingMode.Paper);

    [Fact]
    public async Task RecordTrade_ShouldAddToList()
    {
        var journal = new TradeJournal();
        await journal.RecordTradeAsync(MakeTrade(100));
        journal.GetTrades().Should().HaveCount(1);
    }

    [Fact]
    public async Task WinRate_ShouldCalculateCorrectly()
    {
        var journal = new TradeJournal();
        await journal.RecordTradeAsync(MakeTrade(100));
        await journal.RecordTradeAsync(MakeTrade(-50));
        await journal.RecordTradeAsync(MakeTrade(80));
        journal.WinRate.Should().BeApproximately(66.67m, 0.1m);
    }

    [Fact]
    public async Task ProfitFactor_ShouldCalculateCorrectly()
    {
        var journal = new TradeJournal();
        await journal.RecordTradeAsync(MakeTrade(100));
        await journal.RecordTradeAsync(MakeTrade(-50));
        journal.ProfitFactor.Should().Be(2m); // 100 / 50
    }

    [Fact]
    public async Task TotalPnl_ShouldSumAllTrades()
    {
        var journal = new TradeJournal();
        await journal.RecordTradeAsync(MakeTrade(100));
        await journal.RecordTradeAsync(MakeTrade(-30));
        await journal.RecordTradeAsync(MakeTrade(50));
        journal.TotalPnl.Should().Be(120m);
    }

    [Fact]
    public void Clear_ShouldResetEverything()
    {
        var journal = new TradeJournal();
        journal.Clear();
        journal.GetTrades().Should().BeEmpty();
        journal.WinRate.Should().Be(0);
        journal.TotalPnl.Should().Be(0);
    }

    [Fact]
    public async Task TradeRecorded_Event_ShouldFire()
    {
        var journal = new TradeJournal();
        CompletedTrade? received = null;
        journal.TradeRecorded += (_, trade) => received = trade;

        var trade = MakeTrade(100);
        await journal.RecordTradeAsync(trade);

        received.Should().NotBeNull();
        received!.Pnl.Should().Be(100m);
    }
}
