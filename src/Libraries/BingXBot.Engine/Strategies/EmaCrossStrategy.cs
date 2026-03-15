using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// EMA-Kreuzungsstrategie (Krypto-optimiert).
/// Multi-Konfirmation: EMA-Cross + Volume-Bestätigung + EMA200 Trend-Filter + ATR-Volatilitätsfilter.
/// Breitere Perioden (12/26) für weniger Fehlsignale bei hoher Krypto-Volatilität.
/// </summary>
public class EmaCrossStrategy : IStrategy
{
    public string Name => "EMA Cross";
    public string Description => "EMA-Cross mit Volume + Trend-Filter + ATR-Filter (Krypto-optimiert)";

    private int _fastPeriod = 12;
    private int _slowPeriod = 26;
    private int _trendPeriod = 200;
    private int _atrPeriod = 14;
    private int _volumePeriod = 20;
    private decimal _tpMultiplier = 2m;
    private decimal _minAtrPercent = 0.3m;

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("FastPeriod", "Schnelle EMA Periode", "int", _fastPeriod, 5, 50, 1),
        new("SlowPeriod", "Langsame EMA Periode", "int", _slowPeriod, 15, 100, 1),
        new("TrendPeriod", "Trend-Filter EMA Periode", "int", _trendPeriod, 50, 300, 10),
        new("AtrPeriod", "ATR Periode für Stop-Loss", "int", _atrPeriod, 5, 50, 1),
        new("VolumePeriod", "Volumen-SMA Periode", "int", _volumePeriod, 10, 50, 1),
        new("TpMultiplier", "Take-Profit Multiplikator", "decimal", _tpMultiplier, 1m, 5m, 0.5m),
        new("MinAtrPercent", "Min ATR % für Volatilitätsfilter", "decimal", _minAtrPercent, 0.1m, 2m, 0.1m),
    };

    public SignalResult Evaluate(MarketContext context)
    {
        var candles = context.Candles;
        // Braucht genug Daten für EMA200
        if (candles.Count < _trendPeriod + 5)
            return new SignalResult(Signal.None, 0m, null, null, null, "Zu wenig Daten");

        var fastEma = IndicatorHelper.CalculateEma(candles, _fastPeriod);
        var slowEma = IndicatorHelper.CalculateEma(candles, _slowPeriod);
        var trendEma = IndicatorHelper.CalculateEma(candles, _trendPeriod);
        var atr = IndicatorHelper.CalculateAtr(candles, _atrPeriod);
        var volumeSma = IndicatorHelper.CalculateSma(candles, _volumePeriod);

        var lastFast = fastEma[^1];
        var lastSlow = slowEma[^1];
        var lastTrend = trendEma[^1];
        var lastAtr = atr[^1];
        var lastVolSma = volumeSma[^1];
        var prevFast = fastEma[^2];
        var prevSlow = slowEma[^2];

        if (lastFast == null || lastSlow == null || prevFast == null || prevSlow == null ||
            lastAtr == null || lastTrend == null || lastVolSma == null)
            return new SignalResult(Signal.None, 0m, null, null, null, "Indikatoren nicht bereit");

        var currentPrice = context.CurrentTicker.LastPrice;
        var atrValue = lastAtr.Value;
        var currentVolume = candles[^1].Volume;

        // ATR-Filter: Kein Trade wenn Volatilität zu niedrig (Seitwärtsmarkt)
        var atrPercent = atrValue / currentPrice * 100m;
        if (atrPercent < _minAtrPercent)
            return new SignalResult(Signal.None, 0m, null, null, null,
                $"ATR zu niedrig ({atrPercent:F2}% < {_minAtrPercent}%) - Seitwärtsmarkt");

        // Volume-Konfirmation: Volumen muss über dem SMA liegen
        var volumeAboveAvg = currentVolume > lastVolSma.Value;

        // Bullish Cross: Fast kreuzt über Slow
        if (prevFast <= prevSlow && lastFast > lastSlow)
        {
            // Trend-Filter: Nur Long wenn Preis über EMA200
            if (currentPrice < lastTrend.Value)
                return new SignalResult(Signal.None, 0m, null, null, null,
                    $"EMA-Cross bullish, aber Preis unter EMA{_trendPeriod} (Trend-Filter)");

            // Volume-Konfirmation
            if (!volumeAboveAvg)
                return new SignalResult(Signal.None, 0m, null, null, null,
                    "EMA-Cross bullish, aber Volumen unter Durchschnitt");

            var confidence = 0.8m;
            // Bonus-Confidence wenn alle Filter passen
            if (currentPrice > lastTrend.Value && volumeAboveAvg)
                confidence = 0.9m;

            var sl = currentPrice - atrValue * 1.5m;
            var tp = currentPrice + atrValue * 1.5m * _tpMultiplier;
            return new SignalResult(Signal.Long, confidence, currentPrice, sl, tp,
                $"EMA{_fastPeriod} kreuzt über EMA{_slowPeriod} (Trend+Volume bestätigt)");
        }

        // Bearish Cross: Fast kreuzt unter Slow
        if (prevFast >= prevSlow && lastFast < lastSlow)
        {
            // Trend-Filter: Nur Short wenn Preis unter EMA200
            if (currentPrice > lastTrend.Value)
                return new SignalResult(Signal.None, 0m, null, null, null,
                    $"EMA-Cross bearish, aber Preis über EMA{_trendPeriod} (Trend-Filter)");

            // Volume-Konfirmation
            if (!volumeAboveAvg)
                return new SignalResult(Signal.None, 0m, null, null, null,
                    "EMA-Cross bearish, aber Volumen unter Durchschnitt");

            var confidence = 0.8m;
            if (currentPrice < lastTrend.Value && volumeAboveAvg)
                confidence = 0.9m;

            var sl = currentPrice + atrValue * 1.5m;
            var tp = currentPrice - atrValue * 1.5m * _tpMultiplier;
            return new SignalResult(Signal.Short, confidence, currentPrice, sl, tp,
                $"EMA{_fastPeriod} kreuzt unter EMA{_slowPeriod} (Trend+Volume bestätigt)");
        }

        return new SignalResult(Signal.None, 0m, null, null, null, "Kein Cross");
    }

    public void WarmUp(IReadOnlyList<Candle> history) { }
    public void Reset() { }

    public IStrategy Clone() => new EmaCrossStrategy
    {
        _fastPeriod = _fastPeriod,
        _slowPeriod = _slowPeriod,
        _trendPeriod = _trendPeriod,
        _atrPeriod = _atrPeriod,
        _volumePeriod = _volumePeriod,
        _tpMultiplier = _tpMultiplier,
        _minAtrPercent = _minAtrPercent
    };
}
