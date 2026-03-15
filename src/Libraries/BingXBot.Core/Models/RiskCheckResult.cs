namespace BingXBot.Core.Models;

public record RiskCheckResult(
    bool IsAllowed,
    string? RejectionReason,
    decimal AdjustedPositionSize);
