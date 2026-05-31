using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;

namespace BingXBacktestLab;

/// <summary>
/// Decorator um einen echten <see cref="IPublicMarketDataClient"/>, der Kline-Antworten als JSON
/// auf der Platte cached. Backtests werden dutzendfach mit denselben (Symbol, TF, Zeitraum)-Tupeln
/// wiederholt — ohne Cache wuerde jeder Re-Run BingX erneut paginieren (langsam + Rate-Limit-Gefahr).
/// Der Cache-Key umfasst Symbol, TF und das exakte from/to-Fenster, ist also deterministisch.
/// </summary>
public sealed class CachingPublicClient(IPublicMarketDataClient inner, string cacheDir) : IPublicMarketDataClient
{
    private int _hits;
    private int _misses;

    public int CacheHits => _hits;
    public int CacheMisses => _misses;

    public async Task<List<Candle>> GetKlinesAsync(string symbol, TimeFrame tf, DateTime from, DateTime to, CancellationToken ct = default)
    {
        Directory.CreateDirectory(cacheDir);
        var key = $"{symbol}|{tf}|{from:yyyyMMddHHmm}|{to:yyyyMMddHHmm}";
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(key)))[..16];
        var safeSymbol = symbol.Replace("/", "_").Replace(":", "_");
        var path = Path.Combine(cacheDir, $"{safeSymbol}_{tf}_{hash}.json");

        if (File.Exists(path))
        {
            try
            {
                var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
                var cached = JsonSerializer.Deserialize<List<Candle>>(json);
                if (cached != null)
                {
                    Interlocked.Increment(ref _hits);
                    return cached;
                }
            }
            catch { /* korrupter Cache → neu laden */ }
        }

        Interlocked.Increment(ref _misses);
        var candles = await inner.GetKlinesAsync(symbol, tf, from, to, ct).ConfigureAwait(false);
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(candles), ct).ConfigureAwait(false);
        }
        catch { /* Cache-Schreibfehler ist nicht fatal */ }
        return candles;
    }

    public Task<List<Ticker>> GetAllTickersAsync(CancellationToken ct = default) => inner.GetAllTickersAsync(ct);
    public Task<List<string>> GetAllSymbolsAsync(CancellationToken ct = default) => inner.GetAllSymbolsAsync(ct);
    public Task<DateTime> GetServerTimeAsync(CancellationToken ct = default) => inner.GetServerTimeAsync(ct);
}
