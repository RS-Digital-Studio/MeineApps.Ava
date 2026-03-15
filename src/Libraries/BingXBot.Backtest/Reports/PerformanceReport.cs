using BingXBot.Core.Models;

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
                var variance = returns.Select(r => (r - avgReturn) * (r - avgReturn)).Average();
                var stdDev = Math.Sqrt(variance);
                report.SharpeRatio = stdDev > 0
                    ? (decimal)(avgReturn / stdDev * Math.Sqrt(252))
                    : 0m;
            }
        }

        return report;
    }
}
