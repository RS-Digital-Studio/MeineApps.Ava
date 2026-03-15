using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Risk;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingXBot.Tests.Engine;

public class RiskManagerTests
{
    private static MarketContext CreateContext(int openPositions = 0, decimal balance = 10000m, string symbol = "BTC-USDT")
    {
        var positions = Enumerable.Range(0, openPositions)
            .Select(i => new Position($"SYM{i}-USDT", Side.Buy, 100m, 100m, 1m, 0m, 10m, MarginType.Cross, DateTime.UtcNow))
            .ToList();

        return new MarketContext(
            symbol,
            new List<Candle>(),
            new Ticker(symbol, 50000m, 49999m, 50001m, 10000000m, 5m, DateTime.UtcNow),
            positions,
            new AccountInfo(balance, balance, 0m, 0m));
    }

    [Fact]
    public void ValidateTrade_NoSignal_ShouldReject()
    {
        var risk = new RiskManager(new RiskSettings(), NullLogger<RiskManager>.Instance);
        var signal = new SignalResult(Signal.None, 0m, null, null, null, "Kein Signal");
        var result = risk.ValidateTrade(signal, CreateContext());
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void ValidateTrade_MaxPositionsReached_ShouldReject()
    {
        var settings = new RiskSettings { MaxOpenPositions = 2 };
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        var result = risk.ValidateTrade(signal, CreateContext(openPositions: 2));
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("Max");
    }

    [Fact]
    public void ValidateTrade_UnderLimits_ShouldAllow()
    {
        var risk = new RiskManager(new RiskSettings(), NullLogger<RiskManager>.Instance);
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        var result = risk.ValidateTrade(signal, CreateContext());
        result.IsAllowed.Should().BeTrue();
        result.AdjustedPositionSize.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void CalculatePositionSize_WithStopLoss_ShouldCalculate()
    {
        var settings = new RiskSettings { MaxPositionSizePercent = 2m, MaxLeverage = 10m };
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        var size = risk.CalculatePositionSize("BTC-USDT", 50000m, 49000m, account);
        size.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void CalculatePositionSize_NullStopLoss_ShouldUseFallback()
    {
        var settings = new RiskSettings { MaxPositionSizePercent = 2m, MaxLeverage = 10m };
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        var size = risk.CalculatePositionSize("BTC-USDT", 50000m, null, account);
        size.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void DailyDrawdown_ShouldBlockAfterThreshold()
    {
        var settings = new RiskSettings { MaxDailyDrawdownPercent = 5m, MaxOpenPositions = 10 };
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);

        // Verluste die 5% DD verursachen (550 von 10000 = 5.5%)
        risk.UpdateDailyStats(new CompletedTrade("BTC-USDT", Side.Buy, 50000m, 47500m, 0.1m, -300m, 5m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));
        risk.UpdateDailyStats(new CompletedTrade("ETH-USDT", Side.Buy, 3000m, 2800m, 1m, -250m, 3m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));

        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m));
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("Drawdown");
    }

    [Fact]
    public void ResetDailyStats_ShouldClearDailyPnl()
    {
        var settings = new RiskSettings { MaxDailyDrawdownPercent = 5m, MaxOpenPositions = 10 };
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        risk.UpdateDailyStats(new CompletedTrade("BTC-USDT", Side.Buy, 50000m, 47500m, 0.1m, -600m, 5m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));

        risk.ResetDailyStats();

        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m));
        result.IsAllowed.Should().BeTrue();
    }
}
