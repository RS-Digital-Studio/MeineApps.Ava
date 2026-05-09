using BomberBlast.Core.Multiplayer;
using BomberBlast.Models.Entities;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für Multiplayer-Foundation (Phase 30 — Co-Op/PvP/Async-Replay-Bausteine).
/// Validiert PlayerInputSnapshot Wire-Format-Roundtrip, InputBuffer Ring-Logik,
/// GameStateSnapshot-Hash-Determinismus.
/// </summary>
public class MultiplayerFoundationTests
{
    [Fact]
    public void Mode_DefaultIstSingle()
    {
        ((int)MultiplayerMode.Single).Should().Be(0);
    }

    [Fact]
    public void SpawnPositions_P1UndP2GegenueberliegendeEcken()
    {
        var p1 = MultiplayerSpawnPositions.GetSpawn(PlayerSlot.Player1);
        var p2 = MultiplayerSpawnPositions.GetSpawn(PlayerSlot.Player2);

        p1.Should().Be((1, 1));
        p2.x.Should().BeGreaterThan(p1.x + 8);
        p2.y.Should().BeGreaterThan(p1.y + 5);
    }

    [Fact]
    public void PlayerInputSnapshot_WireFormat_Roundtrip()
    {
        var inputs = new[]
        {
            new PlayerInputSnapshot(PlayerSlot.Player1, Direction.None, false, false, false),
            new PlayerInputSnapshot(PlayerSlot.Player1, Direction.Up, true, false, false),
            new PlayerInputSnapshot(PlayerSlot.Player2, Direction.Down, false, true, false),
            new PlayerInputSnapshot(PlayerSlot.Player2, Direction.Left, true, true, true),
            new PlayerInputSnapshot(PlayerSlot.Player1, Direction.Right, false, false, true),
        };

        foreach (var input in inputs)
        {
            var packed = input.ToWireFormat();
            var unpacked = PlayerInputSnapshot.FromWireFormat(packed);

            unpacked.Slot.Should().Be(input.Slot);
            unpacked.Direction.Should().Be(input.Direction);
            unpacked.BombPressed.Should().Be(input.BombPressed);
            unpacked.DetonatePressed.Should().Be(input.DetonatePressed);
            unpacked.ToggleSpecialBomb.Should().Be(input.ToggleSpecialBomb);
        }
    }

    [Fact]
    public void PlayerInputSnapshot_Empty_LiefertNoneNoFlags()
    {
        var snap = PlayerInputSnapshot.Empty();
        snap.Direction.Should().Be(Direction.None);
        snap.BombPressed.Should().BeFalse();
        snap.DetonatePressed.Should().BeFalse();
    }

    [Fact]
    public void InputBuffer_LeerbeiNeu_PeekLatestIstEmpty()
    {
        var b = new InputBuffer(120);
        b.Count.Should().Be(0);
        b.PeekLatest().Direction.Should().Be(Direction.None);
    }

    [Fact]
    public void InputBuffer_PushUndPeek_LiefertJuengsten()
    {
        var b = new InputBuffer(10);
        b.Push(new PlayerInputSnapshot(PlayerSlot.Player1, Direction.Up, false, false));
        b.Push(new PlayerInputSnapshot(PlayerSlot.Player1, Direction.Down, true, false));

        b.Count.Should().Be(2);
        var latest = b.PeekLatest();
        latest.Direction.Should().Be(Direction.Down);
        latest.BombPressed.Should().BeTrue();
    }

    [Fact]
    public void InputBuffer_PeekHistorical_LiefertVergangeneTicks()
    {
        var b = new InputBuffer(10);
        b.Push(new PlayerInputSnapshot(PlayerSlot.Player1, Direction.Up, false, false));
        b.Push(new PlayerInputSnapshot(PlayerSlot.Player1, Direction.Right, false, false));
        b.Push(new PlayerInputSnapshot(PlayerSlot.Player1, Direction.Down, false, false));

        b.PeekHistorical(0).Direction.Should().Be(Direction.Down);
        b.PeekHistorical(1).Direction.Should().Be(Direction.Right);
        b.PeekHistorical(2).Direction.Should().Be(Direction.Up);
    }

    [Fact]
    public void InputBuffer_RingOverwrite_AeltesteTicksWerdenVerdraengt()
    {
        var b = new InputBuffer(3);
        b.Push(new PlayerInputSnapshot(PlayerSlot.Player1, Direction.Up, false, false));
        b.Push(new PlayerInputSnapshot(PlayerSlot.Player1, Direction.Down, false, false));
        b.Push(new PlayerInputSnapshot(PlayerSlot.Player1, Direction.Left, false, false));
        b.Push(new PlayerInputSnapshot(PlayerSlot.Player1, Direction.Right, false, false)); // verdrängt Up

        b.Count.Should().Be(3);
        b.PeekLatest().Direction.Should().Be(Direction.Right);
        b.PeekHistorical(2).Direction.Should().Be(Direction.Down); // Up wurde überschrieben
    }

    [Fact]
    public void InputBuffer_Clear_ResetzCount()
    {
        var b = new InputBuffer(10);
        b.Push(new PlayerInputSnapshot(PlayerSlot.Player1, Direction.Up, false, false));
        b.Clear();
        b.Count.Should().Be(0);
        b.PeekLatest().Direction.Should().Be(Direction.None);
    }

    [Fact]
    public void GameStateSnapshot_HashIstDeterministisch()
    {
        var snap1 = new GameStateSnapshot(100, 1500, 5, 3, 2, 2200, 10, 7, 1, 1, 2, 3, 4);
        var snap2 = new GameStateSnapshot(100, 1500, 5, 3, 2, 2200, 10, 7, 1, 1, 2, 3, 4);

        snap1.ComputeHash().Should().Be(snap2.ComputeHash(),
            "Identische Snapshots müssen identischen Hash liefern");
    }

    [Fact]
    public void GameStateSnapshot_DifferentScore_LiefertAndererHash()
    {
        var snap1 = new GameStateSnapshot(100, 1500, 5, 3, 2, 2200, 10, 7, 1, 1, 2, 3, 4);
        var snap2 = new GameStateSnapshot(100, 1501, 5, 3, 2, 2200, 10, 7, 1, 1, 2, 3, 4);

        snap1.ComputeHash().Should().NotBe(snap2.ComputeHash(),
            "Score-Diff ergibt unterschiedlichen Hash (Anti-Cheat-Trigger)");
    }

    [Fact]
    public void GameStateSnapshot_IsIdenticalTo_FunktioniertSymmetrisch()
    {
        var snap1 = new GameStateSnapshot(100, 1500, 5, 3, 2, 2200, 10, 7, 1, 1, 2, 3, 4);
        var snap2 = new GameStateSnapshot(100, 1500, 5, 3, 2, 2200, 10, 7, 1, 1, 2, 3, 4);

        snap1.IsIdenticalTo(snap2).Should().BeTrue();
        snap2.IsIdenticalTo(snap1).Should().BeTrue();
    }
}
