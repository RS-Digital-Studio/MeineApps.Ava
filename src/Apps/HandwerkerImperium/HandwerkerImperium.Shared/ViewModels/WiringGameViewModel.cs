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
        }

        InitializeGame();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAME LOGIC
    // ═══════════════════════════════════════════════════════════════════════

    private void InitializeGame()
    {
        // Set wire count and time based on difficulty
        (WireCount, MaxTime) = Difficulty switch
        {
            OrderDifficulty.Easy => (3, 12),
            OrderDifficulty.Medium => (4, 15),
            OrderDifficulty.Hard => (5, 18),
            OrderDifficulty.Expert => (6, 22),
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
        var random = new Random();

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
            WireColor.Purple
        ];
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
            await EndGameAsync(false);
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
/// Colors for wires.
/// </summary>
public enum WireColor
{
    Red,
    Blue,
    Green,
    Yellow,
    Orange,
    Purple
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
