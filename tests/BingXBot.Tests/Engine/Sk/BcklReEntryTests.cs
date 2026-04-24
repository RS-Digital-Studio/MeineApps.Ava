using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>Phase 5 — Tests für BCKL-IMMER-Trigger (Task 2.1).</summary>
public class BcklReEntryTests
{
    [Fact]
    public void GetDynamicBcZone_LiefertNullbei_NichtAktiviert()
    {
        var sm = new SequenceStateMachine(0.3m, 0.2m, 0.382m, 0.786m, minPoint0Candles: 1);
        var (top, bottom) = sm.GetDynamicBcZone();
        top.Should().Be(0m);
        bottom.Should().Be(0m);
    }

    [Fact]
    public void CalculateBCKL_Null_WennPointBFehlt()
    {
        var seq = new Sequence
        {
            Point0 = new SwingPoint(100m, 0, DateTime.MinValue, false),
            PointA = new SwingPoint(110m, 1, DateTime.MinValue, true),
            PointB = null,
            IsLong = true,
            State = SequenceState.Active,
        };
        var result = SequenceDetector.CalculateBCKL(seq);
        result.Should().BeNull();
    }

    [Fact]
    public void CalculateBCKL_NullWennNichtActive()
    {
        var seq = new Sequence
        {
            Point0 = new SwingPoint(100m, 0, DateTime.MinValue, false),
            PointA = new SwingPoint(110m, 1, DateTime.MinValue, true),
            PointB = new SwingPoint(105m, 2, DateTime.MinValue, false),
            IsLong = true,
            State = SequenceState.CorrectionZone,
            Extension100 = 115m,
        };
        var result = SequenceDetector.CalculateBCKL(seq);
        result.Should().BeNull();
    }

    [Fact]
    public void CalculateBCKL_LiefertRetracement_WennAktiv()
    {
        var seq = new Sequence
        {
            Point0 = new SwingPoint(100m, 0, DateTime.MinValue, false),
            PointA = new SwingPoint(110m, 1, DateTime.MinValue, true),
            PointB = new SwingPoint(105m, 2, DateTime.MinValue, false),
            IsLong = true,
            State = SequenceState.Active,
            Extension100 = 115m,
        };
        var result = SequenceDetector.CalculateBCKL(seq);
        result.Should().NotBeNull();
        result!.Value.Bckl500.Should().BeInRange(105m, 115m);
        result.Value.Bckl667.Should().BeInRange(105m, 115m);
    }

    [Fact]
    public void IsInBCKL_TrueWennPreisInZone()
    {
        var inZone = SequenceDetector.IsInBCKL(107.5m, bckl500: 110m, bckl667: 105m);
        inZone.Should().BeTrue();
    }

    [Fact]
    public void IsInBCKL_FalseWennPreisAusserhalb()
    {
        var outZone = SequenceDetector.IsInBCKL(120m, bckl500: 110m, bckl667: 105m);
        outZone.Should().BeFalse();
    }

    [Fact]
    public void IsInBCKL_FunktioniertMitInvertierterReihenfolge()
    {
        var inZone = SequenceDetector.IsInBCKL(107.5m, bckl500: 105m, bckl667: 110m);
        inZone.Should().BeTrue();
    }
}
