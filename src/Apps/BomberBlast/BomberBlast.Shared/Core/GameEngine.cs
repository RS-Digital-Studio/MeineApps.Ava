using BomberBlast.AI;
using BomberBlast.Graphics;
using BomberBlast.Input;
using BomberBlast.Models;
using BomberBlast.Models.Dungeon;
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
    // Event-Handler (als Feld für Dispose-Abmeldung)
    private readonly Action _directionChangedHandler;
    private readonly EventHandler _languageChangedHandler;

    // Dependencies
    private readonly SoundManager _soundManager;
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
    private readonly IDiscoveryService _discoveryService;
    private readonly IDungeonService _dungeonService;
    private readonly IDungeonUpgradeService _dungeonUpgradeService;
    private readonly IGameTrackingService _tracking;
    private readonly IVibrationService _vibration;

    // Discovery-Hints (Erstentdeckung von PowerUps/Mechaniken)
    private readonly DiscoveryOverlay _discoveryOverlay;

    // Gecachte HUD-Labels (nur bei Level-Start und Sprachwechsel aktualisiert)
    private string _hudLabelKills = "KILLS";
    private string _hudLabelTime = "TIME";
    private string _hudLabelScore = "SCORE";
    private string _hudLabelLives = "LIVES";
    private string _hudLabelBombs = "BOMBS";
    private string _hudLabelPower = "POWER";
    private string _hudLabelDeck = "DECK";
    private string _hudLabelBuffs = "BUFFS";

    private bool _disposed;

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
    private bool _isDailyChallenge;
    private bool _isSurvivalMode;
    private bool _isQuickPlayMode;
    private int _quickPlayDifficulty;
    private bool _isDungeonRun;
    private bool _levelCompleteHandled;
    private bool _continueUsed;

    // Dungeon Legendäre Buffs
    private float _timeFreezeTimer;       // TimeFreeze: Alle Gegner eingefroren (3s bei Floor-Start)
    private bool _phantomWalkAvailable;    // Phantom: Buff aktiv im Run
    private bool _phantomWalkActive;       // Phantom: Gerade durch Wände laufend
    private float _phantomWalkTimer;       // Phantom: Verbleibende Dauer (5s)
    private float _phantomCooldownTimer;   // Phantom: Cooldown bis nächste Aktivierung (30s)
    private bool _playerHadWallpassBeforePhantom; // Merkt ob Spieler echtes Wallpass hatte

    // Dungeon-Synergien (B5)
    private bool _synergyBombardierActive;  // ExtraBomb+ExtraFire: +1 je
    private bool _synergyBlitzkriegActive;  // SpeedBoost+BombTimer: -0.5s Zünd
    private bool _synergyFortressActive;    // Shield+ExtraLife: Shield-Regen 20s
    private float _fortressRegenTimer;      // Verstrichene Zeit ohne Schaden
    private bool _synergyMidasActive;       // CoinBonus+GoldRush: Coins bei Kill
    private bool _synergyElementalActive;   // EnemySlow+FireImmunity: Lava→Slow
    private float _dungeonBombFuseReduction;// Kumulative Zündschnur-Reduktion (BombTimer + Blitzkrieg)
    private bool _dungeonEnemySlowActive;   // EnemySlow Buff: 20% langsamere Gegner

    // Floor-Modifikatoren (B4)
    private DungeonFloorModifier _dungeonFloorModifier; // Aktiver Modifikator auf diesem Floor
    private float _dungeonModifierRegenTimer;           // Timer für Regeneration-Modifikator (Shield nach 15s)

    // Survival-Modus: Endloses Spawning mit steigender Schwierigkeit
    private float _survivalSpawnTimer;
    private float _survivalSpawnInterval = 5f;
    private float _survivalTimeElapsed;
    private const float SURVIVAL_MIN_SPAWN_INTERVAL = 0.8f;
    private const float SURVIVAL_SPAWN_DECREASE = 0.12f; // Intervall schrumpft pro Spawn

    // Gecachte Mechanik-Zellen (vermeidet 150-Zellen-Grid-Scan pro Frame)
    private readonly List<Cell> _mechanicCells = new();

    // Wiederverwendbare Liste für Block-Zellen (vermeidet LINQ .ToList() Allokation in PlacePowerUps/PlaceExit)
    private readonly List<Cell> _blockCells = new();

    // Dirty-Listen für geänderte Zellen (vermeidet 3x komplette Grid-Iteration pro Frame)
    private readonly List<Cell> _destroyingCells = new();
    private readonly List<Cell> _afterglowCells = new();
    private readonly List<Cell> _specialEffectCells = new();

    // Gegner-Positions-Cache für Bomben-Slide-Kollision (HashSet für schnellen Contains-Check)
    private readonly HashSet<(int x, int y)> _enemyPositionHashSet = new();

    // Ausstehende Eis-Cleanups (Frame-basiert statt Task.Delay → Thread-Race vermeiden)
    private readonly List<(List<(int x, int y)> cells, float timer)> _pendingIceCleanups = new();

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

    /// <summary>
    /// Schutzschild absorbiert 1 Treffer: Partikel-Burst, "SHIELD!" Text, kurze Unverwundbarkeit.
    /// </summary>
    private void AbsorbShield(SKColor particleColor, int particleCount = 16, float spread = 80f, bool playSound = true)
    {
        _player.HasShield = false;
        _fortressRegenTimer = 0; // Festungs-Synergy: Timer bei Schaden zurücksetzen
        _dungeonModifierRegenTimer = 0; // Regen-Modifikator: Timer bei Schaden zurücksetzen
        _particleSystem.Emit(_player.X, _player.Y, particleCount, particleColor, spread, particleCount >= 16 ? 0.6f : 0.5f);
        _floatingText.Spawn(_player.X, _player.Y - 16, "SHIELD!", new SKColor(0, 229, 255), 16f, 1.2f);
        if (playSound)
            _soundManager.PlaySound(SoundManager.SFX_POWERUP);
        _player.ActivateInvincibility(0.5f);
        _vibration.VibrateMedium();
    }

    /// <summary>
    /// Lokalisierte HUD-Labels cachen (bei Level-Start und Sprachwechsel)
    /// </summary>
    private void CacheHudLabels()
    {
        _hudLabelKills = _localizationService.GetString("HudKills") ?? "KILLS";
        _hudLabelTime = _localizationService.GetString("HudTime") ?? "TIME";
        _hudLabelScore = _localizationService.GetString("HudScore") ?? "SCORE";
        _hudLabelLives = _localizationService.GetString("HudLives") ?? "LIVES";
        _hudLabelBombs = _localizationService.GetString("HudBombs") ?? "BOMBS";
        _hudLabelPower = _localizationService.GetString("HudPower") ?? "POWER";
        _hudLabelDeck = _localizationService.GetString("HudDeck") ?? "DECK";
        _hudLabelBuffs = _localizationService.GetString("HudBuffs") ?? "BUFFS";
    }

    // Slow-Motion bei letztem Kill / hohem Combo
    private float _slowMotionTimer;
    private float _slowMotionFactor = 1f;
    private const float SLOW_MOTION_DURATION = 0.8f; // Sekunden (in Echtzeit)
    private const float SLOW_MOTION_FACTOR = 0.3f; // 30% Geschwindigkeit

    // Sterne-Rating bei Level-Complete (für Overlay-Rendering)
    private int _levelCompleteStars;

    // Erster Sieg (Level 1 zum ersten Mal abgeschlossen)
    private bool _isFirstVictory;

    // Welt-/Wave-Ankündigung
    private float _worldAnnouncementTimer;
    private string _worldAnnouncementText = "";

    // Pontan-Strafe (gestaffeltes Spawning, nach Welt skaliert)
    private bool _pontanPunishmentActive;
    private int _pontanSpawned;
    private float _pontanSpawnTimer;
    private float _pontanInitialDelay; // Gnadenfrist vor erstem Spawn (welt-abhängig)
    private const float PONTAN_WARNING_TIME = 1.5f; // Sekunden Vorwarnung vor Spawn
    private const int PONTAN_MIN_DISTANCE = 5; // Mindestabstand zum Spieler
    private readonly Random _pontanRandom = new(); // Wiederverwendbar statt new Random() pro Aufruf

    /// <summary>Maximale Pontan-Anzahl, skaliert nach Welt (Welt 1-2 weniger)</summary>
    private int GetPontanMaxCount()
    {
        int world = (_currentLevelNumber - 1) / 10;
        return world switch
        {
            0 => 1,  // Welt 1: Nur 1 Pontan
            1 => 2,  // Welt 2: Max 2
            _ => 3   // Welt 3+: Normal (3)
        };
    }

    /// <summary>Spawn-Intervall zwischen Pontans, skaliert nach Welt (frühe Welten langsamer)</summary>
    private float GetPontanSpawnInterval()
    {
        int world = (_currentLevelNumber - 1) / 10;
        return world switch
        {
            0 => 8f,  // Welt 1: 8s zwischen Spawns (viel Zeit zum Reagieren)
            1 => 6f,  // Welt 2: 6s
            2 => 5f,  // Welt 3: 5s (Standard)
            _ => 5f   // Welt 4+: 5s
        };
    }

    /// <summary>Gnadenfrist nach Timer-Ablauf bevor erster Pontan spawnt</summary>
    private float GetPontanInitialDelay()
    {
        int world = (_currentLevelNumber - 1) / 10;
        return world switch
        {
            0 => 5f,  // Welt 1: 5s Gnadenfrist → Spieler kann noch reagieren
            1 => 3f,  // Welt 2: 3s
            _ => 0f   // Welt 3+: Sofort (wie bisher)
        };
    }

    // Pontan-Spawn-Warnung (vorberechnete Position)
    private float _pontanWarningX;
    private float _pontanWarningY;
    private bool _pontanWarningActive;

    // Tutorial
    private float _tutorialWarningTimer;

    // Pause-Button (Touch-Geräte, oben-links)
    private const float PAUSE_BUTTON_SIZE = 40f;
    private const float PAUSE_BUTTON_MARGIN = 10f;
    /// <summary>Callback für Pause-Anfrage vom Touch-Button</summary>
    public event Action? PauseRequested;

    // Score-Aufschlüsselung (für Level-Complete Summary Screen)
    public int LastTimeBonus { get; private set; }
    public int LastEfficiencyBonus { get; private set; }
    public float LastScoreMultiplier { get; private set; }
    public int LastEnemyKillPoints { get; private set; }

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action? GameOver;
    public event Action? LevelComplete;
    public event Action? Victory;
    public event Action<int>? ScoreChanged;
    /// <summary>Coins verdient: (coinsEarned, totalScore, isLevelComplete)</summary>
    public event Action<int, int, bool>? CoinsEarned;
    /// <summary>Joystick-Richtungswechsel (für haptisches Feedback auf Android)</summary>
    public event Action? DirectionChanged;
    /// <summary>Dungeon-Floor abgeschlossen (Belohnung)</summary>
    public event Action<DungeonFloorReward>? DungeonFloorComplete;
    /// <summary>Dungeon-Buff-Auswahl anzeigen</summary>
    public event Action? DungeonBuffSelection;
    /// <summary>Dungeon-Run beendet (Zusammenfassung)</summary>
    public event Action<DungeonRunSummary>? DungeonRunEnd;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    public GameState State => _state;
    public int Score => _player?.Score ?? 0;
    public int Lives => _player?.Lives ?? 0;
    public int CurrentLevel => _currentLevelNumber;
    public float RemainingTime => _timer?.RemainingTime ?? 0;
    public bool IsDailyChallenge => _isDailyChallenge;
    public bool IsSurvivalMode => _isSurvivalMode;
    public bool IsQuickPlayMode => _isQuickPlayMode;
    public bool IsDungeonRun => _isDungeonRun;
    public bool IsTimeFreezeActive => _timeFreezeTimer > 0;
    public float TimeFreezeTimer => _timeFreezeTimer;
    public bool IsPhantomAvailable => _phantomWalkAvailable;
    public bool IsPhantomActive => _phantomWalkActive;
    public float PhantomWalkTimer => _phantomWalkTimer;
    public float PhantomCooldownTimer => _phantomCooldownTimer;
    public bool CanActivatePhantom => _phantomWalkAvailable && !_phantomWalkActive && _phantomCooldownTimer <= 0;
    public int SurvivalKills => _enemiesKilled;
    public float SurvivalTimeElapsed => _survivalTimeElapsed;

    /// <summary>Verbleibende aktive Gegner (für HUD-Anzeige)</summary>
    public int EnemiesRemaining
    {
        get
        {
            int count = 0;
            foreach (var e in _enemies)
                if (e.IsActive && !e.IsDying) count++;
            return count;
        }
    }
    public bool IsCurrentScoreHighScore => _highScoreService.IsHighScore(Score);

    /// <summary>Ob Continue möglich ist (nur Story, nur 1x pro Level-Versuch)</summary>
    public bool CanContinue => !_continueUsed && !_isDailyChallenge && !_isSurvivalMode && !_isQuickPlayMode && !_isDungeonRun;

    /// <summary>Verschiebung nach unten für Banner-Ad oben (Proxy für GameRenderer)</summary>
    public float BannerTopOffset
    {
        get => _renderer.BannerTopOffset;
        set => _renderer.BannerTopOffset = value;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INPUT FORWARDING
    // ═══════════════════════════════════════════════════════════════════════

    public void OnTouchStart(float x, float y, float screenWidth, float screenHeight, long pointerId = 0)
    {
        // Pause-Button prüfen (oben-links, nur wenn Android / Touch)
        if (_state == GameState.Playing && OperatingSystem.IsAndroid())
        {
            float pauseRight = PAUSE_BUTTON_MARGIN + PAUSE_BUTTON_SIZE;
            float pauseTop = PAUSE_BUTTON_MARGIN + BannerTopOffset;
            float pauseBottom = pauseTop + PAUSE_BUTTON_SIZE;
            if (x <= pauseRight + 10 && y >= pauseTop - 10 && y <= pauseBottom + 10)
            {
                PauseRequested?.Invoke();
                return;
            }
        }

        // Discovery-Hint: Tap zum Schließen
        if (_discoveryOverlay.IsActive && _state == GameState.Playing)
        {
            _discoveryOverlay.Dismiss();
            return;
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

        // Karten-Slot im HUD per Tap auswählen
        if (_state == GameState.Playing && _player?.EquippedCards.Count > 0)
        {
            int slotIndex = _renderer.HitTestCardSlot(x, y);
            if (slotIndex >= 0 && slotIndex < _player.EquippedCards.Count)
            {
                // Gleicher Slot nochmal → zurück auf Normal (-1)
                _player.ActiveCardSlot = _player.ActiveCardSlot == slotIndex ? -1 : slotIndex;
                return;
            }
        }

        _inputManager.OnTouchStart(x, y, screenWidth, screenHeight, pointerId);
    }

    public void OnTouchMove(float x, float y, long pointerId = 0)
        => _inputManager.OnTouchMove(x, y, pointerId);

    public void OnTouchEnd(long pointerId = 0)
        => _inputManager.OnTouchEnd(pointerId);

    public void OnKeyDown(Avalonia.Input.Key key)
        => _inputManager.OnKeyDown(key);

    public void OnKeyUp(Avalonia.Input.Key key)
        => _inputManager.OnKeyUp(key);

    public void OnGamepadButtonDown(Input.GamepadButton button)
        => _inputManager.OnGamepadButtonDown(button);

    public void OnGamepadButtonUp(Input.GamepadButton button)
        => _inputManager.OnGamepadButtonUp(button);

    public void SetAnalogStick(float x, float y)
        => _inputManager.SetAnalogStick(x, y);

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public GameEngine(
        SoundManager soundManager,
        IProgressService progressService,
        IHighScoreService highScoreService,
        InputManager inputManager,
        ILocalizationService localizationService,
        IGameStyleService gameStyleService,
        IShopService shopService,
        IPurchaseService purchaseService,
        GameRenderer renderer,
        ITutorialService tutorialService,
        IDiscoveryService discoveryService,
        IDungeonService dungeonService,
        IDungeonUpgradeService dungeonUpgradeService,
        IGameTrackingService tracking,
        IVibrationService vibrationService)
    {
        _soundManager = soundManager;
        _progressService = progressService;
        _highScoreService = highScoreService;
        _inputManager = inputManager;
        _localizationService = localizationService;
        _gameStyleService = gameStyleService;
        _shopService = shopService;
        _purchaseService = purchaseService;

        _renderer = renderer;
        _tutorialService = tutorialService;
        _discoveryService = discoveryService;
        _dungeonService = dungeonService;
        _dungeonUpgradeService = dungeonUpgradeService;
        _tracking = tracking;
        _vibration = vibrationService;
        _tutorialOverlay = new TutorialOverlay(localizationService);
        _discoveryOverlay = new DiscoveryOverlay(localizationService);
        _grid = new GameGrid();
        _timer = new GameTimer();
        _enemyAI = new EnemyAI(_grid);
        _player = new Player(0, 0);

        // Timer-Events abonnieren
        _timer.OnWarning += OnTimeWarning;
        _timer.OnExpired += OnTimeExpired;

        // Haptisches Feedback bei Richtungswechsel weiterleiten
        _directionChangedHandler = () => DirectionChanged?.Invoke();
        _inputManager.DirectionChanged += _directionChangedHandler;

        // HUD-Labels cachen und bei Sprachwechsel aktualisieren
        CacheHudLabels();
        _languageChangedHandler = (_, _) => CacheHudLabels();
        _localizationService.LanguageChanged += _languageChangedHandler;
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

    /// <summary>Score verdoppeln (nach Level-Complete Rewarded Ad) - nur Level-Anteil verdoppeln</summary>
    public void DoubleScore()
    {
        int levelScore = _player.Score - _scoreAtLevelStart;
        if (levelScore <= 0) return;
        _player.Score = (int)Math.Min((long)_player.Score + levelScore, int.MaxValue);
        ScoreChanged?.Invoke(_player.Score);
        CoinsEarned?.Invoke(levelScore, _player.Score, true);
    }

    /// <summary>
    /// Karten-Slot durchschalten: Normal → Slot 0 → Slot 1 → ... → Normal.
    /// Überspringt Slots ohne verbleibende Uses. Fällt auf Normal zurück
    /// wenn keine Karten mit Uses vorhanden sind.
    /// </summary>
    public void ToggleSpecialBomb()
    {
        if (_player == null) return;

        var cards = _player.EquippedCards;
        if (cards.Count == 0)
        {
            _player.ActiveCardSlot = -1;
            return;
        }

        // Ab aktuellem Slot weitersuchen (Slot -1 = Normal, dann 0, 1, 2, 3, zurück zu -1)
        int startSlot = _player.ActiveCardSlot;
        int totalSlots = cards.Count;

        for (int i = 0; i < totalSlots + 1; i++)
        {
            int nextSlot = startSlot + 1 + i;

            // Über die Karten-Slots hinaus → zurück auf Normal (-1)
            if (nextSlot >= totalSlots)
            {
                _player.ActiveCardSlot = -1;
                return;
            }

            // Slot mit verbleibenden Uses gefunden
            if (cards[nextSlot].HasUsesLeft)
            {
                _player.ActiveCardSlot = nextSlot;
                return;
            }
        }

        // Kein Slot mit Uses gefunden → Normal
        _player.ActiveCardSlot = -1;
    }

    /// <summary>
    /// Aktiviert Phantom-Walk (Dungeon Legendary-Buff): 5s durch Wände laufen, 30s Cooldown.
    /// </summary>
    public void ActivatePhantomWalk()
    {
        if (!CanActivatePhantom || _state != GameState.Playing) return;

        _playerHadWallpassBeforePhantom = _player.HasWallpass;
        _phantomWalkActive = true;
        _phantomWalkTimer = 5f;
        _player.HasWallpass = true;

        _floatingText.Spawn(_player.X, _player.Y - 16, "PHANTOM!",
            new SKColor(160, 32, 240), 14f, 1.0f);
        _soundManager.PlaySound(SoundManager.SFX_POWERUP);
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

        // ReducedEffects: ScreenShake, Partikel und atmosphärische Effekte deaktivieren
        bool reducedFx = _inputManager.ReducedEffects;
        _screenShake.Enabled = !reducedFx;
        _particleSystem.Enabled = !reducedFx;
        _renderer.ReducedEffects = reducedFx;

        _screenShake.Update(deltaTime);
        _particleSystem.Update(deltaTime);
        _floatingText.Update(deltaTime);
        _soundManager.Update(deltaTime);

        // Hit-Pause: Update wird übersprungen, Rendering läuft weiter (Freeze-Effekt)
        if (_hitPauseTimer > 0 && !reducedFx)
        {
            _hitPauseTimer -= deltaTime;
            return;
        }
        _hitPauseTimer = 0;

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
        // Discovery-Hint aktiv → Spiel pausiert, nur Overlay-Timer aktualisieren
        if (_discoveryOverlay.IsActive)
        {
            _discoveryOverlay.Update(deltaTime);
            return;
        }

        // Echtzeit-deltaTime speichern BEVOR Slow-Motion angewendet wird
        // Timer und Combo-Timer laufen in Echtzeit (kein Exploit durch Slow-Motion)
        float realDeltaTime = deltaTime;

        // Slow-Motion: deltaTime verlangsamen für dramatischen Effekt
        if (_slowMotionTimer > 0)
        {
            _slowMotionTimer = MathF.Max(0, _slowMotionTimer - realDeltaTime);
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

        // Gegner-Positions-Cache einmal pro Frame aufbauen (für Bomben-Slide-Kollision)
        _enemyPositionHashSet.Clear();
        foreach (var enemy in _enemies)
        {
            if (enemy.IsActive && !enemy.IsDying)
                _enemyPositionHashSet.Add((enemy.GridX, enemy.GridY));
        }

        // Dungeon TimeFreeze: Gegner eingefroren
        if (_timeFreezeTimer > 0)
            _timeFreezeTimer -= realDeltaTime;

        // Dungeon Phantom-Walk Timer aktualisieren
        if (_phantomWalkActive)
        {
            _phantomWalkTimer -= realDeltaTime;
            if (_phantomWalkTimer <= 0)
            {
                _phantomWalkActive = false;
                _phantomCooldownTimer = 30f;
                // Wallpass nur zurücksetzen wenn Spieler es nicht unabhängig hat
                if (!_playerHadWallpassBeforePhantom)
                    _player.HasWallpass = false;
            }
        }
        else if (_phantomCooldownTimer > 0)
        {
            _phantomCooldownTimer -= realDeltaTime;
        }

        UpdatePlayer(deltaTime);
        _inputManager.Update(deltaTime);
        UpdateBombs(deltaTime);
        UpdateExplosions(deltaTime);
        UpdateDestroyingBlocks(deltaTime);
        UpdateSpecialBombEffects(deltaTime);
        UpdatePendingIceCleanups(realDeltaTime);

        // Gegner nur bewegen wenn nicht durch TimeFreeze eingefroren
        if (_timeFreezeTimer <= 0)
            UpdateEnemies(deltaTime);

        UpdateBossAttacks(deltaTime);
        UpdatePowerUps(deltaTime);
        UpdateWorldMechanics(deltaTime);

        // Poison-Schaden-Cooldown dekrementieren (Echtzeit)
        if (_poisonDamageTimer > 0)
            _poisonDamageTimer -= realDeltaTime;

        // Festungs-Synergy: Shield regeneriert nach 20s ohne Schaden
        if (_synergyFortressActive && _isDungeonRun && !_player.HasShield && !_player.IsDying)
        {
            _fortressRegenTimer += realDeltaTime;
            if (_fortressRegenTimer >= 20f)
            {
                _player.HasShield = true;
                _fortressRegenTimer = 0;
                _floatingText.Spawn(_player.X, _player.Y - 16, "SHIELD!",
                    new SKColor(0, 229, 255), 14f, 1.0f);
                _particleSystem.Emit(_player.X, _player.Y, 8,
                    new SKColor(0, 229, 255), 60f, 0.5f);
            }
        }

        // Floor-Modifikator Regeneration: Shield nach 15s ohne Schaden
        if (_dungeonFloorModifier == DungeonFloorModifier.Regeneration && _isDungeonRun
            && !_player.HasShield && !_player.IsDying)
        {
            _dungeonModifierRegenTimer += realDeltaTime;
            if (_dungeonModifierRegenTimer >= 15f)
            {
                _player.HasShield = true;
                _dungeonModifierRegenTimer = 0;
                _floatingText.Spawn(_player.X, _player.Y - 16, "REGEN!",
                    new SKColor(76, 175, 80), 14f, 1.0f);
                _particleSystem.Emit(_player.X, _player.Y, 8,
                    new SKColor(76, 175, 80), 60f, 0.5f);
            }
        }

        CheckCollisions();
        CleanupEntities();

        // Pontan-Strafe (gestaffeltes Spawning nach Timer-Ablauf)
        if (_pontanPunishmentActive)
            UpdatePontanPunishment(realDeltaTime);

        // Survival-Modus: Kontinuierliches Gegner-Spawning
        if (_isSurvivalMode)
            UpdateSurvivalSpawning(realDeltaTime);

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

        // Achievement: Curse-Ende erkennen (vor Update cursed, nach Update nicht mehr)
        var curseBeforeUpdate = _player.IsCursed ? _player.ActiveCurse : CurseType.None;
        _player.Update(deltaTime);
        if (curseBeforeUpdate != CurseType.None && !_player.IsCursed)
        {
            _tracking.OnCurseSurvived(curseBeforeUpdate);
        }

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
                InvalidateEnemyPaths();
            }
            _player.DiarrheaTimer = 0.5f;
        }

        // Bombe platzieren
        if (_inputManager.BombPressed && _player.CanPlaceBomb())
        {
            PlaceBomb();
            // Gegner-Pfade invalidieren → sofortige Neuberechnung (Bombe blockiert Weg)
            InvalidateEnemyPaths();
            // Tutorial: Bomben-Schritt als abgeschlossen markieren
            _tutorialService.CheckStepCompletion(TutorialStepType.PlaceBomb);
        }

        // Manuelle Detonation
        if (_inputManager.DetonatePressed && _player.HasDetonator)
        {
            DetonateAllBombs();
            _tracking.OnDetonatorUsed();
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

        // Achievement: Bomben-Kick zählen
        _tracking.OnBombKicked();
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

                // Trost-Coins (Level-Score ÷ 6, abgerundet)
                int coins = (_player.Score - _scoreAtLevelStart) / 6;
                if (_purchaseService.IsPremium)
                    coins *= 3;
                if (coins > 0)
                {
                    CoinsEarned?.Invoke(coins, _player.Score, false);
                }

                // Survival-Runde beendet (Achievement + BattlePass)
                if (_isSurvivalMode)
                    _tracking.OnSurvivalEnded(_survivalTimeElapsed, _enemiesKilled);

                // Dungeon-Run beenden bei Tod
                if (_isDungeonRun)
                {
                    _tracking.OnDungeonRunCompleted();
                    var summary = _dungeonService.EndRun();
                    _isDungeonRun = false;
                    DungeonRunEnd?.Invoke(summary);
                }

                _tracking.FlushIfDirty();
                GameOver?.Invoke();
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
            // Afterglow-Zellen registrieren
            foreach (var eCell in explosion.AffectedCells)
            {
                var gc = _grid.TryGetCell(eCell.X, eCell.Y);
                if (gc != null && gc.AfterglowTimer > 0)
                    _afterglowCells.Add(gc);
            }
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
            case WorldMechanic.FallingCeiling:
                UpdateFallingCeilingMechanic(deltaTime);
                break;
            case WorldMechanic.Current:
                UpdateCurrentMechanic(deltaTime);
                break;
            case WorldMechanic.Earthquake:
                UpdateEarthquakeMechanic(deltaTime);
                break;
            case WorldMechanic.PlatformGap:
                UpdatePlatformGapMechanic(deltaTime);
                break;
            case WorldMechanic.Fog:
                // Fog hat keine Update-Logik, nur Render-Einschränkung
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

                // 4-Ecken-Kollisionsprüfung (wie Player.CanMoveTo)
                float newX = _player.X + dx;
                float newY = _player.Y + dy;
                float halfSize = GameGrid.CELL_SIZE * 0.35f;
                if (CollisionHelper.CanMoveToPlayer(newX, newY, halfSize, _grid, _player.HasWallpass, _player.HasBombpass))
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

        // Spieler auf Förderband (4-Ecken-Kollisionsprüfung)
        var playerCell = _grid.TryGetCell(_player.GridX, _player.GridY);
        if (playerCell?.Type == CellType.Conveyor)
        {
            float dx = playerCell.ConveyorDirection.GetDeltaX() * conveyorSpeed * deltaTime;
            float dy = playerCell.ConveyorDirection.GetDeltaY() * conveyorSpeed * deltaTime;

            float newX = _player.X + dx;
            float newY = _player.Y + dy;
            float halfSize = GameGrid.CELL_SIZE * 0.35f;
            if (CollisionHelper.CanMoveToPlayer(newX, newY, halfSize, _grid, _player.HasWallpass, _player.HasBombpass))
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
        // Teleporter-Cooldowns aktualisieren (gecachte Zellen statt Grid-Scan)
        foreach (var cell in _mechanicCells)
        {
            if (cell.Type == CellType.Teleporter && cell.TeleporterCooldown > 0)
                cell.TeleporterCooldown -= deltaTime;
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
        // Gecachte Lava-Zellen statt 150-Zellen-Grid-Scan
        foreach (var cell in _mechanicCells)
        {
                if (cell.Type != CellType.LavaCrack) continue;

                cell.LavaCrackTimer += deltaTime;
                int x = cell.X;
                int y = cell.Y;

                // Spieler-Schaden bei aktivem Lava-Riss
                if (cell.IsLavaCrackActive && _player.GridX == x && _player.GridY == y)
                {
                    if (!_player.IsInvincible && !_player.HasSpawnProtection && !_player.IsDying)
                    {
                        if (_player.HasShield)
                        {
                            AbsorbShield(new SKColor(255, 80, 0), particleCount: 12, spread: 60f, playSound: false);
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

    // ═══════════════════════════════════════════════════════════════════════
    // NEUE WELT-MECHANIKEN (FallingCeiling, Current, Earthquake, PlatformGap)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fallende Decke: Nach 60s fallen zufällige leere Zellen als Blöcke herab, danach alle 15s
    /// </summary>
    private float _fallingCeilingTimer;
    private void UpdateFallingCeilingMechanic(float deltaTime)
    {
        _fallingCeilingTimer += deltaTime;
        if (_fallingCeilingTimer >= 60f)
        {
            _fallingCeilingTimer -= 15f; // Alle 15s danach wiederholen

            // 2-3 zufällige leere Zellen werden zu Blöcken
            int count = _pontanRandom.Next(2, 4);
            for (int i = 0; i < count; i++)
            {
                int attempts = 20;
                while (attempts-- > 0)
                {
                    int gx = _pontanRandom.Next(1, _grid.Width - 1);
                    int gy = _pontanRandom.Next(1, _grid.Height - 1);
                    var cell = _grid.TryGetCell(gx, gy);
                    if (cell != null && cell.Type == CellType.Empty && cell.Bomb == null &&
                        !(gx == _player.GridX && gy == _player.GridY))
                    {
                        cell.Type = CellType.Block;
                        // Warn-Partikel am Aufschlagort
                        float px = gx * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                        float py = gy * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                        _particleSystem.Emit(px, py, 8, new SKColor(139, 119, 101), 60f, 0.4f);
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Strömung: Spieler wird langsam in eine Richtung geschoben (Ozean-Welt)
    /// </summary>
    private void UpdateCurrentMechanic(float deltaTime)
    {
        // Strömung nach rechts (langsamer als Conveyor)
        float currentSpeed = 20f * deltaTime;
        float newX = _player.X + currentSpeed;
        float halfSize = GameGrid.CELL_SIZE * 0.35f;
        if (CollisionHelper.CanMoveToPlayer(newX, _player.Y, halfSize, _grid, _player.HasWallpass, _player.HasBombpass))
        {
            _player.X = newX;
        }
    }

    /// <summary>
    /// Erdbeben: Alle 30s verschieben sich einige Blöcke zufällig + ScreenShake
    /// </summary>
    private float _earthquakeTimer;
    private void UpdateEarthquakeMechanic(float deltaTime)
    {
        _earthquakeTimer += deltaTime;
        if (_earthquakeTimer >= 30f)
        {
            _earthquakeTimer = 0;
            _screenShake.Trigger(4f, 0.5f);

            // 3-5 zufällige Blöcke verschieben
            int count = _pontanRandom.Next(3, 6);
            for (int i = 0; i < count; i++)
            {
                // Zufälligen Block finden
                int attempts = 30;
                while (attempts-- > 0)
                {
                    int gx = _pontanRandom.Next(1, _grid.Width - 1);
                    int gy = _pontanRandom.Next(1, _grid.Height - 1);
                    var cell = _grid.TryGetCell(gx, gy);
                    if (cell?.Type != CellType.Block) continue;

                    // Zufällige Richtung
                    var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
                    var (dx, dy) = dirs[_pontanRandom.Next(dirs.Length)];
                    int nx = gx + dx, ny = gy + dy;
                    var target = _grid.TryGetCell(nx, ny);
                    if (target != null && target.Type == CellType.Empty && target.Bomb == null &&
                        !(nx == _player.GridX && ny == _player.GridY))
                    {
                        // Block verschieben, Hidden-Exit mitnehmen
                        target.Type = CellType.Block;
                        target.HasHiddenExit = cell.HasHiddenExit;
                        cell.Type = CellType.Empty;
                        cell.HasHiddenExit = false;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Plattform-Lücken: Spieler stirbt bei Betreten einer PlatformGap-Zelle
    /// </summary>
    private void UpdatePlatformGapMechanic(float deltaTime)
    {
        var cell = _grid.TryGetCell(_player.GridX, _player.GridY);
        if (cell?.Type == CellType.PlatformGap && !_player.IsDying && !_player.IsInvincible && !_player.HasSpawnProtection)
        {
            KillPlayer();
            _floatingText.Spawn(_player.X, _player.Y - 16, "FALL!", SKColors.Red, 16f, 1.5f);
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

    /// <summary>
    /// Alle Gegner-AI-Timer auf 0 setzen und Pfad-Cache leeren.
    /// Erzwingt sofortige Neuberechnung bei nächstem Update (z.B. nach Block-Zerstörung).
    /// </summary>
    private void InvalidateEnemyPaths()
    {
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || enemy.IsDying)
                continue;

            enemy.AIDecisionTimer = 0;
            enemy.Path.Clear();
            enemy.TargetPosition = null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISCOVERY HINTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Discovery-Hint fuer eine ID prüfen und ggf. anzeigen.
    /// Markiert das Item als entdeckt und zeigt Overlay bei Erstentdeckung.
    /// </summary>
    private void TryShowDiscoveryHint(string discoveryId)
    {
        var hintKey = _discoveryService.GetDiscoveryTitleKey(discoveryId);
        if (hintKey != null)
        {
            var descKey = _discoveryService.GetDiscoveryDescKey(discoveryId) ?? hintKey;
            ShowDiscoveryHint(hintKey, descKey);
        }
    }

    /// <summary>
    /// Discovery-Hint anzeigen (pausiert das Spiel bis Tap oder Auto-Dismiss)
    /// </summary>
    private void ShowDiscoveryHint(string titleKey, string descKey)
    {
        // Kein Hint wenn schon einer aktiv ist oder Tutorial läuft
        if (_discoveryOverlay.IsActive || _tutorialService.IsActive)
            return;

        _discoveryOverlay.Show(titleKey, descKey);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BOSS-SPEZIAL-ANGRIFFE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Boss-Spezial-Angriffe: Telegraph → Angriff → Effekt.
    /// Wird nach UpdateEnemies aufgerufen, damit BossEnemy.Update() bereits Timer aktualisiert hat.
    /// </summary>
    private void UpdateBossAttacks(float deltaTime)
    {
        foreach (var enemy in _enemies)
        {
            if (enemy is not BossEnemy boss || !boss.IsActive || boss.IsDying)
                continue;

            // Telegraph gerade gestartet (SpecialAttackTimer ist auf 0 gefallen → TelegraphTimer wurde gesetzt)
            // AttackTargetCells berechnen wenn Telegraph beginnt und noch keine Zellen gesetzt sind
            if (boss.IsTelegraphing && !boss.IsAttacking && boss.AttackTargetCells.Count == 0)
            {
                CalculateBossAttackTargets(boss);

                // Leichter ScreenShake als Warnung
                _screenShake.Trigger(1.5f, 0.2f);
                _soundManager.PlaySound(SoundManager.SFX_TIME_WARNING);
            }

            // Angriff gerade gestartet (IsAttacking true, AttackDuration noch fast voll)
            if (boss.IsAttacking && boss.AttackDuration > 1.4f)
            {
                ExecuteBossAttack(boss);
            }
        }
    }

    /// <summary>
    /// AttackTargetCells für den Boss-Spezial-Angriff berechnen (Telegraph-Phase)
    /// </summary>
    private void CalculateBossAttackTargets(BossEnemy boss)
    {
        // FinalBoss rotiert durch alle 4 Angriffstypen
        var attackType = boss.BossKind;
        if (boss.BossKind == BossType.FinalBoss)
        {
            attackType = boss.AttackRotationIndex switch
            {
                0 => BossType.StoneGolem,
                1 => BossType.IceDragon,
                2 => BossType.FireDemon,
                3 => BossType.ShadowMaster,
                _ => BossType.StoneGolem
            };
        }

        boss.AttackTargetCells.Clear();

        switch (attackType)
        {
            case BossType.StoneGolem:
                CalculateStoneGolemTargets(boss);
                break;
            case BossType.IceDragon:
                CalculateIceDragonTargets(boss);
                break;
            case BossType.FireDemon:
                CalculateFireDemonTargets(boss);
                break;
            case BossType.ShadowMaster:
                // Teleport hat keine Ziel-Zellen (kein Schaden)
                break;
        }
    }

    /// <summary>
    /// StoneGolem: 3-4 zufällige leere Zellen werden zu Blöcken
    /// </summary>
    private void CalculateStoneGolemTargets(BossEnemy boss)
    {
        int count = _pontanRandom.Next(3, 5);
        int attempts = 40;
        while (boss.AttackTargetCells.Count < count && attempts-- > 0)
        {
            int gx = _pontanRandom.Next(1, _grid.Width - 1);
            int gy = _pontanRandom.Next(1, _grid.Height - 1);
            var cell = _grid.TryGetCell(gx, gy);
            if (cell != null && cell.Type == CellType.Empty && cell.Bomb == null &&
                !boss.OccupiesCell(gx, gy))
            {
                boss.AttackTargetCells.Add((gx, gy));
            }
        }
    }

    /// <summary>
    /// IceDragon: Zufällige horizontale Reihe wird eingefroren
    /// </summary>
    private void CalculateIceDragonTargets(BossEnemy boss)
    {
        // Zufällige Reihe (nicht die äußersten Ränder)
        int row = _pontanRandom.Next(1, _grid.Height - 1);
        for (int x = 1; x < _grid.Width - 1; x++)
        {
            var cell = _grid.TryGetCell(x, row);
            if (cell != null && (cell.Type == CellType.Empty || cell.Type == CellType.Ice))
            {
                boss.AttackTargetCells.Add((x, row));
            }
        }
    }

    /// <summary>
    /// FireDemon: Obere oder untere Hälfte des Bodens wird gefährlich
    /// </summary>
    private void CalculateFireDemonTargets(BossEnemy boss)
    {
        // Zufällig obere oder untere Hälfte
        bool upperHalf = _pontanRandom.Next(2) == 0;
        int startY = upperHalf ? 1 : _grid.Height / 2;
        int endY = upperHalf ? _grid.Height / 2 : _grid.Height - 1;

        for (int y = startY; y < endY; y++)
        {
            for (int x = 1; x < _grid.Width - 1; x++)
            {
                var cell = _grid.TryGetCell(x, y);
                if (cell != null && cell.Type == CellType.Empty)
                {
                    boss.AttackTargetCells.Add((x, y));
                }
            }
        }
    }

    /// <summary>
    /// Boss-Angriff ausführen (einmalig beim Wechsel von Telegraph → Angriff)
    /// </summary>
    private void ExecuteBossAttack(BossEnemy boss)
    {
        // Flag setzen damit Angriff nur einmal ausgeführt wird
        // (AttackDuration sinkt unter 1.4f nach dem ersten Frame)

        var attackType = boss.BossKind;
        if (boss.BossKind == BossType.FinalBoss)
        {
            attackType = boss.AttackRotationIndex switch
            {
                0 => BossType.StoneGolem,
                1 => BossType.IceDragon,
                2 => BossType.FireDemon,
                3 => BossType.ShadowMaster,
                _ => BossType.StoneGolem
            };
        }

        switch (attackType)
        {
            case BossType.StoneGolem:
                ExecuteStoneGolemAttack(boss);
                break;
            case BossType.IceDragon:
                ExecuteIceDragonAttack(boss);
                break;
            case BossType.FireDemon:
                ExecuteFireDemonAttack(boss);
                break;
            case BossType.ShadowMaster:
                ExecuteShadowMasterAttack(boss);
                break;
        }
    }

    /// <summary>
    /// StoneGolem-Angriff: Ziel-Zellen werden zu Blöcken (wie FallingCeiling)
    /// </summary>
    private void ExecuteStoneGolemAttack(BossEnemy boss)
    {
        foreach (var (gx, gy) in boss.AttackTargetCells)
        {
            var cell = _grid.TryGetCell(gx, gy);
            if (cell != null && cell.Type == CellType.Empty && cell.Bomb == null)
            {
                cell.Type = CellType.Block;

                // Aufschlag-Partikel
                float px = gx * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                float py = gy * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                _particleSystem.Emit(px, py, 8, new SKColor(139, 119, 101), 80f, 0.5f);
            }
        }

        _screenShake.Trigger(4f, 0.3f);
        _floatingText.Spawn(boss.X, boss.Y - 24, "BLOCKREGEN!",
            new SKColor(180, 140, 90), 18f, 1.5f);
        _soundManager.PlaySound(SoundManager.SFX_EXPLOSION);
    }

    /// <summary>
    /// IceDragon-Angriff: Reihe wird temporär zu Eis (3s Slow-Effekt für Spieler)
    /// </summary>
    private void ExecuteIceDragonAttack(BossEnemy boss)
    {
        foreach (var (gx, gy) in boss.AttackTargetCells)
        {
            var cell = _grid.TryGetCell(gx, gy);
            if (cell != null && cell.Type == CellType.Empty)
            {
                // Temporär zu Eis umwandeln (Mechanik-Zellen cachen)
                cell.Type = CellType.Ice;

                float px = gx * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                float py = gy * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                _particleSystem.Emit(px, py, 4, new SKColor(100, 200, 255), 50f, 0.3f);
            }
        }

        _screenShake.Trigger(3f, 0.2f);
        _floatingText.Spawn(boss.X, boss.Y - 24, "EISATEM!",
            new SKColor(100, 200, 255), 18f, 1.5f);
        _soundManager.PlaySound(SoundManager.SFX_EXPLOSION);

        // Eis nach 3s wieder entfernen (Frame-basierter Timer statt Task.Delay → Thread-sicher)
        _pendingIceCleanups.Add((boss.AttackTargetCells.ToList(), 3f));
    }

    /// <summary>
    /// FireDemon-Angriff: Halber Boden wird Lava (Schaden über AttackTargetCells in CheckCollisions)
    /// </summary>
    private void ExecuteFireDemonAttack(BossEnemy boss)
    {
        foreach (var (gx, gy) in boss.AttackTargetCells)
        {
            float px = gx * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float py = gy * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            _particleSystem.Emit(px, py, 2, new SKColor(255, 80, 0), 40f, 0.3f);
        }

        _screenShake.Trigger(4f, 0.3f);
        _floatingText.Spawn(boss.X, boss.Y - 24, "LAVA-WELLE!",
            new SKColor(255, 100, 0), 18f, 1.5f);
        _soundManager.PlaySound(SoundManager.SFX_EXPLOSION);
    }

    /// <summary>
    /// ShadowMaster-Angriff: Boss teleportiert sich zu zufälliger Position
    /// </summary>
    private void ExecuteShadowMasterAttack(BossEnemy boss)
    {
        // Partikel am alten Standort
        _particleSystem.Emit(boss.X, boss.Y, 12, new SKColor(100, 0, 180), 80f, 0.5f);

        // Zufällige neue Position suchen (mit genug Platz für BossSize)
        int attempts = 30;
        while (attempts-- > 0)
        {
            int gx = _pontanRandom.Next(2, _grid.Width - boss.BossSize - 1);
            int gy = _pontanRandom.Next(2, _grid.Height - boss.BossSize - 1);

            // Prüfen ob alle Zellen frei sind
            bool canPlace = true;
            for (int dy = 0; dy < boss.BossSize && canPlace; dy++)
                for (int dx = 0; dx < boss.BossSize && canPlace; dx++)
                {
                    var cell = _grid.TryGetCell(gx + dx, gy + dy);
                    if (cell == null || cell.Type == CellType.Wall || cell.Type == CellType.Block || cell.Bomb != null)
                        canPlace = false;
                }

            // Nicht direkt auf den Spieler teleportieren
            if (canPlace && Math.Abs(gx - _player.GridX) + Math.Abs(gy - _player.GridY) > 3)
            {
                float newX = gx * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE * boss.BossSize / 2f;
                float newY = gy * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE * boss.BossSize / 2f;
                boss.X = newX;
                boss.Y = newY;

                // Partikel am neuen Standort
                _particleSystem.Emit(boss.X, boss.Y, 12, new SKColor(150, 0, 255), 80f, 0.5f);
                break;
            }
        }

        _screenShake.Trigger(5f, 0.3f);
        _floatingText.Spawn(boss.X, boss.Y - 24, "TELEPORT!",
            new SKColor(150, 0, 255), 18f, 1.5f);
        _soundManager.PlaySound(SoundManager.SFX_ENEMY_DEATH);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ICE CLEANUP (Frame-basiert statt Task.Delay)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ausstehende Eis-Cleanups pro Frame aktualisieren (Timer dekrementieren, bei 0 → CellType.Empty)
    /// </summary>
    private void UpdatePendingIceCleanups(float deltaTime)
    {
        for (int i = _pendingIceCleanups.Count - 1; i >= 0; i--)
        {
            var (cells, timer) = _pendingIceCleanups[i];
            timer -= deltaTime;

            if (timer <= 0)
            {
                // Eis-Zellen zurücksetzen
                foreach (var (gx, gy) in cells)
                {
                    var cell = _grid.TryGetCell(gx, gy);
                    if (cell != null && cell.Type == CellType.Ice)
                        cell.Type = CellType.Empty;
                }
                _pendingIceCleanups.RemoveAt(i);
            }
            else
            {
                _pendingIceCleanups[i] = (cells, timer);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer.OnWarning -= OnTimeWarning;
        _timer.OnExpired -= OnTimeExpired;
        _inputManager.DirectionChanged -= _directionChangedHandler;
        _localizationService.LanguageChanged -= _languageChangedHandler;

        _overlayBgPaint.Dispose();
        _overlayTextPaint.Dispose();
        _overlayFont.Dispose();
        _overlayGlowFilter.Dispose();
        _overlayGlowFilterLarge.Dispose();
        _particleSystem.Dispose();
        _floatingText.Dispose();
        _tutorialOverlay.Dispose();
        _discoveryOverlay.Dispose();
        _inputManager.Dispose();
        _irisClipPath.Dispose();
        _starPath.Dispose();
    }
}
