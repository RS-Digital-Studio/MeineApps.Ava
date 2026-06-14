using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Enums;

namespace BingXBot.Trading.Local;

/// <summary>
/// Trade-Historie + Scanner-Resultate aus lokalen Quellen (DB + In-Memory-Cache).
/// </summary>
public sealed class LocalTradeHistoryService : ITradeHistoryService
{
    private readonly BotDatabaseService? _db;
    private readonly ScannerResultsCache _scannerCache;

    public LocalTradeHistoryService(ScannerResultsCache scannerCache, BotDatabaseService? db = null)
    {
        _db = db;
        _scannerCache = scannerCache;
    }

    public async Task<PagedResult<TradeDto>> QueryAsync(TradeQueryDto query, CancellationToken ct = default)
    {
        if (_db == null)
            return new PagedResult<TradeDto>(Array.Empty<TradeDto>(), 0, query.Page, query.PageSize);

        var all = await _db.GetTradesAsync(modeFilter: query.Mode, limit: 5000).ConfigureAwait(false);

        IEnumerable<Core.Models.CompletedTrade> filtered = all;
        if (!string.IsNullOrWhiteSpace(query.Symbol))
            filtered = filtered.Where(t => string.Equals(t.Symbol, query.Symbol, StringComparison.OrdinalIgnoreCase));
        if (query.FromUtc.HasValue)
            filtered = filtered.Where(t => t.ExitTime >= query.FromUtc.Value);
        if (query.ToUtc.HasValue)
            filtered = filtered.Where(t => t.ExitTime <= query.ToUtc.Value);

        var list = filtered.ToList();
        var totalCount = list.Count;

        var pageSize = Math.Max(1, Math.Min(query.PageSize, 500));
        var page = Math.Max(0, query.Page);
        var items = list
            .OrderByDescending(t => t.ExitTime)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(t => t.ToDto())
            .ToList();

        return new PagedResult<TradeDto>(items, totalCount, page, pageSize);
    }

    public async Task<TradeSummaryDto> GetSummaryAsync(TradingMode? mode, CancellationToken ct = default)
    {
        if (_db == null)
            return new TradeSummaryDto(0, 0, 0, 0m, 0m, 0m, 0m, 0m, 0m, mode);

        var all = await _db.GetTradesAsync(modeFilter: mode, limit: int.MaxValue).ConfigureAwait(false);
        var list = all.ToList();
        if (list.Count == 0)
            return new TradeSummaryDto(0, 0, 0, 0m, 0m, 0m, 0m, 0m, 0m, mode);

        var wins = list.Count(t => t.Pnl > 0);
        var losses = list.Count(t => t.Pnl < 0);
        var total = list.Sum(t => t.Pnl);
        var fees = list.Sum(t => t.Fee);
        var best = list.Max(t => t.Pnl);
        var worst = list.Min(t => t.Pnl);
        var avg = total / list.Count;
        var wr = (wins + losses) == 0 ? 0m : (decimal)wins / (wins + losses);

        return new TradeSummaryDto(
            TotalTrades: list.Count,
            WinCount: wins,
            LossCount: losses,
            WinRate: wr,
            TotalPnl: total,
            AveragePnl: avg,
            BestPnl: best,
            WorstPnl: worst,
            TotalFees: fees,
            Mode: mode);
    }

    public Task<IReadOnlyList<ScannerResultDto>> GetScannerResultsAsync(CancellationToken ct = default) =>
        Task.FromResult(_scannerCache.GetAll());
}

/// <summary>
/// Thread-sicherer Cache für die letzten Scan-Ergebnisse pro Navigator-TF.
/// Multi-TF Standalone: Key ist jetzt <see cref="TimeFrame"/> (nicht mehr Preset).
/// </summary>
public sealed class ScannerResultsCache
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<TimeFrame, ScannerResultDto> _results = new();

    public void Update(ScannerResultDto result)
    {
        _results[result.NavigatorTimeframe] = result;
    }

    public IReadOnlyList<ScannerResultDto> GetAll()
    {
        return _results.Values.OrderBy(r => r.NavigatorTimeframe).ToList();
    }
}
