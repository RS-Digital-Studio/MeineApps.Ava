using System.Linq;
using BomberBlast.Models.Entities;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Regression fuer den Boss-Occupancy-Versatz: X/Y sind die MITTE des Boss (Render-Konvention),
/// die belegten Grid-Zellen muessen aus der linken Kante abgeleitet werden — nicht aus
/// GridX=floor(X/CELL) (= Zelle der Mitte). Sonst lagen Kollisions-/Explosions-Treffer-/Movement-
/// Zellen gegenueber dem gezeichneten Sprite um ~1 Zelle verschoben.
/// CreateAtGrid(gridX, gridY) soll exakt die Zellen [gridX, gridX+BossSize) belegen.
/// </summary>
public class BossOccupancyTests
{
    [Theory]
    [InlineData(BossType.FinalBoss, 3)]     // 3x3
    [InlineData(BossType.StoneGolem, 2)]    // 2x2
    [InlineData(BossType.IceDragon, 2)]
    [InlineData(BossType.FireDemon, 2)]
    [InlineData(BossType.ShadowMaster, 2)]
    public void CreateAtGrid_OccupiesExactlyTheIntendedCells(BossType type, int expectedSize)
    {
        const int gridX = 5;
        const int gridY = 4;
        var boss = BossEnemy.CreateAtGrid(gridX, gridY, type);

        boss.BossSize.Should().Be(expectedSize);
        boss.OccupancyBaseX.Should().Be(gridX, "die linke Belegungs-Zelle muss der CreateAtGrid-Spalte entsprechen");
        boss.OccupancyBaseY.Should().Be(gridY, "die obere Belegungs-Zelle muss der CreateAtGrid-Zeile entsprechen");

        var cells = boss.GetOccupiedCells().ToHashSet();
        cells.Count.Should().Be(expectedSize * expectedSize);
        for (int x = gridX; x < gridX + expectedSize; x++)
            for (int y = gridY; y < gridY + expectedSize; y++)
                cells.Should().Contain((x, y));

        // Zellen direkt links/oberhalb des Bereichs duerfen NICHT belegt sein (kein Versatz).
        boss.OccupiesCell(gridX - 1, gridY).Should().BeFalse();
        boss.OccupiesCell(gridX, gridY - 1).Should().BeFalse();
        // Rechte/untere Grenze ist exklusiv.
        boss.OccupiesCell(gridX + expectedSize, gridY).Should().BeFalse();
        boss.OccupiesCell(gridX, gridY + expectedSize).Should().BeFalse();
        // Ecken des Bereichs sind belegt.
        boss.OccupiesCell(gridX, gridY).Should().BeTrue();
        boss.OccupiesCell(gridX + expectedSize - 1, gridY + expectedSize - 1).Should().BeTrue();
    }

    [Fact]
    public void OccupiesCell_MatchesGetOccupiedCells()
    {
        var boss = BossEnemy.CreateAtGrid(7, 3, BossType.FinalBoss);
        var listed = boss.GetOccupiedCells().ToHashSet();

        // Konsistenz: OccupiesCell == Mitgliedschaft in GetOccupiedCells fuer einen Umkreis.
        for (int x = 4; x <= 12; x++)
            for (int y = 0; y <= 8; y++)
                boss.OccupiesCell(x, y).Should().Be(listed.Contains((x, y)), $"Zelle ({x},{y})");
    }
}
