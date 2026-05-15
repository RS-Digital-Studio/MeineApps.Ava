using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Risk;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingXBot.Tests.Engine;

public class RollingMetricsTests
{
    private static RiskManager CreateManager() => new(new RiskSettings(), NullLogger<RiskManager>.Instance);

    private static CompletedTrade MakeTrade(decimal pnl) => new(
        "BTC-USDT", Side.Buy, 60000m, pnl > 0 ? 61000m : 59000m, 0.1m, pnl, 0.5m,
        DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, "Test", TradingMode.Paper);

    [Fact]
    public void OhneTrades_DefaultWerte()
    {
        var rm = CreateManager();
        rm.RollingWinRate.Should().Be(0);
        rm.RollingProfitFactor.Should().Be(0);
        rm.RollingSharpeRatio.Should().Be(0);
        rm.CurrentConsecutiveLosses.Should().Be(0);
    }

    [Fact]
    public void NurGewinne_WinRate100Prozent()
    {
        var rm = CreateManager();
        for (int i = 0; i < 10; i++)
            rm.UpdateDailyStats(MakeTrade(50m));

        rm.RollingWinRate.Should().Be(1.0m);
        rm.CurrentConsecutiveLosses.Should().Be(0);
    }

    [Fact]
    public void GemischteTrades_KorrekteWinRate()
    {
        var rm = CreateManager();
        rm.UpdateDailyStats(MakeTrade(50m));  // Win
        rm.UpdateDailyStats(MakeTrade(-30m)); // Loss
        rm.UpdateDailyStats(MakeTrade(40m));  // Win
        rm.UpdateDailyStats(MakeTrade(60m));  // Win

        rm.RollingWinRate.Should().Be(0.75m); // 3/4
    }

    [Fact]
    public void ConsecutiveLosses_ZaehltKorrekt()
    {
        var rm = CreateManager();
        rm.UpdateDailyStats(MakeTrade(50m));  // Win → 0
        rm.UpdateDailyStats(MakeTrade(-30m)); // Loss → 1
        rm.UpdateDailyStats(MakeTrade(-20m)); // Loss → 2
        rm.UpdateDailyStats(MakeTrade(-10m)); // Loss → 3

        rm.CurrentConsecutiveLosses.Should().Be(3);

        rm.UpdateDailyStats(MakeTrade(50m)); // Win → Reset
        rm.CurrentConsecutiveLosses.Should().Be(0);
    }

    [Fact]
    public void RollingWindow_MaxSize30()
    {
        var rm = CreateManager();
        // 40 Trades, davon die ersten 10 Verluste, Rest Gewinne
        for (int i = 0; i < 10; i++)
            rm.UpdateDailyStats(MakeTrade(-50m));
        for (int i = 0; i < 30; i++)
            rm.UpdateDailyStats(MakeTrade(50m));

        // Rolling Window enthält nur die letzten 30 → alle Gewinne
        rm.RollingWinRate.Should().Be(1.0m);
    }

    [Fact]
    public void StrategyHealth_WenigerAls10Trades_KeinWarning()
    {
        var rm = CreateManager();
        for (int i = 0; i < 5; i++)
            rm.UpdateDailyStats(MakeTrade(-50m));

        rm.CheckStrategyHealth().Should().BeNull(); // Zu wenig Daten
    }

    [Fact]
    public void StrategyHealth_PauseSchwelle_GibtWarning()
    {
        // Health-Check meldet ab LossStreakPauseAtCount (User-Default 7).
        var settings = new RiskSettings();
        var rm = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        // 10 Gewinne + Pause-Schwelle Verluste in Folge
        for (int i = 0; i < 10; i++)
            rm.UpdateDailyStats(MakeTrade(50m));
        for (int i = 0; i < settings.LossStreakPauseAtCount; i++)
            rm.UpdateDailyStats(MakeTrade(-50m));

        var warning = rm.CheckStrategyHealth();
        warning.Should().NotBeNull();
        warning.Should().Contain("Verluste in Folge");
    }

    [Fact]
    public void TotalPnl_Kumuliert()
    {
        var rm = CreateManager();
        rm.UpdateDailyStats(MakeTrade(100m));
        rm.UpdateDailyStats(MakeTrade(-30m));

        rm.TotalPnl.Should().Be(70m);
    }
}
