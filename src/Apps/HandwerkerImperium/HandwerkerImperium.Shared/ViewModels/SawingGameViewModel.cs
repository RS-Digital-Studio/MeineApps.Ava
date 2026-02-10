using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the Sawing mini-game.
/// Player must stop a moving marker in the target zone.
/// </summary>
public partial class SawingGameViewModel : ObservableObject, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly ILocalizationService _localizationService;
    private DispatcherTimer? _timer;
    private bool _disposed;
    private bool _isEnding;

    // Game configuration
    private const double TICK_INTERVAL_MS = 16; // ~60 FPS
    private const double MARKER_SPEED = 0.022;  // Units per tick (0.0 - 1.0 range), increased for harder gameplay

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
    private MiniGameType _gameType = MiniGameType.Sawing;

    [ObservableProperty]
    private string _gameTitle = "";

    [ObservableProperty]
    private string _gameIcon = "\U0001FA9A";

    [ObservableProperty]
    private string _actionButtonText = "";

    [ObservableProperty]
    private string _instructionText = "";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isResultShown;

    [ObservableProperty]
    private double _markerPosition; // 0.0 to 1.0

    [ObservableProperty]
    private double _perfectZoneStart;

    [ObservableProperty]
    private double _perfectZoneWidth;

    [ObservableProperty]
    private double _goodZoneStart;

    [ObservableProperty]
    private double _goodZoneWidth;

    [ObservableProperty]
    private double _okZoneStart;

    [ObservableProperty]
    private double _okZoneWidth;

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
    private double _speedMultiplier = 1.0;

    [ObservableProperty]
    private bool _canWatchAd;

    // ═══════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES FOR VIEW BINDING
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

    // Timing bar zone pixel properties (assumes 300px bar width)
    private const double BAR_WIDTH = 300.0;

    public double OkZonePixelWidth => OkZoneWidth * BAR_WIDTH;
    public Avalonia.Thickness OkZoneMargin => new(OkZoneStart * BAR_WIDTH, 0, 0, 0);

    public double GoodZonePixelWidth => GoodZoneWidth * BAR_WIDTH;
    public Avalonia.Thickness GoodZoneMargin => new(GoodZoneStart * BAR_WIDTH, 0, 0, 0);

    public double PerfectZonePixelWidth => PerfectZoneWidth * BAR_WIDTH;
    public Avalonia.Thickness PerfectZoneMargin => new(PerfectZoneStart * BAR_WIDTH, 0, 0, 0);

    public Avalonia.Thickness MarkerMargin => new(MarkerPosition * BAR_WIDTH - 3, 0, 0, 0);

    [ObservableProperty]
    private bool _adWatched;

    // Direction of marker movement (1 = right, -1 = left)
    private int _direction = 1;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTY CHANGE HANDLERS (notify computed properties)
    // ═══════════════════════════════════════════════════════════════════════

    partial void OnDifficultyChanged(OrderDifficulty value) => OnPropertyChanged(nameof(DifficultyStars));

    partial void OnMarkerPositionChanged(double value) => OnPropertyChanged(nameof(MarkerMargin));

    partial void OnOkZoneStartChanged(double value)
    {
        OnPropertyChanged(nameof(OkZoneMargin));
    }

    partial void OnOkZoneWidthChanged(double value)
    {
        OnPropertyChanged(nameof(OkZonePixelWidth));
    }

    partial void OnGoodZoneStartChanged(double value)
    {
        OnPropertyChanged(nameof(GoodZoneMargin));
    }

    partial void OnGoodZoneWidthChanged(double value)
    {
        OnPropertyChanged(nameof(GoodZonePixelWidth));
    }

    partial void OnPerfectZoneStartChanged(double value)
    {
        OnPropertyChanged(nameof(PerfectZoneMargin));
    }

    partial void OnPerfectZoneWidthChanged(double value)
    {
        OnPropertyChanged(nameof(PerfectZonePixelWidth));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public SawingGameViewModel(
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

        // Get difficulty and game type from active order
        var activeOrder = _gameStateService.GetActiveOrder();
        if (activeOrder != null)
        {
            Difficulty = activeOrder.Difficulty;

            // Get current task's game type
            var currentTask = activeOrder.CurrentTask;
            if (currentTask != null)
            {
                GameType = currentTask.GameType;
                UpdateGameTypeVisuals();
            }
        }

        // Initialize zones based on difficulty
        InitializeZones();
    }

    private void UpdateGameTypeVisuals()
    {
        string L(string key) => _localizationService.GetString(key);

        (GameTitle, GameIcon, ActionButtonText, InstructionText) = GameType switch
        {
            MiniGameType.Sawing => (L("SawingTitle"), "\U0001FA9A", L("SawNow"), L("StopInGreenZone")),
            MiniGameType.Planing => (L("PlaningTitle"), "\U0001FAB5", L("PlaneNow"), L("StopForSmoothSurface")),
            MiniGameType.TileLaying => (L("TileLayingTitle"), "\U0001F9F1", L("LayNow"), L("StopAtPerfectMoment")),
            MiniGameType.Measuring => (L("MeasuringTitle"), "\U0001F4CF", L("MeasureNow"), L("StopAtRightLength")),
            _ => (L("SawingTitle"), "\U0001FA9A", L("SawNow"), L("StopInGreenZone"))
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAME LOGIC
    // ═══════════════════════════════════════════════════════════════════════

    private void InitializeZones()
    {
        // Get zone sizes based on difficulty
        double perfectSize = Difficulty.GetPerfectZoneSize();
        // Tool-Bonus: Saege vergroessert die Zielzone
        var sawTool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.Saw);
        if (sawTool != null) perfectSize += perfectSize * sawTool.ZoneBonus;
        double goodSize = perfectSize * 2;
        double okSize = perfectSize * 3;

        // Randomize the target position (between 0.2 and 0.8)
        var random = new Random();
        double targetCenter = 0.3 + (random.NextDouble() * 0.4);

        // Calculate zone positions (centered on target)
        PerfectZoneWidth = perfectSize;
        PerfectZoneStart = targetCenter - (perfectSize / 2);

        GoodZoneWidth = goodSize;
        GoodZoneStart = targetCenter - (goodSize / 2);

        OkZoneWidth = okSize;
        OkZoneStart = targetCenter - (okSize / 2);

        // Set speed based on difficulty
        SpeedMultiplier = Difficulty.GetSpeedMultiplier();

        // Reset marker
        MarkerPosition = 0;
        _direction = 1;
    }

    [RelayCommand]
    private void StartGame()
    {
        if (IsPlaying) return;

        IsPlaying = true;
        IsResultShown = false;
        _isEnding = false;
        MarkerPosition = 0;
        _direction = 1;

        // Create and start Avalonia DispatcherTimer
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(TICK_INTERVAL_MS)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaying) return;

        // Move marker
        MarkerPosition += MARKER_SPEED * SpeedMultiplier * _direction;

        // Bounce at edges
        if (MarkerPosition >= 1.0)
        {
            MarkerPosition = 1.0;
            _direction = -1;
        }
        else if (MarkerPosition <= 0.0)
        {
            MarkerPosition = 0.0;
            _direction = 1;
        }
    }

    [RelayCommand]
    private async Task StopMarkerAsync()
    {
        if (!IsPlaying || _isEnding) return;
        _isEnding = true;

        IsPlaying = false;
        _timer?.Stop();

        // Calculate result based on marker position
        Result = CalculateRating(MarkerPosition);

        // Record result in game state
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

        // Calculate rewards preview
        var order = _gameStateService.GetActiveOrder();
        if (order != null)
        {
            decimal baseReward = order.BaseReward / order.Tasks.Count;
            RewardAmount = baseReward * Result.GetRewardPercentage();

            int baseXp = order.BaseXp / order.Tasks.Count;
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
            // Double the rewards
            RewardAmount *= 2;
            XpAmount *= 2;
            AdWatched = true;
            CanWatchAd = false;

            await _audioService.PlaySoundAsync(GameSound.MoneyEarned);
        }
    }

    private MiniGameRating CalculateRating(double position)
    {
        // Check if in perfect zone
        if (position >= PerfectZoneStart && position <= PerfectZoneStart + PerfectZoneWidth)
        {
            return MiniGameRating.Perfect;
        }

        // Check if in good zone
        if (position >= GoodZoneStart && position <= GoodZoneStart + GoodZoneWidth)
        {
            return MiniGameRating.Good;
        }

        // Check if in OK zone
        if (position >= OkZoneStart && position <= OkZoneStart + OkZoneWidth)
        {
            return MiniGameRating.Ok;
        }

        // Missed
        return MiniGameRating.Miss;
    }

    [RelayCommand]
    private void Continue()
    {
        // Check if there are more tasks in the order
        var order = _gameStateService.GetActiveOrder();
        if (order == null)
        {
            NavigationRequested?.Invoke("../..");
            return;
        }

        if (order.IsCompleted)
        {
            // Order complete - grant rewards and go back
            _gameStateService.CompleteActiveOrder();
            NavigationRequested?.Invoke("../..");
        }
        else
        {
            // More tasks - go to next mini-game
            var nextTask = order.CurrentTask;
            if (nextTask != null)
            {
                // Replace current page with next mini-game
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
