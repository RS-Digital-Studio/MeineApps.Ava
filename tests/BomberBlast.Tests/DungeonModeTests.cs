using BomberBlast.Core.Modes;
using BomberBlast.Models.Dungeon;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für DungeonMode-State (v2.0.52 — Phase 9).
/// Validiert Defaults für alle 13 migrierten Properties + Mutability + ModeTag-Konsistenz.
/// </summary>
public class DungeonModeTests
{
    [Fact]
    public void Defaults_AllePropertiesAuf0OderFalseOderNone()
    {
        var mode = new DungeonMode();

        mode.ModeTag.Should().Be("dungeon");

        // Legendaere Buffs
        mode.TimeFreezeTimer.Should().Be(0f);
        mode.PhantomWalkAvailable.Should().BeFalse();
        mode.PhantomWalkActive.Should().BeFalse();
        mode.PhantomWalkTimer.Should().Be(0f);
        mode.PhantomCooldownTimer.Should().Be(0f);
        mode.PlayerHadWallpassBeforePhantom.Should().BeFalse();

        // Synergie-Flags
        mode.SynergyBlitzkriegActive.Should().BeFalse();
        mode.SynergyFortressActive.Should().BeFalse();
        mode.FortressRegenTimer.Should().Be(0f);
        mode.SynergyMidasActive.Should().BeFalse();
        mode.SynergyElementalActive.Should().BeFalse();
        mode.DungeonBombFuseReduction.Should().Be(0f);
        mode.DungeonEnemySlowActive.Should().BeFalse();

        // Floor-Modifier
        mode.FloorModifier.Should().Be(DungeonFloorModifier.None);
        mode.ModifierRegenTimer.Should().Be(0f);
    }

    [Fact]
    public void TimeFreezeTimer_KannGesetztWerden()
    {
        var mode = new DungeonMode { TimeFreezeTimer = 3.0f };
        mode.TimeFreezeTimer.Should().Be(3.0f);
    }

    [Fact]
    public void Phantom_StateMutationFunktioniert()
    {
        var mode = new DungeonMode
        {
            PhantomWalkAvailable = true,
            PhantomWalkActive = true,
            PhantomWalkTimer = 5.0f,
            PhantomCooldownTimer = 30.0f,
            PlayerHadWallpassBeforePhantom = true
        };

        mode.PhantomWalkAvailable.Should().BeTrue();
        mode.PhantomWalkActive.Should().BeTrue();
        mode.PhantomWalkTimer.Should().Be(5.0f);
        mode.PhantomCooldownTimer.Should().Be(30.0f);
        mode.PlayerHadWallpassBeforePhantom.Should().BeTrue();
    }

    [Fact]
    public void Synergien_AllePropertyAccessoren()
    {
        var mode = new DungeonMode
        {
            SynergyBlitzkriegActive = true,
            SynergyFortressActive = true,
            FortressRegenTimer = 12.5f,
            SynergyMidasActive = true,
            SynergyElementalActive = true,
            DungeonBombFuseReduction = 0.8f,
            DungeonEnemySlowActive = true
        };

        mode.SynergyBlitzkriegActive.Should().BeTrue();
        mode.SynergyFortressActive.Should().BeTrue();
        mode.FortressRegenTimer.Should().Be(12.5f);
        mode.SynergyMidasActive.Should().BeTrue();
        mode.SynergyElementalActive.Should().BeTrue();
        mode.DungeonBombFuseReduction.Should().Be(0.8f);
        mode.DungeonEnemySlowActive.Should().BeTrue();
    }

    [Fact]
    public void FloorModifier_KannAlleEnumWerteHalten()
    {
        foreach (var modifier in Enum.GetValues<DungeonFloorModifier>())
        {
            var mode = new DungeonMode { FloorModifier = modifier };
            mode.FloorModifier.Should().Be(modifier);
        }
    }

    [Fact]
    public void ModifierRegenTimer_KannGesetztWerden()
    {
        var mode = new DungeonMode { ModifierRegenTimer = 15.0f };
        mode.ModifierRegenTimer.Should().Be(15.0f);
    }

    [Fact]
    public void ModeTag_IstUniqueZuAllenAnderenModi()
    {
        var dungeonTag = new DungeonMode().ModeTag;
        var otherTags = new[]
        {
            new StoryMode().ModeTag,
            new MasterMode().ModeTag,
            new DailyChallengeMode().ModeTag,
            new QuickPlayMode(1).ModeTag,
            new SurvivalMode().ModeTag,
            new BossRushMode().ModeTag,
            new DailyRaceMode().ModeTag
        };

        otherTags.Should().NotContain(dungeonTag);
    }

    [Fact]
    public void IGameMode_DefaultHooksSindNoOp()
    {
        var mode = new DungeonMode();
        var ctx = new GameModeContext
        {
            Player = new BomberBlast.Models.Entities.Player(0, 0),
            Grid = new BomberBlast.Models.Grid.GameGrid(),
            LevelNumber = 1,
            TimeElapsed = 0f
        };

        var act1 = () => mode.Initialize(ctx);
        var act2 = () => mode.UpdateLogic(0.016f, ctx);
        var act3 = () => mode.OnGameOver(ctx);
        var act4 = () => mode.Cleanup(ctx);

        act1.Should().NotThrow();
        act2.Should().NotThrow();
        act3.Should().NotThrow();
        act4.Should().NotThrow();
        mode.OnLevelComplete(ctx).Should().BeTrue();
    }

    [Fact]
    public void DungeonMode_State_BleibtUeberMehrereInstanzen_Unabhaengig()
    {
        // Sicherstellen dass keine static-Sharings zwischen Instanzen
        var mode1 = new DungeonMode { PhantomWalkActive = true, FortressRegenTimer = 5f };
        var mode2 = new DungeonMode();

        mode2.PhantomWalkActive.Should().BeFalse("Neue Instanz darf nicht alte State sehen");
        mode2.FortressRegenTimer.Should().Be(0f);
    }
}
