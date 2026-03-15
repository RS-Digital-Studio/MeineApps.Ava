using BingXBot.Core.Models;

namespace BingXBot.Tests.Engine;

/// <summary>Hilfsmethoden für Engine-Tests</summary>
public static class TestHelper
{
    /// <summary>Generiert deterministische Test-Candles</summary>
    public static List<Candle> GenerateTestCandles(int count, decimal startPrice = 100m, decimal volatility = 2m)
    {
        var candles = new List<Candle>();
        var rng = new Random(42); // Deterministisch
        var price = startPrice;
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < count; i++)
        {
            var change = (decimal)(rng.NextDouble() - 0.5) * volatility;
            var open = price;
            var close = price + change;
            var high = Math.Max(open, close) + (decimal)rng.NextDouble() * volatility * 0.5m;
            var low = Math.Min(open, close) - (decimal)rng.NextDouble() * volatility * 0.5m;
            var volume = 1000m + (decimal)rng.NextDouble() * 500m;

            candles.Add(new Candle(baseTime.AddHours(i), open, high, low, close, volume, baseTime.AddHours(i + 1)));
            price = close;
        }
        return candles;
    }

    /// <summary>Generiert Candles mit steigendem Trend (für Crossover-Tests)</summary>
    public static List<Candle> GenerateTrendingCandles(int count, decimal startPrice = 100m, bool uptrend = true)
    {
        var candles = new List<Candle>();
        var rng = new Random(42);
        var price = startPrice;
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var trendDirection = uptrend ? 1m : -1m;

        for (int i = 0; i < count; i++)
        {
            // Starker Trend mit kleinem Rauschen
            var trendStep = trendDirection * 0.5m;
            var noise = (decimal)(rng.NextDouble() - 0.5) * 0.3m;
            var open = price;
            var close = price + trendStep + noise;
            var high = Math.Max(open, close) + (decimal)rng.NextDouble() * 0.5m;
            var low = Math.Min(open, close) - (decimal)rng.NextDouble() * 0.5m;
            var volume = 1000m + (decimal)rng.NextDouble() * 500m;

            candles.Add(new Candle(baseTime.AddHours(i), open, high, low, close, volume, baseTime.AddHours(i + 1)));
            price = close;
        }
        return candles;
    }

    /// <summary>Erstellt einen MarketContext aus Candles</summary>
    public static MarketContext CreateContext(List<Candle> candles, string symbol = "BTC-USDT")
    {
        var lastCandle = candles[^1];
        var ticker = new Ticker(symbol, lastCandle.Close, lastCandle.Close - 0.1m,
            lastCandle.Close + 0.1m, 50000m, 1.5m, DateTime.UtcNow);
        var account = new AccountInfo(10000m, 9000m, 0m, 0m);
        return new MarketContext(symbol, candles, ticker, new List<Position>(), account);
    }
}
