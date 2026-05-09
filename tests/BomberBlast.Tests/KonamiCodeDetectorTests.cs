using BomberBlast.Input;
using BomberBlast.Models.Entities;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für KonamiCodeDetector (Phase 28 — AAA-Audit PR5 Easter-Egg).
/// </summary>
public class KonamiCodeDetectorTests
{
    private static readonly KonamiCodeDetector.InputStep[] FullSequence =
    [
        KonamiCodeDetector.InputStep.Up, KonamiCodeDetector.InputStep.Up,
        KonamiCodeDetector.InputStep.Down, KonamiCodeDetector.InputStep.Down,
        KonamiCodeDetector.InputStep.Left, KonamiCodeDetector.InputStep.Right,
        KonamiCodeDetector.InputStep.Left, KonamiCodeDetector.InputStep.Right,
        KonamiCodeDetector.InputStep.Bomb, KonamiCodeDetector.InputStep.Detonate,
    ];

    [Fact]
    public void NeueInstanz_HatKeinenTriggeredFlag()
    {
        var d = new KonamiCodeDetector();
        d.HasBeenTriggered.Should().BeFalse();
        d.Progress.Should().Be(0f);
    }

    [Fact]
    public void VollstaendigeSequenz_TriggertEvent()
    {
        var d = new KonamiCodeDetector();
        var triggered = false;
        d.CodeTriggered += () => triggered = true;

        foreach (var step in FullSequence)
        {
            d.RegisterInput(step);
        }

        triggered.Should().BeTrue();
        d.HasBeenTriggered.Should().BeTrue();
    }

    [Fact]
    public void FalscherInput_ResetetSequenz()
    {
        var d = new KonamiCodeDetector();
        d.RegisterInput(KonamiCodeDetector.InputStep.Up);
        d.RegisterInput(KonamiCodeDetector.InputStep.Up);
        // Falscher Schritt
        d.RegisterInput(KonamiCodeDetector.InputStep.Right);
        // Progress muss zurück sein (auf 0 oder 1 wenn der falsche Schritt zufällig der erste ist)
        d.Progress.Should().BeLessThan(0.3f);
    }

    [Fact]
    public void FalscherInput_AlsErsterSchritt_BeginntNeueSequenz()
    {
        var d = new KonamiCodeDetector();
        d.RegisterInput(KonamiCodeDetector.InputStep.Up); // korrekt
        d.RegisterInput(KonamiCodeDetector.InputStep.Down); // falsch (zweiter sollte Up sein)
        // Up wird zwar verworfen, aber Down ist auch nicht der erste Schritt → 0
        d.RegisterInput(KonamiCodeDetector.InputStep.Up); // wieder erster Schritt
        d.Progress.Should().BeApproximately(0.1f, 0.05f);
    }

    [Fact]
    public void Timeout_ResetetSequenz()
    {
        var d = new KonamiCodeDetector();
        d.RegisterInput(KonamiCodeDetector.InputStep.Up);
        d.Update(5.0f); // > Timeout (3.0s)
        d.Progress.Should().Be(0f);
    }

    [Fact]
    public void NachTrigger_KeineWeiterenAusloesungen()
    {
        var d = new KonamiCodeDetector();
        var fireCount = 0;
        d.CodeTriggered += () => fireCount++;

        // 1. Mal triggern
        foreach (var s in FullSequence) d.RegisterInput(s);
        // 2. Mal — sollte ignoriert werden
        foreach (var s in FullSequence) d.RegisterInput(s);

        fireCount.Should().Be(1);
    }

    [Fact]
    public void FromDirection_LiefertKorrektenStep()
    {
        KonamiCodeDetector.FromDirection(Direction.Up).Should().Be(KonamiCodeDetector.InputStep.Up);
        KonamiCodeDetector.FromDirection(Direction.Down).Should().Be(KonamiCodeDetector.InputStep.Down);
        KonamiCodeDetector.FromDirection(Direction.Left).Should().Be(KonamiCodeDetector.InputStep.Left);
        KonamiCodeDetector.FromDirection(Direction.Right).Should().Be(KonamiCodeDetector.InputStep.Right);
        KonamiCodeDetector.FromDirection(Direction.None).Should().BeNull();
    }

    [Fact]
    public void Reset_LoeschtAktiveSequenz_AberNichtTriggeredFlag()
    {
        var d = new KonamiCodeDetector();
        foreach (var s in FullSequence) d.RegisterInput(s);
        d.Reset();

        d.Progress.Should().Be(0f);
        d.HasBeenTriggered.Should().BeTrue("Reset() soll nur die laufende Sequenz löschen");
    }
}
