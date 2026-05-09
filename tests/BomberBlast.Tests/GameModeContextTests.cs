using BomberBlast.Core.Modes;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für IGameMode-Skeleton (v2.0.46 — AAA-Audit Phase 3).
/// Validiert Compile-Vertrag + Required-Properties.
/// Tatsächliche Mode-Implementierungen kommen in Phase 5+ (separater Sprint).
/// </summary>
public class GameModeContextTests
{
    [Fact]
    public void GameModeContext_KannMitRequiredPropertiesErzeugtWerden()
    {
        var player = new Player(0, 0);
        var grid = new GameGrid();

        var ctx = new GameModeContext
        {
            Player = player,
            Grid = grid,
            CurrentLevel = null,
            LevelNumber = 1,
            TimeElapsed = 0f
        };

        ctx.Player.Should().BeSameAs(player);
        ctx.Grid.Should().BeSameAs(grid);
        ctx.CurrentLevel.Should().BeNull("optional bei Survival/Dungeon");
        ctx.LevelNumber.Should().Be(1);
    }

    [Fact]
    public void GameModeContext_LevelNumber_KannHoheZahlenHalten()
    {
        var ctx = new GameModeContext
        {
            Player = new Player(0, 0),
            Grid = new GameGrid(),
            LevelNumber = 99,  // Daily Challenge
            TimeElapsed = 60f
        };

        ctx.LevelNumber.Should().Be(99);
        ctx.TimeElapsed.Should().Be(60f);
    }

    [Fact]
    public void IGameMode_InterfaceMembers_CompileCheck()
    {
        // Compile-only-Test: stellt sicher dass das Interface alle erwarteten Members hat.
        // Wenn IGameMode jemals umbenannt/erweitert wird, schlägt dieser Test fehl.
        var members = typeof(IGameMode).GetMembers();

        members.Should().Contain(m => m.Name == nameof(IGameMode.ModeTag));
        members.Should().Contain(m => m.Name == nameof(IGameMode.Initialize));
        members.Should().Contain(m => m.Name == nameof(IGameMode.UpdateLogic));
        members.Should().Contain(m => m.Name == nameof(IGameMode.OnLevelComplete));
        members.Should().Contain(m => m.Name == nameof(IGameMode.OnGameOver));
        members.Should().Contain(m => m.Name == nameof(IGameMode.Cleanup));
    }
}
