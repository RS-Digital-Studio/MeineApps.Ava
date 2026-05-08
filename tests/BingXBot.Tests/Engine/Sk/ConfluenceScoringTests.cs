using BingXBot.Engine.Strategies.Confluence;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>Phase 5 — Tests für <see cref="SkConfluenceScorer"/> (Task 2.2).</summary>
public class ConfluenceScoringTests
{
    [Fact]
    public void LeererScorer_HatScoreNull()
    {
        var s = new SkConfluenceScorer();
        s.Score.Should().Be(0);
        s.Confidence.Should().Be(0m);
    }

    [Fact]
    public void PriceAction_BringtPlusEins()
    {
        var s = new SkConfluenceScorer();
        s.Add(ConfluenceCategory.PriceAction, "act");
        s.Score.Should().Be(1);
    }

    [Fact]
    public void GklMasterZone_BringtPlusZwei()
    {
        var s = new SkConfluenceScorer();
        s.Add(ConfluenceCategory.GklMasterZone, "GKL-W1");
        s.Score.Should().Be(2);
    }

    [Fact]
    public void FibonacciGoldenPocket_BringtPlusEins()
    {
        var s = new SkConfluenceScorer();
        s.Add(ConfluenceCategory.FibonacciGoldenPocket, "GP");
        s.Score.Should().Be(1);
    }

    [Fact]
    public void FahrplanAlignment_BringtPlusEins()
    {
        var s = new SkConfluenceScorer();
        s.Add(ConfluenceCategory.FahrplanAlignment, "fp");
        s.Score.Should().Be(1);
    }

    [Fact]
    public void HigherTfSequence_BringtPlusEins()
    {
        var s = new SkConfluenceScorer();
        s.Add(ConfluenceCategory.HigherTfSequence, "htf");
        s.Score.Should().Be(1);
    }

    [Fact]
    public void VolumeSpike_BringtPlusEins()
    {
        var s = new SkConfluenceScorer();
        s.Add(ConfluenceCategory.VolumeSpike, "vol");
        s.Score.Should().Be(1);
    }

    [Fact]
    public void BcklReEntry_BringtPlusEins()
    {
        var s = new SkConfluenceScorer();
        s.Add(ConfluenceCategory.BcklReEntry, "bckl");
        s.Score.Should().Be(1);
    }

    [Fact]
    public void HighProbabilityZone_BringtPlusZwei()
    {
        // Spec §7 — Heiliger Gral: HTF-GKL ∩ LTF-BC bzw. LTF-EXT-Counter → Score-Bonus wie GKL-Masterzone.
        var s = new SkConfluenceScorer();
        s.Add(ConfluenceCategory.HighProbabilityZone, "HighProb-Overlap: HTF-W1-GKL ∩ LTF-BC");
        s.Score.Should().Be(2);
    }

    [Fact]
    public void MaxScore_MitAllenKategorienInklGklUndHighProbability()
    {
        var s = new SkConfluenceScorer();
        s.Add(ConfluenceCategory.PriceAction, "a");
        s.Add(ConfluenceCategory.FibonacciGoldenPocket, "b");
        s.Add(ConfluenceCategory.GklMasterZone, "c");             // +2
        s.Add(ConfluenceCategory.FahrplanAlignment, "d");
        s.Add(ConfluenceCategory.HigherTfSequence, "e");
        s.Add(ConfluenceCategory.VolumeSpike, "f");
        s.Add(ConfluenceCategory.BcklReEntry, "g");
        s.Add(ConfluenceCategory.HighProbabilityZone, "hp");      // +2 (Spec §7 Heiliger Gral)
        s.Add(ConfluenceCategory.FavorableFundingRate, "funding"); // +1 (v1.5.4 Phase 7)
        s.Score.Should().Be(SkConfluenceScorer.MaxScore);
        s.Confidence.Should().Be(1m);
    }

    // BUCH-ONLY: BcDepthAdjustment-Tests entfernt (BcDepthMonitor ist raus).

    [Fact]
    public void Confidence_AnteilAnMaxScore_KorrektBerechnet()
    {
        // v1.5.4: MaxScore = 11 (zuvor 10). 5 einfache Hits → 5/11 ≈ 0.4545 Confidence.
        var s = new SkConfluenceScorer();
        s.Add(ConfluenceCategory.PriceAction, "a");
        s.Add(ConfluenceCategory.FibonacciGoldenPocket, "b");
        s.Add(ConfluenceCategory.FahrplanAlignment, "c");
        s.Add(ConfluenceCategory.HigherTfSequence, "d");
        s.Add(ConfluenceCategory.VolumeSpike, "e");
        s.Confidence.Should().BeApproximately(5m / SkConfluenceScorer.MaxScore, 0.0001m);
    }

    [Fact]
    public void Reasons_EnthaeltAlleEintraege()
    {
        var s = new SkConfluenceScorer();
        s.Add(ConfluenceCategory.PriceAction, "act");
        s.Add(ConfluenceCategory.VolumeSpike, "vol");
        s.Reasons.Should().Contain("act");
        s.Reasons.Should().Contain("vol");
    }
}
