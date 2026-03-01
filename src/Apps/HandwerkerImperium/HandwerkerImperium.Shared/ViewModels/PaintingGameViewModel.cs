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

    // Combo-System: Aufeinanderfolgende korrekte Treffer
    [ObservableProperty]
    private int _comboCount;

    [ObservableProperty]
    private string _comboDisplay = "";

    [ObservableProperty]
    private bool _isComboActive;

    /// <summary>
    /// Bester Combo im aktuellen Spiel (fuer Bonus-Berechnung).
    /// </summary>
    private int _bestCombo;

    /// <summary>
    /// Combo-Multiplikator: 1.0 + (bestCombo / 5) * 0.25
    /// z.B. Combo 5 → 1.25x, Combo 10 → 1.5x, Combo 20 → 2.0x
    /// </summary>
    public decimal ComboMultiplier => 1.0m + (_bestCombo / 5) * 0.25m;

    /// <summary>
    /// Event fuer Combo-Animation in der View.
    /// </summary>
    public event EventHandler? ComboIncreased;

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
        OrderDifficulty.Expert => "★★★★",
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

        InitializeGame();

        CheckAndShowTutorial(MiniGameType.PaintingGame);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAME LOGIC
    // ═══════════════════════════════════════════════════════════════════════

    private void InitializeGame()
    {
        // Set grid size and time based on difficulty
        (GridSize, MaxTime) = Difficulty switch
        {
            OrderDifficulty.Easy => (4, 24),
            OrderDifficulty.Medium => (5, 28),
            OrderDifficulty.Hard => (5, 22),
            OrderDifficulty.Expert => (6, 30),
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
        ComboCount = 0;
        _bestCombo = 0;
        IsComboActive = false;
        ComboDisplay = "";

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
        int shapeType = Random.Shared.Next(3);

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
        int startRow = Random.Shared.Next(0, GridSize / 2);
        int startCol = Random.Shared.Next(0, GridSize / 2);
        int height = Random.Shared.Next(2, GridSize - startRow);
        int width = Random.Shared.Next(2, GridSize - startCol);

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
        int startCol = Random.Shared.Next(1, GridSize - 2);
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

        return colors[Random.Shared.Next(colors.Length)];
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
                await EndGameAsync();
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
    private async Task PaintCellAsync(PaintCell? cell)
    {
        if (cell == null || !IsPlaying || IsResultShown || cell.IsPainted) return;

        // Paint the cell
        cell.IsPainted = true;
        cell.PaintedAt = DateTime.UtcNow;
        cell.PaintColor = SelectedColor;

        if (cell.IsTarget)
        {
            PaintedTargetCount++;

            // Combo erhoehen
            ComboCount++;
            if (ComboCount > _bestCombo) _bestCombo = ComboCount;

            if (ComboCount >= 3)
            {
                IsComboActive = true;
                ComboDisplay = string.Format(
                    _localizationService.GetString("ComboX"), ComboCount);
                ComboIncreased?.Invoke(this, EventArgs.Empty);
                await _audioService.PlaySoundAsync(GameSound.ComboHit);
            }
            else
            {
                await _audioService.PlaySoundAsync(GameSound.ButtonTap);
            }
        }
        else
        {
            MistakeCount++;
            cell.HasError = true;

            // Combo zuruecksetzen
            ComboCount = 0;
            IsComboActive = false;
            ComboDisplay = "";

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

        // Belohnungen berechnen (Combo-Multiplikator anwenden)
        var comboMult = ComboMultiplier;
        var order = _gameStateService.GetActiveOrder();
        if (order != null && IsLastTask)
        {
            // Gesamt-Belohnung mit Combo-Multiplikator
            RewardAmount = order.FinalReward * comboMult;
            XpAmount = (int)(order.FinalXp * comboMult);
        }
        else if (order != null)
        {
            // Teilbelohnung für diese Aufgabe anzeigen
            int taskCount = Math.Max(1, order.Tasks.Count);
            decimal basePerTask = order.BaseReward / taskCount;
            RewardAmount = basePerTask * Result.GetRewardPercentage()
                * order.Difficulty.GetRewardMultiplier() * order.OrderType.GetRewardMultiplier() * comboMult;
            int baseXpPerTask = order.BaseXp / taskCount;
            XpAmount = (int)(baseXpPerTask * Result.GetXpPercentage()
                * order.Difficulty.GetXpMultiplier() * order.OrderType.GetXpMultiplier() * comboMult);
        }
        else
        {
            // QuickJob: Belohnung aus aktivem QuickJob lesen mit Combo-Multiplikator
            var quickJob = _gameStateService.State.ActiveQuickJob;
            RewardAmount = (quickJob?.Reward ?? 0) * comboMult;
            XpAmount = (int)((quickJob?.XpReward ?? 0) * comboMult);
        }

        // Set result display
        ResultText = _localizationService.GetString(Result.GetLocalizationKey());
        ResultEmoji = Result switch
        {
            MiniGameRating.Perfect => "★★★",
            MiniGameRating.Good => "★★",
            MiniGameRating.Ok => "★",
            _ => "💨"
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
        if (!state.SeenMiniGameTutorials.Contains(MiniGameType.PaintingGame))
        {
            state.SeenMiniGameTutorials.Add(MiniGameType.PaintingGame);
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

    /// <summary>Zeitpunkt des Streichens (fuer Frisch-Effekt im Renderer).</summary>
    public DateTime PaintedAt { get; set; }

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
