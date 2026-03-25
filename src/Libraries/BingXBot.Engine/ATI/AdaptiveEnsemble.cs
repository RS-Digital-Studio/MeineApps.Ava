using System.Collections.Concurrent;
using System.Text.Json;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Core.Models.ATI;

namespace BingXBot.Engine.ATI;

/// <summary>
/// Adaptives Strategie-Ensemble: Evaluiert alle Strategien parallel,
/// gewichtet nach ihrer historischen Performance im aktuellen Regime.
/// Erfordert Konsens (Min-Anzahl übereinstimmender Strategien) für ein Signal.
/// </summary>
public class AdaptiveEnsemble
{
    private readonly List<IStrategy> _strategies = new();
    private readonly object _strategiesLock = new();

    // Gewichte pro Strategie pro Regime: _weights[regime][strategyName] = weight
    private readonly ConcurrentDictionary<MarketRegime, ConcurrentDictionary<string, StrategyWeight>> _weights = new();

    /// <summary>Mindestanzahl an Strategien die übereinstimmen müssen (default: 2).</summary>
    public int MinConsensus { get; set; } = 2;

    /// <summary>Registriert eine Strategie im Ensemble.</summary>
    public void RegisterStrategy(IStrategy strategy)
    {
        lock (_strategiesLock)
        {
            // Duplikate vermeiden
            if (_strategies.Any(s => s.Name == strategy.Name)) return;
            _strategies.Add(strategy);
        }
    }

    /// <summary>Entfernt alle registrierten Strategien.</summary>
    public void ClearStrategies()
    {
        lock (_strategiesLock) { _strategies.Clear(); }
    }

    /// <summary>
    /// Evaluiert alle Strategien und bildet ein gewichtetes Konsens-Signal.
    /// </summary>
    public EnsembleVote Evaluate(MarketContext context, MarketRegime regime)
    {
        List<IStrategy> strategies;
        lock (_strategiesLock) { strategies = _strategies.ToList(); }

        if (strategies.Count == 0)
            return CreateEmptyVote();

        // Alle Strategien evaluieren und Stimmen sammeln
        var votes = new List<StrategyVote>();
        foreach (var strategy in strategies)
        {
            try
            {
                var signal = strategy.Evaluate(context);
                var weight = GetWeight(regime, strategy.Name);
                votes.Add(new StrategyVote(strategy.Name, signal.Signal, signal.Confidence,
                    (decimal)weight, signal.StopLoss, signal.TakeProfit));
            }
            catch
            {
                // Fehlerhafte Strategie ignorieren, nicht den Ensemble-Prozess stoppen
            }
        }

        if (votes.Count == 0)
            return CreateEmptyVote();

        // Close-Signale prüfen: Wenn Strategien eine offene Position schließen wollen.
        // Close braucht weniger Konsens (1 reicht) - Schutz vor Verlusten hat Priorität.
        var closeLongVotes = votes.Where(v => v.Signal == Signal.CloseLong).ToList();
        var closeShortVotes = votes.Where(v => v.Signal == Signal.CloseShort).ToList();

        if (closeLongVotes.Count >= 1)
        {
            var totalW = closeLongVotes.Sum(v => v.Weight);
            var conf = totalW > 0 ? closeLongVotes.Sum(v => v.Weight * v.Confidence) / totalW : 0m;
            return new EnsembleVote(Signal.CloseLong, conf, closeLongVotes.Count, votes.Count,
                string.Join(", ", closeLongVotes.Select(v => v.StrategyName)), votes, null, null, null);
        }
        if (closeShortVotes.Count >= 1)
        {
            var totalW = closeShortVotes.Sum(v => v.Weight);
            var conf = totalW > 0 ? closeShortVotes.Sum(v => v.Weight * v.Confidence) / totalW : 0m;
            return new EnsembleVote(Signal.CloseShort, conf, closeShortVotes.Count, votes.Count,
                string.Join(", ", closeShortVotes.Select(v => v.StrategyName)), votes, null, null, null);
        }

        // Entry-Signale: Long/Short nach Konsens gruppieren (None ignorieren)
        var longVotes = votes.Where(v => v.Signal == Signal.Long).ToList();
        var shortVotes = votes.Where(v => v.Signal == Signal.Short).ToList();

        // Konsens bestimmen: Welche Richtung hat die meisten gewichteten Stimmen?
        var longWeight = longVotes.Sum(v => v.Weight * v.Confidence);
        var shortWeight = shortVotes.Sum(v => v.Weight * v.Confidence);

        List<StrategyVote> winningVotes;
        Signal consensusSignal;

        if (longWeight >= shortWeight && longVotes.Count >= MinConsensus)
        {
            winningVotes = longVotes;
            consensusSignal = Signal.Long;
        }
        else if (shortWeight > longWeight && shortVotes.Count >= MinConsensus)
        {
            winningVotes = shortVotes;
            consensusSignal = Signal.Short;
        }
        else
        {
            // Kein Konsens erreicht
            return new EnsembleVote(Signal.None, 0m, 0, votes.Count,
                "", votes, null, null, null);
        }

        // Gewichtete Confidence berechnen
        var totalWeight = winningVotes.Sum(v => v.Weight);
        var weightedConfidence = totalWeight > 0
            ? winningVotes.Sum(v => v.Weight * v.Confidence) / totalWeight
            : 0m;

        var agreeingNames = string.Join(", ", winningVotes.Select(v => v.StrategyName));

        // Bestes Entry/SL/TP aus den zustimmenden Strategien
        var bestEntry = winningVotes.Where(v => v.Signal != Signal.None)
            .Select(v => v.StopLoss).FirstOrDefault(); // Placeholder

        // SL: engster (konservativster) Stop-Loss
        decimal? bestSl = null;
        var slValues = winningVotes.Where(v => v.StopLoss.HasValue).Select(v => v.StopLoss!.Value).ToList();
        if (slValues.Count > 0)
        {
            bestSl = consensusSignal == Signal.Long
                ? slValues.Max()   // Long: höchster SL = engster
                : slValues.Min();  // Short: niedrigster SL = engster
        }

        // TP: weitestes (optimistischstes) Take-Profit
        decimal? bestTp = null;
        var tpValues = winningVotes.Where(v => v.TakeProfit.HasValue).Select(v => v.TakeProfit!.Value).ToList();
        if (tpValues.Count > 0)
        {
            bestTp = consensusSignal == Signal.Long
                ? tpValues.Max()   // Long: höchstes TP = weitestes
                : tpValues.Min();  // Short: niedrigstes TP = weitestes
        }

        return new EnsembleVote(consensusSignal, weightedConfidence,
            winningVotes.Count, votes.Count, agreeingNames, votes,
            null, bestSl, bestTp);
    }

    /// <summary>
    /// Zeichnet ein Trade-Ergebnis auf und aktualisiert das Strategie-Gewicht.
    /// Bayesian Update: Gewinn → Gewicht erhöhen, Verlust → Gewicht senken.
    /// </summary>
    public void RecordOutcome(string strategyName, MarketRegime regime, bool won)
    {
        var regimeWeights = _weights.GetOrAdd(regime, _ => new ConcurrentDictionary<string, StrategyWeight>());
        var weight = regimeWeights.GetOrAdd(strategyName, _ => new StrategyWeight());

        lock (weight)
        {
            if (won)
            {
                weight.Wins++;
                // Gewicht erhöhen (EMA-Update mit Decay)
                weight.CurrentWeight = weight.CurrentWeight * 0.9f + 0.1f * 1.2f;
            }
            else
            {
                weight.Losses++;
                // Gewicht senken
                weight.CurrentWeight = weight.CurrentWeight * 0.9f + 0.1f * 0.8f;
            }

            // Gewicht auf [0.2, 3.0] begrenzen (keine Strategie wird komplett deaktiviert)
            weight.CurrentWeight = Math.Clamp(weight.CurrentWeight, 0.2f, 3.0f);
        }
    }

    /// <summary>Gibt die aktuellen Gewichte aller Strategien in einem Regime zurück.</summary>
    public Dictionary<string, (float Weight, int Wins, int Losses)> GetStrategyWeights(MarketRegime regime)
    {
        var result = new Dictionary<string, (float, int, int)>();
        if (_weights.TryGetValue(regime, out var regimeWeights))
        {
            foreach (var kvp in regimeWeights)
            {
                lock (kvp.Value)
                {
                    result[kvp.Key] = (kvp.Value.CurrentWeight, kvp.Value.Wins, kvp.Value.Losses);
                }
            }
        }
        return result;
    }

    private float GetWeight(MarketRegime regime, string strategyName)
    {
        if (_weights.TryGetValue(regime, out var regimeWeights) &&
            regimeWeights.TryGetValue(strategyName, out var weight))
        {
            lock (weight) { return weight.CurrentWeight; }
        }
        return 1.0f; // Default-Gewicht
    }

    private static EnsembleVote CreateEmptyVote() =>
        new(Signal.None, 0m, 0, 0, "", Array.Empty<StrategyVote>(), null, null, null);

    /// <summary>Serialisiert alle Strategie-Gewichte pro Regime als JSON.</summary>
    public string SerializeState()
    {
        var data = new Dictionary<string, Dictionary<string, float[]>>();
        foreach (var regime in _weights)
        {
            var regimeData = new Dictionary<string, float[]>();
            foreach (var kvp in regime.Value)
            {
                lock (kvp.Value)
                {
                    regimeData[kvp.Key] = new[] { kvp.Value.CurrentWeight, kvp.Value.Wins, kvp.Value.Losses };
                }
            }
            data[regime.Key.ToString()] = regimeData;
        }
        return JsonSerializer.Serialize(data);
    }

    /// <summary>Lädt Strategie-Gewichte pro Regime aus JSON.</summary>
    public void DeserializeState(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, float[]>>>(json);
            if (data == null) return;

            foreach (var regimeKvp in data)
            {
                if (!Enum.TryParse<MarketRegime>(regimeKvp.Key, out var regime)) continue;
                var regimeWeights = _weights.GetOrAdd(regime, _ => new ConcurrentDictionary<string, StrategyWeight>());

                foreach (var stratKvp in regimeKvp.Value)
                {
                    if (stratKvp.Value.Length < 3) continue;
                    var weight = regimeWeights.GetOrAdd(stratKvp.Key, _ => new StrategyWeight());
                    lock (weight)
                    {
                        weight.CurrentWeight = stratKvp.Value[0];
                        weight.Wins = (int)stratKvp.Value[1];
                        weight.Losses = (int)stratKvp.Value[2];
                    }
                }
            }
        }
        catch { /* Korrupte Daten ignorieren */ }
    }

    /// <summary>Internes Gewichts-Tracking pro Strategie.</summary>
    private class StrategyWeight
    {
        public float CurrentWeight = 1.0f;
        public int Wins;
        public int Losses;
    }
}
