using BomberBlast.Models.Levels;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für LevelLayoutGenerator Daily-Race-Determinismus (v2.0.44).
/// Kritisch: Daily Race muss bit-identische Levels für alle Spieler weltweit erzeugen
/// — sonst ist die Bestenliste unfair. Tests validieren GenerateDailyChallengeLevel(seed)
/// liefert reproduzierbares Ergebnis.
/// </summary>
public class LevelLayoutGeneratorTests
{
    [Fact]
    public void GenerateDailyChallengeLevel_GleicherSeed_LiefertIdentischesLayout()
    {
        var level1 = LevelLayoutGenerator.GenerateDailyChallengeLevel(20260509);
        var level2 = LevelLayoutGenerator.GenerateDailyChallengeLevel(20260509);

        level1.Should().NotBeNull();
        level2.Should().NotBeNull();
        level1.Number.Should().Be(level2.Number);
        level1.IsBossLevel.Should().Be(level2.IsBossLevel);
        level1.BlockDensity.Should().Be(level2.BlockDensity);
        level1.Enemies.Count.Should().Be(level2.Enemies.Count);
        level1.TimeLimit.Should().Be(level2.TimeLimit);
    }

    [Fact]
    public void GenerateDailyChallengeLevel_VerschiedeneSeeds_LiefernUnterschiedlicheLayouts()
    {
        var level1 = LevelLayoutGenerator.GenerateDailyChallengeLevel(20260509);
        var level2 = LevelLayoutGenerator.GenerateDailyChallengeLevel(20260510);

        // Mindestens eine Eigenschaft sollte sich unterscheiden bei unterschiedlichen Seeds
        var different = level1.BlockDensity != level2.BlockDensity
                     || level1.Enemies.Count != level2.Enemies.Count
                     || level1.TimeLimit != level2.TimeLimit
                     || level1.Layout != level2.Layout
                     || level1.Mechanic != level2.Mechanic;

        different.Should().BeTrue("Unterschiedliche Seeds sollten unterschiedliche Layouts erzeugen");
    }

    [Fact]
    public void GenerateDailyChallengeLevel_SeedNull_WirftNicht()
    {
        var act = () => LevelLayoutGenerator.GenerateDailyChallengeLevel(0);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateDailyChallengeLevel_MaxIntSeed_WirftNicht()
    {
        var act = () => LevelLayoutGenerator.GenerateDailyChallengeLevel(int.MaxValue);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateDailyChallengeLevel_GeneriertImmerEinValidesLevel()
    {
        var level = LevelLayoutGenerator.GenerateDailyChallengeLevel(12345);

        level.Should().NotBeNull();
        level.Enemies.Count.Should().BeGreaterThan(0);
        level.BlockDensity.Should().BeInRange(0f, 1f);
        level.TimeLimit.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateLevel_Story_LiefertReproduzierbaresLayout()
    {
        var l1 = LevelLayoutGenerator.GenerateLevel(5, 0);
        var l2 = LevelLayoutGenerator.GenerateLevel(5, 0);

        l1.Number.Should().Be(l2.Number);
        l1.IsBossLevel.Should().Be(l2.IsBossLevel);
        l1.Enemies.Count.Should().Be(l2.Enemies.Count);
    }

    [Fact]
    public void GenerateLevel_BossLevel10_IstBoss()
    {
        var level = LevelLayoutGenerator.GenerateLevel(10, 0);
        level.IsBossLevel.Should().BeTrue("Level 10 ist ein Boss-Level (jedes 10. Level)");
    }

    [Fact]
    public void GenerateLevel_Level7_IstKeinBoss()
    {
        var level = LevelLayoutGenerator.GenerateLevel(7, 0);
        level.IsBossLevel.Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void GenerateLevel_AlleMainStoryLevel_LiefernValideObjekte(int levelNumber)
    {
        var level = LevelLayoutGenerator.GenerateLevel(levelNumber, 0);

        level.Should().NotBeNull();
        level.Number.Should().Be(levelNumber);
        // Boss-Level (10/20/30/.../100) haben Boss-Spawn, keine EnemySpawn-Liste — beides valide
        if (!level.IsBossLevel)
            level.Enemies.Count.Should().BeGreaterThan(0);
        else
            level.BossKind.Should().NotBeNull();
    }

    [Fact]
    public void GenerateQuickPlayLevel_GleicherSeedDifficulty_LiefertIdentisch()
    {
        var l1 = LevelLayoutGenerator.GenerateQuickPlayLevel(99999, 5);
        var l2 = LevelLayoutGenerator.GenerateQuickPlayLevel(99999, 5);

        l1.Enemies.Count.Should().Be(l2.Enemies.Count);
        l1.BlockDensity.Should().Be(l2.BlockDensity);
        l1.TimeLimit.Should().Be(l2.TimeLimit);
    }
}
