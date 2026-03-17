using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Trend-Following Multi-Indikator-Strategie (Krypto-optimiert).
/// Die BESTE Strategie für Krypto-Futures: Kombiniert 5 Bedingungen für maximale Sicherheit.
///
/// Long wenn ALLE zutreffen:
///   1. Preis über EMA50 (Aufwärtstrend)
///   2. EMA20 über EMA50 (Trend-Bestätigung)
///   3. RSI > 50 aber &lt; 70 (Momentum vorhanden, nicht überkauft)
///   4. MACD-Histogram steigt (Momentum zunimmt)
///   5. Volumen über 20-Perioden-SMA (Bestätigung)
///
/// Short analog umgekehrt.
/// SL: 2x ATR, TP: 3x ATR (RRR 1.5:1)
/// Confidence: Anzahl erfüllter Bedingungen / 5 (partielle Signale möglich)
/// </summary>
public class TrendFollowStrategy : IStrategy
{
    public string Name => "Trend-Following";
    public string Description => "Multi-Indikator Trend-Following: EMA+RSI+MACD+Volume (5 Bedingungen, Krypto-optimiert)";

    private int _emaFast = 20;
    private int _emaSlow = 50;
    private int _rsiPeriod = 14;
    private int _atrPeriod = 14;
    private decimal _atrMultiplierSl = 2m;
    private decimal _atrMultiplierTp = 3m;
    private int _volumePeriod = 20;
    private decimal _volumeMultiplier = 1.0m;
    private decimal _minConfidence = 0.6m;
    private int _adxPeriod = 14;
    private decimal _minAdx = 20m;

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("EmaFast", "Schnelle EMA Periode", "int", _emaFast, 10, 50, 5),
        new("EmaSlow", "Langsame EMA Periode", "int", _emaSlow, 20, 100, 5),
        new("RsiPeriod", "RSI Periode", "int", _rsiPeriod, 5, 30, 1),
        new("AtrPeriod", "ATR Periode", "int", _atrPeriod, 5, 50, 1),
        new("AtrMultiplierSl", "ATR-Multiplikator für Stop-Loss", "decimal", _atrMultiplierSl, 1m, 5m, 0.5m),
        new("AtrMultiplierTp", "ATR-Multiplikator für Take-Profit", "decimal", _atrMultiplierTp, 1.5m, 8m, 0.5m),
        new("VolumePeriod", "Volumen-SMA Periode", "int", _volumePeriod, 10, 50, 5),
        new("VolumeMultiplier", "Volumen-Schwelle (x Durchschnitt)", "decimal", _volumeMultiplier, 0.5m, 3m, 0.1m),
        new("MinConfidence", "Min. Confidence für Signal", "decimal", _minConfidence, 0.4m, 1m, 0.1m),
        new("AdxPeriod", "ADX Periode (Trend-Stärke)", "int", _adxPeriod, 7, 30, 1),
        new("MinAdx", "Min. ADX für Trend-Signal (>25 = starker Trend)", "decimal", _minAdx, 15m, 40m, 5m),
    };

    public SignalResult Evaluate(MarketContext context)
    {
        var candles = context.Candles;
        // MACD braucht 26+9 Warmup, EMA50 braucht 50, RSI braucht 14
        var minCandles = Math.Max(_emaSlow, 35) + 10;
        if (candles.Count < minCandles)
            return new SignalResult(Signal.None, 0m, null, null, null, "Zu wenig Daten");

        // Alle Indikatoren berechnen
        var emaFast = IndicatorHelper.CalculateEma(candles, _emaFast);
        var emaSlow = IndicatorHelper.CalculateEma(candles, _emaSlow);
        var rsi = IndicatorHelper.CalculateRsi(candles, _rsiPeriod);
        var atr = IndicatorHelper.CalculateAtr(candles, _atrPeriod);
        var (_, _, histogram) = IndicatorHelper.CalculateMacd(candles, 12, 26, 9);
        var volumeSma = IndicatorHelper.CalculateSma(candles, _volumePeriod);
        var adx = IndicatorHelper.CalculateAdx(candles, _adxPeriod);

        var lastEmaFast = emaFast[^1];
        var lastEmaSlow = emaSlow[^1];
        var lastRsi = rsi[^1];
        var lastAtr = atr[^1];
        var lastHist = histogram[^1];
        var prevHist = histogram[^2];
        var lastVolSma = volumeSma[^1];
        var lastAdx = adx[^1];

        if (lastEmaFast == null || lastEmaSlow == null || lastRsi == null ||
            lastAtr == null || lastHist == null || prevHist == null || lastVolSma == null)
            return new SignalResult(Signal.None, 0m, null, null, null, "Indikatoren nicht bereit");

        // ADX-Filter: Kein Trade wenn Trend zu schwach (Seitwärtsmarkt = Noise)
        if (lastAdx.HasValue && lastAdx.Value < _minAdx)
            return new SignalResult(Signal.None, 0m, null, null, null,
                $"ADX zu niedrig ({lastAdx.Value:F0} < {_minAdx}) - kein klarer Trend");

        var currentPrice = context.CurrentTicker.LastPrice;
        var atrValue = lastAtr.Value;
        var currentVolume = candles[^1].Volume;

        // === LONG-Bedingungen prüfen ===
        var longConditions = 0;
        var longReasons = new List<string>();

        // 1. Preis über EMA Slow (Aufwärtstrend)
        if (currentPrice > lastEmaSlow.Value)
        {
            longConditions++;
            longReasons.Add("Preis>EMA" + _emaSlow);
        }

        // 2. EMA Fast über EMA Slow (Trend-Bestätigung)
        if (lastEmaFast.Value > lastEmaSlow.Value)
        {
            longConditions++;
            longReasons.Add($"EMA{_emaFast}>EMA{_emaSlow}");
        }

        // 3. RSI > 50 aber < 70 (Momentum, nicht überkauft)
        if (lastRsi.Value > 50m && lastRsi.Value < 70m)
        {
            longConditions++;
            longReasons.Add($"RSI={lastRsi.Value:F0}");
        }

        // 4. MACD-Histogram steigt (Momentum zunimmt)
        if (lastHist.Value > prevHist.Value)
        {
            longConditions++;
            longReasons.Add("MACD-Hist steigt");
        }

        // 5. Volumen über SMA (Bestätigung)
        if (currentVolume > lastVolSma.Value * _volumeMultiplier)
        {
            longConditions++;
            longReasons.Add("Vol>Avg");
        }

        // === SHORT-Bedingungen prüfen ===
        var shortConditions = 0;
        var shortReasons = new List<string>();

        // 1. Preis unter EMA Slow
        if (currentPrice < lastEmaSlow.Value)
        {
            shortConditions++;
            shortReasons.Add("Preis<EMA" + _emaSlow);
        }

        // 2. EMA Fast unter EMA Slow
        if (lastEmaFast.Value < lastEmaSlow.Value)
        {
            shortConditions++;
            shortReasons.Add($"EMA{_emaFast}<EMA{_emaSlow}");
        }

        // 3. RSI < 50 aber > 30
        if (lastRsi.Value < 50m && lastRsi.Value > 30m)
        {
            shortConditions++;
            shortReasons.Add($"RSI={lastRsi.Value:F0}");
        }

        // 4. MACD-Histogram fällt
        if (lastHist.Value < prevHist.Value)
        {
            shortConditions++;
            shortReasons.Add("MACD-Hist fällt");
        }

        // 5. Volumen über SMA
        if (currentVolume > lastVolSma.Value * _volumeMultiplier)
        {
            shortConditions++;
            shortReasons.Add("Vol>Avg");
        }

        // Signal generieren basierend auf Anzahl erfüllter Bedingungen
        var longConfidence = longConditions / 5m;
        var shortConfidence = shortConditions / 5m;

        // ADX-Bonus: Starker Trend (>40) erhöht Confidence, schwacher (20-25) reduziert
        if (lastAdx.HasValue)
        {
            var adxBonus = lastAdx.Value > 40m ? 0.1m : lastAdx.Value < 25m ? -0.05m : 0m;
            longConfidence += adxBonus;
            shortConfidence += adxBonus;
        }

        // Higher-Timeframe Trend-Konfirmation (wenn verfügbar)
        var htfTrend = IndicatorHelper.GetHigherTimeframeTrend(context.HigherTimeframeCandles);

        // Long-Signal wenn genug Bedingungen erfüllt
        if (longConfidence >= _minConfidence && longConfidence > shortConfidence)
        {
            // Higher-TF warnt: Confidence reduzieren, aber Signal nicht blockieren
            if (htfTrend == -1) longConfidence -= 0.15m;
            else if (htfTrend == 1) longConfidence += 0.05m;

            if (longConfidence < _minConfidence)
                return new SignalResult(Signal.None, 0m, null, null, null,
                    $"Long-Signal ({longConditions}/5), aber Higher-TF bearish → Confidence zu niedrig");

            var sl = currentPrice - atrValue * _atrMultiplierSl;
            var tp = currentPrice + atrValue * _atrMultiplierTp;
            var htfInfo = htfTrend != 0 ? $", HTF:{(htfTrend > 0 ? "Bull" : "Bear")}" : "";
            return new SignalResult(Signal.Long, Math.Min(1m, longConfidence), currentPrice, sl, tp,
                $"Trend-Following Long ({longConditions}/5{htfInfo}): {string.Join(", ", longReasons)}");
        }

        // Short-Signal wenn genug Bedingungen erfüllt
        if (shortConfidence >= _minConfidence && shortConfidence > longConfidence)
        {
            if (htfTrend == 1) shortConfidence -= 0.15m;
            else if (htfTrend == -1) shortConfidence += 0.05m;

            if (shortConfidence < _minConfidence)
                return new SignalResult(Signal.None, 0m, null, null, null,
                    $"Short-Signal ({shortConditions}/5), aber Higher-TF bullish → Confidence zu niedrig");

            var sl = currentPrice + atrValue * _atrMultiplierSl;
            var tp = currentPrice - atrValue * _atrMultiplierTp;
            var htfInfo = htfTrend != 0 ? $", HTF:{(htfTrend > 0 ? "Bull" : "Bear")}" : "";
            return new SignalResult(Signal.Short, Math.Min(1m, shortConfidence), currentPrice, sl, tp,
                $"Trend-Following Short ({shortConditions}/5{htfInfo}): {string.Join(", ", shortReasons)}");
        }

        return new SignalResult(Signal.None, 0m, null, null, null,
            $"Keine klare Trend-Richtung (Long: {longConditions}/5, Short: {shortConditions}/5)");
    }

    public void WarmUp(IReadOnlyList<Candle> history)
    {
        if (history.Count < Math.Max(_emaSlow, 35) + 10) return;
        // Indikatoren vorab berechnen und in den Cache legen
        IndicatorHelper.CalculateEma(history, _emaFast);
        IndicatorHelper.CalculateEma(history, _emaSlow);
        IndicatorHelper.CalculateRsi(history, _rsiPeriod);
        IndicatorHelper.CalculateAtr(history, _atrPeriod);
        IndicatorHelper.CalculateMacd(history, 12, 26, 9);
        IndicatorHelper.CalculateSma(history, _volumePeriod);
        IndicatorHelper.CalculateAdx(history, _adxPeriod);
    }
    public void Reset() { }

    public IStrategy Clone() => new TrendFollowStrategy
    {
        _emaFast = _emaFast,
        _emaSlow = _emaSlow,
        _rsiPeriod = _rsiPeriod,
        _atrPeriod = _atrPeriod,
        _atrMultiplierSl = _atrMultiplierSl,
        _atrMultiplierTp = _atrMultiplierTp,
        _volumePeriod = _volumePeriod,
        _volumeMultiplier = _volumeMultiplier,
        _minConfidence = _minConfidence,
        _adxPeriod = _adxPeriod,
        _minAdx = _minAdx
    };
}
