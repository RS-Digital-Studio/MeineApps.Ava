using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Contracts.Dto;

/// <summary>
/// Server-Status-Snapshot. Wird per GET /status abgefragt und per SignalR-Event BotStateChanged gepusht.
/// </summary>
public record BotStatusDto(
    BotState State,
    TradingMode Mode,
    bool IsHedgeMode,
    IReadOnlyList<TimeFrame> ActiveTimeframes,
    long UptimeSeconds,
    bool HasCredentials,
    bool IsConnected,
    string? LastError = null);

/// <summary>
/// Account-Snapshot: Balance + offene Positionen + PnL-Aggregate.
/// </summary>
public record AccountSnapshotDto(
    decimal Balance,
    decimal Available,
    decimal UnrealizedPnl,
    decimal RealizedPnlToday,
    decimal TotalPnlAllTime,
    int OpenPositionCount,
    IReadOnlyList<PositionDto> Positions,
    DateTime AsOfUtc);

/// <summary>
/// Position-DTO mit Zusatzinfos, die im Remote-Modus fuer UI gebraucht werden (Signal-Metadaten).
/// </summary>
public record PositionDto(
    string Symbol,
    Side Side,
    decimal EntryPrice,
    decimal MarkPrice,
    decimal Quantity,
    decimal UnrealizedPnl,
    decimal UnrealizedPnlPercent,
    decimal Leverage,
    MarginType MarginType,
    decimal? StopLoss,
    decimal? TakeProfit,
    decimal? LiquidationPrice,
    bool IsSmartBreakevenArmed,
    string? StrategyName,
    DateTime OpenTimeUtc);

/// <summary>
/// Offene Order auf BingX (SL/TP, Limit-Pending, etc.).
/// </summary>
public record OpenOrderDto(
    string Symbol,
    string OrderId,
    Side Side,
    OrderType Type,
    decimal Quantity,
    decimal Price,
    decimal? StopPrice,
    OrderStatus Status,
    DateTime CreatedUtc,
    string? RejectionReason = null);

/// <summary>Einzelner Punkt der Equity-Kurve (zum Anzeigen im Chart).</summary>
public record EquityPointDto(
    DateTime TimestampUtc,
    decimal Equity);

/// <summary>Start-Request fuer Bot (Mode + aktive Timeframes + Paper-Startkapital).</summary>
public record BotStartRequest(
    TradingMode Mode,
    decimal? InitialBalance = null,
    IReadOnlyList<TimeFrame>? ActiveTimeframes = null);

/// <summary>Paginierter Result-Wrapper.</summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// Server-Health-Event: feuert, wenn sich der Live-Exchange-Verbindungsstatus aendert.
/// <c>IsDegraded=true</c> heisst "Server laeuft weiter, aber BingX-Verbindung ist aktuell hin" —
/// Client soll einen Hinweis anzeigen (Banner, Toast, Icon-Change). <c>IsDegraded=false</c>
/// signalisiert die Wiederherstellung. <c>Reason</c> ist optionaler Kontext fuer Logs.
/// </summary>
public record ConnectionDegradedDto(
    bool IsDegraded,
    string? Reason,
    DateTime TimestampUtc);
