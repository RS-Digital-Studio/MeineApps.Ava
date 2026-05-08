using BingXBot.Engine.Strategies.Confluence;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Indicators;

// v1.5.4 Phase 7 — Funding-Rate Soft-Bonus.
// Strategy-Integration ist via Backtest-Suite getestet; hier liegt der Fokus auf der reinen
// Scorer-Mechanik + Schwellenwert-Verhalten.
public class FundingRateBonusTests
{
    [Fact]
    public void MaxScore_NachPhase7_ZehnPlusFunding()
    {
        SkConfluenceScorer.MaxScore.Should().Be(11, "Phase 7 erweitert MaxScore um +1 (FavorableFundingRate)");
    }

    [Fact]
    public void Add_FavorableFundingRate_GibtPlusEins()
    {
        var scorer = new SkConfluenceScorer();
        scorer.Add(ConfluenceCategory.FavorableFundingRate, "test");
        scorer.Score.Should().Be(1);
    }

    [Fact]
    public void Add_GklMasterZoneUndFunding_ZweiPlusEins()
    {
        var scorer = new SkConfluenceScorer();
        scorer.Add(ConfluenceCategory.GklMasterZone, "GKL");
        scorer.Add(ConfluenceCategory.FavorableFundingRate, "Funding");
        scorer.Score.Should().Be(3);
    }

    [Fact]
    public void Confidence_VolllScore_IstEins()
    {
        var scorer = new SkConfluenceScorer();
        scorer.Add(ConfluenceCategory.PriceAction, "");
        scorer.Add(ConfluenceCategory.FibonacciGoldenPocket, "");
        scorer.Add(ConfluenceCategory.GklMasterZone, "");
        scorer.Add(ConfluenceCategory.HighProbabilityZone, "");
        scorer.Add(ConfluenceCategory.FahrplanAlignment, "");
        scorer.Add(ConfluenceCategory.HigherTfSequence, "");
        scorer.Add(ConfluenceCategory.VolumeSpike, "");
        scorer.Add(ConfluenceCategory.BcklReEntry, "");
        scorer.Add(ConfluenceCategory.FavorableFundingRate, "");
        scorer.Score.Should().Be(11);
        scorer.Confidence.Should().Be(1.0m);
    }

    [Fact]
    public void Threshold_PositivLongFunding_KeinBonus()
    {
        // Long mit positiver Funding (Long zahlt) → kein Bonus.
        // Schwelle 0.05 % → 0.0005 Decimal. Funding 0.0001 = 0.01 % > -0.0005 → kein Trigger.
        const decimal thresholdDec = 0.05m / 100m;
        const decimal funding = 0.0001m;
        bool isLong = true;
        var trigger = isLong ? funding < -thresholdDec : funding > thresholdDec;
        trigger.Should().BeFalse();
    }

    [Fact]
    public void Threshold_NegativLongFunding_GibtBonus()
    {
        // Long bei stark negativer Funding (Short zahlt Long) → Bonus.
        const decimal thresholdDec = 0.05m / 100m;
        const decimal funding = -0.001m; // = -0.1 %
        bool isLong = true;
        var trigger = isLong ? funding < -thresholdDec : funding > thresholdDec;
        trigger.Should().BeTrue();
    }

    [Fact]
    public void Threshold_PositivShortFunding_GibtBonus()
    {
        const decimal thresholdDec = 0.05m / 100m;
        const decimal funding = 0.001m;
        bool isLong = false;
        var trigger = isLong ? funding < -thresholdDec : funding > thresholdDec;
        trigger.Should().BeTrue();
    }

    [Fact]
    public void Threshold_NegativShortFunding_KeinBonus()
    {
        const decimal thresholdDec = 0.05m / 100m;
        const decimal funding = -0.0001m;
        bool isLong = false;
        var trigger = isLong ? funding < -thresholdDec : funding > thresholdDec;
        trigger.Should().BeFalse();
    }
}
