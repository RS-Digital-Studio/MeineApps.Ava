using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Breakout-Pullback-Strategie (Krypto-optimiert).
/// Erkennt Breakouts über Donchian-Channel und wartet auf Pullback zum Breakout-Level.
/// Entry beim Retest mit Volume-Konfirmation. Komplementär zu CryptoTrendPro
/// (Breakout-Entry vs. Trend-Continuation).
///
/// Phasen:
/// 1. Breakout: Preis bricht über Donchian-Upper/unter Donchian-Lower aus
/// 2. Pullback: Preis kehrt zum Breakout-Level zurück (± 0.5*ATR Toleranz)
/// 3. Entry: Retest mit Volume > SMA bestätigt
///
/// Krypto-Optimierungen:
/// - ADX > 15 (Breakout braucht Mindest-Trend, aber weniger als Trend-Following)
/// - Volume-Spike beim Breakout (min 1.2x Durchschnitt)
/// - ATR-basierte SL/TP (unter Pullback-Low/über Pullback-High)
/// </summary>
public class BreakoutPullbackStrategy : IStrategy
{
    public string Name => "Breakout-Pullback";
    public string Description => "Donchian-Breakout + Pullback-Retest mit Volume-Konfirmation (Krypto-optimiert)";

    private int _donchianPeriod = 20;
    private int _atrPeriod = 14;
    private int _volumePeriod = 20;
    private decimal _volumeBreakoutMultiplier = 1.2m;
    private decimal _volumeRetestMultiplier = 1.0m;
    private decimal _pullbackToleranceAtr = 0.5m;
    private decimal _tpMultiplier = 2.0m;
    private decimal _minAdx = 15m;
    private int _adxPeriod = 14;
    private int _maxPullbackCandles = 10; // Max Kerzen die der Pullback dauern darf

    // Breakout-State pro Symbol (wird per WarmUp/Reset zurückgesetzt)
    private decimal _lastBreakoutLevel;
    private Side _breakoutDirection;
    private int _candlesSinceBreakout;
    private bool _breakoutActive;
    private decimal _breakoutVolume; // Volume beim Breakout

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("DonchianPeriod", "Donchian-Channel Periode", "int", _donchianPeriod, 10, 50, 5),
        new("AtrPeriod", "ATR Periode für SL/TP", "int", _atrPeriod, 5, 30, 1),
        new("VolumePeriod", "Volumen-SMA Periode", "int", _volumePeriod, 10, 50, 5),
        new("PullbackTolerance", "Pullback-Toleranz (ATR-Vielfaches)", "decimal", _pullbackToleranceAtr, 0.2m, 1.5m, 0.1m),
        new("TpMultiplier", "Take-Profit Multiplikator (Donchian-Range)", "decimal", _tpMultiplier, 1.0m, 4.0m, 0.5m),
        new("MinAdx", "Min. ADX für Breakout", "decimal", _minAdx, 10m, 25m, 5m),
    };

    public SignalResult Evaluate(MarketContext context)
    {
        var candles = context.Candles;
        if (candles.Count < _donchianPeriod + 10)
            return NoSignal("Zu wenig Daten");

        var currentPrice = context.CurrentTicker.LastPrice;
        var currentClose = candles[^1].Close;
        var prevClose = candles[^2].Close;
        var currentVolume = candles[^1].Volume;

        // Indikatoren
        var (dcUpper, dcLower, dcMiddle) = IndicatorHelper.CalculateDonchian(candles, _donchianPeriod);
        var atr = IndicatorHelper.CalculateAtr(candles, _atrPeriod);
        var volumeSma = IndicatorHelper.CalculateVolumeSma(candles, _volumePeriod);
        var adx = IndicatorHelper.CalculateAdx(candles, _adxPeriod);

        var lastUpper = dcUpper[^2]; // Vorherige Kerze (aktuelle kann noch offen sein)
        var lastLower = dcLower[^2];
        var lastAtr = atr[^1];
        var lastVolSma = volumeSma[^1];
        var lastAdx = adx[^1];

        if (lastUpper == null || lastLower == null || lastAtr == null || lastVolSma == null || lastAdx == null)
            return NoSignal("Indikatoren nicht bereit");

        var atrValue = lastAtr.Value;
        if (atrValue <= 0)
            return NoSignal("ATR ist 0");

        // ADX-Filter: Mindest-Trendstärke für Breakout
        if (lastAdx.Value < _minAdx)
        {
            _breakoutActive = false;
            return NoSignal($"ADX {lastAdx.Value:F0} < {_minAdx} (zu schwach für Breakout)");
        }

        var donchianRange = lastUpper.Value - lastLower.Value;
        if (donchianRange <= 0)
            return NoSignal("Donchian-Range ist 0");

        // ═══ Phase 1: Breakout erkennen ═══
        if (!_breakoutActive)
        {
            // Bullish Breakout: Close über Donchian-Upper der vorherigen Kerze
            if (prevClose <= lastUpper.Value && currentClose > lastUpper.Value
                && currentVolume > lastVolSma.Value * _volumeBreakoutMultiplier)
            {
                _breakoutActive = true;
                _breakoutDirection = Side.Buy;
                _lastBreakoutLevel = lastUpper.Value;
                _candlesSinceBreakout = 0;
                _breakoutVolume = currentVolume;
                return NoSignal($"Bullish Breakout erkannt bei {lastUpper.Value:F8} - warte auf Pullback");
            }

            // Bearish Breakout: Close unter Donchian-Lower
            if (prevClose >= lastLower.Value && currentClose < lastLower.Value
                && currentVolume > lastVolSma.Value * _volumeBreakoutMultiplier)
            {
                _breakoutActive = true;
                _breakoutDirection = Side.Sell;
                _lastBreakoutLevel = lastLower.Value;
                _candlesSinceBreakout = 0;
                _breakoutVolume = currentVolume;
                return NoSignal($"Bearish Breakout erkannt bei {lastLower.Value:F8} - warte auf Pullback");
            }

            return NoSignal("Kein Breakout");
        }

        // ═══ Phase 2: Pullback + Retest ═══
        _candlesSinceBreakout++;

        // Timeout: Pullback dauert zu lange → Breakout verfallen lassen
        if (_candlesSinceBreakout > _maxPullbackCandles)
        {
            _breakoutActive = false;
            return NoSignal($"Breakout-Timeout nach {_maxPullbackCandles} Kerzen ohne Retest");
        }

        var pullbackTolerance = atrValue * _pullbackToleranceAtr;

        if (_breakoutDirection == Side.Buy)
        {
            // Bullish: Preis muss zum Breakout-Level zurückkommen
            var distToBreakout = currentPrice - _lastBreakoutLevel;
            if (distToBreakout >= -pullbackTolerance && distToBreakout <= pullbackTolerance)
            {
                // Retest! Volume-Konfirmation prüfen
                if (currentVolume > lastVolSma.Value * _volumeRetestMultiplier)
                {
                    _breakoutActive = false;

                    var sl = _lastBreakoutLevel - atrValue * 1.5m;
                    var tp = currentPrice + donchianRange * _tpMultiplier;
                    var confidence = 0.8m;
                    // Höhere Confidence bei stärkerem Breakout-Volumen
                    if (_breakoutVolume > lastVolSma.Value * 2m) confidence = 0.9m;

                    return new SignalResult(Signal.Long, confidence, currentPrice, sl, tp,
                        $"Breakout-Pullback Long: Retest bei {_lastBreakoutLevel:F8} bestätigt (Vol OK)");
                }
                return NoSignal("Pullback zum Breakout-Level, aber Volumen zu niedrig");
            }

            // Preis ist zu weit weg vom Breakout-Level → warten
            if (distToBreakout > pullbackTolerance * 3)
            {
                // Preis läuft ohne Pullback weiter → kein Entry (zu spät)
                _breakoutActive = false;
                return NoSignal("Preis zu weit vom Breakout-Level (kein Pullback)");
            }
        }
        else // Bearish
        {
            var distToBreakout = _lastBreakoutLevel - currentPrice;
            if (distToBreakout >= -pullbackTolerance && distToBreakout <= pullbackTolerance)
            {
                if (currentVolume > lastVolSma.Value * _volumeRetestMultiplier)
                {
                    _breakoutActive = false;

                    var sl = _lastBreakoutLevel + atrValue * 1.5m;
                    var tp = currentPrice - donchianRange * _tpMultiplier;
                    var confidence = 0.8m;
                    if (_breakoutVolume > lastVolSma.Value * 2m) confidence = 0.9m;

                    return new SignalResult(Signal.Short, confidence, currentPrice, sl, tp,
                        $"Breakout-Pullback Short: Retest bei {_lastBreakoutLevel:F8} bestätigt (Vol OK)");
                }
                return NoSignal("Pullback zum Breakout-Level, aber Volumen zu niedrig");
            }

            if (distToBreakout > pullbackTolerance * 3)
            {
                _breakoutActive = false;
                return NoSignal("Preis zu weit vom Breakout-Level (kein Pullback)");
            }
        }

        return NoSignal($"Warte auf Pullback zum Breakout-Level ({_lastBreakoutLevel:F8}, Kerze {_candlesSinceBreakout}/{_maxPullbackCandles})");
    }

    private static SignalResult NoSignal(string reason) =>
        new(Signal.None, 0m, null, null, null, reason);

    public void WarmUp(IReadOnlyList<Candle> history)
    {
        if (history.Count < _donchianPeriod + 10) return;
        IndicatorHelper.CalculateDonchian(history, _donchianPeriod);
        IndicatorHelper.CalculateAtr(history, _atrPeriod);
        IndicatorHelper.CalculateVolumeSma(history, _volumePeriod);
        IndicatorHelper.CalculateAdx(history, _adxPeriod);
    }

    public void Reset()
    {
        _breakoutActive = false;
        _candlesSinceBreakout = 0;
        _lastBreakoutLevel = 0;
    }

    public IStrategy Clone() => new BreakoutPullbackStrategy
    {
        _donchianPeriod = _donchianPeriod,
        _atrPeriod = _atrPeriod,
        _volumePeriod = _volumePeriod,
        _volumeBreakoutMultiplier = _volumeBreakoutMultiplier,
        _volumeRetestMultiplier = _volumeRetestMultiplier,
        _pullbackToleranceAtr = _pullbackToleranceAtr,
        _tpMultiplier = _tpMultiplier,
        _minAdx = _minAdx,
        _adxPeriod = _adxPeriod,
        _maxPullbackCandles = _maxPullbackCandles
    };
}
