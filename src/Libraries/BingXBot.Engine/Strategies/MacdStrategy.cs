using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// MACD-Strategie (Trend-Following).
/// Long wenn MACD-Linie über Signal-Linie kreuzt UND Histogram positiv wird.
/// Short umgekehrt.
/// ATR-basierter Stop-Loss und Take-Profit.
/// </summary>
public class MacdStrategy : IStrategy
{
    public string Name => "MACD";
    public string Description => "Long bei bullischem MACD-Cross + positivem Histogram, Short umgekehrt";

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
        // MACD braucht slowPeriod + signalPeriod Warmup
        if (candles.Count < _slowPeriod + _signalPeriod + 5)
            return new SignalResult(Signal.None, 0m, null, null, null, "Zu wenig Daten");

        var (macd, signal, histogram) = IndicatorHelper.CalculateMacd(candles, _fastPeriod, _slowPeriod, _signalPeriod);
        var atr = IndicatorHelper.CalculateAtr(candles, _atrPeriod);

        var lastMacd = macd[^1];
        var lastSignal = signal[^1];
        var lastHist = histogram[^1];
        var prevMacd = macd[^2];
        var prevSignal = signal[^2];
        var prevHist = histogram[^2];
        var lastAtr = atr[^1];

        if (lastMacd == null || lastSignal == null || lastHist == null ||
            prevMacd == null || prevSignal == null || prevHist == null || lastAtr == null)
            return new SignalResult(Signal.None, 0m, null, null, null, "Indikatoren nicht bereit");

        var currentPrice = context.CurrentTicker.LastPrice;
        var atrValue = lastAtr.Value;

        // Bullish: MACD kreuzt über Signal UND Histogram wird positiv
        if (prevMacd <= prevSignal && lastMacd > lastSignal && lastHist > 0)
        {
            var sl = currentPrice - atrValue * 1.5m;
            var tp = currentPrice + atrValue * 1.5m * _tpMultiplier;
            return new SignalResult(Signal.Long, 0.75m, currentPrice, sl, tp,
                $"MACD kreuzt über Signal, Histogram {lastHist.Value:F4} positiv");
        }

        // Bearish: MACD kreuzt unter Signal UND Histogram wird negativ
        if (prevMacd >= prevSignal && lastMacd < lastSignal && lastHist < 0)
        {
            var sl = currentPrice + atrValue * 1.5m;
            var tp = currentPrice - atrValue * 1.5m * _tpMultiplier;
            return new SignalResult(Signal.Short, 0.75m, currentPrice, sl, tp,
                $"MACD kreuzt unter Signal, Histogram {lastHist.Value:F4} negativ");
        }

        return new SignalResult(Signal.None, 0m, null, null, null,
            $"Kein MACD-Cross (MACD: {lastMacd.Value:F4}, Signal: {lastSignal.Value:F4})");
    }

    public void WarmUp(IReadOnlyList<Candle> history) { /* Warmup-Logik bei Bedarf */ }
    public void Reset() { /* State zuruecksetzen bei Bedarf */ }

    public IStrategy Clone() => new MacdStrategy
    {
        _fastPeriod = _fastPeriod,
        _slowPeriod = _slowPeriod,
        _signalPeriod = _signalPeriod,
        _atrPeriod = _atrPeriod,
        _tpMultiplier = _tpMultiplier
    };
}
