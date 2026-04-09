using BingXBot.Core.Models;
using BingXBot.Core.Models.ATI;

namespace BingXBot.Backtest.Reports;

public class PerformanceReport
{
    public List<CompletedTrade> Trades { get; set; } = new();
    public List<EquityPoint> EquityCurve { get; set; } = new();
    public decimal TotalPnl { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal WinRate { get; set; }
    public decimal ProfitFactor { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal AverageRrr { get; set; }
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public TimeSpan AverageHoldTime { get; set; }

    // Erweiterte Metriken
    public decimal CalmarRatio { get; set; }
    public int MaxConsecutiveLosses { get; set; }
    public int MaxConsecutiveWins { get; set; }
    public decimal SortinoRatio { get; set; }
    public decimal RecoveryFactor { get; set; }

    // Monte Carlo Simulation
    public MonteCarloResult? MonteCarlo { get; set; }

    // Regime-spezifische Metriken (aufgeschlüsselt nach MarketRegime)
    public Dictionary<MarketRegime, RegimeMetrics> RegimeBreakdown { get; set; } = new();

    // CPCV: Overfitting-Wahrscheinlichkeit
    public CpcvResult? Cpcv { get; set; }

    public static PerformanceReport FromTrades(List<CompletedTrade> trades, List<EquityPoint> equityCurve, decimal initialBalance)
    {
        var report = new PerformanceReport
        {
            Trades = trades,
            EquityCurve = equityCurve,
            TotalTrades = trades.Count
        };

        // Max Drawdown aus Equity-Kurve (unabhängig von Trades)
        if (equityCurve.Count > 0)
        {
            decimal peak = equityCurve[0].Equity;
            decimal maxDD = 0;
            decimal maxDDPercent = 0;

            foreach (var point in equityCurve)
            {
                if (point.Equity > peak) peak = point.Equity;
                var dd = peak - point.Equity;
                if (dd > maxDD)
                {
                    maxDD = dd;
                    maxDDPercent = peak > 0 ? dd / peak * 100m : 0m;
                }
            }
            report.MaxDrawdown = maxDD;
            report.MaxDrawdownPercent = maxDDPercent;
        }

        if (trades.Count == 0) return report;

        // Basis-Statistiken
        report.TotalPnl = trades.Sum(t => t.Pnl);
        report.WinningTrades = trades.Count(t => t.Pnl > 0);
        report.LosingTrades = trades.Count(t => t.Pnl <= 0);
        report.WinRate = (decimal)report.WinningTrades / trades.Count * 100m;

        // Durchschnitte
        var winners = trades.Where(t => t.Pnl > 0).ToList();
        var losers = trades.Where(t => t.Pnl < 0).ToList();
        report.AverageWin = winners.Count > 0 ? winners.Average(t => t.Pnl) : 0m;
        report.AverageLoss = losers.Count > 0 ? Math.Abs(losers.Average(t => t.Pnl)) : 0m;
        report.AverageRrr = report.AverageLoss > 0 ? report.AverageWin / report.AverageLoss : 0m;
        report.AverageHoldTime = TimeSpan.FromTicks((long)trades.Average(t => (t.ExitTime - t.EntryTime).Ticks));

        // Profit Factor
        var grossProfit = winners.Sum(t => t.Pnl);
        var grossLoss = Math.Abs(losers.Sum(t => t.Pnl));
        report.ProfitFactor = grossLoss > 0 ? grossProfit / grossLoss : grossProfit > 0 ? decimal.MaxValue : 0m;

        // Calmar Ratio: Return / MaxDrawdown (vereinfacht, nicht annualisiert)
        report.CalmarRatio = report.MaxDrawdownPercent > 0
            ? (report.TotalPnl / initialBalance * 100m) / report.MaxDrawdownPercent
            : 0m;

        // Recovery Factor: TotalPnl / |MaxDrawdown|
        report.RecoveryFactor = report.MaxDrawdown > 0
            ? report.TotalPnl / report.MaxDrawdown
            : 0m;

        // Max aufeinanderfolgende Gewinne/Verluste
        {
            int currentWins = 0, currentLosses = 0;
            int maxWins = 0, maxLosses = 0;
            foreach (var trade in trades)
            {
                if (trade.Pnl > 0)
                {
                    currentWins++;
                    currentLosses = 0;
                    if (currentWins > maxWins) maxWins = currentWins;
                }
                else
                {
                    currentLosses++;
                    currentWins = 0;
                    if (currentLosses > maxLosses) maxLosses = currentLosses;
                }
            }
            report.MaxConsecutiveWins = maxWins;
            report.MaxConsecutiveLosses = maxLosses;
        }

        // Sharpe Ratio (annualisiert) mit Running Balance.
        // Returns werden relativ zur aktuellen Balance berechnet, nicht zur initialen.
        // Verhindert Verzerrung bei wachsendem/schrumpfendem Account.
        if (trades.Count > 1 && initialBalance > 0)
        {
            var returns = new List<double>();
            var runningBalance = (double)initialBalance;

            foreach (var trade in trades)
            {
                if (runningBalance > 0)
                {
                    returns.Add((double)trade.Pnl / runningBalance);
                    runningBalance += (double)trade.Pnl;
                }
            }

            if (returns.Count > 1)
            {
                var avgReturn = returns.Average();
                // Sample-Varianz (N-1) für korrekte Schätzung
                var variance = returns.Select(r => (r - avgReturn) * (r - avgReturn)).Sum() / (returns.Count - 1);
                var stdDev = Math.Sqrt(variance);

                // Annualisierung: Trades/Jahr statt fixem sqrt(252)
                var firstEntry = trades.First().EntryTime;
                var lastExit = trades.Last().ExitTime;
                var years = (lastExit - firstEntry).TotalDays / 365.25;
                var annualizationFactor = years > 0 ? Math.Sqrt(trades.Count / years) : Math.Sqrt(252);

                report.SharpeRatio = stdDev > 0
                    ? (decimal)(avgReturn / stdDev * annualizationFactor)
                    : 0m;

                // Sortino Ratio: Downside-Deviation über ALLE Returns (positive als 0 behandeln)
                var downsideVariance = returns.Select(r => r < 0 ? r * r : 0.0).Sum() / (returns.Count - 1);
                var downsideDeviation = Math.Sqrt(downsideVariance);
                report.SortinoRatio = downsideDeviation > 0
                    ? (decimal)(avgReturn / downsideDeviation * annualizationFactor)
                    : 0m;
            }
        }

        // Monte Carlo Simulation (1000 Durchläufe)
        if (trades.Count >= 5)
        {
            report.MonteCarlo = MonteCarloSimulator.Simulate(trades, initialBalance);
        }

        // CPCV: Combinatorial Purged Cross-Validation (min. 30 Trades)
        if (trades.Count >= 30)
        {
            report.Cpcv = CpcvValidator.Validate(trades, initialBalance);
        }

        // Regime-spezifische Metriken (falls Regime-Daten vorhanden)
        var regimeGroups = trades.Where(t => t.Regime.HasValue).GroupBy(t => t.Regime!.Value);
        foreach (var group in regimeGroups)
        {
            var regimeTrades = group.ToList();
            var wins = regimeTrades.Count(t => t.Pnl > 0);
            var totalPnl = regimeTrades.Sum(t => t.Pnl);
            var grossWins = regimeTrades.Where(t => t.Pnl > 0).Sum(t => t.Pnl);
            var grossLosses = Math.Abs(regimeTrades.Where(t => t.Pnl < 0).Sum(t => t.Pnl));

            report.RegimeBreakdown[group.Key] = new RegimeMetrics
            {
                TradeCount = regimeTrades.Count,
                WinRate = regimeTrades.Count > 0 ? (decimal)wins / regimeTrades.Count : 0m,
                TotalPnl = totalPnl,
                ProfitFactor = grossLosses > 0 ? grossWins / grossLosses : grossWins > 0 ? 99m : 0m,
                AverageRrr = regimeTrades.Count > 0 ? totalPnl / regimeTrades.Count : 0m
            };
        }

        return report;
    }
}

/// <summary>Performance-Metriken aufgeschlüsselt nach MarketRegime.</summary>
public class RegimeMetrics
{
    public int TradeCount { get; init; }
    public decimal WinRate { get; init; }
    public decimal TotalPnl { get; init; }
    public decimal ProfitFactor { get; init; }
    public decimal AverageRrr { get; init; }
}
