using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Bollinger Breakout-Strategie (Krypto-optimiert).
/// Statt Mean-Reversion (gefährlich bei Krypto-Breakouts) wird auf Squeeze+Breakout gesetzt:
/// - Squeeze-Erkennung wenn Bandbreite unter dem Durchschnitt
/// - Breakout Long: Close über oberem Band NACH Squeeze + Volume-Konfirmation
/// - Breakout Short: Close unter unterem Band NACH Squeeze + Volume-Konfirmation
/// </summary>
public class BollingerStrategy : IStrategy
{
    public string Name => "Bollinger Breakout";
    public string Description => "Bollinger Squeeze → Breakout mit Volume-Konfirmation (Krypto-optimiert)";

    private int _period = 20;
    private decimal _stdDev = 2m;
    private int _atrPeriod = 14;
    private int _squeezePeriod = 120;
    private int _volumePeriod = 20;
    private decimal _tpMultiplier = 2m;

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("Period", "Bollinger Periode", "int", _period, 10, 50, 1),
        new("StdDev", "Standardabweichung", "decimal", _stdDev, 1m, 3m, 0.5m),
        new("AtrPeriod", "ATR Periode für Stop-Loss", "int", _atrPeriod, 5, 50, 1),
        new("SqueezePeriod", "Squeeze-Referenz Perioden", "int", _squeezePeriod, 50, 200, 10),
        new("VolumePeriod", "Volumen-SMA Periode", "int", _volumePeriod, 10, 50, 1),
        new("TpMultiplier", "Take-Profit Multiplikator", "decimal", _tpMultiplier, 1m, 5m, 0.5m),
    };

    public SignalResult Evaluate(MarketContext context)
    {
        var candles = context.Candles;
        if (candles.Count < _squeezePeriod + 5)
            return new SignalResult(Signal.None, 0m, null, null, null, "Zu wenig Daten");

        var (upper, middle, lower) = IndicatorHelper.CalculateBollinger(candles, _period, _stdDev);
        var atr = IndicatorHelper.CalculateAtr(candles, _atrPeriod);
        var volumeSma = IndicatorHelper.CalculateSma(candles, _volumePeriod);

        var lastUpper = upper[^1];
        var lastMiddle = middle[^1];
        var lastLower = lower[^1];
        var lastAtr = atr[^1];
        var lastVolSma = volumeSma[^1];

        if (lastUpper == null || lastMiddle == null || lastLower == null || lastAtr == null || lastVolSma == null)
            return new SignalResult(Signal.None, 0m, null, null, null, "Indikatoren nicht bereit");

        var currentPrice = context.CurrentTicker.LastPrice;
        var atrValue = lastAtr.Value;
        var currentClose = candles[^1].Close;
        var currentVolume = candles[^1].Volume;

        // Aktuelle Bandbreite
        var currentBandWidth = lastUpper.Value - lastLower.Value;
        if (currentBandWidth <= 0)
            return new SignalResult(Signal.None, 0m, null, null, null, "Bollinger Bandbreite ist 0");

        // Durchschnittliche Bandbreite über Squeeze-Periode berechnen
        var avgBandWidth = CalculateAverageBandWidth(upper, lower, _squeezePeriod);
        if (avgBandWidth <= 0)
            return new SignalResult(Signal.None, 0m, null, null, null, "Bandbreiten-Durchschnitt nicht berechenbar");

        // Squeeze-Erkennung: Aktuelle Bandbreite unter dem Durchschnitt
        var isSqueezing = currentBandWidth < avgBandWidth;

        // Prüfe ob kürzlich ein Squeeze war (letzte 5 Candles)
        var recentSqueeze = false;
        for (int i = Math.Max(0, upper.Count - 6); i < upper.Count - 1; i++)
        {
            if (upper[i] != null && lower[i] != null)
            {
                var bw = upper[i]!.Value - lower[i]!.Value;
                if (bw < avgBandWidth)
                {
                    recentSqueeze = true;
                    break;
                }
            }
        }

        // Volume-Konfirmation
        var volumeAboveAvg = currentVolume > lastVolSma.Value;

        // Breakout Long: Close über oberem Band nach Squeeze + Volume
        if (currentClose > lastUpper.Value && (isSqueezing || recentSqueeze))
        {
            if (!volumeAboveAvg)
                return new SignalResult(Signal.None, 0m, null, null, null,
                    "Breakout über oberes Band, aber Volumen unter Durchschnitt");

            var confidence = 0.8m;
            // Stärkere Confidence bei größerem Breakout
            var breakoutStrength = (currentClose - lastUpper.Value) / currentBandWidth;
            if (breakoutStrength > 0.1m) confidence = 0.85m;
            if (breakoutStrength > 0.2m) confidence = 0.9m;

            var sl = lastMiddle.Value; // Mittleres Band als Stop-Loss
            var tp = currentPrice + atrValue * 2m * _tpMultiplier;
            return new SignalResult(Signal.Long, confidence, currentPrice, sl, tp,
                $"Bollinger Breakout Long nach Squeeze (Bandbreite: {currentBandWidth:F2} vs Avg: {avgBandWidth:F2})");
        }

        // Breakout Short: Close unter unterem Band nach Squeeze + Volume
        if (currentClose < lastLower.Value && (isSqueezing || recentSqueeze))
        {
            if (!volumeAboveAvg)
                return new SignalResult(Signal.None, 0m, null, null, null,
                    "Breakout unter unteres Band, aber Volumen unter Durchschnitt");

            var confidence = 0.8m;
            var breakoutStrength = (lastLower.Value - currentClose) / currentBandWidth;
            if (breakoutStrength > 0.1m) confidence = 0.85m;
            if (breakoutStrength > 0.2m) confidence = 0.9m;

            var sl = lastMiddle.Value;
            var tp = currentPrice - atrValue * 2m * _tpMultiplier;
            return new SignalResult(Signal.Short, confidence, currentPrice, sl, tp,
                $"Bollinger Breakout Short nach Squeeze (Bandbreite: {currentBandWidth:F2} vs Avg: {avgBandWidth:F2})");
        }

        // Status-Info
        var status = isSqueezing
            ? $"Squeeze aktiv (Bandbreite {currentBandWidth:F2} < Avg {avgBandWidth:F2}) - warte auf Breakout"
            : $"Kein Squeeze (Bandbreite {currentBandWidth:F2}, Avg {avgBandWidth:F2})";

        return new SignalResult(Signal.None, 0m, null, null, null, status);
    }

    /// <summary>
    /// Berechnet die durchschnittliche Bandbreite über die letzten N Perioden.
    /// </summary>
    private static decimal CalculateAverageBandWidth(IReadOnlyList<decimal?> upper, IReadOnlyList<decimal?> lower, int lookback)
    {
        var sum = 0m;
        var count = 0;
        var startIdx = Math.Max(0, upper.Count - lookback);

        for (int i = startIdx; i < upper.Count; i++)
        {
            if (upper[i] != null && lower[i] != null)
            {
                sum += upper[i]!.Value - lower[i]!.Value;
                count++;
            }
        }

        return count > 0 ? sum / count : 0m;
    }

    public void WarmUp(IReadOnlyList<Candle> history) { }
    public void Reset() { }

    public IStrategy Clone() => new BollingerStrategy
    {
        _period = _period,
        _stdDev = _stdDev,
        _atrPeriod = _atrPeriod,
        _squeezePeriod = _squeezePeriod,
        _volumePeriod = _volumePeriod,
        _tpMultiplier = _tpMultiplier
    };
}
