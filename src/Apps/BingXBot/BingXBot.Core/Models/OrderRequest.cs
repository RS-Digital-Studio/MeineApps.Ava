using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

public record OrderRequest(
    string Symbol,
    Side Side,
    OrderType Type,
    decimal Quantity,
    decimal? Price = null,
    decimal? StopPrice = null,
    decimal? TakeProfit = null,
    decimal? StopLoss = null);
