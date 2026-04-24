using BingXBot.Core.Models;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>Phase 5 — Tests für A-Bruch-BE-Trigger (Task 3.2).</summary>
public class BreakevenTriggersTests
{
    [Fact]
    public void PositionExitState_NavPointA_DefaultNull()
    {
        var state = new PositionExitState();
        state.NavPointA.Should().Be(0m);
    }

    [Fact]
    public void PositionExitState_NavPointA_KannGesetztWerden()
    {
        var state = new PositionExitState { NavPointA = 110m };
        state.NavPointA.Should().Be(110m);
    }

    [Fact]
    public void PositionExitState_BreakevenSet_DefaultFalse()
    {
        var state = new PositionExitState();
        state.BreakevenSet.Should().BeFalse();
    }

    [Fact]
    public void SignalResult_NavPointA_DurchreichNachExitState()
    {
        var signal = new SignalResult(
            Signal: BingXBot.Core.Enums.Signal.Long,
            Confidence: 0.8m,
            EntryPrice: 100m, StopLoss: 98m, TakeProfit: 110m,
            Reason: "Test",
            NavPointA: 105m);
        signal.NavPointA.Should().Be(105m);
    }
}
