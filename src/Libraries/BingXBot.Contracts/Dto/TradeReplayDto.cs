namespace BingXBot.Contracts.Dto;

/// <summary>v1.6.4 Phase 13 — Trade-Replay-Result-DTO fuer REST.</summary>
public sealed record TradeReplayResultDto(
    long LiveTradeId,
    string Symbol,
    DateTime EntryTimeUtc,
    decimal? EntryPriceDriftPercent,
    decimal? PnlDriftPercent,
    bool ExitReasonSame,
    string Verdict,
    string? ErrorDetail);
