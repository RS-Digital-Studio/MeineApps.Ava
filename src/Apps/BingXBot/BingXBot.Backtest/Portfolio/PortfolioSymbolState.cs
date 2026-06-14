using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;

namespace BingXBot.Backtest.Portfolio;

/// <summary>
/// Pro-Symbol-Zustand fuer den <see cref="PortfolioBacktestEngine"/>: vorgeladene Nav-Kerzen,
/// inkrementeller Index entlang der gemergten Timeline, eine EIGENE Strategie-Instanz (Symbole
/// teilen sich KEINEN Indikator-State) und der gecachte Markt-Kategorie-Kontext.
/// </summary>
internal sealed class PortfolioSymbolState
{
    /// <summary>Vorgeladene Navigator-Kerzen (H4), aufsteigend nach CloseTime.</summary>
    public List<Candle> Nav { get; }

    /// <summary>Eigene Strategie-Instanz dieses Symbols (bereits ge-WarmUp-t + Reset).</summary>
    public IStrategy Strategy { get; }

    /// <summary>Symbol-Name (z.B. "BTC-USDT").</summary>
    public string Symbol { get; }

    /// <summary>Gecachte Markt-Kategorie (Crypto/Forex/…) — einmal klassifiziert, nicht pro Tick.</summary>
    public MarketCategory Category { get; }

    /// <summary>
    /// CloseTime der ersten Post-Warmup-Kerze. Trading (Evaluate/Entry) erst ab dieser Zeit —
    /// davor lief nur der Indikator-Warmup (wie die Single-Symbol-Engine ab Index warmupSize handelt).
    /// </summary>
    public DateTime TradingStartCloseTime { get; init; }

    /// <summary>
    /// Inkrementeller Index der zuletzt erreichten Kerze (CloseTime &lt;= aktuelle Timeline-Zeit).
    /// -1 solange noch keine Kerze geschlossen hat.
    /// </summary>
    public int NavIdx { get; private set; } = -1;

    public PortfolioSymbolState(string symbol, List<Candle> nav, IStrategy strategy)
    {
        Symbol = symbol;
        Nav = nav;
        Strategy = strategy;
        Category = SymbolClassifier.Classify(symbol);
    }

    /// <summary>
    /// Schiebt <see cref="NavIdx"/> inkrementell auf die letzte Kerze mit <c>CloseTime &lt;= t</c>
    /// (strikt: keine Kerze, die spaeter schliesst, wird sichtbar → kein Look-Ahead).
    /// </summary>
    public void AdvanceTo(DateTime t)
    {
        while (NavIdx < Nav.Count - 1 && Nav[NavIdx + 1].CloseTime <= t)
            NavIdx++;
    }

    /// <summary>True wenn die aktuell erreichte Kerze GENAU bei <paramref name="t"/> schliesst
    /// (d.h. dieses Symbol hat an diesem Timeline-Schritt eine frisch abgeschlossene Kerze).</summary>
    public bool HasCandleAt(DateTime t) => NavIdx >= 0 && Nav[NavIdx].CloseTime == t;

    /// <summary>Die aktuell erreichte Kerze (Voraussetzung: <see cref="NavIdx"/> &gt;= 0).</summary>
    public Candle CurrentCandle => Nav[NavIdx];

    /// <summary>
    /// Zero-Copy-Prefix-Slice der Nav-Kerzen bis einschliesslich <see cref="NavIdx"/>, maximal
    /// <paramref name="maxLen"/> Kerzen (analog zum 200er-Context-Slice der Single-Symbol-Engine).
    /// </summary>
    public IReadOnlyList<Candle> ContextSlice(int maxLen = 200)
    {
        var end = NavIdx + 1;                       // exklusiv
        var start = Math.Max(0, end - maxLen);
        return new CandleSlice(Nav, start, end - start);
    }
}
