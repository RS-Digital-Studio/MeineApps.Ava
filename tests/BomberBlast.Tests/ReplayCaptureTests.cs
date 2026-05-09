using BomberBlast.Core;
using BomberBlast.Models.Entities;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für ReplayCapture (Phase 18b — Replay-Foundation).
/// Validiert Tick-Recording, Roundtrip-Serialization, Schema-Versionierung.
/// </summary>
public class ReplayCaptureTests
{
    [Fact]
    public void NeueInstanz_HatNullTicks()
    {
        var r = new ReplayCapture();
        r.TickCount.Should().Be(0);
    }

    [Fact]
    public void StartCapture_SetztSeedUndModeTag()
    {
        var r = new ReplayCapture();
        r.StartCapture(seed: 123UL, modeTag: "story", levelNumber: 5);

        r.Seed.Should().Be(123UL);
        r.ModeTag.Should().Be("story");
        r.LevelNumber.Should().Be(5);
    }

    [Fact]
    public void RecordTick_PersistiertInputs()
    {
        var r = new ReplayCapture();
        r.StartCapture(0, "story", 1);

        r.RecordTick(Direction.Up, bombPressed: true, detonatePressed: false);
        r.RecordTick(Direction.Right, bombPressed: false, detonatePressed: false);

        r.TickCount.Should().Be(2);
        var t1 = r.GetTick(0);
        t1.direction.Should().Be(Direction.Up);
        t1.bombPressed.Should().BeTrue();
        t1.detonatePressed.Should().BeFalse();

        var t2 = r.GetTick(1);
        t2.direction.Should().Be(Direction.Right);
    }

    [Fact]
    public void GetTick_OutOfRange_LiefertNoneNoBomb()
    {
        var r = new ReplayCapture();
        r.StartCapture(0, "test", 1);
        var t = r.GetTick(99);
        t.direction.Should().Be(Direction.None);
        t.bombPressed.Should().BeFalse();
    }

    [Fact]
    public void Serialize_Deserialize_Roundtrip()
    {
        var r1 = new ReplayCapture();
        r1.StartCapture(seed: 0xDEADBEEFCAFEUL, modeTag: "survival", levelNumber: 50);
        r1.RecordTick(Direction.Up, true, false);
        r1.RecordTick(Direction.Down, false, true);
        r1.RecordTick(Direction.Left, true, true);
        r1.RecordTick(Direction.Right, false, false);
        r1.RecordTick(Direction.None, false, false);

        var bytes = r1.Serialize();
        bytes.Length.Should().BeGreaterThan(0);

        var r2 = ReplayCapture.Deserialize(bytes);
        r2.Seed.Should().Be(r1.Seed);
        r2.ModeTag.Should().Be(r1.ModeTag);
        r2.LevelNumber.Should().Be(r1.LevelNumber);
        r2.TickCount.Should().Be(r1.TickCount);

        for (int i = 0; i < r1.TickCount; i++)
        {
            var t1 = r1.GetTick(i);
            var t2 = r2.GetTick(i);
            t2.direction.Should().Be(t1.direction);
            t2.bombPressed.Should().Be(t1.bombPressed);
            t2.detonatePressed.Should().Be(t1.detonatePressed);
        }
    }

    [Fact]
    public void Deserialize_InvalidVersion_Throws()
    {
        var bad = new byte[14];
        bad[0] = 99; // ungültige Schema-Version
        var act = () => ReplayCapture.Deserialize(bad);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Deserialize_TooShort_Throws()
    {
        var act = () => ReplayCapture.Deserialize(new byte[5]);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void RecordTick_OberSoftCap_WirdNichtUeberschrieben()
    {
        var r = new ReplayCapture();
        r.StartCapture(0, "x", 0);
        for (int i = 0; i <= ReplayCapture.MaxTicks + 100; i++)
        {
            r.RecordTick(Direction.Up, false, false);
        }
        r.TickCount.Should().Be(ReplayCapture.MaxTicks);
    }
}
