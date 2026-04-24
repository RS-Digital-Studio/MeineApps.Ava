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

/// <summary>
/// SK-System Sequenz-Overlay: Fibonacci-Level + 0-A-B-Punkte im Chart.
/// Buch-konforme Level: 50/55.9/61.8/66.7/71/78.6 Retracement, 161.8/200/261.8/423.6 Extension.
/// </summary>
public record SequenceOverlay(
    decimal Point0, decimal PointA, decimal? PointB,
    // Fibonacci-Retracement Level (Buch-Tabelle)
    decimal Ret500, decimal Ret559, decimal Ret618,
    decimal Ret667, decimal Ret71, decimal Ret786,
    // Fibonacci-Extension Level (Buch-Tabelle)
    decimal Ext1618, decimal Ext200,
    decimal Ext2618, decimal Ext4236,
    bool IsLong);
