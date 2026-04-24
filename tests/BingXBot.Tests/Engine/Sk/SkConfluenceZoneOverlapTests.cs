using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>
/// Strukturpunkte-Doku §7 (B19) — Tests für den geometrischen Confluence-Overlap zwischen
/// HTF-GKL-Zone und LTF-BC-Zone bzw. LTF-EXT_1618-Counter-Zone.
/// </summary>
public class SkConfluenceZoneOverlapTests
{
    [Fact]
    public void Overlaps_ÜberlappendeIntervalle_TrueErkannt()
    {
        var a = new SkConfluenceZoneOverlap.PriceZone(100m, 110m);
        var b = new SkConfluenceZoneOverlap.PriceZone(105m, 120m);
        SkConfluenceZoneOverlap.Overlaps(a, b).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_DisjunkteIntervalle_FalseErkannt()
    {
        var a = new SkConfluenceZoneOverlap.PriceZone(100m, 110m);
        var b = new SkConfluenceZoneOverlap.PriceZone(120m, 130m);
        SkConfluenceZoneOverlap.Overlaps(a, b).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_BerührungAnGrenze_GilAlsOverlap()
    {
        // Klassischer Overlap-Test: Berührung an einem Punkt zählt als Overlap (<=), kein strict-less.
        var a = new SkConfluenceZoneOverlap.PriceZone(100m, 110m);
        var b = new SkConfluenceZoneOverlap.PriceZone(110m, 120m);
        SkConfluenceZoneOverlap.Overlaps(a, b).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_UngültigeZone_FalseErkannt()
    {
        var valid = new SkConfluenceZoneOverlap.PriceZone(100m, 110m);
        var invalid = new SkConfluenceZoneOverlap.PriceZone(0m, 0m);
        SkConfluenceZoneOverlap.Overlaps(valid, invalid).Should().BeFalse();
    }

    [Fact]
    public void MakeZone_SortiertLowHigh()
    {
        var zone = SkConfluenceZoneOverlap.MakeZone(120m, 100m);
        zone.Low.Should().Be(100m);
        zone.High.Should().Be(120m);
    }

    [Fact]
    public void BuildBcZone_LiefertRetracement500Bis667()
    {
        var seq = new Sequence
        {
            Point0 = new SwingPoint(100m, 0, DateTime.UtcNow, false),
            PointA = new SwingPoint(120m, 10, DateTime.UtcNow, true),
            Retracement500 = 110m,
            Retracement667 = 107m
        };
        var zone = SkConfluenceZoneOverlap.BuildBcZone(seq);
        zone.Should().NotBeNull();
        zone!.Value.Low.Should().Be(107m);
        zone.Value.High.Should().Be(110m);
    }

    [Fact]
    public void BuildTargetZoneOfOpposite_LiefertExt1618BisExt200()
    {
        var counter = new Sequence
        {
            Point0 = new SwingPoint(140m, 0, DateTime.UtcNow, true),
            PointA = new SwingPoint(100m, 20, DateTime.UtcNow, false),
            Extension1618 = 90m,
            Extension200 = 85m
        };
        var zone = SkConfluenceZoneOverlap.BuildTargetZoneOfOpposite(counter);
        zone.Should().NotBeNull();
        zone!.Value.Low.Should().Be(85m);
        zone.Value.High.Should().Be(90m);
    }

    [Fact]
    public void Evaluate_GklÜberlapptMitLtfBc_MeldetOverlapKindGklAndBc()
    {
        var gklHit = new GklHit(TimeFrame.W1, Gkl500: 110m, Gkl618: 108m, Gkl667: 105m,
            IsUptrend: true, SourcePoint0: 80m, SourceEnd: 140m);
        var ltfSeq = new Sequence
        {
            Point0 = new SwingPoint(100m, 0, DateTime.UtcNow, false),
            PointA = new SwingPoint(120m, 10, DateTime.UtcNow, true),
            IsLong = true,
            Retracement500 = 108m,  // in GKL [105..110]
            Retracement667 = 106m
        };
        var (has, reason, tf, kind) = SkConfluenceZoneOverlap.Evaluate(gklHit, ltfSeq, null);
        has.Should().BeTrue();
        tf.Should().Be(TimeFrame.W1);
        kind.Should().Be(SkConfluenceZoneOverlap.OverlapKind.GklAndBc);
        reason.Should().Contain("GKL");
    }

    [Fact]
    public void Evaluate_GklÜberlapptMitCounterTarget_MeldetOverlapKindCounterTarget()
    {
        var gklHit = new GklHit(TimeFrame.D1, Gkl500: 120m, Gkl618: 118m, Gkl667: 115m,
            IsUptrend: false, SourcePoint0: 140m, SourceEnd: 100m);
        var ltfSeq = new Sequence
        {
            Point0 = new SwingPoint(80m, 0, DateTime.UtcNow, false),
            PointA = new SwingPoint(100m, 10, DateTime.UtcNow, true),
            IsLong = true,
            Retracement500 = 90m,  // NICHT in GKL
            Retracement667 = 85m
        };
        var counter = new Sequence
        {
            Point0 = new SwingPoint(130m, 0, DateTime.UtcNow, true),
            PointA = new SwingPoint(100m, 20, DateTime.UtcNow, false),
            IsLong = false,  // Gegenrichtung zur ltfSeq
            Extension1618 = 118m,  // in GKL [115..120]
            Extension200 = 116m
        };
        var (has, _, tf, kind) = SkConfluenceZoneOverlap.Evaluate(gklHit, ltfSeq, counter);
        has.Should().BeTrue();
        tf.Should().Be(TimeFrame.D1);
        kind.Should().Be(SkConfluenceZoneOverlap.OverlapKind.GklAndCounterTarget);
    }

    [Fact]
    public void Evaluate_KeinGklHit_LiefertFalse()
    {
        var (has, _, _, kind) = SkConfluenceZoneOverlap.Evaluate(null, null, null);
        has.Should().BeFalse();
        kind.Should().Be(SkConfluenceZoneOverlap.OverlapKind.None);
    }

    [Fact]
    public void Evaluate_KeinOverlap_LiefertFalse()
    {
        var gklHit = new GklHit(TimeFrame.W1, Gkl500: 110m, Gkl618: 108m, Gkl667: 105m,
            IsUptrend: true, SourcePoint0: 80m, SourceEnd: 140m);
        var ltfSeq = new Sequence
        {
            Point0 = new SwingPoint(80m, 0, DateTime.UtcNow, false),
            PointA = new SwingPoint(95m, 10, DateTime.UtcNow, true),
            IsLong = true,
            Retracement500 = 90m,   // weit unter GKL
            Retracement667 = 85m
        };
        var (has, _, _, kind) = SkConfluenceZoneOverlap.Evaluate(gklHit, ltfSeq, null);
        has.Should().BeFalse();
        kind.Should().Be(SkConfluenceZoneOverlap.OverlapKind.None);
    }
}
