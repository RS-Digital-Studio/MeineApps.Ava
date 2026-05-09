using System.Text.Json;
using BingXBot.Backtest.Reports;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using VerifyXunit;
using Xunit;

namespace BingXBot.Tests.Backtest;

// Phase 18 / H4 — Snapshot-Tests fuer PerformanceReport.FromTrades.
// Schuetzt vor stillen Drifts in den Metriken (WinRate, ProfitFactor, Sharpe, Sortino, etc.)
// bei Refactorings. Synthetische deterministische Trades als Input — nicht der echte
// BacktestEngine-Run (der haette zu viele bewegliche Inputs fuer einen sinnvollen Snapshot).
//
// Beim ersten Lauf wird die .verified.txt-Baseline angelegt — anpassen via dotnet verify accept.
public class PerformanceReportSnapshotTests
{
    /// <summary>Deterministische Trade-Sequenz — gleiche Eingaben muessen gleichen Output liefern.</summary>
    private static List<CompletedTrade> BuildSyntheticTrades()
    {
        var trades = new List<CompletedTrade>();
        var entry = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

        // 15 Trades — Mix aus Wins/Losses fuer aussagekraeftige Metriken (WinRate, Streaks, PF).
        // Pattern: WWLWWWLWWLLWWWL → 10W/5L, 67% WinRate, kein Streak-Edge-Case.
        var pnls = new decimal[] { 50, 80, -40, 120, 60, 90, -50, 100, 70, -30, -20, 110, 95, 85, -45 };
        for (int i = 0; i < pnls.Length; i++)
        {
            var t = entry.AddDays(i);
            trades.Add(new CompletedTrade(
                Symbol: "BTC-USDT",
                Side: i % 2 == 0 ? Side.Buy : Side.Sell,
                EntryPrice: 50000m + i * 10m,
                ExitPrice: 50000m + i * 10m + (pnls[i] / 0.01m), // synthetisch
                Quantity: 0.01m,
                Pnl: pnls[i],
                Fee: 0.5m,
                EntryTime: t,
                ExitTime: t.AddHours(4),
                Reason: pnls[i] > 0 ? "TP1" : "SL",
                Mode: TradingMode.Backtest,
                NavigatorTimeframe: TimeFrame.H4));
        }
        return trades;
    }

    private static List<EquityPoint> BuildEquityCurve(List<CompletedTrade> trades, decimal initialBalance)
    {
        var curve = new List<EquityPoint>
        {
            new(trades[0].EntryTime.AddMinutes(-1), initialBalance)
        };
        var balance = initialBalance;
        foreach (var t in trades)
        {
            balance += t.Pnl - t.Fee;
            curve.Add(new EquityPoint(t.ExitTime, balance));
        }
        return curve;
    }

    [Fact]
    public Task PerformanceReport_FromSyntheticTrades_ProducesStableMetrics()
    {
        var trades = BuildSyntheticTrades();
        var initial = 10_000m;
        var equity = BuildEquityCurve(trades, initial);
        var report = PerformanceReport.FromTrades(trades, equity, initial);

        // Snapshot nur die deterministischen Metriken — Monte Carlo + CPCV sind stochastisch
        // und wuerden bei jedem Lauf andere Werte erzeugen, deshalb ausgeblendet.
        var snapshot = new
        {
            totalTrades = report.TotalTrades,
            winningTrades = report.WinningTrades,
            losingTrades = report.LosingTrades,
            winRate = report.WinRate,
            totalPnl = report.TotalPnl,
            profitFactor = Math.Round(report.ProfitFactor, 4),
            averageWin = Math.Round(report.AverageWin, 4),
            averageLoss = Math.Round(report.AverageLoss, 4),
            averageRrr = Math.Round(report.AverageRrr, 4),
            maxDrawdown = Math.Round(report.MaxDrawdown, 4),
            maxDrawdownPercent = Math.Round(report.MaxDrawdownPercent, 4),
            calmarRatio = Math.Round(report.CalmarRatio, 4),
            recoveryFactor = Math.Round(report.RecoveryFactor, 4),
            maxConsecutiveWins = report.MaxConsecutiveWins,
            maxConsecutiveLosses = report.MaxConsecutiveLosses,
            // Sharpe + Sortino sind deterministisch, aber float-genauigkeit erfordert Rundung.
            sharpeRatio = Math.Round(report.SharpeRatio, 4),
            sortinoRatio = Math.Round(report.SortinoRatio, 4),
            averageHoldTimeMinutes = (int)report.AverageHoldTime.TotalMinutes
        };
        return Verifier.Verify(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public Task PerformanceReport_AllWins_NoLosses_ProducesExpectedShape()
    {
        // Edge-Case: 100% WinRate. ProfitFactor = MaxValue Wert testen.
        var entry = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var trades = new List<CompletedTrade>();
        for (int i = 0; i < 12; i++)
        {
            var t = entry.AddDays(i);
            trades.Add(new CompletedTrade("ETH-USDT", Side.Buy, 3000m, 3050m, 1m, 50m, 0.5m,
                t, t.AddHours(2), "TP", TradingMode.Backtest, TimeFrame.H4));
        }
        var initial = 5_000m;
        var equity = BuildEquityCurve(trades, initial);
        var report = PerformanceReport.FromTrades(trades, equity, initial);

        var snapshot = new
        {
            winRate = report.WinRate,
            losingTrades = report.LosingTrades,
            // ProfitFactor: bei 0 Loss = MaxValue → in Snapshot als String fuer Stabilitaet.
            profitFactorIsMaxValue = report.ProfitFactor == decimal.MaxValue,
            maxConsecutiveWins = report.MaxConsecutiveWins,
            maxConsecutiveLosses = report.MaxConsecutiveLosses,
            totalPnl = report.TotalPnl
        };
        return Verifier.Verify(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
    }
}
