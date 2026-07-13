using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>Entry-Modus der <see cref="LevelStrategy"/>.</summary>
public enum LevelEntryMode
{
    /// <summary>Abpraller an einem etablierten Level (Support hält / Resistance hält).</summary>
    Bounce,
    /// <summary>Retest eines frisch gebrochenen Levels (Role-Reversal: Resistance wird Support).</summary>
    Retest,
}

/// <summary>
/// Level-Strategie: Handel an horizontalen Support/Resistance-Levels aus geclusterten Swing-Pivots
/// (Lab-Falsifikationsexperiment, NICHT produktiv — analog MeanReversion).
///
/// <list type="bullet">
/// <item><b>Level-Erkennung:</b> Fractal-Swing-Pivots (High/Low strikt extremer als N Kerzen links UND
///   rechts). Nur BESTÄTIGTE Pivots (N Folgekerzen liegen bereits vor) — kein Look-Ahead. Highs und
///   Lows werden GEMEINSAM geclustert (Role-Reversal-Levels, die beide Rollen hatten, sind stärker);
///   Cluster-Toleranz in ATR. Relevant ab <c>minTouches</c> Berührungen.</item>
/// <item><b>Bounce:</b> Kerze testet das nächste Level unter dem Close (Low ≤ Level + Toleranz), war
///   davor über dem Level (kein Test von unten) und schließt in der oberen Kerzenhälfte (Ablehnung)
///   → Long. Spiegelbildlich Short am nächsten Level über dem Close.</item>
/// <item><b>Retest:</b> Innerhalb der letzten <c>retestWindow</c> Kerzen gab es einen frischen
///   Breakout-Close über das Level (Vorkerze noch darunter); die aktuelle Kerze testet das Level von
///   oben und hält (Role-Reversal bestätigt) → Long. Spiegelbildlich Short.</item>
/// <item><b>Market-Entry zum Close:</b> bewusst KEIN Limit am Level — der wäre im Backtest nicht
///   live-treu fillbar (die SK-Falle: Backtest 48 % WR → live 12 % WR). Market-Close ist die
///   pessimistische, ehrliche Untergrenze.</item>
/// <item><b>SL hinter dem LEVEL</b> (nicht hinter dem Close): die These „Level hält" ist genau dann
///   falsifiziert, wenn das Level klar bricht — N×ATR Puffer gegen Docht-Rauschen.</item>
/// <item><b>TP an Levels:</b> TP1 = nächstes Gegen-Level mit ≥ 1R Abstand, TP2 = nächstes mit ≥ 2R.
///   Ohne passendes Level greift der RRR-Fallback (1.5R / 3R). Öffnet UND schließt an Levels.</item>
/// </list>
///
/// Bewusst parameterarm + auf plausiblen Standard-Werten festgenagelt (Overfitting-Schutz).
/// Zustandslos — jede Evaluation rechnet frisch auf den Kerzen. Reine Navigator-TF-Strategie.
/// </summary>
public sealed class LevelStrategy : IStrategy
{
    private readonly LevelEntryMode _mode;
    private readonly int _swingStrength;
    private readonly int _lookback;
    private readonly decimal _clusterTolAtr;
    private readonly decimal _touchTolAtr;
    private readonly int _minTouches;
    private readonly decimal _atrSlMult;
    private readonly int _emaTrendPeriod;
    private readonly int _retestWindow;
    private readonly decimal _tp1FallbackRrr;
    private readonly decimal _tp2FallbackRrr;
    private readonly int _atrPeriod;

    public LevelStrategy(
        LevelEntryMode mode = LevelEntryMode.Bounce,
        // Fractal-Stärke: Pivot muss N Kerzen links UND rechts extremer sein. 3 = mehr (schwächere)
        // Pivots als die Fib-Erkennung (5) — Levels leben vom Clustern mehrerer Touches.
        int swingStrength = 3,
        // Level-Historie in Kerzen. Der Portfolio-Backtest liefert max. 200 Kontext-Kerzen
        // (ContextSlice(200)) — 150 lässt Raum für die Pivot-Bestätigung am Fensterrand.
        int lookback = 150,
        // Pivots näher als N×ATR am Cluster-Mittel bilden EIN Level.
        decimal clusterTolAtr = 0.5m,
        // Wie nah das Kerzen-Extrem dem Level kommen muss, um als Test zu zählen.
        decimal touchTolAtr = 0.25m,
        // Mindest-Berührungen, damit ein Cluster als relevantes Level gilt.
        int minTouches = 2,
        // SL = Level ∓ N×ATR (hinter dem Level, nicht hinter dem Entry).
        decimal atrSlMult = 1.5m,
        // 0 = kein Trendfilter. >0: Bounce-Long nur über der EMA, Bounce-Short nur darunter.
        int emaTrendPeriod = 0,
        // Retest: der Breakout-Close muss innerhalb der letzten N Kerzen liegen.
        int retestWindow = 10,
        decimal tp1FallbackRrr = 1.5m,
        decimal tp2FallbackRrr = 3.0m,
        int atrPeriod = 14)
    {
        _mode = mode;
        _swingStrength = swingStrength;
        _lookback = lookback;
        _clusterTolAtr = clusterTolAtr;
        _touchTolAtr = touchTolAtr;
        _minTouches = minTouches;
        _atrSlMult = atrSlMult;
        _emaTrendPeriod = emaTrendPeriod;
        _retestWindow = retestWindow;
        _tp1FallbackRrr = tp1FallbackRrr;
        _tp2FallbackRrr = tp2FallbackRrr;
        _atrPeriod = atrPeriod;
    }

    public string Name => _mode == LevelEntryMode.Retest
        ? "Level-Retest"
        : _emaTrendPeriod > 0 ? "Level-Bounce-Trend" : "Level-Bounce";

    public string Description => _mode == LevelEntryMode.Retest
        ? "Retest frisch gebrochener S/R-Levels (Role-Reversal), SL hinter dem Level, TP an Gegen-Levels"
        : "Bounce an geclusterten S/R-Levels (≥2 Touches), SL hinter dem Level, TP an Gegen-Levels";

    public IReadOnlyList<StrategyParameter> Parameters => [];

    // Reine Navigator-TF-Strategie — kein W1/D1-Fahrplan nötig (spart die schweren
    // Higher-TF-Klines-Fetches im Scan, wie TrendFollow/MeanReversion).
    public bool RequiresHigherTimeframeContext => false;

    /// <summary>Ein geclustertes horizontales Level: Preis (Cluster-Mittel) + Berührungs-Anzahl.</summary>
    private readonly record struct PriceLevel(decimal Price, int Touches);

    public SignalResult Evaluate(MarketContext context)
    {
        var c = context.Candles;
        // Mindest-Historie: ATR-Einschwingzeit + genug Kerzen für mindestens einige bestätigte Pivots.
        int minBars = Math.Max(Math.Max(_atrPeriod * 3, _emaTrendPeriod), _swingStrength * 2 + 20) + 5;
        if (c.Count < minBars) return None("insufficient_data");

        int i = c.Count - 1;

        // Eine offene Position pro Symbol — kein Nachladen am selben Level (Re-Entry erst nach Exit).
        if (context.OpenPositions.Any(p => string.Equals(p.Symbol, context.Symbol, StringComparison.OrdinalIgnoreCase)))
            return None("position_open");

        var atr = IndicatorHelper.CalculateAtr(c, _atrPeriod);
        if (!atr[i].HasValue || atr[i]!.Value <= 0m) return None("insufficient_data");
        var atrV = atr[i]!.Value;

        decimal? emaV = null;
        if (_emaTrendPeriod > 0)
        {
            var ema = IndicatorHelper.CalculateEma(c, _emaTrendPeriod);
            if (!ema[i].HasValue) return None("insufficient_data");
            emaV = ema[i]!.Value;
        }

        var levels = BuildLevels(c, atrV);
        if (levels.Count == 0) return None("no_levels");

        var candle = c[i];
        var close = candle.Close;
        var prevClose = c[i - 1].Close;
        var touchTol = _touchTolAtr * atrV;
        var mid = (candle.High + candle.Low) / 2m;

        // ---- Long am nächsten Level UNTER dem Close ------------------------------------------
        var support = levels.Where(l => l.Price < close).OrderByDescending(l => l.Price).Cast<PriceLevel?>().FirstOrDefault();
        if (support.HasValue)
        {
            var lv = support.Value;
            bool touched = candle.Low <= lv.Price + touchTol;
            // Ablehnung: Kerze schließt in der oberen Hälfte (Hammer/Bullish-Kerze), Level hat gehalten.
            bool rejected = close >= mid;
            bool setup = _mode == LevelEntryMode.Bounce
                // Bounce: der Preis kam von OBEN (Vorkerze über dem Level) — kein Test von unten.
                ? prevClose > lv.Price
                // Retest: frischer Breakout-Close über das Level innerhalb des Fensters (Role-Reversal).
                : HasFreshBreakout(c, i, lv.Price, up: true);
            bool trendOk = emaV is null || close > emaV.Value;

            if (touched && rejected && setup && trendOk)
            {
                var sl = lv.Price - _atrSlMult * atrV;
                var risk = close - sl;
                if (risk > 0m)
                {
                    var (tp1, tp2) = PickTakeProfits(levels, close, risk, up: true);
                    return new SignalResult(Signal.Long, Confidence(lv.Touches), close, sl, tp1,
                        $"{Name} Long (Level {lv.Price:F4}, {lv.Touches} Touches)",
                        TakeProfit2: tp2, ConfluenceScore: 5, EntryAtr: atrV,
                        // Aktiviert (historisch invertiert benannt) den A-Bruch/2x-SL-Break-Even —
                        // wie TrendFollow, damit der Rest nach TP1 nicht ungeschützt läuft.
                        DisableSmartBreakeven: true);
                }
            }
        }

        // ---- Short am nächsten Level ÜBER dem Close ------------------------------------------
        var resistance = levels.Where(l => l.Price > close).OrderBy(l => l.Price).Cast<PriceLevel?>().FirstOrDefault();
        if (resistance.HasValue)
        {
            var lv = resistance.Value;
            bool touched = candle.High >= lv.Price - touchTol;
            bool rejected = close <= mid;
            bool setup = _mode == LevelEntryMode.Bounce
                ? prevClose < lv.Price
                : HasFreshBreakout(c, i, lv.Price, up: false);
            bool trendOk = emaV is null || close < emaV.Value;

            if (touched && rejected && setup && trendOk)
            {
                var sl = lv.Price + _atrSlMult * atrV;
                var risk = sl - close;
                if (risk > 0m)
                {
                    var (tp1, tp2) = PickTakeProfits(levels, close, risk, up: false);
                    return new SignalResult(Signal.Short, Confidence(lv.Touches), close, sl, tp1,
                        $"{Name} Short (Level {lv.Price:F4}, {lv.Touches} Touches)",
                        TakeProfit2: tp2, ConfluenceScore: 5, EntryAtr: atrV,
                        // Siehe Long-Pfad.
                        DisableSmartBreakeven: true);
                }
            }
        }

        return None("no_signal");
    }

    /// <summary>
    /// Sammelt bestätigte Fractal-Pivots im Lookback-Fenster und clustert sie zu horizontalen Levels.
    /// Nur Pivots mit <see cref="_swingStrength"/> bereits VORLIEGENDEN Folgekerzen (kein Look-Ahead).
    /// </summary>
    private List<PriceLevel> BuildLevels(IReadOnlyList<Candle> c, decimal atrV)
    {
        int last = c.Count - 1;
        int k = _swingStrength;
        int start = Math.Max(k, last - _lookback);
        int end = last - k; // Pivot braucht k Folgekerzen zur Bestätigung — die Signal-Kerze ist nie Pivot.

        List<decimal> pivots = [];
        for (int i0 = start; i0 <= end; i0++)
        {
            bool isHigh = true, isLow = true;
            var hi = c[i0].High;
            var lo = c[i0].Low;
            for (int j = 1; j <= k && (isHigh || isLow); j++)
            {
                if (c[i0 - j].High >= hi || c[i0 + j].High >= hi) isHigh = false;
                if (c[i0 - j].Low <= lo || c[i0 + j].Low <= lo) isLow = false;
            }
            if (isHigh) pivots.Add(hi);
            if (isLow) pivots.Add(lo);
        }
        if (pivots.Count == 0) return [];

        // Preis-sortiert greedy clustern: Pivot gehört zum Cluster, solange er nah am Cluster-Mittel liegt.
        pivots.Sort();
        var tol = _clusterTolAtr * atrV;
        List<PriceLevel> levels = [];
        decimal sum = pivots[0];
        int count = 1;
        for (int p = 1; p < pivots.Count; p++)
        {
            var avg = sum / count;
            if (pivots[p] - avg <= tol) { sum += pivots[p]; count++; }
            else
            {
                if (count >= _minTouches) levels.Add(new PriceLevel(avg, count));
                sum = pivots[p]; count = 1;
            }
        }
        var lastAvg = sum / count;
        if (count >= _minTouches) levels.Add(new PriceLevel(lastAvg, count));
        return levels;
    }

    /// <summary>
    /// Retest-Vorbedingung: Innerhalb der letzten <see cref="_retestWindow"/> Kerzen (vor der aktuellen)
    /// gab es einen frischen Breakout-Close über/unter das Level (Vorkerze noch auf der alten Seite).
    /// </summary>
    private bool HasFreshBreakout(IReadOnlyList<Candle> c, int i, decimal level, bool up)
    {
        int from = Math.Max(1, i - _retestWindow);
        for (int j = from; j < i; j++)
        {
            bool crossed = up
                ? c[j].Close > level && c[j - 1].Close <= level
                : c[j].Close < level && c[j - 1].Close >= level;
            if (crossed) return true;
        }
        return false;
    }

    /// <summary>
    /// TP1 = nächstes Gegen-Level mit ≥ 1R Abstand, TP2 = nächstes darüber/darunter mit ≥ 2R.
    /// Ohne passendes Level: RRR-Fallback. TP2 liegt immer jenseits von TP1.
    /// </summary>
    private (decimal Tp1, decimal Tp2) PickTakeProfits(List<PriceLevel> levels, decimal close, decimal risk, bool up)
    {
        var targets = up
            ? levels.Where(l => l.Price > close).OrderBy(l => l.Price).Select(l => l.Price)
            : levels.Where(l => l.Price < close).OrderByDescending(l => l.Price).Select(l => l.Price);

        decimal? tp1 = null, tp2 = null;
        foreach (var price in targets)
        {
            var reward = Math.Abs(price - close);
            if (tp1 is null && reward >= 1.0m * risk) { tp1 = price; continue; }
            if (tp1 is not null && reward >= 2.0m * risk) { tp2 = price; break; }
        }

        var dir = up ? 1m : -1m;
        var t1 = tp1 ?? close + dir * _tp1FallbackRrr * risk;
        var t2 = tp2 ?? close + dir * _tp2FallbackRrr * risk;
        // Geometrie absichern: TP2 muss jenseits von TP1 liegen (Level-TP1 + Fallback-TP2 können kollidieren).
        if (up && t2 <= t1) t2 = t1 + (t1 - close);
        if (!up && t2 >= t1) t2 = t1 - (close - t1);
        return (t1, t2);
    }

    // Confidence aus Level-Stärke: mehr Touches = stärkeres Level (0.45 bei 2 Touches, +0.15 je weiterer).
    private static decimal Confidence(int touches) => Math.Min(1m, 0.15m + 0.15m * touches);

    private static SignalResult None(string reason) => new(Signal.None, 0m, null, null, null, reason);

    public void WarmUp(IReadOnlyList<Candle> history) { }
    public void Reset() { }

    public IStrategy Clone() => new LevelStrategy(
        _mode, _swingStrength, _lookback, _clusterTolAtr, _touchTolAtr, _minTouches,
        _atrSlMult, _emaTrendPeriod, _retestWindow, _tp1FallbackRrr, _tp2FallbackRrr, _atrPeriod);
}
