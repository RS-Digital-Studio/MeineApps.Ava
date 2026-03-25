using System.Collections.Concurrent;
using Skender.Stock.Indicators;
using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// Indikator-Typ für den Cache-Key (Enum statt String → kein Heap-Alloc).
/// </summary>
public enum IndicatorType : byte
{
    EMA, SMA, RSI, MACD, BollingerBands, ATR, ADX, Stochastic
}

/// <summary>
/// Struct-basierter Cache-Key für IndicatorHelper.
/// Vermeidet String-Allokationen bei jedem Cache-Lookup.
/// </summary>
public readonly struct IndicatorCacheKey : IEquatable<IndicatorCacheKey>
{
    public readonly long ScanGeneration;
    public readonly int CandleCount;
    public readonly decimal LastClose;
    public readonly long LastOpenTimeTicks;
    public readonly IndicatorType Indicator;
    public readonly int Param1; // Erste Periode (EMA, SMA, RSI, ATR, ADX, Stochastik lookback)
    public readonly int Param2; // Zweite Periode (MACD slow, BB stdDev*1000, Stochastik signal)
    public readonly int Param3; // Dritte Periode (MACD signal, Stochastik smooth)

    public IndicatorCacheKey(long gen, IReadOnlyList<Candle> candles, IndicatorType indicator,
        int p1 = 0, int p2 = 0, int p3 = 0)
    {
        ScanGeneration = gen;
        CandleCount = candles.Count;
        if (candles.Count > 0)
        {
            var last = candles[^1];
            LastClose = last.Close;
            LastOpenTimeTicks = last.OpenTime.Ticks;
        }
        Indicator = indicator;
        Param1 = p1;
        Param2 = p2;
        Param3 = p3;
    }

    public bool Equals(IndicatorCacheKey other) =>
        ScanGeneration == other.ScanGeneration &&
        CandleCount == other.CandleCount &&
        LastClose == other.LastClose &&
        LastOpenTimeTicks == other.LastOpenTimeTicks &&
        Indicator == other.Indicator &&
        Param1 == other.Param1 &&
        Param2 == other.Param2 &&
        Param3 == other.Param3;

    public override bool Equals(object? obj) => obj is IndicatorCacheKey k && Equals(k);

    public override int GetHashCode() =>
        HashCode.Combine(ScanGeneration, CandleCount, LastClose, LastOpenTimeTicks,
            Indicator, Param1, Param2, Param3);
}

/// <summary>
/// Wrapper um Skender.Stock.Indicators.
/// Konvertiert BingXBot Candle Records zu Quote Objekten und berechnet Indikatoren.
/// Integrierter Cache: Wenn dieselbe Candle-Sequenz (gleiche Anzahl + letzter Close)
/// mehrmals berechnet wird, werden gecachte Ergebnisse zurückgegeben.
/// Cache wird pro Scan-Durchlauf am Ende geleert (ClearCache()).
/// </summary>
public static class IndicatorHelper
{
    // Cache: Struct-basierter Key vermeidet String-Allokationen pro Lookup.
    // Scan-Generation verhindert Race Conditions bei parallelen Scan-Durchläufen:
    // ClearCache() erhöht den Zähler, alte Cache-Einträge mit altem Zähler werden ignoriert
    private static readonly ConcurrentDictionary<IndicatorCacheKey, object> _cache = new();
    private static long _scanGeneration;

    /// <summary>Cache leeren (am Ende eines Scan-Durchlaufs aufrufen). Thread-safe per Generation.</summary>
    public static void ClearCache()
    {
        Interlocked.Increment(ref _scanGeneration);
        _cache.Clear();
    }

    /// <summary>Candles zu Quotes konvertieren</summary>
    public static IEnumerable<Quote> ToQuotes(IReadOnlyList<Candle> candles)
    {
        return candles.Select(c => new Quote
        {
            Date = c.OpenTime,
            Open = c.Open,
            High = c.High,
            Low = c.Low,
            Close = c.Close,
            Volume = c.Volume
        });
    }

    /// <summary>EMA: gibt Liste von decimal? zurück (null für Warmup-Phase)</summary>
    public static IReadOnlyList<decimal?> CalculateEma(IReadOnlyList<Candle> candles, int period)
    {
        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles, IndicatorType.EMA, period);
        if (_cache.TryGetValue(key, out var cached))
            return (IReadOnlyList<decimal?>)cached;

        var result = (IReadOnlyList<decimal?>)ToQuotes(candles).GetEma(period)
            .Select(r => r.Ema.HasValue ? (decimal?)Convert.ToDecimal(r.Ema.Value) : null)
            .ToList();
        _cache.TryAdd(key, result);
        return result;
    }

    /// <summary>SMA</summary>
    public static IReadOnlyList<decimal?> CalculateSma(IReadOnlyList<Candle> candles, int period)
    {
        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles, IndicatorType.SMA, period);
        if (_cache.TryGetValue(key, out var cached))
            return (IReadOnlyList<decimal?>)cached;

        var result = (IReadOnlyList<decimal?>)ToQuotes(candles).GetSma(period)
            .Select(r => r.Sma.HasValue ? (decimal?)Convert.ToDecimal(r.Sma.Value) : null)
            .ToList();
        _cache.TryAdd(key, result);
        return result;
    }

    /// <summary>RSI</summary>
    public static IReadOnlyList<decimal?> CalculateRsi(IReadOnlyList<Candle> candles, int period = 14)
    {
        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles, IndicatorType.RSI, period);
        if (_cache.TryGetValue(key, out var cached))
            return (IReadOnlyList<decimal?>)cached;

        var result = (IReadOnlyList<decimal?>)ToQuotes(candles).GetRsi(period)
            .Select(r => r.Rsi.HasValue ? (decimal?)Convert.ToDecimal(r.Rsi.Value) : null)
            .ToList();
        _cache.TryAdd(key, result);
        return result;
    }

    /// <summary>MACD (gibt Tuple zurück: Macd, Signal, Histogram)</summary>
    public static (IReadOnlyList<decimal?> Macd, IReadOnlyList<decimal?> Signal, IReadOnlyList<decimal?> Histogram)
        CalculateMacd(IReadOnlyList<Candle> candles, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles, IndicatorType.MACD, fastPeriod, slowPeriod, signalPeriod);
        if (_cache.TryGetValue(key, out var cached))
            return ((IReadOnlyList<decimal?>, IReadOnlyList<decimal?>, IReadOnlyList<decimal?>))cached;

        var results = ToQuotes(candles).GetMacd(fastPeriod, slowPeriod, signalPeriod).ToList();
        var result = (
            (IReadOnlyList<decimal?>)results.Select(r => r.Macd.HasValue ? (decimal?)Convert.ToDecimal(r.Macd.Value) : null).ToList(),
            (IReadOnlyList<decimal?>)results.Select(r => r.Signal.HasValue ? (decimal?)Convert.ToDecimal(r.Signal.Value) : null).ToList(),
            (IReadOnlyList<decimal?>)results.Select(r => r.Histogram.HasValue ? (decimal?)Convert.ToDecimal(r.Histogram.Value) : null).ToList()
        );
        _cache.TryAdd(key, result);
        return result;
    }

    /// <summary>Bollinger Bands (gibt Upper, Middle, Lower zurück)</summary>
    public static (IReadOnlyList<decimal?> Upper, IReadOnlyList<decimal?> Middle, IReadOnlyList<decimal?> Lower)
        CalculateBollinger(IReadOnlyList<Candle> candles, int period = 20, decimal stdDev = 2m)
    {
        // stdDev (z.B. 2.0m) wird als int*1000 gespeichert (2000), da Struct nur ints hat
        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles, IndicatorType.BollingerBands, period, (int)(stdDev * 1000));
        if (_cache.TryGetValue(key, out var cached))
            return ((IReadOnlyList<decimal?>, IReadOnlyList<decimal?>, IReadOnlyList<decimal?>))cached;

        var results = ToQuotes(candles).GetBollingerBands(period, (double)stdDev).ToList();
        var result = (
            (IReadOnlyList<decimal?>)results.Select(r => r.UpperBand.HasValue ? (decimal?)Convert.ToDecimal(r.UpperBand.Value) : null).ToList(),
            (IReadOnlyList<decimal?>)results.Select(r => r.Sma.HasValue ? (decimal?)Convert.ToDecimal(r.Sma.Value) : null).ToList(),
            (IReadOnlyList<decimal?>)results.Select(r => r.LowerBand.HasValue ? (decimal?)Convert.ToDecimal(r.LowerBand.Value) : null).ToList()
        );
        _cache.TryAdd(key, result);
        return result;
    }

    /// <summary>ATR (Average True Range)</summary>
    public static IReadOnlyList<decimal?> CalculateAtr(IReadOnlyList<Candle> candles, int period = 14)
    {
        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles, IndicatorType.ATR, period);
        if (_cache.TryGetValue(key, out var cached))
            return (IReadOnlyList<decimal?>)cached;

        var result = (IReadOnlyList<decimal?>)ToQuotes(candles).GetAtr(period)
            .Select(r => r.Atr.HasValue ? (decimal?)Convert.ToDecimal(r.Atr.Value) : null)
            .ToList();
        _cache.TryAdd(key, result);
        return result;
    }

    /// <summary>ADX (Average Directional Index) - misst Trend-Stärke (nicht Richtung).</summary>
    public static IReadOnlyList<decimal?> CalculateAdx(IReadOnlyList<Candle> candles, int period = 14)
    {
        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles, IndicatorType.ADX, period);
        if (_cache.TryGetValue(key, out var cached))
            return (IReadOnlyList<decimal?>)cached;

        var result = (IReadOnlyList<decimal?>)ToQuotes(candles).GetAdx(period)
            .Select(r => r.Adx.HasValue ? (decimal?)Convert.ToDecimal(r.Adx.Value) : null)
            .ToList();
        _cache.TryAdd(key, result);
        return result;
    }

    /// <summary>
    /// Higher-Timeframe Trend-Check: Berechnet EMA50 auf der höheren Timeframe
    /// und gibt die Trend-Richtung zurück (1=bullish, -1=bearish, 0=neutral).
    /// Nutzbar als Konfirmationsfilter: Signal nur wenn höhere TF zustimmt.
    /// </summary>
    public static int GetHigherTimeframeTrend(IReadOnlyList<Candle>? htfCandles, int emaPeriod = 50)
    {
        if (htfCandles == null || htfCandles.Count < emaPeriod + 5) return 0; // Neutral = kein Filter

        var ema = CalculateEma(htfCandles, emaPeriod);
        var lastEma = ema[^1];
        if (lastEma == null) return 0;

        var currentPrice = htfCandles[^1].Close;
        var deviation = (currentPrice - lastEma.Value) / lastEma.Value;

        // Mindestens 0.5% Abweichung für eine klare Aussage
        if (deviation > 0.005m) return 1;   // Bullish
        if (deviation < -0.005m) return -1;  // Bearish
        return 0; // Neutral
    }

    /// <summary>Stochastik (%K und %D) - Momentum-Oszillator mit Glättung.</summary>
    public static (IReadOnlyList<decimal?> K, IReadOnlyList<decimal?> D) CalculateStochastic(
        IReadOnlyList<Candle> candles, int lookbackPeriods = 14, int signalPeriods = 3, int smoothPeriods = 3)
    {
        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles, IndicatorType.Stochastic, lookbackPeriods, signalPeriods, smoothPeriods);
        if (_cache.TryGetValue(key, out var cached))
            return ((IReadOnlyList<decimal?>, IReadOnlyList<decimal?>))cached;

        var results = ToQuotes(candles).GetStoch(lookbackPeriods, signalPeriods, smoothPeriods).ToList();
        var result = (
            (IReadOnlyList<decimal?>)results.Select(r => r.K.HasValue ? (decimal?)Convert.ToDecimal(r.K.Value) : null).ToList(),
            (IReadOnlyList<decimal?>)results.Select(r => r.D.HasValue ? (decimal?)Convert.ToDecimal(r.D.Value) : null).ToList()
        );
        _cache.TryAdd(key, result);
        return result;
    }
}
