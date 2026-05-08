using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Trading.Stats;

namespace BingXBot.Server.Api;

/// <summary>
/// v1.5.3 Phase 5 — Trade-Stats-Breakdown REST-Endpoint.
/// </summary>
public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.StatsBreakdown, (TradeStatsAggregator aggregator) =>
        {
            var snapshot = aggregator.GetSnapshot();
            var rows = snapshot.Select(s => new TradeStatsBreakdownRowDto(
                Tf: (int)s.NavigatorTimeframe,
                Category: s.Category.ToString(),
                Mode: s.Mode.ToString(),
                TotalTrades: s.TotalTrades,
                WinTrades: s.WinTrades,
                WinRate: s.WinRate,
                TotalPnl: s.TotalPnl,
                AvgPnl: s.AvgPnl,
                TotalFees: s.TotalFees,
                AvgHoldingTimeMinutes: s.AvgHoldingTimeMinutes,
                MaxDrawdown: s.MaxDrawdown)).ToList();
            return Results.Ok(new StatsBreakdownDto(rows));
        });
    }
}
