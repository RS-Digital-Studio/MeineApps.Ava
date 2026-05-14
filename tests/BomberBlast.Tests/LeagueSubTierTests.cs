using BomberBlast.Models.League;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für Sub-Tier-Logik (Phase 19 — L1).
/// Validiert dass die Sub-Tier-Berechnung innerhalb der Punkte-Schwellen korrekt ist
/// und Edge-Cases (Diamond hat kein Sub-Tier, Promotion-Grenzen) abgedeckt sind.
/// </summary>
public class LeagueSubTierTests
{
    [Theory]
    [InlineData(LeagueTier.Bronze, 0, LeagueSubTier.I)]      // Tier-Untergrenze
    [InlineData(LeagueTier.Bronze, 100, LeagueSubTier.I)]    // ~25%
    [InlineData(LeagueTier.Bronze, 150, LeagueSubTier.II)]   // ~37%
    [InlineData(LeagueTier.Bronze, 280, LeagueSubTier.III)]  // ~70%
    [InlineData(LeagueTier.Bronze, 399, LeagueSubTier.III)]  // direkt unter Promotion
    [InlineData(LeagueTier.Silver, 400, LeagueSubTier.I)]    // Silver-Einstieg
    [InlineData(LeagueTier.Silver, 600, LeagueSubTier.II)]
    [InlineData(LeagueTier.Silver, 850, LeagueSubTier.III)]
    [InlineData(LeagueTier.Gold, 900, LeagueSubTier.I)]
    [InlineData(LeagueTier.Gold, 1500, LeagueSubTier.III)]
    [InlineData(LeagueTier.Platinum, 1600, LeagueSubTier.I)]
    [InlineData(LeagueTier.Platinum, 2400, LeagueSubTier.III)]
    public void GetSubTier_LiefertKorrekteSubStufeJeNachPunkten(LeagueTier tier, int points, LeagueSubTier expected)
    {
        tier.GetSubTier(points).Should().Be(expected);
    }

    [Fact]
    public void GetSubTier_Diamond_GibtImmerIIIZurueck()
    {
        // Diamond ist Endgame-Slot, hat keine Sub-Tiers (UI zeigt einfach "Diamond")
        LeagueTier.Diamond.GetSubTier(2500).Should().Be(LeagueSubTier.III);
        LeagueTier.Diamond.GetSubTier(10000).Should().Be(LeagueSubTier.III);
        LeagueTier.Diamond.GetSubTier(int.MaxValue).Should().Be(LeagueSubTier.III);
    }

    [Theory]
    [InlineData(LeagueTier.Bronze, LeagueSubTier.I, "Bronze I")]
    [InlineData(LeagueTier.Silver, LeagueSubTier.II, "Silver II")]
    [InlineData(LeagueTier.Gold, LeagueSubTier.III, "Gold III")]
    [InlineData(LeagueTier.Platinum, LeagueSubTier.II, "Platinum II")]
    [InlineData(LeagueTier.Diamond, LeagueSubTier.I, "Diamond")]
    [InlineData(LeagueTier.Diamond, LeagueSubTier.III, "Diamond")]
    public void GetDisplayName_KombiniertTierUndSubKorrekt(LeagueTier tier, LeagueSubTier sub, string expected)
    {
        tier.GetDisplayName(sub).Should().Be(expected);
    }

    [Fact]
    public void GetSubTierThreshold_LiefertPunkteUntergrenze()
    {
        // Bronze hat range 0..399, Drittel = 133
        LeagueTier.Bronze.GetSubTierThreshold(LeagueSubTier.I).Should().Be(0);
        LeagueTier.Bronze.GetSubTierThreshold(LeagueSubTier.II).Should().Be(133);
        LeagueTier.Bronze.GetSubTierThreshold(LeagueSubTier.III).Should().Be(266);

        // Silver: 400..899, Drittel = 166
        LeagueTier.Silver.GetSubTierThreshold(LeagueSubTier.I).Should().Be(400);
        LeagueTier.Silver.GetSubTierThreshold(LeagueSubTier.II).Should().Be(566);
    }

    [Fact]
    public void GetSubTierCeiling_LiefertOberePunkteGrenze()
    {
        // Bronze III endet bei Promotion-Schwelle Silber (400)
        LeagueTier.Bronze.GetSubTierCeiling(LeagueSubTier.III).Should().Be(400);
        // Bronze II endet bei Untergrenze von Bronze III (266)
        LeagueTier.Bronze.GetSubTierCeiling(LeagueSubTier.II).Should().Be(266);
    }

    [Fact]
    public void GetSubTierThreshold_Diamond_GibtTierUntergrenzeZurueck()
    {
        LeagueTier.Diamond.GetSubTierThreshold(LeagueSubTier.I).Should().Be(2500);
        LeagueTier.Diamond.GetSubTierThreshold(LeagueSubTier.III).Should().Be(2500);
    }

    [Fact]
    public void GetSubTier_StimmtMitGetSubTierThreshold()
    {
        // Konsistenz: GetSubTier(GetSubTierThreshold(sub)) == sub für alle Sub-Tiers
        foreach (var tier in new[] { LeagueTier.Bronze, LeagueTier.Silver, LeagueTier.Gold, LeagueTier.Platinum })
        {
            foreach (var sub in new[] { LeagueSubTier.I, LeagueSubTier.II, LeagueSubTier.III })
            {
                var threshold = tier.GetSubTierThreshold(sub);
                tier.GetSubTier(threshold).Should().Be(sub,
                    $"GetSubTier muss konsistent mit GetSubTierThreshold sein für {tier} {sub}");
            }
        }
    }
}
