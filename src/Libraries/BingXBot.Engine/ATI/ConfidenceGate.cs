using System.Collections.Concurrent;
using System.Text.Json;
using BingXBot.Core.Models.ATI;

namespace BingXBot.Engine.ATI;

/// <summary>
/// ML-basierter Confidence-Filter der aus Trade-Ergebnissen lernt.
/// Phase 1: Bayesian Naive Bayes auf diskretisierten Feature-Buckets (online, kein Training nötig).
/// Phase 2: ML.NET LightGBM (batch-Training, optional wenn genug Daten).
/// </summary>
public class ConfidenceGate
{
    // Bayesian Bucket-Tracker: Key = Feature-Bucket-String, Value = (Wins, Losses)
    private readonly ConcurrentDictionary<string, BucketStats> _bucketStats = new();

    // Gesamt-Statistik (Prior für Bayesian Smoothing)
    private int _totalWins;
    private int _totalLosses;
    private readonly object _statsLock = new();

    /// <summary>Schwellwert: Unter dieser Confidence wird der Trade abgelehnt (default: 0.45).</summary>
    public decimal Threshold { get; set; } = 0.45m;

    /// <summary>Mindestanzahl Trades in einem Bucket bevor er gewichtet wird.</summary>
    public int MinBucketSamples { get; set; } = 5;

    /// <summary>
    /// Mindestanzahl Gesamt-Trades bevor Confidence Gate als Filter aktiv wird.
    /// Unter dieser Schwelle gibt Evaluate() immer (Prior, true) zurück → kein Filtern,
    /// nur Datensammlung. Schützt gegen schlechte Entscheidungen mit zu wenig Daten.
    /// </summary>
    public int MinTradesBeforeLearning { get; set; } = 20;

    /// <summary>
    /// Bewertet die Erfolgswahrscheinlichkeit basierend auf gelernten Mustern.
    /// </summary>
    public (decimal Confidence, bool ShouldTrade) Evaluate(
        FeatureSnapshot features, MarketRegime regime, EnsembleVote ensemble)
    {
        // Cold-Start-Schutz: Unter MinTrades wird nicht gefiltert.
        // Das System sammelt nur Daten, lehnt aber keine Trades ab.
        int totalTrades;
        lock (_statsLock) { totalTrades = _totalWins + _totalLosses; }
        if (totalTrades < MinTradesBeforeLearning)
        {
            var prior = (decimal)GetPriorWinRate();
            return (prior, true); // Immer durchlassen, nur Daten sammeln
        }

        // Feature-Buckets diskretisieren
        var buckets = DiscretizeFeatures(features, regime, ensemble);

        // Bayesian Naive Bayes: P(Win | bucket1, bucket2, ...) ∝ ∏ P(Win | bucket_i)
        var logOddsSum = 0.0;
        var bucketCount = 0;

        foreach (var bucket in buckets)
        {
            if (!_bucketStats.TryGetValue(bucket, out var stats)) continue;

            int wins, losses;
            lock (stats) { wins = stats.Wins; losses = stats.Losses; }

            var total = wins + losses;
            if (total < MinBucketSamples) continue;

            // Bayesian Smoothing: P(Win | bucket) mit Laplace-Glättung
            var prior = GetPriorWinRate();
            var smoothedWinRate = (wins + prior * 2) / (total + 2); // Pseudo-Count = 2

            // Log-Odds akkumulieren (vermeidet Floating-Point-Underflow bei vielen Buckets)
            var odds = smoothedWinRate / (1.0 - smoothedWinRate + 1e-10);
            logOddsSum += Math.Log(odds);
            bucketCount++;
        }

        decimal confidence;
        if (bucketCount == 0)
        {
            // Keine gelernten Daten → nutze Prior (Gesamt-WinRate)
            confidence = (decimal)GetPriorWinRate();
        }
        else
        {
            // H-5 Fix: Prior-Term einbeziehen + Log-Odds SUMMIEREN (nicht mitteln).
            // Naive Bayes: log P(Win|B1..Bn) = log P(Win) + Σ log(P(Bi|Win)/P(Bi|Loss))
            // Mitteln schwächt den Effekt ab und ist mathematisch falsch.
            var priorWinRate = GetPriorWinRate();
            var priorLogOdds = Math.Log(priorWinRate / (1.0 - priorWinRate + 1e-10));
            var totalLogOdds = priorLogOdds + logOddsSum;
            var probability = 1.0 / (1.0 + Math.Exp(-totalLogOdds));
            confidence = (decimal)Math.Clamp(probability, 0.01, 0.99);
        }

        // Ensemble-Konsens als zusätzlichen Faktor einbeziehen
        if (ensemble.TotalCount > 0)
        {
            var consensusRatio = (decimal)ensemble.AgreeingCount / ensemble.TotalCount;
            // 80% Bayesian, 20% Konsens-Stärke
            confidence = confidence * 0.8m + consensusRatio * 0.2m;
        }

        return (confidence, confidence >= Threshold);
    }

    /// <summary>
    /// Zeichnet ein Trade-Ergebnis auf und aktualisiert alle betroffenen Buckets.
    /// </summary>
    public void RecordOutcome(FeatureSnapshot features, MarketRegime regime, EnsembleVote ensemble, bool won)
    {
        var buckets = DiscretizeFeatures(features, regime, ensemble);

        foreach (var bucket in buckets)
        {
            var stats = _bucketStats.GetOrAdd(bucket, _ => new BucketStats());
            lock (stats)
            {
                if (won) stats.Wins++;
                else stats.Losses++;
            }
        }

        lock (_statsLock)
        {
            if (won) _totalWins++;
            else _totalLosses++;
        }
    }

    /// <summary>Gibt Statistiken über gelernte Muster zurück.</summary>
    public (int TotalTrades, int TotalWins, decimal WinRate, int BucketCount,
            List<(string Bucket, int Wins, int Losses, decimal WinRate)> TopBuckets) GetStatistics()
    {
        int totalWins, totalLosses;
        lock (_statsLock) { totalWins = _totalWins; totalLosses = _totalLosses; }

        var total = totalWins + totalLosses;
        var winRate = total > 0 ? (decimal)totalWins / total : 0.5m;

        var topBuckets = _bucketStats
            .Select(kvp =>
            {
                int w, l;
                lock (kvp.Value) { w = kvp.Value.Wins; l = kvp.Value.Losses; }
                var t = w + l;
                return (kvp.Key, w, l, t > 0 ? (decimal)w / t : 0.5m);
            })
            .Where(b => b.w + b.l >= MinBucketSamples)
            .OrderByDescending(b => b.w + b.l)
            .Take(20)
            .ToList();

        return (total, totalWins, winRate, _bucketStats.Count, topBuckets);
    }

    /// <summary>Serialisiert BucketStats + Gesamt-Statistik als JSON.</summary>
    public string SerializeState()
    {
        var data = new Dictionary<string, int[]>();
        foreach (var kvp in _bucketStats)
        {
            lock (kvp.Value) { data[kvp.Key] = new[] { kvp.Value.Wins, kvp.Value.Losses }; }
        }
        int tw, tl;
        lock (_statsLock) { tw = _totalWins; tl = _totalLosses; }
        data["__totals__"] = new[] { tw, tl };
        return JsonSerializer.Serialize(data);
    }

    /// <summary>Lädt BucketStats + Gesamt-Statistik aus JSON.</summary>
    public void DeserializeState(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, int[]>>(json);
            if (data == null) return;

            foreach (var kvp in data)
            {
                if (kvp.Key == "__totals__" && kvp.Value.Length >= 2)
                {
                    lock (_statsLock) { _totalWins = kvp.Value[0]; _totalLosses = kvp.Value[1]; }
                    continue;
                }
                if (kvp.Value.Length >= 2)
                {
                    var stats = _bucketStats.GetOrAdd(kvp.Key, _ => new BucketStats());
                    lock (stats) { stats.Wins = kvp.Value[0]; stats.Losses = kvp.Value[1]; }
                }
            }
        }
        catch { /* Korrupte Daten ignorieren */ }
    }

    /// <summary>Setzt den Classifier zurück (z.B. nach Strategie-Wechsel).</summary>
    public void Reset()
    {
        _bucketStats.Clear();
        lock (_statsLock) { _totalWins = 0; _totalLosses = 0; }
    }

    // === Feature-Diskretisierung ===

    private static List<string> DiscretizeFeatures(FeatureSnapshot f, MarketRegime regime, EnsembleVote ensemble)
    {
        var buckets = new List<string>(12);

        // Einzelne Feature-Buckets (jeder wird separat getrackt)
        buckets.Add($"RSI:{DiscretizeRsi(f.RsiNormalized)}");
        buckets.Add($"ADX:{DiscretizeAdx(f.AdxNormalized)}");
        buckets.Add($"VOL:{DiscretizeVolume(f.VolumeRatio)}");
        buckets.Add($"BB:{DiscretizeBBPosition(f.BollingerPosition)}");
        buckets.Add($"ATR:{DiscretizeAtr(f.AtrPercent)}");
        buckets.Add($"HTF:{(int)f.HtfTrend}");
        buckets.Add($"REGIME:{regime}");
        buckets.Add($"SESSION:{(int)f.SessionId}");
        buckets.Add($"CONSENSUS:{DiscretizeConsensus(ensemble)}");

        // Kombinations-Buckets (wichtige Feature-Paare)
        buckets.Add($"RSI+ADX:{DiscretizeRsi(f.RsiNormalized)}+{DiscretizeAdx(f.AdxNormalized)}");
        buckets.Add($"REGIME+VOL:{regime}+{DiscretizeVolume(f.VolumeRatio)}");
        buckets.Add($"REGIME+ADX:{regime}+{DiscretizeAdx(f.AdxNormalized)}");

        return buckets;
    }

    private static string DiscretizeRsi(float rsi)
    {
        var val = rsi * 100; // Zurück auf 0-100
        if (val < 30) return "oversold";
        if (val > 70) return "overbought";
        return "neutral";
    }

    private static string DiscretizeAdx(float adx)
    {
        var val = adx * 100;
        if (val < 20) return "weak";
        if (val > 40) return "strong";
        return "moderate";
    }

    private static string DiscretizeVolume(float ratio)
    {
        if (ratio < 0.8f) return "low";
        if (ratio > 1.5f) return "high";
        return "normal";
    }

    private static string DiscretizeBBPosition(float pos)
    {
        if (pos < 0.2f) return "lower";
        if (pos > 0.8f) return "upper";
        return "middle";
    }

    private static string DiscretizeAtr(float atrPct)
    {
        if (atrPct < 0.015f) return "low";
        if (atrPct > 0.04f) return "high";
        return "normal";
    }

    private static string DiscretizeConsensus(EnsembleVote e)
    {
        if (e.TotalCount == 0) return "none";
        var ratio = (float)e.AgreeingCount / e.TotalCount;
        if (ratio >= 0.66f) return "strong";
        if (ratio >= 0.33f) return "moderate";
        return "weak";
    }

    private double GetPriorWinRate()
    {
        lock (_statsLock)
        {
            var total = _totalWins + _totalLosses;
            return total > 0 ? (double)_totalWins / total : 0.5;
        }
    }

    private class BucketStats
    {
        public int Wins;
        public int Losses;
    }
}
