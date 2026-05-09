using BomberBlast.Core;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für FixedTimestepRunner (v2.0.54 — AAA-Audit Phase 13).
/// Validiert Akkumulator-Pattern, Tick-Anzahl-Berechnung, Spiral-of-Death-Schutz,
/// Interpolation-Alpha, Enabled-Toggle.
/// </summary>
public class FixedTimestepRunnerTests
{
    [Fact]
    public void Default_DisabledMitNullAccumulator()
    {
        var runner = new FixedTimestepRunner();
        runner.Enabled.Should().BeFalse();
        runner.Accumulator.Should().Be(0);
    }

    [Fact]
    public void GetTicksForFrame_Disabled_LiefertImmer0()
    {
        var runner = new FixedTimestepRunner();
        runner.GetTicksForFrame(1.0f).Should().Be(0);
        runner.GetTicksForFrame(100.0f).Should().Be(0);
        runner.Accumulator.Should().Be(0, "Akkumulator wird im Disabled-Mode nicht gefüllt");
    }

    [Fact]
    public void GetTicksForFrame_60Hz_Liefert1TickPro16ms()
    {
        var runner = new FixedTimestepRunner { Enabled = true };
        // ~16.67ms = 1 Tick bei 60 Hz
        runner.GetTicksForFrame(FixedTimestepRunner.FIXED_TICK_SECONDS).Should().Be(1);
    }

    [Fact]
    public void GetTicksForFrame_30Hz_Liefert2TicksPro33ms()
    {
        var runner = new FixedTimestepRunner { Enabled = true };
        // 33.3ms = 1.998 Ticks bei 60 Hz → 1 voller Tick + Akkumulator-Rest 0.0167
        var firstTicks = runner.GetTicksForFrame(0.0333f);
        firstTicks.Should().BeOneOf(1, 2);  // float-precision-tolerant

        // Nach erstem Frame ist der Akkumulator-Rest gefüllt → zweiter 33ms-Frame liefert 2 Ticks
        var secondTicks = runner.GetTicksForFrame(0.0333f);
        (firstTicks + secondTicks).Should().BeInRange(3, 4,
            "Total über 2 Frames sollte 3-4 Ticks sein (60 Hz Sim auf 30 Hz Host)");
    }

    [Fact]
    public void GetTicksForFrame_AkkumulatorBleibtErhalten()
    {
        var runner = new FixedTimestepRunner { Enabled = true };

        runner.GetTicksForFrame(0.010f);  // 0.010s — kein Tick (< 16.67ms)
        runner.Accumulator.Should().BeApproximately(0.010f, 0.001f);

        runner.GetTicksForFrame(0.010f);  // Total 0.020s — 1 Tick
        runner.Accumulator.Should().BeApproximately(0.020f - FixedTimestepRunner.FIXED_TICK_SECONDS, 0.001f);
    }

    [Fact]
    public void GetTicksForFrame_LangerFrameDrop_KapptBei5Ticks()
    {
        var runner = new FixedTimestepRunner { Enabled = true };

        // 1 Sekunde Frame-Drop = 60 Sim-Ticks vorgesehen, sollte aber gekappt werden
        var ticks = runner.GetTicksForFrame(1.0f);

        ticks.Should().Be(FixedTimestepRunner.MAX_TICKS_PER_FRAME, "Spiral-of-Death-Schutz");
        runner.Accumulator.Should().BeLessThanOrEqualTo(FixedTimestepRunner.FIXED_TICK_SECONDS * FixedTimestepRunner.MAX_TICKS_PER_FRAME,
            "Akkumulator wird gekappt um Cascade zu verhindern");
    }

    [Fact]
    public void GetInterpolationAlpha_RangeNullBis1()
    {
        var runner = new FixedTimestepRunner { Enabled = true };

        runner.GetInterpolationAlpha().Should().Be(0, "Initial 0");

        runner.GetTicksForFrame(0.008f);  // Halber Tick
        runner.GetInterpolationAlpha().Should().BeInRange(0.4f, 0.55f);
    }

    [Fact]
    public void Reset_AccumulatorAuf0()
    {
        var runner = new FixedTimestepRunner { Enabled = true };
        runner.GetTicksForFrame(0.010f);
        runner.Accumulator.Should().BeGreaterThan(0);

        runner.Reset();

        runner.Accumulator.Should().Be(0);
    }

    [Fact]
    public void FixedTickSeconds_Ist60Hz()
    {
        FixedTimestepRunner.FIXED_HZ.Should().Be(60);
        FixedTimestepRunner.FIXED_TICK_SECONDS.Should().BeApproximately(1f / 60f, 0.0001f);
    }

    [Fact]
    public void Determinismus_GleicheInputs_GleicheOutputs()
    {
        var r1 = new FixedTimestepRunner { Enabled = true };
        var r2 = new FixedTimestepRunner { Enabled = true };

        var deltas = new[] { 0.012f, 0.018f, 0.025f, 0.014f, 0.033f, 0.011f };
        var ticks1 = new List<int>();
        var ticks2 = new List<int>();
        foreach (var dt in deltas)
        {
            ticks1.Add(r1.GetTicksForFrame(dt));
            ticks2.Add(r2.GetTicksForFrame(dt));
        }

        ticks1.Should().Equal(ticks2, "Deterministisch — gleiche Wall-Clock-Sequenzen → gleiche Sim-Tick-Counts");
    }
}
