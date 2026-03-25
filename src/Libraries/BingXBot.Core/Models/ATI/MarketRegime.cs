namespace BingXBot.Core.Models.ATI;

/// <summary>
/// Marktregime - bestimmt welche Strategien aktiv sind und wie aggressiv gehandelt wird.
/// </summary>
public enum MarketRegime : byte
{
    /// <summary>Klarer Aufwaertstrend (ADX hoch, EMAs aufsteigend).</summary>
    TrendingBull,
    /// <summary>Klarer Abwaertstrend (ADX hoch, EMAs absteigend).</summary>
    TrendingBear,
    /// <summary>Seitwaertsmarkt (ADX niedrig, enge Bollinger Bands).</summary>
    Range,
    /// <summary>Chaotisch/Crash (extrem hohe Volatilitaet, keine Struktur). Nicht traden!</summary>
    Chaotic
}

/// <summary>
/// Zustand der Regime-Erkennung mit Wahrscheinlichkeiten fuer jeden Zustand.
/// </summary>
public record RegimeState(
    MarketRegime CurrentRegime,
    decimal Confidence,
    decimal TrendingBullProbability,
    decimal TrendingBearProbability,
    decimal RangeProbability,
    decimal ChaoticProbability,
    DateTime DetectedAt);
