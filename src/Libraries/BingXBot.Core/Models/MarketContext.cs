namespace BingXBot.Core.Models;

public record MarketContext(
    string Symbol,
    IReadOnlyList<Candle> Candles,
    Ticker CurrentTicker,
    IReadOnlyList<Position> OpenPositions,
    AccountInfo Account,
    IReadOnlyList<Candle>? HigherTimeframeCandles = null);
