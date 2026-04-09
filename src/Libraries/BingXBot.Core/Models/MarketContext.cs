using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

public record MarketContext(
    string Symbol,
    IReadOnlyList<Candle> Candles,
    Ticker CurrentTicker,
    IReadOnlyList<Position> OpenPositions,
    AccountInfo Account,
    IReadOnlyList<Candle>? HigherTimeframeCandles = null,
    MarketCategory Category = MarketCategory.Crypto,
    /// <summary>Entry-Timeframe Candles für präzises Timing (SK-System 3. Ebene). M5 für Scalping, M15 für DayTrading.</summary>
    IReadOnlyList<Candle>? EntryTimeframeCandles = null);
