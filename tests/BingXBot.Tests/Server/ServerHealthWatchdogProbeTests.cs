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

    // === Phase 18 / A3 — Clock-Drift-Edge-Transitions ===
    // BingX recvWindow = 5 s. Default-Schwellen: Warn @ 2 s, Degrade @ 4 s.

    private static readonly TimeSpan WarnLimit = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DegradeLimit = TimeSpan.FromSeconds(4);

    [Fact]
    public void ClockDrift_BelowWarn_Quiet()
    {
        // 1 s Drift — alles im grünen Bereich.
        var outcome = ServerHealthWatchdog.EvaluateClockDrift(
            absoluteDrift: TimeSpan.FromSeconds(1),
            warnThreshold: WarnLimit, degradeThreshold: DegradeLimit,
            currentlyDriftDegraded: false);
        outcome.NewDriftDegraded.Should().BeFalse();
        outcome.ShouldWarn.Should().BeFalse();
        outcome.ShouldFireDegradedEvent.Should().BeFalse();
        outcome.ShouldFireRecoveryEvent.Should().BeFalse();
    }

    [Fact]
    public void ClockDrift_BetweenWarnAndDegrade_OnlyWarn()
    {
        // 3 s Drift — Warning, aber noch nicht Degrade.
        var outcome = ServerHealthWatchdog.EvaluateClockDrift(
            absoluteDrift: TimeSpan.FromSeconds(3),
            warnThreshold: WarnLimit, degradeThreshold: DegradeLimit,
            currentlyDriftDegraded: false);
        outcome.NewDriftDegraded.Should().BeFalse();
        outcome.ShouldWarn.Should().BeTrue();
        outcome.ShouldFireDegradedEvent.Should().BeFalse();
    }

    [Fact]
    public void ClockDrift_AboveDegrade_FiresDegradedFirstTime()
    {
        // 5 s Drift — Edge-Transition false→true → Event publishen.
        var outcome = ServerHealthWatchdog.EvaluateClockDrift(
            absoluteDrift: TimeSpan.FromSeconds(5),
            warnThreshold: WarnLimit, degradeThreshold: DegradeLimit,
            currentlyDriftDegraded: false);
        outcome.NewDriftDegraded.Should().BeTrue();
        outcome.ShouldFireDegradedEvent.Should().BeTrue();
    }

    [Fact]
    public void ClockDrift_AlreadyDegraded_StaysSilent()
    {
        // 5 s Drift, war schon degraded → kein neues Event (kein Spam).
        var outcome = ServerHealthWatchdog.EvaluateClockDrift(
            absoluteDrift: TimeSpan.FromSeconds(5),
            warnThreshold: WarnLimit, degradeThreshold: DegradeLimit,
            currentlyDriftDegraded: true);
        outcome.NewDriftDegraded.Should().BeTrue();
        outcome.ShouldFireDegradedEvent.Should().BeFalse();
    }

    [Fact]
    public void ClockDrift_Recovers_FiresRecoveryEvent()
    {
        // 1 s Drift, war degraded → Edge-Transition true→false → Recovery-Event.
        var outcome = ServerHealthWatchdog.EvaluateClockDrift(
            absoluteDrift: TimeSpan.FromMilliseconds(800),
            warnThreshold: WarnLimit, degradeThreshold: DegradeLimit,
            currentlyDriftDegraded: true);
        outcome.NewDriftDegraded.Should().BeFalse();
        outcome.ShouldFireRecoveryEvent.Should().BeTrue();
    }

    [Fact]
    public void ClockDrift_RecoversIntoWarnZone_FiresRecoveryButLogsWarn()
    {
        // 3 s Drift (Warn-Zone), war degraded → Recovery aus Degrade-Zone, aber weiterhin Log-Warning.
        var outcome = ServerHealthWatchdog.EvaluateClockDrift(
            absoluteDrift: TimeSpan.FromSeconds(3),
            warnThreshold: WarnLimit, degradeThreshold: DegradeLimit,
            currentlyDriftDegraded: true);
        outcome.NewDriftDegraded.Should().BeFalse();
        outcome.ShouldWarn.Should().BeTrue();
        outcome.ShouldFireRecoveryEvent.Should().BeTrue();
    }

    [Fact]
    public void ClockDrift_NegativeDrift_AlsoCounts()
    {
        // Pi-Uhr läuft 5 s VORAUS — derselbe Disaster-Mode.
        // (Aufrufer übergibt absoluteDrift = (utcNow - serverTime).Duration() — also immer ≥ 0.)
        var outcome = ServerHealthWatchdog.EvaluateClockDrift(
            absoluteDrift: TimeSpan.FromSeconds(6),
            warnThreshold: WarnLimit, degradeThreshold: DegradeLimit,
            currentlyDriftDegraded: false);
        outcome.NewDriftDegraded.Should().BeTrue();
        outcome.ShouldFireDegradedEvent.Should().BeTrue();
    }
}
