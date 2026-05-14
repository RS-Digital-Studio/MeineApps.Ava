using BomberBlast.AI;
using BomberBlast.Core.LevelGeneration;
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
using Microsoft.Extensions.Logging;
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
public sealed partial class GameEngine : IDisposable
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
    private readonly ILevelGenerator _levelGenerator;
    private readonly GameRenderer _renderer;
    private readonly ITutorialService _tutorialService;
    private readonly TutorialOverlay _tutorialOverlay;
    private readonly IDiscoveryService _discoveryService;
    private readonly IDungeonService _dungeonService;
    private readonly IDungeonUpgradeService _dungeonUpgradeService;
    private readonly IGameTrackingService _tracking;
    private readonly IDeckTelemetryService _deckTelemetry;
    private readonly IMasterModeService _masterModeService;
    private readonly ILoadoutService _loadoutService;
    private readonly IBossRushService _bossRushService;
    private readonly ILeagueService _leagueService;
    private readonly IEventService _eventService;
    private readonly IVibrationService _vibration;
    private readonly ILogger<GameEngine> _logger;
    // v2.0.44 — : Accessibility-ColorMatrix wird vor jedem Frame ausgewertet
    private readonly IAccessibilityService _accessibility;
    // v2.0.44 — : Funnel-Tracking
    private readonly IAnalyticsService _analytics;
    // v2.0.45 — : Performance-Telemetry (FPS-Bucket für Crashlytics-Custom-Keys)
    private readonly ITelemetryService _telemetry;
    /// <summary>.1 : Hero/Character-Service fuer Stat-Anwendung beim Spawn.</summary>
    private readonly IHeroService _heroService;
    /// <summary>.2 : Multiplayer-Session-Service fuer 2P-Co-Op-Toggle.</summary>
    private readonly IMultiplayerSessionService _multiplayerSession;
    // Phase 24b — Optional Retention-Service (für First-Win-Cinematic-Trigger).
    // Kein Constructor-Param damit DI-Chain nicht angefasst werden muss; via Property-Injection
    // vom App-Layer gesetzt nach GameEngine-Construction.
    public IRetentionService? RetentionService { get; set; }

    /// <summary>
    ///.2 : Mini-Story-Beats-Service fuer Welt-Intro/Outro-Cutscenes.
    /// Property-Injection (kein Constructor-Param) — GameViewModel setzt es nach Construction.
    /// </summary>
    public IWorldStoryService? WorldStoryService { get; set; }

    // === Phase 18c — FixedTimestep als opt-in Engine-Mode ===================
    // Pragmatischer Ansatz: Wenn FixedTimestepEnabled=true, wird UpdatePlaying mehrmals pro Wall-Clock-Frame
    // mit FIXED_TICK_SECONDS (16.67ms) aufgerufen statt einmal mit variable deltaTime. Das gibt
    // deterministische Sim-Granularität ohne Engine-Refactor. Default off.
    private readonly FixedTimestepRunner _fixedTimestep = new();
    public bool FixedTimestepEnabled
    {
        get => _fixedTimestep.Enabled;
        set
        {
            _fixedTimestep.Enabled = value;
            // Phase 18d — RNG-Provider entsprechend wechseln
            // Variable-Mode → System.Random (Bestand-Verhalten, schnell)
            // Fixed-Mode → DeterministicRandom (plattform-unabhängig deterministisch)
            _rngProvider = value
                ? new DeterministicRngProvider((ulong)DateTime.UtcNow.Ticks)
                : new SystemRngProvider();
        }
    }
    public float FixedTimestepInterpolationAlpha => _fixedTimestep.GetInterpolationAlpha();

    /// <summary>
    /// Phase 18d — RNG-Provider. Default System.Random; im FixedTimestep-Mode DeterministicRandom.
    /// Public read-only damit Engine-interne Subsysteme (LevelGenerator, EnemyAI) sich darauf verlassen können
    /// (Foundation — Hot-Path-Migration aller engine-internen Random-Calls in Phase 18e).
    /// </summary>
    private IRngProvider _rngProvider = new SystemRngProvider();
    public IRngProvider RngProvider => _rngProvider;

    /// <summary>
    /// Setzt einen expliziten Seed für deterministische Replays. Aktiviert automatisch FixedTimestep
    /// und DeterministicRandom-Provider. Wird von Replay-Playback / Anti-Cheat-Verifikation aufgerufen.
    /// </summary>
    public void SetDeterministicSeed(ulong seed)
    {
        _fixedTimestep.Enabled = true;
        _rngProvider = new DeterministicRngProvider(seed);
    }

    // Phase 18e — Engine-internal RNG-Helpers. Im Fixed-Mode geht alles über RngProvider
    // (deterministisch). Im Variable-Mode wird System.Random (_pontanRandom) genutzt für
    // Bestand-Verhalten. Aufrufstellen müssen die Helpers statt _pontanRandom direkt verwenden,
    // wenn sie sim-relevant sind (Pontan-Spawn, Enemy-Spawn-Direction etc.).
    private int EngineRngNext(int max)
        => _fixedTimestep.Enabled ? _rngProvider.Next(max) : _pontanRandom.Next(max);
    private int EngineRngNext(int min, int max)
        => _fixedTimestep.Enabled ? _rngProvider.Next(min, max) : _pontanRandom.Next(min, max);
    private double EngineRngNextDouble()
        => _fixedTimestep.Enabled ? _rngProvider.NextDouble() : _pontanRandom.NextDouble();

    // === Phase 30b — 2P-Co-Op-Engine-Foundation =============================
    // Optionaler Player2 für Co-Op/Versus. Default null (Single-Player).
    // Spawning + Input-Routing + Game-Over-Logic werden in Phase 30c verkabelt.
    private Player? _player2;

    /// <summary>Phase 30b — Aktiver Multiplayer-Modus. Default Single.</summary>
    public BomberBlast.Core.Multiplayer.MultiplayerMode MultiplayerMode { get; private set; }
        = BomberBlast.Core.Multiplayer.MultiplayerMode.Single;

    /// <summary>Phase 30b — Player 2 (null im Single-Player-Modus).</summary>
    public Player? Player2 => _player2;

    /// <summary>
    /// Phase 30b — Aktiviert 2P-Modus. Erzeugt Player2 an gegenüberliegender Spawn-Position.
    /// Idempotent — wenn schon aktiv, no-op. Nur vor Level-Start aufrufen.
    /// </summary>
    public void EnableMultiplayer(BomberBlast.Core.Multiplayer.MultiplayerMode mode)
    {
        if (mode == BomberBlast.Core.Multiplayer.MultiplayerMode.Single)
        {
            DisableMultiplayer();
            return;
        }
        MultiplayerMode = mode;
        if (_player2 == null)
        {
            var spawn = BomberBlast.Core.Multiplayer.MultiplayerSpawnPositions.Player2;
            _player2 = new Player(
                spawn.x * BomberBlast.Models.Grid.GameGrid.CELL_SIZE
                    + BomberBlast.Models.Grid.GameGrid.CELL_SIZE / 2f,
                spawn.y * BomberBlast.Models.Grid.GameGrid.CELL_SIZE
                    + BomberBlast.Models.Grid.GameGrid.CELL_SIZE / 2f);
        }
    }

    /// <summary>Phase 30b — Schaltet 2P-Modus aus + entfernt Player2.</summary>
    public void DisableMultiplayer()
    {
        MultiplayerMode = BomberBlast.Core.Multiplayer.MultiplayerMode.Single;
        _player2 = null;
    }

    /// <summary>
    /// Phase 30b — Game-Over-Logik für 2P-Co-Op: erst wenn BEIDE Spieler tot sind.
    /// </summary>
    public bool IsCoOpGameOver()
    {
        if (MultiplayerMode != BomberBlast.Core.Multiplayer.MultiplayerMode.LocalCoop) return false;
        if (_player2 == null) return false;
        return _player.Lives <= 0 && _player2.Lives <= 0;
    }

    /// <summary>
    /// Phase 30c — Player-2-Update pro Sim-Tick. Wird von UpdatePlayer für Player 2
    /// aufgerufen wenn Multiplayer aktiv ist. Pragmatisch: Bewegung, Cell-Snap, Animation.
    /// Bomb-Place + Detonate kommen über separate Player2-Input-Snapshot-Setter.
    /// </summary>
    public void UpdatePlayer2Movement(float deltaTime)
    {
        if (_player2 == null) return;
        if (!_player2.IsActive) return;
        if (_player2.IsDying)
        {
            _player2.Update(deltaTime);
            return;
        }
        _player2.Update(deltaTime);
        _player2.Move(deltaTime, _grid);
    }

    /// <summary>
    /// Phase 30c — Setzt die Bewegungsrichtung für Player 2 (vom Dual-Input-Layer aufgerufen).
    /// </summary>
    public void SetPlayer2Direction(BomberBlast.Models.Entities.Direction direction)
    {
        if (_player2 == null) return;
        _player2.MovementDirection = direction;
    }

    /// <summary>
    /// Phase 30c+e — Player 2 platziert eine Bombe (vom Dual-Input-Layer aufgerufen).
    /// </summary>
    public bool TryPlaceBombPlayer2()
    {
        if (_player2 == null) return false;
        if (!_player2.CanPlaceBomb()) return false;
        PlaceBombForOwnerInternal(_player2);
        return true;
    }

    /// <summary>
    /// Phase 30e — Internal-Wrapper für PlaceBombForOwner damit der private Pfad
    /// auch von der public TryPlaceBombPlayer2-API erreichbar ist.
    /// </summary>
    private void PlaceBombForOwnerInternal(BomberBlast.Models.Entities.Player owner)
        => PlaceBombForOwner(owner);

    /// <summary>
    /// Phase 30c — Liefert beide Spieler-Snapshots als InputBuffer-Eintrag (für Replay/Sync).
    /// </summary>
    public BomberBlast.Core.Multiplayer.PlayerInputSnapshot GetPlayer1InputSnapshot()
        => new(BomberBlast.Core.Multiplayer.PlayerSlot.Player1,
            _inputManager.MovementDirection, _inputManager.BombPressed, _inputManager.DetonatePressed);

    /// <summary>
    /// Phase 30d — Liefert den Spieler dem der Score zugewiesen wird basierend auf der
    /// Source-Bomb. Im Single-Player-Modus immer Player 1. Im Co-Op-Modus der Bomb-Owner
    /// (Player 1 oder Player 2). Bei null/unbekannt: Player 1 als Default.
    /// </summary>
    internal BomberBlast.Models.Entities.Player ResolveScoringPlayer(BomberBlast.Models.Entities.Bomb? sourceBomb)
    {
        if (MultiplayerMode == BomberBlast.Core.Multiplayer.MultiplayerMode.Single) return _player;
        if (sourceBomb == null) return _player;
        if (_player2 != null && ReferenceEquals(sourceBomb.Owner, _player2)) return _player2;
        return _player;
    }

    /// <summary>
    /// Phase 30d — Co-Op-Combined-Score (P1 + P2).
    /// In Versus-Mode: getrennte Scores. In Co-Op: Summe wird als Team-Score angezeigt.
    /// </summary>
    public int CombinedScore => _player.Score + (_player2?.Score ?? 0);

    // FPS-Tracking: Wall-Clock Sample-Buffer (5s Window, ~150 Samples bei 30 FPS)
    // Audit M12: Initial-Capacity 400 verhindert Queue-Internal-Resize bei 60 FPS x 5s = 300 Items.
    // Queue<T> ist bereits ein Ringbuffer intern; Capacity-Pre-Sizing eliminiert die einzige Allokation.
    private readonly Queue<long> _fpsFrameTicks = new(400);
    private long _lastFpsReportTicks;
    private const long FpsReportIntervalTicks = 5 * TimeSpan.TicksPerSecond;
    // v2.0.47 — Memory-Pressure-Tracking: 60s-Intervall für Heap-Größe + GC-Counts.
    // v2.0.55 — Phase 15 P1-Fix: 30s → 60s + Background-Thread weil GC.GetTotalMemory auf
    // Mono-AOT-Android 1-5ms Heap-Walk kostet (1-Frame-Spike alle 30s war messbar).
    private long _lastMemoryReportTicks;
    private const long MemoryReportIntervalTicks = 60 * TimeSpan.TicksPerSecond;
    // Cached SKColorFilter wenn Colorblind-Modus aktiv (verhindert pro-Frame-Allokation).
    private SKColorFilter? _colorblindFilter;
    private string _lastColorblindMode = "Off";
    // Audit C11: Cached SKPaint fuer Colorblind-SaveLayer — wurde frueher pro Frame
    // (30-60x/s) neu allokiert. ColorFilter wird bei Mode-Wechsel aktualisiert.
    private readonly SKPaint _colorblindLayerPaint = new();

    /// <summary>
    /// Während eines Levels eingesetzte Spezial-Bombentypen. Wird bei
    /// Level-Load geleert und bei CompleteLevel an den IDeckTelemetryService
    /// gemeldet. HashSet, damit jeder Typ maximal 1x pro Level zählt
    /// (verhindert Doppelzählung bei mehreren Einsätzen der gleichen Karte).
    /// </summary>
    private readonly HashSet<BombType> _specialBombTypesUsedInLevel = new();

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

    // Gecachter EnemiesRemaining-Zähler (Dirty-Flag statt 60x/s Iteration)
    private bool _enemiesRemainingDirty = true;
    private int _enemiesRemainingCache;

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
    //.1 : Bool-Flags sind jetzt Computed-Properties auf _currentMode.
    // Mode-Plugin-Migration komplett — Source-of-Truth ist _currentMode, Bools nur Read-View.
    // Set-Operationen (_isXxx = true/false) wurden entfernt; _currentMode wird in jeder
    // StartXxxModeAsync-Methode explizit gesetzt.
    private bool _isDailyChallenge => _currentMode is BomberBlast.Core.Modes.DailyChallengeMode;
    private bool _isSurvivalMode => _currentMode is BomberBlast.Core.Modes.SurvivalMode;
    private bool _isQuickPlayMode => _currentMode is BomberBlast.Core.Modes.QuickPlayMode;
    private BomberBlast.Core.Modes.QuickPlayMode? QuickPlayModeState => _currentMode as BomberBlast.Core.Modes.QuickPlayMode;
    private bool _isDungeonRun => _currentMode is BomberBlast.Core.Modes.DungeonMode;
    private bool _isMasterMode => _currentMode is BomberBlast.Core.Modes.MasterMode;

    /// <summary>
    /// v2.0.49 — Mode-Plugin-Framework : Aktiver Mode (kapselt Mode-spezifischen State).
    /// Wird beim Start einer StartXxxModeAsync-Methode gesetzt.
    /// Backward-Compat: Existierende Bool-Flags (_isSurvivalMode etc.) bleiben parallel als
    /// Source-of-Truth, bis die Engine-Logic auf IGameMode-Hooks migriert ist (Folge-Iterationen).
    /// </summary>
    private BomberBlast.Core.Modes.IGameMode? _currentMode;

    /// <summary>Public-Accessor für den aktiven Mode (Telemetrie + ggf. UI-Logic).</summary>
    public BomberBlast.Core.Modes.IGameMode? CurrentMode => _currentMode;

    /// <summary>
    /// Wall-Clock-Akkumulator seit Mode-Start (für GameModeContext.TimeElapsed).
    /// Wird in StartXxxModeAsync auf 0 gesetzt und in UpdatePlaying inkrementiert.
    /// </summary>
    private float _modeTimeElapsed;

    /// <summary>
    /// Phase 18 — Mode-Plugin-Framework Logic-Hook-Verkabelung (ARCH-1 aus Phase-15-Audit).
    /// Erstellt einen GameModeContext für die aktuellen Engine-State-Werte. Wird vor jedem
    /// IGameMode-Hook-Aufruf gebaut. Allokation ist 1× pro Frame im Hot-Path — akzeptabel,
    /// da Modes aktuell no-op-Defaults haben (Phase 19+ migriert echte Logic in Mode-Klassen).
    /// </summary>
    private BomberBlast.Core.Modes.GameModeContext BuildModeContext() => new()
    {
        Player = _player,
        Grid = _grid,
        CurrentLevel = _currentLevel,
        LevelNumber = _currentLevelNumber,
        TimeElapsed = _modeTimeElapsed,
    };

    /// <summary>Ob der aktuelle Lauf im Master Mode ist (New Game+ ab L100-Clear).</summary>
    public bool IsMasterMode => _isMasterMode;
    private LevelMutator _activeMutator = LevelMutator.None;
    private bool _levelCompleteHandled;
    private bool _continueUsed;

    // v2.0.52 — Phase 9: Dungeon-State liegt jetzt in DungeonMode (CurrentMode-Slot).
    // Bool-Flag _isDungeonRun bleibt als Hot-Path-Convenience erhalten.
    /// <summary>Hilfs-Property: Liefert die aktive DungeonMode-Instanz oder null.</summary>
    private BomberBlast.Core.Modes.DungeonMode? DungeonModeState => _currentMode as BomberBlast.Core.Modes.DungeonMode;

    // Backward-Compat-Felder als Property-Aliasse — gleicher Code, nur State-Lokation gewechselt.
    // Liefert Default-Werte (0/false) wenn DungeonMode nicht aktiv ist.
    private float _timeFreezeTimer
    {
        get => DungeonModeState?.TimeFreezeTimer ?? 0f;
        set { if (DungeonModeState is { } d) d.TimeFreezeTimer = value; }
    }
    private bool _phantomWalkAvailable
    {
        get => DungeonModeState?.PhantomWalkAvailable ?? false;
        set { if (DungeonModeState is { } d) d.PhantomWalkAvailable = value; }
    }
    private bool _phantomWalkActive
    {
        get => DungeonModeState?.PhantomWalkActive ?? false;
        set { if (DungeonModeState is { } d) d.PhantomWalkActive = value; }
    }
    private float _phantomWalkTimer
    {
        get => DungeonModeState?.PhantomWalkTimer ?? 0f;
        set { if (DungeonModeState is { } d) d.PhantomWalkTimer = value; }
    }
    private float _phantomCooldownTimer
    {
        get => DungeonModeState?.PhantomCooldownTimer ?? 0f;
        set { if (DungeonModeState is { } d) d.PhantomCooldownTimer = value; }
    }
    private bool _playerHadWallpassBeforePhantom
    {
        get => DungeonModeState?.PlayerHadWallpassBeforePhantom ?? false;
        set { if (DungeonModeState is { } d) d.PlayerHadWallpassBeforePhantom = value; }
    }
    private bool _synergyBlitzkriegActive
    {
        get => DungeonModeState?.SynergyBlitzkriegActive ?? false;
        set { if (DungeonModeState is { } d) d.SynergyBlitzkriegActive = value; }
    }
    private bool _synergyFortressActive
    {
        get => DungeonModeState?.SynergyFortressActive ?? false;
        set { if (DungeonModeState is { } d) d.SynergyFortressActive = value; }
    }
    private float _fortressRegenTimer
    {
        get => DungeonModeState?.FortressRegenTimer ?? 0f;
        set { if (DungeonModeState is { } d) d.FortressRegenTimer = value; }
    }
    private bool _synergyMidasActive
    {
        get => DungeonModeState?.SynergyMidasActive ?? false;
        set { if (DungeonModeState is { } d) d.SynergyMidasActive = value; }
    }
    private bool _synergyElementalActive
    {
        get => DungeonModeState?.SynergyElementalActive ?? false;
        set { if (DungeonModeState is { } d) d.SynergyElementalActive = value; }
    }
    private float _dungeonBombFuseReduction
    {
        get => DungeonModeState?.DungeonBombFuseReduction ?? 0f;
        set { if (DungeonModeState is { } d) d.DungeonBombFuseReduction = value; }
    }
    private bool _dungeonEnemySlowActive
    {
        get => DungeonModeState?.DungeonEnemySlowActive ?? false;
        set { if (DungeonModeState is { } d) d.DungeonEnemySlowActive = value; }
    }
    private DungeonFloorModifier _dungeonFloorModifier
    {
        get => DungeonModeState?.FloorModifier ?? DungeonFloorModifier.None;
        set { if (DungeonModeState is { } d) d.FloorModifier = value; }
    }
    private float _dungeonModifierRegenTimer
    {
        get => DungeonModeState?.ModifierRegenTimer ?? 0f;
        set { if (DungeonModeState is { } d) d.ModifierRegenTimer = value; }
    }

    // Daily Race (v2.0.42, Plan Task 3.1): Wie Daily Challenge mit Race-Flag,
    // wird nach GameOver via ILeagueService.SubmitDailyRaceScoreAsync gepusht.
    //.1 : Computed-Property auf _currentMode.
    private bool _isDailyRace => _currentMode is BomberBlast.Core.Modes.DailyRaceMode;

    /// <summary>Hilfs-Property: Liefert die aktive DailyRaceMode-Instanz oder null.</summary>
    private BomberBlast.Core.Modes.DailyRaceMode? DailyRaceModeState => _currentMode as BomberBlast.Core.Modes.DailyRaceMode;

    // Boss-Rush-Modus (v2.0.42, Plan Task 3.3): 5 sequenzielle Boss-Bosse mit Score-Akkumulation.
    // Bei Boss-Tod automatisch naechster Boss. Bei Player-Tod Run-Ende mit SubmitRun(score, time, false).
    // Bei 5. Boss-Clear: SubmitRun mit completedAll=true.
    //.1 : Computed-Property auf _currentMode.
    private bool _isBossRushMode => _currentMode is BomberBlast.Core.Modes.BossRushMode;

    /// <summary>
    /// Hilfs-Property: Liefert die aktive BossRushMode-Instanz oder null wenn nicht aktiv.
    /// Wird genutzt um Mode-State (BossIndex/AccumulatedScore/TotalTimeSeconds/Submitted) zu lesen/schreiben.
    /// </summary>
    private BomberBlast.Core.Modes.BossRushMode? BossRushModeState => _currentMode as BomberBlast.Core.Modes.BossRushMode;

    // Survival-Modus: Endloses Spawning mit steigender Schwierigkeit (v2.0.39: Logik in SurvivalSpawner extrahiert).
    // Felder bleiben hier weil sie ueber StartSurvival-Mode-Init / OnSurvivalEnded-Tracking-Reads in der Engine gebraucht werden.
    // v2.0.51 — Phase 8: State liegt in SurvivalMode (CurrentMode-Slot). Helper-Property für Read-Access.
    private BomberBlast.Core.Modes.SurvivalMode? SurvivalModeState => _currentMode as BomberBlast.Core.Modes.SurvivalMode;

    // Lazy-Init Context fuer den SurvivalSpawner (analog _explosionCtx).
    private Modes.SurvivalSpawnContext? _survivalCtx;
    private Modes.SurvivalSpawnContext SurvivalCtx => _survivalCtx ??= new Modes.SurvivalSpawnContext
    {
        Grid = _grid,
        Enemies = _enemies,
        ParticleSystem = _particleSystem,
        PontanRandom = _pontanRandom,
        GetPlayerGridX = () => _player.GridX,
        GetPlayerGridY = () => _player.GridY,
        OnEnemySpawned = () => _enemiesRemainingDirty = true,
    };

    // Gecachte Mechanik-Zellen (vermeidet 150-Zellen-Grid-Scan pro Frame)
    private readonly List<Cell> _mechanicCells = new();

    // _blockCells (wiederverwendbare Liste fuer PlacePowerUps/PlaceExit) ist in
    // Core/LevelGeneration/LevelGenerator.cs gewandert (v2.0.30+).

    // Dirty-Listen für geänderte Zellen (vermeidet 3x komplette Grid-Iteration pro Frame)
    private readonly List<Cell> _destroyingCells = new();
    private readonly List<Cell> _afterglowCells = new();
    private readonly List<Cell> _specialEffectCells = new();

    // Gegner-Positions-Cache für Bomben-Slide-Kollision (HashSet für schnellen Contains-Check)
    private readonly HashSet<(int x, int y)> _enemyPositionHashSet = new();

    // Ausstehende Eis-Cleanups (Frame-basiert statt Task.Delay → Thread-Race vermeiden)
    private readonly List<(List<(int x, int y)> cells, float timer)> _pendingIceCleanups = new();

    /// <summary>
    /// Welle 1 v2.0.58 : Hero-Trait DoubleDetonation (TwinTina).
    /// Wenn eine Spieler-Bombe explodiert, wird 0.5s spaeter eine Sekundaer-Explosion
    /// am selben Spot gespawnt (75% Range, kein Bomb-Cycle, keine Chain-Reaktion).
    /// </summary>
    private readonly List<(int gridX, int gridY, int range, float timer)> _pendingDoubleDetonations = new();

    // Statistics
    private int _bombsUsed;
    private int _enemiesKilled;
    private bool _exitRevealed;
    private Cell? _exitCell; // Gecachte Exit-Position (vermeidet Grid-Iteration pro Frame)
    private int _scoreAtLevelStart; // Score zu Beginn des Levels (für Coin-Berechnung)
    private bool _playerDamagedThisLevel; // Für NoDamage-Achievement

    //.2 : Funnel-Telemetrie — verstrichene Zeit + Tode pro Level.
    private float _levelElapsedSeconds; // Wird in Update() inkrementiert (nur Playing-State)
    private int _deathsInLevel;         // Reset bei Level-Start, +1 bei jedem Player-Tod im Level
    private int _comboTiersInLevel;     // Reset bei Level-Start, +1 bei jedem MEGA/ULTRA-Combo-Tier

    // Timing
    private float _stateTimer;
    private const float START_DELAY = 3f;
    private const float DEATH_DELAY = 2f;
    private const float LEVEL_COMPLETE_DELAY = 3f;

    // Gecachte SKPaint/SKFont für Overlay-Rendering (vermeidet Allokationen pro Frame)
    private readonly SKPaint _overlayBgPaint = new();
    private readonly SKPaint _overlayTextPaint = new() { IsAntialias = true };
    private readonly SKFont _overlayFont = new() { Embolden = true };
    // SKBlurStyle.Solid: Text/Form bleibt scharf-opak, plus äußerer Glow.
    // (Normal blurrt INNEN UND AUSSEN → Text wirkt matschig/unscharf.)
    private readonly SKMaskFilter _overlayGlowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Solid, 3);
    private readonly SKMaskFilter _overlayGlowFilterLarge = SKMaskFilter.CreateBlur(SKBlurStyle.Solid, 4);

    // Victory-Timer
    private float _victoryTimer;
    private const float VICTORY_DELAY = 3f;
    private bool _victoryHandled;

    // Game-Feel-Effekte
    private readonly ScreenShake _screenShake = new();
    private readonly ParticleSystem _particleSystem = new();
    private readonly GameFloatingTextSystem _floatingText = new();
    // v2.0.46 — Accessibility: Audio-Caption-System für gehörlose Spieler
    private readonly SubtitleSystem _subtitles = new();
    // v2.0.46 — : Cinematic-Director für Boss-Reveal-Sequenzen
    private readonly CinematicSequencer _cinematic = new();
    //.2 : ULTRA-Combo Vollbild-Vignette-Flash (Trigger bei Combo ≥ x10)
    private readonly UltraComboFlash _ultraFlash = new();
    //.3 : Damage-Flash (rote Vignette bei Player-Hit, 300ms snap+fade)
    private readonly UltraComboFlash _damageFlash = new();
    private float _hitPauseTimer;

    // Combo-System (Kettenexplosionen)
    // v2.0.54 — Phase 12: Combo-Logic in ComboSystem extrahiert (Pure-Logic, isoliert testbar).
    // Existierende Renderer-Reads nutzen die Read-only-Property-Aliasse.
    private readonly BomberBlast.Core.Combat.ComboSystem _comboSystem = new();
    private int _comboCount => _comboSystem.Count;
    private float _comboTimer => _comboSystem.Timer;
    private const float COMBO_WINDOW = 2f; // Legacy-Const für externe Reads (Renderer)

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
        _floatingText.Spawn(_player.X, _player.Y - 16,
            _localizationService.GetString("FloatShield") ?? "SHIELD!",
            BomberBlastColors.PowerUpCyan, 16f, 1.2f);
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
        CacheOverlayStrings();

        // Dynamische Overlay-Caches auch bei Sprachwechsel aktualisieren (sonst bleiben
        // "Stage X"/"Score: X"/"Level X" in alter Sprache bis zum naechsten State-Wechsel).
        // Nur sinnvoll wenn bereits ein Level aktiv ist (nicht beim allerersten CacheHudLabels-Call im Ctor).
        if (_currentLevel != null)
        {
            CacheStartingOverlayStrings();
            CacheLevelCompleteOverlayStrings();
            CacheGameOverOverlayStrings();
            CacheVictoryOverlayStrings();
        }
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
    // Phase 22 (G8 aus Audit) — Multi-Stage-Pontan-Warning:
    //  3.0s vor Spawn → Audio-Cue + Bildschirmrand-Glow + Subtitle "[ZEIT-WARNUNG]"
    //  1.5s vor Spawn → Pulsierendes "!"-Indicator (existing)
    //  0.5s vor Spawn → Trauma-Spike + roter Flash
    // Begründung: 1.5s allein war als zu schwach markiert. Drei Stufen geben dem Spieler
    // Reaktionszeit auf Mobile (Tap-Latenz) ohne Pontan völlig vorhersehbar zu machen.
    private const float PONTAN_WARNING_TIME = 1.5f; // Stage-2: Position + "!"-Indicator (Bestand)
    private const float PONTAN_EARLY_WARNING_TIME = 3.0f; // Stage-1: Audio-Cue + Subtitle
    private const float PONTAN_FINAL_WARNING_TIME = 0.5f; // Stage-3: Trauma-Spike-Flash
    private bool _pontanEarlyWarningTriggered;
    private bool _pontanFinalWarningTriggered;
    private const int PONTAN_MIN_DISTANCE = 5; // Mindestabstand zum Spieler
    private readonly Random _pontanRandom = new(); // Wiederverwendbar statt new Random() pro Aufruf

    // Lazy-initialisierter Context fuer SpecialExplosionEffects (v2.0.30+ Extract aus GameEngine.Explosion.cs).
    // Erst beim ersten Aufruf erzeugt → _grid muss dann bereits initialisiert sein (im Ctor via new GameGrid()).
    private BomberBlast.Core.Combat.ExplosionEffectsContext? _explosionCtx;
    private BomberBlast.Core.Combat.ExplosionEffectsContext GetExplosionContext() =>
        _explosionCtx ??= new BomberBlast.Core.Combat.ExplosionEffectsContext
        {
            Grid = _grid,
            SpecialEffectCells = _specialEffectCells,
            AfterglowCells = _afterglowCells,
            PowerUps = _powerUps,
            Enemies = _enemies,
            Explosions = _explosions,
            ParticleSystem = _particleSystem,
            FloatingText = _floatingText,
            LocalizationService = _localizationService,
            PontanRandom = _pontanRandom,
            DestroyBlock = DestroyBlock,
            // Phase 30d — Wrapper damit Action<Enemy>-Delegate funktioniert (sourceBomb null = Default-Player)
            KillEnemy = e => KillEnemy(e, null),
            ProcessExplosion = ProcessExplosion
        };

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
    public int PlayerGridX => _player?.GridX ?? 0;
    public int PlayerGridY => _player?.GridY ?? 0;
    public int CurrentLevel => _currentLevelNumber;
    public float RemainingTime => _timer?.RemainingTime ?? 0;
    public bool IsDailyChallenge => _isDailyChallenge;
    public bool IsSurvivalMode => _isSurvivalMode;
    public bool IsQuickPlayMode => _isQuickPlayMode;
    public bool IsDungeonRun => _isDungeonRun;
    public LevelMutator ActiveMutator => _activeMutator;
    public bool HasActiveMutator => _activeMutator != LevelMutator.None;
    public bool IsTimeFreezeActive => _timeFreezeTimer > 0;
    public float TimeFreezeTimer => _timeFreezeTimer;
    public bool IsPhantomAvailable => _phantomWalkAvailable;
    public bool IsPhantomActive => _phantomWalkActive;
    public float PhantomWalkTimer => _phantomWalkTimer;
    public float PhantomCooldownTimer => _phantomCooldownTimer;
    public bool CanActivatePhantom => _phantomWalkAvailable && !_phantomWalkActive && _phantomCooldownTimer <= 0;
    public int SurvivalKills => _enemiesKilled;
    public float SurvivalTimeElapsed => SurvivalModeState?.TimeElapsed ?? 0f;

    /// <summary>Verbleibende aktive Gegner (für HUD-Anzeige, gecacht via Dirty-Flag)</summary>
    public int EnemiesRemaining
    {
        get
        {
            if (_enemiesRemainingDirty)
            {
                int count = 0;
                foreach (var e in _enemies)
                    if (e.IsActive && !e.IsDying) count++;
                _enemiesRemainingCache = count;
                _enemiesRemainingDirty = false;
            }
            return _enemiesRemainingCache;
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
        ILevelGenerator levelGenerator,
        GameRenderer renderer,
        ITutorialService tutorialService,
        IDiscoveryService discoveryService,
        IDungeonService dungeonService,
        IDungeonUpgradeService dungeonUpgradeService,
        IGameTrackingService tracking,
        IDeckTelemetryService deckTelemetry,
        IMasterModeService masterModeService,
        ILoadoutService loadoutService,
        IBossRushService bossRushService,
        ILeagueService leagueService,
        IEventService eventService,
        IVibrationService vibrationService,
        ILogger<GameEngine> logger,
        IAccessibilityService accessibility,
        IAnalyticsService analytics,
        ITelemetryService telemetry,
        //.1 : Hero/Character-System Engine-Integration.
        IHeroService heroService,
        //.2 : 2P-Co-Op-Session-Service Engine-Integration.
        IMultiplayerSessionService multiplayerSession)
    {
        _soundManager = soundManager;
        _progressService = progressService;
        _highScoreService = highScoreService;
        _inputManager = inputManager;
        _localizationService = localizationService;
        _gameStyleService = gameStyleService;
        _shopService = shopService;
        _purchaseService = purchaseService;
        _levelGenerator = levelGenerator;

        _renderer = renderer;
        _tutorialService = tutorialService;
        _discoveryService = discoveryService;
        _dungeonService = dungeonService;
        _dungeonUpgradeService = dungeonUpgradeService;
        _tracking = tracking;
        _deckTelemetry = deckTelemetry;
        _masterModeService = masterModeService;
        _loadoutService = loadoutService;
        _bossRushService = bossRushService;
        _leagueService = leagueService;
        _eventService = eventService;
        _vibration = vibrationService;
        _logger = logger;
        _accessibility = accessibility;
        _analytics = analytics;
        _telemetry = telemetry;
        _heroService = heroService;
        _multiplayerSession = multiplayerSession;
        _tutorialOverlay = new TutorialOverlay(localizationService);
        _discoveryOverlay = new DiscoveryOverlay(localizationService);
        _grid = new GameGrid();
        _timer = new GameTimer();
        _enemyAI = new EnemyAI(_grid);
        _player = new Player(0, 0);

        // Timer-Events abonnieren
        _timer.Warning += OnTimeWarning;
        _timer.Expired += OnTimeExpired;

        // Haptisches Feedback bei Richtungswechsel direkt via IVibrationService
        // (vorher Event-Subscription in MainActivity → das hat den PERF-6 Lazy-Refactor umgangen).
        // DirectionChanged-Event bleibt fuer Tests/zukuenftige Subscriber erhalten.
        _directionChangedHandler = () =>
        {
            _vibration.VibrateTick();
            DirectionChanged?.Invoke();
        };
        _inputManager.DirectionChanged += _directionChangedHandler;

        // Phase 28b — KonamiCode-Easter-Egg-Reward verkabeln
        _inputManager.KonamiDetector.CodeTriggered += OnKonamiCodeTriggered;

        //.2 : Tutorial-Funnel-Telemetrie (per-Step + Final-Complete).
        _tutorialService.StepCompleted += OnTutorialStepCompleted;
        _tutorialService.TutorialCompleted += OnTutorialCompleted;

        // HUD-Labels cachen und bei Sprachwechsel aktualisieren
        CacheHudLabels();
        _languageChangedHandler = (_, _) => CacheHudLabels();
        _localizationService.LanguageChanged += _languageChangedHandler;
    }

    private void OnTutorialStepCompleted(int stepIndex)
    {
        _analytics?.LogEvent(AnalyticsEvents.TutorialStepComplete, new Dictionary<string, object>
        {
            [AnalyticsParams.StepId] = stepIndex,
        });
    }

    private void OnTutorialCompleted()
    {
        _analytics?.LogEvent(AnalyticsEvents.TutorialComplete, new Dictionary<string, object>
        {
            [AnalyticsParams.LevelId] = _currentLevelNumber,
        });
    }

    /// <summary>
    /// Phase 28b — Belohnung für den Konami-Code (1× pro Session):
    /// 1500 Coins-Bonus + Gold-Konfetti + Floating-Text + Vibration.
    /// Coin-Bonus bewusst klein (kein Cheat-Pfad), aber der Spieler hat etwas zum Erinnern.
    /// CoinsEarned-Event signalisiert dem ViewModel-Layer das Hinzufügen via ICoinService.
    /// </summary>
    private void OnKonamiCodeTriggered()
    {
        const int konamiCoins = 1500;
        // Engine kennt CoinService nicht direkt — sendet das Event und ViewModel addiert tatsächlich.
        // Signature: (coinsEarned, totalScore, isLevelComplete). isLevelComplete=false → Sub-Reward.
        CoinsEarned?.Invoke(konamiCoins, _player.Score, false);

        // Floating-Text + Konfetti + Vibration als Sicht-Reward
        var msg = _localizationService.GetString("KonamiCodeReward")
            ?? $"+{konamiCoins} KONAMI!";
        _floatingText.Spawn(_player.X, _player.Y - 30,
            string.Format(msg, konamiCoins),
            new SkiaSharp.SKColor(255, 215, 0), 22f, 3.0f);

        _particleSystem.EmitShaped(_player.X, _player.Y, 32,
            new SkiaSharp.SKColor(255, 215, 0),
            ParticleShape.Circle, 180f, 1.5f, 4f, hasGlow: true);
        _particleSystem.EmitExplosionSparks(_player.X, _player.Y, 18,
            new SkiaSharp.SKColor(255, 200, 50), 220f);

        _vibration.VibrateAchievement();
        _soundManager.PlayStinger(SoundManager.STINGER_VICTORY);

        _logger?.LogInformation("[KonamiCode] Easter-Egg ausgeloest, +{Coins} Coins", konamiCoins);
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

        _floatingText.Spawn(_player.X, _player.Y - 16,
            _localizationService.GetString("FloatPhantom") ?? "PHANTOM!",
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

        // Fog-of-War: Sichtbarkeit basierend auf aktueller Spieler-Position (v2.0.35).
        // Update nur wenn FoW aktiv (kostet sonst 0 — interne Enabled-Prüfung).
        if (_renderer.FogOfWar.IsEnabled && _player != null)
            _renderer.FogOfWar.Update(_player.GridX, _player.GridY);

        // ReducedEffects: ScreenShake, Partikel und atmosphärische Effekte deaktivieren
        bool reducedFx = _inputManager.ReducedEffects;
        _screenShake.Enabled = !reducedFx;
        _particleSystem.Enabled = !reducedFx;
        _renderer.ReducedEffects = reducedFx;

        _screenShake.Update(deltaTime);
        _particleSystem.Update(deltaTime);
        _floatingText.Update(deltaTime);
        _subtitles.Update(deltaTime);
        _cinematic.Update(deltaTime);
        _ultraFlash.Update(deltaTime);
        _damageFlash.Update(deltaTime);
        // Audit H15: SoundManager nur ticken wenn nicht pausiert/im Menu — Music-Volume-Fade
        // wuerde sonst auch im Paused-State laufen waehrend die Music selbst pausiert ist.
        if (_state != GameState.Paused && _state != GameState.Menu)
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
                //.2 : Funnel-Telemetrie tickt Level-Zeit nur waehrend Playing.
                _levelElapsedSeconds += deltaTime;
                // Phase 18c — FixedTimestep opt-in:
                // Wenn aktiv, wird UpdatePlaying mehrere Male pro Frame mit FIXED_TICK_SECONDS aufgerufen
                // (deterministische Sim-Granularität). Im Variable-Mode bleibt der existierende Pfad.
                if (_fixedTimestep.Enabled)
                {
                    int ticks = _fixedTimestep.GetTicksForFrame(deltaTime);
                    for (int i = 0; i < ticks; i++)
                    {
                        UpdatePlaying(FixedTimestepRunner.FIXED_TICK_SECONDS);
                        if (_state != GameState.Playing) break; // Sub-Tick-Transition (Tod/Complete)
                    }
                }
                else
                {
                    UpdatePlaying(deltaTime);
                }
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

        // Phase 18 — Mode-Time-Akkumulator (Wall-Clock, nicht Slow-Motion-affected)
        _modeTimeElapsed += realDeltaTime;

        // Phase 18 — IGameMode.UpdateLogic-Hook verkabeln (ARCH-1 aus Phase-15-Audit).
        // Aktuell sind alle GameModeBase-Defaults no-op — Wirkung ist 0, aber die polymorphe
        // API ist jetzt im Hot-Path verkabelt für Folge-Phasen (Logic-Migration in Mode-Klassen).
        // realDeltaTime wird übergeben (nicht slow-mo-affected — Modi tracken Wall-Clock).
        _currentMode?.UpdateLogic(realDeltaTime, BuildModeContext());

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
                _floatingText.Spawn(_player.X, _player.Y - 16,
                    _localizationService.GetString("FloatShield") ?? "SHIELD!",
                    BomberBlastColors.PowerUpCyan, 14f, 1.0f);
                _particleSystem.Emit(_player.X, _player.Y, 8,
                    BomberBlastColors.PowerUpCyan, 60f, 0.5f);
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
                _floatingText.Spawn(_player.X, _player.Y - 16,
                    _localizationService.GetString("FloatRegen") ?? "REGEN!",
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

        // v2.0.54 — Phase 12: Combo-Timer-Update in ComboSystem (Pure-Logic, Reset bei Timer<=0).
        _comboSystem.Update(realDeltaTime);
        if (_comboSystem.Timer <= 0 && _comboSystem.Count > 0)
            _comboSystem.Reset();

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

        // Input anwenden (ReverseControls-Curse oder MirrorControls-Mutator invertiert die Richtung)
        var inputDir = _inputManager.MovementDirection;
        if ((_player.ActiveCurse == CurseType.ReverseControls || _activeMutator == LevelMutator.MirrorControls)
            && inputDir != Direction.None)
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

        // Phase 30c — Player 2 parallel updaten wenn Co-Op-Modus aktiv
        if (MultiplayerMode != BomberBlast.Core.Multiplayer.MultiplayerMode.Single)
        {
            UpdatePlayer2Movement(deltaTime);
        }

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

        // Phase 22 — Input-Buffer-Tick (G1 aus Audit). Pro Frame um 1 dekrementieren.
        _inputManager.TickInputBuffer();
        // Phase 28b — Konami-Code-Detector mit Inputs füttern (Edge-Triggers für Direction/Bomb/Detonate)
        _inputManager.TickKonamiDetector(deltaTime);

        // Bombe platzieren — entweder direkter Tap ODER gepufferter Press aus den letzten 6 Frames
        bool bombPressNow = _inputManager.BombPressed;
        bool bombFromBuffer = _inputManager.HasBufferedBombPress;
        if ((bombPressNow || bombFromBuffer) && _player.CanPlaceBomb())
        {
            PlaceBomb();
            _inputManager.ConsumeBufferedBombPress();
            // Gegner-Pfade invalidieren → sofortige Neuberechnung (Bombe blockiert Weg)
            InvalidateEnemyPaths();
            // Tutorial: Bomben-Schritt als abgeschlossen markieren
            _tutorialService.CheckStepCompletion(TutorialStepType.PlaceBomb);
        }
        else if (bombPressNow && !_player.CanPlaceBomb())
        {
            // Press kommt zu früh (Limit erreicht / nicht-Cell-Center) → buffern, evtl. nächste Frames konsumieren
            _inputManager.BufferBombPress();
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
                CacheGameOverOverlayStrings();
                _soundManager.PlaySound(SoundManager.SFX_GAME_OVER);
                _soundManager.StopMusic();

                // v2.0.42 Plan Task 3.3: Boss-Rush bei Tod sofort submitten (completedAll=false).
                // Akkumulierter Score aus vorherigen Bossen + aktueller Run-Score.
                // v2.0.54 — Phase 11: TryGetSubmitArgs setzt Submitted=true atomar (Pure-Logic-Hook)
                if (_isBossRushMode && BossRushModeState is { } brm)
                {
                    int finalScore = brm.AccumulatedScore + (_player.Score - _scoreAtLevelStart);
                    if (brm.TryGetSubmitArgs(completedAllBosses: false) is { } sa)
                    {
                        _bossRushService.SubmitRun(finalScore, sa.Time, sa.CompletedAll);
                    }
                }

                // v2.0.54 — Phase 11: DailyRaceMode.TrySubmit() ist Pure-Logic-Hook (mit State-Mutation)
                if (_isDailyRace && DailyRaceModeState is { } drm)
                {
                    int finalScore = _player.Score - _scoreAtLevelStart;
                    if (drm.TrySubmit(finalScore))
                    {
                        // Fire-and-forget — Firebase-Push laueft im Hintergrund, Spieler wartet nicht.
                        _ = _leagueService.SubmitDailyRaceScoreAsync(finalScore);
                    }
                }

                // Trost-Coins (Level-Score ÷ 6, abgerundet)
                int coins = (_player.Score - _scoreAtLevelStart) / 6;
                if (_purchaseService.IsPremium)
                    coins *= 3;
                //.1 : Hero-Coin-Pickup-Multiplier auch bei Trost-Coins.
                var heroForTrost = _heroService.ActiveHero;
                if (Math.Abs(heroForTrost.CoinPickupMultiplier - 1.0f) > 0.001f)
                    coins = (int)Math.Round(coins * heroForTrost.CoinPickupMultiplier);
                if (coins > 0)
                {
                    CoinsEarned?.Invoke(coins, _player.Score, false);
                }

                // Deck-Balancing-Telemetrie: Plays++ für alle eingesetzten Bomben (auch bei GameOver)
                if (_specialBombTypesUsedInLevel.Count > 0)
                    _deckTelemetry.RecordLevelStartedWithBombs(_specialBombTypesUsedInLevel);

                // Survival-Runde beendet (Achievement + BattlePass)
                if (_isSurvivalMode)
                    _tracking.OnSurvivalEnded(SurvivalModeState?.TimeElapsed ?? 0f, _enemiesKilled);

                // Dungeon-Run beenden bei Tod
                if (_isDungeonRun)
                {
                    _tracking.OnDungeonRunCompleted();
                    var summary = _dungeonService.EndRun();
                    // _currentMode = null beendet auch _isDungeonRun (Computed).
                    _currentMode = null;
                    DungeonRunEnd?.Invoke(summary);
                }

                _tracking.FlushIfDirty();

                // v2.0.44 — : GameOver-Telemetrie für Funnel-Drop-off-Analyse.
                //.2 : erweitert um cause + attempt_count fuer Drop-Off-Pattern.
                _analytics?.LogEvent(AnalyticsEvents.LevelFailed, new Dictionary<string, object>
                {
                    ["level"] = _currentLevelNumber,
                    [AnalyticsParams.LevelId] = _currentLevelNumber,
                    [AnalyticsParams.WorldId] = (_currentLevelNumber - 1) / 10 + 1,
                    [AnalyticsParams.Cause] = _timer.IsExpired ? "time" : "enemy_or_bomb",
                    // Tode innerhalb dieser GameOver-Sequenz (jedes Sterben = ein Attempt).
                    [AnalyticsParams.AttemptCount] = _deathsInLevel,
                    [AnalyticsParams.TimeMs] = (long)Math.Max(0L, _levelElapsedSeconds * 1000f),
                    ["score"] = _player.Score,
                    ["mode"] = GetCurrentModeTag()
                });

                // Phase 18 — IGameMode.OnGameOver-Hook (ARCH-1 aus Phase-15-Audit)
                try { _currentMode?.OnGameOver(BuildModeContext()); }
                catch { /* Best-Effort, no-op-Default in GameModeBase */ }

                // Phase 21 (V4) — Defeat-Stinger
                _soundManager.PlayStinger(SoundManager.STINGER_DEFEAT);

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
        _hitPauseTimer = 0; // Defensiv: Hit-Pause zuruecksetzen (verhindert blockierten Countdown)
        CacheStartingOverlayStrings();

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
            int count = EngineRngNext(2, 4);
            for (int i = 0; i < count; i++)
            {
                int attempts = 20;
                while (attempts-- > 0)
                {
                    int gx = EngineRngNext(1, _grid.Width - 1);
                    int gy = EngineRngNext(1, _grid.Height - 1);
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
            int count = EngineRngNext(3, 6);
            for (int i = 0; i < count; i++)
            {
                // Zufälligen Block finden
                int attempts = 30;
                while (attempts-- > 0)
                {
                    int gx = EngineRngNext(1, _grid.Width - 1);
                    int gy = EngineRngNext(1, _grid.Height - 1);
                    var cell = _grid.TryGetCell(gx, gy);
                    if (cell?.Type != CellType.Block) continue;

                    // Zufällige Richtung
                    var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
                    var (dx, dy) = dirs[EngineRngNext(dirs.Length)];
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
            _floatingText.Spawn(_player.X, _player.Y - 16,
                _localizationService.GetString("FloatFall") ?? "FALL!", SKColors.Red, 16f, 1.5f);
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
        // Reverse-for statt RemoveAll(lambda): vermeidet Predicate-Dispatch-Overhead
        // im 30fps-Loop (4 Listen x ~50 Items/Frame = 6000 virtuelle Calls/s gespart).
        for (int i = _bombs.Count - 1; i >= 0; i--)
            if (_bombs[i].IsMarkedForRemoval) _bombs.RemoveAt(i);
        for (int i = _explosions.Count - 1; i >= 0; i--)
            if (_explosions[i].IsMarkedForRemoval) _explosions.RemoveAt(i);
        for (int i = _enemies.Count - 1; i >= 0; i--)
            if (_enemies[i].IsMarkedForRemoval) _enemies.RemoveAt(i);
        for (int i = _powerUps.Count - 1; i >= 0; i--)
            if (_powerUps[i].IsMarkedForRemoval) _powerUps.RemoveAt(i);
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
    /// Audit M29: Iteration ueber _enemies + Pattern-Match akzeptiert — pro Level max. 1-2 Bosses,
    /// kein Hot-Path. Separate _bosses-Liste waere strukturell sauberer, lohnt sich aber nicht.
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
    /// StoneGolem: 3-4 zufällige leere Zellen werden zu Blöcken.
    ///  (Enraged): 5-7 Zellen + bevorzugt rund um den Spieler (max 3 Zellen Abstand).
    /// </summary>
    private void CalculateStoneGolemTargets(BossEnemy boss)
    {
        bool phase2 = boss.CurrentPhase >= 2;
        int count = phase2 ? EngineRngNext(5, 8) : EngineRngNext(3, 5);
        int attempts = phase2 ? 60 : 40;

        // : Ersten Hit garantiert nah am Spieler (1 Zelle daneben).
        if (phase2)
        {
            int px = _player.GridX;
            int py = _player.GridY;
            (int dx, int dy)[] offsets = { (-1, 0), (1, 0), (0, -1), (0, 1) };
            foreach (var (dx, dy) in offsets)
            {
                var nearCell = _grid.TryGetCell(px + dx, py + dy);
                if (nearCell != null && nearCell.Type == CellType.Empty && nearCell.Bomb == null
                    && !boss.OccupiesCell(px + dx, py + dy))
                {
                    boss.AttackTargetCells.Add((px + dx, py + dy));
                    break;
                }
            }
        }

        while (boss.AttackTargetCells.Count < count && attempts-- > 0)
        {
            int gx, gy;
            if (phase2 && attempts > 30)
            {
                // : Erste Hälfte bevorzugt nahe Spieler (3-Zellen-Umkreis)
                gx = Math.Clamp(_player.GridX + EngineRngNext(-3, 4), 1, _grid.Width - 2);
                gy = Math.Clamp(_player.GridY + EngineRngNext(-3, 4), 1, _grid.Height - 2);
            }
            else
            {
                gx = EngineRngNext(1, _grid.Width - 1);
                gy = EngineRngNext(1, _grid.Height - 1);
            }

            var cell = _grid.TryGetCell(gx, gy);
            if (cell != null && cell.Type == CellType.Empty && cell.Bomb == null &&
                !boss.OccupiesCell(gx, gy) &&
                !boss.AttackTargetCells.Contains((gx, gy)))
            {
                boss.AttackTargetCells.Add((gx, gy));
            }
        }
    }

    /// <summary>
    /// IceDragon: Zufällige horizontale Reihe wird eingefroren.
    ///  (Enraged): 2 Reihen — eine über und eine unter dem Spieler (Sandwich-Pattern).
    /// </summary>
    private void CalculateIceDragonTargets(BossEnemy boss)
    {
        if (boss.CurrentPhase >= 2)
        {
            // Sandwich: Reihe über + unter Spieler. Engt Bewegungsfreiheit ein.
            int rowAbove = Math.Max(1, _player.GridY - 1);
            int rowBelow = Math.Min(_grid.Height - 2, _player.GridY + 1);
            for (int x = 1; x < _grid.Width - 1; x++)
            {
                var cellAbove = _grid.TryGetCell(x, rowAbove);
                if (cellAbove != null && (cellAbove.Type == CellType.Empty || cellAbove.Type == CellType.Ice))
                    boss.AttackTargetCells.Add((x, rowAbove));

                if (rowBelow != rowAbove)
                {
                    var cellBelow = _grid.TryGetCell(x, rowBelow);
                    if (cellBelow != null && (cellBelow.Type == CellType.Empty || cellBelow.Type == CellType.Ice))
                        boss.AttackTargetCells.Add((x, rowBelow));
                }
            }
            return;
        }

        // : Zufällige Reihe (nicht die äußersten Ränder)
        int row = EngineRngNext(1, _grid.Height - 1);
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
    /// FireDemon: Obere oder untere Hälfte des Bodens wird gefährlich.
    ///  (Enraged): 3/4 des Bodens — nur ein 1/4-Streifen bleibt als Safe-Zone.
    /// </summary>
    private void CalculateFireDemonTargets(BossEnemy boss)
    {
        if (boss.CurrentPhase >= 2)
        {
            // : 3 von 4 Quadranten = Lava. Quadrant wird zufällig ausgespart.
            int safeQuadrant = EngineRngNext(4); // 0=oben-links, 1=oben-rechts, 2=unten-links, 3=unten-rechts
            int midX = _grid.Width / 2;
            int midY = _grid.Height / 2;
            for (int y = 1; y < _grid.Height - 1; y++)
            {
                for (int x = 1; x < _grid.Width - 1; x++)
                {
                    bool right = x >= midX;
                    bool bottom = y >= midY;
                    int quadrant = (bottom ? 2 : 0) + (right ? 1 : 0);
                    if (quadrant == safeQuadrant) continue;

                    var cell = _grid.TryGetCell(x, y);
                    if (cell != null && cell.Type == CellType.Empty)
                        boss.AttackTargetCells.Add((x, y));
                }
            }
            return;
        }

        // : Zufällig obere oder untere Hälfte
        bool upperHalf = EngineRngNext(2) == 0;
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
    /// Boss-Angriff ausführen (einmalig beim Wechsel von Telegraph → Angriff).
    /// FinalBoss (Enraged): Führt 2 Attacks gleichzeitig aus (Rotation + Rotation+1).
    /// </summary>
    private void ExecuteBossAttack(BossEnemy boss)
    {
        // Flag setzen damit Angriff nur einmal ausgeführt wird
        // (AttackDuration sinkt unter 1.4f nach dem ersten Frame)

        if (boss.BossKind == BossType.FinalBoss && boss.CurrentPhase >= 2)
        {
            // : Doppel-Angriff. Erst aktueller Rotation-Index, dann der nächste.
            var first = BossKindForRotation(boss.AttackRotationIndex);
            var second = BossKindForRotation((boss.AttackRotationIndex + 1) % 4);
            ExecuteSingleBossAttack(boss, first);

            // Für zweite Attack neue Target-Cells berechnen (sonst bleibt die Liste vom ersten Attack stehen).
            boss.AttackTargetCells.Clear();
            CalculateBossAttackTargetsForType(boss, second);
            ExecuteSingleBossAttack(boss, second);
            return;
        }

        var attackType = boss.BossKind == BossType.FinalBoss
            ? BossKindForRotation(boss.AttackRotationIndex)
            : boss.BossKind;
        ExecuteSingleBossAttack(boss, attackType);
    }

    private static BossType BossKindForRotation(int rotationIndex) => rotationIndex switch
    {
        0 => BossType.StoneGolem,
        1 => BossType.IceDragon,
        2 => BossType.FireDemon,
        3 => BossType.ShadowMaster,
        _ => BossType.StoneGolem
    };

    private void CalculateBossAttackTargetsForType(BossEnemy boss, BossType attackType)
    {
        switch (attackType)
        {
            case BossType.StoneGolem: CalculateStoneGolemTargets(boss); break;
            case BossType.IceDragon: CalculateIceDragonTargets(boss); break;
            case BossType.FireDemon: CalculateFireDemonTargets(boss); break;
            case BossType.ShadowMaster: /* ShadowMaster braucht keine Target-Cells */ break;
        }
    }

    private void ExecuteSingleBossAttack(BossEnemy boss, BossType attackType)
    {
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
        _floatingText.Spawn(boss.X, boss.Y - 24,
            _localizationService.GetString("FloatBossBlockRain") ?? "BLOCK RAIN!",
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
        _floatingText.Spawn(boss.X, boss.Y - 24,
            _localizationService.GetString("FloatBossIceBreath") ?? "ICE BREATH!",
            new SKColor(100, 200, 255), 18f, 1.5f);
        _soundManager.PlaySound(SoundManager.SFX_EXPLOSION);

        // Eis nach 3s wieder entfernen (Frame-basierter Timer statt Task.Delay → Thread-sicher).
        // Audit M11: Defensive Copy via new List<>(capacity) statt .ToList() (LINQ-Allokation).
        var iceCells = new List<(int gx, int gy)>(boss.AttackTargetCells.Count);
        iceCells.AddRange(boss.AttackTargetCells);
        _pendingIceCleanups.Add((iceCells, 3f));
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
        _floatingText.Spawn(boss.X, boss.Y - 24,
            _localizationService.GetString("FloatBossLavaWave") ?? "LAVA WAVE!",
            new SKColor(255, 100, 0), 18f, 1.5f);
        _soundManager.PlaySound(SoundManager.SFX_EXPLOSION);
    }

    /// <summary>
    /// ShadowMaster-Angriff: Boss teleportiert sich zu zufälliger Position.
    ///  (Enraged): Spawnt zusätzlich 2 Schatten-Ballom am alten Standort
    /// (kurze Lebenszeit als Mini-Enemies, harassen den Spieler während der Boss neu positioniert).
    /// </summary>
    private void ExecuteShadowMasterAttack(BossEnemy boss)
    {
        bool phase2 = boss.CurrentPhase >= 2;

        // Alte Position für Phase-2-Schatten merken
        float oldX = boss.X;
        float oldY = boss.Y;
        int oldGridX = boss.GridX;
        int oldGridY = boss.GridY;

        // Partikel am alten Standort
        _particleSystem.Emit(oldX, oldY, 12, new SKColor(100, 0, 180), 80f, 0.5f);

        // Zufällige neue Position suchen (mit genug Platz für BossSize)
        int attempts = 30;
        while (attempts-- > 0)
        {
            int gx = EngineRngNext(2, _grid.Width - boss.BossSize - 1);
            int gy = EngineRngNext(2, _grid.Height - boss.BossSize - 1);

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
        _floatingText.Spawn(boss.X, boss.Y - 24,
            _localizationService.GetString("FloatBossTeleport") ?? "TELEPORT!",
            new SKColor(150, 0, 255), 18f, 1.5f);
        _soundManager.PlaySound(SoundManager.SFX_ENEMY_DEATH);

        // : 2 Schatten-Ballom am alten Standort spawnen (Harass-Wave).
        if (phase2)
        {
            (int dx, int dy)[] offsets = { (-1, 0), (1, 0), (0, -1), (0, 1) };
            int spawned = 0;
            foreach (var (dx, dy) in offsets)
            {
                if (spawned >= 2) break;
                int sx = oldGridX + dx;
                int sy = oldGridY + dy;
                var cell = _grid.TryGetCell(sx, sy);
                if (cell == null || cell.Type != CellType.Empty || cell.Bomb != null) continue;

                var shadow = new Enemy(
                    sx * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f,
                    sy * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f,
                    EnemyType.Ballom);
                _enemies.Add(shadow);
                _particleSystem.Emit(shadow.X, shadow.Y, 8, new SKColor(180, 0, 255), 70f, 0.5f);
                spawned++;
            }

            if (spawned > 0)
            {
                _floatingText.Spawn(oldX, oldY - 16,
                    _localizationService.GetString("FloatShadowClone") ?? "SHADOW CLONE!",
                    new SKColor(180, 0, 255), 16f, 1.2f);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ICE CLEANUP (Frame-basiert statt Task.Delay)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ausstehende Eis-Cleanups pro Frame aktualisieren (Timer dekrementieren, bei 0 → CellType.Empty).
    /// Welle 1 v2.0.58: Auch DoubleDetonation-Sekundaer-Explosionen werden hier getickt.
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

        // Welle 1 v2.0.58 : TwinTina-DoubleDetonation Sekundaer-Explosionen.
        for (int i = _pendingDoubleDetonations.Count - 1; i >= 0; i--)
        {
            var (gridX, gridY, range, timer) = _pendingDoubleDetonations[i];
            timer -= deltaTime;
            if (timer <= 0)
            {
                SpawnDoubleDetonationSecondary(gridX, gridY, range);
                _pendingDoubleDetonations.RemoveAt(i);
            }
            else
            {
                _pendingDoubleDetonations[i] = (gridX, gridY, range, timer);
            }
        }
    }

    /// <summary>
    /// Welle 1 v2.0.58 : Spawnt eine standalone Explosion ohne Bomb-Lifecycle.
    /// Wird vom DoubleDetonation-Trait getriggert (TwinTina). Loest KEINE Kettenreaktion aus
    /// (keine vorhandenen Bomben in Reichweite zuenden), damit der Trait nicht zu maechtig wird.
    /// </summary>
    private void SpawnDoubleDetonationSecondary(int gridX, int gridY, int range)
    {
        // Pseudo-Bombe nur als Source-Holder, wird sofort nach Explosion verworfen.
        float px = gridX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        float py = gridY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        var pseudoBomb = new Bomb(px, py, _player, range);
        pseudoBomb.Explode();  // setzt IsExploded → Explosion-Spread funktioniert

        var explosion = new Explosion(pseudoBomb);
        explosion.CalculateSpread(_grid, range);
        _explosions.Add(explosion);

        // Audio + Visual-Feedback (gedaempft, kein Mega-Shake)
        _soundManager.PlaySoundPanned(SoundManager.SFX_EXPLOSION, 0f);
        _particleSystem.EmitShaped(px, py, 8, ParticleColors.Explosion,
            ParticleShape.Circle, 60f, 0.4f, 2f, hasGlow: true);
        _screenShake.AddTrauma(0.2f);

        // Effekte sofort verarbeiten (Block/Enemy-Damage).
        ProcessExplosion(explosion);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer.Warning -= OnTimeWarning;
        _timer.Expired -= OnTimeExpired;
        _inputManager.DirectionChanged -= _directionChangedHandler;
        _inputManager.KonamiDetector.CodeTriggered -= OnKonamiCodeTriggered;
        _tutorialService.StepCompleted -= OnTutorialStepCompleted;
        _tutorialService.TutorialCompleted -= OnTutorialCompleted;
        _localizationService.LanguageChanged -= _languageChangedHandler;

        _overlayBgPaint.Dispose();
        _overlayTextPaint.Dispose();
        _overlayFont.Dispose();
        _overlayGlowFilter.Dispose();
        _overlayGlowFilterLarge.Dispose();
        _particleSystem.Dispose();
        _floatingText.Dispose();
        _subtitles.Dispose();
        _ultraFlash.Dispose();
        _damageFlash.Dispose();
        _tutorialOverlay.Dispose();
        _discoveryOverlay.Dispose();
        // InputManager-Lifetime gehoert dem DI-Container (App.DisposeServices) — hier kein Dispose
        // (Audit C07: doppeltes Dispose fuehrt zu Native-Double-Free bei SKPaint/SKPath).
        _soundManager.Dispose();
        _colorblindLayerPaint.Dispose();
        _irisClipPath.Dispose();
        _starPath.Dispose();
        _colorblindFilter?.Dispose();

        // v2.0.53 — Phase 10 P1-Fix: Mode-Cleanup-Hook + State-Reset bei Dispose
        if (_currentMode is { } m)
        {
            try
            {
                m.Cleanup(new BomberBlast.Core.Modes.GameModeContext
                {
                    Player = _player,
                    Grid = _grid,
                    LevelNumber = _currentLevelNumber,
                    TimeElapsed = 0f
                });
            }
            catch { /* Best-Effort Cleanup */ }
            _currentMode = null;
        }
    }
}
