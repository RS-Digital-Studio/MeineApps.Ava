using BomberBlast.Core.Modes;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für 8 konkrete IGameMode-Klassen (Mode-Plugin-Framework).
/// Validiert ModeTag-Konstanten (unique), default-no-op-Verhalten der Hooks,
/// Mode-spezifische State-Properties.
/// </summary>
public class GameModesTests
{
    private static GameModeContext CreateContext() => new()
    {
        Player = new Player(0, 0),
        Grid = new GameGrid(),
        LevelNumber = 1,
        TimeElapsed = 0f
    };

    [Theory]
    [InlineData(typeof(StoryMode), "story")]
    [InlineData(typeof(MasterMode), "master")]
    [InlineData(typeof(DailyChallengeMode), "daily_challenge")]
    [InlineData(typeof(SurvivalMode), "survival")]
    [InlineData(typeof(DungeonMode), "dungeon")]
    [InlineData(typeof(BossRushMode), "boss_rush")]
    [InlineData(typeof(DailyRaceMode), "daily_race")]
    public void ModeTag_IstKonsistentZurErwartung(Type modeType, string expectedTag)
    {
        var instance = (IGameMode)Activator.CreateInstance(modeType)!;
        instance.ModeTag.Should().Be(expectedTag);
    }

    [Fact]
    public void QuickPlayMode_HatDifficultyState()
    {
        var mode = new QuickPlayMode(difficulty: 5);
        mode.ModeTag.Should().Be("quick");
        mode.Difficulty.Should().Be(5);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(11, 10)]
    [InlineData(-5, 1)]
    public void QuickPlayMode_DifficultyWirdGeclamped(int input, int expected)
    {
        var mode = new QuickPlayMode(difficulty: input);
        mode.Difficulty.Should().Be(expected);
    }

    [Fact]
    public void SurvivalMode_HatErwarteteDefaultsFuerState()
    {
        var mode = new SurvivalMode();
        mode.TimeElapsed.Should().Be(0f);
        mode.SpawnTimer.Should().Be(0f);
        mode.SpawnInterval.Should().Be(4.0f, "Initial Spawn-Interval = 4s analog GameEngine");
    }

    [Fact]
    public void BossRushMode_HatErwarteteDefaultsFuerState()
    {
        var mode = new BossRushMode();
        mode.BossIndex.Should().Be(0, "Erster Boss = StoneGolem (Index 0)");
        mode.AccumulatedScore.Should().Be(0);
        mode.Submitted.Should().BeFalse();
        mode.TotalTimeSeconds.Should().Be(0f);
    }

    [Fact]
    public void DailyRaceMode_HatSubmittedFlag()
    {
        var mode = new DailyRaceMode();
        mode.Submitted.Should().BeFalse();
        mode.ModeTag.Should().Be("daily_race");
    }

    [Fact]
    public void GameModeBase_DefaultMethoden_SindNoOp()
    {
        var mode = new StoryMode();
        var ctx = CreateContext();

        var act1 = () => mode.Initialize(ctx);
        var act2 = () => mode.UpdateLogic(0.016f, ctx);
        var act3 = () => mode.OnGameOver(ctx);
        var act4 = () => mode.Cleanup(ctx);

        act1.Should().NotThrow();
        act2.Should().NotThrow();
        act3.Should().NotThrow();
        act4.Should().NotThrow();
        // OnLevelComplete liefert true (Engine-Default-Behavior)
        mode.OnLevelComplete(ctx).Should().BeTrue();
    }

    [Fact]
    public void AlleModeTagsSind_Unique()
    {
        var tags = new[]
        {
            new StoryMode().ModeTag,
            new MasterMode().ModeTag,
            new DailyChallengeMode().ModeTag,
            new QuickPlayMode(1).ModeTag,
            new SurvivalMode().ModeTag,
            new DungeonMode().ModeTag,
            new BossRushMode().ModeTag,
            new DailyRaceMode().ModeTag
        };

        tags.Distinct().Count().Should().Be(8, "Jeder Mode-Tag muss unique sein für Telemetrie-Filterung");
    }

    [Fact]
    public void BossRushMode_StateMutationIstMoeglich()
    {
        var mode = new BossRushMode();
        mode.BossIndex = 3;
        mode.AccumulatedScore = 50000;
        mode.Submitted = true;

        mode.BossIndex.Should().Be(3);
        mode.AccumulatedScore.Should().Be(50000);
        mode.Submitted.Should().BeTrue();
    }

    [Fact]
    public void SurvivalMode_StateMutationIstMoeglich()
    {
        var mode = new SurvivalMode();
        mode.TimeElapsed = 120f;
        mode.SpawnTimer = 2.5f;
        mode.SpawnInterval = 1.5f;

        mode.TimeElapsed.Should().Be(120f);
        mode.SpawnTimer.Should().Be(2.5f);
        mode.SpawnInterval.Should().Be(1.5f);
    }
}
