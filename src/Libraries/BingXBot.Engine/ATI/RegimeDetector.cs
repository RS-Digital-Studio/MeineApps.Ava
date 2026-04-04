using System.Collections.Concurrent;
using System.Text.Json;
using BingXBot.Core.Models.ATI;

namespace BingXBot.Engine.ATI;

/// <summary>
/// Erkennt das aktuelle Marktregime basierend auf Feature-Analyse.
/// Kombiniert regelbasierte Erkennung mit gelernten HMM-Übergangswahrscheinlichkeiten.
/// Thread-safe für Multi-Symbol-Nutzung.
/// </summary>
public class RegimeDetector
{
    // Geglättete Regime-Scores pro Symbol (Hysterese gegen Flackern)
    private readonly ConcurrentDictionary<string, float[]> _smoothedScores = new();
    // Letztes erkanntes Regime pro Symbol
    private readonly ConcurrentDictionary<string, MarketRegime> _lastRegime = new();

    // HMM-Übergangsmatrix: _transitions[from][to] = Anzahl beobachteter Übergänge
    // Wird per Bayes-Update aus tatsächlichen Regime-Wechseln gelernt
    private readonly int[,] _transitionCounts = new int[4, 4];
    private readonly object _transitionLock = new();

    // EMA-Glättungsfaktor (0.2 = langsame Anpassung, weniger Flackern)
    private const float SmoothingAlpha = 0.2f;

    // Mindest-Confidence um Regime zu wechseln (Hysterese)
    private const float RegimeSwitchThreshold = 0.15f;

    public RegimeDetector()
    {
        // Default-Übergangsmatrix: Regime sind hochpersistent (85%)
        // Entspricht Forschungsergebnissen zu Krypto-Marktregimes
        InitializeDefaultTransitions();
    }

    /// <summary>
    /// Erkennt das aktuelle Marktregime basierend auf Feature-Daten.
    /// </summary>
    public RegimeState Detect(FeatureSnapshot features)
    {
        // 1. Regelbasierte Rohscores berechnen
        var rawScores = CalculateRawScores(features);

        // 2. Mit EMA glätten (Hysterese gegen Flackern)
        var smoothed = SmoothScores(features.Symbol, rawScores);

        // 3. HMM-Übergangswahrscheinlichkeiten einbeziehen
        var posterior = ApplyTransitionPrior(features.Symbol, smoothed);

        // 4. Normalisieren auf Summe = 1
        NormalizeInPlace(posterior);

        // 5. Regime bestimmen (mit Hysterese)
        var (regime, confidence) = DetermineRegime(features.Symbol, posterior);

        return new RegimeState(
            regime, (decimal)confidence,
            (decimal)posterior[0], (decimal)posterior[1],
            (decimal)posterior[2], (decimal)posterior[3],
            DateTime.UtcNow);
    }

    /// <summary>
    /// Zeichnet einen beobachteten Regime-Übergang auf (für HMM-Lernen).
    /// Aufrufen wenn sich das Regime eines Symbols ändert.
    /// </summary>
    public void RecordTransition(MarketRegime from, MarketRegime to)
    {
        lock (_transitionLock)
        {
            _transitionCounts[(int)from, (int)to]++;
        }
    }

    /// <summary>Gibt die aktuelle Übergangsmatrix als Wahrscheinlichkeiten zurück.</summary>
    public decimal[,] GetTransitionMatrix()
    {
        lock (_transitionLock)
        {
            var matrix = new decimal[4, 4];
            for (int from = 0; from < 4; from++)
            {
                var rowSum = 0;
                for (int to = 0; to < 4; to++)
                    rowSum += _transitionCounts[from, to];

                for (int to = 0; to < 4; to++)
                    matrix[from, to] = rowSum > 0 ? (decimal)_transitionCounts[from, to] / rowSum : 0m;
            }
            return matrix;
        }
    }

    // === Interne Methoden ===

    /// <summary>
    /// Berechnet Rohscores für jedes Regime basierend auf Features.
    /// Scores sind NICHT normalisiert (höher = wahrscheinlicher).
    /// </summary>
    private static float[] CalculateRawScores(FeatureSnapshot f)
    {
        // Scores: [TrendingBull, TrendingBear, Range, Chaotic]
        var scores = new float[4];

        var adx = f.AdxNormalized * 100f; // Zurück auf 0-100 Skala für lesbare Schwellen
        var bbWidth = f.BollingerWidth;
        var atrPct = f.AtrPercent;
        var volRatio = f.VolumeRatio;

        // --- TrendingBull ---
        // Starker ADX + Preis über EMAs + bullish EMA-Cross + bullish HTF
        scores[0] = 0f;
        if (adx > 20) scores[0] += (adx - 20) / 40f; // 0-1 ab ADX 20, max bei 60
        if (f.PriceVsEma20 > 0) scores[0] += Math.Min(f.PriceVsEma20 * 5f, 0.5f);
        if (f.PriceVsEma50 > 0) scores[0] += Math.Min(f.PriceVsEma50 * 3f, 0.3f);
        if (f.EmaCrossDirection > 0) scores[0] += 0.3f;
        if (f.HtfTrend > 0) scores[0] += 0.4f;
        if (f.RsiNormalized > 0.5f) scores[0] += (f.RsiNormalized - 0.5f); // RSI > 50 = bullish

        // --- TrendingBear ---
        // Starker ADX + Preis unter EMAs + bearish EMA-Cross + bearish HTF
        scores[1] = 0f;
        if (adx > 20) scores[1] += (adx - 20) / 40f;
        if (f.PriceVsEma20 < 0) scores[1] += Math.Min(-f.PriceVsEma20 * 5f, 0.5f);
        if (f.PriceVsEma50 < 0) scores[1] += Math.Min(-f.PriceVsEma50 * 3f, 0.3f);
        if (f.EmaCrossDirection < 0) scores[1] += 0.3f;
        if (f.HtfTrend < 0) scores[1] += 0.4f;
        if (f.RsiNormalized < 0.5f) scores[1] += (0.5f - f.RsiNormalized);

        // --- Range ---
        // Niedriger ADX + enge Bollinger + niedrige Volatilität
        scores[2] = 0f;
        if (adx < 25) scores[2] += (25 - adx) / 25f; // 0-1, max bei ADX 0
        if (bbWidth < 0.04f) scores[2] += (0.04f - bbWidth) / 0.04f * 0.5f; // Enge Bänder
        if (atrPct < 0.02f) scores[2] += (0.02f - atrPct) / 0.02f * 0.3f; // Niedrige Volatilität
        // Preis nahe der Mitte der Bollinger → Range-typisch
        var bbMidDist = Math.Abs(f.BollingerPosition - 0.5f);
        if (bbMidDist < 0.3f) scores[2] += (0.3f - bbMidDist) / 0.3f * 0.2f;

        // --- Chaotic ---
        // Extrem hohe Volatilität + hohe ATR + hohes Volumen + ADX unklar
        scores[3] = 0f;
        if (atrPct > 0.04f) scores[3] += Math.Min((atrPct - 0.04f) / 0.04f, 1f);
        if (bbWidth > 0.08f) scores[3] += Math.Min((bbWidth - 0.08f) / 0.08f, 0.5f);
        if (volRatio > 2f) scores[3] += Math.Min((volRatio - 2f) / 2f, 0.5f);
        // Hohe Volatilität ohne klaren Trend (ADX kann hoch sein aber Richtung unklar)
        var trendClarity = Math.Abs(f.PriceVsEma50);
        if (atrPct > 0.03f && trendClarity < 0.01f) scores[3] += 0.3f;

        return scores;
    }

    private float[] SmoothScores(string symbol, float[] rawScores)
    {
        var smoothed = _smoothedScores.GetOrAdd(symbol, _ => new float[] { 0.1f, 0.1f, 0.6f, 0.2f }); // Default: leichtes Range-Bias

        // Lock auf das Array: Bei Multi-Symbol-Nutzung kann dasselbe Symbol
        // parallel aus verschiedenen Threads evaluiert werden
        float[] copy;
        lock (smoothed)
        {
            for (int i = 0; i < 4; i++)
                smoothed[i] = smoothed[i] * (1f - SmoothingAlpha) + rawScores[i] * SmoothingAlpha;

            // M-2 Fix: Kopie zurückgeben statt gecachtes Array.
            // Verhindert dass NormalizeInPlace den Cache korrumpiert.
            copy = new float[4];
            Array.Copy(smoothed, copy, 4);
        }

        return copy;
    }

    private float[] ApplyTransitionPrior(string symbol, float[] smoothedScores)
    {
        // K-4 Fix: Immer neue Kopie erstellen (auch wenn kein lastRegime vorhanden).
        // Ohne Kopie würde NormalizeInPlace bei neuem Symbol den gecachten Wert korrumpieren.
        var posterior = new float[4];
        Array.Copy(smoothedScores, posterior, 4);

        if (!_lastRegime.TryGetValue(symbol, out var lastRegime)) return posterior;

        // Übergangswahrscheinlichkeiten als Prior einbeziehen
        lock (_transitionLock)
        {
            var fromIdx = (int)lastRegime;
            var rowSum = 0;
            for (int to = 0; to < 4; to++)
                rowSum += _transitionCounts[fromIdx, to];

            if (rowSum > 10) // Erst ab genug Beobachtungen
            {
                for (int to = 0; to < 4; to++)
                {
                    var transProb = (float)_transitionCounts[fromIdx, to] / rowSum;
                    // 70% Feature-basiert, 30% Transition-Prior
                    posterior[to] = posterior[to] * 0.7f + transProb * 0.3f;
                }
            }
        }

        return posterior;
    }

    private (MarketRegime regime, float confidence) DetermineRegime(string symbol, float[] posterior)
    {
        // Finde das Regime mit dem höchsten Score
        var bestIdx = 0;
        var bestScore = posterior[0];
        for (int i = 1; i < 4; i++)
        {
            if (posterior[i] > bestScore)
            {
                bestIdx = i;
                bestScore = posterior[i];
            }
        }

        var newRegime = (MarketRegime)bestIdx;

        // Hysterese: Nur wechseln wenn der Unterschied zum aktuellen Regime signifikant ist
        if (_lastRegime.TryGetValue(symbol, out var currentRegime) && currentRegime != newRegime)
        {
            var currentScore = posterior[(int)currentRegime];
            if (bestScore - currentScore < RegimeSwitchThreshold)
            {
                // Nicht genug Unterschied → beim aktuellen Regime bleiben
                return (currentRegime, currentScore);
            }

            // Regime-Wechsel → für HMM-Lernen aufzeichnen
            RecordTransition(currentRegime, newRegime);
        }

        _lastRegime[symbol] = newRegime;
        return (newRegime, bestScore);
    }

    private static void NormalizeInPlace(float[] scores)
    {
        var sum = 0f;
        for (int i = 0; i < scores.Length; i++)
        {
            scores[i] = Math.Max(scores[i], 0.001f); // Minimum-Score (Laplace-Glättung)
            sum += scores[i];
        }
        if (sum > 0)
        {
            for (int i = 0; i < scores.Length; i++)
                scores[i] /= sum;
        }
    }

    /// <summary>Serialisiert die Übergangsmatrix als flaches int-Array (4x4=16 Werte).</summary>
    public string SerializeState()
    {
        lock (_transitionLock)
        {
            var flat = new int[16];
            for (int from = 0; from < 4; from++)
                for (int to = 0; to < 4; to++)
                    flat[from * 4 + to] = _transitionCounts[from, to];
            return JsonSerializer.Serialize(flat);
        }
    }

    /// <summary>Lädt die Übergangsmatrix aus einem flachen int-Array.</summary>
    public void DeserializeState(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var flat = JsonSerializer.Deserialize<int[]>(json);
            if (flat == null || flat.Length != 16) return;

            lock (_transitionLock)
            {
                for (int from = 0; from < 4; from++)
                    for (int to = 0; to < 4; to++)
                        _transitionCounts[from, to] = flat[from * 4 + to];
            }
        }
        catch { /* Korrupte Daten ignorieren */ }
    }

    private void InitializeDefaultTransitions()
    {
        // Basierend auf Forschung: Regime sind hochpersistent (85-95%)
        // Default: 85% bleiben, 15% auf andere verteilt
        for (int from = 0; from < 4; from++)
        {
            for (int to = 0; to < 4; to++)
            {
                _transitionCounts[from, to] = (from == to) ? 85 : 5;
            }
        }
    }
}
