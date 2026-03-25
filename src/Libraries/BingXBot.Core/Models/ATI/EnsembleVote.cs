using BingXBot.Core.Enums;

namespace BingXBot.Core.Models.ATI;

/// <summary>
/// Ergebnis der Ensemble-Abstimmung ueber alle aktiven Strategien.
/// </summary>
public record EnsembleVote(
    /// <summary>Konsens-Signal (Long/Short/None basierend auf Mehrheit).</summary>
    Signal ConsensusSignal,
    /// <summary>Gewichtete Confidence (0-1) basierend auf Strategie-Gewichten.</summary>
    decimal WeightedConfidence,
    /// <summary>Anzahl Strategien die fuer dieses Signal gestimmt haben.</summary>
    int AgreeingCount,
    /// <summary>Gesamtzahl aktiver Strategien.</summary>
    int TotalCount,
    /// <summary>Namen der zustimmenden Strategien (kommasepariert).</summary>
    string AgreeingNames,
    /// <summary>Einzelne Stimmen mit Gewicht und Confidence pro Strategie.</summary>
    IReadOnlyList<StrategyVote> Votes,
    /// <summary>Bester Entry-Preis aus den zustimmenden Strategien.</summary>
    decimal? BestEntryPrice,
    /// <summary>Bester Stop-Loss aus den zustimmenden Strategien (engster).</summary>
    decimal? BestStopLoss,
    /// <summary>Bestes Take-Profit aus den zustimmenden Strategien (weitestes).</summary>
    decimal? BestTakeProfit);

/// <summary>
/// Einzelne Strategie-Stimme im Ensemble.
/// </summary>
public record StrategyVote(
    string StrategyName,
    Signal Signal,
    decimal Confidence,
    decimal Weight,
    decimal? StopLoss,
    decimal? TakeProfit);
