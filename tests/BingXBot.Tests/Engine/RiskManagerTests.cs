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
    private static MarketContext CreateContext(int openPositions = 0, decimal balance = 10000m, string symbol = "BTC-USDT", decimal unrealizedPnl = 0m, List<Position>? customPositions = null)
    {
        var positions = customPositions ?? Enumerable.Range(0, openPositions)
            .Select(i => new Position($"SYM{i}-USDT", Side.Buy, 100m, 100m, 1m, 0m, 10m, MarginType.Cross, DateTime.UtcNow))
            .ToList();

        return new MarketContext(
            symbol,
            new List<Candle>(),
            new Ticker(symbol, 50000m, 49999m, 50001m, 10000000m, 5m, DateTime.UtcNow),
            positions,
            new AccountInfo(balance, balance, unrealizedPnl, 0m));
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
    public void CalculatePositionSize_NullStopLoss_ShouldUseConservativeFallback()
    {
        // Ohne SL: halbes Risiko als Margin, max 5x Leverage (statt 10x)
        // 2% von 10000 = 200 USDT Risiko, halbe = 100 USDT Margin, 5x Lev = 500 USDT Position
        // 500 / 50000 = 0.01 BTC
        var settings = new RiskSettings { MaxPositionSizePercent = 2m, MaxLeverage = 10m };
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        var size = risk.CalculatePositionSize("BTC-USDT", 50000m, null, account);
        size.Should().Be(0.01m);
    }

    [Fact]
    public void CalculatePositionSize_NullStopLoss_ShouldBeSmallerThanWithStopLoss()
    {
        // Position ohne SL muss kleiner sein als die gleiche mit engem SL (unter sonst gleichen Bedingungen)
        var settings = new RiskSettings { MaxPositionSizePercent = 2m, MaxLeverage = 10m };
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        var withSl = risk.CalculatePositionSize("BTC-USDT", 50000m, 49000m, account);
        var withoutSl = risk.CalculatePositionSize("BTC-USDT", 50000m, null, account);
        withoutSl.Should().BeLessThan(withSl, "Ohne SL muss konservativer gehandelt werden");
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

    [Fact]
    public void DailyDrawdown_WithUnrealizedLoss_ShouldBlockEarlier()
    {
        // Realisierte Verluste allein reichen nicht (3% < 5%), aber mit unrealisierten
        // Verlusten (-300 = 3%) kommen wir auf 6% >= 5% -> blockiert
        var settings = new RiskSettings { MaxDailyDrawdownPercent = 5m, MaxOpenPositions = 10 };
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);

        // 300 realisierter Verlust = 3%
        risk.UpdateDailyStats(new CompletedTrade("BTC-USDT", Side.Buy, 50000m, 47500m, 0.1m, -300m, 5m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));

        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");

        // Ohne unrealisierte Verluste (keine offenen Positionen): 3% < 5% -> erlaubt
        var resultOk = risk.ValidateTrade(signal, CreateContext(balance: 10000m));
        resultOk.IsAllowed.Should().BeTrue();

        // Mit -300 unrealisierten Verlusten auf einer offenen Position: 3% + 3% = 6% >= 5% -> blockiert
        var losingPosition = new Position("ETH-USDT", Side.Buy, 3000m, 2700m, 1m, -300m, 10m, MarginType.Cross, DateTime.UtcNow);
        var resultBlocked = risk.ValidateTrade(signal, CreateContext(balance: 10000m, customPositions: new List<Position> { losingPosition }));
        resultBlocked.IsAllowed.Should().BeFalse();
        resultBlocked.RejectionReason.Should().Contain("Drawdown");
    }

    [Fact]
    public void DailyDrawdown_MixedPositions_ShouldUseIndividualLosses()
    {
        // Netto-UnrealizedPnl = +200 -300 = -100 (nur 1%), aber der einzelne Verlust
        // von -300 (3%) muss in den Drawdown einfliessen. Bei 3% realisiert + 3% unrealisiert = 6% -> blockiert
        var settings = new RiskSettings { MaxDailyDrawdownPercent = 5m, MaxOpenPositions = 10 };
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);

        // 300 realisierter Verlust = 3%
        risk.UpdateDailyStats(new CompletedTrade("BTC-USDT", Side.Buy, 50000m, 47500m, 0.1m, -300m, 5m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));

        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");

        // Position A: +200, Position B: -300. Netto = -100 (wuerde nur 1% sein),
        // aber Summe der Verluste = -300 (3%). 3% realisiert + 3% unrealisiert = 6% >= 5% -> blockiert
        var positions = new List<Position>
        {
            new("ETH-USDT", Side.Buy, 3000m, 3200m, 1m, 200m, 10m, MarginType.Cross, DateTime.UtcNow),
            new("SOL-USDT", Side.Buy, 150m, 120m, 10m, -300m, 10m, MarginType.Cross, DateTime.UtcNow)
        };
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m, customPositions: positions));
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("Drawdown");
    }

    [Fact]
    public void DailyDrawdown_WithUnrealizedProfit_ShouldNotAffectDrawdown()
    {
        // Positive unrealisierte PnL soll den Drawdown nicht reduzieren
        var settings = new RiskSettings { MaxDailyDrawdownPercent = 5m, MaxOpenPositions = 10 };
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);

        // 550 realisierter Verlust = 5.5% -> blockiert
        risk.UpdateDailyStats(new CompletedTrade("BTC-USDT", Side.Buy, 50000m, 47500m, 0.1m, -550m, 5m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));

        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");

        // Trotz +500 unrealisierter Profit (Position im Plus) bleibt der Drawdown bei 5.5% -> blockiert
        var profitPosition = new Position("ETH-USDT", Side.Buy, 3000m, 3500m, 1m, 500m, 10m, MarginType.Cross, DateTime.UtcNow);
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m, customPositions: new List<Position> { profitPosition }));
        result.IsAllowed.Should().BeFalse();
    }
}
