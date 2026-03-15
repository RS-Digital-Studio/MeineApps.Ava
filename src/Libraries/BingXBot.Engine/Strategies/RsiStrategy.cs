using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// RSI Momentum-Strategie (Krypto-optimiert).
/// Statt Mean-Reversion (gefährlich bei Krypto-Trends) wird RSI als Momentum-Indikator genutzt:
/// - Long wenn RSI von unter 40 über 50 steigt (Momentum-Wechsel)
/// - Short wenn RSI von über 60 unter 50 fällt
/// - Divergenz-Erkennung (Preis neues Hoch, RSI nicht → bearish)
/// - Volume-Konfirmation für alle Signale
/// </summary>
public class RsiStrategy : IStrategy
{
    public string Name => "RSI Momentum";
    public string Description => "RSI als Momentum-Indikator mit Divergenz-Erkennung (Krypto-optimiert)";

    private int _period = 14;
    private decimal _longEntry = 50m;
    private decimal _longTrigger = 40m;
    private decimal _shortEntry = 50m;
    private decimal _shortTrigger = 60m;
    private int _atrPeriod = 14;
    private int _volumePeriod = 20;
    private decimal _tpMultiplier = 2m;
    private int _divergenceLookback = 14;

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("Period", "RSI Periode", "int", _period, 5, 50, 1),
        new("LongTrigger", "Long Trigger (RSI war unter)", "decimal", _longTrigger, 20m, 50m, 5m),
        new("LongEntry", "Long Entry (RSI kreuzt über)", "decimal", _longEntry, 40m, 60m, 5m),
        new("ShortTrigger", "Short Trigger (RSI war über)", "decimal", _shortTrigger, 50m, 80m, 5m),
        new("ShortEntry", "Short Entry (RSI kreuzt unter)", "decimal", _shortEntry, 40m, 60m, 5m),
        new("AtrPeriod", "ATR Periode für Stop-Loss", "int", _atrPeriod, 5, 50, 1),
        new("VolumePeriod", "Volumen-SMA Periode", "int", _volumePeriod, 10, 50, 1),
        new("TpMultiplier", "Take-Profit Multiplikator", "decimal", _tpMultiplier, 1m, 5m, 0.5m),
        new("DivergenceLookback", "Divergenz-Lookback Perioden", "int", _divergenceLookback, 5, 30, 1),
    };

    public SignalResult Evaluate(MarketContext context)
    {
        var candles = context.Candles;
        if (candles.Count < _period + _divergenceLookback + 5)
            return new SignalResult(Signal.None, 0m, null, null, null, "Zu wenig Daten");

        var rsi = IndicatorHelper.CalculateRsi(candles, _period);
        var atr = IndicatorHelper.CalculateAtr(candles, _atrPeriod);
        var volumeSma = IndicatorHelper.CalculateSma(candles, _volumePeriod);

        var lastRsi = rsi[^1];
        var prevRsi = rsi[^2];
        var lastAtr = atr[^1];
        var lastVolSma = volumeSma[^1];

        if (lastRsi == null || prevRsi == null || lastAtr == null || lastVolSma == null)
            return new SignalResult(Signal.None, 0m, null, null, null, "Indikatoren nicht bereit");

        var currentPrice = context.CurrentTicker.LastPrice;
        var atrValue = lastAtr.Value;
        var currentVolume = candles[^1].Volume;
        var volumeAboveAvg = currentVolume > lastVolSma.Value;

        // 1. Divergenz-Erkennung (stärkstes Signal)
        var divergenceResult = CheckDivergence(candles, rsi, currentPrice, atrValue);
        if (divergenceResult.Signal != Signal.None && volumeAboveAvg)
            return divergenceResult;

        // 2. RSI Momentum-Wechsel: Long wenn RSI von unter LongTrigger über LongEntry steigt
        if (prevRsi < _longEntry && lastRsi >= _longEntry && lastRsi < 70m)
        {
            // Prüfe ob RSI kürzlich unter dem Trigger war (Momentum-Aufbau)
            var recentlyBelowTrigger = false;
            for (int i = Math.Max(0, rsi.Count - 6); i < rsi.Count - 1; i++)
            {
                if (rsi[i] != null && rsi[i]!.Value < _longTrigger)
                {
                    recentlyBelowTrigger = true;
                    break;
                }
            }

            if (recentlyBelowTrigger && volumeAboveAvg)
            {
                var confidence = 0.75m;
                var sl = currentPrice - atrValue * 2m;
                var tp = currentPrice + atrValue * 2m * _tpMultiplier;
                return new SignalResult(Signal.Long, confidence, currentPrice, sl, tp,
                    $"RSI Momentum-Wechsel: {prevRsi.Value:F1} → {lastRsi.Value:F1} (über {_longEntry})");
            }
        }

        // 3. RSI Momentum-Wechsel: Short wenn RSI von über ShortTrigger unter ShortEntry fällt
        if (prevRsi > _shortEntry && lastRsi <= _shortEntry && lastRsi > 30m)
        {
            var recentlyAboveTrigger = false;
            for (int i = Math.Max(0, rsi.Count - 6); i < rsi.Count - 1; i++)
            {
                if (rsi[i] != null && rsi[i]!.Value > _shortTrigger)
                {
                    recentlyAboveTrigger = true;
                    break;
                }
            }

            if (recentlyAboveTrigger && volumeAboveAvg)
            {
                var confidence = 0.75m;
                var sl = currentPrice + atrValue * 2m;
                var tp = currentPrice - atrValue * 2m * _tpMultiplier;
                return new SignalResult(Signal.Short, confidence, currentPrice, sl, tp,
                    $"RSI Momentum-Wechsel: {prevRsi.Value:F1} → {lastRsi.Value:F1} (unter {_shortEntry})");
            }
        }

        return new SignalResult(Signal.None, 0m, null, null, null,
            $"RSI {lastRsi.Value:F1} - kein Momentum-Signal");
    }

    /// <summary>
    /// Divergenz-Erkennung: Preis macht neues Hoch/Tief aber RSI nicht → Trendwende.
    /// </summary>
    private SignalResult CheckDivergence(IReadOnlyList<Candle> candles, IReadOnlyList<decimal?> rsi,
        decimal currentPrice, decimal atrValue)
    {
        if (candles.Count < _divergenceLookback + 2 || rsi.Count < _divergenceLookback + 2)
            return new SignalResult(Signal.None, 0m, null, null, null, "");

        // Lookback-Bereich
        var startIdx = candles.Count - _divergenceLookback - 1;
        var endIdx = candles.Count - 2; // Vorletzte Candle als Vergleich

        // Höchster Preis und höchster RSI im Lookback-Bereich
        var highestPrice = candles[startIdx].High;
        var highestRsi = rsi[startIdx] ?? 0m;
        for (int i = startIdx + 1; i <= endIdx; i++)
        {
            if (candles[i].High > highestPrice) highestPrice = candles[i].High;
            if (rsi[i] != null && rsi[i]!.Value > highestRsi) highestRsi = rsi[i]!.Value;
        }

        // Bearish Divergenz: Preis macht neues Hoch, RSI nicht
        var lastRsi = rsi[^1]!.Value;
        if (candles[^1].High >= highestPrice && lastRsi < highestRsi - 5m && lastRsi > 50m)
        {
            var sl = currentPrice + atrValue * 2m;
            var tp = currentPrice - atrValue * 3m;
            return new SignalResult(Signal.Short, 0.85m, currentPrice, sl, tp,
                $"Bearish Divergenz: Preis neues Hoch, RSI fällt ({lastRsi:F1} vs {highestRsi:F1})");
        }

        // Niedrigster Preis und niedrigster RSI im Lookback-Bereich
        var lowestPrice = candles[startIdx].Low;
        var lowestRsi = rsi[startIdx] ?? 100m;
        for (int i = startIdx + 1; i <= endIdx; i++)
        {
            if (candles[i].Low < lowestPrice) lowestPrice = candles[i].Low;
            if (rsi[i] != null && rsi[i]!.Value < lowestRsi) lowestRsi = rsi[i]!.Value;
        }

        // Bullish Divergenz: Preis macht neues Tief, RSI nicht
        if (candles[^1].Low <= lowestPrice && lastRsi > lowestRsi + 5m && lastRsi < 50m)
        {
            var sl = currentPrice - atrValue * 2m;
            var tp = currentPrice + atrValue * 3m;
            return new SignalResult(Signal.Long, 0.85m, currentPrice, sl, tp,
                $"Bullish Divergenz: Preis neues Tief, RSI steigt ({lastRsi:F1} vs {lowestRsi:F1})");
        }

        return new SignalResult(Signal.None, 0m, null, null, null, "");
    }

    public void WarmUp(IReadOnlyList<Candle> history) { }
    public void Reset() { }

    public IStrategy Clone() => new RsiStrategy
    {
        _period = _period,
        _longEntry = _longEntry,
        _longTrigger = _longTrigger,
        _shortEntry = _shortEntry,
        _shortTrigger = _shortTrigger,
        _atrPeriod = _atrPeriod,
        _volumePeriod = _volumePeriod,
        _tpMultiplier = _tpMultiplier,
        _divergenceLookback = _divergenceLookback
    };
}
