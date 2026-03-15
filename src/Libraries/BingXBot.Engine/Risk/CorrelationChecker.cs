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
        var newKlines = await client.GetKlinesAsync(newSymbol, TimeFrame.H1, 100).ConfigureAwait(false);
        if (newKlines.Count < 20) return false;

        var newPrices = newKlines.Select(k => k.Close).ToArray();

        foreach (var pos in openPositions)
        {
            if (pos.Symbol == newSymbol) continue;

            var existingKlines = await client.GetKlinesAsync(pos.Symbol, TimeFrame.H1, 100).ConfigureAwait(false);
            if (existingKlines.Count < 20) continue;

            var existingPrices = existingKlines.Select(k => k.Close).ToArray();
            var minLength = Math.Min(newPrices.Length, existingPrices.Length);

            // ArraySegment statt TakeLast+ToArray (vermeidet Allokation)
            var newSlice = new ArraySegment<decimal>(newPrices, newPrices.Length - minLength, minLength).ToArray();
            var existingSlice = new ArraySegment<decimal>(existingPrices, existingPrices.Length - minLength, minLength).ToArray();
            var correlation = CalculatePearson(newSlice, existingSlice);

            if (Math.Abs(correlation) > maxCorrelation)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Pearson-Korrelationskoeffizient zwischen zwei Dezimal-Reihen.
    /// Berechnung in double um Overflow bei extremen Preisen (z.B. BTC) zu vermeiden.
    /// </summary>
    public static decimal CalculatePearson(decimal[] x, decimal[] y)
    {
        if (x.Length != y.Length || x.Length < 2) return 0m;

        var n = x.Length;
        // In double rechnen um Overflow bei decimal-Multiplikation zu vermeiden
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
        for (int i = 0; i < n; i++)
        {
            var xi = (double)x[i];
            var yi = (double)y[i];
            sumX += xi;
            sumY += yi;
            sumXY += xi * yi;
            sumX2 += xi * xi;
            sumY2 += yi * yi;
        }

        var numerator = n * sumXY - sumX * sumY;
        var denominatorX = n * sumX2 - sumX * sumX;
        var denominatorY = n * sumY2 - sumY * sumY;
        var denominator = Math.Sqrt(denominatorX * denominatorY);

        if (denominator == 0) return 0m;
        return (decimal)(numerator / denominator);
    }
}
