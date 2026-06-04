using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;

namespace BingXBot.Tests.Backtest;

/// <summary>Strategie, die nie ein Signal liefert (fuer Timeline-/State-Tests).</summary>
internal sealed class NoopStrategy : IStrategy
{
    public string Name => "Noop";
    public string Description => "Liefert nie ein Signal.";
    public IReadOnlyList<StrategyParameter> Parameters => [];
    public bool RequiresHigherTimeframeContext => false;
    public SignalResult Evaluate(MarketContext context) => new(Signal.None, 0m, null, null, null, "");
    public void WarmUp(IReadOnlyList<Candle> history) { }
    public void Reset() { }
    public IStrategy Clone() => new NoopStrategy();
}

/// <summary>
/// Test-Strategie, die auf JEDER Kerze ein Long-Signal liefert. SL eng (1 %, damit der
/// MaxRiskPercentPerTrade-Cap die Position NICHT auf unter MinPositionSizeRetentionPercent
/// schrumpft und der Trade abgelehnt wird), TP sehr weit (200 %, wird in den steigenden
/// Test-Kerzen nie getroffen → Positionen bleiben offen und stauen sich gegen den
/// MaxOpenPositions-Gate). Erzwingt so gleichzeitige Entry-Versuche auf allen Symbolen.
/// SL/TP relativ zum aktuellen Close (entryPrice = Close der letzten Kerze).
/// </summary>
internal sealed class AlwaysLongStrategy(decimal slPct = 0.01m, decimal tpPct = 2.0m) : IStrategy
{
    public string Name => "AlwaysLong";
    public string Description => "Long auf jeder Kerze.";
    public IReadOnlyList<StrategyParameter> Parameters => [];
    public bool RequiresHigherTimeframeContext => false;

    public SignalResult Evaluate(MarketContext context)
    {
        if (context.Candles.Count == 0) return new(Signal.None, 0m, null, null, null, "");
        var price = context.Candles[^1].Close;
        return new SignalResult(Signal.Long, 5m, price, price * (1m - slPct), price * (1m + tpPct), "AlwaysLong");
    }

    public void WarmUp(IReadOnlyList<Candle> history) { }
    public void Reset() { }
    public IStrategy Clone() => new AlwaysLongStrategy(slPct, tpPct);
}

/// <summary>
/// Fake-PublicClient: liefert pro Symbol vorbereitete Kerzen aus einem Dictionary, sonst leer.
/// Kein Netz, deterministisch. Optionaler <paramref name="onlyTf"/>-Filter: nur fuer diese TF werden
/// Kerzen geliefert (W1/D1-Anfragen der Single-Engine bleiben leer → kein Extra-Kontext, fairer Vergleich).
/// </summary>
internal sealed class FakePublicClient(IReadOnlyDictionary<string, List<Candle>> klines, TimeFrame? onlyTf = null)
    : IPublicMarketDataClient
{
    public Task<List<Candle>> GetKlinesAsync(string symbol, TimeFrame tf, DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (onlyTf.HasValue && tf != onlyTf.Value) return Task.FromResult<List<Candle>>([]);
        return Task.FromResult(klines.TryGetValue(symbol, out var c) ? new List<Candle>(c) : []);
    }

    public Task<List<Ticker>> GetAllTickersAsync(CancellationToken ct = default) => Task.FromResult(new List<Ticker>());
    public Task<List<string>> GetAllSymbolsAsync(CancellationToken ct = default) => Task.FromResult(klines.Keys.ToList());
    public Task<DateTime> GetServerTimeAsync(CancellationToken ct = default) => Task.FromResult(DateTime.UtcNow);
}

/// <summary>Kerzen-Generatoren fuer Portfolio-Tests.</summary>
internal static class PortfolioCandleGen
{
    public static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Synchron schliessende H4-Kerzen (alle Symbole teilen dieselbe Timeline).</summary>
    public static List<Candle> Trending(int count, decimal startPrice, decimal stepPerCandle)
    {
        var list = new List<Candle>(count);
        var price = startPrice;
        for (int i = 0; i < count; i++)
        {
            var open = price;
            var close = price + stepPerCandle;
            var high = Math.Max(open, close) + 0.5m;
            var low = Math.Min(open, close) - 0.5m;
            var openTime = Start.AddHours(4 * i);
            list.Add(new Candle(openTime, open, high, Math.Max(low, 0.01m), close, 1000m, openTime.AddHours(4)));
            price = Math.Max(close, 0.01m);
        }
        return list;
    }
}
