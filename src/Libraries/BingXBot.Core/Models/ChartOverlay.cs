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
/// SK-System Sequenz-Overlay: Fibonacci-Level, A-B-C Punkte und Korrekturzonen im Chart.
/// </summary>
public record SequenceOverlay(
    // SK-VERIFY: Abweichung #4 — SK-Nomenklatur: 0-A-B statt A-B-C
    decimal Point0, decimal PointA, decimal? PointB,
    // Fibonacci-Retracement Level
    decimal Ret382, decimal Ret500, decimal Ret559,
    decimal Ret618, decimal Ret667, decimal Ret786,
    // Fibonacci-Extension Level
    decimal Ext100, decimal Ext1272, decimal Ext1618, decimal Ext200,
    // Sequenz-Metadaten
    bool IsLong,
    string CharacterPattern,
    string SequenceType);
