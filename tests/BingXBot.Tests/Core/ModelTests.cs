using BingXBot.Core.Models;
using BingXBot.Core.Enums;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Core;

public class ModelTests
{
    [Fact]
    public void Candle_ShouldStoreAllValues()
    {
        var now = DateTime.UtcNow;
        var candle = new Candle(now, 100m, 110m, 95m, 105m, 1000m, now.AddMinutes(1));
        candle.Open.Should().Be(100m);
        candle.High.Should().Be(110m);
        candle.Low.Should().Be(95m);
        candle.Close.Should().Be(105m);
        candle.Volume.Should().Be(1000m);
    }

    [Fact]
    public void CompletedTrade_ShouldStoreMode()
    {
        var trade = new CompletedTrade("BTC-USDT", Side.Buy, 50000m, 51000m, 0.1m,
            100m, 5m, DateTime.UtcNow, DateTime.UtcNow, "EMA-Cross", TradingMode.Paper);
        trade.Mode.Should().Be(TradingMode.Paper);
        trade.Pnl.Should().Be(100m);
    }

    [Fact]
    public void SignalResult_NullableFields_ShouldWork()
    {
        var signal = new SignalResult(Signal.None, 0m, null, null, null, "Kein Signal");
        signal.EntryPrice.Should().BeNull();
        signal.StopLoss.Should().BeNull();
        signal.TakeProfit.Should().BeNull();
        signal.Signal.Should().Be(Signal.None);
    }

    [Fact]
    public void StrategyParameter_ValueType_ShouldBeString()
    {
        var param = new StrategyParameter("Period", "EMA Periode", "int", 20, 5, 200, 1);
        param.ValueType.Should().Be("int");
        param.DefaultValue.Should().Be(20);
    }

    [Fact]
    public void RiskCheckResult_Allowed_ShouldWork()
    {
        var result = new RiskCheckResult(true, null, 0.1m);
        result.IsAllowed.Should().BeTrue();
        result.RejectionReason.Should().BeNull();
    }

    [Fact]
    public void RiskCheckResult_Rejected_ShouldHaveReason()
    {
        var result = new RiskCheckResult(false, "Max Positionen erreicht", 0m);
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("Max");
    }
}
