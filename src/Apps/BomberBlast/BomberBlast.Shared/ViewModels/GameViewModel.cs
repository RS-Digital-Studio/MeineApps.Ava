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
public partial class GameViewModel : ObservableObject, IDisposable
{
    private const float MAX_DELTA_TIME = 0.1f;
    /// <summary>Höhe des Banners in Canvas-Einheiten (~dp) für Viewport-Verschiebung</summary>
    private const float BANNER_AD_HEIGHT = 55f;
    /// <summary>Ab diesem Level wird der Banner im GameView angezeigt</summary>
    private const int BANNER_MIN_LEVEL = 5;

    private readonly GameEngine _gameEngine;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IPurchaseService _purchaseService;
    private readonly IAdService _adService;
    private readonly IProgressService _progressService;
    private readonly IReviewService _reviewService;
    private readonly Stopwatch _frameStopwatch = new();
    private bool _isInitialized;
    private bool _disposed;
    private bool _isGameLoopRunning;

    private string _mode = "story";
    private int _level = 1;
    private bool _continueMode;
    private string _boostType = "";
    private int _lastCoinsEarned;
    private bool _lastIsLevelComplete;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Event to request navigation. Parameter is the route string.
    /// </summary>
    public event Action<string>? NavigationRequested;

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

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public GameViewModel(
        GameEngine gameEngine,
        IRewardedAdService rewardedAdService,
        IPurchaseService purchaseService,
        IAdService adService,
        IProgressService progressService,
        IReviewService reviewService)
    {
        _gameEngine = gameEngine;
        _rewardedAdService = rewardedAdService;
        _purchaseService = purchaseService;
        _adService = adService;
        _progressService = progressService;
        _reviewService = reviewService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Set the game mode and level parameters before starting.
    /// Resets initialization flag so the game engine reinitializes on next OnAppearingAsync.
    /// </summary>
    public void SetParameters(string mode, int level, bool continueMode = false, string boostType = "")
    {
        _mode = mode;
        _level = level;
        _continueMode = continueMode;
        _boostType = boostType;
        _isInitialized = false;
    }

    /// <summary>
    /// Called when the view appears. Initializes the game and starts the loop.
    /// </summary>
    public async Task OnAppearingAsync()
    {
        // Erst Unsubscribe (idempotent), dann Subscribe → verhindert doppelte Subscriptions
        _gameEngine.OnGameOver -= HandleGameOver;
        _gameEngine.OnLevelComplete -= HandleLevelComplete;
        _gameEngine.OnCoinsEarned -= HandleCoinsEarned;
        _gameEngine.OnGameOver += HandleGameOver;
        _gameEngine.OnLevelComplete += HandleLevelComplete;
        _gameEngine.OnCoinsEarned += HandleCoinsEarned;

        if (!_isInitialized)
        {
            _isInitialized = true;
            try
            {
                await InitializeGameAsync();
            }
            catch
            {
                NavigationRequested?.Invoke("..");
                return;
            }
        }

        // Banner-Steuerung: Ab Level 5 oben anzeigen, darunter verstecken
        UpdateBannerForLevel(_gameEngine.CurrentLevel);

        IsLoading = false;
        _frameStopwatch.Restart();
        StartGameLoop();
    }

    /// <summary>
    /// Called when the view disappears. Stops the game loop and pauses.
    /// </summary>
    public void OnDisappearing()
    {
        StopGameLoop();
        _gameEngine.Pause();

        // Banner: Position zurücksetzen auf unten (Standard für andere Views)
        _gameEngine.BannerTopOffset = 0;
        if (_adService.AdsEnabled && !_purchaseService.IsPremium)
        {
            _adService.SetBannerPosition(false);
            _adService.ShowBanner();
        }

        // Unsubscribe from game events to prevent memory leaks
        _gameEngine.OnGameOver -= HandleGameOver;
        _gameEngine.OnLevelComplete -= HandleLevelComplete;
        _gameEngine.OnCoinsEarned -= HandleCoinsEarned;
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
            case "arcade":
                await _gameEngine.StartArcadeModeAsync();
                break;

            case "quick":
                var random = new Random();
                int maxLevel = Math.Max(_progressService.HighestCompletedLevel, 10);
                int randomLevel = random.Next(1, maxLevel + 1);
                await _gameEngine.StartStoryModeAsync(randomLevel);
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
    // BANNER-STEUERUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Banner ab Level 5 oben anzeigen, darunter verstecken.
    /// Setzt auch BannerTopOffset im GameEngine/Renderer.
    /// </summary>
    private void UpdateBannerForLevel(int level)
    {
        if (_purchaseService.IsPremium || !_adService.AdsEnabled)
        {
            _gameEngine.BannerTopOffset = 0;
            return;
        }

        if (level >= BANNER_MIN_LEVEL)
        {
            // Banner oben anzeigen, Viewport nach unten verschieben
            _adService.SetBannerPosition(true);
            _adService.ShowBanner();
            _gameEngine.BannerTopOffset = BANNER_AD_HEIGHT;
        }
        else
        {
            // Banner verstecken (Level 1-4)
            _adService.HideBanner();
            _gameEngine.BannerTopOffset = 0;
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

        // Schedule next frame (render-driven loop)
        if (_isGameLoopRunning)
        {
            Dispatcher.UIThread.Post(() => InvalidateCanvasRequested?.Invoke(),
                DispatcherPriority.Render);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TOUCH INPUT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Handle touch/pointer press.
    /// </summary>
    public void OnPointerPressed(float x, float y, float screenWidth, float screenHeight)
    {
        // Handle game over state - return to menu on tap
        if (_gameEngine.State == GameState.GameOver)
        {
            Dispatcher.UIThread.Post(() => NavigationRequested?.Invoke(".."));
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
            _gameEngine.OnTouchStart(x, y, screenWidth, screenHeight);
        }
    }

    /// <summary>
    /// Handle touch/pointer move.
    /// </summary>
    public void OnPointerMoved(float x, float y)
    {
        if (_gameEngine.State == GameState.Playing)
        {
            _gameEngine.OnTouchMove(x, y);
        }
    }

    /// <summary>
    /// Handle touch/pointer release.
    /// </summary>
    public void OnPointerReleased()
    {
        if (_gameEngine.State == GameState.Playing)
        {
            _gameEngine.OnTouchEnd();
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
        NavigationRequested?.Invoke("Settings");
    }

    [RelayCommand]
    private void QuitToMenu()
    {
        StopGameLoop();
        IsPaused = false;
        NavigationRequested?.Invoke("..");
    }

    [RelayCommand]
    private async Task DoubleScoreAsync()
    {
        if (!CanDoubleScore) return;

        bool rewarded = await _rewardedAdService.ShowAdAsync("score_double");
        if (rewarded)
        {
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
        var level = _gameEngine.IsArcadeMode ? _gameEngine.ArcadeWave : _gameEngine.CurrentLevel;
        var isHighScore = _gameEngine.IsCurrentScoreHighScore;
        var mode = _gameEngine.IsArcadeMode ? "arcade" : "story";
        var coins = _lastCoinsEarned;

        // Score-Aufschlüsselung aus GameEngine
        var enemyPts = _gameEngine.LastEnemyKillPoints;
        var timeBonus = _gameEngine.LastTimeBonus;
        var effBonus = _gameEngine.LastEfficiencyBonus;
        var multiplier = _gameEngine.LastScoreMultiplier;

        if (_gameEngine.CurrentLevel >= 50 && !_gameEngine.IsArcadeMode)
        {
            // Sieg! -> Game Over Screen mit Level-Complete-Flag
            NavigationRequested?.Invoke(
                $"GameOver?score={score}&level={level}&highscore={isHighScore}&mode={mode}" +
                $"&coins={coins}&levelcomplete=true&cancontinue=false" +
                $"&enemypts={enemyPts}&timebonus={timeBonus}&effbonus={effBonus}&multiplier={multiplier:F2}");
        }
        else
        {
            // Naechstes Level
            await _gameEngine.NextLevelAsync();
            _frameStopwatch.Restart();

            // Banner-Status aktualisieren (z.B. Wechsel Level 4 → 5)
            UpdateBannerForLevel(_gameEngine.CurrentLevel);
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

    private async void HandleGameOver()
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(2000);

                var score = _gameEngine.Score;
                var level = _gameEngine.IsArcadeMode ? _gameEngine.ArcadeWave : _gameEngine.CurrentLevel;
                var isHighScore = _gameEngine.IsCurrentScoreHighScore;
                var mode = _gameEngine.IsArcadeMode ? "arcade" : "story";
                var coins = _lastCoinsEarned;
                var canContinue = _gameEngine.CanContinue;

                NavigationRequested?.Invoke(
                    $"GameOver?score={score}&level={level}&highscore={isHighScore}&mode={mode}" +
                    $"&coins={coins}&levelcomplete=false&cancontinue={canContinue}");
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HandleGameOver Fehler: {ex.Message}");
        }
    }

    private async void HandleLevelComplete()
    {
        try
        {
            // Review-Service informieren
            _reviewService.OnLevelCompleted(_gameEngine.CurrentLevel);

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(3000);

                // Score-Verdopplung anbieten (nur fuer Free User mit verfuegbarer Ad)
                bool canDouble = !_purchaseService.IsPremium && _rewardedAdService.IsAvailable;

                if (canDouble)
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
        catch (Exception ex)
        {
            Debug.WriteLine($"HandleLevelComplete Fehler: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSAL
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;

        StopGameLoop();

        _gameEngine.OnGameOver -= HandleGameOver;
        _gameEngine.OnLevelComplete -= HandleLevelComplete;
        _gameEngine.OnCoinsEarned -= HandleCoinsEarned;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
