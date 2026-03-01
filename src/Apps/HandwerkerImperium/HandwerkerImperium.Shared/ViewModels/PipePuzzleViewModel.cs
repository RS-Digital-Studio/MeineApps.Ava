using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the Pipe Puzzle mini-game.
/// Player must rotate pipe segments to connect water from source to drain.
/// Grid is non-square (cols x rows), start/end positions are randomized and locked.
/// </summary>
public partial class PipePuzzleViewModel : ObservableObject, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly ILocalizationService _localizationService;
    private DispatcherTimer? _timer;
    private bool _disposed;
    private bool _isEnding;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    /// <summary>Wird nach Spielende mit Rating (0-3 Sterne) gefeuert.</summary>
    public event EventHandler<int>? GameCompleted;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _orderId = string.Empty;

    [ObservableProperty]
    private OrderDifficulty _difficulty = OrderDifficulty.Medium;

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
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isPuzzleSolved;

    [ObservableProperty]
    private int _maxConnectionDistance;

    [ObservableProperty]
    private bool _isResultShown;

    [ObservableProperty]
    private MiniGameRating _result;

    [ObservableProperty]
    private string _resultText = "";

    [ObservableProperty]
    private string _resultEmoji = "";

    [ObservableProperty]
    private decimal _rewardAmount;

    [ObservableProperty]
    private int _xpAmount;

    [ObservableProperty]
    private bool _canWatchAd;

    [ObservableProperty]
    private bool _adWatched;

    [ObservableProperty]
    private string _taskProgressDisplay = "";

    [ObservableProperty]
    private bool _isLastTask;

    [ObservableProperty]
    private string _continueButtonText = "";

    // Countdown vor Spielstart
    [ObservableProperty]
    private bool _isCountdownActive;

    [ObservableProperty]
    private string _countdownText = "";

    // Sterne-Anzeige
    [ObservableProperty]
    private double _star1Opacity;

    [ObservableProperty]
    private double _star2Opacity;

    [ObservableProperty]
    private double _star3Opacity;

    // Tutorial (beim ersten Spielstart anzeigen)
    [ObservableProperty]
    private bool _showTutorial;

    [ObservableProperty]
    private string _tutorialTitle = "";

    [ObservableProperty]
    private string _tutorialText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    public string DifficultyStars => Difficulty switch
    {
        OrderDifficulty.Easy => "★☆☆",
        OrderDifficulty.Medium => "★★☆",
        OrderDifficulty.Hard => "★★★",
        OrderDifficulty.Expert => "★★★★",
        _ => "★☆☆"
    };

    /// <summary>
    /// Width of the puzzle grid in pixels for WrapPanel constraint.
    /// Each tile is 52px + 4px margin = 56px.
    /// </summary>
    public double PuzzleGridWidth => GridCols * 56;

    partial void OnDifficultyChanged(OrderDifficulty value) => OnPropertyChanged(nameof(DifficultyStars));
    partial void OnGridColsChanged(int value) => OnPropertyChanged(nameof(PuzzleGridWidth));

    // Source/Drain position tracking
    private int _sourceRow, _sourceCol, _drainRow, _drainCol;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public PipePuzzleViewModel(
        IGameStateService gameStateService,
        IAudioService audioService,
        IRewardedAdService rewardedAdService,
        ILocalizationService localizationService)
    {
        _gameStateService = gameStateService;
        _audioService = audioService;
        _rewardedAdService = rewardedAdService;
        _localizationService = localizationService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    public void SetOrderId(string orderId)
    {
        OrderId = orderId;

        var activeOrder = _gameStateService.GetActiveOrder();
        if (activeOrder != null)
        {
            Difficulty = activeOrder.Difficulty;

            int totalTasks = activeOrder.Tasks.Count;
            int currentTaskNum = activeOrder.CurrentTaskIndex + 1;
            TaskProgressDisplay = totalTasks > 1
                ? string.Format(_localizationService.GetString("TaskProgress"), currentTaskNum, totalTasks)
                : "";
            IsLastTask = currentTaskNum >= totalTasks;
            ContinueButtonText = IsLastTask
                ? _localizationService.GetString("Continue")
                : _localizationService.GetString("NextTask");
        }
        else
        {
            TaskProgressDisplay = "";
            IsLastTask = true;
            ContinueButtonText = _localizationService.GetString("Continue");
        }

        InitializePuzzle();

        CheckAndShowTutorial(MiniGameType.PipePuzzle);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAME LOGIC
    // ═══════════════════════════════════════════════════════════════════════

    private void InitializePuzzle()
    {
        // Grid sizes: Cols x Rows (wider than tall)
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
        IsPlaying = false;
        IsPuzzleSolved = false;
        IsResultShown = false;

        GeneratePuzzle();
    }

    private void GeneratePuzzle()
    {
        Tiles.Clear();
        var random = Random.Shared;

        // Randomize source position on left edge
        _sourceRow = random.Next(GridRows);
        _sourceCol = 0;

        // Randomize drain position on right edge (different row preferred)
        _drainCol = GridCols - 1;
        // Ensure drain is at least 1 row away from source for interesting paths
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

        // Generate a solvable path from source to drain
        var path = GeneratePath(random);

        // Fill the grid with pipes
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

                // Check if this cell is on the path
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

                // Mark source and drain (NOT rotatable)
                if (col == _sourceCol && row == _sourceRow)
                {
                    tile.IsSource = true;
                    tile.IsLocked = true;
                    // Source tile should connect from left (external) to path
                    tile.Rotation = tile.SolvedRotation >= 0 ? tile.SolvedRotation : 0;
                }
                else if (col == _drainCol && row == _drainRow)
                {
                    tile.IsDrain = true;
                    tile.IsLocked = true;
                    // Drain tile stays at solved rotation
                    tile.Rotation = tile.SolvedRotation >= 0 ? tile.SolvedRotation : 0;
                }
                else
                {
                    // Randomize initial rotation (player needs to fix it)
                    tile.Rotation = random.Next(4) * 90;
                }

                Tiles.Add(tile);
            }
        }

        // Initial-Verbindungen berechnen (Source + zufällig passende Nachbarn)
        UpdateConnections();
    }

    private List<PathCell> GeneratePath(Random random)
    {
        var path = new List<PathCell>();
        var visited = new HashSet<(int row, int col)>();

        // Use BFS/DFS to find a path from source to drain with interesting turns
        var result = new List<(int row, int col)>();
        if (FindPath(_sourceRow, _sourceCol, _drainRow, _drainCol, visited, result, random))
        {
            // Convert coordinate path to pipe types with correct rotations
            for (int i = 0; i < result.Count; i++)
            {
                var (row, col) = result[i];

                // Determine entry and exit directions
                Direction? entryDir = null;
                Direction? exitDir = null;

                if (i == 0)
                {
                    // Source tile: entry from Left (external)
                    entryDir = Direction.Left;
                }
                else
                {
                    var (prevRow, prevCol) = result[i - 1];
                    entryDir = GetDirectionFrom(prevRow, prevCol, row, col);
                }

                if (i == result.Count - 1)
                {
                    // Drain tile: exit to Right (external)
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

        // Prioritize going Right, but add randomness for Up/Down
        var neighbors = new List<(int r, int c)>
        {
            (row, col + 1),     // Right (primary direction)
            (row - 1, col),     // Up
            (row + 1, col),     // Down
        };

        // Shuffle neighbors with bias towards right
        // Right stays at front with high probability
        if (random.Next(3) > 0)
        {
            // Keep right first but shuffle up/down
            if (random.Next(2) == 0)
                (neighbors[1], neighbors[2]) = (neighbors[2], neighbors[1]);
        }
        else
        {
            // Occasionally try vertical first for more interesting paths
            var verticalFirst = random.Next(2) == 0 ? 1 : 2;
            (neighbors[0], neighbors[verticalFirst]) = (neighbors[verticalFirst], neighbors[0]);
        }

        // Add going left as rare option for really winding paths (medium+ only)
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
        // Direction water comes FROM when entering toRow/toCol from fromRow/fromCol
        if (fromRow < toRow) return Direction.Up;
        if (fromRow > toRow) return Direction.Down;
        if (fromCol < toCol) return Direction.Left;
        return Direction.Right;
    }

    private static Direction GetDirectionTo(int fromRow, int fromCol, int toRow, int toCol)
    {
        // Direction water goes TO from fromRow/fromCol to toRow/toCol
        if (toRow < fromRow) return Direction.Up;
        if (toRow > fromRow) return Direction.Down;
        if (toCol < fromCol) return Direction.Left;
        return Direction.Right;
    }

    /// <summary>
    /// Determines the pipe type and rotation needed to connect entry→exit.
    /// </summary>
    private static (PipeType type, int rotation) GetPipeTypeAndRotation(Direction entry, Direction exit)
    {
        // Straight pipe: connects opposite sides
        if (AreOpposite(entry, exit))
        {
            return (entry == Direction.Left || entry == Direction.Right)
                ? (PipeType.Straight, 0)    // Horizontal
                : (PipeType.Straight, 90);  // Vertical
        }

        // Corner pipe: connects two adjacent sides (L-shape)
        // Base corner at rotation 0: opens Right + Down
        // Rotation  90: opens Down + Left
        // Rotation 180: opens Left + Up
        // Rotation 270: opens Up + Right
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

    [RelayCommand]
    private async Task StartGameAsync()
    {
        if (IsPlaying || IsCountdownActive) return;

        IsResultShown = false;
        _isEnding = false;
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Countdown 3-2-1-Los!
        IsCountdownActive = true;
        foreach (var text in new[] { "3", "2", "1", _localizationService.GetString("CountdownGo") })
        {
            CountdownText = text;
            await Task.Delay(700);
        }
        IsCountdownActive = false;

        // Spiel starten
        IsPlaying = true;
        if (_timer != null) { _timer.Stop(); _timer.Tick -= OnTimerTick; }
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
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
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"Fehler in OnTimerTick: {ex}");
#endif
        }
    }

    [RelayCommand]
    private async Task RotateTileAsync(PipeTile? tile)
    {
        if (tile == null || !IsPlaying || IsResultShown) return;

        // Source and Drain tiles cannot be rotated
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

    private async Task EndGameAsync(bool solved)
    {
        if (_isEnding) return;
        _isEnding = true;

        IsPlaying = false;
        _timer?.Stop();

        if (solved)
        {
            double timeRatio = (double)TimeRemaining / MaxTime;
            int optimalMoves = GridCols * GridRows;
            double moveEfficiency = optimalMoves / (double)Math.Max(MovesCount, 1);

            if (timeRatio > 0.5 && moveEfficiency > 0.4)
                Result = MiniGameRating.Perfect;
            else if (timeRatio > 0.25 && moveEfficiency > 0.25)
                Result = MiniGameRating.Good;
            else
                Result = MiniGameRating.Ok;
        }
        else
        {
            Result = MiniGameRating.Miss;
        }

        _gameStateService.RecordMiniGameResult(Result);

        var sound = Result switch
        {
            MiniGameRating.Perfect => GameSound.Perfect,
            MiniGameRating.Good => GameSound.Good,
            MiniGameRating.Ok => GameSound.ButtonTap,
            _ => GameSound.Miss
        };
        await _audioService.PlaySoundAsync(sound);

        // Belohnungen berechnen
        var order = _gameStateService.GetActiveOrder();
        if (order != null && IsLastTask)
        {
            // Gesamt-Belohnung
            RewardAmount = order.FinalReward;
            XpAmount = order.FinalXp;
        }
        else if (order != null)
        {
            // Teilbelohnung für diese Aufgabe anzeigen
            int taskCount = Math.Max(1, order.Tasks.Count);
            decimal basePerTask = order.BaseReward / taskCount;
            RewardAmount = basePerTask * Result.GetRewardPercentage()
                * order.Difficulty.GetRewardMultiplier() * order.OrderType.GetRewardMultiplier();
            int baseXpPerTask = order.BaseXp / taskCount;
            XpAmount = (int)(baseXpPerTask * Result.GetXpPercentage()
                * order.Difficulty.GetXpMultiplier() * order.OrderType.GetXpMultiplier());
        }
        else
        {
            // QuickJob: Belohnung aus aktivem QuickJob lesen
            var quickJob = _gameStateService.State.ActiveQuickJob;
            RewardAmount = quickJob?.Reward ?? 0;
            XpAmount = quickJob?.XpReward ?? 0;
        }

        ResultText = _localizationService.GetString(Result.GetLocalizationKey());
        ResultEmoji = Result switch
        {
            MiniGameRating.Perfect => "\u2b50\u2b50\u2b50",
            MiniGameRating.Good => "\u2b50\u2b50",
            MiniGameRating.Ok => "\u2b50",
            _ => "\ud83d\udca8"
        };

        IsResultShown = true;

        // Sterne-Bewertung berechnen
        int starCount = Result switch
        {
            MiniGameRating.Perfect => 3,
            MiniGameRating.Good => 2,
            MiniGameRating.Ok => 1,
            _ => 0
        };

        if (IsLastTask)
        {
            // Aggregierte Sterne berechnen (alle Runden zusammen)
            if (order != null && order.TaskResults.Count > 1)
            {
                int totalStarSum = order.TaskResults.Sum(r => r switch
                {
                    MiniGameRating.Perfect => 3,
                    MiniGameRating.Good => 2,
                    MiniGameRating.Ok => 1,
                    _ => 0
                });
                int totalPossible = order.TaskResults.Count * 3;
                starCount = totalPossible > 0
                    ? (int)Math.Round((double)totalStarSum / totalPossible * 3.0)
                    : 0;
                starCount = Math.Clamp(starCount, 0, 3);
            }

            // Sterne staggered einblenden
            Star1Opacity = 0; Star2Opacity = 0; Star3Opacity = 0;
            if (starCount >= 1) { await Task.Delay(200); Star1Opacity = 1.0; }
            if (starCount >= 2) { await Task.Delay(200); Star2Opacity = 1.0; }
            if (starCount >= 3) { await Task.Delay(200); Star3Opacity = 1.0; }

            // Visuelles Event fuer Result-Polish in der View
            GameCompleted?.Invoke(this, starCount);
        }
        else
        {
            // Zwischen-Runde: Sterne sofort setzen, keine Animation
            Star1Opacity = starCount >= 1 ? 1.0 : 0.3;
            Star2Opacity = starCount >= 2 ? 1.0 : 0.3;
            Star3Opacity = starCount >= 3 ? 1.0 : 0.3;
        }

        AdWatched = false;
        CanWatchAd = IsLastTask && _rewardedAdService.IsAvailable;
    }

    [RelayCommand]
    private async Task WatchAdAsync()
    {
        if (!CanWatchAd || AdWatched) return;

        var success = await _rewardedAdService.ShowAdAsync("score_double");
        if (success)
        {
            RewardAmount *= 2;
            XpAmount *= 2;
            AdWatched = true;
            CanWatchAd = false;

            await _audioService.PlaySoundAsync(GameSound.MoneyEarned);
        }
    }

    [RelayCommand]
    private void Continue()
    {
        var order = _gameStateService.GetActiveOrder();
        if (order == null)
        {
            NavigationRequested?.Invoke("../..");
            return;
        }

        if (order.IsCompleted)
        {
            _gameStateService.CompleteActiveOrder();
            NavigationRequested?.Invoke("../..");
        }
        else
        {
            var nextTask = order.CurrentTask;
            if (nextTask != null)
            {
                NavigationRequested?.Invoke($"../{nextTask.GameType.GetRoute()}?orderId={order.Id}");
            }
            else
            {
                NavigationRequested?.Invoke("../..");
            }
        }
    }

    [RelayCommand]
    private void DismissTutorial()
    {
        ShowTutorial = false;
        // Als gesehen markieren und speichern
        var state = _gameStateService.State;
        if (!state.SeenMiniGameTutorials.Contains(MiniGameType.PipePuzzle))
        {
            state.SeenMiniGameTutorials.Add(MiniGameType.PipePuzzle);
            _gameStateService.MarkDirty();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _timer?.Stop();
        IsPlaying = false;

        _gameStateService.CancelActiveOrder();
        NavigationRequested?.Invoke("../..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void CheckAndShowTutorial(MiniGameType gameType)
    {
        var state = _gameStateService.State;
        if (!state.SeenMiniGameTutorials.Contains(gameType))
        {
            TutorialTitle = _localizationService.GetString($"Tutorial{gameType}Title") ?? "";
            TutorialText = _localizationService.GetString($"Tutorial{gameType}Text") ?? "";
            ShowTutorial = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSAL
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;

        _timer?.Stop();
        if (_timer != null)
        {
            _timer.Tick -= OnTimerTick;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════════

public enum PipeType
{
    Straight,   // Connects two opposite sides
    Corner,     // Connects two adjacent sides (L-shape)
    TJunction,  // Connects three sides (T-shape)
    Cross       // Connects all four sides
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

    /// <summary>Whether this tile is locked (cannot be rotated). Source/Drain are locked.</summary>
    public bool IsLocked { get; set; }

    /// <summary>The rotation at which this tile is part of the solution (-1 if not on path).</summary>
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
/// Helper class for path generation.
/// </summary>
internal record PathCell(int Row, int Col, PipeType Type, Direction ExitDirection, int SolvedRotation);
