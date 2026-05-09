using BingXBot.Engine.Strategies.Confluence;
using FsCheck.Xunit;

namespace BingXBot.Tests.Engine.Sk;

// Phase 18 / G6 — Property-Based-Tests fuer SkConfluenceScorer.
// FsCheck v3 generiert zufaellige int-Sequenzen, die wir auf Enum-Indizes mappen — vermeidet
// die Komplexitaet eigener Generators. Pruefen strukturelle Invarianten:
// - Score ∈ [0, len*2] (jede Kategorie max +2 fuer GklMasterZone/HighProbabilityZone)
// - Confidence ∈ [0, 1]
// - Reasons-Cache stable across Reads (verifiziert C1-Allocation-Optimierung)
public class ConfluenceScoringPropertyTests
{
    private static readonly ConfluenceCategory[] AllCategories = Enum.GetValues<ConfluenceCategory>();

    [Property(MaxTest = 200)]
    public bool Score_AlwaysWithinZeroAndAddCount(int[] indices)
    {
        var scorer = new SkConfluenceScorer();
        foreach (var i in indices)
        {
            var cat = AllCategories[(Math.Abs(i) % AllCategories.Length)];
            scorer.Add(cat, "fuzz");
        }
        // Jede Kategorie max +2 (GKL/HighProbability), sonst +1.
        return scorer.Score >= 0 && scorer.Score <= indices.Length * 2;
    }

    [Property(MaxTest = 200)]
    public bool Confidence_AlwaysBetweenZeroAndOne(int[] indices)
    {
        var scorer = new SkConfluenceScorer();
        foreach (var i in indices)
        {
            var cat = AllCategories[(Math.Abs(i) % AllCategories.Length)];
            scorer.Add(cat, "fuzz");
        }
        return scorer.Confidence >= 0m && scorer.Confidence <= 1m;
    }

    [Property(MaxTest = 100)]
    public bool Reasons_StableAcrossMultipleReads(int[] indices)
    {
        // Phase 18 / C1 — Reasons-Cache. Mehrere Reads MUESSEN dieselbe Liste liefern.
        var scorer = new SkConfluenceScorer();
        foreach (var i in indices)
        {
            var cat = AllCategories[(Math.Abs(i) % AllCategories.Length)];
            scorer.Add(cat, "fuzz");
        }
        var read1 = scorer.Reasons;
        var read2 = scorer.Reasons;
        return ReferenceEquals(read1, read2);
    }

    [Property(MaxTest = 100)]
    public bool AddInvalidatesReasonsCache(int[] indices, int extraIdx)
    {
        var scorer = new SkConfluenceScorer();
        foreach (var i in indices)
        {
            var cat = AllCategories[(Math.Abs(i) % AllCategories.Length)];
            scorer.Add(cat, "fuzz");
        }
        var read1 = scorer.Reasons;
        var extra = AllCategories[(Math.Abs(extraIdx) % AllCategories.Length)];
        scorer.Add(extra, "additional");
        var read2 = scorer.Reasons;
        return !ReferenceEquals(read1, read2) && read2.Count == read1.Count + 1;
    }
}
