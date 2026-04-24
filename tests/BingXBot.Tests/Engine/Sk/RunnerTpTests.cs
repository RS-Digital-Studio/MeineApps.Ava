using BingXBot.Core.Configuration;
using BingXBot.Core.Models;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>Phase 5 — Tests für Runner-TP mit Trailing (Task 4.7).</summary>
public class RunnerTpTests
{
    [Fact]
    public void EnableRunner_DefaultFalse()
    {
        var settings = new RiskSettings();
        settings.EnableRunner.Should().BeFalse();
    }

    [Fact]
    public void RunnerPercent_DefaultZehnProzent()
    {
        var settings = new RiskSettings();
        settings.RunnerPercent.Should().Be(0.10m);
    }

    [Fact]
    public void RunnerTrailingAtrMultiplier_DefaultZweiKommaNull()
    {
        var settings = new RiskSettings();
        settings.RunnerTrailingAtrMultiplier.Should().Be(2.0m);
    }

    [Fact]
    public void ExitState_RunnerActive_DefaultFalse()
    {
        var state = new PositionExitState();
        state.RunnerActive.Should().BeFalse();
    }

    [Fact]
    public void ExitState_RunnerHardCap_KannGesetztWerden()
    {
        var state = new PositionExitState { RunnerHardCap = 200m };
        state.RunnerHardCap.Should().Be(200m);
    }

    [Fact]
    public void ExitState_RunnerTrailAnchor_StartetNull()
    {
        var state = new PositionExitState();
        state.RunnerTrailAnchor.Should().Be(0m);
    }

    [Fact]
    public void SignalResult_RunnerHardCap_Durchgereicht()
    {
        var signal = new SignalResult(
            BingXBot.Core.Enums.Signal.Long, 0.8m,
            EntryPrice: 100m, StopLoss: 98m, TakeProfit: 110m,
            Reason: "Test",
            RunnerHardCap: 150m);
        signal.RunnerHardCap.Should().Be(150m);
    }

    [Fact]
    public void RunnerPercent_KannAngepasstWerden()
    {
        var settings = new RiskSettings { RunnerPercent = 0.05m };
        settings.RunnerPercent.Should().Be(0.05m);
    }
}
