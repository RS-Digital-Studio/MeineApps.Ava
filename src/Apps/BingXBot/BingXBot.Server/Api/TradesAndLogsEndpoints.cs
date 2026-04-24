using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Enums;
using BingXBot.Server.Services;
using BotLogLevel = BingXBot.Core.Enums.LogLevel;

namespace BingXBot.Server.Api;

public static class TradesAndLogsEndpoints
{
    public static void MapTradesAndLogsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Trades, async (
            ITradeHistoryService history,
            int? page, int? pageSize, string? mode, string? symbol,
            DateTime? from, DateTime? to, CancellationToken ct) =>
        {
            TradingMode? modeFilter = null;
            if (!string.IsNullOrWhiteSpace(mode) && Enum.TryParse<TradingMode>(mode, true, out var m))
                modeFilter = m;

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

        // Logs-Query aus Server-seitigem Ringpuffer (default 1000 Eintraege, Capacity via
        // Server:LogBufferCapacity). Client nutzt das nach Reconnect oder App-Neustart, um
        // die Log-Historie wiederherzustellen — ohne wuerde LogView leer bleiben bis neue
        // Events kommen.
        app.MapGet(ApiRoutes.Logs, (LogBufferService logs, int? page, int? pageSize, string? minLevel) =>
        {
            BotLogLevel? level = null;
            if (!string.IsNullOrWhiteSpace(minLevel) && Enum.TryParse<BotLogLevel>(minLevel, true, out var lvl))
                level = lvl;
            var result = logs.Query(page ?? 0, pageSize ?? 200, level);
            return Results.Ok(result);
        });
    }
}
