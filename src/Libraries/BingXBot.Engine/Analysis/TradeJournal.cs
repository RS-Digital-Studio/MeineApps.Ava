using BingXBot.Core.Models;

namespace BingXBot.Engine.Analysis;

public class TradeJournal
{
    private readonly List<CompletedTrade> _trades = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public event EventHandler<CompletedTrade>? TradeRecorded;

    public async Task RecordTradeAsync(CompletedTrade trade)
    {
        await _semaphore.WaitAsync();
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

    /// <summary>Gibt eine Kopie aller Trades zurueck (thread-safe).</summary>
    public IReadOnlyList<CompletedTrade> GetTrades()
    {
        _semaphore.Wait();
        try { return _trades.ToList().AsReadOnly(); }
        finally { _semaphore.Release(); }
    }

    public decimal WinRate
    {
        get
        {
            _semaphore.Wait();
            try { return _trades.Count == 0 ? 0 : (decimal)_trades.Count(t => t.Pnl > 0) / _trades.Count * 100m; }
            finally { _semaphore.Release(); }
        }
    }

    public decimal TotalPnl
    {
        get
        {
            _semaphore.Wait();
            try { return _trades.Sum(t => t.Pnl); }
            finally { _semaphore.Release(); }
        }
    }

    public decimal ProfitFactor
    {
        get
        {
            _semaphore.Wait();
            try
            {
                var grossProfit = _trades.Where(t => t.Pnl > 0).Sum(t => t.Pnl);
                var grossLoss = Math.Abs(_trades.Where(t => t.Pnl < 0).Sum(t => t.Pnl));
                return grossLoss == 0 ? grossProfit > 0 ? decimal.MaxValue : 0m : grossProfit / grossLoss;
            }
            finally { _semaphore.Release(); }
        }
    }

    public decimal AverageRrr
    {
        get
        {
            _semaphore.Wait();
            try
            {
                var avgWin = _trades.Where(t => t.Pnl > 0).Select(t => t.Pnl).DefaultIfEmpty(0).Average();
                var avgLoss = Math.Abs(_trades.Where(t => t.Pnl < 0).Select(t => t.Pnl).DefaultIfEmpty(0).Average());
                return avgLoss == 0 ? 0m : avgWin / avgLoss;
            }
            finally { _semaphore.Release(); }
        }
    }

    public void Clear()
    {
        _semaphore.Wait();
        try { _trades.Clear(); }
        finally { _semaphore.Release(); }
    }
}
