using BomberBlast.Models.Entities;
using BomberBlast.Models.Levels;

namespace BomberBlast.Models.Grid;

/// <summary>
/// The game grid (15x10 - optimized for 16:9 landscape with side HUD)
/// </summary>
public class GameGrid
{
    /// <summary>Grid width in cells</summary>
    public const int WIDTH = 15;

    /// <summary>Grid height in cells</summary>
    public const int HEIGHT = 10;

    /// <summary>Cell size in pixels (for rendering)</summary>
    public const int CELL_SIZE = 32;

    private readonly Cell[,] _cells;

    /// <summary>
    /// Get cell at position
    /// </summary>
    public Cell this[int x, int y]
    {
        get
        {
            if (x < 0 || x >= WIDTH || y < 0 || y >= HEIGHT)
                throw new ArgumentOutOfRangeException($"Cell ({x},{y}) is out of bounds");
            return _cells[x, y];
        }
    }

    /// <summary>
    /// Grid width in cells
    /// </summary>
    public int Width => WIDTH;

    /// <summary>
    /// Grid height in cells
    /// </summary>
    public int Height => HEIGHT;

    /// <summary>
    /// Grid width in pixels
    /// </summary>
    public int PixelWidth => WIDTH * CELL_SIZE;

    /// <summary>
    /// Grid height in pixels
    /// </summary>
    public int PixelHeight => HEIGHT * CELL_SIZE;

    public GameGrid()
    {
        _cells = new Cell[WIDTH, HEIGHT];
        Initialize();
    }

    /// <summary>
    /// Initialize empty grid with border walls
    /// </summary>
    private void Initialize()
    {
        for (int x = 0; x < WIDTH; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                _cells[x, y] = new Cell(x, y, CellType.Empty);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LAYOUT-PATTERNS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Layout-Pattern aufsetzen (Wände platzieren basierend auf Layout-Typ)
    /// </summary>
    public void SetupLayoutPattern(LevelLayout layout)
    {
        // Border-Wände immer zuerst
        SetupBorderWalls();

        switch (layout)
        {
            case LevelLayout.Classic:
                SetupClassicInterior();
                break;
            case LevelLayout.Cross:
                SetupCrossInterior();
                break;
            case LevelLayout.Arena:
                SetupArenaInterior();
                break;
            case LevelLayout.Maze:
                SetupMazeInterior();
                break;
            case LevelLayout.TwoRooms:
                SetupTwoRoomsInterior();
                break;
            case LevelLayout.Spiral:
                SetupSpiralInterior();
                break;
            case LevelLayout.Diagonal:
                SetupDiagonalInterior();
                break;
            case LevelLayout.BossArena:
                SetupBossArenaInterior();
                break;
            case LevelLayout.Labyrinth:
                SetupLabyrinthInterior();
                break;
            case LevelLayout.Symmetry:
                SetupSymmetryInterior();
                break;
            case LevelLayout.Islands:
                SetupIslandsInterior();
                break;
            case LevelLayout.Chaos:
                SetupChaosInterior();
                break;
        }
    }

    /// <summary>
    /// Setup classic Bomberman grid pattern with indestructible walls
    /// </summary>
    public void SetupClassicPattern()
    {
        SetupBorderWalls();
        SetupClassicInterior();
    }

    private void SetupBorderWalls()
    {
        for (int x = 0; x < WIDTH; x++)
        {
            _cells[x, 0].Type = CellType.Wall;
            _cells[x, HEIGHT - 1].Type = CellType.Wall;
        }
        for (int y = 0; y < HEIGHT; y++)
        {
            _cells[0, y].Type = CellType.Wall;
            _cells[WIDTH - 1, y].Type = CellType.Wall;
        }
    }

    /// <summary>Klassisches Schachbrett-Muster (Original Bomberman)</summary>
    private void SetupClassicInterior()
    {
        for (int x = 2; x < WIDTH - 1; x += 2)
        {
            for (int y = 2; y < HEIGHT - 1; y += 2)
            {
                _cells[x, y].Type = CellType.Wall;
            }
        }
    }

    /// <summary>Kreuzförmiger Korridor: Offene horizontale + vertikale Achse</summary>
    private void SetupCrossInterior()
    {
        int midX = WIDTH / 2;
        int midY = HEIGHT / 2;

        for (int x = 2; x < WIDTH - 1; x += 2)
        {
            for (int y = 2; y < HEIGHT - 1; y += 2)
            {
                // Kein Wall auf der Kreuz-Achse (±1 Zelle Breite)
                if (Math.Abs(x - midX) <= 1 || Math.Abs(y - midY) <= 1)
                    continue;
                _cells[x, y].Type = CellType.Wall;
            }
        }
    }

    /// <summary>Arena: Großer offener Bereich mit vereinzelten Säulen</summary>
    private void SetupArenaInterior()
    {
        // Nur 4 Säulen symmetrisch platziert
        _cells[4, 3].Type = CellType.Wall;
        _cells[10, 3].Type = CellType.Wall;
        _cells[4, 6].Type = CellType.Wall;
        _cells[10, 6].Type = CellType.Wall;

        // Kleine L-förmige Wände in den Ecken (mehr Deckung)
        _cells[3, 2].Type = CellType.Wall;
        _cells[2, 3].Type = CellType.Wall;
        _cells[11, 2].Type = CellType.Wall;
        _cells[12, 3].Type = CellType.Wall;
        _cells[3, 7].Type = CellType.Wall;
        _cells[2, 6].Type = CellType.Wall;
        _cells[11, 7].Type = CellType.Wall;
        _cells[12, 6].Type = CellType.Wall;
    }

    /// <summary>Labyrinth: Enge Gänge, viele Wände</summary>
    private void SetupMazeInterior()
    {
        // Dichteres Wand-Pattern: Jede zweite Zelle + zusätzliche Wand-Reihen
        for (int x = 2; x < WIDTH - 1; x += 2)
        {
            for (int y = 2; y < HEIGHT - 1; y += 2)
            {
                _cells[x, y].Type = CellType.Wall;
            }
        }

        // Zusätzliche Wände für engere Gänge (nicht auf Spawn-Area)
        // Horizontale Wand-Segmente
        for (int x = 3; x <= 5; x++)
            _cells[x, 4].Type = CellType.Wall;
        for (int x = 9; x <= 11; x++)
            _cells[x, 4].Type = CellType.Wall;
        for (int x = 5; x <= 7; x++)
            _cells[x, 6].Type = CellType.Wall;

        // Vertikale Wand-Segmente
        _cells[6, 2].Type = CellType.Wall;
        _cells[6, 3].Type = CellType.Wall;
        _cells[8, 6].Type = CellType.Wall;
        _cells[8, 7].Type = CellType.Wall;
    }

    /// <summary>Zwei Räume verbunden durch eine Engstelle in der Mitte</summary>
    private void SetupTwoRoomsInterior()
    {
        int midX = WIDTH / 2;

        // Vertikale Trennwand in der Mitte (mit einem Durchgang)
        for (int y = 1; y < HEIGHT - 1; y++)
        {
            // Durchgang bei y=4 und y=5
            if (y == 4 || y == 5)
                continue;
            _cells[midX, y].Type = CellType.Wall;
        }

        // Einige Säulen in jedem Raum
        // Linker Raum
        _cells[3, 3].Type = CellType.Wall;
        _cells[3, 6].Type = CellType.Wall;
        _cells[5, 4].Type = CellType.Wall;

        // Rechter Raum
        _cells[11, 3].Type = CellType.Wall;
        _cells[11, 6].Type = CellType.Wall;
        _cells[9, 5].Type = CellType.Wall;
    }

    /// <summary>Spirale: Von außen nach innen windend</summary>
    private void SetupSpiralInterior()
    {
        // Äußerer Ring (Teilwände, spiralförmig angeordnet)
        // Obere Wand (links nach rechts, lässt Eingang links)
        for (int x = 3; x <= 12; x++)
            _cells[x, 2].Type = CellType.Wall;

        // Rechte Wand (oben nach unten, lässt Eingang unten)
        for (int y = 2; y <= 6; y++)
            _cells[12, y].Type = CellType.Wall;

        // Untere Wand (rechts nach links, lässt Eingang rechts)
        for (int x = 3; x <= 11; x++)
            _cells[x, 7].Type = CellType.Wall;

        // Linke innere Wand (unten nach oben, lässt Eingang oben)
        for (int y = 4; y <= 7; y++)
            _cells[3, y].Type = CellType.Wall;

        // Innerer Kern
        _cells[6, 4].Type = CellType.Wall;
        _cells[7, 4].Type = CellType.Wall;
        _cells[8, 4].Type = CellType.Wall;
        _cells[8, 5].Type = CellType.Wall;
    }

    /// <summary>Diagonale Korridore: Wände bilden diagonale Linien</summary>
    private void SetupDiagonalInterior()
    {
        // Diagonale von links-oben nach rechts-unten
        for (int i = 0; i < Math.Min(WIDTH - 2, HEIGHT - 2); i++)
        {
            int x = 2 + i;
            int y = 2 + (i * (HEIGHT - 3)) / (WIDTH - 3);
            if (x > 0 && x < WIDTH - 1 && y > 0 && y < HEIGHT - 1 && !IsPlayerSpawnArea(x, y))
                _cells[x, y].Type = CellType.Wall;
        }

        // Gegendiagonale
        for (int i = 0; i < Math.Min(WIDTH - 2, HEIGHT - 2); i++)
        {
            int x = WIDTH - 3 - i;
            int y = 2 + (i * (HEIGHT - 3)) / (WIDTH - 3);
            if (x > 0 && x < WIDTH - 1 && y > 0 && y < HEIGHT - 1 && !IsPlayerSpawnArea(x, y))
                _cells[x, y].Type = CellType.Wall;
        }

        // Einige Säulen für mehr Struktur
        _cells[4, 4].Type = CellType.Wall;
        _cells[10, 4].Type = CellType.Wall;
        _cells[7, 2].Type = CellType.Wall;
        _cells[7, 7].Type = CellType.Wall;
    }

    /// <summary>Boss-Arena: Großer offener Raum mit nur 2 Säulen</summary>
    private void SetupBossArenaInterior()
    {
        // Nur 2 symmetrische Säulen als minimale Deckung
        _cells[5, 4].Type = CellType.Wall;
        _cells[5, 5].Type = CellType.Wall;
        _cells[9, 4].Type = CellType.Wall;
        _cells[9, 5].Type = CellType.Wall;
    }

    /// <summary>Labyrinth: Engere Gänge als Maze, mehr Sackgassen</summary>
    private void SetupLabyrinthInterior()
    {
        // Basis-Schachbrett
        for (int x = 2; x < WIDTH - 1; x += 2)
            for (int y = 2; y < HEIGHT - 1; y += 2)
                _cells[x, y].Type = CellType.Wall;

        // Zusätzliche Wände für Sackgassen und enge Gänge
        // Horizontale Sackgassen
        if (!IsPlayerSpawnArea(3, 2)) _cells[3, 2].Type = CellType.Wall;
        if (!IsPlayerSpawnArea(5, 2)) _cells[5, 2].Type = CellType.Wall;
        _cells[9, 2].Type = CellType.Wall;
        _cells[11, 2].Type = CellType.Wall;

        // Vertikale Engstellen
        _cells[3, 5].Type = CellType.Wall;
        _cells[5, 3].Type = CellType.Wall;
        _cells[7, 5].Type = CellType.Wall;
        _cells[9, 3].Type = CellType.Wall;
        _cells[11, 5].Type = CellType.Wall;

        // Sackgassen-Enden
        _cells[5, 7].Type = CellType.Wall;
        _cells[7, 7].Type = CellType.Wall;
        _cells[9, 7].Type = CellType.Wall;
        _cells[11, 7].Type = CellType.Wall;
    }

    /// <summary>Symmetrie: Gespiegelt an X/Y-Achse</summary>
    private void SetupSymmetryInterior()
    {
        int midX = WIDTH / 2;
        int midY = HEIGHT / 2;

        // Linke Hälfte definieren, dann spiegeln
        // L-förmige Strukturen
        if (!IsPlayerSpawnArea(3, 2)) _cells[3, 2].Type = CellType.Wall;
        if (!IsPlayerSpawnArea(3, 3)) _cells[3, 3].Type = CellType.Wall;
        _cells[4, 3].Type = CellType.Wall;
        _cells[5, 5].Type = CellType.Wall;
        if (!IsPlayerSpawnArea(2, 4)) _cells[2, 4].Type = CellType.Wall;

        // Spiegeln an X-Achse (rechte Hälfte)
        for (int x = 1; x < midX; x++)
        {
            for (int y = 1; y < HEIGHT - 1; y++)
            {
                if (_cells[x, y].Type == CellType.Wall)
                {
                    int mirrorX = WIDTH - 1 - x;
                    if (mirrorX > 0 && mirrorX < WIDTH - 1)
                        _cells[mirrorX, y].Type = CellType.Wall;
                }
            }
        }

        // Spiegeln an Y-Achse (untere Hälfte)
        for (int x = 1; x < WIDTH - 1; x++)
        {
            for (int y = 1; y < midY; y++)
            {
                if (_cells[x, y].Type == CellType.Wall)
                {
                    int mirrorY = HEIGHT - 1 - y;
                    if (mirrorY > 0 && mirrorY < HEIGHT - 1)
                        _cells[x, mirrorY].Type = CellType.Wall;
                }
            }
        }

        // Zentrale Säule
        _cells[midX, midY].Type = CellType.Wall;
    }

    /// <summary>Inseln: 4 Insel-Cluster mit schmalen Verbindungen</summary>
    private void SetupIslandsInterior()
    {
        // 4 Cluster-Zentren mit Wänden drumherum, verbunden durch 1-Zellen-Gänge
        // Oben-Links Cluster
        _cells[4, 3].Type = CellType.Wall;
        _cells[4, 4].Type = CellType.Wall;
        _cells[5, 3].Type = CellType.Wall;

        // Oben-Rechts Cluster
        _cells[10, 3].Type = CellType.Wall;
        _cells[10, 4].Type = CellType.Wall;
        _cells[9, 3].Type = CellType.Wall;

        // Unten-Links Cluster
        _cells[4, 6].Type = CellType.Wall;
        _cells[4, 5].Type = CellType.Wall;
        _cells[5, 6].Type = CellType.Wall;

        // Unten-Rechts Cluster
        _cells[10, 6].Type = CellType.Wall;
        _cells[10, 5].Type = CellType.Wall;
        _cells[9, 6].Type = CellType.Wall;

        // Zentrale Verbindungswände (erzwingen schmale Gänge)
        _cells[7, 2].Type = CellType.Wall;
        _cells[7, 7].Type = CellType.Wall;
        if (!IsPlayerSpawnArea(2, 5)) _cells[2, 5].Type = CellType.Wall;
        _cells[12, 5].Type = CellType.Wall;
    }

    /// <summary>Chaos: Zufällig platzierte Wände (hohe Varianz)</summary>
    private void SetupChaosInterior()
    {
        // Seed-basierter Zufall für reproduzierbare Chaos-Layouts
        var random = new Random(42); // Fester Seed für Konsistenz

        // 15-20 zufällige Wände platzieren
        int wallCount = 15 + random.Next(6);
        int placed = 0;

        while (placed < wallCount)
        {
            int x = 2 + random.Next(WIDTH - 3);
            int y = 2 + random.Next(HEIGHT - 3);

            if (x <= 0 || x >= WIDTH - 1 || y <= 0 || y >= HEIGHT - 1)
                continue;
            if (IsPlayerSpawnArea(x, y))
                continue;
            if (_cells[x, y].Type != CellType.Empty)
                continue;

            _cells[x, y].Type = CellType.Wall;
            placed++;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WELT-MECHANIK-ZELLEN PLATZIEREN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Welt-spezifische Spezial-Zellen auf leeren Boden-Feldern platzieren
    /// </summary>
    public void PlaceWorldMechanicCells(WorldMechanic mechanic, Random random)
    {
        switch (mechanic)
        {
            case WorldMechanic.Ice:
                PlaceIceCells(random);
                break;
            case WorldMechanic.Conveyor:
                PlaceConveyorCells(random);
                break;
            case WorldMechanic.Teleporter:
                PlaceTeleporterCells(random);
                break;
            case WorldMechanic.LavaCrack:
                PlaceLavaCrackCells(random);
                break;
            case WorldMechanic.PlatformGap:
                PlacePlatformGapCells(random);
                break;
            // FallingCeiling, Current, Earthquake, Fog: Runtime-Mechaniken → keine Zellen nötig
        }
    }

    /// <summary>Eis-Boden: 30-45% der leeren Zellen werden zu Eis</summary>
    private void PlaceIceCells(Random random)
    {
        var emptyCells = GetPlaceableMechanicCells();
        int count = (int)(emptyCells.Count * (0.3f + random.NextSingle() * 0.15f));
        ShuffleList(emptyCells, random);

        for (int i = 0; i < count && i < emptyCells.Count; i++)
        {
            emptyCells[i].Type = CellType.Ice;
        }
    }

    /// <summary>Förderbänder: 4-8 Streifen in verschiedenen Richtungen</summary>
    private void PlaceConveyorCells(Random random)
    {
        var directions = DirectionExtensions.GetCardinalDirections();
        int stripCount = 4 + random.Next(5); // 4-8 Streifen

        for (int s = 0; s < stripCount; s++)
        {
            var dir = directions[random.Next(directions.Length)];
            bool horizontal = dir is Direction.Left or Direction.Right;

            if (horizontal)
            {
                // Horizontaler Streifen: Eine Reihe, 3-6 Zellen lang
                int y = 2 + random.Next(HEIGHT - 3);
                int startX = 2 + random.Next(WIDTH - 6);
                int length = 3 + random.Next(4);

                for (int i = 0; i < length; i++)
                {
                    int x = startX + i;
                    if (x >= WIDTH - 1) break;
                    var cell = _cells[x, y];
                    if (cell.Type == CellType.Empty && !IsPlayerSpawnArea(x, y))
                    {
                        cell.Type = CellType.Conveyor;
                        cell.ConveyorDirection = dir;
                    }
                }
            }
            else
            {
                // Vertikaler Streifen
                int x = 2 + random.Next(WIDTH - 4);
                int startY = 2 + random.Next(HEIGHT - 5);
                int length = 3 + random.Next(3);

                for (int i = 0; i < length; i++)
                {
                    int y = startY + i;
                    if (y >= HEIGHT - 1) break;
                    var cell = _cells[x, y];
                    if (cell.Type == CellType.Empty && !IsPlayerSpawnArea(x, y))
                    {
                        cell.Type = CellType.Conveyor;
                        cell.ConveyorDirection = dir;
                    }
                }
            }
        }
    }

    /// <summary>Teleporter: 2-3 Paare von gepaarten Portalen</summary>
    private void PlaceTeleporterCells(Random random)
    {
        var emptyCells = GetPlaceableMechanicCells();
        ShuffleList(emptyCells, random);

        int pairCount = 2 + random.Next(2); // 2-3 Paare
        int colorId = 0;

        for (int p = 0; p < pairCount && emptyCells.Count >= 2; p++)
        {
            var cellA = emptyCells[0];
            emptyCells.RemoveAt(0);

            // Partner suchen: Mindestens 5 Zellen Abstand
            Cell? cellB = null;
            for (int i = 0; i < emptyCells.Count; i++)
            {
                int dist = Math.Abs(emptyCells[i].X - cellA.X) + Math.Abs(emptyCells[i].Y - cellA.Y);
                if (dist >= 5)
                {
                    cellB = emptyCells[i];
                    emptyCells.RemoveAt(i);
                    break;
                }
            }

            if (cellB == null) continue;

            cellA.Type = CellType.Teleporter;
            cellA.TeleporterTarget = (cellB.X, cellB.Y);
            cellA.TeleporterColorId = colorId;

            cellB.Type = CellType.Teleporter;
            cellB.TeleporterTarget = (cellA.X, cellA.Y);
            cellB.TeleporterColorId = colorId;

            colorId++;
        }
    }

    /// <summary>Lava-Risse: 15-25% der leeren Zellen, mit zufälligem Timer-Offset</summary>
    private void PlaceLavaCrackCells(Random random)
    {
        var emptyCells = GetPlaceableMechanicCells();
        int count = (int)(emptyCells.Count * (0.15f + random.NextSingle() * 0.1f));
        ShuffleList(emptyCells, random);

        for (int i = 0; i < count && i < emptyCells.Count; i++)
        {
            var cell = emptyCells[i];
            cell.Type = CellType.LavaCrack;
            // Zufälliger Timer-Offset, damit nicht alle gleichzeitig aktiv werden
            cell.LavaCrackTimer = random.NextSingle() * 4f;
        }
    }

    /// <summary>Plattform-Lücken: 10-15% der leeren Zellen werden zu tödlichen Lücken</summary>
    private void PlacePlatformGapCells(Random random)
    {
        var emptyCells = GetPlaceableMechanicCells();
        int count = (int)(emptyCells.Count * (0.1f + random.NextSingle() * 0.05f));
        ShuffleList(emptyCells, random);

        for (int i = 0; i < count && i < emptyCells.Count; i++)
        {
            emptyCells[i].Type = CellType.PlatformGap;
        }
    }

    /// <summary>Leere Zellen sammeln die für Mechaniken geeignet sind (kein Spawn-Bereich)</summary>
    private List<Cell> GetPlaceableMechanicCells()
    {
        var cells = new List<Cell>();
        for (int x = 1; x < WIDTH - 1; x++)
        {
            for (int y = 1; y < HEIGHT - 1; y++)
            {
                if (_cells[x, y].Type == CellType.Empty && !IsPlayerSpawnArea(x, y))
                    cells.Add(_cells[x, y]);
            }
        }
        return cells;
    }

    private static void ShuffleList<T>(List<T> list, Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BLOCK-PLATZIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Place destructible blocks randomly
    /// </summary>
    /// <param name="density">Percentage of empty cells to fill (0.0-1.0)</param>
    /// <param name="random">Random generator for reproducible levels</param>
    public void PlaceBlocks(float density, Random random)
    {
        // Collect all placeable positions (empty cells)
        var placeableCells = new List<Cell>();

        for (int x = 1; x < WIDTH - 1; x++)
        {
            for (int y = 1; y < HEIGHT - 1; y++)
            {
                var cell = _cells[x, y];

                // Skip walls und Spezial-Zellen (Ice, Conveyor, Teleporter, LavaCrack)
                if (cell.Type != CellType.Empty)
                    continue;

                // Skip player spawn area (top-left corner)
                if (IsPlayerSpawnArea(x, y))
                    continue;

                placeableCells.Add(cell);
            }
        }

        // Place blocks based on density
        int blockCount = (int)(placeableCells.Count * density);

        // Fisher-Yates Shuffle (in-place, keine LINQ-Allokation)
        for (int i = placeableCells.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (placeableCells[i], placeableCells[j]) = (placeableCells[j], placeableCells[i]);
        }

        for (int i = 0; i < blockCount && i < placeableCells.Count; i++)
        {
            placeableCells[i].Type = CellType.Block;
        }
    }

    /// <summary>
    /// Check if position is in player spawn area (must remain clear)
    /// </summary>
    private bool IsPlayerSpawnArea(int x, int y)
    {
        // Player spawns at (1,1), keep (1,1), (1,2), (2,1) clear
        return (x == 1 && y == 1) || (x == 1 && y == 2) || (x == 2 && y == 1);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ABFRAGEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if position is valid grid coordinate
    /// </summary>
    public bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < WIDTH && y >= 0 && y < HEIGHT;
    }

    /// <summary>
    /// Try get cell at position (returns null if out of bounds)
    /// </summary>
    public Cell? TryGetCell(int x, int y)
    {
        if (!IsValidPosition(x, y))
            return null;
        return _cells[x, y];
    }

    /// <summary>
    /// Get all cells of a specific type
    /// </summary>
    public IEnumerable<Cell> GetCellsOfType(CellType type)
    {
        for (int x = 0; x < WIDTH; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                if (_cells[x, y].Type == type)
                    yield return _cells[x, y];
            }
        }
    }

    /// <summary>
    /// Count cells of a specific type
    /// </summary>
    public int CountCells(CellType type)
    {
        int count = 0;
        for (int x = 0; x < WIDTH; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                if (_cells[x, y].Type == type)
                    count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Get neighbors of a cell (4-directional)
    /// </summary>
    public IEnumerable<Cell> GetNeighbors(int x, int y)
    {
        if (IsValidPosition(x - 1, y)) yield return _cells[x - 1, y];
        if (IsValidPosition(x + 1, y)) yield return _cells[x + 1, y];
        if (IsValidPosition(x, y - 1)) yield return _cells[x, y - 1];
        if (IsValidPosition(x, y + 1)) yield return _cells[x, y + 1];
    }

    /// <summary>
    /// Convert pixel position to grid position
    /// </summary>
    public (int x, int y) PixelToGrid(float pixelX, float pixelY)
    {
        int gridX = (int)MathF.Floor(pixelX / CELL_SIZE);
        int gridY = (int)MathF.Floor(pixelY / CELL_SIZE);
        return (Math.Clamp(gridX, 0, WIDTH - 1), Math.Clamp(gridY, 0, HEIGHT - 1));
    }

    /// <summary>
    /// Convert grid position to pixel position (center of cell)
    /// </summary>
    public (float x, float y) GridToPixel(int gridX, int gridY)
    {
        return (gridX * CELL_SIZE + CELL_SIZE / 2f, gridY * CELL_SIZE + CELL_SIZE / 2f);
    }

    /// <summary>
    /// Clear all dynamic elements (bombs, explosions, power-ups)
    /// </summary>
    public void ClearDynamicElements()
    {
        for (int x = 0; x < WIDTH; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                var cell = _cells[x, y];
                cell.Bomb = null;
                cell.PowerUp = null;
                cell.IsExploding = false;
                cell.ExplosionProgress = 0;
                cell.IsDestroying = false;
                cell.DestructionProgress = 0;
            }
        }
    }

    /// <summary>
    /// Reset entire grid to empty
    /// </summary>
    public void Reset()
    {
        for (int x = 0; x < WIDTH; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                _cells[x, y].Clear();
            }
        }
    }
}
