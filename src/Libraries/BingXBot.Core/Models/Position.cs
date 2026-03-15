using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

public record Position(
    string Symbol,
    Side Side,
    decimal EntryPrice,
    decimal MarkPrice,
    decimal Quantity,
    decimal UnrealizedPnl,
    decimal Leverage,
    MarginType MarginType,
    DateTime OpenTime);
