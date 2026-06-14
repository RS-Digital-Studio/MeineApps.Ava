using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

/// <summary>
/// Marker für einen abgeschlossenen oder eröffneten Trade im Chart.
/// </summary>
public record TradeMarker(
    DateTime Time,
    decimal Price,
    Side Side,
    bool IsEntry,
    decimal? Pnl = null);

/// <summary>
/// Overlay für eine offene Position: Entry-Linie, SL- und TP-Linien.
/// </summary>
public record ActivePositionOverlay(
    decimal EntryPrice,
    decimal? StopLoss,
    decimal? TakeProfit,
    decimal? TakeProfit2,
    Side Side);
