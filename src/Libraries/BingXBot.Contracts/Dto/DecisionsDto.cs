namespace BingXBot.Contracts.Dto;

/// <summary>
/// v1.5.2 Phase 4 — Decision-Trail-Eintrag (REST + SignalR).
/// Spiegel von <c>BingXBot.Core.Diagnostics.EvaluationDecision</c> als wire-stabiles DTO.
/// </summary>
public sealed record EvaluationDecisionDto(
    DateTime UtcTimestamp,
    string Symbol,
    /// <summary>Numerischer TF-Code aus <c>BingXBot.Core.Enums.TimeFrame</c>.</summary>
    int Tf,
    string SequenceState,
    decimal? Point0,
    decimal? PointA,
    decimal? PointB,
    bool Triggered,
    string? RejectionReason,
    int ConfluenceScore,
    IReadOnlyList<string> ConfluenceCategories,
    IReadOnlyList<string> HardFiltersFailed);

/// <summary>v1.5.2 Phase 4 — Antwort-DTO fuer GET /decisions.</summary>
public sealed record DecisionsQueryResultDto(
    IReadOnlyList<EvaluationDecisionDto> Items,
    int TotalCount);
