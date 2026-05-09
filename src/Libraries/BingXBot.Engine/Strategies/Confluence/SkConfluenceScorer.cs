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
    /// <summary>
    /// Maximal erreichbarer Score, dynamisch berechnet aus den definierten <see cref="ConfluenceCategory"/>-
    /// Werten + Doppel-Gewicht fuer GklMasterZone und HighProbabilityZone (analog <see cref="Add"/>).
    /// Phase 18 / D2 (QW6) — verhindert Off-by-One bei jeder Kategorie-Erweiterung. Vorher hardcoded 11
    /// (musste manuell pro neuer Kategorie hochgezaehlt werden).
    /// </summary>
    public static readonly int MaxScore = ComputeMaxScore();

    private static int ComputeMaxScore()
    {
        var sum = 0;
        foreach (ConfluenceCategory cat in Enum.GetValues<ConfluenceCategory>())
            sum += cat is ConfluenceCategory.GklMasterZone or ConfluenceCategory.HighProbabilityZone ? 2 : 1;
        return sum;
    }

    private readonly List<(ConfluenceCategory Category, string Reason)> _hits = new();
    private int _score;

    /// <summary>Erzielter Score.</summary>
    public int Score => _score;

    /// <summary>Alle aktivierten Bestätigungen mit Begründung (für Logs + UI).</summary>
    public IReadOnlyList<(ConfluenceCategory Category, string Reason)> Hits => _hits;

    // Phase 18 / C1 — Cache fuer Reasons-Property. Vorher allokierte jeder Getter-Aufruf ein
    // neues string[] — bei N Symbolen × M Ticks × Hot-Path-Reads summiert sich das auf Pi-GC-Druck.
    // Cache wird in Add() invalidated.
    private string[]? _reasonsCache;

    /// <summary>Lesbare Reasons für das Signal-Logging. Cached, invalidated bei Add().</summary>
    public IReadOnlyList<string> Reasons
    {
        get
        {
            if (_reasonsCache != null) return _reasonsCache;
            var result = new string[_hits.Count];
            for (var i = 0; i < _hits.Count; i++)
                result[i] = _hits[i].Reason;
            _reasonsCache = result;
            return result;
        }
    }

    /// <summary>Fügt eine Bestätigung hinzu. GklMasterZone und HighProbabilityZone zählen +2, alle anderen +1.</summary>
    public void Add(ConfluenceCategory category, string reason)
    {
        var weight = category is ConfluenceCategory.GklMasterZone or ConfluenceCategory.HighProbabilityZone ? 2 : 1;
        _score += weight;
        _hits.Add((category, reason));
        _reasonsCache = null; // Phase 18 / C1 — Cache invalidieren bei Mutation
    }

    /// <summary>Confidence 0.0-1.0 (dynamisch aus MaxScore, nicht mehr fester /12-Divisor).</summary>
    public decimal Confidence => (decimal)Math.Clamp(_score, 0, MaxScore) / MaxScore;
}
