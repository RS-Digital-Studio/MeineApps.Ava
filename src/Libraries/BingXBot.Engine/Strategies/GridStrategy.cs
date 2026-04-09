using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Smart Grid-Strategie (Krypto-optimiert).
/// Handelt NUR in Range-Märkten, pausiert bei starken Trends:
/// - Trend-Check via EMA50: Starker Trend → kein Grid-Trading
/// - Range-Erkennung: Nur aktiv wenn ATR unter Durchschnitt (= niedriger Volatilität)
/// - Dynamische Grenzen: Grid-Grenzen basierend auf Bollinger-Bändern statt statisch
/// </summary>
public class GridStrategy : IStrategy
{
    public string Name => "Smart Grid";
    public string Description => "Smart Grid: Nur in Range-Märkten, dynamische Grenzen via Bollinger (Krypto-optimiert)";

    private int _gridLevels = 5;
    private decimal _gridSpacingPercent = 1.0m;
    private int _emaPeriod = 50;
    private int _atrPeriod = 14;
    private int _atrAvgPeriod = 50;
    private int _bollingerPeriod = 20;
    private decimal _bollingerStdDev = 2m;
    private decimal _trendThresholdPercent = 3.5m;

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("GridLevels", "Anzahl Grid-Stufen", "int", _gridLevels, 2, 20, 1),
        new("GridSpacing", "Grid-Abstand in Prozent", "decimal", _gridSpacingPercent, 0.1m, 5.0m, 0.1m),
        new("EmaPeriod", "EMA für Trend-Erkennung", "int", _emaPeriod, 20, 100, 5),
        new("AtrPeriod", "ATR Periode", "int", _atrPeriod, 5, 50, 1),
        new("AtrAvgPeriod", "ATR-Durchschnitt Perioden", "int", _atrAvgPeriod, 20, 100, 5),
        new("BollingerPeriod", "Bollinger Periode für Grid-Grenzen", "int", _bollingerPeriod, 10, 50, 1),
        new("BollingerStdDev", "Bollinger Standardabweichung", "decimal", _bollingerStdDev, 1m, 3m, 0.5m),
        new("TrendThreshold", "Trend-Schwelle in %", "decimal", _trendThresholdPercent, 0.5m, 5m, 0.5m),
    };

    public SignalResult Evaluate(MarketContext context)
    {
        var candles = context.Candles;
        if (candles.Count < Math.Max(_emaPeriod, _atrAvgPeriod) + 5)
            return new SignalResult(Signal.None, 0m, null, null, null, "Zu wenig Daten");

        var currentPrice = context.CurrentTicker.LastPrice;

        // 1. Trend-Check via EMA: Starker Trend → KEIN Grid-Trading
        var ema = IndicatorHelper.CalculateEma(candles, _emaPeriod);
        var lastEma = ema[^1];
        if (lastEma == null)
            return new SignalResult(Signal.None, 0m, null, null, null, "EMA nicht bereit");

        var emaDeviation = Math.Abs(currentPrice - lastEma.Value) / lastEma.Value * 100m;
        if (emaDeviation > _trendThresholdPercent)
            return new SignalResult(Signal.None, 0m, null, null, null,
                $"Starker Trend erkannt (Preis {emaDeviation:F1}% von EMA{_emaPeriod} entfernt) - Grid pausiert");

        // 2. Range-Erkennung: ATR muss unter dem Durchschnitt sein
        var atr = IndicatorHelper.CalculateAtr(candles, _atrPeriod);
        var lastAtr = atr[^1];
        if (lastAtr == null)
            return new SignalResult(Signal.None, 0m, null, null, null, "ATR nicht bereit");

        // ATR-Durchschnitt über die letzten N Perioden
        var atrSum = 0m;
        var atrCount = 0;
        var atrStart = Math.Max(0, atr.Count - _atrAvgPeriod);
        for (int i = atrStart; i < atr.Count; i++)
        {
            if (atr[i] != null)
            {
                atrSum += atr[i]!.Value;
                atrCount++;
            }
        }
        var atrAvg = atrCount > 0 ? atrSum / atrCount : lastAtr.Value;

        if (lastAtr.Value > atrAvg * 1.2m) // ATR 20% über Durchschnitt = zu volatil für Grid
            return new SignalResult(Signal.None, 0m, null, null, null,
                $"Zu hohe Volatilität für Grid (ATR {lastAtr.Value:F2} > Avg {atrAvg:F2})");

        // 3. Dynamische Grid-Grenzen: Lookback High/Low (stabiler als Bollinger-Bänder)
        // Bollinger-Bänder werden weiterhin für Squeeze-Erkennung genutzt, aber
        // Grid-Grenzen basieren auf den tatsächlichen Extremwerten der letzten 50 Candles.
        var lookbackCandles = candles.Skip(Math.Max(0, candles.Count - 50));
        var lookbackHigh = lookbackCandles.Max(c => c.High);
        var lookbackLow = lookbackCandles.Min(c => c.Low);

        var gridUpper = lookbackHigh;
        var gridLower = lookbackLow;

        if (currentPrice < gridLower || currentPrice > gridUpper)
            return new SignalResult(Signal.None, 0m, null, null, null,
                $"Preis {currentPrice:F2} außerhalb des dynamischen Grids ({gridLower:F2} - {gridUpper:F2})");

        // 4. Grid-Levels berechnen
        var range = gridUpper - gridLower;
        if (range <= 0)
            return new SignalResult(Signal.None, 0m, null, null, null, "Grid-Range ist 0 oder negativ");

        var stepSize = range / (_gridLevels + 1);
        var gridLevels = new List<decimal>();
        for (int i = 1; i <= _gridLevels; i++)
            gridLevels.Add(gridLower + stepSize * i);

        // Nächstes Grid-Level unter/über dem aktuellen Preis (nullable, da am Rand keins existieren kann)
        var belowLevels = gridLevels.Where(l => l < currentPrice).OrderByDescending(l => l).ToList();
        var aboveLevels = gridLevels.Where(l => l > currentPrice).OrderBy(l => l).ToList();
        decimal? nearestBelow = belowLevels.Count > 0 ? belowLevels[0] : null;
        decimal? nearestAbove = aboveLevels.Count > 0 ? aboveLevels[0] : null;

        // Am Grid-Rand: Kein Signal wenn kein Level in der gewünschten Richtung existiert
        if (nearestBelow == null && nearestAbove == null)
            return new SignalResult(Signal.None, 0m, null, null, null, "Preis zwischen keinen Grid-Levels");

        var spacingThreshold = currentPrice * _gridSpacingPercent / 100m;
        if (spacingThreshold <= 0)
            return new SignalResult(Signal.None, 0m, null, null, null, "Grid-Spacing ist 0");

        // Preis nahe am unteren Grid-Level → Long (nur wenn auch ein oberes Level für TP existiert)
        if (nearestBelow.HasValue && nearestAbove.HasValue && currentPrice - nearestBelow.Value < spacingThreshold)
        {
            var sl = nearestBelow.Value - stepSize * 0.5m;
            var tp = nearestAbove.Value;
            var confidence = 0.6m + (1m - (currentPrice - nearestBelow.Value) / spacingThreshold) * 0.3m;
            return new SignalResult(Signal.Long, Math.Min(1m, confidence), currentPrice, sl, tp,
                $"Smart Grid Buy-Zone bei {nearestBelow.Value:F2} (Range-Markt bestätigt)");
        }

        // Preis nahe am oberen Grid-Level → Short (nur wenn auch ein unteres Level für TP existiert)
        if (nearestAbove.HasValue && nearestBelow.HasValue && nearestAbove.Value - currentPrice < spacingThreshold)
        {
            var sl = nearestAbove.Value + stepSize * 0.5m;
            var tp = nearestBelow.Value;
            var confidence = 0.6m + (1m - (nearestAbove.Value - currentPrice) / spacingThreshold) * 0.3m;
            return new SignalResult(Signal.Short, Math.Min(1m, confidence), currentPrice, sl, tp,
                $"Smart Grid Sell-Zone bei {nearestAbove.Value:F2} (Range-Markt bestätigt)");
        }

        return new SignalResult(Signal.None, 0m, null, null, null,
            $"Zwischen Grid-Levels (nächstes Buy: {nearestBelow?.ToString("F2") ?? "-"}, nächstes Sell: {nearestAbove?.ToString("F2") ?? "-"})");
    }

    public void WarmUp(IReadOnlyList<Candle> history)
    {
        if (history.Count < Math.Max(_emaPeriod, _atrAvgPeriod) + 5) return;
        IndicatorHelper.CalculateEma(history, _emaPeriod);
        IndicatorHelper.CalculateAtr(history, _atrPeriod);
        IndicatorHelper.CalculateBollinger(history, _bollingerPeriod, _bollingerStdDev);
    }
    public void Reset() { }

    public IStrategy Clone() => new GridStrategy
    {
        _gridLevels = _gridLevels,
        _gridSpacingPercent = _gridSpacingPercent,
        _emaPeriod = _emaPeriod,
        _atrPeriod = _atrPeriod,
        _atrAvgPeriod = _atrAvgPeriod,
        _bollingerPeriod = _bollingerPeriod,
        _bollingerStdDev = _bollingerStdDev,
        _trendThresholdPercent = _trendThresholdPercent
    };
}
