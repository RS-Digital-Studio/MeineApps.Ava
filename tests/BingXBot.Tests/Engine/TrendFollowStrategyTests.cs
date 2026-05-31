using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Strategies;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Verhaltens-Tests fuer die TrendFollow-Strategie. Verifiziert die SL/TP-Geometrie (Long: SL &lt; Entry &lt; TP,
/// Short: TP &lt; Entry &lt; SL), den Market-Entry (PreferLimitOrder=false, EntryPrice=Close) und dass ohne
/// Trend/Breakout kein Signal kommt.
/// </summary>
public class TrendFollowStrategyTests
{
    private static MarketContext Ctx(IReadOnlyList<Candle> candles)
    {
        var last = candles[^1];
        var ticker = new Ticker("TEST-USDT", last.Close, last.Close, last.Close, 1000m, 0m, last.CloseTime);
        return new MarketContext("TEST-USDT", candles, ticker, [], new AccountInfo(1000m, 1000m, 0m, 0m),
            NavigatorTimeframe: TimeFrame.H4);
    }

    private static List<Candle> Trending(int count, decimal start, decimal stepPct, decimal noisePct = 0m)
    {
        var list = new List<Candle>(count);
        var price = start;
        var t = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < count; i++)
        {
            var open = price;
            var close = price * (1m + stepPct);
            var high = Math.Max(open, close) * (1m + noisePct);
            var low = Math.Min(open, close) * (1m - noisePct);
            list.Add(new Candle(t.AddHours(4 * i), open, high, low, close, 1000m, t.AddHours(4 * (i + 1))));
            price = close;
        }
        return list;
    }

    [Fact]
    public void Evaluate_StrongUptrendBreakout_ShouldEmitLongWithValidGeometry()
    {
        // Sauberer, starker Aufwaertstrend → Donchian-Breakout + EMA/DMI bullish.
        var candles = Trending(120, 100m, 0.012m, noisePct: 0.002m);
        var strategy = new TrendFollowStrategy();
        var signal = strategy.Evaluate(Ctx(candles));

        signal.Signal.Should().Be(Signal.Long);
        signal.PreferLimitOrder.Should().BeFalse("TrendFollow nutzt Market-Entry (Backtest-treu)");
        signal.EntryPrice.Should().Be(candles[^1].Close);
        signal.StopLoss.Should().BeLessThan(signal.EntryPrice!.Value);
        signal.TakeProfit.Should().BeGreaterThan(signal.EntryPrice!.Value);
        signal.TakeProfit2.Should().BeGreaterThan(signal.TakeProfit!.Value);

        // RRR von TP1 muss >= 1.5 sein (Default tp1Rrr).
        var risk = signal.EntryPrice!.Value - signal.StopLoss!.Value;
        var reward = signal.TakeProfit!.Value - signal.EntryPrice!.Value;
        (reward / risk).Should().BeApproximately(1.5m, 0.01m);
    }

    [Fact]
    public void Evaluate_StrongDowntrendBreakdown_ShouldEmitShortWithValidGeometry()
    {
        var candles = Trending(120, 100m, -0.012m, noisePct: 0.002m);
        var strategy = new TrendFollowStrategy();
        var signal = strategy.Evaluate(Ctx(candles));

        signal.Signal.Should().Be(Signal.Short);
        signal.StopLoss.Should().BeGreaterThan(signal.EntryPrice!.Value);
        signal.TakeProfit.Should().BeLessThan(signal.EntryPrice!.Value);
        signal.TakeProfit2.Should().BeLessThan(signal.TakeProfit!.Value);
    }

    [Fact]
    public void Evaluate_OpenPositionOnSymbol_ShouldNotEmit()
    {
        var candles = Trending(120, 100m, 0.012m, noisePct: 0.002m);
        var last = candles[^1];
        var ticker = new Ticker("TEST-USDT", last.Close, last.Close, last.Close, 1000m, 0m, last.CloseTime);
        var openPos = new Position("TEST-USDT", Side.Buy, last.Close, last.Close, 1m, 0m, 5m, MarginType.Isolated, last.CloseTime);
        var ctx = new MarketContext("TEST-USDT", candles, ticker, [openPos], new AccountInfo(1000m, 1000m, 0m, 0m),
            NavigatorTimeframe: TimeFrame.H4);

        new TrendFollowStrategy().Evaluate(ctx).Signal.Should().Be(Signal.None);
    }

    [Fact]
    public void Evaluate_InsufficientData_ShouldNotEmit()
    {
        var candles = Trending(20, 100m, 0.012m);
        new TrendFollowStrategy().Evaluate(Ctx(candles)).Signal.Should().Be(Signal.None);
    }
}
