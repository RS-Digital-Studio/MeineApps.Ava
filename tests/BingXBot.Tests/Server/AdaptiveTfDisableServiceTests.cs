using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Server.Services;
using BingXBot.Trading;
using BingXBot.Trading.Stats;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingXBot.Tests.Server;

// v1.6.6 Phase 17 — Adaptive TF-Disable.
public class AdaptiveTfDisableServiceTests
{
    [Fact]
    public void FlagOff_NoDisable()
    {
        var bus = new BotEventBus();
        var agg = new TradeStatsAggregator(bus);
        var settings = new ScannerSettings { EnableAdaptiveTfDisable = false, AdaptiveTfMinTrades = 5 };
        var svc = new AdaptiveTfDisableService(agg, settings, NullLogger<AdaptiveTfDisableService>.Instance);

        // 10 Verlust-Trades auf H1 → ohne Flag bleibt H1 enabled.
        for (int i = 0; i < 10; i++)
            bus.PublishTrade(MakeTrade(TimeFrame.H1, pnl: -10m));

        svc.Run();
        svc.IsTfDisabled(TimeFrame.H1).Should().BeFalse();
    }

    [Fact]
    public void BelowMinTrades_NoDisable()
    {
        var bus = new BotEventBus();
        var agg = new TradeStatsAggregator(bus);
        var settings = new ScannerSettings
        {
            EnableAdaptiveTfDisable = true,
            AdaptiveTfMinTrades = 20,
            AdaptiveTfMinWinRate = 0.5m,
        };
        var svc = new AdaptiveTfDisableService(agg, settings, NullLogger<AdaptiveTfDisableService>.Instance);

        // Nur 10 Trades → unter Min-Sample-Size (20). Egal wie schlecht die WinRate ist.
        for (int i = 0; i < 10; i++) bus.PublishTrade(MakeTrade(TimeFrame.H1, pnl: -10m));

        svc.Run();
        svc.IsTfDisabled(TimeFrame.H1).Should().BeFalse();
    }

    [Fact]
    public void WinRateBelowThreshold_TfDisabled()
    {
        var bus = new BotEventBus();
        var agg = new TradeStatsAggregator(bus);
        var settings = new ScannerSettings
        {
            EnableAdaptiveTfDisable = true,
            AdaptiveTfMinTrades = 20,
            AdaptiveTfMinWinRate = 0.5m,
            AdaptiveTfDisableHours = 24,
        };
        var svc = new AdaptiveTfDisableService(agg, settings, NullLogger<AdaptiveTfDisableService>.Instance);

        // 30 Verlust-Trades → WinRate 0 % < 50 % → Disable.
        for (int i = 0; i < 30; i++) bus.PublishTrade(MakeTrade(TimeFrame.H1, pnl: -10m));

        svc.Run();
        svc.IsTfDisabled(TimeFrame.H1).Should().BeTrue();
        svc.GetDisabledUntil(TimeFrame.H1).Should().NotBeNull();
        svc.GetDisabledUntil(TimeFrame.H1)!.Value.Should().BeAfter(DateTime.UtcNow.AddHours(23));
    }

    [Fact]
    public void WinRateGoodEnough_TfStaysEnabled()
    {
        var bus = new BotEventBus();
        var agg = new TradeStatsAggregator(bus);
        var settings = new ScannerSettings
        {
            EnableAdaptiveTfDisable = true,
            AdaptiveTfMinTrades = 10,
            AdaptiveTfMinWinRate = 0.4m,
        };
        var svc = new AdaptiveTfDisableService(agg, settings, NullLogger<AdaptiveTfDisableService>.Instance);

        // 6 Wins + 4 Losses = 60 % WinRate > 40 % → bleibt enabled.
        for (int i = 0; i < 6; i++) bus.PublishTrade(MakeTrade(TimeFrame.H1, pnl: 10m));
        for (int i = 0; i < 4; i++) bus.PublishTrade(MakeTrade(TimeFrame.H1, pnl: -5m));

        svc.Run();
        svc.IsTfDisabled(TimeFrame.H1).Should().BeFalse();
    }

    [Fact]
    public void DisableExpires_AfterHours()
    {
        var bus = new BotEventBus();
        var agg = new TradeStatsAggregator(bus);
        var settings = new ScannerSettings
        {
            EnableAdaptiveTfDisable = true,
            AdaptiveTfMinTrades = 10,
            AdaptiveTfMinWinRate = 0.5m,
            AdaptiveTfDisableHours = -1, // Negative Hours → Disable-Cutoff in der Vergangenheit
        };
        var svc = new AdaptiveTfDisableService(agg, settings, NullLogger<AdaptiveTfDisableService>.Instance);

        for (int i = 0; i < 30; i++) bus.PublishTrade(MakeTrade(TimeFrame.H1, pnl: -10m));
        svc.Run();
        // Bei DisableHours=-1 liegt der Cutoff in der Vergangenheit → IsTfDisabled returnt false
        // sobald die Zeit ueberschritten ist.
        svc.IsTfDisabled(TimeFrame.H1).Should().BeFalse();
    }

    private static CompletedTrade MakeTrade(TimeFrame tf, decimal pnl) =>
        new(
            Symbol: "BTC-USDT",
            Side: Side.Buy,
            EntryPrice: 50000m,
            ExitPrice: 50000m + pnl,
            Quantity: 0.01m,
            Pnl: pnl,
            Fee: 0.5m,
            EntryTime: DateTime.UtcNow.AddMinutes(-30),
            ExitTime: DateTime.UtcNow,
            Reason: "Test",
            Mode: TradingMode.Live,
            NavigatorTimeframe: tf);
}
