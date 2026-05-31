using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Trend-Following-Strategie: Donchian-Breakout IN Trend-Richtung, mit dem Markt statt dagegen.
///
/// Bewusster Gegenentwurf zum SK-System (Reversal, enge SL, niedriges RRR):
/// <list type="bullet">
/// <item><b>Mit dem Markt:</b> Entry nur in Richtung des etablierten Trends (EMA-Lage + DMI-Richtung + ADX-Stärke).</item>
/// <item><b>Market-Entry zum Close:</b> Kein Limit in einer Korrektur-Zone. Der geplante Entry-Preis
///   ist exakt der Markt-Close — dadurch stimmt die SL/TP-Geometrie im Backtest und im Live-Betrieb
///   ueberein (winziger Backtest-Live-Gap, im Gegensatz zum SK-Limit-System).</item>
/// <item><b>Weiter ATR-SL:</b> SL = N×ATR statt enger 0,3 %-Clamp → Markt-Rauschen stoppt nicht aus.</item>
/// <item><b>Hohes RRR:</b> TP1 bei 1,5R (50 %), TP2 bei 3R (Rest). Auch bei &lt;50 % WinRate profitabel.</item>
/// </list>
///
/// Bewusst parameterarm (Overfitting-Schutz): Donchian-Periode, EMA-Trendfilter, ATR-SL-Multiplikator,
/// ADX-Mindeststärke, zwei RRR-Stufen. Zustandslos — jede Evaluation rechnet frisch auf den Kerzen.
/// </summary>
public sealed class TrendFollowStrategy : IStrategy
{
    private readonly int _donchianPeriod;
    private readonly int _emaPeriod;
    private readonly int _atrPeriod;
    private readonly int _adxPeriod;
    private readonly decimal _adxMin;
    private readonly decimal _atrSlMultiplier;
    private readonly decimal _tp1Rrr;
    private readonly decimal _tp2Rrr;

    public TrendFollowStrategy(
        int donchianPeriod = 20,
        int emaPeriod = 50,
        int atrPeriod = 14,
        int adxPeriod = 14,
        decimal adxMin = 20m,
        decimal atrSlMultiplier = 2.5m,
        decimal tp1Rrr = 1.5m,
        decimal tp2Rrr = 3.0m)
    {
        _donchianPeriod = donchianPeriod;
        _emaPeriod = emaPeriod;
        _atrPeriod = atrPeriod;
        _adxPeriod = adxPeriod;
        _adxMin = adxMin;
        _atrSlMultiplier = atrSlMultiplier;
        _tp1Rrr = tp1Rrr;
        _tp2Rrr = tp2Rrr;
    }

    public string Name => "TrendFollow";
    public string Description => "Donchian-Breakout in Trend-Richtung (EMA+ADX/DMI-Filter), ATR-SL, RRR 1.5/3.0";
    public IReadOnlyList<StrategyParameter> Parameters => [];

    public SignalResult Evaluate(MarketContext context)
    {
        var c = context.Candles;
        int minBars = Math.Max(_emaPeriod, Math.Max(_donchianPeriod, _adxPeriod * 3)) + 5;
        if (c.Count < minBars) return None("insufficient_data");

        int i = c.Count - 1;

        // Eine offene Position pro Symbol — kein Nachladen im selben Trend (Re-Entry erst nach Exit).
        if (context.OpenPositions.Any(p => string.Equals(p.Symbol, context.Symbol, StringComparison.OrdinalIgnoreCase)))
            return None("position_open");

        var ema = IndicatorHelper.CalculateEma(c, _emaPeriod);
        var atr = IndicatorHelper.CalculateAtr(c, _atrPeriod);
        var (adx, pdi, mdi) = IndicatorHelper.CalculateAdxWithDi(c, _adxPeriod);
        var (donUp, donLo, _) = IndicatorHelper.CalculateDonchian(c, _donchianPeriod);

        if (!ema[i].HasValue || !atr[i].HasValue || atr[i]!.Value <= 0m
            || !adx[i].HasValue || !pdi[i].HasValue || !mdi[i].HasValue
            || !donUp[i - 1].HasValue || !donLo[i - 1].HasValue)
            return None("insufficient_data");

        var close = c[i].Close;
        var prevClose = c[i - 1].Close;
        var atrV = atr[i]!.Value;
        var emaV = ema[i]!.Value;
        var adxV = adx[i]!.Value;
        var pdiV = pdi[i]!.Value;
        var mdiV = mdi[i]!.Value;
        var upPrev = donUp[i - 1]!.Value; // hoechstes Hoch der N Kerzen VOR der aktuellen
        var loPrev = donLo[i - 1]!.Value;

        bool strongTrend = adxV >= _adxMin;

        // Echter Breakout-Crossover: vorherige Kerze noch im Kanal, aktuelle schliesst darueber/darunter.
        bool breakoutUp = prevClose <= upPrev && close > upPrev;
        bool breakoutDown = prevClose >= loPrev && close < loPrev;

        bool trendUp = close > emaV && pdiV > mdiV;
        bool trendDown = close < emaV && mdiV > pdiV;

        if (strongTrend && breakoutUp && trendUp)
        {
            var sl = close - _atrSlMultiplier * atrV;
            var risk = close - sl;
            if (risk <= 0m) return None("sl_geometry_error");
            var tp1 = close + _tp1Rrr * risk;
            var tp2 = close + _tp2Rrr * risk;
            return new SignalResult(Signal.Long, Confidence(adxV), close, sl, tp1,
                $"TrendFollow Long (ADX {adxV:F0}, Donchian-Breakout > {upPrev:F4})",
                TakeProfit2: tp2, ConfluenceScore: 5, PreferLimitOrder: false, EntryAtr: atrV);
        }

        if (strongTrend && breakoutDown && trendDown)
        {
            var sl = close + _atrSlMultiplier * atrV;
            var risk = sl - close;
            if (risk <= 0m) return None("sl_geometry_error");
            var tp1 = close - _tp1Rrr * risk;
            var tp2 = close - _tp2Rrr * risk;
            return new SignalResult(Signal.Short, Confidence(adxV), close, sl, tp1,
                $"TrendFollow Short (ADX {adxV:F0}, Donchian-Breakout < {loPrev:F4})",
                TakeProfit2: tp2, ConfluenceScore: 5, PreferLimitOrder: false, EntryAtr: atrV);
        }

        return None("no_signal");
    }

    private static decimal Confidence(decimal adx) => Math.Min(1m, Math.Max(0.3m, adx / 50m));
    private static SignalResult None(string reason) => new(Signal.None, 0m, null, null, null, reason);

    public void WarmUp(IReadOnlyList<Candle> history) { }
    public void Reset() { }

    public IStrategy Clone() => new TrendFollowStrategy(
        _donchianPeriod, _emaPeriod, _atrPeriod, _adxPeriod, _adxMin, _atrSlMultiplier, _tp1Rrr, _tp2Rrr);
}
