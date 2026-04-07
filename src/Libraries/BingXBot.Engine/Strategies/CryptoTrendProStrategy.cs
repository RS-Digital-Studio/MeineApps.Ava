using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// CryptoTrendPro v3 - Optimiert für Krypto-Futures 2024-2026.
///
/// Kernprinzip: Wenige Trades mit hoher Überzeugung. Confluence-Scoring statt binäre Bedingungen.
/// Supertrend als Primärsignal (weniger Whipsaws als EMA-Cross), volatilitäts-adaptive SL/TP.
///
/// Entry (Long): Score >= 8 von 12 Punkten:
///   +2  D1 Preis > EMA 50 (mittelfristiger Uptrend)
///   +2  H4 Supertrend(10, 3.0) bullish
///   +1  H4 EMA 12 > EMA 26 (Trend-Bestätigung)
///   +1  H4 ADX > 20 UND steigend (+DI > -DI)
///   +1  H4 RSI 45-80 (Momentum, nicht extrem)
///   +1  H4 Volumen > 1.5x SMA(20) (institutionelles Interesse)
///   +2  BTC-Kontext (extern via MarketFilter, wird als HTF-Candles übergeben)
///   +1  Funding-Rate günstig (extern via MarketContext)
///   +1  Cooldown respektiert (kein Verlust in letzten 8h, extern)
///
/// Exit: Multi-Stage (TP1 30% → Smart-BE → TP2 30% → Chandelier-Trailing 40% / Regime-Exit)
/// SL/TP: Volatilitäts-adaptiv basierend auf ATR-Perzentil (engere Stops in ruhigen, weitere in volatilen Phasen)
/// Max erreichbarer Score: ~10 (Funding + Cooldown werden extern geprüft, nicht im Score)
/// </summary>
public class CryptoTrendProStrategy : IStrategy
{
    public string Name => "CryptoTrendPro";
    public string Description => "Krypto-Futures Strategie: Supertrend + Confluence-Scoring + vol-adaptive SL/TP + Multi-Stage Exit";

    // Supertrend-Parameter
    private int _supertrendPeriod = 10;
    private decimal _supertrendMultiplier = 3.0m;

    // EMA-Parameter
    private int _emaFast = 12;
    private int _emaSlow = 26;
    private int _emaTrendFilter = 50; // Auf D1 (via HTF-Candles)

    // ADX-Parameter
    private int _adxPeriod = 14;
    private decimal _minAdx = 18m;

    // RSI-Parameter
    private int _rsiPeriod = 14;
    private decimal _rsiLongMin = 40m;
    private decimal _rsiLongMax = 75m;
    private decimal _rsiShortMin = 25m;
    private decimal _rsiShortMax = 60m;

    // Volumen-Parameter
    private int _volumePeriod = 20;
    private decimal _volumeMultiplier = 1.0m;

    // ATR / Exit-Parameter
    private int _atrPeriod = 14;
    private int _atrPercentileLookback = 100;

    // Confluence
    private int _minScore = 6;

    // Aktiver Modus (für vol-adaptive Multiplikatoren)
    private TradingModePreset _activePreset = TradingModePreset.Swing;

    /// <summary>Aktueller Trading-Modus.</summary>
    public TradingModePreset ActivePreset => _activePreset;

    /// <summary>
    /// Wendet ein Trading-Mode-Preset auf alle Parameter an.
    /// Danach sind die Felder für den gewählten Modus optimiert.
    /// </summary>
    public void ApplyPreset(TradingModePreset mode)
    {
        _activePreset = mode;
        if (mode == TradingModePreset.Custom) return;

        var p = TradingModeDefaults.GetStrategyPreset(mode);
        _supertrendPeriod = p.SupertrendPeriod;
        _supertrendMultiplier = p.SupertrendMultiplier;
        _emaFast = p.EmaFast;
        _emaSlow = p.EmaSlow;
        _emaTrendFilter = p.EmaTrendFilter;
        _adxPeriod = p.AdxPeriod;
        _minAdx = p.MinAdx;
        _rsiPeriod = p.RsiPeriod;
        _rsiLongMin = p.RsiLongMin;
        _rsiLongMax = p.RsiLongMax;
        _rsiShortMin = p.RsiShortMin;
        _rsiShortMax = p.RsiShortMax;
        _volumePeriod = p.VolumePeriod;
        _volumeMultiplier = p.VolumeMultiplier;
        _atrPeriod = p.AtrPeriod;
        _atrPercentileLookback = p.AtrPercentileLookback;
        _minScore = p.MinScore;
    }

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("SupertrendPeriod", "Supertrend Periode", "int", _supertrendPeriod, 5, 20, 1),
        new("SupertrendMultiplier", "Supertrend ATR-Multiplikator", "decimal", _supertrendMultiplier, 1.5m, 5.0m, 0.5m),
        new("EmaFast", "Schnelle EMA Periode", "int", _emaFast, 8, 20, 1),
        new("EmaSlow", "Langsame EMA Periode", "int", _emaSlow, 20, 50, 1),
        new("EmaTrendFilter", "Trend-Filter EMA (auf D1/HTF)", "int", _emaTrendFilter, 20, 100, 5),
        new("AdxPeriod", "ADX Periode", "int", _adxPeriod, 7, 30, 1),
        new("MinAdx", "Min. ADX für Trend (20=Standard)", "decimal", _minAdx, 15m, 30m, 5m),
        new("RsiPeriod", "RSI Periode", "int", _rsiPeriod, 7, 21, 1),
        new("VolumePeriod", "Volumen-SMA Periode", "int", _volumePeriod, 10, 50, 5),
        new("VolumeMultiplier", "Volumen-Schwelle (x Durchschnitt)", "decimal", _volumeMultiplier, 1.0m, 3.0m, 0.1m),
        new("AtrPeriod", "ATR Periode (SL/TP-Berechnung)", "int", _atrPeriod, 7, 21, 1),
        new("MinScore", "Min. Confluence-Score (8-12)", "int", _minScore, 6, 12, 1),
    };

    public SignalResult Evaluate(MarketContext context)
    {
        var candles = context.Candles;
        if (candles.Count < Math.Max(_emaSlow, _supertrendPeriod) + 20)
            return NoSignal("Zu wenig Daten");

        var currentPrice = context.CurrentTicker.LastPrice;

        // ═══════════════════════════════════════════════════════════
        // Indikatoren berechnen
        // ═══════════════════════════════════════════════════════════

        var (stValue, stBullish) = IndicatorHelper.CalculateSupertrend(candles, _supertrendPeriod, _supertrendMultiplier);
        var emaFast = IndicatorHelper.CalculateEma(candles, _emaFast);
        var emaSlow = IndicatorHelper.CalculateEma(candles, _emaSlow);
        var (adxValues, pdi, mdi) = IndicatorHelper.CalculateAdxWithDi(candles, _adxPeriod);
        var rsi = IndicatorHelper.CalculateRsi(candles, _rsiPeriod);
        var atr = IndicatorHelper.CalculateAtr(candles, _atrPeriod);
        var volumeSma = IndicatorHelper.CalculateVolumeSma(candles, _volumePeriod);

        // Letzte Werte extrahieren
        var lastStBullish = stBullish[^1];
        var prevStBullish = stBullish.Count >= 2 ? stBullish[^2] : null;
        var lastEmaFast = emaFast[^1];
        var lastEmaSlow = emaSlow[^1];
        var lastAdx = adxValues[^1];
        var prevAdx = adxValues.Count >= 2 ? adxValues[^2] : null;
        var lastPdi = pdi[^1];
        var lastMdi = mdi[^1];
        var lastRsi = rsi[^1];
        var lastAtr = atr[^1];
        var lastVolSma = volumeSma[^1];

        if (lastStBullish == null || lastEmaFast == null || lastEmaSlow == null ||
            lastAdx == null || lastRsi == null || lastAtr == null || lastVolSma == null ||
            lastPdi == null || lastMdi == null)
            return NoSignal("Indikatoren nicht bereit");

        if (lastAtr.Value <= 0)
            return NoSignal("ATR ist 0 - kein valider SL/TP möglich");

        var currentVolume = candles[^1].Volume;
        var atrValue = lastAtr.Value;
        var adxRising = prevAdx.HasValue && lastAdx.Value > prevAdx.Value;

        // ═══════════════════════════════════════════════════════════
        // Supertrend-Flip Detection (Primärsignal)
        // ═══════════════════════════════════════════════════════════

        var supertrendJustFlippedBullish = prevStBullish.HasValue && !prevStBullish.Value && lastStBullish.Value;
        var supertrendJustFlippedBearish = prevStBullish.HasValue && prevStBullish.Value && !lastStBullish.Value;
        var supertrendIsBullish = lastStBullish.Value;
        var supertrendIsBearish = !lastStBullish.Value;

        // ═══════════════════════════════════════════════════════════
        // Confluence-Scoring: Long
        // ═══════════════════════════════════════════════════════════

        var longScore = 0;
        var longReasons = new List<string>();

        // +2: D1 Trend-Filter (EMA auf HTF-Candles)
        if (context.HigherTimeframeCandles != null && context.HigherTimeframeCandles.Count >= _emaTrendFilter + 5)
        {
            var htfEma = IndicatorHelper.CalculateEma(context.HigherTimeframeCandles, _emaTrendFilter);
            if (htfEma[^1].HasValue && context.HigherTimeframeCandles[^1].Close > htfEma[^1]!.Value)
            {
                longScore += 2;
                longReasons.Add($"D1>EMA{_emaTrendFilter}");
            }
        }

        // +2: H4 Supertrend bullish
        if (supertrendIsBullish)
        {
            longScore += 2;
            longReasons.Add(supertrendJustFlippedBullish ? "ST-Flip↑" : "ST↑");
        }

        // +1: EMA 12 > EMA 26
        if (lastEmaFast.Value > lastEmaSlow.Value)
        {
            longScore += 1;
            longReasons.Add($"EMA{_emaFast}>EMA{_emaSlow}");
        }

        // +1: ADX > 20 UND steigend UND +DI > -DI (bullish Trend)
        if (lastAdx.Value >= _minAdx && adxRising && lastPdi.Value > lastMdi.Value)
        {
            longScore += 1;
            longReasons.Add($"ADX={lastAdx.Value:F0}↑");
        }

        // +1: RSI im bullish-Bereich (45-80)
        if (lastRsi.Value >= _rsiLongMin && lastRsi.Value <= _rsiLongMax)
        {
            longScore += 1;
            longReasons.Add($"RSI={lastRsi.Value:F0}");
        }

        // +1: Volumen über Durchschnitt
        if (currentVolume > lastVolSma.Value * _volumeMultiplier)
        {
            longScore += 1;
            longReasons.Add("Vol>Avg");
        }

        // +2: BTC-Kontext (Higher-Timeframe Supertrend)
        // Wird über HigherTimeframeCandles übergeben (BTC-Candles wenn Alt-Trade, eigene wenn BTC-Trade)
        var htfTrend = IndicatorHelper.GetHigherTimeframeTrend(context.HigherTimeframeCandles, _emaTrendFilter);
        if (htfTrend > 0)
        {
            // Bereits in D1>EMA gezählt, nur Bonus wenn Supertrend auch bullish
            if (context.HigherTimeframeCandles != null && context.HigherTimeframeCandles.Count > _supertrendPeriod + 5)
            {
                var (_, htfStBullish) = IndicatorHelper.CalculateSupertrend(
                    context.HigherTimeframeCandles, _supertrendPeriod, _supertrendMultiplier);
                if (htfStBullish[^1] == true) // HTF-Supertrend-Bonus unabhängig vom aktuellen Score
                {
                    longScore += 1; // Bonus für HTF-Supertrend-Alignment
                    longReasons.Add("HTF-ST↑");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // Confluence-Scoring: Short (analog invertiert)
        // ═══════════════════════════════════════════════════════════

        var shortScore = 0;
        var shortReasons = new List<string>();

        // +2: D1 unter EMA-Filter
        if (context.HigherTimeframeCandles != null && context.HigherTimeframeCandles.Count >= _emaTrendFilter + 5)
        {
            var htfEma = IndicatorHelper.CalculateEma(context.HigherTimeframeCandles, _emaTrendFilter);
            if (htfEma[^1].HasValue && context.HigherTimeframeCandles[^1].Close < htfEma[^1]!.Value)
            {
                shortScore += 2;
                shortReasons.Add($"D1<EMA{_emaTrendFilter}");
            }
        }

        // +2: H4 Supertrend bearish
        if (supertrendIsBearish)
        {
            shortScore += 2;
            shortReasons.Add(supertrendJustFlippedBearish ? "ST-Flip↓" : "ST↓");
        }

        // +1: EMA 12 < EMA 26
        if (lastEmaFast.Value < lastEmaSlow.Value)
        {
            shortScore += 1;
            shortReasons.Add($"EMA{_emaFast}<EMA{_emaSlow}");
        }

        // +1: ADX > 20 UND steigend UND -DI > +DI (bearish Trend)
        if (lastAdx.Value >= _minAdx && adxRising && lastMdi.Value > lastPdi.Value)
        {
            shortScore += 1;
            shortReasons.Add($"ADX={lastAdx.Value:F0}↑");
        }

        // +1: RSI im bearish-Bereich (20-55)
        if (lastRsi.Value >= _rsiShortMin && lastRsi.Value <= _rsiShortMax)
        {
            shortScore += 1;
            shortReasons.Add($"RSI={lastRsi.Value:F0}");
        }

        // +1: Volumen über Durchschnitt
        if (currentVolume > lastVolSma.Value * _volumeMultiplier)
        {
            shortScore += 1;
            shortReasons.Add("Vol>Avg");
        }

        // +1-2: HTF bearish
        if (htfTrend < 0)
        {
            if (context.HigherTimeframeCandles != null && context.HigherTimeframeCandles.Count > _supertrendPeriod + 5)
            {
                var (_, htfStBullish) = IndicatorHelper.CalculateSupertrend(
                    context.HigherTimeframeCandles, _supertrendPeriod, _supertrendMultiplier);
                if (htfStBullish[^1] == false) // HTF-Supertrend-Bonus unabhängig vom aktuellen Score
                {
                    shortScore += 1;
                    shortReasons.Add("HTF-ST↓");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // Close-Signale: Supertrend-Flip + ADX-Verfall
        // Nur auslösen wenn ADX stark genug ist (Supertrend-Flip in Range = Noise, ignorieren)
        // ═══════════════════════════════════════════════════════════

        // Supertrend-Flip: Nur closen wenn ADX > 20 (echter Trendwechsel, nicht Range-Noise)
        if (supertrendJustFlippedBearish && lastAdx.Value >= _minAdx
            && context.OpenPositions.Any(p => p.Side == Side.Buy))
        {
            return new SignalResult(Signal.CloseLong, 0.9m, currentPrice, null, null,
                $"Supertrend-Flip bearish → Long schließen (ADX={lastAdx.Value:F0})");
        }
        if (supertrendJustFlippedBullish && lastAdx.Value >= _minAdx
            && context.OpenPositions.Any(p => p.Side == Side.Sell))
        {
            return new SignalResult(Signal.CloseShort, 0.9m, currentPrice, null, null,
                $"Supertrend-Flip bullish → Short schließen (ADX={lastAdx.Value:F0})");
        }

        // ADX-Verfall: Nur bei starkem Verfall (unter 10, nicht 15 - bei M15 ist 15 normal)
        if (lastAdx.Value < 10m && prevAdx.HasValue && prevAdx.Value >= 10m)
        {
            if (context.OpenPositions.Any(p => p.Side == Side.Buy))
                return new SignalResult(Signal.CloseLong, 0.7m, currentPrice, null, null,
                    $"ADX unter 10 gefallen → Trend tot, Long schließen");
            if (context.OpenPositions.Any(p => p.Side == Side.Sell))
                return new SignalResult(Signal.CloseShort, 0.7m, currentPrice, null, null,
                    $"ADX unter 10 gefallen → Trend tot, Short schließen");
        }

        // ═══════════════════════════════════════════════════════════
        // Signal-Generierung basierend auf Score
        // ═══════════════════════════════════════════════════════════

        // ATR-Perzentil für volatilitäts-adaptive SL/TP
        var atrPercentile = IndicatorHelper.CalculateAtrPercentile(candles, _atrPeriod, _atrPercentileLookback);
        var (slMult, tp1Mult, tp2Mult, trailMult) = GetVolAdaptiveMultipliers(atrPercentile);

        // Large-Caps (>500M Volume) brauchen weniger Confluence - stabiler und liquider
        var isLargeCap = context.CurrentTicker.Volume24h >= 500_000_000m;
        var effectiveMinScore = isLargeCap ? Math.Max(4, _minScore - 2) : _minScore;

        // Long-Signal
        if (longScore >= effectiveMinScore && longScore > shortScore)
        {
            // Keine bestehende Long-Position im selben Symbol
            if (context.OpenPositions.Any(p => p.Symbol == context.Symbol && p.Side == Side.Buy))
                return NoSignal("Bereits Long in diesem Symbol");

            var confidence = Math.Min(1m, longScore / 12m);
            var sl = currentPrice - atrValue * slMult;
            var tp1 = currentPrice + atrValue * tp1Mult;
            var tp2 = currentPrice + atrValue * tp2Mult;

            // Sicherheit: SL mindestens 0.5% vom Entry entfernt (Spread-Schutz bei Meme-Coins)
            var minSlDistance = currentPrice * 0.005m;
            if (currentPrice - sl < minSlDistance)
                sl = currentPrice - minSlDistance;

            var preferLimit = longScore >= 10;
            var limitEntry = preferLimit ? currentPrice - atrValue * 0.1m : currentPrice;

            return new SignalResult(Signal.Long, confidence, limitEntry, sl, tp1,
                $"CTP Long [{longScore}/12] ATR-P{atrPercentile}: {string.Join(", ", longReasons)}",
                TakeProfit2: tp2, ConfluenceScore: longScore, PreferLimitOrder: preferLimit);
        }

        // Short-Signal
        if (shortScore >= effectiveMinScore && shortScore > longScore)
        {
            if (context.OpenPositions.Any(p => p.Symbol == context.Symbol && p.Side == Side.Sell))
                return NoSignal("Bereits Short in diesem Symbol");

            var confidence = Math.Min(1m, shortScore / 12m);
            var sl = currentPrice + atrValue * slMult;
            var tp1 = currentPrice - atrValue * tp1Mult;
            var tp2 = currentPrice - atrValue * tp2Mult;

            // Sicherheit: SL mindestens 0.5% vom Entry entfernt (Spread-Schutz)
            var minSlDistShort = currentPrice * 0.005m;
            if (sl - currentPrice < minSlDistShort)
                sl = currentPrice + minSlDistShort;

            var preferLimitShort = shortScore >= 10;
            var limitEntryShort = preferLimitShort ? currentPrice + atrValue * 0.1m : currentPrice;

            return new SignalResult(Signal.Short, confidence, limitEntryShort, sl, tp1,
                $"CTP Short [{shortScore}/12] ATR-P{atrPercentile}: {string.Join(", ", shortReasons)}",
                TakeProfit2: tp2, ConfluenceScore: shortScore, PreferLimitOrder: preferLimitShort);
        }

        var bestSide = longScore > shortScore ? "Long" : "Short";
        var bestScore = Math.Max(longScore, shortScore);
        return NoSignal($"Score zu niedrig ({bestSide}: {bestScore}/12, brauche {_minScore})");
    }

    /// <summary>
    /// Volatilitäts-adaptive SL/TP-Multiplikatoren basierend auf ATR-Perzentil und aktivem Modus.
    /// Scalping: Engere Multiplikatoren. DayTrading: Mittel. Swing: Weiter.
    /// </summary>
    private (decimal slMult, decimal tp1Mult, decimal tp2Mult, decimal trailMult)
        GetVolAdaptiveMultipliers(int atrPercentile)
    {
        return TradingModeDefaults.GetVolAdaptiveMultipliers(_activePreset, atrPercentile);
    }

    /// <summary>
    /// Gibt den empfohlenen Positions-Skalierungsfaktor basierend auf Confluence-Score zurück.
    /// Score 8-9 = 75%, 10-11 = 100%, 12 = 125%.
    /// Wird extern im TradingServiceBase angewendet.
    /// </summary>
    public static decimal GetPositionScaleFactor(int score)
    {
        return score switch
        {
            >= 12 => 1.25m,
            >= 10 => 1.0m,
            >= 8  => 0.75m,
            _     => 0m // Kein Trade
        };
    }

    /// <summary>
    /// Gibt den empfohlenen Leverage basierend auf MaxLeverage, ATR-Perzentil und Score zurück.
    /// Der eingestellte MaxLeverage wird als Basis genommen und bei hoher Volatilität oder
    /// schwachem Signal leicht reduziert (max -20..30%), nie drastisch.
    /// </summary>
    public static int GetAdaptiveLeverage(int atrPercentile, int score, bool isBtc, int maxLeverage = 3)
    {
        var result = maxLeverage;

        // Hohe Volatilität → leichte Reduzierung (max 30% weniger)
        if (atrPercentile >= 90) result = Math.Max(1, (int)(maxLeverage * 0.7m));      // Extrem: -30%
        else if (atrPercentile >= 75) result = Math.Max(1, (int)(maxLeverage * 0.85m)); // Hoch: -15%

        // Niedriger Score → 1 Stufe weniger (nur wenn > 1)
        if (score <= 9 && result > 1) result--;

        return Math.Max(1, result);
    }

    private static SignalResult NoSignal(string reason) =>
        new(Signal.None, 0m, null, null, null, reason);

    public void WarmUp(IReadOnlyList<Candle> history)
    {
        if (history.Count < Math.Max(_emaSlow, _supertrendPeriod) + 20) return;
        IndicatorHelper.CalculateSupertrend(history, _supertrendPeriod, _supertrendMultiplier);
        IndicatorHelper.CalculateEma(history, _emaFast);
        IndicatorHelper.CalculateEma(history, _emaSlow);
        IndicatorHelper.CalculateAdxWithDi(history, _adxPeriod);
        IndicatorHelper.CalculateRsi(history, _rsiPeriod);
        IndicatorHelper.CalculateAtr(history, _atrPeriod);
        IndicatorHelper.CalculateVolumeSma(history, _volumePeriod);
    }

    public void Reset() { }

    public IStrategy Clone() => new CryptoTrendProStrategy
    {
        _activePreset = _activePreset,
        _supertrendPeriod = _supertrendPeriod,
        _supertrendMultiplier = _supertrendMultiplier,
        _emaFast = _emaFast,
        _emaSlow = _emaSlow,
        _emaTrendFilter = _emaTrendFilter,
        _adxPeriod = _adxPeriod,
        _minAdx = _minAdx,
        _rsiPeriod = _rsiPeriod,
        _rsiLongMin = _rsiLongMin,
        _rsiLongMax = _rsiLongMax,
        _rsiShortMin = _rsiShortMin,
        _rsiShortMax = _rsiShortMax,
        _volumePeriod = _volumePeriod,
        _volumeMultiplier = _volumeMultiplier,
        _atrPeriod = _atrPeriod,
        _atrPercentileLookback = _atrPercentileLookback,
        _minScore = _minScore
    };
}
