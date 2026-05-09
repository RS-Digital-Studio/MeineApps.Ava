using BomberBlast.Models.Entities;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für Phase 22b — Player Squash & Stretch (G2 aus AAA-Audit).
/// Validiert dass die Sprite-Skalierung in plausiblen Grenzen bleibt und korrekt
/// auf Direction/IsMoving/IsDying reagiert.
/// </summary>
public class PlayerSquashStretchTests
{
    [Fact]
    public void Idle_SquashScale_IstEins()
    {
        var p = new Player(0, 0)
        {
            MovementDirection = Direction.None,
        };
        p.SquashScaleX.Should().Be(1f);
        p.SquashScaleY.Should().Be(1f);
    }

    [Fact]
    public void HorizontalBewegung_StreckXAchse()
    {
        var p = new Player(0, 0)
        {
            MovementDirection = Direction.Right,
        };
        // Update für ein paar Frames damit Squash-Phase wackelt
        p.Update(0.1f);
        p.SquashScaleX.Should().BeGreaterThan(1f, "Bei Horizontal-Bewegung wird X gestreckt");
        p.SquashScaleX.Should().BeLessThan(1.15f, "Squash-Range ist subtil (~5-10%)");
    }

    [Fact]
    public void VertikalBewegung_StreckYAchse()
    {
        var p = new Player(0, 0)
        {
            MovementDirection = Direction.Up,
        };
        p.Update(0.1f);
        p.SquashScaleY.Should().BeGreaterThan(1f, "Bei Vertikal-Bewegung wird Y gestreckt");
    }

    [Fact]
    public void Sterben_KollabiertSprite()
    {
        var p = new Player(0, 0)
        {
            MovementDirection = Direction.None,
        };
        p.Kill();
        // Update für 0.25s damit DeathTimer halbe Strecke
        for (int i = 0; i < 25; i++) p.Update(0.01f);

        // Beim Tod kollabiert auf 0.6-1.0 — auf jeden Fall <1.0
        p.SquashScaleX.Should().BeLessThan(1f);
        p.SquashScaleY.Should().BeLessThan(1f);
        p.SquashScaleX.Should().BeGreaterThanOrEqualTo(0.55f);
    }

    [Fact]
    public void SquashScale_BleibtImmerImSinnvollenBereich()
    {
        // Property-Test: über viele Update-Calls darf SquashScale nie negativ oder >2 werden
        var rng = new Random(42);
        var directions = new[] { Direction.None, Direction.Up, Direction.Down, Direction.Left, Direction.Right };

        var p = new Player(0, 0);
        for (int i = 0; i < 1000; i++)
        {
            p.MovementDirection = directions[rng.Next(directions.Length)];
            p.Update((float)rng.NextDouble() * 0.05f);
            p.SquashScaleX.Should().BeInRange(0.3f, 1.5f);
            p.SquashScaleY.Should().BeInRange(0.3f, 1.5f);
        }
    }
}
