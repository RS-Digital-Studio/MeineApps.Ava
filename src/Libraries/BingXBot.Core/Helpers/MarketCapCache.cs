using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BingXBot.Core.Helpers;

/// <summary>
/// Cached die Top-N Kryptowährungen nach Market Cap von CoinGecko (Free API).
/// Wird einmal pro Stunde aktualisiert. Liefert eine Whitelist von Symbolen
/// die auf BingX als "-USDT" Perpetual Futures verfügbar sind.
///
/// CoinGecko Free API: 10-30 Req/Min, /coins/markets braucht nur 1 Request für Top-250.
/// </summary>
public static class MarketCapCache
{
    private static readonly HttpClient _client;
    private static readonly ConcurrentDictionary<string, int> _rankBySymbol = new();
    private static DateTime _lastUpdate = DateTime.MinValue;
    private static readonly TimeSpan _updateInterval = TimeSpan.FromHours(1);
    private static readonly SemaphoreSlim _refreshLock = new(1, 1); // Verhindert parallele CoinGecko-Requests

    static MarketCapCache()
    {
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // CoinGecko blockiert Requests ohne User-Agent mit 403 Forbidden
        _client.DefaultRequestHeaders.Add("User-Agent", "BingXBot/1.0 (Trading Bot)");
        _client.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    /// <summary>True wenn mindestens einmal erfolgreich geladen wurde.</summary>
    public static bool IsLoaded => _rankBySymbol.Count > 0;

    /// <summary>Anzahl der gecachten Coins.</summary>
    public static int CachedCount => _rankBySymbol.Count;

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
    /// Aktualisiert den Cache von CoinGecko (Top-250 nach Market Cap).
    /// Wird automatisch aufgerufen wenn der Cache älter als 1 Stunde ist.
    /// Fehler werden ignoriert — der alte Cache bleibt aktiv.
    /// </summary>
    public static async Task RefreshIfNeededAsync()
    {
        if (DateTime.UtcNow - _lastUpdate < _updateInterval && _rankBySymbol.Count > 0)
            return;

        // Lock: Nur ein Thread darf gleichzeitig CoinGecko aufrufen
        // (Im Multi-Mode starten 3 Services parallel → ohne Lock = 3x Request → 403)
        if (!await _refreshLock.WaitAsync(0).ConfigureAwait(false))
            return; // Anderer Thread lädt bereits

        try
        {
            // Double-Check nach Lock
            if (DateTime.UtcNow - _lastUpdate < _updateInterval && _rankBySymbol.Count > 0)
                return;

            // CoinGecko Free API: Top-250 Coins nach Market Cap
            var url = "https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&order=market_cap_desc&per_page=250&page=1&sparkline=false";
            var coins = await _client.GetFromJsonAsync<List<CoinGeckoMarketItem>>(url).ConfigureAwait(false);

            if (coins == null || coins.Count == 0) return;

            _rankBySymbol.Clear();
            for (int i = 0; i < coins.Count; i++)
            {
                var ticker = coins[i].Symbol?.ToUpperInvariant();
                if (string.IsNullOrEmpty(ticker)) continue;

                // CoinGecko Symbol → BingX Symbol Mapping
                // CoinGecko: "btc", "eth", "sol" → BingX: "BTC-USDT", "ETH-USDT", "SOL-USDT"
                var bingxSymbol = $"{ticker}-USDT";
                _rankBySymbol[bingxSymbol] = i + 1; // Rang 1-basiert
            }

            _lastUpdate = DateTime.UtcNow;
        }
        catch (Exception)
        {
            // Exception NICHT schlucken — TradingServiceBase muss sie sehen für Logging
            throw;
        }
        finally
        {
            _refreshLock.Release();
        }
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

/// <summary>CoinGecko /coins/markets Response-Item (nur die Felder die wir brauchen).</summary>
internal class CoinGeckoMarketItem
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("market_cap_rank")]
    public int? MarketCapRank { get; set; }
}
