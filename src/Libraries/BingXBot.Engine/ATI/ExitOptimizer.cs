using System.Collections.Concurrent;
using System.Text.Json;
using BingXBot.Core.Enums;
using BingXBot.Core.Models.ATI;

namespace BingXBot.Engine.ATI;

/// <summary>
/// Adaptives Exit-Management: Lernt optimale SL/TP-Parameter
/// basierend auf Regime, Confidence und historischen Ergebnissen.
/// </summary>
public class ExitOptimizer
{
    // Trackt Exit-Ergebnisse pro Kontext: [Regime_ConfidenceLevel] → ExitStats
    private readonly ConcurrentDictionary<string, ExitStats> _exitStats = new();

    // Default-Multiplikatoren pro Regime (ATR-basiert)
    private static readonly Dictionary<MarketRegime, (float SlMultiplier, float TpMultiplier, float TrailingPercent)> _defaults = new()
    {
        [MarketRegime.TrendingBull] = (2.0f, 4.0f, 1.5f),
        [MarketRegime.TrendingBear] = (2.0f, 4.0f, 1.5f),
        [MarketRegime.Range] = (1.2f, 2.0f, 0.8f),
        [MarketRegime.Chaotic] = (1.5f, 2.5f, 1.0f) // Enger, schnell raus
    };

    /// <summary>
    /// Berechnet optimierte SL/TP/Trailing-Parameter basierend auf Regime und Confidence.
    /// </summary>
    public (decimal? StopLoss, decimal? TakeProfit, decimal TrailingPercent) OptimizeExit(
        FeatureSnapshot features, MarketRegime regime, Signal signal,
        decimal atr, decimal entryPrice, decimal ensembleConfidence)
    {
        if (atr <= 0 || entryPrice <= 0)
            return (null, null, 1.5m);

        // Basis-Multiplikatoren aus gelernten Daten oder Defaults
        var (slMult, tpMult, trailingPct) = GetMultipliers(regime, ensembleConfidence);

        // Confidence-basierte Anpassung:
        // Hohe Confidence → etwas weiter SL (mehr Spielraum), etwas weiter TP (mehr Potential)
        // Niedrige Confidence → enger SL (weniger Risiko), enger TP (schneller rausnehmen)
        var confFactor = (float)ensembleConfidence;
        slMult *= (0.8f + confFactor * 0.4f); // Bei Confidence 0.5 → 1.0x, bei 1.0 → 1.2x
        tpMult *= (0.7f + confFactor * 0.6f); // Bei Confidence 0.5 → 1.0x, bei 1.0 → 1.3x

        // SL/TP berechnen
        decimal? sl, tp;
        if (signal == Signal.Long)
        {
            sl = entryPrice - atr * (decimal)slMult;
            tp = entryPrice + atr * (decimal)tpMult;
        }
        else // Short
        {
            sl = entryPrice + atr * (decimal)slMult;
            tp = entryPrice - atr * (decimal)tpMult;
        }

        return (sl, tp, (decimal)trailingPct);
    }

    /// <summary>
    /// Zeichnet ein Exit-Ergebnis auf und lernt daraus.
    /// </summary>
    public void RecordExitOutcome(
        MarketRegime regime, decimal confidence,
        decimal slMultiplierUsed, decimal tpMultiplierUsed,
        bool won, decimal pnlPercent)
    {
        var key = GetContextKey(regime, confidence);
        var stats = _exitStats.GetOrAdd(key, _ => new ExitStats());

        lock (stats)
        {
            stats.TradeCount++;
            if (won)
            {
                stats.Wins++;
                stats.TotalWinPnl += (float)pnlPercent;
                // Guter Trade mit diesen Multiplikatoren → Gewichte anpassen
                stats.AvgWinningSl = EmaUpdate(stats.AvgWinningSl, (float)slMultiplierUsed, 0.1f);
                stats.AvgWinningTp = EmaUpdate(stats.AvgWinningTp, (float)tpMultiplierUsed, 0.1f);
            }
            else
            {
                stats.Losses++;
                stats.TotalLossPnl += (float)pnlPercent;
                stats.AvgLosingSl = EmaUpdate(stats.AvgLosingSl, (float)slMultiplierUsed, 0.1f);
                stats.AvgLosingTp = EmaUpdate(stats.AvgLosingTp, (float)tpMultiplierUsed, 0.1f);
            }
        }
    }

    /// <summary>Gibt Exit-Statistiken pro Kontext zurück (für Debug/UI).</summary>
    public Dictionary<string, (int Trades, int Wins, float AvgWinSl, float AvgWinTp)> GetExitStatistics()
    {
        var result = new Dictionary<string, (int, int, float, float)>();
        foreach (var kvp in _exitStats)
        {
            lock (kvp.Value)
            {
                result[kvp.Key] = (kvp.Value.TradeCount, kvp.Value.Wins,
                    kvp.Value.AvgWinningSl, kvp.Value.AvgWinningTp);
            }
        }
        return result;
    }

    // === Interne Methoden ===

    private (float SlMult, float TpMult, float TrailingPct) GetMultipliers(MarketRegime regime, decimal confidence)
    {
        var defaults = _defaults[regime];
        var key = GetContextKey(regime, confidence);

        if (!_exitStats.TryGetValue(key, out var stats))
            return defaults;

        lock (stats)
        {
            // Erst ab genug Trades die gelernten Werte nutzen
            if (stats.TradeCount < 10)
                return defaults;

            // Gewinnende Multiplikatoren bevorzugen (wenn verfügbar)
            var sl = stats.AvgWinningSl > 0 ? stats.AvgWinningSl : defaults.SlMultiplier;
            var tp = stats.AvgWinningTp > 0 ? stats.AvgWinningTp : defaults.TpMultiplier;

            // Sanft zwischen Default und gelerntem Wert mischen (70% gelernt, 30% default)
            sl = sl * 0.7f + defaults.SlMultiplier * 0.3f;
            tp = tp * 0.7f + defaults.TpMultiplier * 0.3f;

            // Trailing an Regime anpassen
            var trailing = defaults.TrailingPercent;
            if (stats.Wins > stats.Losses && stats.TradeCount >= 20)
            {
                // Gute Performance → etwas weiter trailing (mehr laufen lassen)
                trailing *= 1.1f;
            }

            return (sl, tp, trailing);
        }
    }

    private static string GetContextKey(MarketRegime regime, decimal confidence)
    {
        // Confidence in 3 Stufen diskretisieren
        var confLevel = confidence switch
        {
            >= 0.7m => "high",
            >= 0.5m => "mid",
            _ => "low"
        };
        return $"{regime}_{confLevel}";
    }

    private static float EmaUpdate(float current, float newValue, float alpha)
    {
        if (current <= 0) return newValue;
        return current * (1f - alpha) + newValue * alpha;
    }

    /// <summary>Serialisiert alle ExitStats als JSON.</summary>
    public string SerializeState()
    {
        var data = new Dictionary<string, float[]>();
        foreach (var kvp in _exitStats)
        {
            lock (kvp.Value)
            {
                data[kvp.Key] = new[]
                {
                    kvp.Value.TradeCount, kvp.Value.Wins, kvp.Value.Losses,
                    kvp.Value.TotalWinPnl, kvp.Value.TotalLossPnl,
                    kvp.Value.AvgWinningSl, kvp.Value.AvgWinningTp,
                    kvp.Value.AvgLosingSl, kvp.Value.AvgLosingTp
                };
            }
        }
        return JsonSerializer.Serialize(data);
    }

    /// <summary>Lädt ExitStats aus JSON.</summary>
    public void DeserializeState(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, float[]>>(json);
            if (data == null) return;

            foreach (var kvp in data)
            {
                if (kvp.Value.Length < 9) continue;
                var stats = _exitStats.GetOrAdd(kvp.Key, _ => new ExitStats());
                lock (stats)
                {
                    stats.TradeCount = (int)kvp.Value[0];
                    stats.Wins = (int)kvp.Value[1];
                    stats.Losses = (int)kvp.Value[2];
                    stats.TotalWinPnl = kvp.Value[3];
                    stats.TotalLossPnl = kvp.Value[4];
                    stats.AvgWinningSl = kvp.Value[5];
                    stats.AvgWinningTp = kvp.Value[6];
                    stats.AvgLosingSl = kvp.Value[7];
                    stats.AvgLosingTp = kvp.Value[8];
                }
            }
        }
        catch { /* Korrupte Daten ignorieren */ }
    }

    private class ExitStats
    {
        public int TradeCount;
        public int Wins;
        public int Losses;
        public float TotalWinPnl;
        public float TotalLossPnl;
        public float AvgWinningSl;  // EMA der SL-Multiplikatoren bei Gewinn-Trades
        public float AvgWinningTp;  // EMA der TP-Multiplikatoren bei Gewinn-Trades
        public float AvgLosingSl;   // EMA der SL-Multiplikatoren bei Verlust-Trades
        public float AvgLosingTp;   // EMA der TP-Multiplikatoren bei Verlust-Trades
    }
}
