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
/// ViewModel for the Wiring mini-game.
/// Player must connect colored wires from left to right.
/// </summary>
public partial class WiringGameViewModel : ObservableObject, IDisposable
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
    private ObservableCollection<Wire> _leftWires = [];

    [ObservableProperty]
    private ObservableCollection<Wire> _rightWires = [];

    [ObservableProperty]
    private int _wireCount = 4;

    [ObservableProperty]
    private int _connectedCount;

    [ObservableProperty]
    private int _timeRemaining;

    [ObservableProperty]
    private int _maxTime = 30;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isResultShown;

    [ObservableProperty]
    private Wire? _selectedLeftWire;

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

    partial void OnDifficultyChanged(OrderDifficulty value) => OnPropertyChanged(nameof(DifficultyStars));

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public WiringGameViewModel(
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

        CheckAndShowTutorial(MiniGameType.WiringGame);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAME LOGIC
    // ═══════════════════════════════════════════════════════════════════════

    private void InitializeGame()
    {
        // Set wire count and time based on difficulty
        (WireCount, MaxTime) = Difficulty switch
        {
            OrderDifficulty.Easy => (3, 15),
            OrderDifficulty.Medium => (4, 16),
            OrderDifficulty.Hard => (5, 17),
            OrderDifficulty.Expert => (7, 18),
            _ => (4, 15)
        };

        // Tool-Bonus: Schraubendreher gibt Extra-Sekunden
        var tool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.Screwdriver);
        TimeRemaining = MaxTime + (tool?.TimeBonus ?? 0);
        ConnectedCount = 0;
        IsPlaying = false;
        IsResultShown = false;
        SelectedLeftWire = null;

        GenerateWires();
    }

    private void GenerateWires()
    {
        LeftWires.Clear();
        RightWires.Clear();

        var colors = GetWireColors();
        var random = Random.Shared;

        // Create wires with colors
        for (int i = 0; i < WireCount; i++)
        {
            var color = colors[i];

            LeftWires.Add(new Wire
            {
                Index = i,
                WireColor = color,
                IsLeft = true
            });

            RightWires.Add(new Wire
            {
                Index = i,
                WireColor = color,
                IsLeft = false
            });
        }

        // Shuffle right wires (so they don't match positions)
        var shuffledRight = RightWires.OrderBy(_ => random.Next()).ToList();
        RightWires.Clear();
        for (int i = 0; i < shuffledRight.Count; i++)
        {
            var wire = shuffledRight[i];
            wire.Index = i;
            RightWires.Add(wire);
        }
    }

    private List<WireColor> GetWireColors()
    {
        return
        [
            WireColor.Red,
            WireColor.Blue,
            WireColor.Green,
            WireColor.Yellow,
            WireColor.Orange,
            WireColor.Purple,
            WireColor.Cyan
        ];
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
            System.Diagnostics.Debug.WriteLine($"Fehler in OnTimerTick: {ex}");
        }
    }

    [RelayCommand]
    private async Task SelectLeftWireAsync(Wire? wire)
    {
        if (wire == null || !IsPlaying || IsResultShown || wire.IsConnected) return;

        // Deselect previous
        if (SelectedLeftWire != null)
        {
            SelectedLeftWire.IsSelected = false;
        }

        // Select new wire
        wire.IsSelected = true;
        SelectedLeftWire = wire;

        await _audioService.PlaySoundAsync(GameSound.ButtonTap);
    }

    [RelayCommand]
    private async Task SelectRightWireAsync(Wire? wire)
    {
        if (wire == null || !IsPlaying || IsResultShown || wire.IsConnected) return;
        if (SelectedLeftWire == null) return;

        // Check if colors match
        if (SelectedLeftWire.WireColor == wire.WireColor)
        {
            // Correct match!
            SelectedLeftWire.IsConnected = true;
            SelectedLeftWire.IsSelected = false;
            wire.IsConnected = true;
            ConnectedCount++;

            await _audioService.PlaySoundAsync(GameSound.Good);

            // Check if all wires are connected
            if (ConnectedCount >= WireCount)
            {
                await EndGameAsync(true);
            }
        }
        else
        {
            // Wrong match - flash error
            wire.HasError = true;
            await _audioService.PlaySoundAsync(GameSound.Miss);

            // Reset error state after a short delay
            await Task.Delay(300);
            wire.HasError = false;
        }

        // Deselect
        SelectedLeftWire.IsSelected = false;
        SelectedLeftWire = null;
    }

    private async Task EndGameAsync(bool completed)
    {
        if (_isEnding) return;
        _isEnding = true;

        IsPlaying = false;
        _timer?.Stop();

        // Calculate rating based on performance
        if (completed)
        {
            double timeRatio = (double)TimeRemaining / MaxTime;

            if (timeRatio > 0.6)
                Result = MiniGameRating.Perfect;
            else if (timeRatio > 0.3)
                Result = MiniGameRating.Good;
            else
                Result = MiniGameRating.Ok;
        }
        else
        {
            // Partial credit based on connections made
            double completionRatio = (double)ConnectedCount / WireCount;

            if (completionRatio >= 0.75)
                Result = MiniGameRating.Ok;
            else
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
        if (!state.SeenMiniGameTutorials.Contains(MiniGameType.WiringGame))
        {
            state.SeenMiniGameTutorials.Add(MiniGameType.WiringGame);
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
/// Colors for wires.
/// </summary>
public enum WireColor
{
    Red,
    Blue,
    Green,
    Yellow,
    Orange,
    Purple,
    Cyan
}

/// <summary>
/// Represents a single wire in the wiring game.
/// </summary>
public partial class Wire : ObservableObject
{
    public int Index { get; set; }
    public WireColor WireColor { get; set; }
    public bool IsLeft { get; set; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _hasError;

    // Notify visual properties when state changes
    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(BorderWidth));
    }

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(ContentOpacity));
    }

    partial void OnHasErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
    }

    /// <summary>
    /// Gets the hex color string for this wire.
    /// </summary>
    public string ColorHex => WireColor switch
    {
        WireColor.Red => "#FF4444",
        WireColor.Blue => "#4444FF",
        WireColor.Green => "#44FF44",
        WireColor.Yellow => "#FFFF44",
        WireColor.Orange => "#FF8844",
        WireColor.Purple => "#AA44FF",
        WireColor.Cyan => "#00BCD4",
        _ => "#888888"
    };

    /// <summary>
    /// Gets the emoji representation.
    /// </summary>
    public string Emoji => WireColor switch
    {
        WireColor.Red => "🔴",
        WireColor.Blue => "🔵",
        WireColor.Green => "🟢",
        WireColor.Yellow => "🟡",
        WireColor.Orange => "🟠",
        WireColor.Purple => "🟣",
        WireColor.Cyan => "🔷",
        _ => "⚪"
    };

    /// <summary>
    /// Background color based on wire state (selected, connected, error).
    /// </summary>
    public string BackgroundColor => HasError
        ? "#40FF4444"    // Red tint for error
        : IsConnected
            ? "#3000FF00" // Green tint for connected
            : IsSelected
                ? "#30FFFFFF" // Light highlight for selected
                : "Transparent";

    /// <summary>
    /// Content opacity (dimmed when connected).
    /// </summary>
    public double ContentOpacity => IsConnected ? 0.5 : 1.0;

    /// <summary>
    /// Border thickness (thicker when selected).
    /// </summary>
    public double BorderWidth => IsSelected ? 4 : 3;
}
