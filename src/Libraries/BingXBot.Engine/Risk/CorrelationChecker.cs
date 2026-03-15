using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;

namespace BingXBot.Engine.Risk;

public class CorrelationChecker
{
    /// <summary>
    /// Prüft ob ein neues Symbol zu stark mit bestehenden offenen Positionen korreliert.
    /// </summary>
    public async Task<bool> IsCorrelatedAsync(
        string newSymbol,
        IReadOnlyList<Position> openPositions,
        decimal maxCorrelation,
        IExchangeClient client)
    {
        if (openPositions.Count == 0) return false;

        // Klines für das neue Symbol laden
        var newKlines = await client.GetKlinesAsync(newSymbol, TimeFrame.H1, 100);
        if (newKlines.Count < 20) return false;

        var newPrices = newKlines.Select(k => k.Close).ToArray();

        foreach (var pos in openPositions)
        {
            if (pos.Symbol == newSymbol) continue;

            var existingKlines = await client.GetKlinesAsync(pos.Symbol, TimeFrame.H1, 100);
            if (existingKlines.Count < 20) continue;

            var existingPrices = existingKlines.Select(k => k.Close).ToArray();
            var minLength = Math.Min(newPrices.Length, existingPrices.Length);

            var correlation = CalculatePearson(
                newPrices.TakeLast(minLength).ToArray(),
                existingPrices.TakeLast(minLength).ToArray());

            if (Math.Abs(correlation) > maxCorrelation)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Pearson-Korrelationskoeffizient zwischen zwei Dezimal-Reihen.
    /// </summary>
    public static decimal CalculatePearson(decimal[] x, decimal[] y)
    {
        if (x.Length != y.Length || x.Length < 2) return 0m;

        var n = x.Length;
        var sumX = x.Sum();
        var sumY = y.Sum();
        var sumXY = x.Zip(y, (a, b) => a * b).Sum();
        var sumX2 = x.Sum(v => v * v);
        var sumY2 = y.Sum(v => v * v);

        var numerator = n * sumXY - sumX * sumY;
        var denominatorX = n * sumX2 - sumX * sumX;
        var denominatorY = n * sumY2 - sumY * sumY;
        var denominator = (decimal)Math.Sqrt((double)(denominatorX * denominatorY));

        if (denominator == 0m) return 0m;
        return numerator / denominator;
    }
}
