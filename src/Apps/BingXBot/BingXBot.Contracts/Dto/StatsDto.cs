namespace BingXBot.Contracts.Dto;

/// <summary>
/// v1.5.3 Phase 5 — Trade-Stats-Aggregat-Zeile fuer GET /stats/breakdown.
/// </summary>
public sealed record TradeStatsBreakdownRowDto(
    int Tf,                       // (int)TimeFrame
    string Category,              // MarketCategory.ToString()
    string Mode,                  // TradingMode.ToString()
    int TotalTrades,
    int WinTrades,
    decimal WinRate,
    decimal TotalPnl,
    decimal AvgPnl,
    decimal TotalFees,
    double AvgHoldingTimeMinutes,
    decimal MaxDrawdown);

/// <summary>v1.5.3 Phase 5 — Antwort-DTO fuer GET /stats/breakdown.</summary>
public sealed record StatsBreakdownDto(
    IReadOnlyList<TradeStatsBreakdownRowDto> Rows);
