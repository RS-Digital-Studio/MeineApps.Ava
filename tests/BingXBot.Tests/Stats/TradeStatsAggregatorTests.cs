using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Trading;
using BingXBot.Trading.Stats;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Stats;

// v1.5.3 Phase 5 — Per-TF + Per-Category Trade-Stats.
public class TradeStatsAggregatorTests
{
    [Fact]
    public void Aggregate_GroupsByTfAndCategory()
    {
        var agg = new TradeStatsAggregator();
        agg.Apply(MakeTrade("BTC-USDT", Side.Buy, pnl: 100m, fee: 1m, tf: TimeFrame.H1));
        agg.Apply(MakeTrade("BTC-USDT", Side.Sell, pnl: -50m, fee: 1m, tf: TimeFrame.H1));
        agg.Apply(MakeTrade("BTC-USDT", Side.Buy, pnl: 200m, fee: 2m, tf: TimeFrame.H4));

        var snapshot = agg.GetSnapshot();
        snapshot.Should().HaveCount(2); // (H1, Crypto, Live), (H4, Crypto, Live)
        var h1 = snapshot.Single(s => s.NavigatorTimeframe == TimeFrame.H1);
        h1.TotalTrades.Should().Be(2);
        h1.WinTrades.Should().Be(1);
        h1.TotalPnl.Should().Be(50m);
        h1.WinRate.Should().Be(0.5m);
    }

    [Fact]
    public void WinRate_HandlesEmptyGroup()
    {
        var agg = new TradeStatsAggregator();
        var snapshot = agg.GetSnapshot();
        snapshot.Should().BeEmpty();
    }

    [Fact]
    public void Replay_FromTrades_RebuildsCorrectly()
    {
        var agg = new TradeStatsAggregator();
        var trades = new[]
        {
            MakeTrade("BTC-USDT", Side.Buy, pnl: 100m, fee: 1m, tf: TimeFrame.H1),
            MakeTrade("BTC-USDT", Side.Sell, pnl: -50m, fee: 1m, tf: TimeFrame.H1),
            MakeTrade("ETH-USDT", Side.Buy, pnl: 75m, fee: 0.5m, tf: TimeFrame.D1),
        };

        agg.ReplayFromTrades(trades);
        var snap = agg.GetSnapshot();
        snap.Should().HaveCount(2);
        snap.Sum(s => s.TotalTrades).Should().Be(3);
        snap.Sum(s => s.TotalPnl).Should().Be(125m);
    }

    [Fact]
    public void Subscribe_TradeCompleted_UpdatesAggregate()
    {
        var bus = new BotEventBus();
        var agg = new TradeStatsAggregator(bus);

        bus.PublishTrade(MakeTrade("BTC-USDT", Side.Buy, pnl: 50m, fee: 1m, tf: TimeFrame.H1));
        bus.PublishTrade(MakeTrade("BTC-USDT", Side.Sell, pnl: -25m, fee: 0.5m, tf: TimeFrame.H1));

        var snap = agg.GetSnapshot();
        snap.Should().HaveCount(1);
        snap[0].TotalTrades.Should().Be(2);
        snap[0].TotalPnl.Should().Be(25m);

        agg.Dispose();
    }

    [Fact]
    public void MaxDrawdown_BerechnetPeakToTrough()
    {
        var agg = new TradeStatsAggregator();
        // PnL-Sequenz: +100 (peak=100), -50 (trough, dd=50), +30 (peak bleibt 100, dd=20),
        // -100 (trough=-20, dd=120). MaxDD = 120.
        agg.Apply(MakeTrade("BTC-USDT", Side.Buy, pnl: 100m, fee: 0m, tf: TimeFrame.H1));
        agg.Apply(MakeTrade("BTC-USDT", Side.Buy, pnl: -50m, fee: 0m, tf: TimeFrame.H1));
        agg.Apply(MakeTrade("BTC-USDT", Side.Buy, pnl: 30m, fee: 0m, tf: TimeFrame.H1));
        agg.Apply(MakeTrade("BTC-USDT", Side.Buy, pnl: -100m, fee: 0m, tf: TimeFrame.H1));

        var snap = agg.GetSnapshot();
        snap[0].MaxDrawdown.Should().Be(120m);
    }

    [Fact]
    public void TradFi_SymbolKlassifizierung_NCFXIstForex()
    {
        var agg = new TradeStatsAggregator();
        agg.Apply(MakeTrade("NCFXEURUSD", Side.Buy, pnl: 10m, fee: 0.1m, tf: TimeFrame.H4));
        var snap = agg.GetSnapshot();
        snap[0].Category.Should().Be(MarketCategory.Forex);
    }

    private static CompletedTrade MakeTrade(string symbol, Side side, decimal pnl, decimal fee, TimeFrame tf,
        TradingMode mode = TradingMode.Live) =>
        new(
            Symbol: symbol,
            Side: side,
            EntryPrice: 50000m,
            ExitPrice: 50000m + (side == Side.Buy ? 100m : -100m),
            Quantity: 0.01m,
            Pnl: pnl,
            Fee: fee,
            EntryTime: DateTime.UtcNow.AddMinutes(-30),
            ExitTime: DateTime.UtcNow,
            Reason: "Test",
            Mode: mode,
            NavigatorTimeframe: tf);
}
