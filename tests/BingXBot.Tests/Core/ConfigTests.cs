using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Core;

public class ConfigTests
{
    [Fact]
    public void RiskSettings_ShouldHaveSafeDefaults()
    {
        var s = new RiskSettings();
        s.MaxPositionSizePercent.Should().Be(2m);
        s.MaxDailyDrawdownPercent.Should().Be(5m);
        s.MaxTotalDrawdownPercent.Should().Be(15m);
        s.MaxOpenPositions.Should().Be(3);
        s.MaxLeverage.Should().Be(10m);
        s.EnableTrailingStop.Should().BeTrue();
    }

    [Fact]
    public void BotSettings_DefaultMode_ShouldBePaper()
    {
        var s = new BotSettings();
        s.LastMode.Should().Be(TradingMode.Paper);
        s.Risk.Should().NotBeNull();
        s.Scanner.Should().NotBeNull();
    }

    [Fact]
    public void BacktestSettings_ShouldHaveBingXFees()
    {
        var s = new BacktestSettings();
        s.MakerFee.Should().Be(0.0002m);
        s.TakerFee.Should().Be(0.0005m);
        s.InitialBalance.Should().Be(1000m);
    }

    [Fact]
    public void ScannerSettings_ShouldHaveDefaults()
    {
        var s = new ScannerSettings();
        s.MinVolume24h.Should().Be(10_000_000m);
        s.ScanTimeFrame.Should().Be(TimeFrame.H1);
        s.MaxResults.Should().Be(10);
    }
}
