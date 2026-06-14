namespace BingXBot.Core.Models;

public record Ticker(
    string Symbol,
    decimal LastPrice,
    decimal BidPrice,
    decimal AskPrice,
    decimal Volume24h,
    decimal PriceChangePercent24h,
    DateTime Timestamp);
