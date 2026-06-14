using BingXBot.Core.Models;

namespace BingXBot.Backtest.Reports;

/// <summary>
/// Combinatorial Purged Cross-Validation (CPCV).
/// Systematische Aufteilung der Trade-Sequenz in N Blöcke.
/// Jede Kombination von K Test-Blöcken wird gegen die verbleibenden Train-Blöcke evaluiert.
/// Purging: Trades in der Übergangszone zwischen Train und Test werden entfernt.
/// Ergebnis: Probability of Backtest Overfitting (PBO).
/// </summary>
public static class CpcvValidator
{
    /// <summary>
    /// Führt CPCV auf den gegebenen Trades durch.
    /// N = Anzahl Blöcke (default 6), K = Anzahl Test-Blöcke pro Kombination (default 2).
    /// Purge = Anzahl Trades die an Block-Grenzen entfernt werden (default 2).
    /// </summary>
    public static CpcvResult Validate(
        IReadOnlyList<CompletedTrade> trades,
        decimal initialBalance,
        int nBlocks = 6,
        int kTestBlocks = 2,
        int purgeSize = 2)
    {
        if (trades.Count < nBlocks * 5)
            return CpcvResult.Empty;

        // Trades chronologisch sortieren
        var sorted = trades.OrderBy(t => t.EntryTime).ToList();
        var blockSize = sorted.Count / nBlocks;

        // Blöcke erstellen
        var blocks = new List<List<CompletedTrade>>();
        for (int i = 0; i < nBlocks; i++)
        {
            var start = i * blockSize;
            var end = (i == nBlocks - 1) ? sorted.Count : (i + 1) * blockSize;
            blocks.Add(sorted.GetRange(start, end - start));
        }

        // Alle Kombinationen von K aus N Test-Blöcken generieren
        var combinations = GetCombinations(nBlocks, kTestBlocks);
        var oosReturns = new List<decimal>(); // Out-of-Sample Returns pro Kombination
        var isReturns = new List<decimal>();  // In-Sample Returns pro Kombination

        foreach (var testIndices in combinations)
        {
            var testTrades = new List<CompletedTrade>();
            var trainTrades = new List<CompletedTrade>();
            var testSet = new HashSet<int>(testIndices);

            for (int i = 0; i < nBlocks; i++)
            {
                if (testSet.Contains(i))
                {
                    testTrades.AddRange(blocks[i]);
                }
                else
                {
                    // Purging: Entferne Trades an den Grenzen zu Test-Blöcken
                    var block = blocks[i];
                    var purgedBlock = block;

                    if (purgeSize > 0 && block.Count > purgeSize * 2)
                    {
                        // Prüfe ob dieser Block an einen Test-Block grenzt
                        var nextIsTest = i + 1 < nBlocks && testSet.Contains(i + 1);
                        var prevIsTest = i - 1 >= 0 && testSet.Contains(i - 1);

                        var startPurge = prevIsTest ? purgeSize : 0;
                        var endPurge = nextIsTest ? purgeSize : 0;

                        if (startPurge + endPurge < block.Count)
                            purgedBlock = block.GetRange(startPurge, block.Count - startPurge - endPurge);
                    }

                    trainTrades.AddRange(purgedBlock);
                }
            }

            // In-Sample und Out-of-Sample Performance berechnen
            var isReturn = CalculateReturn(trainTrades, initialBalance);
            var oosReturn = CalculateReturn(testTrades, initialBalance);

            isReturns.Add(isReturn);
            oosReturns.Add(oosReturn);
        }

        // PBO berechnen: Anteil der Kombinationen wo OOS-Return <= 0
        // Wenn viele Kombinationen im OOS negativ sind → hohes Overfitting-Risiko
        var totalCombinations = oosReturns.Count;
        var negativeOos = oosReturns.Count(r => r <= 0);

        // Degradation: Wie viel Performance geht von IS zu OOS verloren?
        var avgIsReturn = isReturns.Count > 0 ? isReturns.Average() : 0m;
        var avgOosReturn = oosReturns.Count > 0 ? oosReturns.Average() : 0m;
        var degradation = avgIsReturn != 0 ? (1 - avgOosReturn / avgIsReturn) * 100m : 100m;

        return new CpcvResult
        {
            Combinations = totalCombinations,
            Blocks = nBlocks,
            TestBlocksPerCombination = kTestBlocks,
            PurgeSize = purgeSize,
            // PBO: Wahrscheinlichkeit dass der Backtest overfitted ist (0-100%)
            ProbabilityOfOverfitting = totalCombinations > 0 ? (decimal)negativeOos / totalCombinations * 100m : 0m,
            AvgInSampleReturn = avgIsReturn,
            AvgOutOfSampleReturn = avgOosReturn,
            // Degradation: Wie viel % Performance geht von IS zu OOS verloren
            Degradation = Math.Clamp(degradation, 0m, 100m),
            OosReturns = oosReturns
        };
    }

    private static decimal CalculateReturn(List<CompletedTrade> trades, decimal initialBalance)
    {
        if (trades.Count == 0) return 0m;
        var totalPnl = trades.Sum(t => t.Pnl);
        return initialBalance > 0 ? totalPnl / initialBalance * 100m : 0m;
    }

    /// <summary>Generiert alle Kombinationen von K aus N (C(N,K)).</summary>
    private static List<int[]> GetCombinations(int n, int k)
    {
        var result = new List<int[]>();
        var combo = new int[k];
        GenerateCombinations(result, combo, 0, 0, n, k);
        return result;
    }

    private static void GenerateCombinations(List<int[]> result, int[] combo, int start, int depth, int n, int k)
    {
        if (depth == k)
        {
            result.Add((int[])combo.Clone());
            return;
        }
        for (int i = start; i < n; i++)
        {
            combo[depth] = i;
            GenerateCombinations(result, combo, i + 1, depth + 1, n, k);
        }
    }
}

/// <summary>Ergebnis einer CPCV-Validation.</summary>
public class CpcvResult
{
    public int Combinations { get; init; }
    public int Blocks { get; init; }
    public int TestBlocksPerCombination { get; init; }
    public int PurgeSize { get; init; }

    /// <summary>Wahrscheinlichkeit dass der Backtest overfitted ist (0-100%). Unter 30% = akzeptabel.</summary>
    public decimal ProbabilityOfOverfitting { get; init; }

    /// <summary>Durchschnittlicher In-Sample-Return (%).</summary>
    public decimal AvgInSampleReturn { get; init; }

    /// <summary>Durchschnittlicher Out-of-Sample-Return (%).</summary>
    public decimal AvgOutOfSampleReturn { get; init; }

    /// <summary>Performance-Degradation von IS zu OOS in %. Unter 30% = akzeptabel.</summary>
    public decimal Degradation { get; init; }

    /// <summary>Alle OOS-Returns der einzelnen Kombinationen.</summary>
    public List<decimal> OosReturns { get; init; } = new();

    public static CpcvResult Empty => new() { Combinations = 0 };
    public bool IsEmpty => Combinations == 0;
}
