using System.Collections.Concurrent;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;

namespace BingXBacktestLab;

/// <summary>
/// In-Memory-Decorator vor dem <see cref="CachingPublicClient"/> (Disk-JSON). Beim Parameter-Sweep
/// werden dieselben (Symbol, TF, from, to)-Klines zehntausendfach gebraucht — ohne RAM-Layer wuerde
/// jeder Backtest die JSON-Datei neu deserialisieren (der eigentliche Flaschenhals). Hier wird jede
/// Kline-Sequenz genau einmal materialisiert und danach aus dem <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// bedient — thread-safe fuer die parallele Sweep-Ausfuehrung.
/// </summary>
public sealed class MemoryKlineCache(IPublicMarketDataClient inner) : IPublicMarketDataClient
{
    private readonly ConcurrentDictionary<string, List<Candle>> _cache = new();

    public int Entries => _cache.Count;

    public async Task<List<Candle>> GetKlinesAsync(string symbol, TimeFrame tf, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var key = $"{symbol}|{tf}|{from:yyyyMMddHHmm}|{to:yyyyMMddHHmm}";
        if (_cache.TryGetValue(key, out var hit))
            return hit;

        var fresh = await inner.GetKlinesAsync(symbol, tf, from, to, ct).ConfigureAwait(false);
        // Cold-Race (zwei Threads laden denselben Key gleichzeitig) ist harmlos: identische Daten,
        // letzter Schreiber gewinnt. Der vorgelagerte Preload macht das aber praktisch unmoeglich.
        _cache[key] = fresh;
        return fresh;
    }

    public Task<List<Ticker>> GetAllTickersAsync(CancellationToken ct = default) => inner.GetAllTickersAsync(ct);
    public Task<List<string>> GetAllSymbolsAsync(CancellationToken ct = default) => inner.GetAllSymbolsAsync(ct);
    public Task<DateTime> GetServerTimeAsync(CancellationToken ct = default) => inner.GetServerTimeAsync(ct);
}
