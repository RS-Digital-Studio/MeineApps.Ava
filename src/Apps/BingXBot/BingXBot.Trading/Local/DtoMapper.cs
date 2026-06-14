using BingXBot.Contracts.Dto;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Trading.Local;

/// <summary>
/// Zentrale Konvertierungen zwischen Domain-Modellen (BingXBot.Core) und Contracts-DTOs.
/// Wird sowohl im Server (Local-Impls) als auch im Client (Remote-Antworten) verwendet.
/// </summary>
public static class DtoMapper
{
    public static TradeDto ToDto(this CompletedTrade t, long id = 0, string? strategyName = null)
    {
        var pnlPercent = t.EntryPrice > 0 && t.Quantity > 0
            ? (t.ExitPrice - t.EntryPrice) / t.EntryPrice * 100m * (t.Side == Side.Buy ? 1 : -1)
            : 0m;

        return new TradeDto(
            Id: id,
            Symbol: t.Symbol,
            Side: t.Side,
            EntryPrice: t.EntryPrice,
            ExitPrice: t.ExitPrice,
            Quantity: t.Quantity,
            Pnl: t.Pnl,
            PnlPercent: pnlPercent,
            Fee: t.Fee,
            EntryTimeUtc: t.EntryTime,
            ExitTimeUtc: t.ExitTime,
            Reason: t.Reason,
            Mode: t.Mode,
            StrategyName: strategyName,
            NavigatorTimeframe: t.NavigatorTimeframe);
    }

    public static PositionDto ToDto(
        this Position p,
        decimal? stopLoss = null,
        decimal? takeProfit = null,
        decimal? liquidationPrice = null,
        bool smartBreakevenArmed = false,
        string? strategyName = null)
    {
        var notional = p.EntryPrice * p.Quantity;
        var pnlPercent = notional > 0 ? p.UnrealizedPnl / notional * 100m : 0m;

        return new PositionDto(
            Symbol: p.Symbol,
            Side: p.Side,
            EntryPrice: p.EntryPrice,
            MarkPrice: p.MarkPrice,
            Quantity: p.Quantity,
            UnrealizedPnl: p.UnrealizedPnl,
            UnrealizedPnlPercent: pnlPercent,
            Leverage: p.Leverage,
            MarginType: p.MarginType,
            StopLoss: stopLoss,
            TakeProfit: takeProfit,
            LiquidationPrice: liquidationPrice,
            IsSmartBreakevenArmed: smartBreakevenArmed,
            StrategyName: strategyName,
            OpenTimeUtc: p.OpenTime);
    }

    public static LogEntryDto ToDto(this LogEntry e) =>
        new(e.Timestamp, e.Level, e.Category, e.Message, e.Symbol);

    public static EquityPointDto ToDto(this EquityPoint p) =>
        new(p.Time, p.Equity);

    public static OpenOrderDto ToDto(this Order o) =>
        new(
            Symbol: o.Symbol,
            OrderId: o.OrderId,
            Side: o.Side,
            Type: o.Type,
            Quantity: o.Quantity,
            Price: o.Price,
            StopPrice: o.StopPrice,
            Status: o.Status,
            CreatedUtc: o.CreateTime,
            RejectionReason: o.RejectionReason);
}
