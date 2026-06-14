using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

/// <summary>
/// Order-Repraesentation. <see cref="ReduceOnly"/> ist seit v1.4.0 Phase 0.1 (Finding 0.1)
/// Pflicht-Feld: Bot-platzierte TP-Limits (LIMIT mit reduceOnly=true) muessen vom
/// Cancel-Filter in <c>LiveTradingService.SlTpManager.CancelNativeSlTpOrdersAsync</c>
/// erkannt werden. BingX liefert das Flag im OpenOrders-Response zurueck.
/// </summary>
public record Order(
    string OrderId,
    string Symbol,
    Side Side,
    OrderType Type,
    decimal Price,
    decimal Quantity,
    decimal? StopPrice,
    DateTime CreateTime,
    OrderStatus Status,
    string? RejectionReason = null,
    bool ReduceOnly = false);
