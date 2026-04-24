using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;

namespace BingXBot.Contracts.Dto;

/// <summary>
/// Backtest-Request: Was soll backgetestet werden.
/// RiskOverride + ScannerOverride erlauben Ad-hoc-Abweichungen ohne globale Settings zu aendern.
/// </summary>
public record BacktestRequestDto(
    string Symbol,
    TimeFrame TimeFrame,
    DateTime StartUtc,
    DateTime EndUtc,
    string StrategyName,
    decimal InitialBalance,
    RiskSettings? RiskOverride = null,
    ScannerSettings? ScannerOverride = null);

public enum BacktestJobState
{
    Queued,
    Running,
    Completed,
    Cancelled,
    Failed
}

public record BacktestJobDto(
    string JobId,
    BacktestJobState State,
    DateTime QueuedAtUtc);

public record BacktestStatusDto(
    string JobId,
    BacktestJobState State,
    float Progress,
    int CurrentBar,
    int TotalBars,
    int EstimatedSecondsRemaining,
    string? Error);

public record BacktestTradeDto(
    string Symbol,
    Side Side,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal Quantity,
    decimal Pnl,
    decimal PnlPercent,
    DateTime EntryTimeUtc,
    DateTime ExitTimeUtc,
    string Reason);

/// <summary>
/// Vollstaendiger Backtest-Report. Metriken + Trade-Liste + Equity-Kurve.
/// </summary>
public record BacktestResultDto(
    string JobId,
    BacktestRequestDto Request,
    decimal FinalBalance,
    decimal TotalPnl,
    decimal TotalPnlPercent,
    decimal MaxDrawdown,
    decimal MaxDrawdownPercent,
    decimal SharpeRatio,
    decimal SortinoRatio,
    decimal CalmarRatio,
    decimal ProfitFactor,
    decimal WinRate,
    int TotalTrades,
    int WinningTrades,
    int LosingTrades,
    int MaxConsecutiveWins,
    int MaxConsecutiveLosses,
    IReadOnlyList<BacktestTradeDto> Trades,
    IReadOnlyList<EquityPointDto> EquityCurve,
    DateTime CompletedUtc);

public record BacktestProgressDto(
    string JobId,
    float Progress,
    int CurrentBar,
    int TotalBars);
