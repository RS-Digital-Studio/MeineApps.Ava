using BomberBlast.AI.PathFinding;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für den A*-Pathfinding-Algorithmus.
/// Isolierte Tests ohne GameEngine-Abhängigkeit.
/// </summary>
public class AStarTests
{
    // ─── Hilfsmethoden ───────────────────────────────────────────────────────

    /// <summary>
    /// Erzeugt ein leeres Grid mit Border-Wänden (klassisches Setup).
    /// </summary>
    private static GameGrid ErstelleLeereArena()
    {
        var grid = new GameGrid();
        grid.SetupClassicPattern();
        return grid;
    }

    /// <summary>
    /// Erzeugt ein Grid ohne jegliche Wände (komplett offen).
    /// </summary>
    private static GameGrid ErstelleOffenesGrid()
    {
        return new GameGrid(); // Nur leere Zellen, keine Wände
    }

    // ─── Grundlegende Pfadsuche ───────────────────────────────────────────────

    [Fact]
    public void FindPath_OffeneArena_GibtPfadZumZielZurueck()
    {
        // Vorbereitung
        var grid = ErstelleOffenesGrid();
        var aStar = new AStar(grid);

        // Ausführung: Von (1,1) nach (5,5)
        var pfad = aStar.FindPath(1, 1, 5, 5);

        // Prüfung
        pfad.Should().NotBeEmpty("ein Pfad muss gefunden werden wenn kein Hindernis existiert");
        pfad.Peek().Should().Be((2, 1), "der erste Schritt muss ein benachbarter Knoten sein");
    }

    [Fact]
    public void FindPath_StartGleichZiel_GibtStartpositionZurueck()
    {
        // Vorbereitung
        var grid = ErstelleOffenesGrid();
        var aStar = new AStar(grid);

        // Ausführung: Start = Ziel
        var pfad = aStar.FindPath(3, 3, 3, 3);

        // Prüfung: AStar gibt die Startposition selbst zurück
        pfad.Should().HaveCount(1, "Start=Ziel → nur die Position selbst");
        pfad.Peek().Should().Be((3, 3));
    }

    [Fact]
    public void FindPath_MitHindernissen_UmgehtWaende()
    {
        // Vorbereitung: Grid mit Wand-Block der den direkten Weg sperrt
        var grid = ErstelleOffenesGrid();
        // Wand in der Mitte platzieren (blockiert direkte Route von (1,3) nach (5,3))
        for (int x = 3; x <= 3; x++)
            grid[x, 3].Type = CellType.Wall;

        var aStar = new AStar(grid);

        // Ausführung
        var pfad = aStar.FindPath(1, 3, 5, 3);

        // Prüfung: Pfad muss existieren aber über Umweg
        pfad.Should().NotBeEmpty("Umweg über freie Zellen muss möglich sein");
        // Der Pfad darf keine Wand-Zelle (3,3) enthalten
        var schritte = new List<(int, int)>();
        while (pfad.Count > 0) schritte.Add(pfad.Dequeue());
        schritte.Should().NotContain((3, 3), "Wand-Zellen dürfen nicht betreten werden");
    }

    [Fact]
    public void FindPath_KeinPfadMoeglich_GibtLeerenPfadZurueck()
    {
        // Vorbereitung: Spieler komplett eingeschlossen
        var grid = ErstelleOffenesGrid();
        // Vollständige Einschließung von Zelle (2,2)
        grid[1, 2].Type = CellType.Wall;
        grid[3, 2].Type = CellType.Wall;
        grid[2, 1].Type = CellType.Wall;
        grid[2, 3].Type = CellType.Wall;

        var aStar = new AStar(grid);

        // Ausführung: Von eingeschlossenem Start (2,2) nach (10,5)
        var pfad = aStar.FindPath(2, 2, 10, 5);

        // Prüfung
        pfad.Should().BeEmpty("kein Pfad möglich wenn Start vollständig eingeschlossen ist");
    }

    [Fact]
    public void FindPath_PfadLaengeKorrekt_ManhattanDistanz()
    {
        // Vorbereitung: Komplett offenes Grid, gerader Weg
        var grid = ErstelleOffenesGrid();
        var aStar = new AStar(grid);

        // Ausführung: Horizontale Linie (1,1) → (5,1): 4 Schritte
        var pfad = aStar.FindPath(1, 1, 5, 1);

        // Prüfung: In einem hindernisfreien Grid = Manhattan-Distanz
        pfad.Should().HaveCount(4,
            "Manhattan-Distanz von (1,1) nach (5,1) beträgt 4 Schritte");
    }

    [Fact]
    public void FindPath_CanPassWallsTrue_DurchquertrZerstoerbareBlöcke()
    {
        // Vorbereitung: Block direkt auf dem Pfad
        var grid = ErstelleOffenesGrid();
        grid[3, 1].Type = CellType.Block; // zerstörbarer Block

        var aStar = new AStar(grid);

        // Ausführung: Wallpass-Modus
        var pfad = aStar.FindPath(1, 1, 5, 1, canPassWalls: true);

        // Prüfung: Mit canPassWalls=true muss der Block betreten werden können
        pfad.Should().NotBeEmpty("canPassWalls=true erlaubt Betreten von Block-Zellen");
        var schritte = new List<(int x, int y)>();
        while (pfad.Count > 0) schritte.Add(pfad.Dequeue());
        schritte.Should().Contain((3, 1), "Block-Zelle muss direkt betreten werden (kürzester Weg)");
    }

    [Fact]
    public void FindPath_AvoidBombsTrue_UmgehtBombenZellen()
    {
        // Vorbereitung: Bombe auf direktem Pfad
        var grid = ErstelleOffenesGrid();
        var spieler = new Player(32f, 32f); // Dummy-Spieler für Bombe
        var bombe = Bomb.CreateAtGrid(3, 1, spieler);
        grid[3, 1].Bomb = bombe;

        var aStar = new AStar(grid);

        // Ausführung: avoidBombs=true (Standard)
        var pfad = aStar.FindPath(1, 1, 5, 1, avoidBombs: true);

        // Prüfung
        pfad.Should().NotBeEmpty("Umweg um Bombe herum muss möglich sein");
        var schritte = new List<(int x, int y)>();
        while (pfad.Count > 0) schritte.Add(pfad.Dequeue());
        schritte.Should().NotContain((3, 1), "Bomben-Zellen werden bei avoidBombs=true gemieden");
    }

    [Fact]
    public void FindPath_AvoidBombsFalse_BenutztKürzestenWegTrotzBombe()
    {
        // Vorbereitung: Bombe auf direktem Pfad
        var grid = ErstelleOffenesGrid();
        var spieler = new Player(32f, 32f);
        var bombe = Bomb.CreateAtGrid(3, 1, spieler);
        grid[3, 1].Bomb = bombe;

        var aStar = new AStar(grid);

        // Ausführung: avoidBombs=false → direkter Weg trotz Bombe
        var pfad = aStar.FindPath(1, 1, 5, 1, avoidBombs: false);

        // Prüfung: Direkter Weg durch Bomben-Zelle
        pfad.Should().HaveCount(4, "direkter Weg ohne Umweg: 4 Schritte");
    }

    // ─── Safe-Cell-Finder ────────────────────────────────────────────────────

    [Fact]
    public void FindSafeCell_GefahrenzoneOhneZiel_GibtSichereZellZurueck()
    {
        // Vorbereitung
        var grid = ErstelleOffenesGrid();
        var aStar = new AStar(grid);

        // Gefahrenzone: (3,3) ist gefährlich
        var gefahrenzone = new HashSet<(int, int)> { (3, 3), (3, 4), (3, 5) };

        // Ausführung: Von mitten in der Gefahrenzone aus sichere Zelle suchen
        var sicher = aStar.FindSafeCell(3, 3, gefahrenzone);

        // Prüfung
        sicher.Should().NotBeNull("es muss eine sichere Zelle in Reichweite geben");
        gefahrenzone.Should().NotContain(sicher!.Value,
            "gefundene Zelle darf nicht in der Gefahrenzone sein");
    }

    [Fact]
    public void FindSafeCell_BeistsAllesBelegt_GibtNullZurueck()
    {
        // Vorbereitung: Kleines Grid komplett in Gefahrenzone
        var grid = ErstelleOffenesGrid();
        var aStar = new AStar(grid);

        // Alle begehbaren Zellen als gefährlich markieren
        var gefahrenzone = new HashSet<(int, int)>();
        for (int x = 0; x < GameGrid.WIDTH; x++)
            for (int y = 0; y < GameGrid.HEIGHT; y++)
                gefahrenzone.Add((x, y));

        // Ausführung
        var sicher = aStar.FindSafeCell(7, 5, gefahrenzone);

        // Prüfung: Keine sichere Zelle findbar
        sicher.Should().BeNull("wenn alle Zellen gefährlich sind, gibt es keine sichere Zelle");
    }

    [Fact]
    public void FindSafeCell_StartIstSchonSicher_GibtStartPosZurueck()
    {
        // Vorbereitung
        var grid = ErstelleOffenesGrid();
        var aStar = new AStar(grid);
        var gefahrenzone = new HashSet<(int, int)>(); // Leere Gefahrenzone

        // Ausführung: Start-Position ist selbst sicher
        var sicher = aStar.FindSafeCell(5, 5, gefahrenzone);

        // Prüfung
        sicher.Should().Be((5, 5), "wenn Start bereits sicher ist, sofort zurückgeben");
    }
}
