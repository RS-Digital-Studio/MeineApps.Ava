using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Trading.Stats;

namespace BingXBot.Trading.Local;

/// <summary>
/// v1.5.3 Phase 5 — LocalStatsService: Direkter Zugriff auf
/// <see cref="TradeStatsAggregator"/>-Snapshot ohne HTTP.
/// </summary>
public sealed class LocalStatsService : IStatsService
{
    private readonly TradeStatsAggregator _aggregator;

    public LocalStatsService(TradeStatsAggregator aggregator)
    {
        _aggregator = aggregator;
    }

    public Task<StatsBreakdownDto> GetBreakdownAsync(CancellationToken ct = default)
    {
        var snapshot = _aggregator.GetSnapshot();
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
        return Task.FromResult(new StatsBreakdownDto(rows));
    }
}
