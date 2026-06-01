using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Enums;
using BingXBot.Server.Services;
using BingXBot.Trading;
using BotLogLevel = BingXBot.Core.Enums.LogLevel;

namespace BingXBot.Server.Api;

public static class TradesAndLogsEndpoints
{
    public static void MapTradesAndLogsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Trades, async (
            ITradeHistoryService history,
            BingXBot.Trading.BotDatabaseService db,
            BingXBot.Core.Interfaces.IAppPaths paths,
            int? page, int? pageSize, string? mode, string? symbol,
            DateTime? from, DateTime? to, bool? archive, CancellationToken ct) =>
        {
            TradingMode? modeFilter = null;
            if (!string.IsNullOrWhiteSpace(mode) && Enum.TryParse<TradingMode>(mode, true, out var m))
                modeFilter = m;

            // v1.6.1 Phase 11 — bei ?archive=true zusaetzlich archivierte Trades einblenden.
            if (archive == true)
            {
                var archiveDir = Path.Combine(Path.GetDirectoryName(paths.DatabasePath) ?? ".", "archives");
                var combined = await db.GetTradesIncludingArchiveAsync(archiveDir, modeFilter, limit: 5000)
                    .ConfigureAwait(false);
                IEnumerable<BingXBot.Core.Models.CompletedTrade> q = combined;
                if (!string.IsNullOrWhiteSpace(symbol)) q = q.Where(t => t.Symbol == symbol);
                if (from.HasValue) q = q.Where(t => t.EntryTime >= from.Value);
                if (to.HasValue) q = q.Where(t => t.ExitTime <= to.Value);
                var pg = page ?? 0; var ps = pageSize ?? 100;
                var paged = q.Skip(pg * ps).Take(ps).ToList();
                return Results.Ok(new { Items = paged, Page = pg, PageSize = ps, Total = q.Count() });
            }

            var query = new TradeQueryDto(
                Page: page ?? 0,
                PageSize: pageSize ?? 100,
                Mode: modeFilter,
                Symbol: symbol,
                FromUtc: from,
                ToUtc: to);
            var result = await history.QueryAsync(query, ct);
            return Results.Ok(result);
        });

        app.MapGet(ApiRoutes.ScannerResults, async (ITradeHistoryService history, CancellationToken ct) =>
        {
            var results = await history.GetScannerResultsAsync(ct);
            return Results.Ok(results);
        });

        // Aggregate-Summary — spart Client Bandbreite bei 1000+ Trades
        app.MapGet(ApiRoutes.TradesSummary, async (
            ITradeHistoryService history, string? mode, CancellationToken ct) =>
        {
            TradingMode? modeFilter = null;
            if (!string.IsNullOrWhiteSpace(mode) && Enum.TryParse<TradingMode>(mode, true, out var m))
                modeFilter = m;
            var summary = await history.GetSummaryAsync(modeFilter, ct);
            return Results.Ok(summary);
        });

        // Logs-Query aus Server-seitigem Ringpuffer (default 1000 Eintraege, Capacity via
        // Server:LogBufferCapacity). Client nutzt das nach Reconnect oder App-Neustart, um
        // die Log-Historie wiederherzustellen — ohne wuerde LogView leer bleiben bis neue
        // Events kommen.
        // DB-FALLBACK: Nach einem Server-Restart ist der RAM-Ringpuffer leer, obwohl die
        // LogEntries-Tabelle die Historie persistiert haelt → frueher lieferte /logs faelschlich
        // {items:[],totalCount:0} trotz voller DB. Bei leerem Puffer aus der DB nachladen.
        app.MapGet(ApiRoutes.Logs, async (LogBufferService logs, BotDatabaseService db,
            int? page, int? pageSize, string? minLevel, CancellationToken ct) =>
        {
            BotLogLevel? level = null;
            if (!string.IsNullOrWhiteSpace(minLevel) && Enum.TryParse<BotLogLevel>(minLevel, true, out var lvl))
                level = lvl;

            var size = pageSize ?? 200;
            var result = logs.Query(page ?? 0, size, level);
            if (result.TotalCount > 0)
                return Results.Ok(result);

            var dbLogs = await db.GetLogsAsync(size, level).ConfigureAwait(false);
            var dtos = dbLogs
                .Select(e => new LogEntryDto(e.Timestamp, e.Level, e.Category, e.Message, e.Symbol))
                .ToList();
            return Results.Ok(new PagedResult<LogEntryDto>(dtos, dtos.Count, 0, size));
        });
    }
}
