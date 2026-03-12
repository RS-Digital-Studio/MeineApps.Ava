using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für Explosions-Ausbreitung, Zelltypen und Timing.
/// </summary>
public class ExplosionTests
{
    // ─── Hilfsmethoden ───────────────────────────────────────────────────────

    private static GameGrid ErstelleLeereArena()
    {
        // Leeres Grid ohne Wände für saubere Ausbreitungs-Tests
        return new GameGrid();
    }

    private static Player ErstelleTestSpieler()
    {
        return new Player(
            3 * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f,
            3 * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f);
    }

    private static (Bomb bomb, Explosion explosion) ErstelleExplosion(
        GameGrid grid, int gridX, int gridY, int range)
    {
        var spieler = ErstelleTestSpieler();
        var bombe = new Bomb(
            gridX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f,
            gridY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f,
            spieler, range);
        var explosion = new Explosion(bombe);
        explosion.CalculateSpread(grid, range);
        return (bombe, explosion);
    }

    // ─── Ausbreitungs-Berechnung ─────────────────────────────────────────────

    [Fact]
    public void CalculateSpread_Reichweite1_TrifftNachbarzellen()
    {
        // Vorbereitung
        var grid = ErstelleLeereArena();
        var (_, explosion) = ErstelleExplosion(grid, 5, 5, 1);

        // Ausführung: AffectedCells auslesen
        var getroffenePositionen = explosion.AffectedCells
            .Select(c => (c.X, c.Y))
            .ToHashSet();

        // Prüfung: Zentrum + 4 Richtungen
        getroffenePositionen.Should().Contain((5, 5), "Zentrum muss immer getroffen werden");
        getroffenePositionen.Should().Contain((4, 5), "linke Nachbarzelle");
        getroffenePositionen.Should().Contain((6, 5), "rechte Nachbarzelle");
        getroffenePositionen.Should().Contain((5, 4), "obere Nachbarzelle");
        getroffenePositionen.Should().Contain((5, 6), "untere Nachbarzelle");
        getroffenePositionen.Should().HaveCount(5, "Reichweite 1: Zentrum + 4 Zellen");
    }

    [Fact]
    public void CalculateSpread_WandStopptExplosion()
    {
        // Vorbereitung: Wand direkt neben Bombe
        var grid = ErstelleLeereArena();
        grid[6, 5].Type = CellType.Wall; // Wand rechts der Bombe
        var (_, explosion) = ErstelleExplosion(grid, 5, 5, 3);

        // Prüfung: Wand und alles dahinter nicht getroffen
        var xPositionen = explosion.AffectedCells
            .Where(c => c.Y == 5)
            .Select(c => c.X)
            .ToList();

        xPositionen.Should().NotContain(6, "Wand blockiert Explosion komplett");
        xPositionen.Should().NotContain(7, "hinter der Wand: kein Schaden");
        xPositionen.Should().NotContain(8, "hinter der Wand: kein Schaden");
    }

    [Fact]
    public void CalculateSpread_BlockWirdGetroffen_AberStopptAusbreitung()
    {
        // Vorbereitung: Zerstörbarer Block auf Ausbreitungsweg
        var grid = ErstelleLeereArena();
        grid[7, 5].Type = CellType.Block;
        var (_, explosion) = ErstelleExplosion(grid, 5, 5, 3);

        // Prüfung: Block wird getroffen, dahinter kein Schaden
        var getroffenePositionen = explosion.AffectedCells
            .Select(c => (c.X, c.Y))
            .ToHashSet();

        getroffenePositionen.Should().Contain((7, 5),
            "Block selbst wird von Explosion getroffen (und zerstört)");
        getroffenePositionen.Should().NotContain((8, 5),
            "hinter dem Block: keine Explosion");
    }

    [Fact]
    public void CalculateSpread_Reichweite0_TrifftNurZentrum()
    {
        // Vorbereitung
        var grid = ErstelleLeereArena();
        var (_, explosion) = ErstelleExplosion(grid, 5, 5, 0);

        // Prüfung: Nur Zentrum
        explosion.AffectedCells.Should().HaveCount(1, "Reichweite 0: nur Zentrum");
        explosion.AffectedCells[0].X.Should().Be(5);
        explosion.AffectedCells[0].Y.Should().Be(5);
    }

    [Fact]
    public void CalculateSpread_AmGridRand_GeihtNichtAusserhalb()
    {
        // Vorbereitung: Bombe am Rand des Grids
        var grid = ErstelleLeereArena();
        var (_, explosion) = ErstelleExplosion(grid, 0, 0, 3);

        // Prüfung: Keine negativen Koordinaten
        explosion.AffectedCells
            .Should().AllSatisfy(c =>
            {
                c.X.Should().BeGreaterThanOrEqualTo(0, "X darf nicht negativ sein");
                c.Y.Should().BeGreaterThanOrEqualTo(0, "Y darf nicht negativ sein");
                c.X.Should().BeLessThan(GameGrid.WIDTH, "X darf nicht über Grid-Breite gehen");
                c.Y.Should().BeLessThan(GameGrid.HEIGHT, "Y darf nicht über Grid-Höhe gehen");
            });
    }

    // ─── Zelltypen ───────────────────────────────────────────────────────────

    [Fact]
    public void CalculateSpread_ZentrumHatCenterTyp()
    {
        // Vorbereitung
        var grid = ErstelleLeereArena();
        var (_, explosion) = ErstelleExplosion(grid, 5, 5, 2);

        // Prüfung: Zentrum-Zelle hat korrekten Typ
        var zentrum = explosion.AffectedCells.First(c => c.X == 5 && c.Y == 5);
        zentrum.Type.Should().Be(ExplosionCellType.Center,
            "die Bombe-Zelle selbst ist immer Center");
    }

    [Fact]
    public void CalculateSpread_EndzelleRechtsHatRightEndTyp()
    {
        // Vorbereitung
        var grid = ErstelleLeereArena();
        var (_, explosion) = ErstelleExplosion(grid, 5, 5, 2);

        // Prüfung: Letzte Zelle rechts hat RightEnd-Typ
        var endzelle = explosion.AffectedCells.FirstOrDefault(c => c.X == 7 && c.Y == 5);
        endzelle.Type.Should().Be(ExplosionCellType.RightEnd,
            "letzte Zelle der rechten Ausbreitung muss RightEnd sein");
    }

    [Fact]
    public void CalculateSpread_MittelzelleHorizontalHatKorrektenTyp()
    {
        // Vorbereitung: Reichweite 3, damit Mittelzellen entstehen
        var grid = ErstelleLeereArena();
        var (_, explosion) = ErstelleExplosion(grid, 5, 5, 3);

        // Prüfung: Zelle in der Mitte der horizontalen Ausbreitung
        var mittelzelle = explosion.AffectedCells.FirstOrDefault(c => c.X == 6 && c.Y == 5);
        mittelzelle.Type.Should().Be(ExplosionCellType.HorizontalMiddle,
            "Mittelzellen horizontaler Ausbreitung müssen HorizontalMiddle sein");
    }

    // ─── Timer und Lebenszyklus ───────────────────────────────────────────────

    [Fact]
    public void Update_TimerLaeuftAb_MarksForRemoval()
    {
        // Vorbereitung
        var grid = ErstelleLeereArena();
        var (_, explosion) = ErstelleExplosion(grid, 5, 5, 1);

        // Ausführung: Mehr als DURATION warten
        explosion.Update(Explosion.DURATION + 0.1f);

        // Prüfung
        explosion.IsMarkedForRemoval.Should().BeTrue(
            "Explosion muss nach Ablauf der Dauer zum Entfernen markiert werden");
        explosion.IsActive.Should().BeFalse(
            "abgelaufene Explosion ist nicht mehr aktiv");
    }

    [Fact]
    public void Update_NochAktiv_ProgressNähertSich1()
    {
        // Vorbereitung
        var grid = ErstelleLeereArena();
        var (_, explosion) = ErstelleExplosion(grid, 5, 5, 1);

        // Ausführung: Halbe Explosionsdauer
        explosion.Update(Explosion.DURATION * 0.5f);

        // Prüfung: Progress-Werte aktualisiert
        explosion.AffectedCells
            .Should().AllSatisfy(c =>
                c.Progress.Should().BeApproximately(0.5f, 0.05f,
                    "bei halber Dauer muss Progress bei ~0.5 sein"));
    }

    // ─── Grid-Markierungen ────────────────────────────────────────────────────

    [Fact]
    public void CalculateSpread_MarkiertGitterzellenAlsExplodierend()
    {
        // Vorbereitung
        var grid = ErstelleLeereArena();
        var (_, explosion) = ErstelleExplosion(grid, 5, 5, 1);

        // Prüfung: Betroffene Grid-Zellen sind markiert
        grid[5, 5].IsExploding.Should().BeTrue("Zentrum-Zelle muss als explodierend markiert sein");
        grid[6, 5].IsExploding.Should().BeTrue("Rechte Nachbar-Zelle muss markiert sein");
    }
}
