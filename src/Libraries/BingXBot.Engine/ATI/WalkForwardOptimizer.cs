using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using GeneticSharp;

namespace BingXBot.Engine.ATI;

/// <summary>
/// Walk-Forward Parameter-Optimierung: Optimiert Strategie-Parameter rollierend
/// über historische Daten mit genetischem Algorithmus.
/// Train-Fenster → Optimierung → Test-Fenster → Verschieben → Wiederholen.
/// </summary>
public class WalkForwardOptimizer
{
    /// <summary>Ergebnis eines Walk-Forward-Durchlaufs.</summary>
    public record WalkForwardResult(
        IReadOnlyList<WalkForwardWindow> Windows,
        decimal AggregatedSharpe,
        decimal AggregatedProfitFactor,
        decimal AggregatedWinRate,
        IReadOnlyDictionary<string, object> BestParameters);

    /// <summary>Ein einzelnes Walk-Forward-Fenster.</summary>
    public record WalkForwardWindow(
        DateTime TrainStart, DateTime TrainEnd,
        DateTime TestStart, DateTime TestEnd,
        decimal InSampleSharpe,
        decimal OutOfSampleSharpe,
        decimal OutOfSamplePnl,
        IReadOnlyDictionary<string, object> OptimizedParameters);

    /// <summary>Fitness-Funktion: Bewertet eine Parameter-Kombination via Backtest.</summary>
    public delegate decimal FitnessFunction(IStrategy strategy, IReadOnlyList<Candle> candles);

    // GA-Einstellungen
    /// <summary>Populationsgröße pro Generation.</summary>
    public int PopulationSize { get; set; } = 50;
    /// <summary>Maximale Generationen pro Fenster.</summary>
    public int MaxGenerations { get; set; } = 30;
    /// <summary>Verhältnis Train:Test (default: 2:1).</summary>
    public int TrainTestRatio { get; set; } = 2;

    /// <summary>
    /// Führt eine Walk-Forward-Optimierung durch.
    /// </summary>
    /// <param name="strategy">Die zu optimierende Strategie (wird geklont pro Fenster).</param>
    /// <param name="allCandles">Vollständige historische Candle-Daten.</param>
    /// <param name="windowSizeCandles">Größe eines Test-Fensters in Candles.</param>
    /// <param name="fitnessFunc">Fitness-Funktion die eine Strategie auf Candles bewertet.</param>
    public WalkForwardResult Optimize(
        IStrategy strategy, IReadOnlyList<Candle> allCandles,
        int windowSizeCandles, FitnessFunction fitnessFunc)
    {
        var trainSize = windowSizeCandles * TrainTestRatio;
        var totalNeeded = trainSize + windowSizeCandles;

        if (allCandles.Count < totalNeeded)
            throw new ArgumentException($"Nicht genug Daten: {allCandles.Count} Candles, benötigt {totalNeeded}");

        var windows = new List<WalkForwardWindow>();
        var parameters = strategy.Parameters;

        // Fenster rollierend verschieben
        var offset = 0;
        while (offset + totalNeeded <= allCandles.Count)
        {
            // Train-Daten
            var trainCandles = allCandles.Skip(offset).Take(trainSize).ToList();
            // Test-Daten
            var testCandles = allCandles.Skip(offset + trainSize).Take(windowSizeCandles).ToList();

            // GA: Beste Parameter auf Train-Daten finden
            var (bestParams, inSampleFitness) = OptimizeWindow(strategy, parameters, trainCandles, fitnessFunc);

            // Out-of-Sample testen
            var testStrategy = strategy.Clone();
            ApplyParameters(testStrategy, parameters, bestParams);
            var outOfSampleFitness = fitnessFunc(testStrategy, testCandles);

            var paramDict = new Dictionary<string, object>();
            for (int i = 0; i < parameters.Count; i++)
                paramDict[parameters[i].Name] = bestParams[i];

            windows.Add(new WalkForwardWindow(
                trainCandles[0].OpenTime, trainCandles[^1].CloseTime,
                testCandles[0].OpenTime, testCandles[^1].CloseTime,
                inSampleFitness, outOfSampleFitness, outOfSampleFitness,
                paramDict));

            // Fenster verschieben
            offset += windowSizeCandles;
        }

        if (windows.Count == 0)
            throw new InvalidOperationException("Keine Walk-Forward-Fenster konnten berechnet werden");

        // Aggregierte Metriken
        var aggSharpe = windows.Average(w => w.OutOfSampleSharpe);
        var aggPf = windows.Average(w => w.OutOfSamplePnl);
        var aggWr = windows.Count(w => w.OutOfSamplePnl > 0) / (decimal)windows.Count;

        // Beste Parameter: die des letzten (aktuellsten) Fensters
        var bestFinalParams = windows[^1].OptimizedParameters;

        return new WalkForwardResult(windows, aggSharpe, aggPf, aggWr, bestFinalParams);
    }

    // === GA-Optimierung eines einzelnen Fensters ===

    private (object[] BestParams, decimal BestFitness) OptimizeWindow(
        IStrategy strategy, IReadOnlyList<StrategyParameter> parameters,
        IReadOnlyList<Candle> trainCandles, FitnessFunction fitnessFunc)
    {
        if (parameters.Count == 0)
        {
            // Keine Parameter → nur Fitness berechnen
            var fitness = fitnessFunc(strategy, trainCandles);
            return (Array.Empty<object>(), fitness);
        }

        // Chromosome definieren: FloatingPointChromosome mit Min/Max pro Parameter
        var minValues = new double[parameters.Count];
        var maxValues = new double[parameters.Count];
        var bits = new int[parameters.Count];
        var decimals = new int[parameters.Count];

        for (int i = 0; i < parameters.Count; i++)
        {
            var p = parameters[i];
            minValues[i] = Convert.ToDouble(p.MinValue ?? p.DefaultValue);
            maxValues[i] = Convert.ToDouble(p.MaxValue ?? p.DefaultValue);

            // Sicherstellen dass min < max
            if (minValues[i] >= maxValues[i])
            {
                minValues[i] = Convert.ToDouble(p.DefaultValue) * 0.5;
                maxValues[i] = Convert.ToDouble(p.DefaultValue) * 2.0;
            }

            bits[i] = 16; // 16 Bit Auflösung pro Parameter
            decimals[i] = p.ValueType == "int" ? 0 : 2;
        }

        var chromosome = new FloatingPointChromosome(minValues, maxValues, bits, decimals);
        var population = new Population(PopulationSize, PopulationSize * 2, chromosome);

        // Fitness: Klone Strategie, setze Parameter, führe Backtest aus
        var fitnessObj = new FuncFitness(c =>
        {
            var fc = (FloatingPointChromosome)c;
            var values = fc.ToFloatingPoints();

            var clone = strategy.Clone();
            ApplyParameters(clone, parameters, values.Select(v => (object)v).ToArray());

            try
            {
                var result = fitnessFunc(clone, trainCandles);
                return (double)result;
            }
            catch
            {
                return -1000.0; // Ungültige Parameter → schlechte Fitness
            }
        });

        var selection = new TournamentSelection();
        var crossover = new UniformCrossover();
        var mutation = new FlipBitMutation();
        var termination = new GenerationNumberTermination(MaxGenerations);

        var ga = new GeneticAlgorithm(population, fitnessObj, selection, crossover, mutation)
        {
            Termination = termination
        };

        ga.Start();

        var bestChromosome = (FloatingPointChromosome)ga.BestChromosome;
        var bestValues = bestChromosome.ToFloatingPoints();
        var bestFitness = (decimal)(ga.BestChromosome.Fitness ?? 0);

        return (bestValues.Select(v => (object)v).ToArray(), bestFitness);
    }

    /// <summary>
    /// Wendet Parameter-Werte auf eine Strategie an (per Reflection, wie StrategyViewModel).
    /// Convention: UI-Name "FastPeriod" → privates Feld "_fastPeriod".
    /// </summary>
    private static void ApplyParameters(IStrategy strategy, IReadOnlyList<StrategyParameter> parameters, object[] values)
    {
        var type = strategy.GetType();
        for (int i = 0; i < Math.Min(parameters.Count, values.Length); i++)
        {
            var fieldName = "_" + char.ToLower(parameters[i].Name[0]) + parameters[i].Name[1..];
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field == null) continue;

            try
            {
                if (field.FieldType == typeof(int))
                    field.SetValue(strategy, Convert.ToInt32(values[i]));
                else if (field.FieldType == typeof(decimal))
                    field.SetValue(strategy, Convert.ToDecimal(values[i]));
                else if (field.FieldType == typeof(double))
                    field.SetValue(strategy, Convert.ToDouble(values[i]));
                else if (field.FieldType == typeof(float))
                    field.SetValue(strategy, Convert.ToSingle(values[i]));
            }
            catch
            {
                // Parameter konnte nicht gesetzt werden → ignorieren
            }
        }
    }
}
