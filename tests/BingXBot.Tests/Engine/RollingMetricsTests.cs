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

    // === RestoreRollingWindow — Rehydrierung nach Engine-Restart ===
    // Synthetische Backfill-Records (EntryPrice=0 oder Quantity=0) tragen keine verwertbaren
    // Returns und werden ausgefiltert. Nach Restore sollen die Rolling-Properties nur die validen
    // Trades widerspiegeln.

    /// <summary>Trade mit explizit gesetzter ExitTime und Entry/Qty fuer Restore-Reihenfolge-Tests.</summary>
    private static CompletedTrade MakeRestoreTrade(decimal pnl, DateTime exitTime, decimal entryPrice = 60000m, decimal quantity = 0.1m) => new(
        "BTC-USDT", Side.Buy, entryPrice, pnl > 0 ? 61000m : 59000m, quantity, pnl, 0.5m,
        exitTime.AddHours(-1), exitTime, "Restore", TradingMode.Live);

    [Fact]
    public void RestoreRollingWindow_FiltertSynthetischeTrades()
    {
        var rm = CreateManager();
        var baseTime = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var trades = new[]
        {
            MakeRestoreTrade(50m, baseTime),                          // valide → Win
            MakeRestoreTrade(-30m, baseTime.AddHours(1), entryPrice: 0m), // synthetisch (Entry=0) → raus
            MakeRestoreTrade(40m, baseTime.AddHours(2)),                  // valide → Win
            MakeRestoreTrade(-20m, baseTime.AddHours(3), quantity: 0m),   // synthetisch (Qty=0) → raus
        };

        rm.RestoreRollingWindow(trades);

        // Nur 2 valide Trades (beide Wins) → WinRate 100 %, PF aus reinen Gewinnen → 99 (Sentinel).
        rm.RecentTrades.Should().HaveCount(2);
        rm.RollingWinRate.Should().Be(1.0m);
        rm.RollingProfitFactor.Should().Be(99m);
    }

    [Fact]
    public void RestoreRollingWindow_NimmtLetzte30NachExitTimeSortiert()
    {
        var rm = CreateManager();
        var baseTime = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        // 35 valide Trades in absteigender ExitTime-Reihenfolge eingespeist (also unsortiert).
        // Die ersten 5 (aeltesten) sind Verluste, die juengsten 30 Gewinne.
        var trades = new List<CompletedTrade>();
        for (var i = 0; i < 5; i++)
            trades.Add(MakeRestoreTrade(-50m, baseTime.AddHours(i)));      // aelteste → fallen raus
        for (var i = 5; i < 35; i++)
            trades.Add(MakeRestoreTrade(50m, baseTime.AddHours(i)));       // juengste 30 → Gewinne
        // Reihenfolge absichtlich verdrehen, damit der OrderBy(ExitTime) im Code greifen muss.
        trades.Reverse();

        rm.RestoreRollingWindow(trades);

        // Fenster auf 30 begrenzt → nur die juengsten 30 (alle Gewinne) → WinRate 100 %.
        rm.RecentTrades.Should().HaveCount(30);
        rm.RollingWinRate.Should().Be(1.0m);
    }

    [Fact]
    public void RestoreRollingWindow_SpiegeltProfitFactorDerValidenTrades()
    {
        var rm = CreateManager();
        var baseTime = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var trades = new[]
        {
            MakeRestoreTrade(100m, baseTime),
            MakeRestoreTrade(-25m, baseTime.AddHours(1)),
            MakeRestoreTrade(-25m, baseTime.AddHours(2)),
            MakeRestoreTrade(-50m, baseTime.AddHours(3), entryPrice: 0m), // synthetisch → raus
        };

        rm.RestoreRollingWindow(trades);

        // Valide: +100 Gewinn, 50 Verlust → PF = 100/50 = 2.0; WinRate 1/3.
        rm.RecentTrades.Should().HaveCount(3);
        rm.RollingProfitFactor.Should().Be(2.0m);
        rm.RollingWinRate.Should().BeApproximately(1m / 3m, 1e-8m);
    }

    [Fact]
    public void RestoreRollingWindow_LeereListe_FensterBleibtLeer()
    {
        var rm = CreateManager();
        rm.RestoreRollingWindow(Array.Empty<CompletedTrade>());

        rm.RecentTrades.Should().BeEmpty();
        rm.RollingWinRate.Should().Be(0m);
        rm.RollingProfitFactor.Should().Be(0m);
    }

    [Fact]
    public void RestoreRollingWindow_NurSynthetischeTrades_FensterBleibtLeer()
    {
        var rm = CreateManager();
        var baseTime = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var trades = new[]
        {
            MakeRestoreTrade(50m, baseTime, entryPrice: 0m),
            MakeRestoreTrade(-30m, baseTime.AddHours(1), quantity: 0m),
        };

        rm.RestoreRollingWindow(trades);

        rm.RecentTrades.Should().BeEmpty();
        rm.RollingWinRate.Should().Be(0m);
        rm.RollingProfitFactor.Should().Be(0m);
    }

    [Fact]
    public void RestoreRollingWindow_UeberschreibtBestehendesFenster()
    {
        var rm = CreateManager();
        // Vorbestand: 3 Verluste.
        for (var i = 0; i < 3; i++)
            rm.UpdateDailyStats(MakeTrade(-50m));

        var baseTime = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        rm.RestoreRollingWindow(new[]
        {
            MakeRestoreTrade(50m, baseTime),
            MakeRestoreTrade(50m, baseTime.AddHours(1)),
        });

        // Restore leert das Fenster zuerst → nur die 2 neuen Gewinne.
        rm.RecentTrades.Should().HaveCount(2);
        rm.RollingWinRate.Should().Be(1.0m);
    }
}
