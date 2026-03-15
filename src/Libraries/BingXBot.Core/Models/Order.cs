using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

public record Order(
    string OrderId,
    string Symbol,
    Side Side,
    OrderType Type,
    decimal Price,
    decimal Quantity,
    decimal? StopPrice,
    DateTime CreateTime,
    OrderStatus Status);
