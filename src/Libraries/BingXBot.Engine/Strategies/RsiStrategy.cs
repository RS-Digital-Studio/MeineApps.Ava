using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// RSI-Strategie (Mean Reversion).
/// Long wenn RSI unter Oversold-Schwelle, Short wenn RSI über Overbought-Schwelle.
/// ATR-basierter Stop-Loss und Take-Profit.
/// </summary>
public class RsiStrategy : IStrategy
{
    public string Name => "RSI";
    public string Description => "Long bei überverkauft (RSI < Oversold), Short bei überkauft (RSI > Overbought)";

    private int _period = 14;
    private decimal _oversold = 30m;
    private decimal _overbought = 70m;
    private int _atrPeriod = 14;
    private decimal _tpMultiplier = 2m;

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("Period", "RSI Periode", "int", _period, 5, 50, 1),
        new("Oversold", "Überverkauft-Schwelle", "decimal", _oversold, 10m, 40m, 5m),
        new("Overbought", "Überkauft-Schwelle", "decimal", _overbought, 60m, 90m, 5m),
        new("AtrPeriod", "ATR Periode für Stop-Loss", "int", _atrPeriod, 5, 50, 1),
        new("TpMultiplier", "Take-Profit Multiplikator", "decimal", _tpMultiplier, 1m, 5m, 0.5m),
    };

    public SignalResult Evaluate(MarketContext context)
    {
        var candles = context.Candles;
        if (candles.Count < _period + 5)
            return new SignalResult(Signal.None, 0m, null, null, null, "Zu wenig Daten");

        var rsi = IndicatorHelper.CalculateRsi(candles, _period);
        var atr = IndicatorHelper.CalculateAtr(candles, _atrPeriod);

        var lastRsi = rsi[^1];
        var lastAtr = atr[^1];

        if (lastRsi == null || lastAtr == null)
            return new SignalResult(Signal.None, 0m, null, null, null, "Indikatoren nicht bereit");

        var currentPrice = context.CurrentTicker.LastPrice;
        var atrValue = lastAtr.Value;

        // Überverkauft -> Long
        if (lastRsi < _oversold)
        {
            // Stärkere Confidence je tiefer der RSI
            var confidence = Math.Min(1m, 0.5m + (_oversold - lastRsi.Value) / _oversold * 0.5m);
            var sl = currentPrice - atrValue * 1.5m;
            var tp = currentPrice + atrValue * 1.5m * _tpMultiplier;
            return new SignalResult(Signal.Long, confidence, currentPrice, sl, tp,
                $"RSI {lastRsi.Value:F1} unter Oversold ({_oversold})");
        }

        // Überkauft -> Short
        if (lastRsi > _overbought)
        {
            var confidence = Math.Min(1m, 0.5m + (lastRsi.Value - _overbought) / (100m - _overbought) * 0.5m);
            var sl = currentPrice + atrValue * 1.5m;
            var tp = currentPrice - atrValue * 1.5m * _tpMultiplier;
            return new SignalResult(Signal.Short, confidence, currentPrice, sl, tp,
                $"RSI {lastRsi.Value:F1} über Overbought ({_overbought})");
        }

        return new SignalResult(Signal.None, 0m, null, null, null,
            $"RSI {lastRsi.Value:F1} im neutralen Bereich");
    }

    public void WarmUp(IReadOnlyList<Candle> history) { /* Warmup-Logik bei Bedarf */ }
    public void Reset() { /* State zuruecksetzen bei Bedarf */ }

    public IStrategy Clone() => new RsiStrategy
    {
        _period = _period,
        _oversold = _oversold,
        _overbought = _overbought,
        _atrPeriod = _atrPeriod,
        _tpMultiplier = _tpMultiplier
    };
}
