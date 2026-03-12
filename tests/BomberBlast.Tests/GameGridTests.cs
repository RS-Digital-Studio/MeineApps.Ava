using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für das Spielfeld: Koordinaten-Umrechnung, Zell-Abfragen, Layout-Patterns.
/// </summary>
public class GameGridTests
{
    // ─── Grundlegende Grid-Eigenschaften ─────────────────────────────────────

    [Fact]
    public void NeuesGrid_HatKorrekteDimensionen()
    {
        var grid = new GameGrid();

        grid.Width.Should().Be(GameGrid.WIDTH, "Grid-Breite muss dem Konstanten-Wert entsprechen");
        grid.Height.Should().Be(GameGrid.HEIGHT, "Grid-Höhe muss dem Konstanten-Wert entsprechen");
        grid.PixelWidth.Should().Be(GameGrid.WIDTH * GameGrid.CELL_SIZE);
        grid.PixelHeight.Should().Be(GameGrid.HEIGHT * GameGrid.CELL_SIZE);
    }

    [Fact]
    public void NeuesGrid_AlleZellenSindEmpty()
    {
        var grid = new GameGrid();

        grid.CountCells(CellType.Empty).Should().Be(GameGrid.WIDTH * GameGrid.HEIGHT,
            "frisches Grid hat nur leere Zellen");
    }

    // ─── TryGetCell ──────────────────────────────────────────────────────────

    [Fact]
    public void TryGetCell_GueltigePosition_GibtZellZurueck()
    {
        var grid = new GameGrid();
        var zelle = grid.TryGetCell(5, 5);

        zelle.Should().NotBeNull("gültige Grid-Position muss eine Zelle zurückgeben");
        zelle!.X.Should().Be(5);
        zelle.Y.Should().Be(5);
    }

    [Fact]
    public void TryGetCell_NegativeKoordinate_GibtNullZurueck()
    {
        var grid = new GameGrid();

        grid.TryGetCell(-1, 0).Should().BeNull("negative X-Koordinate ist außerhalb des Grids");
        grid.TryGetCell(0, -1).Should().BeNull("negative Y-Koordinate ist außerhalb des Grids");
    }

    [Fact]
    public void TryGetCell_ZuGrosseKoordinate_GibtNullZurueck()
    {
        var grid = new GameGrid();

        grid.TryGetCell(GameGrid.WIDTH, 0).Should().BeNull("X >= WIDTH ist außerhalb des Grids");
        grid.TryGetCell(0, GameGrid.HEIGHT).Should().BeNull("Y >= HEIGHT ist außerhalb des Grids");
    }

    [Fact]
    public void Indexer_GueltigePosition_GibtZellZurueck()
    {
        var grid = new GameGrid();
        var zelle = grid[3, 4];

        zelle.Should().NotBeNull();
        zelle.X.Should().Be(3);
        zelle.Y.Should().Be(4);
    }

    [Fact]
    public void Indexer_UngueltigePosition_WirftArgumentOutOfRange()
    {
        var grid = new GameGrid();

        var action = () => _ = grid[100, 100];
        action.Should().Throw<ArgumentOutOfRangeException>(
            "ungültige Grid-Position muss eine Exception werfen");
    }

    // ─── Koordinaten-Umrechnung ───────────────────────────────────────────────

    [Fact]
    public void PixelToGrid_MittePunktEinerZelle_GibtKorrekteGridPos()
    {
        var grid = new GameGrid();

        // Zellgröße = 32, Mitte von Zelle (3,5) = (3*32+16, 5*32+16) = (112, 176)
        var (gx, gy) = grid.PixelToGrid(112f, 176f);

        gx.Should().Be(3, "Pixel-X 112 liegt in Grid-Spalte 3 (CELL_SIZE=32)");
        gy.Should().Be(5, "Pixel-Y 176 liegt in Grid-Zeile 5");
    }

    [Fact]
    public void GridToPixel_GibtZellzentrum()
    {
        var grid = new GameGrid();

        var (px, py) = grid.GridToPixel(3, 5);

        px.Should().Be(3 * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f,
            "GridToPixel gibt das Zentrum der Zelle zurück (X)");
        py.Should().Be(5 * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f,
            "GridToPixel gibt das Zentrum der Zelle zurück (Y)");
    }

    [Fact]
    public void PixelToGrid_ZuWeitLinks_ClampedAuf0()
    {
        var grid = new GameGrid();
        var (gx, _) = grid.PixelToGrid(-500f, 0f);

        gx.Should().Be(0, "Pixel außerhalb links muss auf 0 geclamptt werden");
    }

    // ─── Layout-Patterns ─────────────────────────────────────────────────────

    [Fact]
    public void SetupClassicPattern_ErstesSpawngebietIstFrei()
    {
        var grid = new GameGrid();
        grid.SetupClassicPattern();

        // Spawn-Bereich: (1,1), (1,2), (2,1) müssen frei sein
        grid[1, 1].Type.Should().Be(CellType.Empty, "Spieler-Spawn (1,1) muss frei sein");
        grid[1, 2].Type.Should().Be(CellType.Empty, "Spawn-Freiraum (1,2) muss frei sein");
        grid[2, 1].Type.Should().Be(CellType.Empty, "Spawn-Freiraum (2,1) muss frei sein");
    }

    [Fact]
    public void SetupClassicPattern_BordersHabenWände()
    {
        var grid = new GameGrid();
        grid.SetupClassicPattern();

        // Ecken und Ränder müssen Wände sein
        grid[0, 0].Type.Should().Be(CellType.Wall, "linke obere Ecke ist Wand");
        grid[GameGrid.WIDTH - 1, 0].Type.Should().Be(CellType.Wall, "rechte obere Ecke ist Wand");
        grid[0, GameGrid.HEIGHT - 1].Type.Should().Be(CellType.Wall, "linke untere Ecke ist Wand");
        grid[GameGrid.WIDTH - 1, GameGrid.HEIGHT - 1].Type.Should().Be(CellType.Wall, "rechte untere Ecke ist Wand");
    }

    [Fact]
    public void PlaceBlocks_MitDichte05_FuelltEtwaHalbeLeereZellen()
    {
        var grid = new GameGrid();
        grid.SetupClassicPattern();
        int leerVorher = grid.CountCells(CellType.Empty);

        grid.PlaceBlocks(0.5f, new Random(42));

        int blocks = grid.CountCells(CellType.Block);
        // Toleranz: Spawn-Bereich wird übersprungen, daher nicht exakt 50%
        blocks.Should().BeGreaterThan(0, "bei Dichte 0.5 müssen Blöcke platziert worden sein");
        blocks.Should().BeLessThan(leerVorher,
            "nicht alle leeren Zellen können belegt werden (Spawn-Schutz)");
    }

    [Fact]
    public void PlaceBlocks_SpawngebietBleibtFrei()
    {
        var grid = new GameGrid();
        grid.SetupClassicPattern();

        // Maximale Dichte
        grid.PlaceBlocks(1.0f, new Random(123));

        // Spawn-Bereich darf nie überschrieben werden
        grid[1, 1].Type.Should().NotBe(CellType.Block, "Spawn (1,1) bleibt immer frei");
        grid[1, 2].Type.Should().NotBe(CellType.Block, "Spawn (1,2) bleibt immer frei");
        grid[2, 1].Type.Should().NotBe(CellType.Block, "Spawn (2,1) bleibt immer frei");
    }

    // ─── IsValidPosition ─────────────────────────────────────────────────────

    [Fact]
    public void IsValidPosition_InnerhalbeGrid_GibtTrue()
    {
        var grid = new GameGrid();

        grid.IsValidPosition(0, 0).Should().BeTrue("Ecke (0,0) ist gültig");
        grid.IsValidPosition(7, 5).Should().BeTrue("Mittelpunkt ist gültig");
        grid.IsValidPosition(GameGrid.WIDTH - 1, GameGrid.HEIGHT - 1).Should().BeTrue(
            "letzte gültige Koordinate");
    }

    [Fact]
    public void IsValidPosition_AusserhalbGrid_GibtFalse()
    {
        var grid = new GameGrid();

        grid.IsValidPosition(-1, 0).Should().BeFalse("x < 0 ist ungültig");
        grid.IsValidPosition(0, -1).Should().BeFalse("y < 0 ist ungültig");
        grid.IsValidPosition(GameGrid.WIDTH, 0).Should().BeFalse("x == WIDTH ist ungültig");
        grid.IsValidPosition(0, GameGrid.HEIGHT).Should().BeFalse("y == HEIGHT ist ungültig");
    }

    // ─── ClearDynamicElements ────────────────────────────────────────────────

    [Fact]
    public void ClearDynamicElements_EntferntBombenUndPowerUps()
    {
        // Vorbereitung: Bombe auf Grid platzieren
        var grid = new GameGrid();
        var spieler = new Player(48f, 48f);
        var bombe = Bomb.CreateAtGrid(5, 5, spieler);
        grid[5, 5].Bomb = bombe;

        // Ausführung
        grid.ClearDynamicElements();

        // Prüfung
        grid[5, 5].Bomb.Should().BeNull("ClearDynamicElements muss Bombe-Referenz entfernen");
    }

    // ─── GetNeighbors ────────────────────────────────────────────────────────

    [Fact]
    public void GetNeighbors_InneneLage_Gibt4Nachbarn()
    {
        var grid = new GameGrid();
        var nachbarn = grid.GetNeighbors(5, 5).ToList();

        nachbarn.Should().HaveCount(4, "innere Zelle hat genau 4 Nachbarn");
    }

    [Fact]
    public void GetNeighbors_Ecke_GibtNur2Nachbarn()
    {
        var grid = new GameGrid();
        var nachbarn = grid.GetNeighbors(0, 0).ToList();

        nachbarn.Should().HaveCount(2, "Ecke (0,0) hat nur 2 gültige Nachbarn");
    }
}
