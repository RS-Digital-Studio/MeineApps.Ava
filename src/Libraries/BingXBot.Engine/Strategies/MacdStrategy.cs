using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// MACD + Histogram-Momentum-Strategie (Krypto-optimiert).
/// Statt einfachem MACD-Cross (zu viele Fehlsignale, Signal kommt zu spät):
/// - Histogram-Momentum: Long wenn Histogram negativ→positiv wechselt UND 2 steigende Balken
/// - Zero-Line-Cross: Stärkeres Signal wenn MACD die Null-Linie kreuzt
/// - Trend-Kontext: Nur Long wenn MACD > 0 (Aufwärtstrend), nur Short wenn MACD < 0
/// </summary>
public class MacdStrategy : IStrategy
{
    public string Name => "MACD";
    public string Description => "MACD Histogram-Momentum + Zero-Line-Cross + Trend-Kontext (Krypto-optimiert)";

    private int _fastPeriod = 12;
    private int _slowPeriod = 26;
    private int _signalPeriod = 9;
    private int _atrPeriod = 14;
    private decimal _tpMultiplier = 2m;

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("FastPeriod", "Schnelle MACD Periode", "int", _fastPeriod, 5, 20, 1),
        new("SlowPeriod", "Langsame MACD Periode", "int", _slowPeriod, 15, 50, 1),
        new("SignalPeriod", "Signal-Linie Periode", "int", _signalPeriod, 3, 15, 1),
        new("AtrPeriod", "ATR Periode für Stop-Loss", "int", _atrPeriod, 5, 50, 1),
        new("TpMultiplier", "Take-Profit Multiplikator", "decimal", _tpMultiplier, 1m, 5m, 0.5m),
    };

    public SignalResult Evaluate(MarketContext context)
    {
        var candles = context.Candles;
        // MACD braucht slowPeriod + signalPeriod + 3 extra für Histogram-Momentum
        if (candles.Count < _slowPeriod + _signalPeriod + 5)
            return new SignalResult(Signal.None, 0m, null, null, null, "Zu wenig Daten");

        var (macd, signal, histogram) = IndicatorHelper.CalculateMacd(candles, _fastPeriod, _slowPeriod, _signalPeriod);
        var atr = IndicatorHelper.CalculateAtr(candles, _atrPeriod);

        var lastMacd = macd[^1];
        var lastHist = histogram[^1];
        var prevHist = histogram[^2];
        var prevPrevHist = histogram.Count >= 3 ? histogram[^3] : null;
        var prevMacd = macd[^2];
        var lastAtr = atr[^1];

        if (lastMacd == null || lastHist == null || prevHist == null || prevMacd == null || lastAtr == null)
            return new SignalResult(Signal.None, 0m, null, null, null, "Indikatoren nicht bereit");

        var currentPrice = context.CurrentTicker.LastPrice;
        var atrValue = lastAtr.Value;

        // 1. Zero-Line-Cross (stärkstes Signal): MACD kreuzt die Null-Linie
        if (prevMacd <= 0 && lastMacd > 0)
        {
            // Bullish: MACD von negativ zu positiv
            var sl = currentPrice - atrValue * 2m;
            var tp = currentPrice + atrValue * 2m * _tpMultiplier;
            return new SignalResult(Signal.Long, 0.9m, currentPrice, sl, tp,
                $"MACD Zero-Line-Cross bullish (MACD: {lastMacd.Value:F4})");
        }

        if (prevMacd >= 0 && lastMacd < 0)
        {
            // Bearish: MACD von positiv zu negativ
            var sl = currentPrice + atrValue * 2m;
            var tp = currentPrice - atrValue * 2m * _tpMultiplier;
            return new SignalResult(Signal.Short, 0.9m, currentPrice, sl, tp,
                $"MACD Zero-Line-Cross bearish (MACD: {lastMacd.Value:F4})");
        }

        // 2. Histogram-Momentum: Wechsel + 2 aufeinanderfolgende steigende/fallende Balken
        var histogramRising = lastHist > prevHist;
        var histogramConsecutiveRising = prevPrevHist != null && prevHist > prevPrevHist && lastHist > prevHist;
        var histogramFalling = lastHist < prevHist;
        var histogramConsecutiveFalling = prevPrevHist != null && prevHist < prevPrevHist && lastHist < prevHist;

        // Long: Histogram wechselt von negativ zu positiv UND steigt konsekutiv
        if (prevHist <= 0 && lastHist > 0 && histogramRising)
        {
            // Trend-Kontext: Nur Long wenn MACD > 0 (Aufwärtstrend) oder gerade aufbauend
            if (lastMacd >= 0 || histogramConsecutiveRising)
            {
                var confidence = lastMacd > 0 ? 0.8m : 0.7m;
                if (histogramConsecutiveRising) confidence += 0.05m;

                var sl = currentPrice - atrValue * 1.5m;
                var tp = currentPrice + atrValue * 1.5m * _tpMultiplier;
                return new SignalResult(Signal.Long, Math.Min(1m, confidence), currentPrice, sl, tp,
                    $"MACD Histogram bullish (Hist: {lastHist.Value:F4}, MACD: {lastMacd.Value:F4})");
            }
        }

        // Short: Histogram wechselt von positiv zu negativ UND fällt konsekutiv
        if (prevHist >= 0 && lastHist < 0 && histogramFalling)
        {
            // Trend-Kontext: Nur Short wenn MACD < 0 (Abwärtstrend) oder gerade abbauend
            if (lastMacd <= 0 || histogramConsecutiveFalling)
            {
                var confidence = lastMacd < 0 ? 0.8m : 0.7m;
                if (histogramConsecutiveFalling) confidence += 0.05m;

                var sl = currentPrice + atrValue * 1.5m;
                var tp = currentPrice - atrValue * 1.5m * _tpMultiplier;
                return new SignalResult(Signal.Short, Math.Min(1m, confidence), currentPrice, sl, tp,
                    $"MACD Histogram bearish (Hist: {lastHist.Value:F4}, MACD: {lastMacd.Value:F4})");
            }
        }

        return new SignalResult(Signal.None, 0m, null, null, null,
            $"Kein MACD-Signal (MACD: {lastMacd.Value:F4}, Hist: {lastHist.Value:F4})");
    }

    public void WarmUp(IReadOnlyList<Candle> history)
    {
        if (history.Count < _slowPeriod + _signalPeriod + 5) return;
        IndicatorHelper.CalculateMacd(history, _fastPeriod, _slowPeriod, _signalPeriod);
        IndicatorHelper.CalculateAtr(history, _atrPeriod);
    }
    public void Reset() { }

    public IStrategy Clone() => new MacdStrategy
    {
        _fastPeriod = _fastPeriod,
        _slowPeriod = _slowPeriod,
        _signalPeriod = _signalPeriod,
        _atrPeriod = _atrPeriod,
        _tpMultiplier = _tpMultiplier
    };
}
