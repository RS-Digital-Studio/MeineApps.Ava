using System.Diagnostics;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Core;
using BomberBlast.Services;
using MeineApps.Core.Premium.Ava.Services;
using SkiaSharp;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel for the game page.
/// Wraps GameEngine, manages the game loop via render-driven updates, and owns SKCanvas rendering.
/// </summary>
public partial class GameViewModel : ObservableObject, INavigable, IDisposable
{
    private const float MAX_DELTA_TIME = 0.05f;

    private readonly GameEngine _gameEngine;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IPurchaseService _purchaseService;
    private readonly IAdService _adService;
    private readonly IProgressService _progressService;
    private readonly IReviewService _reviewService;
    private readonly IAppLogger _logger;
    private readonly Stopwatch _frameStopwatch = new();
    private CancellationTokenSource _gameEventCts = new();
    private bool _isInitialized;
    private bool _disposed;
    private bool _isGameLoopRunning;

    private string _mode = "story";
    private int _level = 1;
    private int _difficulty = 5;
    private bool _continueMode;
    private string _boostType = "";
    private int _dungeonFloor;
    private int _dungeonSeed;
    private int _lastCoinsEarned;
    private bool _lastIsLevelComplete;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Typsicheres Navigations-Event (ersetzt String-basierte Routen).
    /// </summary>
    public event Action<NavigationRequest>? NavigationRequested;

    /// <summary>
    /// Event to request the canvas to invalidate (repaint).
    /// </summary>
    public event Action? InvalidateCanvasRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _isPaused;

    // Score-Verdopplung nach Level-Complete
    [ObservableProperty]
    private bool _showScoreDoubleOverlay;

    [ObservableProperty]
    private int _levelCompleteScore;

    [ObservableProperty]
    private string _levelCompleteScoreText = "";

    [ObservableProperty]
    private bool _canDoubleScore;

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Current game state from the engine.
    /// </summary>
    public GameState State => _gameEngine.State;

    /// <summary>
    /// The game engine instance (for views that need direct access to render).
    /// </summary>
    public GameEngine Engine => _gameEngine;

    /// <summary>
    /// Ob der Game-Loop laeuft (fuer View-seitige Render-Steuerung).
    /// </summary>
    public bool IsGameLoopRunning => _isGameLoopRunning;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public GameViewModel(
        GameEngine gameEngine,
        IRewardedAdService rewardedAdService,
        IPurchaseService purchaseService,
        IAdService adService,
        IProgressService progressService,
        IReviewService reviewService,
        IAppLogger logger)
    {
        _gameEngine = gameEngine;
        _rewardedAdService = rewardedAdService;
        _purchaseService = purchaseService;
        _adService = adService;
        _progressService = progressService;
        _reviewService = reviewService;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Set the game mode and level parameters before starting.
    /// Resets initialization flag so the game engine reinitializes on next OnAppearingAsync.
    /// </summary>
    public void SetParameters(string mode, int level, bool continueMode = false, string boostType = "", int difficulty = 5, int dungeonFloor = 0, int dungeonSeed = 0)
    {
        _mode = mode;
        _level = level;
        _difficulty = difficulty;
        _continueMode = continueMode;
        _boostType = boostType;
        _dungeonFloor = dungeonFloor;
        _dungeonSeed = dungeonSeed;
        _isInitialized = false;
    }

    /// <summary>
    /// Called when the view appears. Initializes the game and starts the loop.
    /// </summary>
    public async Task OnAppearingAsync()
    {
        // Alten CancellationToken abbrechen, neuen erstellen
        _gameEventCts.Cancel();
        _gameEventCts.Dispose();
        _gameEventCts = new CancellationTokenSource();

        // Erst Unsubscribe (idempotent), dann Subscribe → verhindert doppelte Subscriptions
        _gameEngine.OnGameOver -= HandleGameOver;
        _gameEngine.OnLevelComplete -= HandleLevelComplete;
        _gameEngine.OnCoinsEarned -= HandleCoinsEarned;
        _gameEngine.OnPauseRequested -= HandlePauseRequested;
        _gameEngine.OnGameOver += HandleGameOver;
        _gameEngine.OnLevelComplete += HandleLevelComplete;
        _gameEngine.OnCoinsEarned += HandleCoinsEarned;
        _gameEngine.OnPauseRequested += HandlePauseRequested;

        if (!_isInitialized)
        {
            _isInitialized = true;
            try
            {
                await InitializeGameAsync();
            }
            catch
            {
                NavigationRequested?.Invoke(new GoBack());
                return;
            }
        }

        // Kein Banner während Gameplay - Banner verstecken
        _adService.HideBanner();
        _gameEngine.BannerTopOffset = 0;

        IsLoading = false;
        _frameStopwatch.Restart();
        StartGameLoop();
    }

    /// <summary>
    /// Called when the view disappears. Stops the game loop and pauses.
    /// </summary>
    public void OnDisappearing()
    {
        // Laufende Delays (HandleGameOver/HandleLevelComplete) abbrechen
        _gameEventCts.Cancel();

        StopGameLoop();
        _gameEngine.Pause();

        // Banner-Offset zurücksetzen (Banner wird in BomberBlast nicht angezeigt)
        _gameEngine.BannerTopOffset = 0;

        // Unsubscribe from game events to prevent memory leaks
        _gameEngine.OnGameOver -= HandleGameOver;
        _gameEngine.OnLevelComplete -= HandleLevelComplete;
        _gameEngine.OnCoinsEarned -= HandleCoinsEarned;
        _gameEngine.OnPauseRequested -= HandlePauseRequested;
    }

    private async Task InitializeGameAsync()
    {
        _lastCoinsEarned = 0;
        _lastIsLevelComplete = false;

        // Continue-Modus: Spiel fortsetzen statt neu initialisieren
        if (_continueMode)
        {
            _continueMode = false;
            _gameEngine.ContinueAfterGameOver();
            return;
        }

        switch (_mode.ToLower())
        {
            case "survival":
                await _gameEngine.StartSurvivalModeAsync();
                break;

            case "daily":
                await _gameEngine.StartDailyChallengeModeAsync(_level); // _level wird als Seed verwendet
                break;

            case "quick":
                await _gameEngine.StartQuickPlayModeAsync(_level, _difficulty);
                break;

            case "dungeon":
                await _gameEngine.StartDungeonFloorAsync(_dungeonFloor, _dungeonSeed);
                break;

            case "story":
            default:
                await _gameEngine.StartStoryModeAsync(_level);
                break;
        }

        // Power-Up Boost anwenden (aus Rewarded Ad)
        if (!string.IsNullOrEmpty(_boostType))
        {
            _gameEngine.ApplyBoostPowerUp(_boostType);
            _boostType = "";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAME LOOP (render-driven)
    // ═══════════════════════════════════════════════════════════════════════

    private void StartGameLoop()
    {
        _isGameLoopRunning = true;
        // Kick off the first frame
        InvalidateCanvasRequested?.Invoke();
    }

    private void StopGameLoop()
    {
        _isGameLoopRunning = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RENDERING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called by the SKCanvasView to paint the game.
    /// Drives both update and render in a single frame - each paint triggers the next.
    /// </summary>
    public void OnPaintSurface(SKCanvas canvas, int width, int height)
    {
        // Update game state if the loop is running
        if (_isGameLoopRunning)
        {
            // Stopwatch ist praeziser und guenstiger als DateTime.Now
            float deltaTime = (float)_frameStopwatch.Elapsed.TotalSeconds;
            _frameStopwatch.Restart();

            // Clamp delta time to prevent large jumps
            deltaTime = Math.Min(deltaTime, MAX_DELTA_TIME);

            _gameEngine.Update(deltaTime);
        }

        // Always render current state
        _gameEngine.Render(canvas, width, height);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TOUCH INPUT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Handle touch/pointer press.
    /// </summary>
    public void OnPointerPressed(float x, float y, float screenWidth, float screenHeight, long pointerId = 0)
    {
        // GameOver: Nicht auf Taps reagieren → HandleGameOver navigiert automatisch nach Delay
        // Verhindert Race Condition (Tap während 2s-Delay → doppelte Navigation)
        if (_gameEngine.State == GameState.GameOver)
        {
            return;
        }

        // Handle paused state - resume on tap
        if (_gameEngine.State == GameState.Paused)
        {
            _gameEngine.Resume();
            IsPaused = false;
            return;
        }

        // Forward touch to input manager via game engine
        if (_gameEngine.State == GameState.Playing)
        {
            _gameEngine.OnTouchStart(x, y, screenWidth, screenHeight, pointerId);
        }
    }

    /// <summary>
    /// Handle touch/pointer move.
    /// </summary>
    public void OnPointerMoved(float x, float y, long pointerId = 0)
    {
        if (_gameEngine.State == GameState.Playing)
        {
            _gameEngine.OnTouchMove(x, y, pointerId);
        }
    }

    /// <summary>
    /// Handle touch/pointer release.
    /// </summary>
    public void OnPointerReleased(long pointerId = 0)
    {
        if (_gameEngine.State == GameState.Playing)
        {
            _gameEngine.OnTouchEnd(pointerId);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KEYBOARD INPUT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Forward keyboard key-down to the game engine.
    /// </summary>
    public void OnKeyDown(Key key)
    {
        // Escape toggles pause
        if (key == Key.Escape)
        {
            if (_gameEngine.State == GameState.Playing)
                Pause();
            else if (_gameEngine.State == GameState.Paused)
                Resume();
            return;
        }

        // T = Spezial-Bomben-Typ durchschalten
        if (key == Key.T)
        {
            ToggleSpecialBomb();
            return;
        }

        if (_gameEngine.State == GameState.Playing)
        {
            _gameEngine.OnKeyDown(key);
        }
    }

    /// <summary>
    /// Forward keyboard key-up to the game engine.
    /// </summary>
    public void OnKeyUp(Key key)
    {
        _gameEngine.OnKeyUp(key);
    }

    /// <summary>
    /// Gamepad Face-Button gedrückt. Start=Pause, Y=ToggleSpecialBomb, Rest an Engine.
    /// </summary>
    public void OnGamepadButtonDown(BomberBlast.Input.GamepadButton button)
    {
        // Start = Pause (analog zu Escape)
        if (button == BomberBlast.Input.GamepadButton.Start)
        {
            if (IsPaused)
                Resume();
            else
                Pause();
            return;
        }

        // Y = Spezial-Bomben-Typ durchschalten (analog zu T)
        if (button == BomberBlast.Input.GamepadButton.Y)
        {
            ToggleSpecialBomb();
            return;
        }

        if (_gameEngine.State == GameState.Playing)
        {
            _gameEngine.OnGamepadButtonDown(button);
        }
    }

    /// <summary>
    /// Gamepad Face-Button losgelassen.
    /// </summary>
    public void OnGamepadButtonUp(BomberBlast.Input.GamepadButton button)
    {
        _gameEngine.OnGamepadButtonUp(button);
    }

    /// <summary>
    /// Analog-Stick Werte setzen (-1.0 bis 1.0 pro Achse).
    /// </summary>
    public void SetAnalogStick(float x, float y)
    {
        if (_gameEngine.State == GameState.Playing)
        {
            _gameEngine.SetAnalogStick(x, y);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void Pause()
    {
        if (_gameEngine.State == GameState.Playing)
        {
            _gameEngine.Pause();
            IsPaused = true;
        }
    }

    [RelayCommand]
    private void Resume()
    {
        _gameEngine.Resume();
        IsPaused = false;
    }

    [RelayCommand]
    private async Task Restart()
    {
        _isInitialized = false;
        IsPaused = false;
        await InitializeGameAsync();
        _isInitialized = true;
        _frameStopwatch.Restart();
    }

    [RelayCommand]
    private void Settings()
    {
        _gameEngine.Pause();
        IsPaused = false;
        NavigationRequested?.Invoke(new GoSettings());
    }

    /// <summary>
    /// Spezial-Bomben-Typ durchschalten (Normal → Ice → Fire → Sticky → Normal).
    /// Nur verfügbare Typen werden angezeigt.
    /// </summary>
    public void ToggleSpecialBomb()
    {
        if (_gameEngine.State == GameState.Playing)
        {
            _gameEngine.ToggleSpecialBomb();
        }
    }

    [RelayCommand]
    private void QuitToMenu()
    {
        StopGameLoop();
        IsPaused = false;
        NavigationRequested?.Invoke(new GoBack());
    }

    [RelayCommand]
    private async Task DoubleScoreAsync()
    {
        if (!CanDoubleScore) return;

        // Premium: Reward sofort gratis (kein Ad nötig)
        bool rewarded = _purchaseService.IsPremium || await _rewardedAdService.ShowAdAsync("score_double");
        if (rewarded)
        {
            if (!_purchaseService.IsPremium) RewardedAdCooldownTracker.RecordAdShown();
            // Score im Engine verdoppeln
            _gameEngine.DoubleScore();
            LevelCompleteScore = _gameEngine.Score;
            LevelCompleteScoreText = LevelCompleteScore.ToString("N0");
            CanDoubleScore = false;
        }

        // Overlay schliessen und zum naechsten Level
        ShowScoreDoubleOverlay = false;
        await ProceedToNextLevel();
    }

    [RelayCommand]
    private async Task SkipDoubleScore()
    {
        ShowScoreDoubleOverlay = false;
        await ProceedToNextLevel();
    }

    /// <summary>
    /// Weiter zum naechsten Level oder Game-Over-Screen (bei Level 50 / Sieg)
    /// </summary>
    private async Task ProceedToNextLevel()
    {
        var score = _gameEngine.Score;
        var level = _gameEngine.CurrentLevel;
        var isHighScore = _gameEngine.IsCurrentScoreHighScore;
        var mode = _gameEngine.IsSurvivalMode ? "survival" : _gameEngine.IsDailyChallenge ? "daily" : _gameEngine.IsQuickPlayMode ? "quick" : "story";
        var coins = _lastCoinsEarned;

        // Score-Aufschlüsselung aus GameEngine
        var enemyPts = _gameEngine.LastEnemyKillPoints;
        var timeBonus = _gameEngine.LastTimeBonus;
        var effBonus = _gameEngine.LastEfficiencyBonus;
        var multiplier = _gameEngine.LastScoreMultiplier;

        if (_gameEngine.CurrentLevel >= 100 && !_gameEngine.IsDailyChallenge && !_gameEngine.IsQuickPlayMode)
        {
            // Alle 100 Level geschafft → Victory-Screen!
            NavigationRequested?.Invoke(new GoVictory(Score: score, Coins: coins));
        }
        else if (_gameEngine.IsDailyChallenge || _gameEngine.IsQuickPlayMode)
        {
            // Daily Challenge / Quick Play → Game Over Screen mit Level-Complete-Flag (kein NextLevel)
            NavigationRequested?.Invoke(new GoGameOver(
                Score: score, Level: level, IsHighScore: isHighScore, Mode: mode,
                Coins: coins, LevelComplete: true, CanContinue: false,
                EnemyPoints: enemyPts, TimeBonus: timeBonus, EfficiencyBonus: effBonus, Multiplier: multiplier));
        }
        else
        {
            // Nächstes Level
            await _gameEngine.NextLevelAsync();
            _frameStopwatch.Restart();

            // Game-Loop neu starten (war ggf. gestoppt wegen Score-Verdopplungs-Overlay)
            StartGameLoop();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAME EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════════════

    private void HandleCoinsEarned(int coins, int totalScore, bool isLevelComplete)
    {
        _lastCoinsEarned = coins;
        _lastIsLevelComplete = isLevelComplete;
    }

    private void HandlePauseRequested()
    {
        Dispatcher.UIThread.Post(() => Pause());
    }

    private async void HandleGameOver()
    {
        try
        {
            // Token VOR dem Delay erfassen, damit Cancel in OnDisappearing wirkt
            var ct = _gameEventCts.Token;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(2000, ct);

                // Nach Delay prüfen ob Navigation noch gewünscht (nicht disposed/navigiert)
                if (ct.IsCancellationRequested || _disposed) return;

                var score = _gameEngine.Score;
                var level = _gameEngine.CurrentLevel;
                var isHighScore = _gameEngine.IsCurrentScoreHighScore;
                var mode = _gameEngine.IsSurvivalMode ? "survival" : _gameEngine.IsDailyChallenge ? "daily" : _gameEngine.IsQuickPlayMode ? "quick" : "story";
                var coins = _lastCoinsEarned;
                var canContinue = _gameEngine.CanContinue;

                // Survival: Kills und Zeit direkt im Record übergeben
                var survivalKills = _gameEngine.IsSurvivalMode ? _gameEngine.SurvivalKills : 0;
                var survivalTime = _gameEngine.IsSurvivalMode ? _gameEngine.SurvivalTimeElapsed : 0f;

                NavigationRequested?.Invoke(new GoGameOver(
                    Score: score, Level: level, IsHighScore: isHighScore, Mode: mode,
                    Coins: coins, LevelComplete: false, CanContinue: canContinue,
                    Kills: survivalKills, SurvivalTime: survivalTime));
            });
        }
        catch (OperationCanceledException)
        {
            // Erwarteter Abbruch bei Navigation während Delay → ignorieren
        }
        catch (Exception ex)
        {
            _logger.LogError("HandleGameOver Fehler", ex);
        }
    }

    private async void HandleLevelComplete()
    {
        try
        {
            // Review-Service informieren
            _reviewService.OnLevelCompleted(_gameEngine.CurrentLevel);

            // Token VOR dem Delay erfassen
            var ct = _gameEventCts.Token;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(1000, ct);

                // Nach Delay prüfen ob Navigation noch gewünscht
                if (ct.IsCancellationRequested || _disposed) return;

                // Premium: Score automatisch verdoppeln (kein Dialog, kein Ad)
                if (_purchaseService.IsPremium)
                {
                    _gameEngine.DoubleScore();
                    await ProceedToNextLevel();
                }
                // Free User: Score-Verdopplung per Rewarded Ad anbieten
                else if (_rewardedAdService.IsAvailable && RewardedAdCooldownTracker.CanShowAd)
                {
                    // Game-Loop stoppen waehrend Overlay sichtbar
                    StopGameLoop();
                    LevelCompleteScore = _gameEngine.Score;
                    LevelCompleteScoreText = LevelCompleteScore.ToString("N0");
                    CanDoubleScore = true;
                    ShowScoreDoubleOverlay = true;
                    // Overlay-Buttons uebernehmen die weitere Navigation
                }
                else
                {
                    // Kein Overlay -> direkt weiter
                    await ProceedToNextLevel();
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Erwarteter Abbruch bei Navigation während Delay → ignorieren
        }
        catch (Exception ex)
        {
            _logger.LogError("HandleLevelComplete Fehler", ex);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSAL
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;

        _gameEventCts.Cancel();
        _gameEventCts.Dispose();
        StopGameLoop();

        _gameEngine.OnGameOver -= HandleGameOver;
        _gameEngine.OnLevelComplete -= HandleLevelComplete;
        _gameEngine.OnCoinsEarned -= HandleCoinsEarned;
        _gameEngine.OnPauseRequested -= HandlePauseRequested;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
