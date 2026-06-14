using BingXBot.Contracts.Dto;

namespace BingXBot.Contracts.Services;

/// <summary>
/// v1.5.3 Phase 5 — Trade-Stats-Breakdown abfragen.
/// Server: direkter Zugriff auf TradeStatsAggregator-Snapshot.
/// Client: HTTP GET /api/v1/stats/breakdown.
/// </summary>
public interface IStatsService
{
    Task<StatsBreakdownDto> GetBreakdownAsync(CancellationToken ct = default);
}
