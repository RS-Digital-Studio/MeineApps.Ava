using Skender.Stock.Indicators;
using BingXBot.Core.Models;

namespace BingXBot.Engine.Indicators;

/// <summary>
/// Wrapper um Skender.Stock.Indicators.
/// Konvertiert BingXBot Candle Records zu Quote Objekten und berechnet Indikatoren.
/// </summary>
public static class IndicatorHelper
{
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
        var quotes = ToQuotes(candles);
        return quotes.GetEma(period)
            .Select(r => r.Ema.HasValue ? (decimal?)Convert.ToDecimal(r.Ema.Value) : null)
            .ToList();
    }

    /// <summary>SMA</summary>
    public static IReadOnlyList<decimal?> CalculateSma(IReadOnlyList<Candle> candles, int period)
    {
        var quotes = ToQuotes(candles);
        return quotes.GetSma(period)
            .Select(r => r.Sma.HasValue ? (decimal?)Convert.ToDecimal(r.Sma.Value) : null)
            .ToList();
    }

    /// <summary>RSI</summary>
    public static IReadOnlyList<decimal?> CalculateRsi(IReadOnlyList<Candle> candles, int period = 14)
    {
        var quotes = ToQuotes(candles);
        return quotes.GetRsi(period)
            .Select(r => r.Rsi.HasValue ? (decimal?)Convert.ToDecimal(r.Rsi.Value) : null)
            .ToList();
    }

    /// <summary>MACD (gibt Tuple zurück: Macd, Signal, Histogram)</summary>
    public static (IReadOnlyList<decimal?> Macd, IReadOnlyList<decimal?> Signal, IReadOnlyList<decimal?> Histogram)
        CalculateMacd(IReadOnlyList<Candle> candles, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        var quotes = ToQuotes(candles);
        var results = quotes.GetMacd(fastPeriod, slowPeriod, signalPeriod).ToList();
        return (
            results.Select(r => r.Macd.HasValue ? (decimal?)Convert.ToDecimal(r.Macd.Value) : null).ToList(),
            results.Select(r => r.Signal.HasValue ? (decimal?)Convert.ToDecimal(r.Signal.Value) : null).ToList(),
            results.Select(r => r.Histogram.HasValue ? (decimal?)Convert.ToDecimal(r.Histogram.Value) : null).ToList()
        );
    }

    /// <summary>Bollinger Bands (gibt Upper, Middle, Lower zurück)</summary>
    public static (IReadOnlyList<decimal?> Upper, IReadOnlyList<decimal?> Middle, IReadOnlyList<decimal?> Lower)
        CalculateBollinger(IReadOnlyList<Candle> candles, int period = 20, decimal stdDev = 2m)
    {
        var quotes = ToQuotes(candles);
        var results = quotes.GetBollingerBands(period, (double)stdDev).ToList();
        return (
            results.Select(r => r.UpperBand.HasValue ? (decimal?)Convert.ToDecimal(r.UpperBand.Value) : null).ToList(),
            results.Select(r => r.Sma.HasValue ? (decimal?)Convert.ToDecimal(r.Sma.Value) : null).ToList(),
            results.Select(r => r.LowerBand.HasValue ? (decimal?)Convert.ToDecimal(r.LowerBand.Value) : null).ToList()
        );
    }

    /// <summary>ATR (Average True Range)</summary>
    public static IReadOnlyList<decimal?> CalculateAtr(IReadOnlyList<Candle> candles, int period = 14)
    {
        var quotes = ToQuotes(candles);
        return quotes.GetAtr(period)
            .Select(r => r.Atr.HasValue ? (decimal?)Convert.ToDecimal(r.Atr.Value) : null)
            .ToList();
    }
}
