using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

/// <summary>
/// Marktkontext für die SK-Strategy-Evaluation.
/// Buch-konforme Chart-Hierarchie: Weekly → Daily → H4 → H1 → M30 (Entry-Chart).
/// </summary>
public record MarketContext(
    string Symbol,
    /// <summary>Haupt-Candles (4H — Scanner-Timeframe). Übergeordnete Sequenz-Analyse.</summary>
    IReadOnlyList<Candle> Candles,
    Ticker CurrentTicker,
    IReadOnlyList<Position> OpenPositions,
    AccountInfo Account,
    /// <summary>H1-Candles (Filter-Timeframe). Prüft ob Korrektur Schwung verliert.</summary>
    IReadOnlyList<Candle>? HigherTimeframeCandles = null,
    MarketCategory Category = MarketCategory.Crypto,
    /// <summary>M30-Candles (Entry-Chart nach SK-Buch). Primärer Trigger-Timeframe.</summary>
    IReadOnlyList<Candle>? EntryTimeframeCandles = null,
    /// <summary>Daily-Candles für übergeordneten Fahrplan (BLASH, Daily-GKLs, ATH/ATL-Position).</summary>
    IReadOnlyList<Candle>? DailyCandles = null,
    /// <summary>Weekly-Candles für Top-Level Fahrplan (Weekly-GKLs).</summary>
    IReadOnlyList<Candle>? WeeklyCandles = null);
