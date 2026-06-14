using System.Collections.Concurrent;

namespace BingXBot.Core.Helpers;

/// <summary>
/// Cache für Top-N Kryptowährungen nach Market Cap.
/// 04.05.2026: HTTP-Backend wurde nach <c>BingXBot.Engine.Helpers.CoinGeckoMarketCapProvider</c>
/// extrahiert (Layer-Verletzung beseitigt — Core ist jetzt netzwerk-frei). Diese Klasse hält
/// nur noch den thread-safen Lookup-Cache und wird vom Provider via <see cref="SetRankings"/>
/// befüllt. Aufrufer (ScanHelper/MarketScanner) lesen weiterhin statisch — Backwards-Compat
/// bleibt erhalten.
/// </summary>
public static class MarketCapCache
{
    private static readonly ConcurrentDictionary<string, int> _rankBySymbol = new();
    private static DateTime _lastUpdate = DateTime.MinValue;

    /// <summary>True wenn mindestens einmal erfolgreich geladen wurde.</summary>
    public static bool IsLoaded => _rankBySymbol.Count > 0;

    /// <summary>Anzahl der gecachten Coins.</summary>
    public static int CachedCount => _rankBySymbol.Count;

    /// <summary>Zeitpunkt der letzten erfolgreichen Aktualisierung (für Provider-Stale-Check).</summary>
    public static DateTime LastUpdateUtc => _lastUpdate;

    /// <summary>
    /// Prüft ob ein Symbol in den Top-N nach Market Cap ist.
    /// Gibt false zurück wenn das Symbol nicht im Cache ist (kein "alles erlauben" Fallback).
    /// </summary>
    public static bool IsTopCoin(string symbol, int topN = 100)
    {
        if (_rankBySymbol.Count == 0) return false; // Cache leer → NICHT erlauben (Volume-Fallback greift in ScanHelper)
        return _rankBySymbol.TryGetValue(NormalizeSymbol(symbol), out var rank) && rank <= topN;
    }

    /// <summary>Gibt den Market-Cap-Rang zurück (1 = größte). 0 wenn nicht im Cache.</summary>
    public static int GetRank(string symbol)
    {
        _rankBySymbol.TryGetValue(NormalizeSymbol(symbol), out var rank);
        return rank;
    }

    /// <summary>
    /// Wird vom <c>IMarketCapProvider</c> aufgerufen, wenn neue Rankings geladen wurden.
    /// Ersetzt den kompletten Cache atomar.
    /// </summary>
    /// <param name="rankedSymbols">Dictionary Symbol → Rang (1-basiert).</param>
    public static void SetRankings(IReadOnlyDictionary<string, int> rankedSymbols)
    {
        _rankBySymbol.Clear();
        foreach (var kvp in rankedSymbols)
            _rankBySymbol[kvp.Key] = kvp.Value;
        _lastUpdate = DateTime.UtcNow;
    }

    /// <summary>Normalisiert ein BingX-Symbol für den Lookup (z.B. "1000PEPE-USDT" → "PEPE-USDT").</summary>
    private static string NormalizeSymbol(string symbol)
    {
        // BingX hat spezielle Prefixe für kleine Coins: 1000PEPE, 1000000BABYDOGE etc.
        // CoinGecko hat das nicht → Prefix entfernen für Matching
        var s = symbol;
        if (s.StartsWith("1000000")) s = s[7..];
        else if (s.StartsWith("100000")) s = s[6..];
        else if (s.StartsWith("10000")) s = s[5..];
        else if (s.StartsWith("1000")) s = s[4..];
        return s;
    }
}
