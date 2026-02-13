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
/// ViewModel for the Painting mini-game.
/// Player must paint all target cells without painting outside the lines.
/// </summary>
public partial class PaintingGameViewModel : ObservableObject, IDisposable
{
    private static readonly Random _random = new();

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

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _orderId = string.Empty;

    [ObservableProperty]
    private OrderDifficulty _difficulty = OrderDifficulty.Medium;

    [ObservableProperty]
    private ObservableCollection<PaintCell> _cells = [];

    [ObservableProperty]
    private int _gridSize = 5;

    [ObservableProperty]
    private int _targetCellCount;

    [ObservableProperty]
    private int _paintedTargetCount;

    [ObservableProperty]
    private int _mistakeCount;

    [ObservableProperty]
    private int _timeRemaining;

    [ObservableProperty]
    private int _maxTime = 30;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isResultShown;

    [ObservableProperty]
    private string _selectedColor = "#4169E1";

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
    private double _paintProgress;

    [ObservableProperty]
    private bool _canWatchAd;

    [ObservableProperty]
    private bool _adWatched;

    // ═══════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets the difficulty as star display string.
    /// </summary>
    public string DifficultyStars => Difficulty switch
    {
        OrderDifficulty.Easy => "★☆☆",
        OrderDifficulty.Medium => "★★☆",
        OrderDifficulty.Hard => "★★★",
        _ => "★☆☆"
    };

    /// <summary>
    /// Width of the paint grid in pixels for WrapPanel constraint.
    /// Each cell is 50px + 4px margin = 54px.
    /// </summary>
    public double PaintGridWidth => GridSize * 54;

    partial void OnDifficultyChanged(OrderDifficulty value) => OnPropertyChanged(nameof(DifficultyStars));
    partial void OnGridSizeChanged(int value) => OnPropertyChanged(nameof(PaintGridWidth));

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public PaintingGameViewModel(
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
    // INITIALIZATION (replaces IQueryAttributable)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initialize the game with an order ID.
    /// </summary>
    public void SetOrderId(string orderId)
    {
        OrderId = orderId;

        var activeOrder = _gameStateService.GetActiveOrder();
        if (activeOrder != null)
        {
            Difficulty = activeOrder.Difficulty;
        }

        InitializeGame();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAME LOGIC
    // ═══════════════════════════════════════════════════════════════════════

    private void InitializeGame()
    {
        // Set grid size and time based on difficulty
        (GridSize, MaxTime) = Difficulty switch
        {
            OrderDifficulty.Easy => (4, 20),
            OrderDifficulty.Medium => (5, 28),
            OrderDifficulty.Hard => (5, 32),
            OrderDifficulty.Expert => (6, 38),
            _ => (5, 28)
        };

        // Tool-Bonus: Pinsel gibt Extra-Sekunden
        var tool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.Paintbrush);
        TimeRemaining = MaxTime + (tool?.TimeBonus ?? 0);
        PaintedTargetCount = 0;
        MistakeCount = 0;
        IsPlaying = false;
        IsResultShown = false;
        PaintProgress = 0;

        // Choose a random paint color
        SelectedColor = GetRandomPaintColor();

        GenerateCanvas();
    }

    private void GenerateCanvas()
    {
        Cells.Clear();

        // Generate a shape pattern for the target area
        var targetPattern = GenerateTargetPattern();

        for (int row = 0; row < GridSize; row++)
        {
            for (int col = 0; col < GridSize; col++)
            {
                bool isTarget = targetPattern[row, col];

                Cells.Add(new PaintCell
                {
                    Row = row,
                    Column = col,
                    Index = row * GridSize + col,
                    IsTarget = isTarget,
                    TargetColor = SelectedColor
                });
            }
        }

        TargetCellCount = Cells.Count(c => c.IsTarget);
    }

    private bool[,] GenerateTargetPattern()
    {
        var pattern = new bool[GridSize, GridSize];

        // Generate different shapes based on difficulty
        int shapeType = _random.Next(3);

        switch (shapeType)
        {
            case 0: // Rectangle
                GenerateRectangle(pattern);
                break;
            case 1: // L-Shape
                GenerateLShape(pattern);
                break;
            case 2: // T-Shape
                GenerateTShape(pattern);
                break;
        }

        return pattern;
    }

    private void GenerateRectangle(bool[,] pattern)
    {
        int startRow = _random.Next(0, GridSize / 2);
        int startCol = _random.Next(0, GridSize / 2);
        int height = _random.Next(2, GridSize - startRow);
        int width = _random.Next(2, GridSize - startCol);

        for (int r = startRow; r < startRow + height; r++)
        {
            for (int c = startCol; c < startCol + width; c++)
            {
                pattern[r, c] = true;
            }
        }
    }

    private void GenerateLShape(bool[,] pattern)
    {
        // Vertical part
        int startCol = _random.Next(1, GridSize - 2);
        for (int r = 0; r < GridSize - 1; r++)
        {
            pattern[r, startCol] = true;
        }

        // Horizontal part at bottom
        for (int c = startCol; c < GridSize; c++)
        {
            pattern[GridSize - 2, c] = true;
        }
    }

    private void GenerateTShape(bool[,] pattern)
    {
        int midRow = GridSize / 2;
        int midCol = GridSize / 2;

        // Vertical part
        for (int r = 0; r < GridSize; r++)
        {
            pattern[r, midCol] = true;
        }

        // Horizontal part at top
        for (int c = 1; c < GridSize - 1; c++)
        {
            pattern[1, c] = true;
        }
    }

    private static string GetRandomPaintColor()
    {
        var colors = new[]
        {
            "#4169E1", // Royal Blue
            "#32CD32", // Lime Green
            "#FF6347", // Tomato
            "#FFD700", // Gold
            "#9370DB", // Medium Purple
            "#20B2AA"  // Light Sea Green
        };

        return colors[_random.Next(colors.Length)];
    }

    [RelayCommand]
    private async Task StartGameAsync()
    {
        if (IsPlaying) return;

        IsPlaying = true;
        IsResultShown = false;
        _isEnding = false;
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Start countdown timer using Avalonia DispatcherTimer
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaying || _isEnding) return;

        TimeRemaining--;

        if (TimeRemaining <= 0)
        {
            await EndGameAsync();
        }
    }

    [RelayCommand]
    private async Task PaintCellAsync(PaintCell? cell)
    {
        if (cell == null || !IsPlaying || IsResultShown || cell.IsPainted) return;

        // Paint the cell
        cell.IsPainted = true;
        cell.PaintColor = SelectedColor;

        if (cell.IsTarget)
        {
            PaintedTargetCount++;
            await _audioService.PlaySoundAsync(GameSound.ButtonTap);
        }
        else
        {
            MistakeCount++;
            cell.HasError = true;
            await _audioService.PlaySoundAsync(GameSound.Miss);
        }

        // Update progress (avoid division by zero)
        PaintProgress = TargetCellCount > 0
            ? (double)PaintedTargetCount / TargetCellCount
            : 0;

        // Check if all target cells are painted
        if (PaintedTargetCount >= TargetCellCount)
        {
            await EndGameAsync();
        }
    }

    private async Task EndGameAsync()
    {
        if (_isEnding) return;
        _isEnding = true;

        IsPlaying = false;
        _timer?.Stop();

        // Calculate rating based on performance (avoid division by zero)
        double completionRatio = TargetCellCount > 0
            ? (double)PaintedTargetCount / TargetCellCount
            : 0;
        int totalAttempts = PaintedTargetCount + MistakeCount;
        double accuracy = totalAttempts > 0
            ? (double)PaintedTargetCount / totalAttempts
            : 0;

        if (completionRatio >= 1.0 && MistakeCount == 0)
        {
            Result = MiniGameRating.Perfect;
        }
        else if (completionRatio >= 0.9 && accuracy >= 0.8)
        {
            Result = MiniGameRating.Good;
        }
        else if (completionRatio >= 0.7 && accuracy >= 0.6)
        {
            Result = MiniGameRating.Ok;
        }
        else
        {
            Result = MiniGameRating.Miss;
        }

        // Record result
        _gameStateService.RecordMiniGameResult(Result);

        // Play sound
        var sound = Result switch
        {
            MiniGameRating.Perfect => GameSound.Perfect,
            MiniGameRating.Good => GameSound.Good,
            MiniGameRating.Ok => GameSound.ButtonTap,
            _ => GameSound.Miss
        };
        await _audioService.PlaySoundAsync(sound);

        // Calculate rewards
        var order = _gameStateService.GetActiveOrder();
        if (order != null)
        {
            int taskCount = Math.Max(1, order.Tasks.Count);
            decimal baseReward = order.BaseReward / taskCount;
            RewardAmount = baseReward * Result.GetRewardPercentage();

            int baseXp = order.BaseXp / taskCount;
            XpAmount = (int)(baseXp * Result.GetXpPercentage());
        }

        // Set result display
        ResultText = _localizationService.GetString(Result.GetLocalizationKey());
        ResultEmoji = Result switch
        {
            MiniGameRating.Perfect => "⭐⭐⭐",
            MiniGameRating.Good => "⭐⭐",
            MiniGameRating.Ok => "⭐",
            _ => "💨"
        };

        IsResultShown = true;
        AdWatched = false;
        CanWatchAd = _rewardedAdService.IsAvailable;
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
    private void Cancel()
    {
        _timer?.Stop();
        IsPlaying = false;

        _gameStateService.CancelActiveOrder();
        NavigationRequested?.Invoke("../..");
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

/// <summary>
/// Represents a single cell in the painting canvas.
/// </summary>
public partial class PaintCell : ObservableObject
{
    public int Row { get; set; }
    public int Column { get; set; }
    public int Index { get; set; }
    public bool IsTarget { get; set; }
    public string TargetColor { get; set; } = "#FFFFFF";

    [ObservableProperty]
    private bool _isPainted;

    [ObservableProperty]
    private string _paintColor = "Transparent";

    [ObservableProperty]
    private bool _hasError;

    // Notify computed display properties when paint state changes
    partial void OnIsPaintedChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayColor));
        OnPropertyChanged(nameof(IsPaintedCorrectly));
    }

    partial void OnPaintColorChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayColor));
    }

    /// <summary>
    /// Gets the background color for display.
    /// Target cells show a faint outline, non-target cells are wall color.
    /// </summary>
    public string DisplayColor => IsPainted
        ? PaintColor
        : IsTarget
            ? "#30FFFFFF"  // Faint target indication
            : "#4A5568";   // Wall color

    /// <summary>
    /// Gets the border color.
    /// </summary>
    public string BorderColor => IsTarget ? "#60FFFFFF" : "#2D3748";

    /// <summary>
    /// Whether this cell is correctly painted (target cell that was painted).
    /// </summary>
    public bool IsPaintedCorrectly => IsTarget && IsPainted;
}
