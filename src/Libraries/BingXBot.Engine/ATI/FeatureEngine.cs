using BingXBot.Core.Models;
using BingXBot.Core.Models.ATI;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.ATI;

/// <summary>
/// Extrahiert normalisierte Features aus einem MarketContext.
/// Alle 19 Features werden auf [-1,1] oder [0,1] normalisiert.
/// </summary>
public static class FeatureEngine
{
    /// <summary>
    /// Extrahiert einen vollständigen Feature-Snapshot aus dem aktuellen Marktzustand.
    /// </summary>
    public static FeatureSnapshot Extract(MarketContext context, float fundingRate = 0f)
    {
        var candles = context.Candles;
        var snapshot = new FeatureSnapshot
        {
            Symbol = context.Symbol,
            Timestamp = DateTime.UtcNow,
            FundingRate = fundingRate
        };

        if (candles.Count < 50) return snapshot;

        var close = (float)candles[^1].Close;
        if (close <= 0f) return snapshot;

        // Indikatoren berechnen (nutzt IndicatorHelper-Cache)
        var ema20 = IndicatorHelper.CalculateEma(candles, 20);
        var ema50 = IndicatorHelper.CalculateEma(candles, 50);
        var ema200 = IndicatorHelper.CalculateEma(candles, 200);
        var rsi = IndicatorHelper.CalculateRsi(candles);
        var macd = IndicatorHelper.CalculateMacd(candles);
        var bb = IndicatorHelper.CalculateBollinger(candles);
        var atr = IndicatorHelper.CalculateAtr(candles);
        var adx = IndicatorHelper.CalculateAdx(candles);
        var stoch = IndicatorHelper.CalculateStochastic(candles);

        // Preis-Features
        snapshot.PriceVsEma20 = NormalizeDeviation(close, LastValue(ema20));
        snapshot.PriceVsEma50 = NormalizeDeviation(close, LastValue(ema50));
        snapshot.PriceVsEma200 = NormalizeDeviation(close, LastValue(ema200));

        var e20 = LastValue(ema20);
        var e50 = LastValue(ema50);
        snapshot.EmaCrossDirection = (e20 > 0 && e50 > 0) ? Math.Sign(e20 - e50) : 0f;

        // Momentum-Features
        snapshot.RsiNormalized = Clamp(LastValue(rsi) / 100f);
        var atrVal = LastValue(atr);
        var macdHist = LastValue(macd.Histogram);
        snapshot.MacdHistogramNormalized = (atrVal > 0) ? Clamp(macdHist / atrVal) : 0f;
        snapshot.StochKNormalized = Clamp(LastValue(stoch.K) / 100f);
        snapshot.StochDNormalized = Clamp(LastValue(stoch.D) / 100f);

        // Volatilitäts-Features
        snapshot.AtrPercent = Clamp(atrVal / close);
        var bbUpper = LastValue(bb.Upper);
        var bbMiddle = LastValue(bb.Middle);
        var bbLower = LastValue(bb.Lower);
        snapshot.BollingerWidth = (bbMiddle > 0) ? Clamp((bbUpper - bbLower) / bbMiddle) : 0f;
        var bbRange = bbUpper - bbLower;
        snapshot.BollingerPosition = (bbRange > 0) ? Clamp((close - bbLower) / bbRange) : 0.5f;

        // Trend-Features
        snapshot.AdxNormalized = Clamp(LastValue(adx) / 100f);
        snapshot.HtfTrend = IndicatorHelper.GetHigherTimeframeTrend(context.HigherTimeframeCandles);

        // Volumen-Features: Aktuelles Volumen / Durchschnitt der letzten 20 Perioden
        snapshot.VolumeRatio = CalculateVolumeRatio(candles);

        // Session
        snapshot.SessionId = GetSessionId(DateTime.UtcNow);

        // Pattern-Features
        snapshot.ConsecutiveUpCandles = Clamp(CountConsecutiveCandles(candles, up: true) / 10f);
        snapshot.ConsecutiveDownCandles = Clamp(CountConsecutiveCandles(candles, up: false) / 10f);

        if (candles.Count >= 21)
        {
            var close20Ago = (float)candles[^21].Close;
            snapshot.RecentReturnPercent = (close20Ago > 0) ? Clamp((close - close20Ago) / close20Ago) : 0f;
        }

        return snapshot;
    }

    // === Hilfsmethoden ===

    private static float LastValue(IReadOnlyList<decimal?> values)
    {
        if (values.Count == 0) return 0f;
        return values[^1].HasValue ? (float)values[^1]!.Value : 0f;
    }

    private static float NormalizeDeviation(float price, float reference)
    {
        if (reference <= 0f) return 0f;
        return Clamp((price - reference) / reference);
    }

    /// <summary>Begrenzt den Wert auf [-2, 2] um Extremwerte zu verhindern.</summary>
    private static float Clamp(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
        return Math.Clamp(value, -2f, 2f);
    }

    private static float CalculateVolumeRatio(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 21) return 1f;

        var currentVolume = (float)candles[^1].Volume;
        var avgVolume = 0f;
        for (int i = candles.Count - 21; i < candles.Count - 1; i++)
            avgVolume += (float)candles[i].Volume;
        avgVolume /= 20f;

        return (avgVolume > 0) ? Clamp(currentVolume / avgVolume) : 1f;
    }

    private static float GetSessionId(DateTime utcNow)
    {
        var hour = utcNow.Hour;
        if (hour < 8) return 0f;   // Asia
        if (hour < 14) return 1f;  // Europe
        if (hour < 22) return 2f;  // US
        return 3f;                  // Late/Overlap
    }

    private static int CountConsecutiveCandles(IReadOnlyList<Candle> candles, bool up)
    {
        var count = 0;
        for (int i = candles.Count - 1; i >= 0; i--)
        {
            var isUp = candles[i].Close > candles[i].Open;
            if (up ? isUp : !isUp)
                count++;
            else
                break;
        }
        return count;
    }
}
