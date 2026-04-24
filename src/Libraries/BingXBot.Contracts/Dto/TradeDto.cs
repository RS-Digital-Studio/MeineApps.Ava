using BingXBot.Core.Enums;

namespace BingXBot.Contracts.Dto;

/// <summary>
/// Trade-Historie-Eintrag — serialisiert fuer Client-Listen.
/// </summary>
public record TradeDto(
    long Id,
    string Symbol,
    Side Side,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal Quantity,
    decimal Pnl,
    decimal PnlPercent,
    decimal Fee,
    DateTime EntryTimeUtc,
    DateTime ExitTimeUtc,
    string Reason,
    TradingMode Mode,
    string? StrategyName,
    /// <summary>Multi-TF Standalone: Navigator-TF des auslösenden Signals (für TF-Badge).</summary>
    TimeFrame NavigatorTimeframe = TimeFrame.H4);

/// <summary>Query-Parameter fuer Trade-Historie (Server mappt auf SQL).</summary>
public record TradeQueryDto(
    int Page = 0,
    int PageSize = 100,
    TradingMode? Mode = null,
    string? Symbol = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);

/// <summary>Scanner-Result pro Symbol (Letzter Scan, nach Score sortiert).</summary>
public record ScannerSymbolDto(
    string Symbol,
    decimal Price,
    decimal Volume24h,
    decimal PriceChangePercent,
    int Score,
    string? SuggestedSide,
    string? Reason);

public record ScannerResultDto(
    TimeFrame NavigatorTimeframe,
    DateTime TimestampUtc,
    IReadOnlyList<ScannerSymbolDto> Symbols);

/// <summary>Log-Entry wie in LogView angezeigt.</summary>
public record LogEntryDto(
    DateTime TimestampUtc,
    LogLevel Level,
    string Category,
    string Message,
    string? Symbol);

/// <summary>Activity-Feed-Eintrag: Trade/System/Warning (Pre-formatiert fuer schnelle UI).</summary>
public record ActivityFeedDto(
    DateTime TimestampUtc,
    string Category,
    string Message,
    LogLevel Level,
    string? Symbol);

/// <summary>Ticker-Update per SignalR (gedrosselt auf 1/s/Symbol).</summary>
public record TickerUpdateDto(
    string Symbol,
    decimal Price,
    DateTime TimestampUtc);

/// <summary>Position-Update per SignalR (MarkPrice + UnrealizedPnl-Aenderungen).</summary>
public record PositionUpdateDto(
    string Symbol,
    Side Side,
    decimal MarkPrice,
    decimal UnrealizedPnl,
    decimal UnrealizedPnlPercent,
    DateTime TimestampUtc);

/// <summary>Margin-Warnung fuer Position nahe Liquidation.</summary>
public record MarginWarningDto(
    string Symbol,
    decimal CurrentPrice,
    decimal LiquidationPrice,
    decimal DistancePercent,
    DateTime TimestampUtc);

/// <summary>Bot-State-Change-Event: State-Transition mit Grund.</summary>
public record BotStateChangedDto(
    BotState State,
    TradingMode Mode,
    string? Reason,
    DateTime TimestampUtc);
