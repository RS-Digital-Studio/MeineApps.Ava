using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;

namespace BingXBot.Engine.Helpers;

/// <summary>
/// 04.05.2026 — CoinGecko-Backend für <see cref="MarketCapCache"/>. Ersetzt die vormals
/// in Core eingebettete HTTP-Logik (Layer-Verletzung). Singleton (DI registrieren als Singleton).
///
/// CoinGecko Free API: 10-30 Req/Min, /coins/markets braucht nur 1 Request für Top-250.
/// Aktualisierungs-Intervall: 1 Stunde. SemaphoreSlim verhindert parallele Refreshes
/// (Multi-TF-Loops könnten sonst gleichzeitig 403 triggern).
/// </summary>
public sealed class CoinGeckoMarketCapProvider : IMarketCapProvider, IDisposable
{
    private readonly HttpClient _client;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromHours(1);

    public CoinGeckoMarketCapProvider(HttpClient? client = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // CoinGecko blockiert Requests ohne User-Agent mit 403 Forbidden
        if (!_client.DefaultRequestHeaders.Contains("User-Agent"))
            _client.DefaultRequestHeaders.Add("User-Agent", "BingXBot/1.0 (Trading Bot)");
        if (!_client.DefaultRequestHeaders.Contains("Accept"))
            _client.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    /// <summary>
    /// Aktualisiert den Cache von CoinGecko (Top-250 nach Market Cap).
    /// Fehler werden geworfen — TradingServiceBase fängt sie für Logging.
    /// </summary>
    public async Task RefreshIfNeededAsync(CancellationToken ct = default)
    {
        if (DateTime.UtcNow - MarketCapCache.LastUpdateUtc < UpdateInterval && MarketCapCache.IsLoaded)
            return;

        // Lock: Nur ein Thread darf gleichzeitig CoinGecko aufrufen
        if (!await _refreshLock.WaitAsync(0, ct).ConfigureAwait(false))
            return; // Anderer Thread lädt bereits

        try
        {
            // Double-Check nach Lock
            if (DateTime.UtcNow - MarketCapCache.LastUpdateUtc < UpdateInterval && MarketCapCache.IsLoaded)
                return;

            var url = "https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&order=market_cap_desc&per_page=250&page=1&sparkline=false";
            var coins = await _client.GetFromJsonAsync<List<CoinGeckoMarketItem>>(url, ct).ConfigureAwait(false);

            if (coins == null || coins.Count == 0) return;

            var rankings = new Dictionary<string, int>(coins.Count);
            for (int i = 0; i < coins.Count; i++)
            {
                var ticker = coins[i].Symbol?.ToUpperInvariant();
                if (string.IsNullOrEmpty(ticker)) continue;

                // CoinGecko Symbol → BingX Symbol Mapping
                var bingxSymbol = $"{ticker}-USDT";
                rankings[bingxSymbol] = i + 1; // Rang 1-basiert
            }

            MarketCapCache.SetRankings(rankings);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose()
    {
        _refreshLock.Dispose();
    }

    /// <summary>CoinGecko /coins/markets Response-Item (nur die Felder die wir brauchen).</summary>
    private sealed class CoinGeckoMarketItem
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("market_cap_rank")]
        public int? MarketCapRank { get; set; }
    }
}
