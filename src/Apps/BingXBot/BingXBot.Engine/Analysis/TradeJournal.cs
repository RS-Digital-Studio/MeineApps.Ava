using BingXBot.Core.Models;

namespace BingXBot.Engine.Analysis;

/// <summary>Alle Metriken in einem Durchlauf berechnet (konsistent, ein Lock).</summary>
public record TradeStatistics(
    int TotalCount,
    int WinCount,
    int LossCount,
    int CancelledCount,
    decimal WinRate,
    decimal TotalPnl,
    decimal ProfitFactor,
    decimal AverageRrr);

public class TradeJournal
{
    private readonly List<CompletedTrade> _trades = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>Gründe die einen Trade als abgebrochen markieren (Margin-Fehler, Ablehnung etc.).</summary>
    private static readonly string[] CancelledReasons = ["abgebrochen", "rejected", "Margin", "cancelled", "Notfall"];

    public event EventHandler<CompletedTrade>? TradeRecorded;

    public async Task RecordTradeAsync(CompletedTrade trade)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _trades.Add(trade);
        }
        finally
        {
            _semaphore.Release();
        }
        TradeRecorded?.Invoke(this, trade);
    }

    /// <summary>Gibt eine Kopie aller Trades zurück (thread-safe).</summary>
    public IReadOnlyList<CompletedTrade> GetTrades()
    {
        _semaphore.Wait();
        try { return _trades.ToList().AsReadOnly(); }
        finally { _semaphore.Release(); }
    }

    /// <summary>
    /// Berechnet alle Metriken in einem einzigen Lock-Zyklus.
    /// Konsistent: Alle Werte basieren auf demselben Snapshot der Trade-Liste.
    /// </summary>
    public TradeStatistics GetStatistics()
    {
        _semaphore.Wait();
        try
        {
            var cancelled = _trades.Count(IsCancelled);
            var valid = _trades.Where(t => !IsCancelled(t)).ToList();
            var wins = valid.Count(t => t.Pnl > 0);
            var losses = valid.Count(t => t.Pnl <= 0);
            var winRate = valid.Count == 0 ? 0m : (decimal)wins / valid.Count * 100m;
            var totalPnl = _trades.Sum(t => t.Pnl);
            var grossProfit = _trades.Where(t => t.Pnl > 0).Sum(t => t.Pnl);
            var grossLoss = Math.Abs(_trades.Where(t => t.Pnl < 0).Sum(t => t.Pnl));
            var profitFactor = grossLoss == 0 ? grossProfit > 0 ? decimal.MaxValue : 0m : grossProfit / grossLoss;
            var avgWin = _trades.Where(t => t.Pnl > 0).Select(t => t.Pnl).DefaultIfEmpty(0).Average();
            var avgLoss = Math.Abs(_trades.Where(t => t.Pnl < 0).Select(t => t.Pnl).DefaultIfEmpty(0).Average());
            var avgRrr = avgLoss == 0 ? 0m : avgWin / avgLoss;

            return new TradeStatistics(
                _trades.Count, wins, losses, cancelled,
                winRate, totalPnl, profitFactor, avgRrr);
        }
        finally { _semaphore.Release(); }
    }

    // Einzelne Properties bleiben für Abwärtskompatibilität, delegieren an GetStatistics-Logik
    public int CancelledCount { get { _semaphore.Wait(); try { return _trades.Count(IsCancelled); } finally { _semaphore.Release(); } } }
    public decimal WinRate { get { _semaphore.Wait(); try { var v = _trades.Where(t => !IsCancelled(t)).ToList(); return v.Count == 0 ? 0 : (decimal)v.Count(t => t.Pnl > 0) / v.Count * 100m; } finally { _semaphore.Release(); } } }
    public decimal TotalPnl { get { _semaphore.Wait(); try { return _trades.Sum(t => t.Pnl); } finally { _semaphore.Release(); } } }
    public decimal ProfitFactor { get { _semaphore.Wait(); try { var gp = _trades.Where(t => t.Pnl > 0).Sum(t => t.Pnl); var gl = Math.Abs(_trades.Where(t => t.Pnl < 0).Sum(t => t.Pnl)); return gl == 0 ? gp > 0 ? decimal.MaxValue : 0m : gp / gl; } finally { _semaphore.Release(); } } }
    public decimal AverageRrr { get { _semaphore.Wait(); try { var aw = _trades.Where(t => t.Pnl > 0).Select(t => t.Pnl).DefaultIfEmpty(0).Average(); var al = Math.Abs(_trades.Where(t => t.Pnl < 0).Select(t => t.Pnl).DefaultIfEmpty(0).Average()); return al == 0 ? 0m : aw / al; } finally { _semaphore.Release(); } } }

    /// <summary>Prüft ob ein Trade als abgebrochen gilt (anhand des Reason-Felds).</summary>
    private static bool IsCancelled(CompletedTrade t) =>
        CancelledReasons.Any(r => t.Reason.Contains(r, StringComparison.OrdinalIgnoreCase));

    public void Clear()
    {
        _semaphore.Wait();
        try { _trades.Clear(); }
        finally { _semaphore.Release(); }
    }
}
