using System.Diagnostics;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using BomberBlast.Core;
using BomberBlast.Services;
using MeineApps.Core.Premium.Ava.Services;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel fuer die Spielseite.
/// Kapselt GameEngine, steuert den Game-Loop via render-getriebene Updates und besitzt SKCanvas-Rendering.
/// </summary>
public sealed partial class GameViewModel : ViewModelBase, INavigable, IDisposable
{
    // Audit H01: Spiral-of-Death-Schutz mit 10-FPS-Floor (statt frueher 0.05s = 20-FPS-Floor).
    // 50ms-Cap war zu strikt fuer 30-FPS-Target: 80ms-GC-Spike kappte deltaTime auf 50ms,
    // Sim verlor 30ms → Bombe mit Restfuse=0.06s ueberstand Stutter, Spieler starb unfair.
    // FixedTimestepRunner waere die deterministische Alternative — bleibt fuer Replay/Anti-Cheat reserviert.
    private const float MAX_DELTA_TIME = 0.1f;

    private readonly GameEngine _gameEngine;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IPurchaseService _purchaseService;
    private readonly IAdService _adService;
    private readonly IProgressService _progressService;
    private readonly IReviewService _reviewService;
    private readonly IGameAssetService _assetService;
    private readonly ILogger<GameViewModel> _logger;
    // Persistenz wird waehrend des laufenden Spiels ausgesetzt (Anti-Ruckeln): Coin-/Achievement-/
    // Collection-Saves stauen sich im Speicher und werden erst nach dem Spiel auf Disk geschrieben.
    private readonly MeineApps.Core.Ava.Services.IPreferencesService _preferences;
    // Vermeidet doppelte Preload-Triggerings pro Welt (fire-and-forget Task schmeisst sonst Warnings)
    private int _lastPreloadedWorldIndex = -1;
    private readonly Stopwatch _frameStopwatch = new();
    // TEMP-DIAGNOSE: Live-Frame-Profiler (Inter-Frame-Zeit, Update/Render-Split, GC, Allokations-Rate).
    private readonly Core.Diagnostics.FrameProfiler _frameProfiler = new();
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
    private bool _masterMode;
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
    /// Event um die Canvas zur Neuzeichnung aufzufordern.
    /// </summary>
    public event Action? InvalidateCanvasRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyOverlayOpen))]
    private bool _isLoading = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyOverlayOpen))]
    private bool _isPaused;

    // Score-Verdopplung nach Level-Complete
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyOverlayOpen))]
    private bool _showScoreDoubleOverlay;

    /// <summary>
    /// Audit H04: Aggregat-Flag fuer alle Overlays die Input blockieren sollen.
    /// View bindet GameCanvas.IsHitTestVisible="{Binding !IsAnyOverlayOpen}" — verhindert
    /// dass User waehrend Pause/ScoreDouble/ContextHelp Bomben legen oder den Joystick bewegen.
    /// </summary>
    public bool IsAnyOverlayOpen => IsPaused || ShowScoreDoubleOverlay || IsContextHelpVisible || IsLoading;

    [ObservableProperty]
    private int _levelCompleteScore;

    [ObservableProperty]
    private string _levelCompleteScoreText = "";

    [ObservableProperty]
    private bool _canDoubleScore;

    /// <summary>
    /// Kontext-Hilfe-Overlay sichtbar (Tutorial-Replay-Pin, v2.0.37).
    /// Modus-spezifische Tipps als Overlay; pausiert das Spiel waehrend offen.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyOverlayOpen))]
    private bool _isContextHelpVisible;

    /// <summary>Erste modus-spezifische Tipp-Zeile fuer das ContextHelp-Overlay.</summary>
    [ObservableProperty]
    private string _contextHelpTip1 = "";

    /// <summary>Zweite modus-spezifische Tipp-Zeile fuer das ContextHelp-Overlay.</summary>
    [ObservableProperty]
    private string _contextHelpTip2 = "";

    /// <summary>Dritte Tipp-Zeile (kann leer sein wenn Modus nur 2 Tipps hat, z.B. Daily/Master).</summary>
    [ObservableProperty]
    private string _contextHelpTip3 = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktueller Spielzustand aus der Engine.
    /// </summary>
    public GameState State => _gameEngine.State;

    /// <summary>
    /// Die GameEngine-Instanz (fuer Views die direkten Zugriff zum Rendern brauchen).
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
        IGameAssetService assetService,
        ILogger<GameViewModel> logger,
        IRetentionService retentionService,
        IWorldStoryService worldStoryService,
        ITutorialService tutorialService,
        MeineApps.Core.Ava.Services.IPreferencesService preferences,
        MeineApps.Core.Ava.Localization.ILocalizationService? localizationService = null)
    {
        _gameEngine = gameEngine;
        _rewardedAdService = rewardedAdService;
        _purchaseService = purchaseService;
        _adService = adService;
        _progressService = progressService;
        _reviewService = reviewService;
        _assetService = assetService;
        _logger = logger;
        _tutorialService = tutorialService;
        _preferences = preferences;
        _localizationService = localizationService;

        // Phase 24b — RetentionService property-injecten damit GameEngine.PlayFirstWinCinematic
        // beim ECHTEN ersten Sieg getriggert werden kann.
        _gameEngine.RetentionService = retentionService;
        //.2 : WorldStoryService — fuer Welt-Intro/Outro-Cutscenes.
        _gameEngine.WorldStoryService = worldStoryService;
    }

    // Welle 3 v2.0.58 : Tutorial-Routing — Story-Mode startet automatisch
    // mit der naechsten offenen Tutorial-Phase wenn der Spieler noch nicht alle 3 abgeschlossen hat.
    private readonly ITutorialService _tutorialService;

    // v2.0.60 (B-C2): Lokalisierung für Tutorial-Skip-Confirm und andere Dialog-Strings.
    // Nullable für Backward-Compat (manche Test-Setups injizieren ohne).
    private readonly MeineApps.Core.Ava.Localization.ILocalizationService? _localizationService;

    /// <summary>
    /// v2.0.60 (B-C2): Confirm-Request für Tutorial-Skip. MainView abonniert.
    /// Params: title, message, acceptText (= behalten), cancelText (= überspringen).
    /// Return true = Behalten (Default), false = Überspringen.
    /// </summary>
    public event Func<string, string, string, string, Task<bool>>? ConfirmationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Spielmodus und Level-Parameter setzen vor dem Start.
    /// Setzt das Initialisierungs-Flag zurueck, damit die Engine beim naechsten OnAppearingAsync neu initialisiert.
    /// </summary>
    public void SetParameters(string mode, int level, bool continueMode = false, string boostType = "", int difficulty = 5, int dungeonFloor = 0, int dungeonSeed = 0, bool masterMode = false)
    {
        _mode = mode;
        _level = level;
        _difficulty = difficulty;
        _continueMode = continueMode;
        _boostType = boostType;
        _dungeonFloor = dungeonFloor;
        _dungeonSeed = dungeonSeed;
        _masterMode = masterMode;
        _isInitialized = false;

        // Welt-Hintergrund fire-and-forget preloaden (Countdown läuft ~3s,
        // WebP-Decode < 100ms auf Mid-Tier). Welt 1 ist bereits im Splash preloaded —
        // nur Welt 2-10 nachladen um erste-Frame-Fallback zu vermeiden.
        // Audit M28: Vorherige Preloads cancelln bei rapidem Welt-Wechsel.
        if (mode == "story" && level > 0)
        {
            int worldIndex = _progressService.GetWorldForLevel(level) - 1; // 1-basiert → 0-basiert
            if (worldIndex > 0 && worldIndex != _lastPreloadedWorldIndex)
            {
                _lastPreloadedWorldIndex = worldIndex;
                _preloadCts?.Cancel();
                _preloadCts?.Dispose();  // altes CTS freigeben vor Neuzuweisung
                _preloadCts = new CancellationTokenSource();
                var token = _preloadCts.Token;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (token.IsCancellationRequested) return;
                        await _assetService.PreloadAsync(GameAssetPaths.GetWorldPreloadAssets(worldIndex));
                    }
                    catch (OperationCanceledException) { /* User wechselte Welt vor Preload-Ende */ }
                    catch (Exception ex) { _logger?.LogWarning($"Welt-Preload fehlgeschlagen (World {worldIndex}): {ex.Message}"); }
                }, token);
            }
        }
    }

    // Audit M28: CTS fuer Welt-Preload — wird bei jedem neuen SetParameters cancellt.
    private CancellationTokenSource? _preloadCts;

    /// <summary>
    /// Wird aufgerufen wenn die View erscheint. Initialisiert das Spiel und startet den Loop.
    /// </summary>
    public async Task OnAppearingAsync()
    {
        // Alten CancellationToken abbrechen, neuen erstellen
        _gameEventCts.Cancel();
        _gameEventCts.Dispose();
        _gameEventCts = new CancellationTokenSource();

        // Erst Unsubscribe (idempotent), dann Subscribe → verhindert doppelte Subscriptions
        _gameEngine.GameOver -= HandleGameOver;
        _gameEngine.LevelComplete -= HandleLevelComplete;
        _gameEngine.CoinsEarned -= HandleCoinsEarned;
        _gameEngine.TutorialSkipRequested -= HandleTutorialSkipRequested;
        _gameEngine.GameOver += HandleGameOver;
        _gameEngine.LevelComplete += HandleLevelComplete;
        _gameEngine.CoinsEarned += HandleCoinsEarned;
        _gameEngine.TutorialSkipRequested += HandleTutorialSkipRequested;

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
    /// Wird aufgerufen wenn die View verschwindet. Stoppt den Game-Loop und pausiert.
    /// </summary>
    public void OnDisappearing()
    {
        // Laufende Delays (HandleGameOver/HandleLevelComplete) abbrechen
        _gameEventCts.Cancel();

        StopGameLoop();
        _gameEngine.Pause();

        // Banner-Offset zurücksetzen (Banner wird in BomberBlast nicht angezeigt)
        _gameEngine.BannerTopOffset = 0;

        // Event-Abmeldung gegen Memory Leaks
        _gameEngine.GameOver -= HandleGameOver;
        _gameEngine.LevelComplete -= HandleLevelComplete;
        _gameEngine.CoinsEarned -= HandleCoinsEarned;
        _gameEngine.TutorialSkipRequested -= HandleTutorialSkipRequested;
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

            case "bossrush":
                // v2.0.42 Plan Task 3.3: Boss-Rush Single-Boss-Run-Variante.
                // bossIndex wird als _dungeonFloor uebergeben (re-use Floor-Parameter aus GoGame).
                await _gameEngine.StartBossRushModeAsync(_dungeonFloor);
                break;

            case "dailyrace":
                // v2.0.42 Plan Task 3.1: Daily Bomb Race - alle Spieler weltweit bekommen identisches Level via Date-Seed.
                // Score wird in GameEngine GameOver-Hook automatisch an LeagueService.SubmitDailyRaceScoreAsync geschickt.
                await _gameEngine.StartDailyRaceModeAsync();
                break;

            case "tutorial":
                // Welle 3 v2.0.58 : Direkt-Aufruf einer Tutorial-Phase (1/2/3 als _level).
                await _gameEngine.StartTutorialPhaseAsync(Math.Clamp(_level, 1, 3));
                break;

            case "story":
            default:
                // Welle 3 v2.0.58 : Wenn Tutorial nicht komplett UND Spieler startet
                // Story-L1, route auf naechste offene Tutorial-Phase. Schuetzt Genre-Neulinge davor,
                // dass sie das Tutorial im Story-L1 erleiden — sie bekommen eigene Mini-Levels.
                if (_level <= 1 && !_tutorialService.IsCompleted)
                {
                    int phase = _tutorialService.IsPhaseCompleted(Models.TutorialPhase.Movement)
                        ? (_tutorialService.IsPhaseCompleted(Models.TutorialPhase.Bombs) ? 3 : 2)
                        : 1;
                    await _gameEngine.StartTutorialPhaseAsync(phase);
                }
                else
                {
                    await _gameEngine.StartStoryModeAsync(_level, _masterMode);
                }
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
        // Disk-Persistenz aussetzen: waehrend des Spiels gewonnene Coins/Achievements/Collection
        // werden nur im Speicher gehalten — kein JSON-Serialize + File-Write im Render-Loop (Anti-Ruckeln).
        _preferences.SuspendPersistence();
        // Erstes Frame anstoßen
        InvalidateCanvasRequested?.Invoke();
    }

    private void StopGameLoop()
    {
        _isGameLoopRunning = false;
        // Spiel beendet/pausiert/Overlay → aufgestauten Fortschritt jetzt einmal auf Disk schreiben.
        _preferences.ResumePersistence();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RENDERING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wird von der SKCanvasView zum Zeichnen des Spiels aufgerufen.
    /// Treibt sowohl Update als auch Render in einem Frame - jedes Paint loest das naechste aus.
    /// </summary>
    public void OnPaintSurface(SKCanvas canvas, int width, int height)
    {
        // TEMP-DIAGNOSE: Inter-Frame-Zeit + Update/Render-Split fuer den FrameProfiler messen.
        double rawFrameMs = 0, updMs = 0, rendMs = 0;
        bool profile = _isGameLoopRunning;

        // Spielzustand aktualisieren wenn der Loop laeuft
        if (_isGameLoopRunning)
        {
            // Stopwatch ist praeziser und guenstiger als DateTime.Now
            rawFrameMs = _frameStopwatch.Elapsed.TotalMilliseconds; // echte (ungecappte) Inter-Frame-Zeit
            float deltaTime = (float)_frameStopwatch.Elapsed.TotalSeconds;
            _frameStopwatch.Restart();

            // Delta-Zeit begrenzen um große Spruenge zu verhindern
            deltaTime = Math.Min(deltaTime, MAX_DELTA_TIME);

            long updStart = Stopwatch.GetTimestamp();
            _gameEngine.Update(deltaTime);
            updMs = (Stopwatch.GetTimestamp() - updStart) * 1000.0 / Stopwatch.Frequency;
        }

        // Immer aktuellen Zustand rendern
        long rendStart = Stopwatch.GetTimestamp();
        _gameEngine.Render(canvas, width, height);
        rendMs = (Stopwatch.GetTimestamp() - rendStart) * 1000.0 / Stopwatch.Frequency;

        if (profile)
            _frameProfiler.Record(rawFrameMs, updMs, rendMs, _gameEngine.State);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TOUCH INPUT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Touch-/Pointer-Druck verarbeiten.
    /// </summary>
    public void OnPointerPressed(float x, float y, float screenWidth, float screenHeight, long pointerId = 0)
    {
        // GameOver: Nicht auf Taps reagieren → HandleGameOver navigiert automatisch nach Delay
        // Verhindert Race Condition (Tap während 2s-Delay → doppelte Navigation)
        if (_gameEngine.State == GameState.GameOver)
        {
            return;
        }

        // Pausierter Zustand - bei Tap fortsetzen
        if (_gameEngine.State == GameState.Paused)
        {
            _gameEngine.Resume();
            IsPaused = false;
            return;
        }

        // Touch an InputManager via GameEngine weiterleiten
        if (_gameEngine.State == GameState.Playing)
        {
            _gameEngine.OnTouchStart(x, y, screenWidth, screenHeight, pointerId);
        }
    }

    /// <summary>
    /// Touch-/Pointer-Bewegung verarbeiten.
    /// </summary>
    public void OnPointerMoved(float x, float y, long pointerId = 0)
    {
        if (_gameEngine.State == GameState.Playing)
        {
            _gameEngine.OnTouchMove(x, y, pointerId);
        }
    }

    /// <summary>
    /// Touch-/Pointer-Loslassen verarbeiten.
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
    /// Tastatur-KeyDown an die GameEngine weiterleiten.
    /// </summary>
    public void OnKeyDown(Key key)
    {
        // Escape: zuerst ContextHelp zurück ins Pause-Menü, sonst Pause umschalten.
        if (key == Key.Escape)
        {
            if (IsContextHelpVisible)
                CloseContextHelp();
            else if (_gameEngine.State == GameState.Playing)
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
    /// Tastatur-KeyUp an die GameEngine weiterleiten.
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
        // Start = Pause (analog zu Escape); im ContextHelp zuerst zurück ins Pause-Menü.
        if (button == BomberBlast.Input.GamepadButton.Start)
        {
            if (IsContextHelpVisible)
                CloseContextHelp();
            else if (IsPaused)
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

    /// <summary>
    /// Tutorial-Replay-Pin (v2.0.37). Pausiert das Spiel, befuellt 3 modus-spezifische Tipps,
    /// zeigt das ContextHelp-Overlay. Verfuegbar in Story/Survival/Dungeon/Daily/Master.
    /// </summary>
    [RelayCommand]
    private void ShowContextHelp()
    {
        if (IsContextHelpVisible) return;

        // Tipps abhaengig vom aktuellen Modus zusammenstellen
        var (tip1, tip2, tip3) = _mode.ToLowerInvariant() switch
        {
            "story" when _masterMode => (
                Resources.Strings.AppStrings.ContextHelpMaster1,
                Resources.Strings.AppStrings.ContextHelpMaster2,
                ""),
            "story" => (
                Resources.Strings.AppStrings.ContextHelpStory1,
                Resources.Strings.AppStrings.ContextHelpStory2,
                Resources.Strings.AppStrings.ContextHelpStory3),
            "survival" => (
                Resources.Strings.AppStrings.ContextHelpSurvival1,
                Resources.Strings.AppStrings.ContextHelpSurvival2,
                Resources.Strings.AppStrings.ContextHelpSurvival3),
            "dungeon" => (
                Resources.Strings.AppStrings.ContextHelpDungeon1,
                Resources.Strings.AppStrings.ContextHelpDungeon2,
                Resources.Strings.AppStrings.ContextHelpDungeon3),
            "daily" => (
                Resources.Strings.AppStrings.ContextHelpDaily1,
                Resources.Strings.AppStrings.ContextHelpDaily2,
                ""),
            _ => (
                Resources.Strings.AppStrings.ContextHelpStory1,
                Resources.Strings.AppStrings.ContextHelpStory2,
                Resources.Strings.AppStrings.ContextHelpStory3),
        };

        ContextHelpTip1 = tip1;
        ContextHelpTip2 = tip2;
        ContextHelpTip3 = tip3;

        // Wird aus dem Pause-Overlay geöffnet: Engine bleibt pausiert, Pause-Overlay ausblenden
        // und stattdessen das ContextHelp-Overlay zeigen (kein Overlay-Stapel / Z-Konflikt).
        if (_gameEngine.State == GameState.Playing)
            _gameEngine.Pause();

        IsPaused = false;
        IsContextHelpVisible = true;
    }

    /// <summary>
    /// Schliesst das ContextHelp-Overlay und kehrt ins Pause-Overlay zurück.
    /// </summary>
    [RelayCommand]
    private void CloseContextHelp()
    {
        if (!IsContextHelpVisible) return;
        IsContextHelpVisible = false;
        // Zurück zum Pause-Overlay (ContextHelp wird von dort geöffnet) — Engine bleibt pausiert,
        // der Spieler nimmt das Spiel über den Resume-Button wieder auf.
        IsPaused = true;
    }

    // Gate gegen Double-Ad-Race: Tap zweimal auf Score-Verdoppeln-Button
    // wuerde sonst zwei Ads laden + Score x4 statt x2 (Cheat-Window).
    // Pattern aus GameOverViewModel._adInFlight.
    private bool _doubleScoreAdInFlight;

    [RelayCommand]
    private async Task DoubleScoreAsync()
    {
        if (!CanDoubleScore) return;
        if (_doubleScoreAdInFlight) return; // Zweiter Klick waehrend erster Ad laeuft

        // Sofort deaktivieren um Doppelklick waehrend async-Gap zu verhindern
        CanDoubleScore = false;
        _doubleScoreAdInFlight = true;
        try
        {
            // Premium: Reward sofort gratis (kein Ad nötig)
            bool rewarded = _purchaseService.IsPremium || await _rewardedAdService.ShowAdAsync("score_double");
            if (rewarded)
            {
                if (!_purchaseService.IsPremium) RewardedAdCooldownTracker.RecordAdShown();
                // Score im Engine verdoppeln
                _gameEngine.DoubleScore();
                LevelCompleteScore = _gameEngine.Score;
                LevelCompleteScoreText = LevelCompleteScore.ToString("N0");
            }
            // Bei rewarded=false bleibt CanDoubleScore=false (User hat Versuch verbraucht via Skip-Pfad)

            // Overlay schliessen und zum naechsten Level
            ShowScoreDoubleOverlay = false;
        }
        finally
        {
            _doubleScoreAdInFlight = false;
        }
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

        // Welle 3 v2.0.58 : Tutorial-Level (Number < 0) → naechste Phase oder Story-L1.
        // Kein Victory/GameOver-Screen, kein Score-Submit — direkt weiter im Onboarding-Flow.
        if (level < 0)
        {
            int completedPhase = -level;  // -1 → , -2 → , -3 → 
            if (completedPhase < 3 && !_tutorialService.IsCompleted)
            {
                // Naechste Tutorial-Phase
                await _gameEngine.StartTutorialPhaseAsync(completedPhase + 1);
                StartGameLoop();
                return;
            }
            // Letzte Phase abgeschlossen ODER Skip → starte Story-L1 mit Soft-Onboarding.
            await _gameEngine.StartStoryModeAsync(1, masterMode: false);
            StartGameLoop();
            return;
        }

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

    /// <summary>
    /// v2.0.60 (B-C2): Tutorial-Skip Confirm-Dialog. User-Klick auf Skip-Button löst
    /// Confirm aus, der Default ist "Tutorial behalten" damit Ungeduld-Klick nicht direkt
    /// in Normalschwierigkeit landet.
    /// </summary>
    private async void HandleTutorialSkipRequested()
    {
        if (ConfirmationRequested == null)
        {
            // Kein Dialog-Hook → direkt skippen (Backward-Compat falls VM noch nicht verdrahtet).
            _gameEngine.ConfirmTutorialSkip();
            return;
        }

        // try/catch ist Pflicht: async void + ein await, der bei App-Backgrounding (DialogPresenter
        // bricht offene Confirms via TrySetCanceled ab) eine OperationCanceledException wirft —
        // ungefangen waere das ein unhandled Exception/Crash.
        try
        {
            string title = _localizationService?.GetString("SkipTutorialTitle") ?? "Tutorial überspringen?";
            string message = _localizationService?.GetString("SkipTutorialMessage")
                ?? "Das Tutorial erklärt das Bomb-Timing und die Bewegung. Möchtest du es wirklich überspringen?";
            string keepLabel = _localizationService?.GetString("SkipTutorialKeep") ?? "Tutorial behalten";
            string confirmLabel = _localizationService?.GetString("SkipTutorialConfirm") ?? "Überspringen";

            var task = Dispatcher.UIThread.InvokeAsync(
                () => ConfirmationRequested.Invoke(title, message, keepLabel, confirmLabel));
            bool keep = await task;

            // Logik: Accept-Button = "Behalten" (Default-Schutz für Genre-Neulinge).
            // !keep = "Überspringen" wurde gewählt.
            if (!keep)
            {
                _gameEngine.ConfirmTutorialSkip();
            }
        }
        catch (OperationCanceledException)
        {
            // Dialog bei App-Backgrounding abgebrochen → Tutorial bleibt aktiv.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleTutorialSkipRequested fehlgeschlagen");
        }
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
            _logger.LogError(ex, "HandleGameOver Fehler");
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
            _logger.LogError(ex, "HandleLevelComplete Fehler");
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

        _gameEngine.GameOver -= HandleGameOver;
        _gameEngine.LevelComplete -= HandleLevelComplete;
        _gameEngine.CoinsEarned -= HandleCoinsEarned;
        _gameEngine.TutorialSkipRequested -= HandleTutorialSkipRequested;

        _preloadCts?.Cancel();
        _preloadCts?.Dispose();
        _preloadCts = null;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
