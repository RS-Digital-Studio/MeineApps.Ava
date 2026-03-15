using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Bollinger Bands Mean-Reversion-Strategie.
/// Long wenn Preis unter unterem Band, Short wenn Preis über oberem Band.
/// ATR-basierter Stop-Loss, mittleres Band als Take-Profit-Orientierung.
/// </summary>
public class BollingerStrategy : IStrategy
{
    public string Name => "Bollinger Bands";
    public string Description => "Mean Reversion: Long unter unterem Band, Short über oberem Band";

    private int _period = 20;
    private decimal _stdDev = 2m;
    private int _atrPeriod = 14;
    private decimal _tpMultiplier = 1.5m;

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("Period", "Bollinger Periode", "int", _period, 10, 50, 1),
        new("StdDev", "Standardabweichung", "decimal", _stdDev, 1m, 3m, 0.5m),
        new("AtrPeriod", "ATR Periode für Stop-Loss", "int", _atrPeriod, 5, 50, 1),
        new("TpMultiplier", "Take-Profit Multiplikator", "decimal", _tpMultiplier, 1m, 5m, 0.5m),
    };

    public SignalResult Evaluate(MarketContext context)
    {
        var candles = context.Candles;
        if (candles.Count < _period + 5)
            return new SignalResult(Signal.None, 0m, null, null, null, "Zu wenig Daten");

        var (upper, middle, lower) = IndicatorHelper.CalculateBollinger(candles, _period, _stdDev);
        var atr = IndicatorHelper.CalculateAtr(candles, _atrPeriod);

        var lastUpper = upper[^1];
        var lastMiddle = middle[^1];
        var lastLower = lower[^1];
        var lastAtr = atr[^1];

        if (lastUpper == null || lastMiddle == null || lastLower == null || lastAtr == null)
            return new SignalResult(Signal.None, 0m, null, null, null, "Indikatoren nicht bereit");

        var currentPrice = context.CurrentTicker.LastPrice;
        var atrValue = lastAtr.Value;
        var bandWidth = lastUpper.Value - lastLower.Value;

        // Preis unter unterem Band -> Long (Mean Reversion nach oben)
        if (currentPrice < lastLower.Value)
        {
            // Stärkere Confidence je weiter unter dem Band
            var distance = lastLower.Value - currentPrice;
            var confidence = Math.Min(1m, 0.6m + distance / bandWidth * 0.4m);
            var sl = currentPrice - atrValue * 1.5m;
            var tp = lastMiddle.Value + (lastMiddle.Value - currentPrice) * (_tpMultiplier - 1m);
            return new SignalResult(Signal.Long, confidence, currentPrice, sl, tp,
                $"Preis {currentPrice:F2} unter unterem Band {lastLower.Value:F2}");
        }

        // Preis über oberem Band -> Short (Mean Reversion nach unten)
        if (currentPrice > lastUpper.Value)
        {
            var distance = currentPrice - lastUpper.Value;
            var confidence = Math.Min(1m, 0.6m + distance / bandWidth * 0.4m);
            var sl = currentPrice + atrValue * 1.5m;
            var tp = lastMiddle.Value - (currentPrice - lastMiddle.Value) * (_tpMultiplier - 1m);
            return new SignalResult(Signal.Short, confidence, currentPrice, sl, tp,
                $"Preis {currentPrice:F2} über oberem Band {lastUpper.Value:F2}");
        }

        return new SignalResult(Signal.None, 0m, null, null, null,
            $"Preis innerhalb der Bänder ({lastLower.Value:F2} - {lastUpper.Value:F2})");
    }

    public void WarmUp(IReadOnlyList<Candle> history) { /* Warmup-Logik bei Bedarf */ }
    public void Reset() { /* State zuruecksetzen bei Bedarf */ }

    public IStrategy Clone() => new BollingerStrategy
    {
        _period = _period,
        _stdDev = _stdDev,
        _atrPeriod = _atrPeriod,
        _tpMultiplier = _tpMultiplier
    };
}
