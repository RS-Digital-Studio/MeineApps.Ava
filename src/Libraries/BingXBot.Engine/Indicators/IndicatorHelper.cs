using System.Collections.Concurrent;
using Skender.Stock.Indicators;
using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// Indikator-Typ für den Cache-Key (Enum statt String → kein Heap-Alloc).
/// </summary>
public enum IndicatorType : byte
{
    EMA, SMA, RSI, MACD, BollingerBands, ATR, ADX, Stochastic, Supertrend, AtrPercentile, Fibonacci, Donchian, ZigZag
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
    // M-9-analog (gegen Cross-Symbol-Kollision): Innerhalb eines Scans haben alle Symbole derselben
    // TF dieselbe LastOpenTimeTicks und oft denselben CandleCount → LastClose war frueher das EINZIGE
    // Unterscheidungsmerkmal. Zwei Coins mit gleichem letzten Close (Sub-Cent-Memecoins) bekamen
    // denselben Cache-Eintrag → falsche Indikatoren fuer Trade-Entscheidungen. First-Candle einbeziehen
    // (analog zum bereits gehaerteten Quotes-Cache) eliminiert die Kollision praktisch vollstaendig.
    public readonly decimal FirstClose;
    public readonly long FirstOpenTimeTicks;
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
            var first = candles[0];
            FirstClose = first.Close;
            FirstOpenTimeTicks = first.OpenTime.Ticks;
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
        FirstClose == other.FirstClose &&
        FirstOpenTimeTicks == other.FirstOpenTimeTicks &&
        Indicator == other.Indicator &&
        Param1 == other.Param1 &&
        Param2 == other.Param2 &&
        Param3 == other.Param3;

    public override bool Equals(object? obj) => obj is IndicatorCacheKey k && Equals(k);

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(ScanGeneration); h.Add(CandleCount); h.Add(LastClose); h.Add(LastOpenTimeTicks);
        h.Add(FirstClose); h.Add(FirstOpenTimeTicks);
        h.Add(Indicator); h.Add(Param1); h.Add(Param2); h.Add(Param3);
        return h.ToHashCode();
    }
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

    // Cache-Statistiken (P3-3, 24.04.2026): Hit/Miss-Counter fuer Performance-Diagnose.
    // Atomische Zaehler via Interlocked, kein Lock-Overhead. Werden bei ClearCache() NICHT resettet —
    // geben Lifetime-Verhalten zurueck. Via GetCacheStats() als Snapshot abfragbar.
    private static long _cacheHits;
    private static long _cacheMisses;
    private static long _quotesHits;
    private static long _quotesMisses;

    // Quotes-Cache: Vermeidet wiederholte Konvertierung derselben Candle-Liste.
    // Key = (CandleCount, LastClose, LastOpenTimeTicks, FirstOpenTimeTicks), Value = List<Quote>
    // M-9 Fix: FirstOpenTimeTicks einbeziehen um Kollisionen bei gleicher Länge+letztem Close zu vermeiden
    private static readonly ConcurrentDictionary<(int Count, decimal Close, long LastTicks, long FirstTicks), List<Quote>> _quotesCache = new();

    /// <summary>Cache leeren (am Ende eines Scan-Durchlaufs aufrufen). Thread-safe per Generation.</summary>
    public static void ClearCache()
    {
        Interlocked.Increment(ref _scanGeneration);
        _cache.Clear();
        _quotesCache.Clear();
    }

    /// <summary>Snapshot der Cache-Statistiken fuer Debug/Monitoring-Endpoints.</summary>
    public readonly record struct CacheStats(
        long IndicatorHits,
        long IndicatorMisses,
        long QuotesHits,
        long QuotesMisses,
        int IndicatorCacheSize,
        int QuotesCacheSize,
        long ScanGeneration)
    {
        public double IndicatorHitRate => IndicatorHits + IndicatorMisses == 0
            ? 0.0
            : (double)IndicatorHits / (IndicatorHits + IndicatorMisses);

        public double QuotesHitRate => QuotesHits + QuotesMisses == 0
            ? 0.0
            : (double)QuotesHits / (QuotesHits + QuotesMisses);
    }

    /// <summary>Liefert Snapshot der Cache-Performance fuer Debug-Endpoint.</summary>
    public static CacheStats GetCacheStats() => new(
        IndicatorHits: Interlocked.Read(ref _cacheHits),
        IndicatorMisses: Interlocked.Read(ref _cacheMisses),
        QuotesHits: Interlocked.Read(ref _quotesHits),
        QuotesMisses: Interlocked.Read(ref _quotesMisses),
        IndicatorCacheSize: _cache.Count,
        QuotesCacheSize: _quotesCache.Count,
        ScanGeneration: Interlocked.Read(ref _scanGeneration));

    /// <summary>
    /// Wrapper um <c>_cache.TryGetValue</c> mit Hit/Miss-Statistik.
    /// Signatur kompatibel zu <see cref="ConcurrentDictionary{TKey,TValue}.TryGetValue"/>, damit die
    /// Aufrufstellen minimal-invasiv umgestellt werden koennen (nur Funktions-Name austauschen).
    /// </summary>
    private static bool TryCacheGet(IndicatorCacheKey key, out object cached)
    {
        if (_cache.TryGetValue(key, out var hit))
        {
            Interlocked.Increment(ref _cacheHits);
            cached = hit;
            return true;
        }
        Interlocked.Increment(ref _cacheMisses);
        cached = null!;
        return false;
    }

    /// <summary>Candles zu Quotes konvertieren (gecacht pro Candle-Set).</summary>
    public static List<Quote> ToQuotes(IReadOnlyList<Candle> candles)
    {
        if (candles.Count == 0) return [];

        var last = candles[^1];
        var first = candles[0];
        var key = (candles.Count, last.Close, last.OpenTime.Ticks, first.OpenTime.Ticks);

        if (_quotesCache.TryGetValue(key, out var cached))
        {
            Interlocked.Increment(ref _quotesHits);
            return cached;
        }
        Interlocked.Increment(ref _quotesMisses);

        var quotes = new List<Quote>(candles.Count);
        for (int i = 0; i < candles.Count; i++)
        {
            var c = candles[i];
            quotes.Add(new Quote
            {
                Date = c.OpenTime,
                Open = c.Open,
                High = c.High,
                Low = c.Low,
                Close = c.Close,
                Volume = c.Volume
            });
        }

        _quotesCache.TryAdd(key, quotes);
        return quotes;
    }

    /// <summary>EMA: gibt Liste von decimal? zurück (null für Warmup-Phase)</summary>
    public static IReadOnlyList<decimal?> CalculateEma(IReadOnlyList<Candle> candles, int period)
    {
        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles, IndicatorType.EMA, period);
        if (TryCacheGet(key, out var cached))
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
        if (TryCacheGet(key, out var cached))
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
        if (TryCacheGet(key, out var cached))
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
        if (TryCacheGet(key, out var cached))
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
        if (TryCacheGet(key, out var cached))
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
        if (TryCacheGet(key, out var cached))
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
        if (TryCacheGet(key, out var cached))
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

    /// <summary>
    /// Volume-SMA: Berechnet den SMA auf dem VOLUMEN (nicht auf dem Close-Preis).
    /// CalculateSma() nutzt Skender's GetSma() das immer auf Close rechnet - für
    /// Volume-Vergleiche MUSS diese Methode verwendet werden.
    /// </summary>
    public static IReadOnlyList<decimal?> CalculateVolumeSma(IReadOnlyList<Candle> candles, int period)
    {
        if (candles.Count == 0 || period <= 0)
            return [];

        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles, IndicatorType.SMA, period, 1); // Param2=1 unterscheidet von Close-SMA
        if (TryCacheGet(key, out var cached))
            return (IReadOnlyList<decimal?>)cached;

        // Manueller SMA auf Volume-Werte (Skender bietet keine Feld-Auswahl)
        var result = new List<decimal?>(candles.Count);
        for (int i = 0; i < candles.Count; i++)
        {
            if (i < period - 1)
            {
                result.Add(null);
                continue;
            }
            var sum = 0m;
            for (int j = i - period + 1; j <= i; j++)
                sum += candles[j].Volume;
            result.Add(sum / period);
        }

        _cache.TryAdd(key, result);
        return result;
    }

    /// <summary>Stochastik (%K und %D) - Momentum-Oszillator mit Glättung.</summary>
    public static (IReadOnlyList<decimal?> K, IReadOnlyList<decimal?> D) CalculateStochastic(
        IReadOnlyList<Candle> candles, int lookbackPeriods = 14, int signalPeriods = 3, int smoothPeriods = 3)
    {
        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles, IndicatorType.Stochastic, lookbackPeriods, signalPeriods, smoothPeriods);
        if (TryCacheGet(key, out var cached))
            return ((IReadOnlyList<decimal?>, IReadOnlyList<decimal?>))cached;

        var results = ToQuotes(candles).GetStoch(lookbackPeriods, signalPeriods, smoothPeriods).ToList();
        var result = (
            (IReadOnlyList<decimal?>)results.Select(r => r.K.HasValue ? (decimal?)Convert.ToDecimal(r.K.Value) : null).ToList(),
            (IReadOnlyList<decimal?>)results.Select(r => r.D.HasValue ? (decimal?)Convert.ToDecimal(r.D.Value) : null).ToList()
        );
        _cache.TryAdd(key, result);
        return result;
    }

    /// <summary>
    /// Supertrend-Indikator: Gibt Trend-Richtung und Supertrend-Linie zurück.
    /// Bullish = Preis über Supertrend-Linie, Bearish = darunter.
    /// Supertrend-Flip = stärkstes Trend-Signal (weniger Whipsaws als EMA-Cross).
    /// </summary>
    public static (IReadOnlyList<decimal?> SupertrendValue, IReadOnlyList<bool?> IsBullish)
        CalculateSupertrend(IReadOnlyList<Candle> candles, int lookbackPeriods = 10, decimal multiplier = 3.0m)
    {
        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles,
            IndicatorType.Supertrend, lookbackPeriods, (int)(multiplier * 100));
        if (TryCacheGet(key, out var cached))
            return ((IReadOnlyList<decimal?>, IReadOnlyList<bool?>))cached;

        var results = ToQuotes(candles).GetSuperTrend(lookbackPeriods, (double)multiplier).ToList();
        var result = (
            (IReadOnlyList<decimal?>)results.Select(r =>
                r.SuperTrend.HasValue ? (decimal?)Convert.ToDecimal(r.SuperTrend.Value) : null).ToList(),
            (IReadOnlyList<bool?>)results.Select(r =>
                r.SuperTrend.HasValue ? (bool?)(r.UpperBand == null) : null).ToList()
            // Wenn UpperBand null ist → Preis über Supertrend → bullish
        );
        _cache.TryAdd(key, result);
        return result;
    }

    /// <summary>
    /// ATR-Perzentil: Wo liegt der aktuelle ATR im Vergleich zu den letzten N Perioden (0-100).
    /// Für volatilitäts-adaptive SL/TP-Multiplikatoren.
    /// </summary>
    public static int CalculateAtrPercentile(IReadOnlyList<Candle> candles, int atrPeriod = 14, int lookback = 100)
    {
        var atr = CalculateAtr(candles, atrPeriod);
        if (atr.Count == 0 || atr[^1] == null) return 50; // Default: Mitte

        var currentAtr = atr[^1]!.Value;
        var startIdx = Math.Max(0, atr.Count - lookback);
        var values = new List<decimal>();
        for (int i = startIdx; i < atr.Count; i++)
        {
            if (atr[i].HasValue) values.Add(atr[i]!.Value);
        }
        if (values.Count < 10) return 50;

        var belowCount = values.Count(v => v <= currentAtr);
        return (int)((double)belowCount / values.Count * 100);
    }

    /// <summary>
    /// Fibonacci-Retracement- und Extension-Level basierend auf dem letzten signifikanten Swing.
    /// Fractal-basierte Swing-Erkennung (swingStrength=5 Kerzen links+rechts = robust).
    /// Gibt null zurück wenn kein signifikanter Swing gefunden wird oder Range zu klein (unter 2*ATR).
    /// </summary>
    // Sentinel-Objekt für null-Ergebnisse im Cache (ConcurrentDictionary erlaubt keine null-Values)
    private static readonly object _fibNullSentinel = new();

    public static FibonacciLevels? CalculateFibonacciLevels(IReadOnlyList<Candle> candles, int swingStrength = 5)
    {
        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles, IndicatorType.Fibonacci, swingStrength);
        if (TryCacheGet(key, out var cached))
            return cached == _fibNullSentinel ? null : (FibonacciLevels?)cached;

        var result = CalculateFibonacciInternal(candles, swingStrength);
        _cache.TryAdd(key, result ?? _fibNullSentinel);
        return result;
    }

    private static FibonacciLevels? CalculateFibonacciInternal(IReadOnlyList<Candle> candles, int swingStrength)
    {
        if (candles.Count < swingStrength * 2 + 10) return null;

        // Fractal-basierte Swing-Erkennung: Ein Swing-High ist ein High das höher ist
        // als die N Kerzen links UND rechts davon. Analog für Swing-Low.
        decimal swingHigh = 0, swingLow = 0;
        int swingHighIdx = -1, swingLowIdx = -1;

        // Von rechts nach links suchen (neueste Swings zuerst)
        // Start bei swingStrength vom Ende (brauchen N Kerzen rechts zur Bestätigung)
        for (int i = candles.Count - 1 - swingStrength; i >= swingStrength; i--)
        {
            // Swing-High prüfen
            if (swingHighIdx == -1)
            {
                var isSwingHigh = true;
                var high = candles[i].High;
                for (int j = 1; j <= swingStrength; j++)
                {
                    if (candles[i - j].High >= high || candles[i + j].High >= high)
                    { isSwingHigh = false; break; }
                }
                if (isSwingHigh) { swingHigh = high; swingHighIdx = i; }
            }

            // Swing-Low prüfen
            if (swingLowIdx == -1)
            {
                var isSwingLow = true;
                var low = candles[i].Low;
                for (int j = 1; j <= swingStrength; j++)
                {
                    if (candles[i - j].Low <= low || candles[i + j].Low <= low)
                    { isSwingLow = false; break; }
                }
                if (isSwingLow) { swingLow = low; swingLowIdx = i; }
            }

            if (swingHighIdx != -1 && swingLowIdx != -1) break;
        }

        if (swingHighIdx == -1 || swingLowIdx == -1) return null;
        // Swings müssen mindestens 3 Kerzen auseinander liegen (sonst bedeutungslos)
        if (Math.Abs(swingHighIdx - swingLowIdx) < 3) return null;

        var range = swingHigh - swingLow;
        if (range <= 0) return null;

        // Minimum-Range-Check: Swing muss signifikant sein (mindestens 1% des Preises)
        var midPrice = (swingHigh + swingLow) / 2m;
        if (midPrice > 0 && range / midPrice < 0.01m) return null;

        // Trend-Richtung: Swing-High NACH Swing-Low = Uptrend (Retracement nach unten)
        var isUptrend = swingHighIdx > swingLowIdx;

        // Retracement-Level (zwischen Low und High)
        var r236 = swingHigh - range * 0.236m;
        var r382 = swingHigh - range * 0.382m;
        var r500 = swingHigh - range * 0.500m;
        var r618 = swingHigh - range * 0.618m;
        var r786 = swingHigh - range * 0.786m;

        // Extension-Level (über den Swing hinaus)
        decimal ext1272, ext1618, ext2618;
        if (isUptrend)
        {
            // Uptrend: Extensions über das High hinaus
            ext1272 = swingLow + range * 1.272m;
            ext1618 = swingLow + range * 1.618m;
            ext2618 = swingLow + range * 2.618m;
        }
        else
        {
            // Downtrend: Extensions unter das Low hinaus
            ext1272 = swingHigh - range * 1.272m;
            ext1618 = swingHigh - range * 1.618m;
            ext2618 = swingHigh - range * 2.618m;
        }

        return new FibonacciLevels(
            swingHigh, swingLow, isUptrend,
            r236, r382, r500, r618, r786,
            ext1272, ext1618, ext2618);
    }

    /// <summary>
    /// Berechnet die Nähe des aktuellen Preises zum nächsten Fibonacci-Level.
    /// Gibt einen Wert zwischen 0 (weit entfernt) und 1 (exakt auf Level) zurück.
    /// Toleranz = Anteil der ATR (default 0.5 = halbe ATR).
    /// </summary>
    public static float CalculateFibProximity(IReadOnlyList<Candle> candles, decimal currentPrice, decimal atr)
    {
        if (atr <= 0 || currentPrice <= 0) return 0f;

        var fib = CalculateFibonacciLevels(candles);
        if (fib == null) return 0f;

        // Key-Level: 38.2%, 50%, 61.8% (die wichtigsten Retracement-Level)
        var keyLevels = new[] { fib.R382, fib.R500, fib.R618 };
        var minDistance = decimal.MaxValue;

        foreach (var level in keyLevels)
        {
            var dist = Math.Abs(currentPrice - level);
            if (dist < minDistance) minDistance = dist;
        }

        // Normalisiert: 1.0 bei Distanz=0, 0.0 bei Distanz >= ATR
        var proximity = 1f - (float)(minDistance / atr);
        return Math.Clamp(proximity, 0f, 1f);
    }

    /// <summary>
    /// ADX mit DI-Richtungen (+DI und -DI) für Trend-Richtung UND -Stärke.
    /// ADX = Stärke, +DI > -DI = bullish, -DI > +DI = bearish.
    /// ADX steigend = Trend wird stärker (wichtiger als absoluter Wert).
    /// </summary>
    public static (IReadOnlyList<decimal?> Adx, IReadOnlyList<decimal?> Pdi, IReadOnlyList<decimal?> Mdi)
        CalculateAdxWithDi(IReadOnlyList<Candle> candles, int period = 14)
    {
        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles,
            IndicatorType.ADX, period, 1); // Param2=1 unterscheidet von Standard-ADX
        if (TryCacheGet(key, out var cached))
            return ((IReadOnlyList<decimal?>, IReadOnlyList<decimal?>, IReadOnlyList<decimal?>))cached;

        var results = ToQuotes(candles).GetAdx(period).ToList();
        var result = (
            (IReadOnlyList<decimal?>)results.Select(r =>
                r.Adx.HasValue ? (decimal?)Convert.ToDecimal(r.Adx.Value) : null).ToList(),
            (IReadOnlyList<decimal?>)results.Select(r =>
                r.Pdi.HasValue ? (decimal?)Convert.ToDecimal(r.Pdi.Value) : null).ToList(),
            (IReadOnlyList<decimal?>)results.Select(r =>
                r.Mdi.HasValue ? (decimal?)Convert.ToDecimal(r.Mdi.Value) : null).ToList()
        );
        _cache.TryAdd(key, result);
        return result;
    }

    /// <summary>
    /// Donchian-Channel: Höchstes High und niedrigstes Low der letzten N Perioden.
    /// Für Breakout-Erkennung: Preis über Upper = Breakout bullish, unter Lower = Breakout bearish.
    /// </summary>
    public static (IReadOnlyList<decimal?> Upper, IReadOnlyList<decimal?> Lower, IReadOnlyList<decimal?> Middle)
        CalculateDonchian(IReadOnlyList<Candle> candles, int period = 20)
    {
        var key = new IndicatorCacheKey(Interlocked.Read(ref _scanGeneration), candles, IndicatorType.Donchian, period);
        if (TryCacheGet(key, out var cached))
            return ((IReadOnlyList<decimal?>, IReadOnlyList<decimal?>, IReadOnlyList<decimal?>))cached;

        var upper = new List<decimal?>(candles.Count);
        var lower = new List<decimal?>(candles.Count);
        var middle = new List<decimal?>(candles.Count);

        for (int i = 0; i < candles.Count; i++)
        {
            if (i < period - 1)
            {
                upper.Add(null);
                lower.Add(null);
                middle.Add(null);
                continue;
            }

            var high = decimal.MinValue;
            var low = decimal.MaxValue;
            for (int j = i - period + 1; j <= i; j++)
            {
                if (candles[j].High > high) high = candles[j].High;
                if (candles[j].Low < low) low = candles[j].Low;
            }

            upper.Add(high);
            lower.Add(low);
            middle.Add((high + low) / 2m);
        }

        var result = ((IReadOnlyList<decimal?>)upper, (IReadOnlyList<decimal?>)lower, (IReadOnlyList<decimal?>)middle);
        _cache.TryAdd(key, result);
        return result;
    }
}

/// <summary>
/// Fibonacci-Retracement- und Extension-Level basierend auf einem Swing-High/Low.
/// </summary>
public record FibonacciLevels(
    decimal SwingHigh,
    decimal SwingLow,
    bool IsUptrend,
    decimal R236,
    decimal R382,
    decimal R500,
    decimal R618,
    decimal R786,
    decimal Ext1272,
    decimal Ext1618,
    decimal Ext2618)
{
    /// <summary>Swing-Range (High - Low).</summary>
    public decimal Range => SwingHigh - SwingLow;

    /// <summary>Alle Key-Retracement-Level (38.2%, 50%, 61.8%) als Array.</summary>
    public decimal[] KeyLevels => [R382, R500, R618];
}
