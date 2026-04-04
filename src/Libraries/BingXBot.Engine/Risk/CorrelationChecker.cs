using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;

namespace BingXBot.Engine.Risk;

public class CorrelationChecker
{
    /// <summary>
    /// Prüft ob ein neues Symbol zu stark mit bestehenden offenen Positionen korreliert.
    /// Nutzt IPublicMarketDataClient (kein API-Key nötig, funktioniert in Paper + Live).
    /// Klines-Calls laufen parallel für alle Positionen (statt sequentiell).
    /// Optional: Bereits geladene Klines für das neue Symbol können übergeben werden.
    /// </summary>
    public static async Task<bool> IsCorrelatedAsync(
        string newSymbol,
        IReadOnlyList<Position> openPositions,
        decimal maxCorrelation,
        IPublicMarketDataClient client,
        CancellationToken ct = default,
        IReadOnlyList<Candle>? preloadedNewSymbolKlines = null)
    {
        if (openPositions.Count == 0) return false;

        var to = DateTime.UtcNow;
        var from = to.AddHours(-100);

        // Klines für neues Symbol: Bereits geladene nutzen oder neu laden
        var newKlines = preloadedNewSymbolKlines
            ?? (IReadOnlyList<Candle>)await client.GetKlinesAsync(newSymbol, TimeFrame.H1, from, to, ct).ConfigureAwait(false);
        if (newKlines.Count < 20) return false;

        // Klines für alle offenen Positionen PARALLEL laden (statt sequentiell)
        // Positionen direkt filtern ohne Zwischen-Liste
        var klineTasks = new List<Task<List<Candle>>>();
        foreach (var pos in openPositions)
        {
            if (pos.Symbol != newSymbol)
                klineTasks.Add(client.GetKlinesAsync(pos.Symbol, TimeFrame.H1, from, to, ct));
        }
        if (klineTasks.Count == 0) return false;

        List<Candle>[] allKlines;
        try
        {
            allKlines = await Task.WhenAll(klineTasks).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }

        // Pearson-Korrelation gegen jede offene Position berechnen
        // Index-basiert statt Array-Kopien (vermeidet ToArray() + ArraySegment.ToArray())
        for (int i = 0; i < allKlines.Length; i++)
        {
            var existingKlines = allKlines[i];
            if (existingKlines.Count < 20) continue;

            var minLength = Math.Min(newKlines.Count, existingKlines.Count);
            var correlation = CalculatePearsonFromCandles(newKlines, existingKlines, minLength);

            if (Math.Abs(correlation) > maxCorrelation)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Pearson-Korrelationskoeffizient direkt auf Candle-Listen (vermeidet Array-Kopien).
    /// Liest die letzten minLength Close-Werte aus beiden Listen.
    /// </summary>
    public static decimal CalculatePearsonFromCandles(
        IReadOnlyList<Candle> x, IReadOnlyList<Candle> y, int minLength)
    {
        if (minLength < 2) return 0m;

        var xOffset = x.Count - minLength;
        var yOffset = y.Count - minLength;

        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
        for (int i = 0; i < minLength; i++)
        {
            var xi = (double)x[xOffset + i].Close;
            var yi = (double)y[yOffset + i].Close;
            sumX += xi;
            sumY += yi;
            sumXY += xi * yi;
            sumX2 += xi * xi;
            sumY2 += yi * yi;
        }

        var numerator = minLength * sumXY - sumX * sumY;
        var denominatorX = minLength * sumX2 - sumX * sumX;
        var denominatorY = minLength * sumY2 - sumY * sumY;
        var denominator = Math.Sqrt(denominatorX * denominatorY);

        if (denominator == 0) return 0m;
        return (decimal)(numerator / denominator);
    }

    /// <summary>
    /// Pearson-Korrelationskoeffizient zwischen zwei Dezimal-Arrays.
    /// Berechnung in double um Overflow bei extremen Preisen (z.B. BTC) zu vermeiden.
    /// </summary>
    public static decimal CalculatePearson(decimal[] x, decimal[] y)
    {
        if (x.Length != y.Length || x.Length < 2) return 0m;

        var n = x.Length;
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
