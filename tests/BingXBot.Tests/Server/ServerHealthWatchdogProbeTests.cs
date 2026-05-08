using BingXBot.Server.Services;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Server;

// v1.6.5 Phase 15 — BingX-Active-Probe fuer ConnectionDegraded.
// Tests gegen die pure-function EvaluateProbe.
public class ServerHealthWatchdogProbeTests
{
    [Fact]
    public void ProbeFailsTwice_FiresDegradedTrue()
    {
        // 1. Failure: kein Event, Counter steigt.
        var step1 = ServerHealthWatchdog.EvaluateProbe(
            isBotRunning: true, probeOk: false, passiveLiveDegraded: false,
            currentlyDegraded: false, consecutiveFailuresBefore: 0);
        step1.NewConsecutiveFailures.Should().Be(1);
        step1.FireEvent.Should().BeFalse();
        step1.NewDegraded.Should().BeFalse();

        // 2. Failure: Edge-Transition → FireEvent + Degraded=true.
        var step2 = ServerHealthWatchdog.EvaluateProbe(
            isBotRunning: true, probeOk: false, passiveLiveDegraded: false,
            currentlyDegraded: false, consecutiveFailuresBefore: step1.NewConsecutiveFailures);
        step2.NewConsecutiveFailures.Should().Be(2);
        step2.FireEvent.Should().BeTrue();
        step2.NewDegraded.Should().BeTrue();
    }

    [Fact]
    public void ProbeFailsOnce_NoEvent()
    {
        // Single-Failure-Tolerance — keine Edge-Transition bei einem einzelnen Probe-Fail.
        var outcome = ServerHealthWatchdog.EvaluateProbe(
            isBotRunning: true, probeOk: false, passiveLiveDegraded: false,
            currentlyDegraded: false, consecutiveFailuresBefore: 0);
        outcome.FireEvent.Should().BeFalse();
        outcome.NewDegraded.Should().BeFalse();
        outcome.NewConsecutiveFailures.Should().Be(1);
    }

    [Fact]
    public void ProbeRecovers_FiresDegradedFalse()
    {
        // Aktuell Degraded=true, Probe wird wieder gruen → Edge-Transition Recovery.
        var outcome = ServerHealthWatchdog.EvaluateProbe(
            isBotRunning: true, probeOk: true, passiveLiveDegraded: false,
            currentlyDegraded: true, consecutiveFailuresBefore: 2);
        outcome.NewDegraded.Should().BeFalse();
        outcome.NewConsecutiveFailures.Should().Be(0);
        outcome.FireEvent.Should().BeTrue();
    }

    [Fact]
    public void BotNotRunning_NoProbe()
    {
        // Bot nicht laufend → Probe pausiert. Wenn vorher Degraded war, wird das zurueckgesetzt + Event.
        var outcome = ServerHealthWatchdog.EvaluateProbe(
            isBotRunning: false, probeOk: false, passiveLiveDegraded: false,
            currentlyDegraded: true, consecutiveFailuresBefore: 5);
        outcome.NewDegraded.Should().BeFalse();
        outcome.NewConsecutiveFailures.Should().Be(0);
        outcome.FireEvent.Should().BeTrue("currentlyDegraded=true → Reset-Event feuert");

        // Wenn Bot nicht laeuft + nichts vorher → kein Event.
        var quiet = ServerHealthWatchdog.EvaluateProbe(
            isBotRunning: false, probeOk: false, passiveLiveDegraded: false,
            currentlyDegraded: false, consecutiveFailuresBefore: 0);
        quiet.FireEvent.Should().BeFalse();
    }

    [Fact]
    public void ProbeOk_ButPassiveDegraded_FiresDegradedTrue()
    {
        // Public-API erreichbar, aber LiveTradingManager.IsConnected=false → Auth-Token-Issue.
        var outcome = ServerHealthWatchdog.EvaluateProbe(
            isBotRunning: true, probeOk: true, passiveLiveDegraded: true,
            currentlyDegraded: false, consecutiveFailuresBefore: 0);
        outcome.NewDegraded.Should().BeTrue();
        outcome.FireEvent.Should().BeTrue();
    }

    [Fact]
    public void StableState_NoEvent()
    {
        // Wenn nichts sich aendert (probeOk + passiveOk + nicht degraded), kein Event.
        var outcome = ServerHealthWatchdog.EvaluateProbe(
            isBotRunning: true, probeOk: true, passiveLiveDegraded: false,
            currentlyDegraded: false, consecutiveFailuresBefore: 0);
        outcome.NewDegraded.Should().BeFalse();
        outcome.FireEvent.Should().BeFalse();
    }
}
