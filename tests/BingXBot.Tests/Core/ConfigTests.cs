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
        s.MaxPositionSizePercent.Should().Be(10m);
        s.MaxDailyDrawdownPercent.Should().Be(0m); // Deaktiviert
        s.MaxTotalDrawdownPercent.Should().Be(10m);
        s.MaxOpenPositions.Should().Be(10);
        s.MaxLeverage.Should().Be(25m);
        s.EnableTrailingStop.Should().BeTrue();
        s.EnableMultiStageExit.Should().BeTrue();
        s.Tp1CloseRatio.Should().Be(0.3m);
        s.Tp2CloseRatio.Should().Be(0.3m);
        s.MinRiskRewardRatio.Should().Be(1.0m);
        s.SmartBreakevenAtrMultiplier.Should().Be(0.5m);
        s.MaxHoldHours.Should().Be(48);
        s.CooldownHours.Should().Be(0); // Deaktiviert
        s.MaxTradesPerDay.Should().Be(0); // 0 = unbegrenzt (geändert)
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
        s.MinVolume24h.Should().Be(20_000_000m);
        s.ScanTimeFrame.Should().Be(TimeFrame.H4);
        s.MaxResults.Should().Be(50);
        s.ScanIntervalSeconds.Should().Be(300); // H4 = 5min (Ticker + Kandidaten-Check)
    }
}
