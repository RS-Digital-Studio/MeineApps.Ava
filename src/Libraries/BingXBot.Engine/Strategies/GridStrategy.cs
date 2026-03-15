using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Grid-Trading-Strategie.
/// Arbeitet in einem definierten Preisbereich mit gestaffelten Buy/Sell-Levels.
/// Long am unteren Grid-Level, Short am oberen.
/// </summary>
public class GridStrategy : IStrategy
{
    public string Name => "Grid";
    public string Description => "Grid-Trading: Gestaffelte Orders in definiertem Preisbereich";

    private int _gridLevels = 5;
    private decimal _gridSpacingPercent = 1.0m;
    private decimal _upperBound = 0m; // 0 = automatisch aus Candles
    private decimal _lowerBound = 0m; // 0 = automatisch aus Candles

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("GridLevels", "Anzahl Grid-Stufen", "int", _gridLevels, 2, 20, 1),
        new("GridSpacing", "Grid-Abstand in Prozent", "decimal", _gridSpacingPercent, 0.1m, 5.0m, 0.1m),
        new("UpperBound", "Obere Grenze (0 = automatisch)", "decimal", _upperBound, 0m, 1000000m, 1m),
        new("LowerBound", "Untere Grenze (0 = automatisch)", "decimal", _lowerBound, 0m, 1000000m, 1m),
    };

    public SignalResult Evaluate(MarketContext context)
    {
        var candles = context.Candles;
        if (candles.Count < 20)
            return new SignalResult(Signal.None, 0m, null, null, null, "Zu wenig Daten");

        var currentPrice = context.CurrentTicker.LastPrice;

        // Grenzen bestimmen (automatisch aus den letzten Candles oder manuell)
        var upper = _upperBound > 0 ? _upperBound : candles.TakeLast(50).Max(c => c.High);
        var lower = _lowerBound > 0 ? _lowerBound : candles.TakeLast(50).Min(c => c.Low);

        if (currentPrice < lower || currentPrice > upper)
            return new SignalResult(Signal.None, 0m, null, null, null,
                $"Preis {currentPrice:F2} außerhalb des Grids ({lower:F2} - {upper:F2})");

        // Grid-Levels berechnen
        var range = upper - lower;
        var stepSize = range / (_gridLevels + 1);
        var gridLevels = new List<decimal>();
        for (int i = 1; i <= _gridLevels; i++)
            gridLevels.Add(lower + stepSize * i);

        // Nächstes Grid-Level unter dem aktuellen Preis (Buy-Level)
        var nearestBelow = gridLevels.Where(l => l < currentPrice).OrderByDescending(l => l).FirstOrDefault();
        // Nächstes Grid-Level über dem aktuellen Preis (Sell-Level)
        var nearestAbove = gridLevels.Where(l => l > currentPrice).OrderBy(l => l).FirstOrDefault();

        // Preis-Abstand zum nächsten Level als Prozent
        var spacingThreshold = currentPrice * _gridSpacingPercent / 100m;

        // Preis nahe am unteren Grid-Level -> Long
        if (nearestBelow > 0 && currentPrice - nearestBelow < spacingThreshold)
        {
            var sl = nearestBelow - stepSize * 0.5m;
            var tp = nearestAbove > 0 ? nearestAbove : currentPrice + stepSize;
            var confidence = 0.6m + (1m - (currentPrice - nearestBelow) / spacingThreshold) * 0.3m;
            return new SignalResult(Signal.Long, Math.Min(1m, confidence), currentPrice, sl, tp,
                $"Preis nahe Grid-Level {nearestBelow:F2} (Buy-Zone)");
        }

        // Preis nahe am oberen Grid-Level -> Short
        if (nearestAbove > 0 && nearestAbove - currentPrice < spacingThreshold)
        {
            var sl = nearestAbove + stepSize * 0.5m;
            var tp = nearestBelow > 0 ? nearestBelow : currentPrice - stepSize;
            var confidence = 0.6m + (1m - (nearestAbove - currentPrice) / spacingThreshold) * 0.3m;
            return new SignalResult(Signal.Short, Math.Min(1m, confidence), currentPrice, sl, tp,
                $"Preis nahe Grid-Level {nearestAbove:F2} (Sell-Zone)");
        }

        return new SignalResult(Signal.None, 0m, null, null, null,
            $"Preis zwischen Grid-Levels (nächstes Buy: {nearestBelow:F2}, nächstes Sell: {nearestAbove:F2})");
    }

    public void WarmUp(IReadOnlyList<Candle> history) { /* Warmup-Logik bei Bedarf */ }
    public void Reset() { /* State zuruecksetzen bei Bedarf */ }

    public IStrategy Clone() => new GridStrategy
    {
        _gridLevels = _gridLevels,
        _gridSpacingPercent = _gridSpacingPercent,
        _upperBound = _upperBound,
        _lowerBound = _lowerBound
    };
}
