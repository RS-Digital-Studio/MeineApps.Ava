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
    private const double MARKER_SPEED = 0.017;  // Units pro Tick (0.0-1.0), reduziert für bessere Spielbarkeit

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    /// <summary>Wird beim Spielstart nach Countdown gefeuert.</summary>
    public event EventHandler? GameStarted;

    /// <summary>Wird nach Spielende mit Rating (0-3 Sterne) gefeuert.</summary>
    public event EventHandler<int>? GameCompleted;

    /// <summary>Wird bei Zonen-Treffer gefeuert (Zone-Name: "Perfect", "Good", "Ok", "Miss").</summary>
    public event EventHandler<string>? ZoneHit;

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

    /// <summary>Fortschritts-Anzeige z.B. "Aufgabe 2/3" (leer bei QuickJobs/Einzelaufgaben).</summary>
    [ObservableProperty]
    private string _taskProgressDisplay = "";

    /// <summary>Ob dies die letzte Aufgabe des Auftrags ist (bestimmt ob Belohnungen angezeigt werden).</summary>
    [ObservableProperty]
    private bool _isLastTask;

    /// <summary>Text für den Continue-Button ("Nächste Aufgabe" oder "Weiter").</summary>
    [ObservableProperty]
    private string _continueButtonText = "";

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
        OrderDifficulty.Expert => "★★★★",
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

    // Countdown vor Spielstart
    [ObservableProperty]
    private bool _isCountdownActive;

    [ObservableProperty]
    private string _countdownText = "";

    // Sterne-Anzeige (staggered: 0→1 mit Verzoegerung)
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

        // Zustand zurücksetzen (sonst bleibt Ergebnis-Screen stehen)
        IsPlaying = false;
        IsResultShown = false;

        // Get difficulty and game type from active order
        var activeOrder = _gameStateService.GetActiveOrder();
        if (activeOrder != null)
        {
            Difficulty = activeOrder.Difficulty;

            // Fortschritts-Anzeige: "Aufgabe X/Y"
            int totalTasks = activeOrder.Tasks.Count;
            int currentTaskNum = activeOrder.CurrentTaskIndex + 1;
            if (totalTasks > 1)
            {
                var taskLabel = _localizationService.GetString("TaskProgress");
                TaskProgressDisplay = string.Format(taskLabel, currentTaskNum, totalTasks);
            }
            else
            {
                TaskProgressDisplay = "";
            }

            // Letzte Aufgabe? (nach RecordTaskResult wird IsCompleted true)
            IsLastTask = currentTaskNum >= totalTasks;
            ContinueButtonText = IsLastTask
                ? _localizationService.GetString("Continue")
                : _localizationService.GetString("NextTask");

            // Get current task's game type
            var currentTask = activeOrder.CurrentTask;
            if (currentTask != null)
            {
                GameType = currentTask.GameType;
                UpdateGameTypeVisuals();
            }
        }
        else
        {
            // QuickJob: Immer letzte (einzige) Aufgabe
            TaskProgressDisplay = "";
            IsLastTask = true;
            ContinueButtonText = _localizationService.GetString("Continue");
        }

        // Initialize zones based on difficulty
        InitializeZones();

        CheckAndShowTutorial(GameType);
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
        var random = Random.Shared;
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
    private async Task StartGameAsync()
    {
        if (IsPlaying || IsCountdownActive) return;

        IsResultShown = false;
        _isEnding = false;
        MarkerPosition = 0;
        _direction = 1;

        // Countdown 3-2-1-Los!
        IsCountdownActive = true;
        foreach (var text in new[] { "3", "2", "1", _localizationService.GetString("CountdownGo") })
        {
            CountdownText = text;
            await Task.Delay(700);
        }
        IsCountdownActive = false;

        // Spiel starten
        GameStarted?.Invoke(this, EventArgs.Empty);
        IsPlaying = true;
        if (_timer != null) { _timer.Stop(); _timer.Tick -= OnTimerTick; }
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

        // Zonen-Treffer Event feuern
        ZoneHit?.Invoke(this, Result.GetLocalizationKey());

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

        // Belohnungen berechnen
        var order = _gameStateService.GetActiveOrder();
        if (order != null && IsLastTask)
        {
            // Gesamt-Belohnung aus allen bisherigen Ratings + aktuellem Ergebnis
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
    private void DismissTutorial()
    {
        ShowTutorial = false;
        // Als gesehen markieren und speichern
        var state = _gameStateService.State;
        if (!state.SeenMiniGameTutorials.Contains(GameType))
        {
            state.SeenMiniGameTutorials.Add(GameType);
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
