using BingXBot.Core.Models;

namespace BingXBot.Engine.Strategies.Confluence;

/// <summary>
/// Task 2.2 — Hierarchisch gewichteter Confluence-Score nach SK-Buch.
/// Alte Logik vergab 5 gleich gewichtete Punkte. Buch fordert Bestätigungs-GRUPPEN mit
/// unterschiedlichem Gewicht. GKL-Treffer (Königsdisziplin) bringt +2, Rest je +1.
/// Max-Score: 8.
///
/// Buch-Zitat: "Der Heilige Gral: Überschneidungen (Confluence). [...] Die stärksten
/// Trades entstehen genau dann, wenn verschiedene Fibonacci-Level aus völlig unterschiedlichen
/// Zügen exakt übereinanderliegen und eine kompakte, massive 'Box' im Chart bilden."
/// </summary>
public sealed class SkConfluenceScorer
{
    /// <summary>Maximal erreichbarer Score (Summe aller Kategorien inkl. GKL-Bonus + High-Probability-Bonus).</summary>
    public const int MaxScore = 10;

    private readonly List<(ConfluenceCategory Category, string Reason)> _hits = new();
    private int _score;

    /// <summary>Erzielter Score.</summary>
    public int Score => _score;

    /// <summary>Alle aktivierten Bestätigungen mit Begründung (für Logs + UI).</summary>
    public IReadOnlyList<(ConfluenceCategory Category, string Reason)> Hits => _hits;

    /// <summary>Lesbare Reasons für das Signal-Logging.</summary>
    public IReadOnlyList<string> Reasons
    {
        get
        {
            var result = new string[_hits.Count];
            for (var i = 0; i < _hits.Count; i++)
                result[i] = _hits[i].Reason;
            return result;
        }
    }

    /// <summary>Fügt eine Bestätigung hinzu. GklMasterZone und HighProbabilityZone zählen +2, alle anderen +1.</summary>
    public void Add(ConfluenceCategory category, string reason)
    {
        var weight = category is ConfluenceCategory.GklMasterZone or ConfluenceCategory.HighProbabilityZone ? 2 : 1;
        _score += weight;
        _hits.Add((category, reason));
    }

    /// <summary>Confidence 0.0-1.0 (dynamisch aus MaxScore, nicht mehr fester /12-Divisor).</summary>
    public decimal Confidence => (decimal)Math.Clamp(_score, 0, MaxScore) / MaxScore;
}
