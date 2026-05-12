using BomberBlast.Graphics;
using BomberBlast.Models;
using BomberBlast.Models.Dungeon;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Core.LevelGeneration;
using BomberBlast.Models.Levels;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Core;

/// <summary>
/// Level-Verwaltung: Laden, PowerUps, Exit, Gegner, Abschluss
/// </summary>
public sealed partial class GameEngine
{
    /// <summary>
    /// Story-Modus starten. Bei masterMode=true werden Gegner 50% schneller und
    /// Low/Normal-Typen zu High-Typen aufgewertet (siehe ApplyMasterModeEnemyUpgrade).
    /// Master-Mode-Parameter wird gegen <see cref="IMasterModeService.IsUnlocked"/>
    /// validiert — falscher Navigation-Parameter (z.B. durch Deep-Link) wird auf
    /// normalen Story-Modus zurückgesetzt.
    /// </summary>
    public async Task StartStoryModeAsync(int levelNumber, bool masterMode = false)
    {
        // v2.0.55 — Phase 15 P1-Fix: Cinematic-Sequencer bei Mode-Wechsel stoppen
        // (sonst kann Boss-Reveal-Cinematic nach Mode-Wechsel weiterlaufen).
        _cinematic?.Stop();
        // Sprint 5.1: _isDailyChallenge ist Computed auf _currentMode.
        // Sprint 5.1: _isSurvivalMode ist Computed auf _currentMode.
        // Sprint 5.1: _isQuickPlayMode ist Computed auf _currentMode.
        // Sprint 5.1: _isDungeonRun ist Computed auf _currentMode.
        // v2.0.52 Code-Review-Fix: Auch BossRush + DailyRace explizit zuruecksetzen.
        // Vorher: Bei Boss-Rush-Abort -> Story-Start blieb _isBossRushMode=true -> Stale-Bool-Path
        // in CompleteLevel/Victory + falscher Render-Event-Block.
        // Sprint 5.1: _isBossRushMode ist Computed auf _currentMode.
        // Sprint 5.1: _isDailyRace ist Computed auf _currentMode.
        // Defense-in-Depth: Master-Mode nur wenn wirklich unlocked.
        // Downgrade auf Normal-Mode wird geloggt — hilft beim Debuggen falls Navigation
        // einen unerwarteten masterMode=true liefert (z.B. durch veraltete Preference).
        // Sprint 5.1 AAA-Audit #11: Mode-Selection bestimmt _currentMode direkt;
        // _isMasterMode ist jetzt Computed-Property auf _currentMode.
        bool effectiveMaster = masterMode && _masterModeService.IsUnlocked;

        // v2.0.49 — Mode-Plugin-Framework Phase 2: aktiver Mode setzen
        _currentMode = effectiveMaster
            ? new BomberBlast.Core.Modes.MasterMode()
            : new BomberBlast.Core.Modes.StoryMode();
        if (masterMode && !effectiveMaster)
        {
            // IAppLogger statt Debug.WriteLine: Auch im Release-Build via LogCat sichtbar (Android),
            // hilft beim Debuggen wenn ein veralteter Deep-Link masterMode=true setzt.
            _logger.LogWarning(
                $"[GameEngine] Master-Mode für L{levelNumber} angefordert aber !IsUnlocked → Normal-Mode-Fallback");
        }
        _currentLevelNumber = levelNumber;
        _currentLevel = LevelLayoutGenerator.GenerateLevel(levelNumber, _progressService.HighestCompletedLevel);
        _activeMutator = _currentLevel.Mutator;
        _continueUsed = false;

        // Sprint 7.2 AAA-Audit #22: 2P-Co-Op aktivieren wenn vom MultiplayerSession-Service gesetzt.
        // Player2 wird auf gegenueberliegender Spawn-Position erzeugt (siehe Multiplayer-Foundation).
        EnableMultiplayer(_multiplayerSession.CurrentMode);

        _player.ResetForNewGame();
        ApplyHeroStats();   // Sprint 7.1 AAA-Audit #21: Hero-Stats VOR Upgrades anwenden.
        ApplyUpgrades();
        MutatorEffects.Apply(_player, _activeMutator);
        ApplyLoadoutBoosts(levelNumber);  // v2.0.41 Plan Task 3.2: Pre-Level-Boosts anwenden
        await LoadLevelAsync();
        if (_isMasterMode) ApplyMasterModeEnemyUpgrade();

        // Welt-spezifische Musik (Boss-Track hat Priorität, sonst Welt-Track mit Gameplay-Fallback)
        int world = (_currentLevelNumber - 1) / 10 + 1;
        if (_currentLevel.MusicTrack == "boss")
            _soundManager.PlayMusic(SoundManager.MUSIC_BOSS);
        else
            _soundManager.PlayMusicWithFallback(SoundManager.GetWorldMusicKey(world - 1), SoundManager.MUSIC_GAMEPLAY);

        // Welt-/Boss-/Mutator-Ankündigung
        if (_currentLevel.IsBossLevel)
        {
            // Typspezifischer Boss-Name (Stone Golem, Ice Dragon, ...) statt generischem "BOSS FIGHT!"
            // Bei Duo-Boss-Encounter (BossKind2 != null): Beide Namen verbunden mit "&"
            _worldAnnouncementText = ComposeBossBannerText(_currentLevel.BossKind, _currentLevel.BossKind2);
            _worldAnnouncementTimer = 2.5f;

            // v2.0.46 — Audio-Caption: "[BOSS BRÜLLT]" für gehörlose Spieler
            if (_accessibility?.SubtitlesEnabled == true)
            {
                _subtitles.Show(_localizationService.GetString("SubtitleBossRoar") ?? "[BOSS ROARS]", duration: 3f);
            }

            // v2.0.46 — Cinematic-Director Phase 1: Boss-Reveal-Sequenz (1.5s)
            PlayBossRevealCinematic();
        }
        else if (_activeMutator != LevelMutator.None)
        {
            // Mutator-Ankündigung (z.B. "MUTATOR: Double Speed")
            var mutatorName = _levelGenerator.GetMutatorDisplayName(_activeMutator);
            var format = _localizationService.GetString("MutatorActive") ?? "Mutator: {0}";
            _worldAnnouncementText = string.Format(format, mutatorName);
            _worldAnnouncementTimer = 2.5f;
        }
        else
        {
            _worldAnnouncementText = string.Format(
                _localizationService.GetString("AnnounceWorld") ?? "WORLD {0}", world);
            _worldAnnouncementTimer = 2.0f;
        }

        // Sprint 2.2 AAA-Audit #2: Funnel-Event level_start mit Welt + Lives + Master-Mode-Flag.
        _analytics?.LogEvent(AnalyticsEvents.LevelStart, new Dictionary<string, object>
        {
            [AnalyticsParams.LevelId] = levelNumber,
            [AnalyticsParams.WorldId] = world,
            [AnalyticsParams.Lives] = _player.Lives,
            ["master_mode"] = _isMasterMode ? 1 : 0,
            ["mutator"] = _activeMutator.ToString(),
        });

        // Sprint 2.2: boss_encounter wenn das Level einen Boss hat (vor dem Kampf, fuer Drop-Off-Funnel).
        if (_currentLevel.IsBossLevel && _currentLevel.BossKind is { } bossKind)
        {
            _analytics?.LogEvent(AnalyticsEvents.BossEncounter, new Dictionary<string, object>
            {
                [AnalyticsParams.BossType] = bossKind.ToString(),
                [AnalyticsParams.Phase] = 1,
                [AnalyticsParams.LevelId] = levelNumber,
                [AnalyticsParams.WorldId] = world,
            });
        }
    }

    /// <summary>
    /// Liefert einen typspezifischen Boss-Banner-Text. Bei Duo-Bossen (Welt 9 = FinalBoss + ShadowMaster,
    /// Welt 10 = 2x FinalBoss) werden beide Namen verbunden zurückgegeben.
    /// Fallback bei fehlender Lokalisierung ist "BOSS FIGHT!".
    /// </summary>
    private string ComposeBossBannerText(BossType? primary, BossType? secondary)
    {
        if (primary == null)
            return _localizationService.GetString("AnnounceBossFight") ?? "BOSS FIGHT!";

        var primaryName = GetBossDisplayName(primary.Value);

        if (secondary.HasValue && secondary.Value != primary.Value)
        {
            var secondaryName = GetBossDisplayName(secondary.Value);
            return $"{primaryName} & {secondaryName}";
        }

        if (secondary.HasValue)
        {
            // Gleicher Boss zweimal → Plural-Form über RESX (z.B. "Final Bosses")
            var pluralKey = $"BossNamePlural{primary.Value}";
            var plural = _localizationService.GetString(pluralKey);
            if (!string.IsNullOrEmpty(plural))
                return plural;
            return $"{primaryName} x2";
        }

        return primaryName;
    }

    /// <summary>Lokalisierter Anzeige-Name für einen Boss-Typ. Fallback ist der Enum-Name.</summary>
    private string GetBossDisplayName(BossType bossType)
    {
        var key = $"BossName{bossType}";
        return _localizationService.GetString(key) ?? bossType.ToString().ToUpperInvariant();
    }

    /// <summary>
    /// v2.0.46 — Cinematic-Director Phase 1: Boss-Reveal-Sequenz.
    /// Spielt 1.5s lang ein orchestriertes Effekt-Set ab: Particle-Bursts an Boss-Position,
    /// Trauma-Shake-Spike, Floating-Text-Stinger.
    /// Findet die Boss-Position über die Boss-Enemy-Liste (gespawnt in LoadLevelAsync).
    /// Bei Duo-Boss werden beide Bosse mit Bursts versehen.
    /// </summary>
    private void PlayBossRevealCinematic()
    {
        // Boss-Position(en) ermitteln
        var bossPositions = new List<(float x, float y, BossType type)>();
        foreach (var enemy in _enemies)
        {
            if (enemy is BossEnemy boss)
                bossPositions.Add((boss.X, boss.Y, boss.BossKind));
        }

        // Wenn (noch) keine Boss-Spawn-Daten verfügbar → Fallback auf Spielfeld-Mitte
        if (bossPositions.Count == 0)
        {
            float cx = _grid.PixelWidth / 2f;
            float cy = _grid.PixelHeight / 2f;
            bossPositions.Add((cx, cy, BossType.StoneGolem));
        }

        // Sprint 5.3 AAA-Audit #13: Music-Boost waehrend Boss-Reveal-Cinematic (+15% Music fuer 8s).
        // Verstaerkt die Atmosphaere — der Boss-Encounter ist filmisch wertvoll, Music-Boost
        // macht den Moment epischer. 8s = Cinematic-Dauer + Initial-Phase nach Spielstart.
        _soundManager.BusMixer.Boost(BomberBlast.Core.Audio.AudioBus.Music, 1.15f, 8.0f);

        // Sequence definieren — 1.5s gesamt
        var events = new List<CinematicSequencer.TimedEvent>
        {
            // 0.0s: Initialer Particle-Burst um jeden Boss (Gold-Funken)
            new(0.0f, () =>
            {
                foreach (var (x, y, _) in bossPositions)
                {
                    _particleSystem.EmitShaped(x, y, 14, BomberBlastColors.Gold,
                        ParticleShape.Circle, 110f, 0.7f, 3f, hasGlow: true);
                }
                _screenShake.AddTrauma(0.45f);
                // Phase 21 (V4) — Boss-Reveal-Stinger + leichter Pull-Back
                _screenShake.TriggerPullBack(magnitude: 0.7f, durationSeconds: 1.0f);
                _soundManager.PlayStinger(SoundManager.STINGER_BOSS_REVEAL);
            }),

            // 0.25s: Zweiter Burst in welt-spezifischer Akzentfarbe (Boss-Theme)
            new(0.25f, () =>
            {
                foreach (var (x, y, type) in bossPositions)
                {
                    var color = type switch
                    {
                        BossType.StoneGolem => new SKColor(150, 130, 100),
                        BossType.IceDragon => new SKColor(80, 200, 255),
                        BossType.FireDemon => new SKColor(255, 80, 30),
                        BossType.ShadowMaster => new SKColor(180, 80, 220),
                        BossType.FinalBoss => new SKColor(255, 50, 100),
                        _ => BomberBlastColors.Gold
                    };
                    _particleSystem.EmitShaped(x, y, 18, color,
                        ParticleShape.Circle, 130f, 0.8f, 4f, hasGlow: true);
                }
            }),

            // 0.6s: Floating-Stinger ("BOSS!" oder Boss-Name) über erstem Boss
            new(0.6f, () =>
            {
                if (bossPositions.Count > 0)
                {
                    var (x, y, type) = bossPositions[0];
                    var stinger = GetBossDisplayName(type);
                    _floatingText.Spawn(x, y - 48, stinger,
                        new SKColor(255, 80, 80), fontSize: 28f, duration: 1.5f);
                }
                _screenShake.AddTrauma(0.3f);
            }),

            // 1.0s: Finaler Burst + Vibration
            new(1.0f, () =>
            {
                foreach (var (x, y, _) in bossPositions)
                {
                    _particleSystem.EmitShaped(x, y, 22, new SKColor(255, 255, 220),
                        ParticleShape.Circle, 90f, 0.6f, 2f, hasGlow: true);
                }
                _vibration.VibrateBossRoar();
            })
        };

        // v2.0.47 — Cinematic-Director Phase 2: Camera-Zoom auf erste Boss-Position
        if (bossPositions.Count > 0)
        {
            _cinematic.MaxCameraZoom = 0.35f;  // 35% Zoom-In auf Boss
            _cinematic.ZoomPivotX = bossPositions[0].x;
            _cinematic.ZoomPivotY = bossPositions[0].y;
        }

        _cinematic.Play(durationSeconds: 1.5f, events);
    }

    /// <summary>
    /// Daily-Challenge-Modus starten (einmaliges Level pro Tag)
    /// </summary>
    public async Task StartDailyChallengeModeAsync(int seed)
    {
        _cinematic?.Stop();  // v2.0.55 P1-Fix
        // Sprint 5.1: _isDailyChallenge wird durch _currentMode = new DailyChallengeMode() unten gesetzt.
        // Sprint 5.1: _isSurvivalMode ist Computed auf _currentMode.
        // Sprint 5.1: _isQuickPlayMode ist Computed auf _currentMode.
        // Sprint 5.1: _isDungeonRun ist Computed auf _currentMode.
        // Sprint 5.1 AAA-Audit #11: _isMasterMode ist jetzt Computed auf _currentMode.
        // Sprint 5.1: _isBossRushMode ist Computed auf _currentMode.
        // Sprint 5.1: _isDailyRace ist Computed auf _currentMode.
        _currentMode = new BomberBlast.Core.Modes.DailyChallengeMode();
        _activeMutator = LevelMutator.None;
        _currentLevelNumber = 99;
        _currentLevel = LevelLayoutGenerator.GenerateDailyChallengeLevel(seed);
        _continueUsed = false;

        // Sprint 5.2 AAA-Audit #12: Daily-Challenge → deterministischer Seed fuer ALLE Spieler weltweit.
        // Pontan-Spawns, Drop-Positionen, AI-Random-Movement-Fallback laufen jetzt synchron auf
        // allen Geraeten (x64/ARM64) — Voraussetzung fuer Daily-Leaderboard-Fairness.
        SetDeterministicSeed((ulong)seed);

        _player.ResetForNewGame();
        ApplyHeroStats();   // Sprint 7.1 AAA-Audit #21: Hero-Stats VOR Upgrades anwenden.
        ApplyUpgrades();
        await LoadLevelAsync();

        _soundManager.PlayMusic(SoundManager.MUSIC_GAMEPLAY);

        _worldAnnouncementText = "DAILY CHALLENGE";
        _worldAnnouncementTimer = 2.5f;
    }

    /// <summary>
    /// Quick-Play-Modus starten (einzelnes Level mit Seed + Schwierigkeit, kein Progress)
    /// </summary>
    public async Task StartQuickPlayModeAsync(int seed, int difficulty)
    {
        _cinematic?.Stop();  // v2.0.55 P1-Fix
        // Sprint 5.1: _isDailyChallenge ist Computed auf _currentMode.
        // Sprint 5.1: _isSurvivalMode ist Computed auf _currentMode.
        // Sprint 5.1: _isQuickPlayMode wird durch _currentMode = new QuickPlayMode() unten gesetzt.
        // Sprint 5.1: _isDungeonRun ist Computed auf _currentMode.
        // Sprint 5.1 AAA-Audit #11: _isMasterMode ist jetzt Computed auf _currentMode.
        // Sprint 5.1: _isBossRushMode ist Computed auf _currentMode.
        // Sprint 5.1: _isDailyRace ist Computed auf _currentMode.
        // v2.0.50 — Phase 7: Difficulty wird in QuickPlayMode gehalten (im Konstruktor geclamped 1-10).
        _currentMode = new BomberBlast.Core.Modes.QuickPlayMode(difficulty);
        _activeMutator = LevelMutator.None;
        _currentLevelNumber = difficulty * 10; // Für Welt-Palette
        _currentLevel = LevelLayoutGenerator.GenerateQuickPlayLevel(seed, difficulty);
        _continueUsed = true; // Kein Continue im Quick-Play

        _player.ResetForNewGame();
        ApplyHeroStats();   // Sprint 7.1 AAA-Audit #21: Hero-Stats VOR Upgrades anwenden.
        ApplyUpgrades();
        await LoadLevelAsync();

        _soundManager.PlayMusic(_currentLevel.MusicTrack == "boss"
            ? SoundManager.MUSIC_BOSS
            : SoundManager.MUSIC_GAMEPLAY);

        _worldAnnouncementText = "QUICK PLAY";
        _worldAnnouncementTimer = 2.0f;
    }

    /// <summary>
    /// Survival-Modus starten (endlos, ohne Exit, Kill-basiertes Scoring)
    /// </summary>
    public async Task StartSurvivalModeAsync()
    {
        _cinematic?.Stop();  // v2.0.55 P1-Fix
        // Sprint 5.1: _isDailyChallenge ist Computed auf _currentMode.
        // Sprint 5.1: _isSurvivalMode wird durch _currentMode = new SurvivalMode() unten gesetzt.
        // Sprint 5.1: _isQuickPlayMode ist Computed auf _currentMode.
        // Sprint 5.1: _isDungeonRun ist Computed auf _currentMode.
        // Sprint 5.1 AAA-Audit #11: _isMasterMode ist jetzt Computed auf _currentMode.
        // Sprint 5.1: _isBossRushMode ist Computed auf _currentMode.
        // Sprint 5.1: _isDailyRace ist Computed auf _currentMode.
        _currentMode = new BomberBlast.Core.Modes.SurvivalMode();
        _activeMutator = LevelMutator.None;
        _currentLevelNumber = 1;
        _currentLevel = LevelLayoutGenerator.GenerateSurvivalLevel();
        _continueUsed = true; // Kein Continue im Survival

        // v2.0.51 — Phase 8: State liegt in SurvivalMode (im _currentMode-Slot).
        // SurvivalMode-Defaults: TimeElapsed=0, SpawnInterval=4s. Erster Spawn nach 4s setzen.
        var survivalState = (BomberBlast.Core.Modes.SurvivalMode)_currentMode!;
        survivalState.TimeElapsed = 0f;
        survivalState.SpawnTimer = 4f;       // Erster Spawn nach 4s
        survivalState.SpawnInterval = 4f;

        _player.ResetForNewGame();
        ApplyHeroStats();   // Sprint 7.1 AAA-Audit #21: Hero-Stats VOR Upgrades anwenden.
        ApplyUpgrades();
        _player.Lives = 1; // Nur 1 Leben im Survival (kein Shop-Bonus)

        await LoadLevelAsync();

        _soundManager.PlayMusic(SoundManager.MUSIC_GAMEPLAY);

        _worldAnnouncementText = "SURVIVAL!";
        _worldAnnouncementTimer = 2.5f;
    }

    /// <summary>
    /// Daily-Race-Modus starten (v2.0.42, Plan Task 3.1): identisches Level fuer alle Spieler weltweit
    /// (Seed via ILeagueService.GetDailyRaceSeed). Score wird nach GameOver via SubmitDailyRaceScoreAsync gepusht.
    /// </summary>
    public async Task StartDailyRaceModeAsync()
    {
        _cinematic?.Stop();  // v2.0.55 P1-Fix
        // Sprint 5.1: _isDailyChallenge ist Computed auf _currentMode.
        // Sprint 5.1: _isDailyRace wird durch _currentMode = new DailyRaceMode() unten gesetzt.
        // Sprint 5.1: _isSurvivalMode ist Computed auf _currentMode.
        // Sprint 5.1: _isQuickPlayMode ist Computed auf _currentMode.
        // Sprint 5.1: _isDungeonRun ist Computed auf _currentMode.
        // Sprint 5.1 AAA-Audit #11: _isMasterMode ist jetzt Computed auf _currentMode.
        // Sprint 5.1: _isBossRushMode ist Computed auf _currentMode.
        // v2.0.50 — Phase 7: DailyRaceMode mit Submitted=false initialisiert (Default-Property)
        _currentMode = new BomberBlast.Core.Modes.DailyRaceMode();
        _activeMutator = LevelMutator.None;
        _currentLevelNumber = 99;
        var seed = _leagueService.GetDailyRaceSeed(DateTime.UtcNow);
        _currentLevel = LevelLayoutGenerator.GenerateDailyChallengeLevel(seed);
        _continueUsed = true; // Kein Continue im Daily Race

        // Sprint 5.2 AAA-Audit #12: Daily-Race → deterministisch fuer alle Spieler.
        SetDeterministicSeed((ulong)seed);

        _player.ResetForNewGame();
        ApplyHeroStats();   // Sprint 7.1 AAA-Audit #21: Hero-Stats VOR Upgrades anwenden.
        ApplyUpgrades();
        await LoadLevelAsync();

        _soundManager.PlayMusic(SoundManager.MUSIC_GAMEPLAY);
        _worldAnnouncementText = "DAILY RACE";
        _worldAnnouncementTimer = 2.5f;
    }

    /// <summary>
    /// Boss-Rush-Modus starten (v2.0.42, Plan Task 3.3): einen der 5 Bosse hintereinander.
    /// bossIndex 0-4 mappt auf BossRushService.BossSequence (StoneGolem→IceDragon→FireDemon→ShadowMaster→FinalBoss).
    /// Score wird in <c>_bossRushAccumulatedScore</c> aufaddiert. Bei Boss-Tod kommt der naechste,
    /// bei Tod / 5. Boss-Clear wird via SubmitRun gemeldet.
    /// </summary>
    public async Task StartBossRushModeAsync(int bossIndex)
    {
        // Sprint 5.1: _isDailyChallenge ist Computed auf _currentMode.
        // Sprint 5.1: _isSurvivalMode ist Computed auf _currentMode.
        // Sprint 5.1: _isQuickPlayMode ist Computed auf _currentMode.
        // Sprint 5.1: _isDungeonRun ist Computed auf _currentMode.
        // Sprint 5.1 AAA-Audit #11: _isMasterMode ist jetzt Computed auf _currentMode.
        // Sprint 5.1: _isBossRushMode wird durch _currentMode = new BossRushMode() unten gesetzt.
        // v2.0.49 — Boss-Rush-Mode setzen (bei Erst-Aufruf bossIndex=0 neuer Mode-State,
        // bei Folge-Bossen wird der existing Mode beibehalten damit AccumulatedScore stimmt)
        if (bossIndex <= 0 || _currentMode is not BomberBlast.Core.Modes.BossRushMode)
            _currentMode = new BomberBlast.Core.Modes.BossRushMode();
        _activeMutator = LevelMutator.None;
        _continueUsed = true; // Kein Continue im Boss-Rush

        if (bossIndex < 0 || bossIndex >= _bossRushService.BossSequence.Count)
            bossIndex = 0;

        // v2.0.50 — Phase 7: Mode-State liegt in BossRushMode-Instanz.
        // _currentMode wurde bereits oben auf BossRushMode gesetzt.
        var bossRushState = (BomberBlast.Core.Modes.BossRushMode)_currentMode!;
        if (bossIndex == 0)
        {
            // Bei erstem Boss: State zuruecksetzen + Submit-Flag freigeben
            bossRushState.BossIndex = 0;
            bossRushState.AccumulatedScore = 0;
            bossRushState.TotalTimeSeconds = 0f;
            bossRushState.Submitted = false;
            _player.ResetForNewGame();
            ApplyUpgrades();
        }
        else
        {
            bossRushState.BossIndex = bossIndex;
            // Score-Akkumulation passiert im UpdateLevelComplete-Branch BEVOR diese Methode aufgerufen wird
            // (im Auto-Switch-Pfad). Bei Direkt-Aufruf von Aussen mit bossIndex > 0 (selten, z.B. Test):
            // Score wuerde nicht akkumulieren — dokumentierter Edge-Case.
            // Spieler heilen + Stats behalten (kein voller Reset).
            _player.HasShield = true;
        }

        // Story-Boss-Level fuer den entsprechenden Boss-Typ generieren — boss-spezifischer Welt-Build
        var bossType = _bossRushService.BossSequence[bossIndex];
        int storyLevelForBoss = bossType switch
        {
            Models.Entities.BossType.StoneGolem => 10,
            Models.Entities.BossType.IceDragon => 30,
            Models.Entities.BossType.FireDemon => 50,
            Models.Entities.BossType.ShadowMaster => 70,
            Models.Entities.BossType.FinalBoss => 100,
            _ => 10
        };
        _currentLevelNumber = storyLevelForBoss;
        _currentLevel = LevelLayoutGenerator.GenerateLevel(storyLevelForBoss, int.MaxValue);

        await LoadLevelAsync();

        _soundManager.PlayMusic(SoundManager.MUSIC_BOSS);
        _worldAnnouncementText = $"BOSS {bossIndex + 1} / 5";
        _worldAnnouncementTimer = 2.5f;
    }

    /// <summary>
    /// Dungeon-Floor starten (Roguelike-Modus)
    /// </summary>
    public async Task StartDungeonFloorAsync(int floor, int seed)
    {
        _cinematic?.Stop();  // v2.0.55 P1-Fix
        // Sprint 5.1: _isDailyChallenge ist Computed auf _currentMode.
        // Sprint 5.1: _isSurvivalMode ist Computed auf _currentMode.
        // Sprint 5.1: _isQuickPlayMode ist Computed auf _currentMode.
        // Sprint 5.1: _isDungeonRun wird durch _currentMode = new DungeonMode() unten gesetzt.
        // Sprint 5.1 AAA-Audit #11: _isMasterMode ist jetzt Computed auf _currentMode.
        // Sprint 5.1: _isBossRushMode ist Computed auf _currentMode.
        // Sprint 5.1: _isDailyRace ist Computed auf _currentMode.
        // v2.0.49 — Dungeon-Mode setzen (nur beim ersten Floor; bei Folge-Floors bleibt der Mode)
        if (_currentMode is not BomberBlast.Core.Modes.DungeonMode)
            _currentMode = new BomberBlast.Core.Modes.DungeonMode();
        _activeMutator = LevelMutator.None;
        _currentLevelNumber = Math.Min(floor * 10, 100); // Floor → Schwierigkeit (World-Mapping)

        // Raum-Typ + Modifikator aus DungeonService lesen
        var runState = _dungeonService.RunState;
        var roomType = runState?.CurrentRoomType ?? DungeonRoomType.Normal;
        var challengeMode = runState?.CurrentChallengeMode ?? DungeonChallengeMode.SpeedRun;
        var floorModifier = runState?.CurrentModifier ?? DungeonFloorModifier.None;

        // Rest-Raum: Kein Kampf → automatisch Buff-Auswahl triggern (kein Level laden)
        if (roomType == DungeonRoomType.Rest)
        {
            _continueUsed = true;
            // Rest-Raum triggert sofort Buff-Auswahl + Heilung
            DungeonBuffSelection?.Invoke();
            return;
        }

        _currentLevel = LevelLayoutGenerator.GenerateDungeonFloor(floor, seed, roomType, challengeMode, floorModifier);
        _continueUsed = true; // Kein Continue im Dungeon

        if (floor == 1)
        {
            _player.ResetForNewGame();
            ApplyUpgrades();
            // Dungeon-Buffs anwenden
            ApplyDungeonBuffs();
            _player.Lives = _dungeonService.RunState?.Lives ?? 1;
        }
        else
        {
            // Zwischen-Floors: HP behalten, Buffs anwenden
            ApplyDungeonBuffs();
            _player.Lives = _dungeonService.RunState?.Lives ?? 1;
        }

        await LoadLevelAsync();

        // Floor-Modifikator anwenden (nach Level-Generierung)
        ApplyDungeonFloorModifier(floorModifier);

        var isBoss = DungeonBuffCatalog.IsBossFloor(floor);
        _soundManager.PlayMusic(isBoss ? SoundManager.MUSIC_BOSS : SoundManager.MUSIC_DUNGEON);

        // Ankündigung mit Raum-Typ (lokalisiert)
        string roomLabel = roomType switch
        {
            DungeonRoomType.Elite => _localizationService.GetString("AnnounceElite") ?? "ELITE",
            DungeonRoomType.Treasure => _localizationService.GetString("AnnounceTreasure") ?? "TREASURE",
            DungeonRoomType.Challenge => _localizationService.GetString("AnnounceChallenge") ?? "CHALLENGE",
            _ => ""
        };
        string floorText = isBoss
            ? string.Format(_localizationService.GetString("AnnounceBossFloor") ?? "BOSS - FLOOR {0}", floor)
            : string.Format(_localizationService.GetString("AnnounceFloor") ?? "FLOOR {0}", floor);
        if (!string.IsNullOrEmpty(roomLabel))
            floorText = $"{roomLabel} - {string.Format(_localizationService.GetString("AnnounceFloor") ?? "FLOOR {0}", floor)}";
        _worldAnnouncementText = floorText;
        _worldAnnouncementTimer = 2.5f;
    }

    /// <summary>
    /// Wendet Floor-Modifikator-Effekte an (nach Level-Generierung).
    /// Einige Modifikatoren ändern Grid-Zellen oder Spieler-Stats für den aktuellen Floor.
    /// </summary>
    private void ApplyDungeonFloorModifier(DungeonFloorModifier modifier)
    {
        _dungeonFloorModifier = modifier;
        _dungeonModifierRegenTimer = 0;

        switch (modifier)
        {
            case DungeonFloorModifier.LavaBorders:
                // Äußere Reihe = Lava (sofortiger Tod)
                for (int x = 0; x < GameGrid.WIDTH; x++)
                {
                    var top = _grid.TryGetCell(x, 0);
                    var bottom = _grid.TryGetCell(x, GameGrid.HEIGHT - 1);
                    if (top != null && top.Type == CellType.Empty) { top.IsLavaActive = true; top.LavaTimer = 999f; _specialEffectCells.Add(top); }
                    if (bottom != null && bottom.Type == CellType.Empty) { bottom.IsLavaActive = true; bottom.LavaTimer = 999f; _specialEffectCells.Add(bottom); }
                }
                for (int y = 1; y < GameGrid.HEIGHT - 1; y++)
                {
                    var left = _grid.TryGetCell(0, y);
                    var right = _grid.TryGetCell(GameGrid.WIDTH - 1, y);
                    if (left != null && left.Type == CellType.Empty) { left.IsLavaActive = true; left.LavaTimer = 999f; _specialEffectCells.Add(left); }
                    if (right != null && right.Type == CellType.Empty) { right.IsLavaActive = true; right.LavaTimer = 999f; _specialEffectCells.Add(right); }
                }
                break;

            case DungeonFloorModifier.FastBombs:
                // Wird in PlaceBomb() berücksichtigt (50% kürzere Zündschnur)
                _dungeonBombFuseReduction += Bomb.DEFAULT_FUSE_TIME * 0.5f;
                break;

            case DungeonFloorModifier.BigExplosions:
                // Alle Explosionen +2 Range
                _player.FireRange += 2;
                break;

            case DungeonFloorModifier.Regeneration:
                // Shield-Regeneration nach 15s → wird in UpdatePlaying() geprüft
                // (nutzt eigenen Timer, schneller als Festungs-Synergy)
                break;

            case DungeonFloorModifier.Darkness:
                // Fog wird bereits in LevelGenerator gesetzt
                break;

            case DungeonFloorModifier.DoubleSpawns:
                // Gegner-Verdopplung bereits in LevelGenerator
                break;

            case DungeonFloorModifier.Wealthy:
                // Coin-Multiplikator in DungeonService.CompleteFloor()
                break;
        }
    }

    /// <summary>
    /// v2.0.41 Plan Task 3.2: Wendet das gespeicherte Loadout fuer das gegebene Level an.
    /// Bezahlung ist bereits durch <see cref="ILoadoutService.Purchase"/> erfolgt — hier nur Effekt-Anwendung.
    /// Loadout wird nicht hier konsumiert: erst nach erfolgreichem CompleteLevel via ClearLoadout.
    /// </summary>
    private void ApplyLoadoutBoosts(int levelNumber)
    {
        var loadout = _loadoutService.GetSavedLoadout(levelNumber);
        if (loadout.Count == 0) return;

        // Konstanten aus Player.cs gespiegelt (dort private). Aenderungen mitziehen.
        const int MAX_BOMB_COUNT = 10;
        const int MAX_FIRE_RANGE = 10;
        const int MAX_SPEED_LEVEL = 3;

        foreach (var boost in loadout)
        {
            switch (boost.Type)
            {
                case LoadoutBoostType.ExtraBomb:
                    _player.MaxBombs = Math.Min(_player.MaxBombs + 1, MAX_BOMB_COUNT);
                    break;
                case LoadoutBoostType.ExtraFire:
                    _player.FireRange = Math.Min(_player.FireRange + 1, MAX_FIRE_RANGE);
                    break;
                case LoadoutBoostType.SpeedBoost:
                    _player.SpeedLevel = MAX_SPEED_LEVEL;
                    break;
                case LoadoutBoostType.Wallpass:
                    _player.HasWallpass = true;
                    break;
                case LoadoutBoostType.Invincibility:
                    _player.ActivateInvincibility(30f);
                    break;
            }
        }
    }

    /// <summary>
    /// Wendet aktive Dungeon-Buffs auf den Spieler an
    /// </summary>
    private void ApplyDungeonBuffs()
    {
        var state = _dungeonService.RunState;
        if (state?.ActiveBuffs == null) return;

        // Legendäre Buff-Flags zurücksetzen
        _timeFreezeTimer = 0;
        _phantomWalkAvailable = false;
        _phantomWalkActive = false;
        _phantomWalkTimer = 0;
        _phantomCooldownTimer = 0;
        _dungeonEnemySlowActive = false;
        _dungeonBombFuseReduction = 0;

        foreach (var buff in state.ActiveBuffs)
        {
            switch (buff)
            {
                case DungeonBuffType.ExtraBomb:
                    _player.MaxBombs++;
                    break;
                case DungeonBuffType.ExtraFire:
                    _player.FireRange++;
                    break;
                case DungeonBuffType.SpeedBoost:
                    _player.SpeedLevel = Math.Min(_player.SpeedLevel + 1, 3);
                    break;
                case DungeonBuffType.Shield:
                    _player.HasShield = true;
                    break;
                case DungeonBuffType.FireImmunity:
                    _player.HasFlamepass = true;
                    break;
                case DungeonBuffType.BlastRadius:
                    _player.FireRange += 2;
                    break;

                // Legendäre Buffs
                case DungeonBuffType.Berserker:
                    // +2 Bomben, +2 Feuer (Leben-Abzug bereits in DungeonService.ApplyBuff)
                    _player.MaxBombs += 2;
                    _player.FireRange += 2;
                    break;
                case DungeonBuffType.TimeFreeze:
                    // Wird nach LoadLevelAsync() aktiviert (3s Freeze bei Floor-Start)
                    _timeFreezeTimer = 3f;
                    break;
                case DungeonBuffType.BombTimer:
                    // Basis: -0.5s Zündschnur (wird via _dungeonBombFuseReduction angewendet)
                    break;
                case DungeonBuffType.EnemySlow:
                    // Basis: Gegner 20% langsamer
                    _dungeonEnemySlowActive = true;
                    break;
                case DungeonBuffType.GoldRush:
                    // Coin-Multiplikator wird in DungeonService.CompleteFloor() angewendet
                    break;
                case DungeonBuffType.Phantom:
                    // 5s durch Wände laufen, 30s Cooldown - Aktivierung per Spieler-Input
                    _phantomWalkAvailable = true;
                    break;
            }
        }

        // Synergien per pure Resolver auswerten (v2.0.39: Logik in Core/Dungeon/DungeonSynergyResolver.cs)
        var synergy = Dungeon.DungeonSynergyResolver.Resolve(state.ActiveBuffs);

        _synergyBlitzkriegActive = synergy.Blitzkrieg;
        _synergyFortressActive = synergy.Fortress;
        _fortressRegenTimer = 0;
        _synergyMidasActive = synergy.Midas;
        _synergyElementalActive = synergy.Elemental;
        _dungeonBombFuseReduction = synergy.BombFuseReduction;

        // Bombardier-Bonus: ExtraBomb + ExtraFire → nochmal +1 auf beides (sofort angewandt).
        if (synergy.Bombardier)
        {
            _player.MaxBombs++;
            _player.FireRange++;
        }
    }

    /// <summary>
    /// Level laden und initialisieren
    /// </summary>
    private async Task LoadLevelAsync()
    {
        if (_currentLevel == null)
            return;

        // State zurücksetzen
        _state = GameState.Starting;
        _stateTimer = 0;
        CacheStartingOverlayStrings();
        _bombsUsed = 0;
        _enemiesKilled = 0;
        _exitRevealed = false;
        _exitCell = null;
        _scoreAtLevelStart = _player.Score;
        _playerDamagedThisLevel = false;
        // Sprint 2.2 AAA-Audit #2: Funnel-Telemetrie reset.
        _levelElapsedSeconds = 0f;
        _deathsInLevel = 0;

        // Deck-Telemetrie: Counter für dieses Level zurücksetzen
        _specialBombTypesUsedInLevel.Clear();

        // Entities leeren
        _enemies.Clear();
        _enemiesRemainingDirty = true;
        _bombs.Clear();
        _explosions.Clear();
        _powerUps.Clear();
        _enemyPositionIndex.Clear();
        _enemyPositionHashSet.Clear();
        _destroyingCells.Clear();
        _afterglowCells.Clear();
        _specialEffectCells.Clear();
        _pendingIceCleanups.Clear();
        _particleSystem.Clear();
        _floatingText.Clear();
        _screenShake.Reset();
        _hitPauseTimer = 0;
        _comboSystem.Reset();
        _pontanPunishmentActive = false;
        _pontanSpawned = 0;
        _pontanInitialDelay = 0;
        _pontanEarlyWarningTriggered = false;
        _pontanFinalWarningTriggered = false;
        _defeatAllCooldown = 0;
        _fallingCeilingTimer = 0;
        _earthquakeTimer = 0;

        // Grid aufbauen
        _grid.Reset();

        // Layout-Pattern verwenden (oder Classic als Fallback)
        if (_currentLevel.Layout.HasValue)
            _grid.SetupLayoutPattern(_currentLevel.Layout.Value);
        else
            _grid.SetupClassicPattern();

        // Sprint 5.2 AAA-Audit #12: Level-Seed wird zum Re-Seed des IRngProvider verwendet.
        // Im Daily/Daily-Race-Mode bereits durch SetDeterministicSeed gesetzt; bei anderen
        // Modi bleibt das hier als zusaetzliches Re-Seed pro Level fuer Generator-Reproduzierbarkeit.
        if (_currentLevel.Seed.HasValue)
        {
            SetDeterministicSeed((ulong)_currentLevel.Seed.Value);
        }
        var random = new Random(_currentLevel.Seed ?? Environment.TickCount);

        // Welt-Mechanik-Zellen platzieren (VOR Blöcken, damit Blöcke nur auf leere Zellen kommen)
        _mechanicCells.Clear();
        if (_currentLevel.Mechanic != WorldMechanic.None)
        {
            _grid.PlaceWorldMechanicCells(_currentLevel.Mechanic, random);

            // Mechanik-Zellen cachen (Teleporter/LavaCrack brauchen pro-Frame-Update)
            for (int cy = 0; cy < _grid.Height; cy++)
                for (int cx = 0; cx < _grid.Width; cx++)
                {
                    var c = _grid[cx, cy];
                    if (c.Type == CellType.Teleporter || c.Type == CellType.LavaCrack)
                        _mechanicCells.Add(c);
                }
        }

        // Blöcke platzieren (überspringt Spezial-Zellen automatisch)
        _grid.PlaceBlocks(_currentLevel.BlockDensity, random);

        // Spieler spawnen bei (1,1)
        _player.SetGridPosition(1, 1);
        _player.MovementDirection = Direction.None;
        _inputManager.Reset(); // Input-State zurücksetzen (verhindert Geister-Bewegung im nächsten Level)

        // Level-Generierung via LevelGenerator (extrahiert aus GameEngine.Level.cs, v2.0.30+)
        var genCtx = BuildGenerationContext(random);

        // PowerUps in Blöcken platzieren (mutiert Grid-Zellen direkt)
        _levelGenerator.PlacePowerUps(genCtx);

        // Exit unter einem Block platzieren (nicht im Survival-Modus)
        if (!_isSurvivalMode)
            _levelGenerator.PlaceExit(genCtx);

        // Gegner spawnen — Generator gibt Liste zurueck, wir haengen sie in _enemies und tracken Boss-Encounter
        var spawnedEnemies = _levelGenerator.SpawnEnemies(genCtx);
        _enemies.AddRange(spawnedEnemies);
        _enemiesRemainingDirty = true;
        foreach (var e in spawnedEnemies)
        {
            if (e is BossEnemy boss)
                _tracking.OnBossEncountered(boss.BossKind);
        }
        _originalEnemyCount = _enemies.Count;

        // Welt-Theme setzen (basierend auf Level-Nummer)
        int worldIndex = (_currentLevelNumber - 1) / 10;
        _renderer.SetWorldTheme(worldIndex);

        // Nebel aktivieren fuer Schattenwelt (Welt 10)
        _renderer.SetFogEnabled(_currentLevel.Mechanic == WorldMechanic.Fog);

        // Fog-of-War (v2.0.35): Ab L50 Normal-Modus ODER Master-Modus ab L1.
        // Welt 10 nutzt das simplere FogOverlay (oben) — FoW nur für L50-90.
        // Sichtradius schrumpft mit Schwierigkeit: L50-59 → 5, L60-89 → 4, Master → 4.
        bool isStoryMode = !_isDailyChallenge && !_isSurvivalMode && !_isQuickPlayMode && !_isDungeonRun;
        bool fowActive = isStoryMode
            && (_isMasterMode || (_currentLevelNumber >= 50 && _currentLevel.Mechanic != WorldMechanic.Fog));
        if (fowActive)
        {
            int radius = (_currentLevelNumber >= 60 || _isMasterMode) ? 4 : 5;
            _renderer.FogOfWar.Enable(_grid.Width, _grid.Height, radius);
        }
        else
        {
            _renderer.FogOfWar.Disable();
        }

        // Timer zurücksetzen
        _timer.Reset(_currentLevel.TimeLimit);

        // Spieler aktivieren
        _player.IsActive = true;

        // Tutorial starten bei Level 1 wenn noch nicht abgeschlossen
        if (_currentLevelNumber == 1 && !_tutorialService.IsCompleted)
        {
            _tutorialService.Start();
            _tutorialWarningTimer = 0;
        }

        // Discovery-Hint für Welt-Mechanik (bei erstem Kontakt)
        if (_currentLevel.Mechanic != WorldMechanic.None)
        {
            TryShowDiscoveryHint("mechanic_" + _currentLevel.Mechanic.ToString().ToLower());
        }

        // Phase 18 — Mode-Lifecycle: Initialize-Hook für IGameMode aufrufen.
        // Reihenfolge ist wichtig: NACH Player-Reset, NACH Grid-Setup, NACH Timer-Reset.
        // Modi können hier ihren initialen State setzen (Survival-SpawnTimer, BossRush-StartTime, ...).
        _modeTimeElapsed = 0f;
        try { _currentMode?.Initialize(BuildModeContext()); }
        catch { /* Best-Effort, no-op-Default in GameModeBase */ }
    }

    /// <summary>
    /// Shop-Upgrades auf den Spieler anwenden.
    /// Im Dungeon: Nur Base-Stats (Shop-Bonuse gelten nicht, Dungeon-Buffs werden separat addiert).
    /// In Story/Daily/QuickPlay/Survival: Volle Shop-Bonuse.
    /// </summary>
    /// <summary>
    /// Sprint 6.1 AAA-Audit #15: Boss-Modifier Summoner spawnt einen Mini-Enemy (Ballom)
    /// in der Naehe des Bosses. Wird alle 8s aufgerufen, max 4 Minions gleichzeitig.
    /// </summary>
    private void SpawnSummonerMinion(BossEnemy boss)
    {
        // Cap bei 4 lebenden Minions vom Summoner — keine Spam-Spawn-Spirale.
        int liveMinions = 0;
        foreach (var e in _enemies)
            if (e.Type == EnemyType.Ballom && e.IsActive && !e.IsDying)
                liveMinions++;
        if (liveMinions >= 4) return;

        // Spawn-Position: Eine der 4 Nachbar-Zellen vom Boss (free + not blocked).
        var rng = new Random();
        var offsets = new (int dx, int dy)[] { (-1, 0), (1, 0), (0, -1), (0, 1) };
        for (int i = 0; i < 4; i++) (offsets[i], offsets[rng.Next(4)]) = (offsets[rng.Next(4)], offsets[i]);

        foreach (var (dx, dy) in offsets)
        {
            int tx = boss.GridX + dx;
            int ty = boss.GridY + dy;
            var cell = _grid.TryGetCell(tx, ty);
            if (cell?.Type != CellType.Empty || cell.Bomb != null) continue;
            // Mini-Ballom spawnen (1x Speed, kein Elite — bewusst schwach).
            var minion = new Enemy(
                tx * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f,
                ty * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f,
                EnemyType.Ballom);
            _enemies.Add(minion);
            // Visueller Spawn-Effekt: kleine Lila-Partikel (Summoner-Theme).
            _particleSystem.Emit(minion.X, minion.Y, 8, new SKColor(200, 100, 255), 60f, 0.5f);
            return;
        }
    }

    private void ApplyHeroStats()
    {
        // Sprint 7.1 AAA-Audit #21: Hero-Definition vom aktiven Hero anwenden.
        // Wird VOR ApplyUpgrades aufgerufen damit Shop-Bonuse die Hero-Werte erhoehen koennen.
        // Im Dungeon werden Hero-Stats ueberschrieben (ApplyUpgrades setzt dort eigene Base-Stats).
        var hero = _heroService.ActiveHero;
        _player.MaxBombs = hero.StartMaxBombs;
        _player.FireRange = hero.StartFireRange;
        _player.SpeedLevel = hero.StartSpeedLevel;
        _player.Lives = hero.StartLives;
    }

    private void ApplyUpgrades()
    {
        if (_isDungeonRun)
        {
            // Dungeon: Verbesserte Base-Stats (Shop-Bonuse gelten nicht, Dungeon-Buffs werden separat addiert)
            _player.MaxBombs = 2;
            _player.FireRange = 2;
            _player.HasSpeed = true;
            _player.Lives = 1;
            _player.HasShield = false;

            // Permanente Dungeon-Upgrades (gekauft mit DungeonCoins)
            int startBombs = _dungeonUpgradeService.GetUpgradeLevel(DungeonUpgradeCatalog.StartingBombs);
            if (startBombs > 0) _player.MaxBombs += startBombs;

            int startFire = _dungeonUpgradeService.GetUpgradeLevel(DungeonUpgradeCatalog.StartingFire);
            if (startFire > 0) _player.FireRange += startFire;

            int startSpeed = _dungeonUpgradeService.GetUpgradeLevel(DungeonUpgradeCatalog.StartingSpeed);
            if (startSpeed > 0) _player.SpeedLevel = Math.Min(_player.SpeedLevel + 1, 3);

            int startShield = _dungeonUpgradeService.GetUpgradeLevel(DungeonUpgradeCatalog.StartingShield);
            if (startShield > 0) _player.HasShield = true;
        }
        else
        {
            // Story/Daily/QuickPlay/Survival: Shop-Bonuse anwenden
            _player.MaxBombs = _shopService.GetStartBombs();
            _player.FireRange = _shopService.GetStartFire();
            _player.HasSpeed = _shopService.HasStartSpeed();
            _player.Lives = _shopService.GetStartLives();
            _player.HasShield = _shopService.Upgrades.GetLevel(UpgradeType.ShieldStart) >= 1;
        }

        // Karten-Deck laden (beide Modi - Karten sind separate Mechanik)
        if (!_isDungeonRun && !_tracking.Cards.HasMigrated)
        {
            _tracking.Cards.MigrateFromShop(
                _shopService.HasIceBomb(),
                _shopService.HasFireBomb(),
                _shopService.HasStickyBomb());
        }

        // Ausgerüstete Karten für dieses Level laden (mit frischen Uses pro Level)
        _player.EquippedCards = _tracking.Cards.GetEquippedCardsForGameplay();
        _player.ActiveCardSlot = -1; // Startet immer auf Normalbombe
    }

    /// <summary>
    /// Baut den Context fuer den LevelGenerator aus Engine-State.
    /// Zentraler Ort damit Generator-Aufrufe nicht jedes Mal die Felder durchreichen.
    /// </summary>
    private BomberBlast.Core.LevelGeneration.LevelGenerationContext BuildGenerationContext(Random random)
    {
        return new BomberBlast.Core.LevelGeneration.LevelGenerationContext
        {
            Grid = _grid,
            CurrentLevel = _currentLevel!,
            Random = random,
            PowerUpLuckLevel = _shopService.Upgrades.GetLevel(UpgradeType.PowerUpLuck)
        };
    }

    private void CheckExitReveal()
    {
        if (_exitRevealed || _isSurvivalMode)
            return;

        // Manuelle Schleife statt LINQ (wird pro Enemy-Kill aufgerufen)
        foreach (var enemy in _enemies)
        {
            if (enemy.IsActive && !enemy.IsDying)
                return;
        }

        RevealExit();
    }

    private void RevealExit()
    {
        _exitRevealed = true;

        // Zuerst: Versteckten Exit-Block suchen und dort aufdecken
        for (int x = 1; x < GameGrid.WIDTH - 1; x++)
        {
            for (int y = 1; y < GameGrid.HEIGHT - 1; y++)
            {
                var cell = _grid[x, y];
                if (cell.HasHiddenExit)
                {
                    cell.HasHiddenExit = false;
                    // Block wird zum Exit (auch wenn er noch nicht zerstört wurde)
                    cell.Type = CellType.Exit;
                    cell.IsDestroying = false;
                    cell.DestructionProgress = 0;
                    _exitCell = cell;
                    _soundManager.PlaySound(SoundManager.SFX_EXIT_APPEAR);

                    float epx = cell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                    float epy = cell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                    _particleSystem.Emit(epx, epy, 12, ParticleColors.ExitReveal, 60f, 0.8f);
                    _particleSystem.Emit(epx, epy, 6, ParticleColors.ExitRevealLight, 40f, 0.5f);

                    // Goldener Floating-Text: Ausgang gefunden!
                    var exitText = _localizationService.GetString("ExitRevealed") ?? "EXIT!";
                    _floatingText.Spawn(epx, epy - 20, exitText, BomberBlastColors.Gold, 22f, 2.0f);
                    _vibration.VibrateMedium();
                    return;
                }
            }
        }

        // Fallback: Kein versteckter Exit-Block gefunden → auf leerer Zelle platzieren
        Cell? bestCell = null;
        int bestDist = -1;

        for (int x = 1; x < GameGrid.WIDTH - 1; x++)
        {
            for (int y = 1; y < GameGrid.HEIGHT - 1; y++)
            {
                var cell = _grid[x, y];
                if (cell.Type != CellType.Empty || cell.Bomb != null || cell.PowerUp != null)
                    continue;

                int dist = Math.Abs(cell.X - _player.GridX) + Math.Abs(cell.Y - _player.GridY);
                if (dist > bestDist)
                {
                    bestDist = dist;
                    bestCell = cell;
                }
            }
        }

        // Letzter Fallback: Beliebige begehbare Zelle (ignoriert Bombs/PowerUps)
        if (bestCell == null)
        {
            for (int fx = 1; fx < GameGrid.WIDTH - 1 && bestCell == null; fx++)
                for (int fy = 1; fy < GameGrid.HEIGHT - 1 && bestCell == null; fy++)
                {
                    var fc = _grid[fx, fy];
                    if (fc.Type == CellType.Empty)
                        bestCell = fc;
                }
        }

        if (bestCell != null)
        {
            bestCell.Type = CellType.Exit;
            _exitCell = bestCell;
            _soundManager.PlaySound(SoundManager.SFX_EXIT_APPEAR);

            float epx = bestCell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float epy = bestCell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            _particleSystem.Emit(epx, epy, 12, ParticleColors.ExitReveal, 60f, 0.8f);
            _particleSystem.Emit(epx, epy, 6, ParticleColors.ExitRevealLight, 40f, 0.5f);

            // Goldener Floating-Text: Ausgang gefunden!
            var exitText = _localizationService.GetString("ExitRevealed") ?? "EXIT!";
            _floatingText.Spawn(epx, epy - 20, exitText, BomberBlastColors.Gold, 22f, 2.0f);
            _vibration.VibrateMedium();
        }
    }

    /// <summary>
    /// Master Mode Enemy-Upgrade: Ersetzt Low/Normal-Intelligence Gegner durch
    /// stärkere Varianten. Behält Position + IsMiniSplitter-Flag. Wird EINMAL nach
    /// LoadLevelAsync aufgerufen (Bosse + Spezial-Gegner bleiben unverändert).
    /// </summary>
    /// <remarks>
    /// Upgrade-Tabelle:
    /// Ballom → Minvo, Onil → Pass, Doll → Pontan,
    /// Minvo → Pass, Kondoria → Pontan, Ovapi → Pontan.
    /// Pass/Pontan (bereits Max-Intel) und Spezial-Typen
    /// (Tanker/Ghost/Splitter/Mimic) bleiben unverändert.
    /// </remarks>
    private void ApplyMasterModeEnemyUpgrade()
    {
        for (int i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (enemy is BossEnemy) continue;
            if (enemy.IsMiniSplitter) continue; // Splits bleiben Splitter

            var upgraded = enemy.Type switch
            {
                EnemyType.Ballom => EnemyType.Minvo,
                EnemyType.Onil => EnemyType.Pass,
                EnemyType.Doll => EnemyType.Pontan,
                EnemyType.Minvo => EnemyType.Pass,
                EnemyType.Kondoria => EnemyType.Pontan,
                EnemyType.Ovapi => EnemyType.Pontan,
                _ => enemy.Type // Pass, Pontan, Tanker, Ghost, Splitter, Mimic bleiben
            };

            if (upgraded != enemy.Type)
            {
                var replacement = Enemy.CreateAtGrid(enemy.GridX, enemy.GridY, upgraded);
                _enemies[i] = replacement;
            }
        }
    }

    private void UpdateEnemies(float deltaTime)
    {
        // Gefahrenzone EINMAL pro Frame vorberechnen (nicht pro Gegner → P-R6-1)
        _enemyAI.PreCalculateDangerZone(_bombs);

        foreach (var enemy in _enemies)
        {
            // Mimic-Aktivierung VOR dem IsActive-Check (Mimics sind !IsActive bis aktiviert)
            if (!enemy.IsDying && enemy.IsDisguised && enemy.Type == EnemyType.Mimic)
            {
                if (enemy.TryActivateMimic(_player.GridX, _player.GridY))
                {
                    // Aktivierungs-Effekt: Partikel + Warnung
                    _particleSystem.Emit(enemy.X, enemy.Y, 10, new SKColor(255, 50, 50), 80f, 0.5f);
                    _floatingText.Spawn(enemy.X, enemy.Y - 16,
                        _localizationService.GetString("FloatMimic") ?? "MIMIC!", SKColors.Red, 16f, 1.5f);
                    _soundManager.PlaySound(SoundManager.SFX_ENEMY_DEATH);
                }
                enemy.Update(deltaTime); // Getarnter Mimic braucht trotzdem Update (für Animation)
                continue;
            }

            if (!enemy.IsActive && !enemy.IsDying)
                continue;

            // Boss: Eigene Bewegungslogik + vereinfachte AI (Richtung zum Spieler)
            if (enemy is BossEnemy boss)
            {
                if (boss.IsActive && !boss.IsDying)
                {
                    // Verlangsamung: Frost (50%), TimeWarp (50%), BlackHole (70%) - kumulativ
                    // DoubleSpeed-Mutator + Master Mode: Gegner 50% schneller (nicht kombinierbar, max 1.5x)
                    float bossDt = (_activeMutator == LevelMutator.DoubleSpeed || _isMasterMode) ? deltaTime * 1.5f : deltaTime;
                    var bossCell = _grid.TryGetCell(boss.GridX, boss.GridY);
                    if (bossCell != null)
                    {
                        if (bossCell.IsFrozen) bossDt *= 0.5f;
                        if (bossCell.IsTimeWarped) bossDt *= 0.5f;
                        if (bossCell.IsBlackHole) bossDt *= 0.3f;
                        // Elementar-Synergy: Lava verlangsamt Gegner
                        if (bossCell.IsLavaActive && _synergyElementalActive) bossDt *= 0.4f;
                        // EnemySlow-Buff: Gegner 20% langsamer
                        if (_dungeonEnemySlowActive) bossDt *= 0.8f;
                    }

                    // Boss-AI: Bewegt sich auf den Spieler zu (vereinfacht, kein A*)
                    UpdateBossAI(boss, bossDt);
                    boss.MoveBoss(bossDt, _grid);
                }
                boss.Update(deltaTime);

                // Sprint 6.1 AAA-Audit #15: Boss-Modifier Summoner — wenn der Cooldown abgelaufen ist,
                // spawnt der Boss einen Mini-Enemy in der Naehe. Engine-side weil Enemy-Liste mutiert.
                if (boss.TryConsumeSummonRequest())
                {
                    SpawnSummonerMinion(boss);
                }

                continue;
            }

            if (enemy.IsActive && !enemy.IsDying)
            {
                // Verlangsamung: Frost (50%), TimeWarp (50%), BlackHole (70%) - kumulativ
                // DoubleSpeed-Mutator + Master Mode: Gegner 50% schneller (nicht kombinierbar, max 1.5x)
                float enemyDt = (_activeMutator == LevelMutator.DoubleSpeed || _isMasterMode) ? deltaTime * 1.5f : deltaTime;
                var enemyCell = _grid.TryGetCell(enemy.GridX, enemy.GridY);
                if (enemyCell != null)
                {
                    if (enemyCell.IsFrozen) enemyDt *= 0.5f;
                    if (enemyCell.IsTimeWarped) enemyDt *= 0.5f;
                    if (enemyCell.IsBlackHole) enemyDt *= 0.3f;
                    // Elementar-Synergy: Lava verlangsamt Gegner
                    if (enemyCell.IsLavaActive && _synergyElementalActive) enemyDt *= 0.4f;
                    // EnemySlow-Buff: Gegner 20% langsamer
                    if (_dungeonEnemySlowActive) enemyDt *= 0.8f;
                }

                _enemyAI.Update(enemy, _player, enemyDt);

                // Boss nutzt eigene Bewegungslogik (größere Kollisions-Box)
                if (enemy is BossEnemy bossEnemy)
                    bossEnemy.MoveBoss(enemyDt, _grid);
            }

            enemy.Update(deltaTime);
        }
    }

    /// <summary>
    /// Boss-AI: Vereinfachte Richtungswahl (bewegt sich auf Spieler zu, wechselt periodisch)
    /// Kein A*-Pathfinding, da der Boss zu groß dafür ist.
    /// </summary>
    private void UpdateBossAI(BossEnemy boss, float deltaTime)
    {
        // Während Telegraph/Angriff steht der Boss still
        if (boss.IsTelegraphing || boss.IsAttacking)
        {
            boss.MovementDirection = Direction.None;
            return;
        }

        // AI-Entscheidung alle 0.8s (Enraged: 0.5s)
        boss.AIDecisionTimer -= deltaTime;
        if (boss.AIDecisionTimer > 0 && boss.MovementDirection != Direction.None)
            return;

        boss.AIDecisionTimer = boss.IsEnraged ? 0.5f : 0.8f;

        // Richtung zum Spieler berechnen
        float dx = _player.X - boss.X;
        float dy = _player.Y - boss.Y;

        // Bevorzugt die Achse mit größerer Distanz
        Direction preferred;
        if (MathF.Abs(dx) > MathF.Abs(dy))
            preferred = dx > 0 ? Direction.Right : Direction.Left;
        else
            preferred = dy > 0 ? Direction.Down : Direction.Up;

        // Zufällig: 70% bevorzugte Richtung, 30% zufällig (damit Boss nicht perfekt verfolgt)
        if (EngineRngNextDouble() < 0.3)
        {
            var dirs = new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
            preferred = dirs[EngineRngNext(dirs.Length)];
        }

        // Duo-Boss: Ausweichen wenn bevorzugte Richtung zu Kollision mit anderem Boss führt
        if (_currentLevel?.BossKind2.HasValue == true)
        {
            foreach (var other in _enemies)
            {
                if (other == boss || other is not BossEnemy otherBoss || otherBoss.IsDying) continue;

                // Prüfe ob die nächste Position in der BoundingBox des anderen Bosses liegt
                int nextX = boss.GridX + preferred.GetDeltaX();
                int nextY = boss.GridY + preferred.GetDeltaY();
                if (otherBoss.OccupiesCell(nextX, nextY))
                {
                    // Ausweich-Richtung: senkrecht zur bevorzugten Richtung
                    preferred = preferred is Direction.Up or Direction.Down
                        ? (dx > 0 ? Direction.Right : Direction.Left)
                        : (dy > 0 ? Direction.Down : Direction.Up);
                    break;
                }
            }
        }

        boss.MovementDirection = preferred;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SURVIVAL-MODUS: Kontinuierliches Gegner-Spawning
    // ═══════════════════════════════════════════════════════════════════════
    // v2.0.39: Spawn-Logik in Modes/SurvivalSpawner.cs extrahiert (Context-Pattern).
    // Felder bleiben hier (Mode-Init / Tracking-Reads). Diese Methode delegiert per ref.

    /// <summary>
    /// Survival: Gegner spawnen in steigender Frequenz. Schwierigkeit nimmt mit der Zeit zu.
    /// v2.0.51 — Phase 8: SurvivalSpawner.Update nimmt SurvivalMode-Instanz statt ref-Parameter.
    /// </summary>
    private void UpdateSurvivalSpawning(float deltaTime)
    {
        if (SurvivalModeState is { } survivalMode)
        {
            Modes.SurvivalSpawner.Update(SurvivalCtx, deltaTime, survivalMode);
        }
    }

    private void UpdatePowerUps(float deltaTime)
    {
        foreach (var powerUp in _powerUps)
        {
            powerUp.Update(deltaTime);
        }
    }

    private void CompleteLevel()
    {
        // Idempotenz-Guard: Wenn bereits NICHT Playing (z.B. Spieler gerade gestorben oder Level bereits complete),
        // nicht nochmal ausfuehren. Schuetzt gegen Tod+Exit-Race im selben Frame.
        if (_state != GameState.Playing)
            return;

        _state = GameState.LevelComplete;
        _stateTimer = 0;
        _levelCompleteHandled = false;
        _timer.Pause();
        _vibration.VibrateLevelComplete();

        // Phase 18 — IGameMode.OnLevelComplete-Hook (ARCH-1 aus Phase-15-Audit).
        // Return-Wert ist aktuell informativ — wir feuern weiterhin den Engine-LevelComplete-Pfad.
        // Folge-Phasen können Mode-spezifische Pre-Logic einbauen (z.B. Score-Modifier, Reward-Override).
        try { _currentMode?.OnLevelComplete(BuildModeContext()); }
        catch { /* Best-Effort, no-op-Default in GameModeBase */ }

        // v2.0.46 — Audio-Caption für gehörlose Spieler
        if (_accessibility?.SubtitlesEnabled == true)
        {
            _subtitles.Show(_localizationService.GetString("SubtitleLevelComplete") ?? "[LEVEL COMPLETE]");
        }

        // Deck-Balancing-Telemetrie: Level-Erfolg mit allen eingesetzten Spezial-Bomben melden
        if (_specialBombTypesUsedInLevel.Count > 0)
        {
            _deckTelemetry.RecordLevelStartedWithBombs(_specialBombTypesUsedInLevel);
            _deckTelemetry.RecordLevelCompletedWithBombs(_specialBombTypesUsedInLevel);
        }

        // Enemy-Kill-Punkte merken (nur Level-Score, nicht kumulierter Gesamtscore)
        LastEnemyKillPoints = _player.Score - _scoreAtLevelStart;

        // Bonusberechnung mit Shop-Upgrades
        int timeBonusMultiplier = _shopService.GetTimeBonusMultiplier();
        // NoTimer-Mutator cappen: TimeLimit=99999 ergaebe timeBonus = 99999*20 = ~2 Mio Bonus-Punkte pro Level → Score-Farming.
        // Cap auf Level-spezifische maximale Sinnvolle Zeit (doppelte baseScore-Threshold aequivalent ~1,5min).
        float cappedRemainingTime = _currentLevel != null && _currentLevel.Mutator == LevelMutator.NoTimer
            ? 0f  // NoTimer = kein TimeBonus (Level ist bereits einfacher ohne Zeitdruck)
            : _timer.RemainingTime;
        int timeBonus = (int)cappedRemainingTime * timeBonusMultiplier;

        // Gestufter Effizienzbonus (skaliert nach Welt).
        // BAL-32 (18.04.2026): Welt 1 bekommt großzügigere Schwellen (8/14/20 statt 5/8/12),
        // weil Einsteiger noch kein Deck haben und mehr Blöcke räumen müssen. Ab Welt 2 gelten
        // die klassischen, strengeren Schwellen als Skill-Maßstab.
        int world = (_currentLevelNumber - 1) / 10; // 0-9
        int topBombThreshold = world == 0 ? 8 : 5;
        int midBombThreshold = world == 0 ? 14 : 8;
        int lowBombThreshold = world == 0 ? 20 : 12;

        int efficiencyBonus = 0;
        if (_bombsUsed <= topBombThreshold)
            efficiencyBonus = world switch { 0 => 4000, 1 => 6000, 2 => 8000, 3 => 12000, _ => 15000 };
        else if (_bombsUsed <= midBombThreshold)
            efficiencyBonus = world switch { 0 => 2500, 1 => 4000, 2 => 5000, 3 => 8000, _ => 10000 };
        else if (_bombsUsed <= lowBombThreshold)
            efficiencyBonus = world switch { 0 => 1500, 1 => 2000, 2 => 2500, 3 => 4000, _ => 5000 };

        // Score-Multiplikator NUR auf Level-Score anwenden (nicht den gesamten kumulierten Score)
        int levelScoreBeforeBonus = _player.Score - _scoreAtLevelStart;
        int levelTotal = levelScoreBeforeBonus + timeBonus + efficiencyBonus;

        float scoreMultiplier = _shopService.GetScoreMultiplier();
        if (scoreMultiplier > 1.0f)
        {
            levelTotal = (int)(levelTotal * scoreMultiplier);
        }

        _player.Score = _scoreAtLevelStart + levelTotal;

        // Score-Aufschlüsselung speichern
        LastTimeBonus = timeBonus;
        LastEfficiencyBonus = efficiencyBonus;
        LastScoreMultiplier = scoreMultiplier;
        CacheLevelCompleteOverlayStrings();

        _soundManager.PlaySound(SoundManager.SFX_LEVEL_COMPLETE);
        ScoreChanged?.Invoke(_player.Score);

        // Erster Sieg: Level 1 zum ersten Mal abgeschlossen
        _isFirstVictory = _currentLevelNumber == 1 && _progressService.HighestCompletedLevel == 0;
        if (_isFirstVictory)
        {
            // Extra Gold-Partikel für ersten Sieg
            _particleSystem.EmitShaped(_player.X, _player.Y, 24, BomberBlastColors.Gold,
                Graphics.ParticleShape.Circle, 150f, 1.0f, 3.5f, hasGlow: true);
            _particleSystem.EmitExplosionSparks(_player.X, _player.Y, 16, new SKColor(255, 200, 50), 180f);

            // Phase 24b — RetentionService.RegisterFirstWin: True wenn dies wirklich der erste Win EVER
            // (idempotent, kein zweiter Trigger nach App-Reinstall mit erhaltenem Cloud-Save).
            if (RetentionService?.RegisterFirstWin() == true)
            {
                PlayFirstWinCinematic();
            }
        }

        // Coins basierend auf Level-Score (nicht kumuliert, verhindert Inflation)
        // Welt 1 (Level 1-10): Score/2 statt Score/3 für bessere Früh-Progression.
        // Mutator-Level: Coin-Basis auf Max(echter Score, baseScore*3) — sonst bekommt
        // Spieler 3 Sterne aber weniger Coins (timeBonus=0 bei NoTimer) → unfair, meidet Mutator-Levels.
        int levelScore = _player.Score - _scoreAtLevelStart;
        if (_currentLevel != null && _currentLevel.Mutator != LevelMutator.None)
        {
            int fairMutatorScore = _progressService.GetBaseScoreForLevel(_currentLevelNumber) * 3;
            if (fairMutatorScore > levelScore + _scoreAtLevelStart)
                levelScore = fairMutatorScore - _scoreAtLevelStart;
        }
        int coinDivisor = _currentLevelNumber <= 10 ? 2 : 3;
        int coins = levelScore / coinDivisor;

        // CoinBonus-Upgrade: L1 = +25%, L2 = +60% (BAL-2, 01.05.2026)
        // L2 bekommt +35% extra (statt vorher +25%), weil 17.000 Coins L2-Preis sich
        // sonst kaum amortisiert (vorher: 15 Welt-3-Level fuer ROI; jetzt: ~10 Level).
        int coinBonusLevel = _shopService.Upgrades.GetLevel(UpgradeType.CoinBonus);
        if (coinBonusLevel > 0)
        {
            float coinMultiplier = coinBonusLevel switch
            {
                1 => 1.25f,
                >= 2 => 1.60f,
                _ => 1.0f
            };
            coins = (int)(coins * coinMultiplier);
        }

        if (_purchaseService.IsPremium)
            coins *= 2;
        CoinsEarned?.Invoke(coins, _player.Score, true);

        // Coin-Floating-Text über dem Exit (gold, groß)
        if (coins > 0 && _exitCell != null)
        {
            float coinX = _exitCell.X * Models.Grid.GameGrid.CELL_SIZE + Models.Grid.GameGrid.CELL_SIZE / 2f;
            float coinY = _exitCell.Y * Models.Grid.GameGrid.CELL_SIZE;
            _floatingText.Spawn(coinX, coinY, $"+{coins} Coins", BomberBlastColors.Gold, 18f, 1.5f);
        }

        // Boss-Level Erst-Abschluss: 5 Gems (L10, L20, ..., L100)
        // Nur Story-Modus, nicht bei Replay (HighestCompletedLevel < aktuelles Level)
        if (_currentLevel!.IsBossLevel
            && !_isDungeonRun && !_isQuickPlayMode && !_isDailyChallenge && !_isSurvivalMode
            && _progressService.HighestCompletedLevel < _currentLevelNumber)
        {
            _tracking.OnBossLevelFirstComplete(_currentLevelNumber);

            // Floating-Text "+5 Gems!" in Cyan über dem Spieler
            float gemX = _player.X;
            float gemY = _player.Y - Models.Grid.GameGrid.CELL_SIZE;
            _floatingText.Spawn(gemX, gemY, "+5 Gems!", new SKColor(0, 188, 212), 20f, 2.0f);
        }

        // Dungeon-Floor abgeschlossen
        if (_isDungeonRun)
        {
            var reward = _dungeonService.CompleteFloor();
            DungeonFloorComplete?.Invoke(reward);

            // Karten-Drop bei Dungeon-Floor
            if (reward.CardDrop >= 0)
                _tracking.Cards.AddCard((BombType)reward.CardDrop);

            // Tracking: Dungeon-Floor (Achievement + BattlePass + Liga + Missionen)
            int floor = _dungeonService.RunState?.CurrentFloor ?? 1;
            _tracking.OnDungeonFloorCompleted(floor);

            if (floor % 5 == 0) // Boss-Floor
                _tracking.OnDungeonBossDefeated();

            return; // Kein Story-Progress im Dungeon
        }

        // Boss-Rush: Score akkumulieren, nicht in Story-Tracking einspielen.
        // Naechster Boss wird in UpdateLevelComplete (bzw. Auto-Switch ueber NextLevelAsync) gestartet.
        // Bei 5. Boss-Clear (_bossRushIndex == 4) wird SubmitRun(completedAll=true) ausgefuehrt + Victory.
        if (_isBossRushMode)
        {
            // Tracking: Boss-Kill als Story-Boss-Defeat zaehlen (Achievement-Pfad)
            _tracking.OnBossLevelFirstComplete(_currentLevelNumber);
            _tracking.FlushIfDirty();
            return;
        }

        // Achievements prüfen (G-R6-1)
        // Master Mode: Separater Pfad, kein Normal-Progress-Update (damit Story-Sterne
        // unverändert bleiben und Master-Clears isoliert getrackt werden).
        if (_isMasterMode && !_isQuickPlayMode)
        {
            int baseScore = _progressService.GetBaseScoreForLevel(_currentLevelNumber);
            int masterLevelScore = _player.Score - _scoreAtLevelStart + timeBonus + efficiencyBonus;
            int masterStars = masterLevelScore switch
            {
                _ when masterLevelScore >= baseScore * 3 => 3,
                _ when masterLevelScore >= baseScore * 2 => 2,
                _ when masterLevelScore >= baseScore => 1,
                _ => 0
            };
            _levelCompleteStars = masterStars;

            _tracking.OnMasterLevelCompleted(
                _currentLevelNumber, _player.Score, masterStars, !_playerDamagedThisLevel);

            _tracking.FlushIfDirty();
            return; // Kein weiteres Normal-Mode-Tracking
        }

        // Score + BestScore ZUERST speichern, damit GetLevelStars/GetTotalStars korrekt sind
        // Quick-Play: Kein Progress/Sterne/Achievements speichern (Spaß-Modus ohne Fortschritt)
        if (!_isQuickPlayMode)
        {
            // Gem-Trickle: 3 Gems bei erstmaligem 3-Sterne-Abschluss (nachhaltige Gem-Quelle)
            int oldStars = _progressService.GetLevelStars(_currentLevelNumber);

            // Mutator-Bonus: Mutator-Level (ab Welt 6) garantieren 3 Sterne bei Completion.
            // Grund: DoubleSpeed/MirrorControls/InvisibleBlocks machen 3-Sterne-Runs statistisch unwahrscheinlich.
            // Fairness-Logik: Schwierigkeit = Belohnung, nicht Strafe. Completion reicht fuer Max-Sterne.
            int scoreToSave = _player.Score;
            if (_currentLevel != null && _currentLevel.Mutator != LevelMutator.None)
            {
                int threeStarThreshold = _progressService.GetBaseScoreForLevel(_currentLevelNumber) * 3;
                if (scoreToSave < threeStarThreshold)
                    scoreToSave = threeStarThreshold;
            }
            _progressService.SetLevelBestScore(_currentLevelNumber, scoreToSave);

            int stars = _progressService.GetLevelStars(_currentLevelNumber);
            _levelCompleteStars = stars;

            // Story-Modus: Erstmaliger 3-Sterne → 3 Gem Bonus (nachhaltige Gem-Quelle)
            if (stars == 3 && oldStars < 3 && !_isDailyChallenge && !_isSurvivalMode)
            {
                _tracking.OnFirstThreeStars();
                float gemX = _player.X;
                float gemY = _player.Y - Models.Grid.GameGrid.CELL_SIZE * 1.5f;
                _floatingText.Spawn(gemX, gemY, "+3 Gems", new SKColor(0, 188, 212), 16f, 1.5f);
            }
            float timeUsed = _currentLevel!.TimeLimit - _timer.RemainingTime;

            // Tracking: Level-Complete (Achievement + Liga + BattlePass + Missionen)
            bool isMutatorLevel = _currentLevel != null && _currentLevel.Mutator != LevelMutator.None;
            _tracking.OnStoryLevelCompleted(
                _currentLevelNumber, _player.Score, stars, _bombsUsed,
                _timer.RemainingTime, timeUsed, !_playerDamagedThisLevel,
                _progressService.GetTotalStars(), _isDailyChallenge, isMutatorLevel);

            // Achievement: Prüfe ob die Welt jetzt perfekt ist (alle 30 Sterne)
            int currentWorld = (_currentLevelNumber - 1) / 10 + 1;
            if (currentWorld == 1 || currentWorld == 5 || currentWorld == 10)
            {
                bool worldPerfect = true;
                int startLevel = (currentWorld - 1) * 10 + 1;
                for (int i = startLevel; i < startLevel + 10; i++)
                {
                    if (_progressService.GetLevelStars(i) < 3)
                    {
                        worldPerfect = false;
                        break;
                    }
                }
                if (worldPerfect)
                    _tracking.OnWorldPerfected(currentWorld);
            }

            // Alle Tracking-Daten persistieren BEVOR CompleteLevel die Level-Completion markiert.
            // Wenn App zwischen Tracking und CompleteLevel crasht, sind Achievements/XP/Liga bereits sicher.
            _tracking.FlushIfDirty();

            // CompleteLevel als LETZTE Aktion persistieren:
            // Wenn jetzt App crasht (Home-Button → OOM), ist Level nicht als abgeschlossen markiert
            // → Spieler muss Level erneut durchspielen. Aber: Sterne + alle Tracking-Events sind gesichert.
            // Rationale: Tracking-Loss waere irreversibel (kein Replay-Mechanismus),
            // CompleteLevel-Loss ist durch Replay reparierbar.
            _progressService.CompleteLevel(_currentLevelNumber);

            // v2.0.41 Plan Task 3.2: Loadout-Boosts sind verbraucht (waren bereits beim Start angewandt
            // + bezahlt). Bei Wiederholung des Levels muss der Spieler das Loadout neu kaufen.
            _loadoutService.ClearLoadout(_currentLevelNumber);
        }

        // Tracking: Quick-Play (Achievement + Missionen)
        if (_isQuickPlayMode)
            _tracking.OnQuickPlayCompleted(QuickPlayModeState?.Difficulty ?? 1);
    }

    private void UpdateLevelComplete(float deltaTime)
    {
        _stateTimer += deltaTime;

        if (_stateTimer >= LEVEL_COMPLETE_DELAY && !_levelCompleteHandled)
        {
            _levelCompleteHandled = true;

            // Fortschritt wurde bereits bei CompleteLevel() sofort persistiert (SetLevelBestScore + CompleteLevel).
            // Hier nur noch Cleanup + Navigation-Event.

            _tracking.FlushIfDirty();

            // Boss-Rush: Auto-Switch zum naechsten Boss oder Victory bei 5. Boss-Clear.
            // v2.0.54 — Phase 11: Score-Akkumulation + Next-Index-Berechnung in BossRushMode
            // (Pure-Logic, isoliert testbar). Engine triggert nur den Async-Switch + Victory-Event.
            if (_isBossRushMode && BossRushModeState is { } brm)
            {
                int levelScore = _player.Score - _scoreAtLevelStart;
                int nextIndex = brm.AccumulateScoreAndGetNextBossIndex(levelScore, _bossRushService.BossSequence.Count);

                if (nextIndex >= 0)
                {
                    // Naechster Boss in der Sequenz: Fire-and-Forget StartBossRushModeAsync.
                    _ = StartBossRushModeAsync(nextIndex).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            _logger?.LogError("BossRush transition failed", t.Exception?.GetBaseException() ?? new Exception("Unknown"));
                            // Fallback: Game-Over + Submit was bisher akkumuliert wurde
                            if (BossRushModeState?.TryGetSubmitArgs(completedAllBosses: false) is { } fa)
                            {
                                _bossRushService.SubmitRun(fa.Score, fa.Time, fa.CompletedAll);
                            }
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                _state = GameState.GameOver;
                                // Phase 18 — IGameMode.OnGameOver-Hook auch im BossRush-Fallback
                                try { _currentMode?.OnGameOver(BuildModeContext()); }
                                catch { /* Best-Effort */ }
                                GameOver?.Invoke();
                            });
                        }
                    }, TaskScheduler.Default);
                    return;
                }
                else
                {
                    // Alle 5 Bosse geschafft → Run abgeschlossen mit completedAll=true.
                    if (brm.TryGetSubmitArgs(completedAllBosses: true) is { } args)
                    {
                        _bossRushService.SubmitRun(args.Score, args.Time, args.CompletedAll);
                    }
                    Victory?.Invoke();
                    return;
                }
            }

            // v2.0.44 — AAA-Audit: Funnel-Tracking für Live-Ops-Entscheidungen.
            // Sprint 2.2 AAA-Audit #2: erweiterte Parameter — time_ms, stars, deaths fuer
            // praezisere Level-Difficulty-Auswertung in Firebase-Dashboards.
            int worldForLevel = (_currentLevelNumber - 1) / 10 + 1;
            int starsEarned = _progressService.GetLevelStars(_currentLevelNumber);
            _analytics?.LogEvent(AnalyticsEvents.LevelComplete, new Dictionary<string, object>
            {
                ["level"] = _currentLevelNumber,
                [AnalyticsParams.LevelId] = _currentLevelNumber,
                [AnalyticsParams.WorldId] = worldForLevel,
                [AnalyticsParams.TimeMs] = (long)Math.Max(0L, _levelElapsedSeconds * 1000f),
                [AnalyticsParams.Stars] = starsEarned,
                [AnalyticsParams.Deaths] = _deathsInLevel,
                ["score"] = _player.Score,
                ["mode"] = GetCurrentModeTag(),
                ["master_mode"] = _isMasterMode
            });
            // Sprint 2.2: boss_defeated wenn das Level einen Boss hatte (paired mit boss_encounter beim Start)
            if (_currentLevel?.IsBossLevel == true && _currentLevel.BossKind is { } defeatedBoss)
            {
                _analytics?.LogEvent(AnalyticsEvents.BossDefeated, new Dictionary<string, object>
                {
                    [AnalyticsParams.BossType] = defeatedBoss.ToString(),
                    [AnalyticsParams.TimeMs] = (long)Math.Max(0L, _levelElapsedSeconds * 1000f),
                    [AnalyticsParams.DamageTaken] = _deathsInLevel,
                    [AnalyticsParams.LevelId] = _currentLevelNumber,
                });
            }
            LevelComplete?.Invoke();
        }
    }

    /// <summary>
    /// Liefert einen kurzen Mode-Tag für Telemetrie/Crash-Custom-Keys.
    /// v2.0.49 — Phase 2: Bevorzugt CurrentMode.ModeTag, fällt auf Bool-Flag-Logic zurück
    /// für Modi die noch nicht über StartXxxModeAsync den _currentMode setzen.
    /// </summary>
    private string GetCurrentModeTag()
    {
        if (_currentMode != null) return _currentMode.ModeTag;
        // Fallback: Bool-Flag-Logic (Backward-Compat während der Migration)
        if (_isDungeonRun) return "dungeon";
        if (_isSurvivalMode) return "survival";
        if (_isQuickPlayMode) return "quick";
        if (_isDailyChallenge) return "daily_challenge";
        if (_isDailyRace) return "daily_race";
        if (_isBossRushMode) return "boss_rush";
        if (_isMasterMode) return "master";
        return "story";
    }

    private void UpdateVictory(float deltaTime)
    {
        _victoryTimer += deltaTime;
        if (_victoryTimer >= VICTORY_DELAY && !_victoryHandled)
        {
            _victoryHandled = true;
            _soundManager.StopMusic();

            // High Score speichern
            if (_highScoreService.IsHighScore(_player.Score))
            {
                _highScoreService.AddScore("PLAYER", _player.Score, 100);
            }

            // v2.0.44 — Funnel-Tracking: Story-Mode-Sieg ist eine Mega-Konversion
            _analytics?.LogEvent(_isBossRushMode ? AnalyticsEvents.BossRushStart : "victory_story", new Dictionary<string, object>
            {
                ["score"] = _player.Score,
                ["mode"] = GetCurrentModeTag()
            });

            // v2.0.48 — Audio-Caption für Sieges-Fanfare
            if (_accessibility?.SubtitlesEnabled == true)
            {
                _subtitles.Show(_localizationService.GetString("SubtitleVictoryFanfare") ?? "[VICTORY FANFARE]", duration: 3f);
            }

            // v2.0.48 — Cinematic-Director: Victory-Big-Win-Sequence
            PlayVictoryCinematic();

            // Coins wurden bereits in CompleteLevel (Level 50) gutgeschrieben → kein Doppel-Credit
            Victory?.Invoke();
        }
    }

    /// <summary>
    /// v2.0.48 — Victory-Big-Win-Cinematic. 4 Confetti-Wellen + finaler Gold-Burst über 2.5s.
    /// Reuse von CinematicSequencer mit Camera-Punch-Out (Zoom 1.0 → 0.92 → 1.0 für Stinger-Feeling).
    /// </summary>
    private void PlayVictoryCinematic()
    {
        float cx = _grid.PixelWidth / 2f;
        float cy = _grid.PixelHeight / 2f;

        var events = new List<CinematicSequencer.TimedEvent>
        {
            // Welle 1: Initiale Konfetti-Explosion
            new(0.0f, () =>
            {
                _particleSystem.EmitShaped(cx, cy, 30, BomberBlastColors.Gold,
                    ParticleShape.Circle, 200f, 1.5f, 4f, hasGlow: true);
                _vibration.VibrateLevelComplete();
            }),
            // Welle 2: Multi-Color-Konfetti aus 4 Ecken
            new(0.5f, () =>
            {
                var colors = new[]
                {
                    new SKColor(255, 100, 100),
                    new SKColor(100, 255, 150),
                    new SKColor(100, 180, 255),
                    new SKColor(255, 200, 50)
                };
                for (int i = 0; i < 4; i++)
                {
                    var corner = i switch
                    {
                        0 => (cx - 200, cy - 100),
                        1 => (cx + 200, cy - 100),
                        2 => (cx - 200, cy + 100),
                        _ => (cx + 200, cy + 100)
                    };
                    _particleSystem.EmitShaped(corner.Item1, corner.Item2, 18, colors[i],
                        ParticleShape.Rectangle, 160f, 1.8f, 3f);
                }
            }),
            // Welle 3: Mid-Burst zentral
            new(1.2f, () =>
            {
                _particleSystem.EmitShaped(cx, cy - 40, 24, new SKColor(255, 255, 220),
                    ParticleShape.Spark, 220f, 1.5f, 3.5f, hasGlow: true);
                _screenShake.AddTrauma(0.4f);
            }),
            // Welle 4: Finale Gold-Explosion + Achievement-ähnlicher Stinger
            new(2.0f, () =>
            {
                _particleSystem.EmitShaped(cx, cy, 40, BomberBlastColors.Gold,
                    ParticleShape.Circle, 280f, 2f, 5f, hasGlow: true);
                _vibration.VibrateAchievement();
            })
        };

        _cinematic.MaxCameraZoom = 0;  // Kein Zoom für Victory (Confetti soll voll sichtbar sein)
        _cinematic.Play(durationSeconds: 2.5f, events);
    }

    /// <summary>
    /// Phase 24b — First-Win-Cinematic (Royal-Match-Pattern).
    /// 4-stufige Sequenz über 4 Sekunden mit eskalierenden Gold-Bursts, Stinger,
    /// Camera-Pull-Back und Trauma-Spike. Wird NUR beim ECHTEN ersten Sieg eines Spielers
    /// ausgelöst (RetentionService.RegisterFirstWin).
    /// </summary>
    private void PlayFirstWinCinematic()
    {
        float cx = _grid.PixelWidth / 2f;
        float cy = _grid.PixelHeight / 2f;

        var events = new List<CinematicSequencer.TimedEvent>
        {
            // Stufe 1 (0.0s): Initialer Gold-Burst um Spieler + Stinger + Camera-Pull-Back
            new(0.0f, () =>
            {
                _particleSystem.EmitShaped(_player.X, _player.Y, 32, BomberBlastColors.Gold,
                    ParticleShape.Circle, 220f, 1.5f, 4.5f, hasGlow: true);
                _particleSystem.EmitExplosionSparks(_player.X, _player.Y, 20, new SKColor(255, 200, 50), 220f);
                _screenShake.AddTrauma(0.4f);
                _screenShake.TriggerPullBack(magnitude: 1.0f, durationSeconds: 0.6f);
                _soundManager.PlayStinger(SoundManager.STINGER_VICTORY);
                _vibration.VibrateAchievement();
            }),
            // Stufe 2 (0.7s): Multi-Color-Konfetti aus 6 Punkten
            new(0.7f, () =>
            {
                var colors = new[]
                {
                    new SKColor(255, 100, 100),
                    new SKColor(100, 255, 150),
                    new SKColor(100, 180, 255),
                    BomberBlastColors.Gold,
                    new SKColor(255, 100, 220),
                    new SKColor(100, 240, 220),
                };
                for (int i = 0; i < 6; i++)
                {
                    var angle = (i / 6f) * MathF.PI * 2f;
                    var px = cx + MathF.Cos(angle) * 220f;
                    var py = cy + MathF.Sin(angle) * 130f;
                    _particleSystem.EmitShaped(px, py, 16, colors[i],
                        ParticleShape.Rectangle, 180f, 2.0f, 3.5f);
                }
            }),
            // Stufe 3 (1.5s): Mid-Burst zentral + Subtitle (für gehörlose Spieler)
            new(1.5f, () =>
            {
                _particleSystem.EmitShaped(cx, cy, 28, new SKColor(255, 255, 220),
                    ParticleShape.Spark, 250f, 1.8f, 4f, hasGlow: true);
                _screenShake.AddTrauma(0.35f);
                if (_accessibility?.SubtitlesEnabled == true)
                {
                    _subtitles.Show(_localizationService.GetString("SubtitleFirstWin")
                        ?? "[FIRST VICTORY!]", duration: 2.5f);
                }
            }),
            // Stufe 4 (2.5s): Finale Mega-Gold-Explosion + Floating-Text
            new(2.5f, () =>
            {
                _particleSystem.EmitShaped(cx, cy, 50, BomberBlastColors.Gold,
                    ParticleShape.Circle, 300f, 2.2f, 5.5f, hasGlow: true);
                _particleSystem.EmitExplosionSparks(cx, cy, 30, new SKColor(255, 240, 100), 280f);
                _screenShake.TriggerPullBack(magnitude: 0.8f, durationSeconds: 0.5f);
                _soundManager.PlayStinger(SoundManager.STINGER_VICTORY);
                _vibration.VibrateLevelComplete();
                _floatingText.Spawn(cx, cy - 60,
                    _localizationService.GetString("FloatFirstWin") ?? "ERSTER SIEG!",
                    BomberBlastColors.Gold, 28f, 3.5f);
            })
        };

        _cinematic.MaxCameraZoom = 0; // Kein Zoom — Konfetti soll voll sichtbar sein
        _cinematic.Play(durationSeconds: 4.0f, events);
    }

    private void OnTimeWarning()
    {
        _soundManager.PlaySound(SoundManager.SFX_TIME_WARNING);
        // v2.0.46 — Audio-Caption für gehörlose Spieler
        if (_accessibility?.SubtitlesEnabled == true)
        {
            _subtitles.Show(_localizationService.GetString("SubtitleTimeWarning") ?? "[TIME WARNING]");
        }
    }

    private void OnTimeExpired()
    {
        // State-Guard: Timer kann in der letzten Kerze laufen, waehrend Spieler gleichzeitig
        // den Exit erreicht oder stirbt. In diesen Faellen kein Pontan mehr spawnen.
        if (_state != GameState.Playing)
            return;

        // Gestaffeltes Pontan-Spawning starten (welt-abhängige Gnadenfrist + Intervall)
        _pontanPunishmentActive = true;
        _pontanSpawned = 0;
        _pontanInitialDelay = GetPontanInitialDelay();
        _pontanSpawnTimer = _pontanInitialDelay > 0 ? _pontanInitialDelay : 0; // Gnadenfrist oder sofort
    }

    /// <summary>
    /// Gestaffeltes Pontan-Spawning mit Vorwarnung (pulsierendes "!" 1.5s vor Spawn)
    /// </summary>
    private void UpdatePontanPunishment(float deltaTime)
    {
        int maxCount = GetPontanMaxCount();
        if (!_pontanPunishmentActive || _pontanSpawned >= maxCount)
        {
            _pontanPunishmentActive = false;
            _pontanWarningActive = false;
            return;
        }

        _pontanSpawnTimer -= deltaTime;

        // Phase 22 (G8) — Stage 1: Early-Warning bei 3s (Audio + Subtitle + Time-Warning-SFX)
        if (!_pontanEarlyWarningTriggered && _pontanSpawnTimer <= PONTAN_EARLY_WARNING_TIME && _pontanSpawnTimer > 0)
        {
            _pontanEarlyWarningTriggered = true;
            _soundManager.PlaySound(SoundManager.SFX_TIME_WARNING);
            if (_accessibility?.SubtitlesEnabled == true)
            {
                _subtitles.Show(_localizationService.GetString("SubtitleTimeWarning") ?? "[TIME WARNING]", duration: 2f);
            }
        }

        // Stage 2 (Bestand): Position vorberechnen wenn Timer unter 1.5s fällt
        if (!_pontanWarningActive && _pontanSpawnTimer <= PONTAN_WARNING_TIME && _pontanSpawnTimer > 0)
        {
            PreCalculateNextPontanSpawn();
        }

        // Phase 22 (G8) — Stage 3: Final-Warning bei 0.5s (Trauma-Spike als visuelles Crescendo)
        if (!_pontanFinalWarningTriggered && _pontanSpawnTimer <= PONTAN_FINAL_WARNING_TIME && _pontanSpawnTimer > 0)
        {
            _pontanFinalWarningTriggered = true;
            _screenShake.AddTrauma(0.25f);
        }

        if (_pontanSpawnTimer > 0)
            return;

        _pontanSpawnTimer = GetPontanSpawnInterval();
        _pontanWarningActive = false;
        // Reset für nächsten Pontan-Spawn-Zyklus
        _pontanEarlyWarningTriggered = false;
        _pontanFinalWarningTriggered = false;

        // Pontan an der vorberechneten Position spawnen
        SpawnPontanAtWarningPosition();
    }

    /// <summary>
    /// Nächste Pontan-Spawn-Position vorberechnen und Warnung aktivieren
    /// </summary>
    private void PreCalculateNextPontanSpawn()
    {
        int playerCellX = _player.GridX;
        int playerCellY = _player.GridY;

        for (int attempts = 0; attempts < 40; attempts++)
        {
            int x = EngineRngNext(3, GameGrid.WIDTH - 1);
            int y = EngineRngNext(3, GameGrid.HEIGHT - 1);

            if (Math.Abs(x - playerCellX) + Math.Abs(y - playerCellY) < PONTAN_MIN_DISTANCE)
                continue;

            var cell = _grid.TryGetCell(x, y);
            if (cell == null || cell.Type != CellType.Empty)
                continue;
            if (cell.Bomb != null || cell.PowerUp != null)
                continue;

            bool enemyOnCell = false;
            foreach (var existing in _enemies)
            {
                if (existing.IsActive && existing.GridX == x && existing.GridY == y)
                {
                    enemyOnCell = true;
                    break;
                }
            }
            if (enemyOnCell) continue;

            _pontanWarningX = x * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            _pontanWarningY = y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            _pontanWarningActive = true;
            return;
        }
    }

    /// <summary>
    /// Pontan an der vorberechneten Warnposition spawnen
    /// </summary>
    private void SpawnPontanAtWarningPosition()
    {
        if (!_pontanWarningActive)
        {
            // Fallback: Keine Vorberechnung → direkt suchen
            PreCalculateNextPontanSpawn();
            if (!_pontanWarningActive) return;
        }

        int gx = (int)MathF.Floor(_pontanWarningX / GameGrid.CELL_SIZE);
        int gy = (int)MathF.Floor(_pontanWarningY / GameGrid.CELL_SIZE);

        // Validierung (Zelle könnte sich geändert haben)
        var cell = _grid.TryGetCell(gx, gy);
        if (cell == null || cell.Type != CellType.Empty || cell.Bomb != null)
        {
            // Position ungültig → neue suchen
            PreCalculateNextPontanSpawn();
            if (!_pontanWarningActive) return;
            gx = (int)MathF.Floor(_pontanWarningX / GameGrid.CELL_SIZE);
            gy = (int)MathF.Floor(_pontanWarningY / GameGrid.CELL_SIZE);
        }

        var enemy = Enemy.CreateAtGrid(gx, gy, EnemyType.Pontan);
        _enemies.Add(enemy);
        _enemiesRemainingDirty = true;
        _pontanSpawned++;

        // Spawn-Partikel
        _particleSystem.Emit(_pontanWarningX, _pontanWarningY, 8, new SKColor(255, 0, 80), 60f, 0.5f);
        _floatingText.Spawn(_pontanWarningX, _pontanWarningY - 16, "!", new SKColor(255, 0, 0), 24f, 1.0f);
    }

    /// <summary>
    /// Zum nächsten Level wechseln
    /// </summary>
    public async Task NextLevelAsync()
    {
        _currentLevelNumber++;
        if (_currentLevelNumber > 100)
        {
            _state = GameState.Victory;
            _victoryTimer = 0;
            _victoryHandled = false;
            _timer.Pause();
            CacheVictoryOverlayStrings();
            _soundManager.PlaySound(SoundManager.SFX_LEVEL_COMPLETE);
            return;
        }
        _currentLevel = LevelLayoutGenerator.GenerateLevel(_currentLevelNumber, _progressService.HighestCompletedLevel);
        _activeMutator = _currentLevel.Mutator;

        // Welt-/Boss-/Mutator-Ankündigung (typspezifischer Boss-Name statt generischem Banner)
        if (_currentLevel.IsBossLevel)
        {
            _worldAnnouncementText = ComposeBossBannerText(_currentLevel.BossKind, _currentLevel.BossKind2);
            _worldAnnouncementTimer = 2.5f;
        }
        else if (_activeMutator != LevelMutator.None)
        {
            var mutatorName = _levelGenerator.GetMutatorDisplayName(_activeMutator);
            var format = _localizationService.GetString("MutatorActive") ?? "Mutator: {0}";
            _worldAnnouncementText = string.Format(format, mutatorName);
            _worldAnnouncementTimer = 2.5f;
        }
        else if ((_currentLevelNumber - 1) % 10 == 0)
        {
            int world = (_currentLevelNumber - 1) / 10 + 1;
            _worldAnnouncementText = string.Format(
                _localizationService.GetString("AnnounceWorld") ?? "WORLD {0}", world);
            _worldAnnouncementTimer = 2.0f;
        }

        // Upgrades + Mutator-Effekte anwenden
        ApplyUpgrades();
        BomberBlast.Core.LevelGeneration.MutatorEffects.Apply(_player, _activeMutator);

        // Musik-Wechsel bei Boss-Level
        if (_currentLevel.MusicTrack == "boss")
            _soundManager.PlayMusic(SoundManager.MUSIC_BOSS);

        await LoadLevelAsync();
    }
}
