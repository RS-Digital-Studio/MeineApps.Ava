using BomberBlast.AI;
using BomberBlast.Graphics;
using BomberBlast.Input;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Models.Levels;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using SkiaSharp;
// ReSharper disable InconsistentNaming

namespace BomberBlast.Core;

/// <summary>
/// Haupt-Game-Engine: Kern mit Feldern, Properties, Update-Loop und State-Management.
/// Aufgeteilt in partial classes:
/// - GameEngine.cs (dieser) → Kern
/// - GameEngine.Collision.cs → Kollisionserkennung
/// - GameEngine.Explosion.cs → Bomben/Explosionen/Block-Zerstörung
/// - GameEngine.Level.cs → Level-Verwaltung (Laden, PowerUps, Gegner, Abschluss)
/// - GameEngine.Render.cs → Overlay-Rendering
/// </summary>
public partial class GameEngine : IDisposable
{
    // Dependencies
    private readonly SoundManager _soundManager;
    private readonly SpriteSheet _spriteSheet;
    private readonly IProgressService _progressService;
    private readonly IHighScoreService _highScoreService;
    private readonly InputManager _inputManager;
    private readonly ILocalizationService _localizationService;
    private readonly IGameStyleService _gameStyleService;
    private readonly IShopService _shopService;
    private readonly IPurchaseService _purchaseService;
    private readonly GameRenderer _renderer;
    private readonly ITutorialService _tutorialService;
    private readonly TutorialOverlay _tutorialOverlay;

    // Game state
    private GameState _state = GameState.Menu;
    private GameTimer _timer;
    private GameGrid _grid;
    private EnemyAI _enemyAI;

    // Entities
    private Player _player;
    private readonly List<Enemy> _enemies = new();
    private readonly List<Bomb> _bombs = new();
    private readonly List<Explosion> _explosions = new();
    private readonly List<PowerUp> _powerUps = new();

    // Level info
    private Level? _currentLevel;
    private int _currentLevelNumber;
    private bool _isArcadeMode;
    private int _arcadeWave;
    private bool _levelCompleteHandled;
    private bool _continueUsed;

    // Statistics
    private int _bombsUsed;
    private int _enemiesKilled;
    private bool _exitRevealed;

    // Timing
    private float _stateTimer;
    private const float START_DELAY = 2f;
    private const float DEATH_DELAY = 2f;
    private const float LEVEL_COMPLETE_DELAY = 3f;

    // Gecachte SKPaint/SKFont für Overlay-Rendering (vermeidet Allokationen pro Frame)
    private readonly SKPaint _overlayBgPaint = new();
    private readonly SKPaint _overlayTextPaint = new() { IsAntialias = true };
    private readonly SKFont _overlayFont = new() { Embolden = true };
    private readonly SKMaskFilter _overlayGlowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3);
    private readonly SKMaskFilter _overlayGlowFilterLarge = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4);

    // Victory-Timer
    private float _victoryTimer;
    private const float VICTORY_DELAY = 3f;
    private bool _victoryHandled;

    // Game-Feel-Effekte
    private readonly ScreenShake _screenShake = new();
    private readonly ParticleSystem _particleSystem = new();
    private float _hitPauseTimer;

    // Tutorial
    private float _tutorialWarningTimer;

    // Score-Aufschlüsselung (für Level-Complete Summary Screen)
    public int LastTimeBonus { get; private set; }
    public int LastEfficiencyBonus { get; private set; }
    public float LastScoreMultiplier { get; private set; }
    public int LastEnemyKillPoints { get; private set; }

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action? OnGameOver;
    public event Action? OnLevelComplete;
    public event Action? OnVictory;
    public event Action<int>? OnScoreChanged;
    /// <summary>Coins verdient: (coinsEarned, totalScore, isLevelComplete)</summary>
    public event Action<int, int, bool>? OnCoinsEarned;
    /// <summary>Arcade Wave-Milestone erreicht: (wave, bonusCoins)</summary>
    public event Action<int, int>? OnWaveMilestone;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    public GameState State => _state;
    public int Score => _player?.Score ?? 0;
    public int Lives => _player?.Lives ?? 0;
    public int CurrentLevel => _currentLevelNumber;
    public int ArcadeWave => _arcadeWave;
    public float RemainingTime => _timer?.RemainingTime ?? 0;
    public bool IsArcadeMode => _isArcadeMode;
    public bool IsCurrentScoreHighScore => _highScoreService.IsHighScore(Score);

    /// <summary>Ob Continue möglich ist (nur Story, nur 1x pro Level-Versuch)</summary>
    public bool CanContinue => !_continueUsed && !_isArcadeMode;

    /// <summary>Verschiebung nach unten für Banner-Ad oben (Proxy für GameRenderer)</summary>
    public float BannerTopOffset
    {
        get => _renderer.BannerTopOffset;
        set => _renderer.BannerTopOffset = value;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INPUT FORWARDING
    // ═══════════════════════════════════════════════════════════════════════

    public void OnTouchStart(float x, float y, float screenWidth, float screenHeight)
    {
        // Tutorial: Skip-Button oder Tap-to-Continue prüfen
        if (_tutorialService.IsActive && _state == GameState.Playing)
        {
            if (_tutorialOverlay.IsSkipButtonHit(x, y))
            {
                _tutorialService.Skip();
                return;
            }

            // Warning-Schritt: Tap zum Weitermachen
            if (_tutorialService.CurrentStep?.Type == TutorialStepType.Warning)
            {
                _tutorialService.NextStep();
                _tutorialWarningTimer = 0;
                return;
            }
        }

        _inputManager.OnTouchStart(x, y, screenWidth, screenHeight);
    }

    public void OnTouchMove(float x, float y)
        => _inputManager.OnTouchMove(x, y);

    public void OnTouchEnd()
        => _inputManager.OnTouchEnd();

    public void OnKeyDown(Avalonia.Input.Key key)
        => _inputManager.OnKeyDown(key);

    public void OnKeyUp(Avalonia.Input.Key key)
        => _inputManager.OnKeyUp(key);

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public GameEngine(
        SoundManager soundManager,
        SpriteSheet spriteSheet,
        IProgressService progressService,
        IHighScoreService highScoreService,
        InputManager inputManager,
        ILocalizationService localizationService,
        IGameStyleService gameStyleService,
        IShopService shopService,
        IPurchaseService purchaseService,
        GameRenderer renderer,
        ITutorialService tutorialService)
    {
        _soundManager = soundManager;
        _spriteSheet = spriteSheet;
        _progressService = progressService;
        _highScoreService = highScoreService;
        _inputManager = inputManager;
        _localizationService = localizationService;
        _gameStyleService = gameStyleService;
        _shopService = shopService;
        _purchaseService = purchaseService;

        _renderer = renderer;
        _tutorialService = tutorialService;
        _tutorialOverlay = new TutorialOverlay(localizationService);
        _grid = new GameGrid();
        _timer = new GameTimer();
        _enemyAI = new EnemyAI(_grid);
        _player = new Player(0, 0);

        // Timer-Events abonnieren
        _timer.OnWarning += OnTimeWarning;
        _timer.OnExpired += OnTimeExpired;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC ACTIONS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Boost-PowerUp anwenden (aus Rewarded Ad vor Level-Start)</summary>
    public void ApplyBoostPowerUp(string boostType)
    {
        switch (boostType)
        {
            case "speed":
                _player.HasSpeed = true;
                break;
            case "fire":
                _player.FireRange += 1;
                break;
            case "bombs":
                _player.MaxBombs += 1;
                break;
        }
    }

    /// <summary>Score verdoppeln (nach Level-Complete Rewarded Ad)</summary>
    public void DoubleScore()
    {
        int scoreBefore = _player.Score;
        _player.Score *= 2;
        int coinsEarned = _player.Score - scoreBefore;
        OnScoreChanged?.Invoke(_player.Score);
        OnCoinsEarned?.Invoke(coinsEarned, _player.Score, true);
    }

    /// <summary>Spiel nach Game Over fortsetzen (per Rewarded Ad)</summary>
    public void ContinueAfterGameOver()
    {
        if (_continueUsed) return;

        _continueUsed = true;
        _player.Lives = 1;
        RespawnPlayer();
    }

    /// <summary>Spiel pausieren</summary>
    public void Pause()
    {
        if (_state == GameState.Playing)
        {
            _state = GameState.Paused;
            _timer.Pause();
            _soundManager.PauseMusic();
        }
    }

    /// <summary>Spiel fortsetzen</summary>
    public void Resume()
    {
        if (_state == GameState.Paused)
        {
            _state = GameState.Playing;
            _timer.Resume();
            _soundManager.ResumeMusic();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UPDATE LOOP
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Game-State pro Frame aktualisieren</summary>
    public void Update(float deltaTime)
    {
        _renderer.Update(deltaTime);
        _screenShake.Update(deltaTime);
        _particleSystem.Update(deltaTime);

        // Hit-Pause: Update wird übersprungen, Rendering läuft weiter (Freeze-Effekt)
        if (_hitPauseTimer > 0)
        {
            _hitPauseTimer -= deltaTime;
            return;
        }

        switch (_state)
        {
            case GameState.Starting:
                UpdateStarting(deltaTime);
                break;

            case GameState.Playing:
                UpdatePlaying(deltaTime);
                break;

            case GameState.PlayerDied:
                UpdatePlayerDied(deltaTime);
                break;

            case GameState.LevelComplete:
                UpdateLevelComplete(deltaTime);
                break;

            case GameState.Victory:
                UpdateVictory(deltaTime);
                break;

            case GameState.Paused:
                // Nichts tun
                break;
        }
    }

    private void UpdateStarting(float deltaTime)
    {
        _stateTimer += deltaTime;
        if (_stateTimer >= START_DELAY)
        {
            _state = GameState.Playing;
            _timer.Start();
        }
    }

    private void UpdatePlaying(float deltaTime)
    {
        _timer.Update(deltaTime);
        UpdatePlayer(deltaTime);
        _inputManager.Update(deltaTime);
        UpdateBombs(deltaTime);
        UpdateExplosions(deltaTime);
        UpdateDestroyingBlocks(deltaTime);
        UpdateEnemies(deltaTime);
        UpdatePowerUps(deltaTime);
        CheckCollisions();
        CheckWinCondition();
        CleanupEntities();

        // Tutorial: Warning-Schritt auto-advance nach 3 Sekunden
        if (_tutorialService.IsActive && _tutorialService.CurrentStep?.Type == TutorialStepType.Warning)
        {
            _tutorialWarningTimer += deltaTime;
            if (_tutorialWarningTimer >= 3f)
            {
                _tutorialService.NextStep();
                _tutorialWarningTimer = 0;
            }
        }
    }

    private void UpdatePlayer(float deltaTime)
    {
        if (_player.IsDying || !_player.IsActive)
        {
            _player.Update(deltaTime);
            return;
        }

        // Detonator-Button Sichtbarkeit aktualisieren
        _inputManager.HasDetonator = _player.HasDetonator;

        // Input anwenden
        _player.MovementDirection = _inputManager.MovementDirection;
        _player.Move(deltaTime, _grid);
        _player.Update(deltaTime);

        // Tutorial: Bewegungs-Schritt als abgeschlossen markieren
        if (_tutorialService.IsActive && _player.IsMoving)
        {
            _tutorialService.CheckStepCompletion(TutorialStepType.Move);
        }

        // Bombe platzieren
        if (_inputManager.BombPressed && _player.CanPlaceBomb())
        {
            PlaceBomb();
            // Tutorial: Bomben-Schritt als abgeschlossen markieren
            _tutorialService.CheckStepCompletion(TutorialStepType.PlaceBomb);
        }

        // Manuelle Detonation
        if (_inputManager.DetonatePressed && _player.HasDetonator)
        {
            DetonateAllBombs();
        }
    }

    private void UpdatePlayerDied(float deltaTime)
    {
        _stateTimer += deltaTime;
        _player.Update(deltaTime);

        if (_stateTimer >= DEATH_DELAY)
        {
            _player.Lives--;

            if (_player.Lives <= 0)
            {
                _state = GameState.GameOver;
                _soundManager.PlaySound(SoundManager.SFX_GAME_OVER);
                _soundManager.StopMusic();

                // High Score speichern (Arcade)
                if (_isArcadeMode)
                {
                    if (_highScoreService.IsHighScore(_player.Score))
                    {
                        _highScoreService.AddScore("PLAYER", _player.Score, _arcadeWave);
                    }
                }

                // Trost-Coins (halber Score, abgerundet)
                int coins = _player.Score / 2;
                if (coins > 0)
                {
                    OnCoinsEarned?.Invoke(coins, _player.Score, false);
                }

                OnGameOver?.Invoke();
            }
            else
            {
                RespawnPlayer();
            }
        }
    }

    private void RespawnPlayer()
    {
        _player.Respawn(
            1 * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f,
            1 * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f);

        _state = GameState.Starting;
        _stateTimer = 0;

        // Bomben und Explosionen leeren
        foreach (var bomb in _bombs)
        {
            var cell = _grid.TryGetCell(bomb.GridX, bomb.GridY);
            if (cell != null) cell.Bomb = null;
        }
        _bombs.Clear();

        foreach (var explosion in _explosions)
        {
            explosion.ClearFromGrid(_grid);
        }
        _explosions.Clear();

        _inputManager.Reset();
    }

    private void CleanupEntities()
    {
        _bombs.RemoveAll(b => b.IsMarkedForRemoval);
        _explosions.RemoveAll(e => e.IsMarkedForRemoval);
        _enemies.RemoveAll(e => e.IsMarkedForRemoval);
        _powerUps.RemoveAll(p => p.IsMarkedForRemoval);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        _timer.OnWarning -= OnTimeWarning;
        _timer.OnExpired -= OnTimeExpired;

        _overlayBgPaint.Dispose();
        _overlayTextPaint.Dispose();
        _overlayFont.Dispose();
        _overlayGlowFilter.Dispose();
        _overlayGlowFilterLarge.Dispose();
        _particleSystem.Dispose();
        _tutorialOverlay.Dispose();
    }
}
