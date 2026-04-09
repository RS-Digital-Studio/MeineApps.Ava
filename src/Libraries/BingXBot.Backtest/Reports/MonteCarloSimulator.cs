using BingXBot.Core.Models;

namespace BingXBot.Backtest.Reports;

/// <summary>
/// Monte Carlo Simulation: Randomisiert Trade-Reihenfolge und generiert
/// Konfidenz-Intervalle für Drawdown, Returns und Ruin-Wahrscheinlichkeit.
/// </summary>
public static class MonteCarloSimulator
{
    /// <summary>
    /// Führt eine Monte Carlo Simulation mit N Durchläufen auf den gegebenen Trades durch.
    /// Jeder Durchlauf shuffled die Trade-Reihenfolge und berechnet eine Equity-Kurve.
    /// </summary>
    public static MonteCarloResult Simulate(IReadOnlyList<CompletedTrade> trades, decimal initialBalance, int iterations = 1000)
    {
        if (trades.Count < 5)
            return MonteCarloResult.Empty;

        var rng = new Random(42); // Reproduzierbar
        var pnls = trades.Select(t => t.Pnl).ToArray();
        var maxDrawdowns = new decimal[iterations];
        var finalReturns = new decimal[iterations];
        var ruinCount = 0;
        var ruinThreshold = initialBalance * -0.5m; // Ruin = 50% Verlust

        for (int i = 0; i < iterations; i++)
        {
            // Fisher-Yates Shuffle
            var shuffled = (decimal[])pnls.Clone();
            for (int j = shuffled.Length - 1; j > 0; j--)
            {
                var k = rng.Next(j + 1);
                (shuffled[j], shuffled[k]) = (shuffled[k], shuffled[j]);
            }

            // Equity-Kurve berechnen
            var equity = initialBalance;
            var peak = equity;
            var maxDd = 0m;
            var hitRuin = false;

            foreach (var pnl in shuffled)
            {
                equity += pnl;
                if (equity > peak) peak = equity;
                var dd = peak - equity;
                if (dd > maxDd) maxDd = dd;
                if (equity - initialBalance <= ruinThreshold) hitRuin = true;
            }

            maxDrawdowns[i] = peak > 0 ? maxDd / peak * 100m : 0m; // Max DD in %
            finalReturns[i] = (equity - initialBalance) / initialBalance * 100m; // Return in %
            if (hitRuin) ruinCount++;
        }

        // Sortieren für Perzentil-Berechnung
        Array.Sort(maxDrawdowns);
        Array.Sort(finalReturns);

        return new MonteCarloResult
        {
            Iterations = iterations,
            TradeCount = trades.Count,
            // Drawdown-Konfidenz: 95. Perzentil = "In 95% der Fälle ist der Max-DD nicht schlimmer als X%"
            MaxDrawdown50 = maxDrawdowns[iterations / 2],
            MaxDrawdown95 = maxDrawdowns[Math.Min(iterations - 1, (int)(iterations * 0.95))],
            MaxDrawdown99 = maxDrawdowns[Math.Min(iterations - 1, (int)(iterations * 0.99))],
            // Return-Konfidenz: 5. Perzentil = "In 95% der Fälle ist der Return mindestens X%"
            Return5 = finalReturns[Math.Max(0, (int)(iterations * 0.05) - 1)],
            Return50 = finalReturns[iterations / 2],
            Return95 = finalReturns[Math.Min(iterations - 1, (int)(iterations * 0.95))],
            // Ruin-Wahrscheinlichkeit
            RuinProbability = (decimal)ruinCount / iterations * 100m
        };
    }
}

/// <summary>
/// Ergebnis einer Monte Carlo Simulation mit Konfidenz-Intervallen.
/// </summary>
public class MonteCarloResult
{
    public int Iterations { get; init; }
    public int TradeCount { get; init; }

    // Max Drawdown Perzentile (in %)
    public decimal MaxDrawdown50 { get; init; }
    public decimal MaxDrawdown95 { get; init; }
    public decimal MaxDrawdown99 { get; init; }

    // Return Perzentile (in %)
    public decimal Return5 { get; init; }
    public decimal Return50 { get; init; }
    public decimal Return95 { get; init; }

    /// <summary>Wahrscheinlichkeit den Ruin-Schwellenwert (50% Verlust) zu erreichen, in %.</summary>
    public decimal RuinProbability { get; init; }

    public static MonteCarloResult Empty => new()
    {
        Iterations = 0, TradeCount = 0
    };

    public bool IsEmpty => Iterations == 0;
}
