using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// EMA-Kreuzungsstrategie.
/// Long wenn schnelle EMA über langsame EMA kreuzt, Short umgekehrt.
/// ATR-basierter Stop-Loss und Take-Profit.
/// </summary>
public class EmaCrossStrategy : IStrategy
{
    public string Name => "EMA Cross";
    public string Description => "Kreuzt Fast-EMA über Slow-EMA -> Long, darunter -> Short";

    private int _fastPeriod = 9;
    private int _slowPeriod = 21;
    private int _atrPeriod = 14;
    private decimal _tpMultiplier = 2m;

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("FastPeriod", "Schnelle EMA Periode", "int", _fastPeriod, 3, 50, 1),
        new("SlowPeriod", "Langsame EMA Periode", "int", _slowPeriod, 10, 200, 1),
        new("AtrPeriod", "ATR Periode für Stop-Loss", "int", _atrPeriod, 5, 50, 1),
        new("TpMultiplier", "Take-Profit Multiplikator", "decimal", _tpMultiplier, 1m, 5m, 0.5m),
    };

    public SignalResult Evaluate(MarketContext context)
    {
        var candles = context.Candles;
        if (candles.Count < _slowPeriod + 5)
            return new SignalResult(Signal.None, 0m, null, null, null, "Zu wenig Daten");

        var fastEma = IndicatorHelper.CalculateEma(candles, _fastPeriod);
        var slowEma = IndicatorHelper.CalculateEma(candles, _slowPeriod);
        var atr = IndicatorHelper.CalculateAtr(candles, _atrPeriod);

        var lastFast = fastEma[^1];
        var lastSlow = slowEma[^1];
        var lastAtr = atr[^1];
        var prevFast = fastEma[^2];
        var prevSlow = slowEma[^2];

        if (lastFast == null || lastSlow == null || prevFast == null || prevSlow == null || lastAtr == null)
            return new SignalResult(Signal.None, 0m, null, null, null, "Indikatoren nicht bereit");

        var currentPrice = context.CurrentTicker.LastPrice;
        var atrValue = lastAtr.Value;

        // Bullish Cross: Fast kreuzt über Slow
        if (prevFast <= prevSlow && lastFast > lastSlow)
        {
            var sl = currentPrice - atrValue * 1.5m;
            var tp = currentPrice + atrValue * 1.5m * _tpMultiplier;
            return new SignalResult(Signal.Long, 0.8m, currentPrice, sl, tp,
                $"EMA{_fastPeriod} kreuzt über EMA{_slowPeriod}");
        }

        // Bearish Cross: Fast kreuzt unter Slow
        if (prevFast >= prevSlow && lastFast < lastSlow)
        {
            var sl = currentPrice + atrValue * 1.5m;
            var tp = currentPrice - atrValue * 1.5m * _tpMultiplier;
            return new SignalResult(Signal.Short, 0.8m, currentPrice, sl, tp,
                $"EMA{_fastPeriod} kreuzt unter EMA{_slowPeriod}");
        }

        return new SignalResult(Signal.None, 0m, null, null, null, "Kein Cross");
    }

    public void WarmUp(IReadOnlyList<Candle> history) { /* Warmup-Logik bei Bedarf */ }
    public void Reset() { /* State zuruecksetzen bei Bedarf */ }

    public IStrategy Clone() => new EmaCrossStrategy
    {
        _fastPeriod = _fastPeriod,
        _slowPeriod = _slowPeriod,
        _atrPeriod = _atrPeriod,
        _tpMultiplier = _tpMultiplier
    };
}
