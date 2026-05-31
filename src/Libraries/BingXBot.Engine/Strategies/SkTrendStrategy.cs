using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// "Reparierte SK" — nutzt die SK-Sequenz-Erkennung (Smart-Money-Strukturpunkte) als Entry-Trigger,
/// behebt aber die drei strukturellen Verlustquellen des Original-SK:
/// <list type="number">
/// <item><b>Trend-Filter:</b> SK steigt in Korrekturen ein. Hier wird das SK-Signal nur durchgelassen,
///   wenn die Korrektur IN Trend-Richtung liegt (Long nur über EMA, Short nur darunter) — aus dem
///   riskanten Reversal wird ein Pullback-Entry im Trend.</item>
/// <item><b>Market-Entry zum Close:</b> PreferLimitOrder=false und EntryPrice=Close. Damit stimmt die
///   SL/TP-Geometrie im Backtest und Live ueberein (das Original-SK legt Limits in der Korrektur-Zone,
///   was Backtest und Live divergieren laesst).</item>
/// <item><b>ATR-SL + hohes RRR:</b> SL = N×ATR statt enger Fix-Pip-Clamp (0,3 %), TP1 1,5R / TP2 3R
///   statt RRR ~1,2. Markt-Rauschen stoppt nicht mehr aus.</item>
/// </list>
/// Erbt damit den selektiven, strukturbasierten Entry der SK, aber mit gesundem Risk-Profil.
/// </summary>
public sealed class SkTrendStrategy : IStrategy
{
    private readonly SequenzKonzeptStrategy _sk = new();
    private readonly int _emaPeriod;
    private readonly int _atrPeriod;
    private readonly decimal _atrSlMultiplier;
    private readonly decimal _tp1Rrr;
    private readonly decimal _tp2Rrr;

    public SkTrendStrategy(int emaPeriod = 50, int atrPeriod = 14, decimal atrSlMultiplier = 2.5m,
        decimal tp1Rrr = 1.5m, decimal tp2Rrr = 3.0m)
    {
        _emaPeriod = emaPeriod;
        _atrPeriod = atrPeriod;
        _atrSlMultiplier = atrSlMultiplier;
        _tp1Rrr = tp1Rrr;
        _tp2Rrr = tp2Rrr;
    }

    public string Name => "SkTrend";
    public string Description => "SK-Sequenz-Trigger + Trend-Filter + Market-Entry + ATR-SL/RRR (reparierte SK)";
    public IReadOnlyList<StrategyParameter> Parameters => [];

    public SignalResult Evaluate(MarketContext context)
    {
        var sk = _sk.Evaluate(context);
        if (sk.Signal is not (Signal.Long or Signal.Short))
            return new SignalResult(Signal.None, 0m, null, null, null, sk.Reason);

        var c = context.Candles;
        int i = c.Count - 1;
        if (c.Count < _emaPeriod + 5)
            return None("insufficient_data");

        if (context.OpenPositions.Any(p => string.Equals(p.Symbol, context.Symbol, StringComparison.OrdinalIgnoreCase)))
            return None("position_open");

        var ema = IndicatorHelper.CalculateEma(c, _emaPeriod);
        var atr = IndicatorHelper.CalculateAtr(c, _atrPeriod);
        if (!ema[i].HasValue || !atr[i].HasValue || atr[i]!.Value <= 0m)
            return None("insufficient_data");

        var close = c[i].Close;
        var emaV = ema[i]!.Value;
        var atrV = atr[i]!.Value;

        // Trend-Filter: SK-Signal nur in Trend-Richtung (Pullback statt Gegen-Trend-Reversal).
        if (sk.Signal == Signal.Long && close <= emaV) return None("counter_trend");
        if (sk.Signal == Signal.Short && close >= emaV) return None("counter_trend");

        if (sk.Signal == Signal.Long)
        {
            var sl = close - _atrSlMultiplier * atrV;
            var risk = close - sl;
            if (risk <= 0m) return None("sl_geometry_error");
            return new SignalResult(Signal.Long, sk.Confidence, close, sl, close + _tp1Rrr * risk,
                "SkTrend Long (SK-Sequenz, Pullback im Aufwaertstrend)",
                TakeProfit2: close + _tp2Rrr * risk, ConfluenceScore: sk.ConfluenceScore,
                PreferLimitOrder: false, EntryAtr: atrV);
        }
        else
        {
            var sl = close + _atrSlMultiplier * atrV;
            var risk = sl - close;
            if (risk <= 0m) return None("sl_geometry_error");
            return new SignalResult(Signal.Short, sk.Confidence, close, sl, close - _tp1Rrr * risk,
                "SkTrend Short (SK-Sequenz, Pullback im Abwaertstrend)",
                TakeProfit2: close - _tp2Rrr * risk, ConfluenceScore: sk.ConfluenceScore,
                PreferLimitOrder: false, EntryAtr: atrV);
        }
    }

    private static SignalResult None(string reason) => new(Signal.None, 0m, null, null, null, reason);

    public void WarmUp(IReadOnlyList<Candle> history) => _sk.WarmUp(history);
    public void Reset() => _sk.Reset();
    public IStrategy Clone() => new SkTrendStrategy(_emaPeriod, _atrPeriod, _atrSlMultiplier, _tp1Rrr, _tp2Rrr);
}
