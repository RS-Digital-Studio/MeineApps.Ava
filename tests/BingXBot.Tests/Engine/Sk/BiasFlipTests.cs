using BingXBot.Engine.Indicators;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>Phase 5 — Tests für Bias-Flip (Task 4.9).</summary>
public class BiasFlipTests
{
    [Fact]
    public void InitAsBiasFlip_SetztPointsUndState()
    {
        var sm = new SequenceStateMachine(0.3m, 0.2m, 0.382m, 0.786m, minPoint0Candles: 1);
        sm.InitAsBiasFlip(oldExtreme: 120m, breakPrice: 95m, breakIndex: 10);

        sm.Point0.Should().Be(120m);
        sm.PointA.Should().Be(95m);
        sm.State.Should().Be(SmState.SucheB);
        sm.PromotedToLarger.Should().BeTrue();
    }

    [Fact]
    public void InitAsBiasFlip_ResetsInvalidationHints()
    {
        var sm = new SequenceStateMachine(0.3m, 0.2m, 0.382m, 0.786m, minPoint0Candles: 1);
        sm.InitAsBiasFlip(100m, 90m, 5);
        sm.Has100ExtensionReached.Should().BeFalse();
    }

    [Fact]
    public void WasActivatedBeforeInvalidation_DefaultFalse()
    {
        var sm = new SequenceStateMachine(0.3m, 0.2m, 0.382m, 0.786m, minPoint0Candles: 1);
        sm.WasActivatedBeforeInvalidation.Should().BeFalse();
    }

    [Fact]
    public void ResetBiasFlipHint_LoeschtAlleFelder()
    {
        var sm = new SequenceStateMachine(0.3m, 0.2m, 0.382m, 0.786m, minPoint0Candles: 1);
        sm.InitAsBiasFlip(100m, 90m, 5);
        sm.ResetBiasFlipHint();
        sm.WasActivatedBeforeInvalidation.Should().BeFalse();
        sm.LastBreakPrice.Should().Be(0m);
        sm.LastBreakIndex.Should().Be(-1);
    }

    [Fact]
    public void InitAsBiasFlip_PotentialB_GleichPointA()
    {
        var sm = new SequenceStateMachine(0.3m, 0.2m, 0.382m, 0.786m, minPoint0Candles: 1);
        sm.InitAsBiasFlip(120m, 95m, 10);
        sm.PotentialB.Should().Be(95m);
    }

    [Fact]
    public void FromCandlesBoth_EnableBiasFlipParameter_VorhandenAlsDefault()
    {
        // Compile-Check: Parameter enableBiasFlip mit Default=true
        var candles = new List<BingXBot.Core.Models.Candle>();
        var t = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
            candles.Add(new BingXBot.Core.Models.Candle(t, 100m, 101m, 99m, 100m, 1000m, t.AddHours(1)));
        var (_, _, _) = SequenceStateMachine.FromCandlesBoth(candles, enableBiasFlip: false);
        // No exception = pass
    }

    [Fact]
    public void Point0IndexNachBiasFlip_KorrektGesetzt()
    {
        var sm = new SequenceStateMachine(0.3m, 0.2m, 0.382m, 0.786m, minPoint0Candles: 1);
        sm.InitAsBiasFlip(120m, 95m, 10);
        sm.Point0Index.Should().Be(9); // breakIndex - 1
        sm.PointAIndex.Should().Be(10);
    }
}
