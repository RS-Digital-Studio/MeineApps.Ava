using BomberBlast.Graphics;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für CinematicSequencer (v2.0.46 — AAA-Audit Phase 3).
/// Validiert Play/Update/Stop-Lifecycle, Event-Trigger-at-Time, ordered Sortierung,
/// Action-Exception-Resilience.
/// </summary>
public class CinematicSequencerTests
{
    [Fact]
    public void Initial_NichtAktiv_Progress0()
    {
        var seq = new CinematicSequencer();
        seq.IsPlaying.Should().BeFalse();
        seq.Progress.Should().Be(0f);
        seq.TimeRemaining.Should().Be(0f);
    }

    [Fact]
    public void Play_StartetSequence_IstAktiv()
    {
        var seq = new CinematicSequencer();
        seq.Play(durationSeconds: 1.0f, events: new List<CinematicSequencer.TimedEvent>());

        seq.IsPlaying.Should().BeTrue();
        seq.Progress.Should().Be(0f);
        seq.TimeRemaining.Should().BeApproximately(1.0f, 0.001f);
    }

    [Fact]
    public void Update_FeuertEventBeiTrigger()
    {
        int triggered = 0;
        var seq = new CinematicSequencer();
        seq.Play(1.0f, new[]
        {
            new CinematicSequencer.TimedEvent(0.5f, () => triggered++)
        });

        seq.Update(0.4f);  // 0.4s — noch nicht
        triggered.Should().Be(0);

        seq.Update(0.2f);  // 0.6s — jetzt
        triggered.Should().Be(1);

        seq.Update(0.5f);  // 1.1s — Sequence beendet, kein erneutes Feuern
        triggered.Should().Be(1);
    }

    [Fact]
    public void Update_FeuertMehrereFaelligeEventsImSelbenFrame()
    {
        int counter = 0;
        var seq = new CinematicSequencer();
        seq.Play(2.0f, new[]
        {
            new CinematicSequencer.TimedEvent(0.1f, () => counter++),
            new CinematicSequencer.TimedEvent(0.2f, () => counter++),
            new CinematicSequencer.TimedEvent(0.3f, () => counter++)
        });

        // Großer Time-Step springt über alle 3 Events
        seq.Update(1.0f);

        counter.Should().Be(3, "alle fälligen Events in einem Frame");
    }

    [Fact]
    public void Play_SortiertUnsortierteEvents()
    {
        var order = new List<int>();
        var seq = new CinematicSequencer();
        seq.Play(1.0f, new[]
        {
            new CinematicSequencer.TimedEvent(0.5f, () => order.Add(2)),
            new CinematicSequencer.TimedEvent(0.1f, () => order.Add(1)),
            new CinematicSequencer.TimedEvent(0.8f, () => order.Add(3))
        });

        seq.Update(0.15f); // 0.1 fällig
        seq.Update(0.4f);  // 0.5 fällig
        seq.Update(0.4f);  // 0.8 fällig (1.0 ≥ duration → Stop)

        order.Should().BeEquivalentTo(new[] { 1, 2, 3 }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void Update_AbgelaufeneSequence_BeendetSichSelbst()
    {
        var seq = new CinematicSequencer();
        seq.Play(1.0f, new List<CinematicSequencer.TimedEvent>());

        seq.Update(1.5f); // Über die Duration

        seq.IsPlaying.Should().BeFalse();
        seq.Progress.Should().Be(0f);
    }

    [Fact]
    public void Stop_BrichtSequenceAb()
    {
        var seq = new CinematicSequencer();
        bool fired = false;
        seq.Play(1.0f, new[]
        {
            new CinematicSequencer.TimedEvent(0.5f, () => fired = true)
        });

        seq.Stop();
        seq.Update(0.6f);

        seq.IsPlaying.Should().BeFalse();
        fired.Should().BeFalse("Stopped Sequence darf nicht mehr feuern");
    }

    [Fact]
    public void Update_ActionWirft_AndereEventsLaufenWeiter()
    {
        int afterCount = 0;
        var seq = new CinematicSequencer();
        seq.Play(1.0f, new[]
        {
            new CinematicSequencer.TimedEvent(0.1f, () => throw new InvalidOperationException("test")),
            new CinematicSequencer.TimedEvent(0.2f, () => afterCount++)
        });

        var act = () => seq.Update(0.3f);

        act.Should().NotThrow("Best-Effort: Exception in einem Event darf Sequence nicht abbrechen");
        afterCount.Should().Be(1);
    }

    [Fact]
    public void Progress_LaeuftLinearVon0Bis1()
    {
        var seq = new CinematicSequencer();
        seq.Play(2.0f, new List<CinematicSequencer.TimedEvent>());

        seq.Update(0.5f);
        seq.Progress.Should().BeApproximately(0.25f, 0.01f);

        seq.Update(0.5f);
        seq.Progress.Should().BeApproximately(0.5f, 0.01f);

        seq.Update(1.0f); // Endet hier (Update setzt _isPlaying=false)
        seq.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public void TimeRemaining_NurAktivWaehrendSequence()
    {
        var seq = new CinematicSequencer();
        seq.Play(1.0f, new List<CinematicSequencer.TimedEvent>());
        seq.Update(0.4f);
        seq.TimeRemaining.Should().BeApproximately(0.6f, 0.01f);

        seq.Stop();
        seq.TimeRemaining.Should().Be(0f);
    }
}
