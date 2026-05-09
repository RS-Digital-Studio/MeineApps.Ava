using System.Text.Json;
using BingXBot.Engine.Strategies.Confluence;
using VerifyXunit;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

// Phase 18 / G5 — Snapshot-Tests fuer SkConfluenceScorer.
// Schuetzt vor stillen Verhaltens-Drifts beim Hinzufuegen neuer Kategorien oder Aendern
// der Gewichtung. Beim ersten Lauf wird die .verified.txt-Baseline angelegt; spaetere
// Aenderungen muessen explizit per `dotnet test` + Diff-Tool akzeptiert werden.
//
// Hinweis: Beim ersten Lauf scheitert der Test, generiert eine .received.txt-Datei.
// Diese wird zur Baseline (.verified.txt) befoerdert via `dotnet verify accept` oder
// manuell durch Umbenennen.
public class ConfluenceScoringSnapshotTests
{
    [Fact]
    public Task AllCategories_ProducesExpectedScoreBreakdown()
    {
        // Setup: jede Kategorie genau 1×.
        var scorer = new SkConfluenceScorer();
        foreach (ConfluenceCategory cat in Enum.GetValues<ConfluenceCategory>())
            scorer.Add(cat, $"Test:{cat}");

        var snapshot = new
        {
            maxScore = SkConfluenceScorer.MaxScore,
            score = scorer.Score,
            confidence = scorer.Confidence,
            hits = scorer.Hits.Select(h => new
            {
                category = h.Category.ToString(),
                reason = h.Reason
            }).ToList()
        };

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        return Verifier.Verify(json);
    }

    [Fact]
    public Task GklOnly_HasDoubleWeightAndConfidenceFraction()
    {
        var scorer = new SkConfluenceScorer();
        scorer.Add(ConfluenceCategory.GklMasterZone, "GKL-Hit");
        var snapshot = new
        {
            maxScore = SkConfluenceScorer.MaxScore,
            score = scorer.Score,
            confidence = scorer.Confidence,
            singleHit = scorer.Hits.Single().Category.ToString()
        };
        return Verifier.Verify(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
    }
}
