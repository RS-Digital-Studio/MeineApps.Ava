using BomberBlast.Core.Multiplayer;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für die Phase-30b-Engine-API (Multiplayer-Foundation).
/// Wir testen nur das öffentliche API-Verhalten ohne Engine-Construction —
/// vollständige Co-Op-Tests folgen in Phase 30c (Multi-Player-Integration).
/// </summary>
public class GameEngineMultiplayerTests
{
    [Fact]
    public void MultiplayerMode_Default_IstSingle()
    {
        // Sanity-Check: Enum-Default ist Single (Index 0)
        ((int)MultiplayerMode.Single).Should().Be(0);
        Enum.GetValues<MultiplayerMode>().Should().Contain(MultiplayerMode.LocalCoop);
        Enum.GetValues<MultiplayerMode>().Should().Contain(MultiplayerMode.LocalVersus);
        Enum.GetValues<MultiplayerMode>().Should().Contain(MultiplayerMode.AsyncGhost);
        Enum.GetValues<MultiplayerMode>().Should().Contain(MultiplayerMode.RealtimeServer);
    }

    [Fact]
    public void SpawnPositions_AusGetSpawn_KonsistentMitProperties()
    {
        MultiplayerSpawnPositions.GetSpawn(PlayerSlot.Player1).Should()
            .Be(MultiplayerSpawnPositions.Player1);
        MultiplayerSpawnPositions.GetSpawn(PlayerSlot.Player2).Should()
            .Be(MultiplayerSpawnPositions.Player2);
    }
}
