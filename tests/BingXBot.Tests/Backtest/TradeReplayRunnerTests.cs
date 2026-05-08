using BingXBot.Backtest;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Backtest;

// v1.6.4 Phase 13 — Trade-Replay-Runner.
//
// Tests fokussieren auf die pure Compare-Function. Volle BacktestEngine-Integration
// erfordert historische Klines + Strategy-Setup — abgedeckt durch FiveMonthLiveBacktest.
public class TradeReplayRunnerTests
{
    [Fact]
    public void Replay_SimpleTpHit_VerdictIdentical()
    {
        // Beide Trades sind nahezu identisch (PnL-Drift < 1 %, Reason = TP).
        var live = MakeTrade("BTC-USDT", entry: 50000m, exit: 51000m, pnl: 100m, reason: "Take-Profit bei 51000");
        var bt = MakeTrade("BTC-USDT", entry: 50001m, exit: 51000m, pnl: 100.5m, reason: "Take-Profit bei 51000");

        var report = TradeReplayRunner.CompareTrades(live, bt);
        report.Verdict.Should().Be(TradeReplayVerdict.Identical);
        report.ExitReasonSame.Should().BeTrue();
    }

    [Fact]
    public void Replay_SlHit_VerdictIdentical()
    {
        var live = MakeTrade("BTC-USDT", entry: 50000m, exit: 49500m, pnl: -50m, reason: "Stop-Loss bei 49500");
        var bt = MakeTrade("BTC-USDT", entry: 50001m, exit: 49500m, pnl: -50.2m, reason: "Stop-Loss bei 49500");

        var report = TradeReplayRunner.CompareTrades(live, bt);
        report.Verdict.Should().Be(TradeReplayVerdict.Identical);
    }

    [Fact]
    public void Replay_RunnerTrade_VerdictMinorDrift()
    {
        // Beim Runner ist Trail-Logik eng → Drift 1-5 % erwartet.
        var live = MakeTrade("BTC-USDT", entry: 50000m, exit: 52500m, pnl: 250m, reason: "Runner-Exit: Trailing-SL");
        var bt = MakeTrade("BTC-USDT", entry: 50000m, exit: 52440m, pnl: 244m, reason: "Runner-Exit: Trailing-SL");

        var report = TradeReplayRunner.CompareTrades(live, bt);
        report.Verdict.Should().Be(TradeReplayVerdict.MinorDrift);
    }

    [Fact]
    public void Replay_BacktestNoTrade_VerdictError()
    {
        var live = MakeTrade("BTC-USDT", entry: 50000m, exit: 51000m, pnl: 100m, reason: "TP1");
        var report = TradeReplayRunner.CompareTrades(live, backtest: null);
        report.Verdict.Should().Be(TradeReplayVerdict.Error);
        report.ErrorDetail.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Replay_LogicMismatch_PnlDriftHuge()
    {
        // Live wins +100, Backtest verliert -50 → PnL-Drift > 100 % → LogicMismatch.
        var live = MakeTrade("BTC-USDT", entry: 50000m, exit: 51000m, pnl: 100m, reason: "TP");
        var bt = MakeTrade("BTC-USDT", entry: 50000m, exit: 49500m, pnl: -50m, reason: "Stop-Loss");

        var report = TradeReplayRunner.CompareTrades(live, bt);
        report.Verdict.Should().Be(TradeReplayVerdict.LogicMismatch);
        report.ExitReasonSame.Should().BeFalse();
    }

    [Fact]
    public void ExtractExitCategory_KennsKategorien()
    {
        TradeReplayRunner.ExtractExitCategory("Take-Profit bei 51000").Should().Be("TP");
        TradeReplayRunner.ExtractExitCategory("Stop-Loss bei 49000").Should().Be("SL");
        TradeReplayRunner.ExtractExitCategory("Runner-Exit: Trailing-SL").Should().Be("Runner");
        TradeReplayRunner.ExtractExitCategory("Notfall-Stop").Should().Be("Emergency");
        TradeReplayRunner.ExtractExitCategory("Partial Close (TP1)").Should().Be("TP");
        TradeReplayRunner.ExtractExitCategory("").Should().Be("");
    }

    private static CompletedTrade MakeTrade(string symbol, decimal entry, decimal exit, decimal pnl, string reason) =>
        new(
            Symbol: symbol,
            Side: pnl >= 0m ? Side.Buy : Side.Buy,
            EntryPrice: entry,
            ExitPrice: exit,
            Quantity: 0.01m,
            Pnl: pnl,
            Fee: 0.5m,
            EntryTime: DateTime.UtcNow.AddMinutes(-30),
            ExitTime: DateTime.UtcNow,
            Reason: reason,
            Mode: TradingMode.Live,
            NavigatorTimeframe: TimeFrame.H1);
}
