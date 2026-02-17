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
    private readonly IAchievementService _achievementService;

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
    private bool _isDailyChallenge;
    private int _arcadeWave;
    private bool _levelCompleteHandled;
    private bool _continueUsed;

    // Statistics
    private int _bombsUsed;
    private int _enemiesKilled;
    private bool _exitRevealed;
    private Cell? _exitCell; // Gecachte Exit-Position (vermeidet Grid-Iteration pro Frame)
    private int _scoreAtLevelStart; // Score zu Beginn des Levels (für Coin-Berechnung)
    private bool _playerDamagedThisLevel; // Für NoDamage-Achievement

    // Timing
    private float _stateTimer;
    private const float START_DELAY = 3f;
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
    private readonly GameFloatingTextSystem _floatingText = new();
    private float _hitPauseTimer;

    // Combo-System (Kettenexplosionen)
    private int _comboCount;
    private float _comboTimer;
    private const float COMBO_WINDOW = 2f; // Sekunden

    // "DEFEAT ALL!" Cooldown (verhindert Spam bei jedem Frame)
    private float _defeatAllCooldown;

    // Slow-Motion bei letztem Kill / hohem Combo
    private float _slowMotionTimer;
    private float _slowMotionFactor = 1f;
    private const float SLOW_MOTION_DURATION = 0.8f; // Sekunden (in Echtzeit)
    private const float SLOW_MOTION_FACTOR = 0.3f; // 30% Geschwindigkeit

    // Sterne-Rating bei Level-Complete (für Overlay-Rendering)
    private int _levelCompleteStars;

    // Welt-/Wave-Ankündigung
    private float _worldAnnouncementTimer;
    private string _worldAnnouncementText = "";

    // Pontan-Strafe (gestaffeltes Spawning)
    private bool _pontanPunishmentActive;
    private int _pontanSpawned;
    private float _pontanSpawnTimer;
    private const int PONTAN_MAX_COUNT = 3;
    private const float PONTAN_SPAWN_INTERVAL = 5f; // Sekunden zwischen Spawns
    private const int PONTAN_MIN_DISTANCE = 5; // Mindestabstand zum Spieler
    private readonly Random _pontanRandom = new(); // Wiederverwendbar statt new Random() pro Aufruf

    // Tutorial
    private float _tutorialWarningTimer;

    // Pause-Button (Touch-Geräte, oben-links)
    private const float PAUSE_BUTTON_SIZE = 40f;
    private const float PAUSE_BUTTON_MARGIN = 10f;
    /// <summary>Callback für Pause-Anfrage vom Touch-Button</summary>
    public event Action? OnPauseRequested;

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
    public bool IsDailyChallenge => _isDailyChallenge;
    public bool IsCurrentScoreHighScore => _highScoreService.IsHighScore(Score);

    /// <summary>Ob Continue möglich ist (nur Story, nur 1x pro Level-Versuch)</summary>
    public bool CanContinue => !_continueUsed && !_isArcadeMode && !_isDailyChallenge;

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
        // Pause-Button prüfen (oben-links, nur wenn Android / Touch)
        if (_state == GameState.Playing && OperatingSystem.IsAndroid())
        {
            float pauseRight = PAUSE_BUTTON_MARGIN + PAUSE_BUTTON_SIZE;
            float pauseTop = PAUSE_BUTTON_MARGIN + BannerTopOffset;
            float pauseBottom = pauseTop + PAUSE_BUTTON_SIZE;
            if (x <= pauseRight + 10 && y >= pauseTop - 10 && y <= pauseBottom + 10)
            {
                OnPauseRequested?.Invoke();
                return;
            }
        }

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
        ITutorialService tutorialService,
        IAchievementService achievementService)
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
        _achievementService = achievementService;
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
                // SpeedLevel um 1 erhöhen (nicht nur auf 1 setzen, falls schon vorhanden)
                _player.SpeedLevel = Math.Min(_player.SpeedLevel + 1, 3);
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
        _floatingText.Update(deltaTime);
        _soundManager.Update(deltaTime);

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
        // Echtzeit-deltaTime speichern BEVOR Slow-Motion angewendet wird
        // Timer und Combo-Timer laufen in Echtzeit (kein Exploit durch Slow-Motion)
        float realDeltaTime = deltaTime;

        // Slow-Motion: deltaTime verlangsamen für dramatischen Effekt
        if (_slowMotionTimer > 0)
        {
            _slowMotionTimer -= realDeltaTime; // Echtzeit runterzählen
            float progress = _slowMotionTimer / SLOW_MOTION_DURATION;
            // Sanftes Easing: langsam → normal (Ease-Out)
            _slowMotionFactor = SLOW_MOTION_FACTOR + (1f - SLOW_MOTION_FACTOR) * (1f - progress);
            deltaTime *= _slowMotionFactor;
        }
        else
        {
            _slowMotionFactor = 1f;
        }

        // Timer + Combo laufen in Echtzeit (nicht durch Slow-Motion beeinflusst)
        _timer.Update(realDeltaTime);

        UpdatePlayer(deltaTime);
        _inputManager.Update(deltaTime);
        UpdateBombs(deltaTime);
        UpdateExplosions(deltaTime);
        UpdateDestroyingBlocks(deltaTime);
        UpdateEnemies(deltaTime);
        UpdatePowerUps(deltaTime);
        UpdateWorldMechanics(deltaTime);
        CheckCollisions();
        CleanupEntities();

        // Pontan-Strafe (gestaffeltes Spawning nach Timer-Ablauf)
        if (_pontanPunishmentActive)
            UpdatePontanPunishment(realDeltaTime);

        // Welt-Ankündigungs-Timer aktualisieren
        if (_worldAnnouncementTimer > 0)
            _worldAnnouncementTimer -= realDeltaTime;

        // Collecting-PowerUp-Animationen aktualisieren
        UpdateCollectingPowerUps(deltaTime);

        // Combo-Timer in Echtzeit aktualisieren (Slow-Motion verlängert keine Combos)
        if (_comboTimer > 0)
        {
            _comboTimer -= realDeltaTime;
            if (_comboTimer <= 0)
                _comboCount = 0;
        }

        // "DEFEAT ALL!" Cooldown aktualisieren
        if (_defeatAllCooldown > 0)
            _defeatAllCooldown -= realDeltaTime;

        // Tutorial: Warning-Schritt auto-advance nach 3 Sekunden (Echtzeit)
        if (_tutorialService.IsActive && _tutorialService.CurrentStep?.Type == TutorialStepType.Warning)
        {
            _tutorialWarningTimer += realDeltaTime;
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

        // Input anwenden (ReverseControls-Curse invertiert die Richtung)
        var inputDir = _inputManager.MovementDirection;
        if (_player.ActiveCurse == CurseType.ReverseControls && inputDir != Direction.None)
        {
            inputDir = inputDir switch
            {
                Direction.Up => Direction.Down,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                Direction.Right => Direction.Left,
                _ => inputDir
            };
        }
        _player.MovementDirection = inputDir;

        // Vor der Bewegung Position merken (für Kick-Detection)
        int prevGridX = _player.GridX;
        int prevGridY = _player.GridY;
        _player.Move(deltaTime, _grid);
        _player.Update(deltaTime);

        // Kick-Mechanik: Wenn Spieler auf eine Bombe läuft und Kick hat
        if (_player.HasKick && _player.IsMoving)
        {
            TryKickBomb(prevGridX, prevGridY);
        }

        // Tutorial: Bewegungs-Schritt als abgeschlossen markieren
        if (_tutorialService.IsActive && _player.IsMoving)
        {
            _tutorialService.CheckStepCompletion(TutorialStepType.Move);
        }

        // Diarrhea-Curse: Automatisch Bomben legen
        if (_player.ActiveCurse == CurseType.Diarrhea && _player.DiarrheaTimer <= 0)
        {
            if (_player.CanPlaceBomb())
            {
                PlaceBomb();
            }
            _player.DiarrheaTimer = 0.5f;
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

    /// <summary>
    /// Kick-Mechanik: Prüft ob der Spieler auf eine Bombe gelaufen ist und kickt sie
    /// </summary>
    private void TryKickBomb(int prevGridX, int prevGridY)
    {
        int curGridX = _player.GridX;
        int curGridY = _player.GridY;

        // Nur wenn Spieler die Zelle gewechselt hat
        if (curGridX == prevGridX && curGridY == prevGridY) return;

        var cell = _grid.TryGetCell(curGridX, curGridY);
        if (cell?.Bomb == null || cell.Bomb.IsSliding || cell.Bomb.HasExploded) return;

        // Bombe in Bewegungsrichtung des Spielers kicken
        cell.Bomb.Kick(_player.FacingDirection);
        cell.Bomb = null; // Aus Grid entfernen, UpdateBombSlide registriert sie am Ziel
        _soundManager.PlaySound(SoundManager.SFX_PLACE_BOMB); // Kick-Sound (kann später eigenen bekommen)
    }

    private void UpdatePlayerDied(float deltaTime)
    {
        _stateTimer += deltaTime;
        _player.Update(deltaTime);

        // Bomben, Explosionen und Gegner laufen weiter (klassisches Bomberman-Verhalten)
        UpdateBombs(deltaTime);
        UpdateExplosions(deltaTime);
        UpdateDestroyingBlocks(deltaTime);
        UpdateEnemies(deltaTime);
        CleanupEntities();

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

                // Trost-Coins (Level-Score ÷ 6, abgerundet)
                int coins = (_player.Score - _scoreAtLevelStart) / 6;
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

    // ═══════════════════════════════════════════════════════════════════════
    // WELT-MECHANIKEN (Ice, Conveyor, Teleporter, LavaCrack)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Welt-spezifische Mechaniken pro Frame aktualisieren
    /// </summary>
    private void UpdateWorldMechanics(float deltaTime)
    {
        if (_currentLevel == null || _currentLevel.Mechanic == WorldMechanic.None)
            return;

        switch (_currentLevel.Mechanic)
        {
            case WorldMechanic.Ice:
                UpdateIceMechanic(deltaTime);
                break;
            case WorldMechanic.Conveyor:
                UpdateConveyorMechanic(deltaTime);
                break;
            case WorldMechanic.Teleporter:
                UpdateTeleporterMechanic(deltaTime);
                break;
            case WorldMechanic.LavaCrack:
                UpdateLavaCrackMechanic(deltaTime);
                break;
        }
    }

    /// <summary>
    /// Eis: Spieler rutscht in Bewegungsrichtung weiter wenn auf Eis (Trägheit)
    /// Implementiert als erhöhte Geschwindigkeit + verringerter Grip
    /// </summary>
    private void UpdateIceMechanic(float deltaTime)
    {
        var cell = _grid.TryGetCell(_player.GridX, _player.GridY);
        if (cell?.Type == CellType.Ice)
        {
            // Eis-Boost: Auf Eis bewegt sich der Spieler 40% schneller (Rutsch-Gefühl)
            // Die Player.Move() Methode berechnet bereits die Geschwindigkeit,
            // hier wenden wir den Effekt als nachträglichen Positions-Nudge an
            if (_player.IsMoving && _player.FacingDirection != Direction.None)
            {
                float iceBoost = _player.Speed * 0.4f * deltaTime;
                float dx = _player.FacingDirection.GetDeltaX() * iceBoost;
                float dy = _player.FacingDirection.GetDeltaY() * iceBoost;

                // Nur anwenden wenn Zielposition begehbar ist
                float newX = _player.X + dx;
                float newY = _player.Y + dy;
                int targetGX = (int)MathF.Floor(newX / GameGrid.CELL_SIZE);
                int targetGY = (int)MathF.Floor(newY / GameGrid.CELL_SIZE);
                var targetCell = _grid.TryGetCell(targetGX, targetGY);
                if (targetCell != null && targetCell.IsWalkable(_player.HasWallpass, _player.HasBombpass))
                {
                    _player.X = newX;
                    _player.Y = newY;
                }
            }
        }
    }

    /// <summary>
    /// Förderband: Schiebt Spieler und Gegner langsam in Pfeilrichtung
    /// </summary>
    private void UpdateConveyorMechanic(float deltaTime)
    {
        float conveyorSpeed = 40f; // Pixel pro Sekunde

        // Spieler auf Förderband
        var playerCell = _grid.TryGetCell(_player.GridX, _player.GridY);
        if (playerCell?.Type == CellType.Conveyor)
        {
            float dx = playerCell.ConveyorDirection.GetDeltaX() * conveyorSpeed * deltaTime;
            float dy = playerCell.ConveyorDirection.GetDeltaY() * conveyorSpeed * deltaTime;

            float newX = _player.X + dx;
            float newY = _player.Y + dy;
            int targetGX = (int)MathF.Floor(newX / GameGrid.CELL_SIZE);
            int targetGY = (int)MathF.Floor(newY / GameGrid.CELL_SIZE);
            var targetCell = _grid.TryGetCell(targetGX, targetGY);
            if (targetCell != null && targetCell.IsWalkable(_player.HasWallpass, _player.HasBombpass))
            {
                _player.X = newX;
                _player.Y = newY;
            }
        }

        // Gegner auf Förderbändern
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || enemy.IsDying) continue;
            var enemyCell = _grid.TryGetCell(enemy.GridX, enemy.GridY);
            if (enemyCell?.Type != CellType.Conveyor) continue;

            float dx = enemyCell.ConveyorDirection.GetDeltaX() * conveyorSpeed * deltaTime;
            float dy = enemyCell.ConveyorDirection.GetDeltaY() * conveyorSpeed * deltaTime;
            float newX = enemy.X + dx;
            float newY = enemy.Y + dy;
            int targetGX = (int)MathF.Floor(newX / GameGrid.CELL_SIZE);
            int targetGY = (int)MathF.Floor(newY / GameGrid.CELL_SIZE);
            var targetCell = _grid.TryGetCell(targetGX, targetGY);
            if (targetCell != null && targetCell.IsWalkable())
            {
                enemy.X = newX;
                enemy.Y = newY;
            }
        }
    }

    /// <summary>
    /// Teleporter: Transportiert Spieler/Gegner zum gepaarten Portal
    /// </summary>
    private void UpdateTeleporterMechanic(float deltaTime)
    {
        // Teleporter-Cooldowns aktualisieren
        for (int x = 0; x < GameGrid.WIDTH; x++)
        {
            for (int y = 0; y < GameGrid.HEIGHT; y++)
            {
                var cell = _grid[x, y];
                if (cell.Type == CellType.Teleporter && cell.TeleporterCooldown > 0)
                    cell.TeleporterCooldown -= deltaTime;
            }
        }

        // Spieler-Teleportation
        var playerCell = _grid.TryGetCell(_player.GridX, _player.GridY);
        if (playerCell?.Type == CellType.Teleporter && playerCell.TeleporterCooldown <= 0 && playerCell.TeleporterTarget.HasValue)
        {
            var target = playerCell.TeleporterTarget.Value;
            var targetCell = _grid.TryGetCell(target.x, target.y);
            if (targetCell != null)
            {
                float newX = target.x * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                float newY = target.y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                _player.X = newX;
                _player.Y = newY;

                // Cooldown auf beiden Seiten setzen (verhindert Ping-Pong)
                playerCell.TeleporterCooldown = 1.0f;
                targetCell.TeleporterCooldown = 1.0f;

                // Partikel-Effekt
                _particleSystem.Emit(newX, newY, 10, new SkiaSharp.SKColor(100, 200, 255), 60f, 0.5f);
                _soundManager.PlaySound(SoundManager.SFX_POWERUP);
            }
        }

        // Gegner-Teleportation
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || enemy.IsDying) continue;
            var enemyCell = _grid.TryGetCell(enemy.GridX, enemy.GridY);
            if (enemyCell?.Type != CellType.Teleporter || enemyCell.TeleporterCooldown > 0 || !enemyCell.TeleporterTarget.HasValue)
                continue;

            var target = enemyCell.TeleporterTarget.Value;
            var targetCell = _grid.TryGetCell(target.x, target.y);
            if (targetCell == null) continue;

            enemy.X = target.x * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            enemy.Y = target.y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;

            enemyCell.TeleporterCooldown = 1.5f; // Gegner etwas längerer Cooldown
            targetCell.TeleporterCooldown = 1.5f;
        }
    }

    /// <summary>
    /// Lava-Risse: Timer hochzählen, Schaden bei aktivem Zustand
    /// </summary>
    private void UpdateLavaCrackMechanic(float deltaTime)
    {
        for (int x = 0; x < GameGrid.WIDTH; x++)
        {
            for (int y = 0; y < GameGrid.HEIGHT; y++)
            {
                var cell = _grid[x, y];
                if (cell.Type != CellType.LavaCrack) continue;

                cell.LavaCrackTimer += deltaTime;

                // Spieler-Schaden bei aktivem Lava-Riss
                if (cell.IsLavaCrackActive && _player.GridX == x && _player.GridY == y)
                {
                    if (!_player.IsInvincible && !_player.HasSpawnProtection && !_player.IsDying)
                    {
                        if (_player.HasShield)
                        {
                            _player.HasShield = false;
                            _particleSystem.Emit(_player.X, _player.Y, 12,
                                new SkiaSharp.SKColor(255, 80, 0), 60f, 0.5f);
                            _floatingText.Spawn(_player.X, _player.Y - 16,
                                "SHIELD!", new SkiaSharp.SKColor(0, 229, 255), 16f, 1.2f);
                            _player.ActivateInvincibility(0.5f);
                        }
                        else
                        {
                            KillPlayer();
                        }
                    }
                }

                // Gegner-Schaden bei aktivem Lava-Riss
                if (cell.IsLavaCrackActive)
                {
                    for (int i = _enemies.Count - 1; i >= 0; i--)
                    {
                        var enemy = _enemies[i];
                        if (!enemy.IsActive || enemy.IsDying) continue;
                        if (enemy.GridX == x && enemy.GridY == y)
                        {
                            KillEnemy(enemy);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// PowerUps die gerade eingesammelt werden: Timer runterzählen, bei 0 endgültig entfernen
    /// </summary>
    private void UpdateCollectingPowerUps(float deltaTime)
    {
        for (int i = _powerUps.Count - 1; i >= 0; i--)
        {
            var pu = _powerUps[i];
            if (!pu.IsBeingCollected) continue;

            pu.CollectTimer -= deltaTime;
            if (pu.CollectTimer <= 0)
            {
                pu.IsMarkedForRemoval = true;
            }
        }
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
        _floatingText.Dispose();
        _tutorialOverlay.Dispose();
        _inputManager.Dispose();
    }
}
