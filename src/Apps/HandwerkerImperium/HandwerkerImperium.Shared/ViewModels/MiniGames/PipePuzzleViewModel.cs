using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels.MiniGames;

/// <summary>
/// ViewModel für das Rohr-Puzzle-Minispiel.
/// Spieler dreht Rohrsegmente, um Wasser von der Quelle zum Abfluss zu verbinden.
/// Grid ist nicht-quadratisch (Spalten x Zeilen), Start-/Endpositionen sind zufällig und gesperrt.
/// </summary>
public sealed partial class PipePuzzleViewModel : BaseMiniGameViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL-SPEZIFISCHE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<PipeTile> _tiles = [];

    [ObservableProperty]
    private int _gridCols = 6;

    [ObservableProperty]
    private int _gridRows = 5;

    [ObservableProperty]
    private int _movesCount;

    [ObservableProperty]
    private int _timeRemaining;

    [ObservableProperty]
    private int _maxTime = 60;

    [ObservableProperty]
    private bool _isPuzzleSolved;

    [ObservableProperty]
    private int _maxConnectionDistance;

    /// <summary>
    /// Breite des Puzzle-Grids in Pixeln für WrapPanel-Constraint.
    /// Jedes Tile: 52px + 4px Margin = 56px.
    /// </summary>
    public double PuzzleGridWidth => GridCols * 56;

    partial void OnGridColsChanged(int value) => OnPropertyChanged(nameof(PuzzleGridWidth));

    // Quell-/Abfluss-Positionen
    private int _sourceRow, _sourceCol, _drainRow, _drainCol;

    // ═══════════════════════════════════════════════════════════════════════
    // BASIS-KLASSE IMPLEMENTIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    protected override MiniGameType GameMiniGameType => MiniGameType.PipePuzzle;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public PipePuzzleViewModel(
        IGameStateService gameStateService,
        IAudioService audioService,
        IRewardedAdService rewardedAdService,
        ILocalizationService localizationService)
        : base(gameStateService, audioService, rewardedAdService, localizationService)
    {
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL-INITIALISIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    protected override void InitializeGame()
    {
        // Grid-Größen: Spalten x Zeilen (breiter als hoch)
        (GridCols, GridRows, MaxTime) = Difficulty switch
        {
            OrderDifficulty.Easy => (5, 4, 40),
            OrderDifficulty.Medium => (6, 5, 55),
            OrderDifficulty.Hard => (7, 6, 75),
            OrderDifficulty.Expert => (8, 7, 95),
            _ => (6, 5, 55)
        };

        // Tool-Bonus: Rohrzange gibt Extra-Sekunden
        var tool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.PipeWrench);
        TimeRemaining = MaxTime + (tool?.TimeBonus ?? 0);
        MovesCount = 0;
        IsPuzzleSolved = false;

        GeneratePuzzle();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TIMER-TICK
    // ═══════════════════════════════════════════════════════════════════════

    protected override async void OnGameTimerTick(object? sender, EventArgs e)
    {
        try
        {
            if (!IsPlaying || _isEnding) return;

            TimeRemaining--;

            if (TimeRemaining <= 0)
            {
                await EndGameAsync(false);
            }
        }
        catch
        {
            // Timer-Fehler still behandelt
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL-LOGIK: Puzzle-Generierung
    // ═══════════════════════════════════════════════════════════════════════

    private void GeneratePuzzle()
    {
        Tiles.Clear();
        var random = Random.Shared;

        // Zufällige Quell-Position am linken Rand
        _sourceRow = random.Next(GridRows);
        _sourceCol = 0;

        // Zufällige Abfluss-Position am rechten Rand (andere Zeile bevorzugt)
        _drainCol = GridCols - 1;
        if (GridRows >= 3)
        {
            do
            {
                _drainRow = random.Next(GridRows);
            } while (Math.Abs(_drainRow - _sourceRow) < 1);
        }
        else
        {
            _drainRow = random.Next(GridRows);
        }

        // Lösbaren Pfad von Quelle zu Abfluss generieren
        var path = GeneratePath(random);

        // Grid mit Rohren füllen
        for (int row = 0; row < GridRows; row++)
        {
            for (int col = 0; col < GridCols; col++)
            {
                var tile = new PipeTile
                {
                    Row = row,
                    Column = col,
                    Index = row * GridCols + col
                };

                // Prüfen ob diese Zelle auf dem Pfad liegt
                var pathCell = path.FirstOrDefault(p => p.Row == row && p.Col == col);
                if (pathCell != null)
                {
                    tile.PipeType = pathCell.Type;
                    tile.IsPartOfSolution = true;
                    tile.SolvedRotation = pathCell.SolvedRotation;
                }
                else
                {
                    tile.PipeType = GetRandomPipeType(random);
                    tile.SolvedRotation = -1;
                }

                // Quelle und Abfluss markieren (NICHT drehbar)
                if (col == _sourceCol && row == _sourceRow)
                {
                    tile.IsSource = true;
                    tile.IsLocked = true;
                    tile.Rotation = tile.SolvedRotation >= 0 ? tile.SolvedRotation : 0;
                }
                else if (col == _drainCol && row == _drainRow)
                {
                    tile.IsDrain = true;
                    tile.IsLocked = true;
                    tile.Rotation = tile.SolvedRotation >= 0 ? tile.SolvedRotation : 0;
                }
                else
                {
                    // Zufällige Anfangsrotation (Spieler muss korrigieren)
                    tile.Rotation = random.Next(4) * 90;
                }

                Tiles.Add(tile);
            }
        }

        // Initiale Verbindungen berechnen
        UpdateConnections();
    }

    private List<PathCell> GeneratePath(Random random)
    {
        var path = new List<PathCell>();
        var visited = new HashSet<(int row, int col)>();

        var result = new List<(int row, int col)>();
        if (FindPath(_sourceRow, _sourceCol, _drainRow, _drainCol, visited, result, random))
        {
            for (int i = 0; i < result.Count; i++)
            {
                var (row, col) = result[i];

                Direction? entryDir = null;
                Direction? exitDir = null;

                if (i == 0)
                {
                    entryDir = Direction.Left;
                }
                else
                {
                    var (prevRow, prevCol) = result[i - 1];
                    entryDir = GetDirectionFrom(prevRow, prevCol, row, col);
                }

                if (i == result.Count - 1)
                {
                    exitDir = Direction.Right;
                }
                else
                {
                    var (nextRow, nextCol) = result[i + 1];
                    exitDir = GetDirectionTo(row, col, nextRow, nextCol);
                }

                if (entryDir.HasValue && exitDir.HasValue)
                {
                    var (pipeType, rotation) = GetPipeTypeAndRotation(entryDir.Value, exitDir.Value);
                    path.Add(new PathCell(row, col, pipeType, exitDir.Value, rotation));
                }
            }
        }

        return path;
    }

    private bool FindPath(int row, int col, int targetRow, int targetCol,
        HashSet<(int, int)> visited, List<(int, int)> result, Random random)
    {
        if (row < 0 || row >= GridRows || col < 0 || col >= GridCols)
            return false;
        if (visited.Contains((row, col)))
            return false;

        visited.Add((row, col));
        result.Add((row, col));

        if (row == targetRow && col == targetCol)
            return true;

        // Priorisiert rechts, aber mit Zufall für hoch/runter
        var neighbors = new List<(int r, int c)>
        {
            (row, col + 1),     // Rechts (Hauptrichtung)
            (row - 1, col),     // Hoch
            (row + 1, col),     // Runter
        };

        // Nachbarn mischen mit Bias Richtung rechts
        if (random.Next(3) > 0)
        {
            if (random.Next(2) == 0)
                (neighbors[1], neighbors[2]) = (neighbors[2], neighbors[1]);
        }
        else
        {
            // Gelegentlich vertikal zuerst für interessantere Pfade
            var verticalFirst = random.Next(2) == 0 ? 1 : 2;
            (neighbors[0], neighbors[verticalFirst]) = (neighbors[verticalFirst], neighbors[0]);
        }

        // Links als seltene Option für verschlungene Pfade (ab Medium)
        if (GridCols >= 6 && random.Next(8) == 0 && col > 1)
        {
            neighbors.Add((row, col - 1));
        }

        foreach (var (nr, nc) in neighbors)
        {
            if (FindPath(nr, nc, targetRow, targetCol, visited, result, random))
                return true;
        }

        // Backtrack
        result.RemoveAt(result.Count - 1);
        visited.Remove((row, col));
        return false;
    }

    private static Direction GetDirectionFrom(int fromRow, int fromCol, int toRow, int toCol)
    {
        if (fromRow < toRow) return Direction.Up;
        if (fromRow > toRow) return Direction.Down;
        if (fromCol < toCol) return Direction.Left;
        return Direction.Right;
    }

    private static Direction GetDirectionTo(int fromRow, int fromCol, int toRow, int toCol)
    {
        if (toRow < fromRow) return Direction.Up;
        if (toRow > fromRow) return Direction.Down;
        if (toCol < fromCol) return Direction.Left;
        return Direction.Right;
    }

    /// <summary>
    /// Bestimmt Rohr-Typ und Rotation für eine Eingang-Ausgang-Verbindung.
    /// </summary>
    private static (PipeType type, int rotation) GetPipeTypeAndRotation(Direction entry, Direction exit)
    {
        // Gerades Rohr: verbindet gegenüberliegende Seiten
        if (AreOpposite(entry, exit))
        {
            return (entry == Direction.Left || entry == Direction.Right)
                ? (PipeType.Straight, 0)    // Horizontal
                : (PipeType.Straight, 90);  // Vertikal
        }

        // Eck-Rohr: verbindet zwei angrenzende Seiten (L-Form)
        var pair = (entry, exit);
        return pair switch
        {
            (Direction.Right, Direction.Down) or (Direction.Down, Direction.Right) => (PipeType.Corner, 0),
            (Direction.Down, Direction.Left) or (Direction.Left, Direction.Down) => (PipeType.Corner, 90),
            (Direction.Left, Direction.Up) or (Direction.Up, Direction.Left) => (PipeType.Corner, 180),
            (Direction.Up, Direction.Right) or (Direction.Right, Direction.Up) => (PipeType.Corner, 270),
            _ => (PipeType.Corner, 0)
        };
    }

    private static bool AreOpposite(Direction a, Direction b)
    {
        return (a == Direction.Left && b == Direction.Right) ||
               (a == Direction.Right && b == Direction.Left) ||
               (a == Direction.Up && b == Direction.Down) ||
               (a == Direction.Down && b == Direction.Up);
    }

    private static PipeType GetRandomPipeType(Random random)
    {
        return random.Next(4) switch
        {
            0 => PipeType.Straight,
            1 => PipeType.Corner,
            2 => PipeType.TJunction,
            _ => PipeType.Cross
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL-LOGIK: Tile-Rotation und Verbindungsprüfung
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task RotateTileAsync(PipeTile? tile)
    {
        if (tile == null || !IsPlaying || IsResultShown) return;

        // Quelle und Abfluss können nicht gedreht werden
        if (tile.IsLocked) return;

        tile.Rotation = (tile.Rotation + 90) % 360;
        MovesCount++;

        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Verbindungen aktualisieren (visuelles Feedback + Lösung prüfen)
        if (UpdateConnections())
        {
            IsPuzzleSolved = true;
            await EndGameAsync(true);
        }
    }

    private bool CheckIfSolved()
    {
        var visited = new HashSet<int>();
        return TracePath(_sourceRow, _sourceCol, Direction.Left, visited);
    }

    private bool TracePath(int row, int col, Direction fromDirection, HashSet<int> visited)
    {
        if (row < 0 || row >= GridRows || col < 0 || col >= GridCols)
            return false;

        int index = row * GridCols + col;

        if (visited.Contains(index))
            return false;

        visited.Add(index);

        var tile = Tiles[index];

        if (!tile.ConnectsFrom(fromDirection))
            return false;

        if (tile.IsDrain)
            return true;

        var exits = tile.GetExitDirections(fromDirection);

        foreach (var exit in exits)
        {
            int nextRow = row;
            int nextCol = col;
            Direction nextFrom;

            switch (exit)
            {
                case Direction.Up:
                    nextRow--;
                    nextFrom = Direction.Down;
                    break;
                case Direction.Down:
                    nextRow++;
                    nextFrom = Direction.Up;
                    break;
                case Direction.Left:
                    nextCol--;
                    nextFrom = Direction.Right;
                    break;
                case Direction.Right:
                    nextCol++;
                    nextFrom = Direction.Left;
                    break;
                default:
                    continue;
            }

            if (TracePath(nextRow, nextCol, nextFrom, visited))
                return true;
        }

        return false;
    }

    /// <summary>
    /// BFS von der Quelle: Markiert alle verbundenen Tiles (IsConnected + ConnectionDistance).
    /// Gibt true zurück wenn der Abfluss erreicht wurde (Puzzle gelöst).
    /// </summary>
    private bool UpdateConnections()
    {
        // Alle Verbindungen zurücksetzen
        foreach (var tile in Tiles)
        {
            tile.IsConnected = false;
            tile.ConnectionDistance = -1;
        }

        int sourceIndex = _sourceRow * GridCols + _sourceCol;
        if (sourceIndex < 0 || sourceIndex >= Tiles.Count) return false;

        var sourceTile = Tiles[sourceIndex];
        if (!sourceTile.ConnectsFrom(Direction.Left)) return false;

        // BFS von Quelle
        sourceTile.IsConnected = true;
        sourceTile.ConnectionDistance = 0;
        bool drainReached = sourceTile.IsDrain;

        var queue = new Queue<(int row, int col, Direction from, int distance)>();
        var visited = new HashSet<int> { sourceIndex };

        foreach (var exit in sourceTile.GetExitDirections(Direction.Left))
        {
            var (nr, nc, nf) = GetNeighbor(_sourceRow, _sourceCol, exit);
            queue.Enqueue((nr, nc, nf, 1));
        }

        int maxDist = 0;
        while (queue.Count > 0)
        {
            var (row, col, fromDir, dist) = queue.Dequeue();
            if (row < 0 || row >= GridRows || col < 0 || col >= GridCols) continue;

            int index = row * GridCols + col;
            if (visited.Contains(index)) continue;

            var tile = Tiles[index];
            if (!tile.ConnectsFrom(fromDir)) continue;

            visited.Add(index);
            tile.IsConnected = true;
            tile.ConnectionDistance = dist;
            if (dist > maxDist) maxDist = dist;

            if (tile.IsDrain) drainReached = true;

            foreach (var exit in tile.GetExitDirections(fromDir))
            {
                var (nr, nc, nf) = GetNeighbor(row, col, exit);
                queue.Enqueue((nr, nc, nf, dist + 1));
            }
        }

        MaxConnectionDistance = maxDist;
        return drainReached;
    }

    private static (int row, int col, Direction from) GetNeighbor(int row, int col, Direction exit)
    {
        return exit switch
        {
            Direction.Up => (row - 1, col, Direction.Down),
            Direction.Down => (row + 1, col, Direction.Up),
            Direction.Left => (row, col - 1, Direction.Right),
            Direction.Right => (row, col + 1, Direction.Left),
            _ => (row, col, exit)
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPIELENDE
    // ═══════════════════════════════════════════════════════════════════════

    private async Task EndGameAsync(bool solved)
    {
        if (!StopGame()) return;

        MiniGameRating rating;
        if (solved)
        {
            double timeRatio = (double)TimeRemaining / MaxTime;
            // Optimale Züge = Anzahl drehbarer Pfad-Tiles (nicht das gesamte Grid)
            int optimalMoves = Tiles.Count(t => t.IsPartOfSolution && !t.IsLocked);
            if (optimalMoves < 1) optimalMoves = 1;
            double moveEfficiency = optimalMoves / (double)Math.Max(MovesCount, 1);

            if (timeRatio > 0.5 && moveEfficiency > 0.4)
                rating = MiniGameRating.Perfect;
            else if (timeRatio > 0.25 && moveEfficiency > 0.25)
                rating = MiniGameRating.Good;
            else
                rating = MiniGameRating.Ok;
        }
        else
        {
            rating = MiniGameRating.Miss;
        }

        await ShowResultAsync(rating);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════════

public enum PipeType
{
    Straight,   // Verbindet zwei gegenüberliegende Seiten
    Corner,     // Verbindet zwei angrenzende Seiten (L-Form)
    TJunction,  // Verbindet drei Seiten (T-Form)
    Cross       // Verbindet alle vier Seiten
}

public enum Direction
{
    Up,
    Down,
    Left,
    Right
}

public partial class PipeTile : ObservableObject
{
    public int Row { get; set; }
    public int Column { get; set; }
    public int Index { get; set; }
    public PipeType PipeType { get; set; }
    public bool IsSource { get; set; }
    public bool IsDrain { get; set; }
    public bool IsPartOfSolution { get; set; }

    /// <summary>Ob dieses Tile gesperrt ist (nicht drehbar). Quelle/Abfluss sind gesperrt.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Die Rotation, bei der dieses Tile Teil der Lösung ist (-1 wenn nicht auf dem Pfad).</summary>
    public int SolvedRotation { get; set; } = -1;

    /// <summary>BFS-Distanz von der Quelle (-1 wenn nicht verbunden). Für progressive Wasser-Animation.</summary>
    public int ConnectionDistance { get; set; } = -1;

    [ObservableProperty]
    private int _rotation;

    [ObservableProperty]
    private bool _isConnected;

    partial void OnRotationChanged(int value)
    {
        OnPropertyChanged(nameof(HasTopOpening));
        OnPropertyChanged(nameof(HasBottomOpening));
        OnPropertyChanged(nameof(HasLeftOpening));
        OnPropertyChanged(nameof(HasRightOpening));
    }

    public bool HasTopOpening => GetOpenings().Contains(Direction.Up);
    public bool HasBottomOpening => GetOpenings().Contains(Direction.Down);
    public bool HasLeftOpening => GetOpenings().Contains(Direction.Left);
    public bool HasRightOpening => GetOpenings().Contains(Direction.Right);

    public bool ConnectsFrom(Direction fromDirection)
    {
        var openings = GetOpenings();
        return openings.Contains(fromDirection);
    }

    public List<Direction> GetExitDirections(Direction fromDirection)
    {
        var openings = GetOpenings();
        return openings.Where(d => d != fromDirection).ToList();
    }

    private List<Direction> GetOpenings()
    {
        var baseOpenings = PipeType switch
        {
            PipeType.Straight => new[] { Direction.Left, Direction.Right },
            PipeType.Corner => new[] { Direction.Right, Direction.Down },
            PipeType.TJunction => new[] { Direction.Right, Direction.Down, Direction.Left },
            PipeType.Cross => new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right },
            _ => Array.Empty<Direction>()
        };

        return baseOpenings.Select(d => RotateDirection(d, Rotation)).ToList();
    }

    private static Direction RotateDirection(Direction dir, int rotation)
    {
        int steps = rotation / 90;
        for (int i = 0; i < steps; i++)
        {
            dir = dir switch
            {
                Direction.Up => Direction.Right,
                Direction.Right => Direction.Down,
                Direction.Down => Direction.Left,
                Direction.Left => Direction.Up,
                _ => dir
            };
        }
        return dir;
    }
}

/// <summary>
/// Hilfsklasse für die Pfad-Generierung.
/// </summary>
internal record PathCell(int Row, int Col, PipeType Type, Direction ExitDirection, int SolvedRotation);
