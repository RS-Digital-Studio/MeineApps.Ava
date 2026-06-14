using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Mean-Reversion-Strategie: Fade von Bollinger-Band-Extremen IM Range-Regime, gegen den
/// nicht-organischen Order-Flow zwangsliquidierter ueberhebelter Retail-Trader.
///
/// Oekonomische Gegenseite (im Gegensatz zu reinem Trend-Following ohne nachweisbaren Edge):
/// <list type="bullet">
/// <item><b>Range-Regime-Gate (PFLICHT):</b> nur handeln, wenn ADX &lt; <c>adxMax</c> (kein Trend).
///   SSRN-belegt: ohne dieses Gate ist der Erwartungswert von Band-Fades NEGATIV (in starken
///   Trends laeuft der Preis durch das Band hindurch — der Fade wird ueberrollt).</item>
/// <item><b>Crowd-Fade:</b> Long, wenn der Preis UNTER das untere Band faellt UND RSI &lt; <c>rsiLow</c>
///   (Crowd panik-verkauft); Short, wenn der Preis UEBER das obere Band steigt UND RSI &gt; <c>rsiHigh</c>
///   (Crowd FOMO-kauft). Der nicht-organische Flush kehrt statistisch zur Mitte zurueck.</item>
/// <item><b>Enges statistisches Mean-Ziel:</b> TP1 = SMA20 (Mittel-Band), TP2 = gegenueberliegendes
///   Band (Rest laeuft auf das Durchschwingen). SL = N×ATR JENSEITS des Extrems (Markt-Rauschen
///   stoppt nicht aus, aber ein echter Trend-Bruch des Bands beendet den Trade).</item>
/// <item><b>Market-Entry zum Close:</b> bewusst KEIN Limit-Maker-Entry am Extrem — der waere im
///   SimulatedExchange nicht live-treu fillbar (die SK-Falle: Backtest 48 % WR → live 12 % WR).
///   Market-Close ist die pessimistische, ehrliche Untergrenze.</item>
/// </list>
///
/// Bewusst parameterarm + auf Standard-Werten festgenagelt (Overfitting-Schutz: Standard-Params
/// schlagen "optimierte" OOS, SSRN). Zustandslos — jede Evaluation rechnet frisch auf den Kerzen.
/// Reine H4-pro-Symbol-Strategie: kein W1/D1-Fahrplan noetig.
/// </summary>
public sealed class MeanReversionStrategy : IStrategy
{
    private readonly int _bbPeriod;
    private readonly decimal _bbStdDev;
    private readonly int _rsiPeriod;
    private readonly decimal _rsiLow;
    private readonly decimal _rsiHigh;
    private readonly decimal _adxMax;
    private readonly decimal _atrSlMult;
    private readonly int _atrPeriod;
    private readonly int _adxPeriod;

    public MeanReversionStrategy(
        int bbPeriod = 20,
        decimal bbStdDev = 2.0m,
        int rsiPeriod = 14,
        decimal rsiLow = 30m,
        decimal rsiHigh = 70m,
        // HARTES Regime-Gate: nur Range (ADX < adxMax). Ohne dieses Gate ist der Erwartungswert negativ.
        decimal adxMax = 20m,
        // SL = N×ATR jenseits des Extrems (weiter als das Band, damit Rauschen nicht ausstoppt).
        decimal atrSlMult = 1.5m,
        int atrPeriod = 14,
        int adxPeriod = 14)
    {
        _bbPeriod = bbPeriod;
        _bbStdDev = bbStdDev;
        _rsiPeriod = rsiPeriod;
        _rsiLow = rsiLow;
        _rsiHigh = rsiHigh;
        _adxMax = adxMax;
        _atrSlMult = atrSlMult;
        _atrPeriod = atrPeriod;
        _adxPeriod = adxPeriod;
    }

    public string Name => "MeanReversion";
    public string Description => "Bollinger-Band-Fade im Range-Regime (ADX<20, RSI-Extrem), TP=SMA20/Gegen-Band, ATR-SL";
    public IReadOnlyList<StrategyParameter> Parameters => [];

    // Reine H4-Mean-Reversion pro Symbol — kein W1/D1-Fahrplan noetig (erspart dem Scan die
    // schweren Higher-TF-Klines-Fetches, wie TrendFollow).
    public bool RequiresHigherTimeframeContext => false;

    public SignalResult Evaluate(MarketContext context)
    {
        var c = context.Candles;
        int minBars = Math.Max(_bbPeriod, Math.Max(_rsiPeriod, _adxPeriod * 3)) + 5;
        if (c.Count < minBars) return None("insufficient_data");

        int i = c.Count - 1;

        // Eine offene Position pro Symbol — kein Nachladen im selben Fade (Re-Entry erst nach Exit).
        if (context.OpenPositions.Any(p => string.Equals(p.Symbol, context.Symbol, StringComparison.OrdinalIgnoreCase)))
            return None("position_open");

        var (upper, middle, lower) = IndicatorHelper.CalculateBollinger(c, _bbPeriod, _bbStdDev);
        var rsi = IndicatorHelper.CalculateRsi(c, _rsiPeriod);
        var (adx, _, _) = IndicatorHelper.CalculateAdxWithDi(c, _adxPeriod);
        var atr = IndicatorHelper.CalculateAtr(c, _atrPeriod);

        if (!upper[i].HasValue || !middle[i].HasValue || !lower[i].HasValue
            || !rsi[i].HasValue || !adx[i].HasValue
            || !atr[i].HasValue || atr[i]!.Value <= 0m)
            return None("insufficient_data");

        var close = c[i].Close;
        var upperV = upper[i]!.Value;
        var middleV = middle[i]!.Value;
        var lowerV = lower[i]!.Value;
        var rsiV = rsi[i]!.Value;
        var adxV = adx[i]!.Value;
        var atrV = atr[i]!.Value;

        // HARTES Regime-Gate: nur im Range-Regime faden (ADX unter Schwelle = kein etablierter Trend).
        // In einem Trend laeuft der Preis durch das Band — der Fade wird ueberrollt (negativer EV).
        if (adxV >= _adxMax) return None("trend_regime");

        // Long-Fade: Crowd panik-verkauft unter das untere Band + RSI ueberverkauft.
        bool longFade = close < lowerV && rsiV < _rsiLow;
        // Short-Fade: Crowd FOMO-kauft ueber das obere Band + RSI ueberkauft.
        bool shortFade = close > upperV && rsiV > _rsiHigh;

        if (longFade)
        {
            // SL JENSEITS des Extrems (unter dem unteren Band): entry - N×ATR. Ein echter Trend-Bruch
            // des Bands beendet den Trade; reines Rauschen am Band stoppt nicht aus.
            var sl = close - _atrSlMult * atrV;
            var risk = close - sl;
            if (risk <= 0m) return None("sl_geometry_error");

            // TP1 = SMA20 (enges statistisches Mean-Ziel). Da close < lowerV < middleV ist middleV > close
            // → TP auf der richtigen Seite. TP2 = oberes Band (Rest laeuft auf das Durchschwingen).
            var tp1 = middleV;
            var tp2 = upperV;
            if (tp1 <= close) return None("tp_geometry_error");

            // DisableSmartBreakeven=true aktiviert (historisch invertiert benannt) den A-Bruch/2x-SL-BE
            // im Live-/Backtest-Exit-Pfad — analog TrendFollow gesetzt, damit der Rest nach TP1 nicht
            // ungeschuetzt laeuft. VORBEHALT: MR-Trades sind kurz mit engem TP1=SMA20; ein BE-Trigger bei
            // 2R koennte VOR dem Mean-Ziel greifen und den Trade vorzeitig auf BE schliessen statt das
            // Mean ausspielen zu lassen. Falls MR den Test besteht, ist DisableSmartBreakeven=false
            // (kein 2R-BE) als A/B-Variante gegen diese Geometrie zu pruefen.
            return new SignalResult(Signal.Long, Confidence(adxV, rsiV, _rsiLow), close, sl, tp1,
                $"MeanReversion Long-Fade (ADX {adxV:F0} < {_adxMax:F0}, RSI {rsiV:F0}, Close < Lower {lowerV:F4})",
                TakeProfit2: tp2, ConfluenceScore: 5, EntryAtr: atrV,
                DisableSmartBreakeven: true);
        }

        if (shortFade)
        {
            var sl = close + _atrSlMult * atrV;
            var risk = sl - close;
            if (risk <= 0m) return None("sl_geometry_error");

            // TP1 = SMA20 (close > upperV > middleV → middleV < close → TP unter dem Entry, korrekt fuer Short).
            // TP2 = unteres Band.
            var tp1 = middleV;
            var tp2 = lowerV;
            if (tp1 >= close) return None("tp_geometry_error");

            // Siehe Long-Pfad (gleicher BE-Vorbehalt).
            return new SignalResult(Signal.Short, Confidence(adxV, rsiV, _rsiHigh), close, sl, tp1,
                $"MeanReversion Short-Fade (ADX {adxV:F0} < {_adxMax:F0}, RSI {rsiV:F0}, Close > Upper {upperV:F4})",
                TakeProfit2: tp2, ConfluenceScore: 5, EntryAtr: atrV,
                DisableSmartBreakeven: true);
        }

        return None("no_signal");
    }

    // Confidence aus Range-Klarheit (je niedriger ADX, desto reiner die Range) + RSI-Extremitaet.
    private decimal Confidence(decimal adx, decimal rsi, decimal rsiThreshold)
    {
        var rangeScore = Math.Clamp((_adxMax - adx) / _adxMax, 0m, 1m);
        var rsiScore = Math.Clamp(Math.Abs(rsi - rsiThreshold) / 20m, 0m, 1m);
        return Math.Min(1m, Math.Max(0.3m, (rangeScore + rsiScore) / 2m));
    }

    private static SignalResult None(string reason) => new(Signal.None, 0m, null, null, null, reason);

    public void WarmUp(IReadOnlyList<Candle> history) { }
    public void Reset() { }

    public IStrategy Clone() => new MeanReversionStrategy(
        _bbPeriod, _bbStdDev, _rsiPeriod, _rsiLow, _rsiHigh, _adxMax, _atrSlMult, _atrPeriod, _adxPeriod);
}
