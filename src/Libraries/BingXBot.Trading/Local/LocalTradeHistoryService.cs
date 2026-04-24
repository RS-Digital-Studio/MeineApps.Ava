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
