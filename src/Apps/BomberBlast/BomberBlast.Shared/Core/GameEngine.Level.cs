using BomberBlast.Graphics;
using BomberBlast.Models;
using BomberBlast.Models.Dungeon;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Models.Levels;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Core;

/// <summary>
/// Level-Verwaltung: Laden, PowerUps, Exit, Gegner, Abschluss
/// </summary>
public partial class GameEngine
{
    /// <summary>
    /// Story-Modus starten
    /// </summary>
    public async Task StartStoryModeAsync(int levelNumber)
    {
        _isDailyChallenge = false;
        _isSurvivalMode = false;
        _isQuickPlayMode = false;
        _currentLevelNumber = levelNumber;
        _currentLevel = LevelGenerator.GenerateLevel(levelNumber, _progressService.HighestCompletedLevel);
        _continueUsed = false;

        _player.ResetForNewGame();
        ApplyUpgrades();
        await LoadLevelAsync();

        _soundManager.PlayMusic(_currentLevel.MusicTrack == "boss"
            ? SoundManager.MUSIC_BOSS
            : SoundManager.MUSIC_GAMEPLAY);

        // Welt-/Boss-Ankündigung
        int world = (_currentLevelNumber - 1) / 10 + 1;
        if (_currentLevel.IsBossLevel)
        {
            _worldAnnouncementText = $"BOSS FIGHT!";
            _worldAnnouncementTimer = 2.5f;
        }
        else
        {
            _worldAnnouncementText = $"WORLD {world}";
            _worldAnnouncementTimer = 2.0f;
        }
    }

    /// <summary>
    /// Daily-Challenge-Modus starten (einmaliges Level pro Tag)
    /// </summary>
    public async Task StartDailyChallengeModeAsync(int seed)
    {
        _isDailyChallenge = true;
        _isSurvivalMode = false;
        _isQuickPlayMode = false;
        _currentLevelNumber = 99;
        _currentLevel = LevelGenerator.GenerateDailyChallengeLevel(seed);
        _continueUsed = false;

        _player.ResetForNewGame();
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
        _isDailyChallenge = false;
        _isSurvivalMode = false;
        _isQuickPlayMode = true;
        _quickPlayDifficulty = difficulty;
        _currentLevelNumber = difficulty * 10; // Für Welt-Palette
        _currentLevel = LevelGenerator.GenerateQuickPlayLevel(seed, difficulty);
        _continueUsed = true; // Kein Continue im Quick-Play

        _player.ResetForNewGame();
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
        _isDailyChallenge = false;
        _isSurvivalMode = true;
        _isQuickPlayMode = false;
        _currentLevelNumber = 1;
        _currentLevel = LevelGenerator.GenerateSurvivalLevel();
        _continueUsed = true; // Kein Continue im Survival

        _survivalTimeElapsed = 0;
        _survivalSpawnTimer = 4f; // Erster Spawn nach 4s
        _survivalSpawnInterval = 4f;

        _player.ResetForNewGame();
        ApplyUpgrades();
        _player.Lives = 1; // Nur 1 Leben im Survival (kein Shop-Bonus)

        await LoadLevelAsync();

        _soundManager.PlayMusic(SoundManager.MUSIC_GAMEPLAY);

        _worldAnnouncementText = "SURVIVAL!";
        _worldAnnouncementTimer = 2.5f;
    }

    /// <summary>
    /// Dungeon-Floor starten (Roguelike-Modus)
    /// </summary>
    public async Task StartDungeonFloorAsync(int floor, int seed)
    {
        _isDailyChallenge = false;
        _isSurvivalMode = false;
        _isQuickPlayMode = false;
        _isDungeonRun = true;
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
            OnDungeonBuffSelection?.Invoke();
            return;
        }

        _currentLevel = LevelGenerator.GenerateDungeonFloor(floor, seed, roomType, challengeMode, floorModifier);
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
        _soundManager.PlayMusic(isBoss ? SoundManager.MUSIC_BOSS : SoundManager.MUSIC_GAMEPLAY);

        // Ankündigung mit Raum-Typ
        string roomLabel = roomType switch
        {
            DungeonRoomType.Elite => "ELITE",
            DungeonRoomType.Treasure => "TREASURE",
            DungeonRoomType.Challenge => "CHALLENGE",
            _ => ""
        };
        string floorText = isBoss ? $"BOSS - FLOOR {floor}" : $"FLOOR {floor}";
        if (!string.IsNullOrEmpty(roomLabel))
            floorText = $"{roomLabel} - FLOOR {floor}";
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
                    if (top != null && top.Type == CellType.Empty) { top.IsLavaActive = true; top.LavaTimer = 999f; }
                    if (bottom != null && bottom.Type == CellType.Empty) { bottom.IsLavaActive = true; bottom.LavaTimer = 999f; }
                }
                for (int y = 1; y < GameGrid.HEIGHT - 1; y++)
                {
                    var left = _grid.TryGetCell(0, y);
                    var right = _grid.TryGetCell(GameGrid.WIDTH - 1, y);
                    if (left != null && left.Type == CellType.Empty) { left.IsLavaActive = true; left.LavaTimer = 999f; }
                    if (right != null && right.Type == CellType.Empty) { right.IsLavaActive = true; right.LavaTimer = 999f; }
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

        // Synergien prüfen (zwei bestimmte Buffs → Bonus-Effekt)
        _synergyBombardierActive = false;
        _synergyBlitzkriegActive = false;
        _synergyFortressActive = false;
        _fortressRegenTimer = 0;
        _synergyMidasActive = false;
        _synergyElementalActive = false;

        var buffs = state.ActiveBuffs;

        // Bombardier: ExtraBomb + ExtraFire → nochmal +1 auf beides
        if (buffs.Contains(DungeonBuffType.ExtraBomb) && buffs.Contains(DungeonBuffType.ExtraFire))
        {
            _synergyBombardierActive = true;
            _player.MaxBombs++;
            _player.FireRange++;
        }

        // Blitzkrieg: SpeedBoost + BombTimer → Bomben-Timer -0.5s extra
        if (buffs.Contains(DungeonBuffType.SpeedBoost) && buffs.Contains(DungeonBuffType.BombTimer))
            _synergyBlitzkriegActive = true;

        // Festung: Shield + ExtraLife → Shield regeneriert nach 20s ohne Schaden
        if (buffs.Contains(DungeonBuffType.Shield) && buffs.Contains(DungeonBuffType.ExtraLife))
            _synergyFortressActive = true;

        // Midas: CoinBonus + GoldRush → Gegner droppen Mini-Coins bei Tod
        if (buffs.Contains(DungeonBuffType.CoinBonus) && buffs.Contains(DungeonBuffType.GoldRush))
            _synergyMidasActive = true;

        // Elementar: EnemySlow + FireImmunity → Lava verlangsamt Gegner statt Spieler zu schaden
        if (buffs.Contains(DungeonBuffType.EnemySlow) && buffs.Contains(DungeonBuffType.FireImmunity))
            _synergyElementalActive = true;

        // Kumulative Zündschnur-Reduktion berechnen (BombTimer-Buff + Blitzkrieg-Synergy)
        _dungeonBombFuseReduction = 0;
        if (buffs.Contains(DungeonBuffType.BombTimer))
            _dungeonBombFuseReduction += 0.5f;
        if (_synergyBlitzkriegActive)
            _dungeonBombFuseReduction += 0.5f;
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
        _bombsUsed = 0;
        _enemiesKilled = 0;
        _exitRevealed = false;
        _exitCell = null;
        _scoreAtLevelStart = _player.Score;
        _playerDamagedThisLevel = false;

        // Entities leeren
        _enemies.Clear();
        _bombs.Clear();
        _explosions.Clear();
        _powerUps.Clear();
        _enemyPositionCache.Clear();
        _enemyPositionHashSet.Clear();
        _destroyingCells.Clear();
        _afterglowCells.Clear();
        _specialEffectCells.Clear();
        _pendingIceCleanups.Clear();
        _particleSystem.Clear();
        _floatingText.Clear();
        _screenShake.Reset();
        _hitPauseTimer = 0;
        _comboCount = 0;
        _comboTimer = 0;
        _pontanPunishmentActive = false;
        _pontanSpawned = 0;
        _pontanInitialDelay = 0;
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

        // PowerUps in Blöcken platzieren
        PlacePowerUps(random);

        // Exit unter einem Block platzieren (nicht im Survival-Modus)
        if (!_isSurvivalMode)
            PlaceExit(random);

        // Gegner spawnen
        SpawnEnemies(random);
        _originalEnemyCount = _enemies.Count;

        // Welt-Theme setzen (basierend auf Level-Nummer)
        int worldIndex = (_currentLevelNumber - 1) / 10;
        _renderer.SetWorldTheme(worldIndex);

        // Nebel aktivieren fuer Schattenwelt (Welt 10)
        _renderer.SetFogEnabled(_currentLevel.Mechanic == WorldMechanic.Fog);

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
    }

    /// <summary>
    /// Shop-Upgrades auf den Spieler anwenden.
    /// Im Dungeon: Nur Base-Stats (Shop-Bonuse gelten nicht, Dungeon-Buffs werden separat addiert).
    /// In Story/Daily/QuickPlay/Survival: Volle Shop-Bonuse.
    /// </summary>
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

    private void PlacePowerUps(Random random)
    {
        // Wiederverwendbare Liste statt LINQ .ToList() (vermeidet Heap-Allokation pro Aufruf)
        _blockCells.Clear();
        for (int y = 0; y < _grid.Height; y++)
            for (int x = 0; x < _grid.Width; x++)
                if (_grid[x, y].Type == CellType.Block)
                    _blockCells.Add(_grid[x, y]);

        if (_blockCells.Count == 0 || _currentLevel?.PowerUps == null)
            return;

        var blocks = _blockCells;

        // Fisher-Yates Shuffle (in-place, keine LINQ-Allokation)
        for (int i = blocks.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (blocks[i], blocks[j]) = (blocks[j], blocks[i]);
        }

        int blockIndex = 0;
        foreach (var powerUp in _currentLevel.PowerUps)
        {
            if (blockIndex >= blocks.Count)
                break;

            Cell targetCell;
            if (powerUp.X.HasValue && powerUp.Y.HasValue)
            {
                targetCell = _grid.TryGetCell(powerUp.X.Value, powerUp.Y.Value) ?? blocks[blockIndex++];
            }
            else
            {
                targetCell = blocks[blockIndex++];
            }

            if (targetCell.Type == CellType.Block)
            {
                targetCell.HiddenPowerUp = powerUp.Type;
            }
        }

        // PowerUpLuck-Upgrade: Zusätzliche zufällige PowerUps
        int extraPowerUps = _shopService.Upgrades.GetLevel(UpgradeType.PowerUpLuck);
        if (extraPowerUps > 0)
        {
            var basicPowerUps = new[] { PowerUpType.BombUp, PowerUpType.Fire, PowerUpType.Speed };
            for (int i = 0; i < extraPowerUps && blockIndex < blocks.Count; i++)
            {
                var cell = blocks[blockIndex++];
                if (cell.Type == CellType.Block && cell.HiddenPowerUp == null)
                {
                    cell.HiddenPowerUp = basicPowerUps[random.Next(basicPowerUps.Length)];
                }
            }
        }
    }

    private void PlaceExit(Random random)
    {
        // Wiederverwendbare Liste statt LINQ .Where().ToList() (vermeidet Heap-Allokation)
        _blockCells.Clear();
        for (int y = 0; y < _grid.Height; y++)
            for (int x = 0; x < _grid.Width; x++)
            {
                var c = _grid[x, y];
                if (c.Type == CellType.Block && c.HiddenPowerUp == null)
                    _blockCells.Add(c);
            }

        var blocks = _blockCells;

        // Fallback: Wenn ALLE Blöcke ein HiddenPowerUp haben → alle Blöcke nehmen
        // Exit hat Priorität über PowerUp (wird unten auf null gesetzt)
        if (blocks.Count == 0)
        {
            for (int y = 0; y < _grid.Height; y++)
                for (int x = 0; x < _grid.Width; x++)
                    if (_grid[x, y].Type == CellType.Block)
                        _blockCells.Add(_grid[x, y]);

            if (blocks.Count == 0)
                return;
        }

        Cell exitCell;
        if (_currentLevel?.ExitPosition != null)
        {
            exitCell = _grid.TryGetCell(_currentLevel.ExitPosition.Value.x, _currentLevel.ExitPosition.Value.y)
                ?? blocks[random.Next(blocks.Count)];
        }
        else
        {
            // Exit aus den entferntesten Blöcken zufällig wählen (nicht immer der gleiche Spot)
            // Sammle alle Blöcke die mindestens 60% der maximalen Distanz haben
            int maxDist = 0;
            for (int i = 0; i < blocks.Count; i++)
            {
                int dist = Math.Abs(blocks[i].X - 1) + Math.Abs(blocks[i].Y - 1);
                if (dist > maxDist) maxDist = dist;
            }

            int threshold = (int)(maxDist * 0.6f);
            var farBlocks = new List<Cell>();
            for (int i = 0; i < blocks.Count; i++)
            {
                int dist = Math.Abs(blocks[i].X - 1) + Math.Abs(blocks[i].Y - 1);
                if (dist >= threshold)
                    farBlocks.Add(blocks[i]);
            }

            exitCell = farBlocks.Count > 0
                ? farBlocks[random.Next(farBlocks.Count)]
                : blocks[random.Next(blocks.Count)];
        }

        // Exit unter dem Block verstecken (klassisches Bomberman)
        exitCell.HiddenPowerUp = null;
        exitCell.HasHiddenExit = true;
    }

    private void SpawnEnemies(Random random)
    {
        if (_currentLevel?.Enemies == null)
            return;

        // Gültige Spawn-Positionen (nicht in Spieler-Nähe, nicht auf Wänden/Blöcken)
        var validPositions = new List<(int x, int y)>();
        for (int x = 1; x < GameGrid.WIDTH - 1; x++)
        {
            for (int y = 1; y < GameGrid.HEIGHT - 1; y++)
            {
                if (x <= 3 && y <= 3)
                    continue;

                var cell = _grid[x, y];
                if (cell.Type == CellType.Empty)
                {
                    validPositions.Add((x, y));
                }
            }
        }

        foreach (var spawn in _currentLevel.Enemies)
        {
            for (int i = 0; i < spawn.Count; i++)
            {
                (int x, int y) pos;
                if (spawn.X.HasValue && spawn.Y.HasValue)
                {
                    pos = (spawn.X.Value, spawn.Y.Value);
                }
                else if (validPositions.Count > 0)
                {
                    int index = random.Next(validPositions.Count);
                    pos = validPositions[index];
                    validPositions.RemoveAt(index);
                }
                else
                {
                    // Fallback: Bis zu 40 Versuche für gültige Position
                    bool placed = false;
                    pos = (0, 0);
                    for (int attempt = 0; attempt < 40; attempt++)
                    {
                        pos = (random.Next(3, GameGrid.WIDTH - 2), random.Next(3, GameGrid.HEIGHT - 2));
                        var fallbackCell = _grid.TryGetCell(pos.x, pos.y);
                        if (fallbackCell != null && fallbackCell.Type == CellType.Empty)
                        {
                            placed = true;
                            break;
                        }
                    }
                    if (!placed) continue; // Nur nach 40 Fehlversuchen aufgeben
                }

                var enemy = Enemy.CreateAtGrid(pos.x, pos.y, spawn.Type);
                _enemies.Add(enemy);
            }
        }

        // Boss spawnen wenn Boss-Level
        if (_currentLevel.BossKind.HasValue)
        {
            // Boss in der Arena-Mitte platzieren (weit vom Spieler)
            int bossX = GameGrid.WIDTH / 2;
            int bossY = GameGrid.HEIGHT / 2;

            // Sicherstellen dass die Boss-Zellen frei sind (Blöcke entfernen)
            var bossType = _currentLevel.BossKind.Value;
            int bossSize = bossType == BossType.FinalBoss ? 3 : 2;
            for (int dy = 0; dy < bossSize; dy++)
            {
                for (int dx = 0; dx < bossSize; dx++)
                {
                    var cell = _grid.TryGetCell(bossX + dx, bossY + dy);
                    if (cell != null && cell.Type == CellType.Block)
                    {
                        cell.Type = CellType.Empty;
                        cell.HasHiddenExit = false;
                        cell.HiddenPowerUp = null;
                    }
                }
            }

            var boss = BossEnemy.CreateAtGrid(bossX, bossY, bossType);
            _enemies.Add(boss);

            // Sammlungs-Album: Boss als angetroffen melden
            _tracking.OnBossEncountered(bossType);
        }
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
                    _floatingText.Spawn(enemy.X, enemy.Y - 16, "MIMIC!", SKColors.Red, 16f, 1.5f);
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
                    float bossDt = deltaTime;
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
                continue;
            }

            if (enemy.IsActive && !enemy.IsDying)
            {
                // Verlangsamung: Frost (50%), TimeWarp (50%), BlackHole (70%) - kumulativ
                float enemyDt = deltaTime;
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
        if (_pontanRandom.NextDouble() < 0.3)
        {
            var dirs = new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
            preferred = dirs[_pontanRandom.Next(dirs.Length)];
        }

        boss.MovementDirection = preferred;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SURVIVAL-MODUS: Kontinuierliches Gegner-Spawning
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Survival: Gegner spawnen in steigender Frequenz. Schwierigkeit nimmt mit der Zeit zu.
    /// </summary>
    private void UpdateSurvivalSpawning(float deltaTime)
    {
        _survivalTimeElapsed += deltaTime;
        _survivalSpawnTimer -= deltaTime;

        if (_survivalSpawnTimer > 0)
            return;

        // Gegner spawnen
        SpawnSurvivalEnemy();

        // Intervall verringern (wird schwerer über Zeit)
        _survivalSpawnInterval = MathF.Max(SURVIVAL_MIN_SPAWN_INTERVAL,
            _survivalSpawnInterval - SURVIVAL_SPAWN_DECREASE);
        _survivalSpawnTimer = _survivalSpawnInterval;
    }

    /// <summary>
    /// Einzelnen Gegner im Survival-Modus spawnen. Typ basierend auf überlebter Zeit.
    /// </summary>
    private void SpawnSurvivalEnemy()
    {
        // Gegner-Typ basierend auf Überlebenszeit wählen
        EnemyType type;
        if (_survivalTimeElapsed < 20)
            type = EnemyType.Ballom;
        else if (_survivalTimeElapsed < 45)
            type = _pontanRandom.Next(3) switch { 0 => EnemyType.Onil, 1 => EnemyType.Doll, _ => EnemyType.Ballom };
        else if (_survivalTimeElapsed < 90)
            type = _pontanRandom.Next(4) switch { 0 => EnemyType.Onil, 1 => EnemyType.Doll, 2 => EnemyType.Minvo, _ => EnemyType.Doll };
        else if (_survivalTimeElapsed < 150)
            type = _pontanRandom.Next(5) switch { 0 => EnemyType.Minvo, 1 => EnemyType.Ovapi, 2 => EnemyType.Tanker, 3 => EnemyType.Doll, _ => EnemyType.Kondoria };
        else
            type = _pontanRandom.Next(6) switch { 0 => EnemyType.Ovapi, 1 => EnemyType.Tanker, 2 => EnemyType.Ghost, 3 => EnemyType.Pontan, 4 => EnemyType.Splitter, _ => EnemyType.Mimic };

        // Spawn-Position finden (weit vom Spieler, auf leerer Zelle)
        for (int attempts = 0; attempts < 40; attempts++)
        {
            int x = _pontanRandom.Next(2, GameGrid.WIDTH - 1);
            int y = _pontanRandom.Next(2, GameGrid.HEIGHT - 1);

            if (Math.Abs(x - _player.GridX) + Math.Abs(y - _player.GridY) < 4)
                continue;

            var cell = _grid.TryGetCell(x, y);
            if (cell == null || cell.Type != CellType.Empty || cell.Bomb != null)
                continue;

            var enemy = Enemy.CreateAtGrid(x, y, type);
            _enemies.Add(enemy);

            // Spawn-Partikel
            float spawnX = x * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float spawnY = y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            _particleSystem.Emit(spawnX, spawnY, 6, new SKColor(255, 100, 50), 50f, 0.4f);
            return;
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
        _state = GameState.LevelComplete;
        _stateTimer = 0;
        _levelCompleteHandled = false;
        _timer.Pause();
        _vibration.VibratePattern();

        // Enemy-Kill-Punkte merken (nur Level-Score, nicht kumulierter Gesamtscore)
        LastEnemyKillPoints = _player.Score - _scoreAtLevelStart;

        // Bonusberechnung mit Shop-Upgrades
        int timeBonusMultiplier = _shopService.GetTimeBonusMultiplier();
        int timeBonus = (int)_timer.RemainingTime * timeBonusMultiplier;

        // Gestufter Effizienzbonus (skaliert nach Welt, Welt 1-2 angehoben)
        int world = (_currentLevelNumber - 1) / 10; // 0-9
        int efficiencyBonus = 0;
        if (_bombsUsed <= 5)
            efficiencyBonus = world switch { 0 => 4000, 1 => 6000, 2 => 8000, 3 => 12000, _ => 15000 };
        else if (_bombsUsed <= 8)
            efficiencyBonus = world switch { 0 => 2500, 1 => 4000, 2 => 5000, 3 => 8000, _ => 10000 };
        else if (_bombsUsed <= 12)
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

        _soundManager.PlaySound(SoundManager.SFX_LEVEL_COMPLETE);
        OnScoreChanged?.Invoke(_player.Score);

        // Erster Sieg: Level 1 zum ersten Mal abgeschlossen
        _isFirstVictory = _currentLevelNumber == 1 && _progressService.HighestCompletedLevel == 0;
        if (_isFirstVictory)
        {
            // Extra Gold-Partikel für ersten Sieg
            _particleSystem.EmitShaped(_player.X, _player.Y, 24, new SKColor(255, 215, 0),
                Graphics.ParticleShape.Circle, 150f, 1.0f, 3.5f, hasGlow: true);
            _particleSystem.EmitExplosionSparks(_player.X, _player.Y, 16, new SKColor(255, 200, 50), 180f);
        }

        // Coins basierend auf Level-Score (nicht kumuliert, verhindert Inflation)
        int levelScore = _player.Score - _scoreAtLevelStart;
        int coins = levelScore / 3;

        // CoinBonus-Upgrade: +25% / +50% extra Coins
        int coinBonusLevel = _shopService.Upgrades.GetLevel(UpgradeType.CoinBonus);
        if (coinBonusLevel > 0)
        {
            float coinMultiplier = 1f + coinBonusLevel * 0.25f;
            coins = (int)(coins * coinMultiplier);
        }

        if (_purchaseService.IsPremium)
            coins *= 3;
        OnCoinsEarned?.Invoke(coins, _player.Score, true);

        // Coin-Floating-Text über dem Exit (gold, groß)
        if (coins > 0 && _exitCell != null)
        {
            float coinX = _exitCell.X * Models.Grid.GameGrid.CELL_SIZE + Models.Grid.GameGrid.CELL_SIZE / 2f;
            float coinY = _exitCell.Y * Models.Grid.GameGrid.CELL_SIZE;
            _floatingText.Spawn(coinX, coinY, $"+{coins} Coins", new SKColor(255, 215, 0), 18f, 1.5f);
        }

        // Dungeon-Floor abgeschlossen
        if (_isDungeonRun)
        {
            var reward = _dungeonService.CompleteFloor();
            OnDungeonFloorComplete?.Invoke(reward);

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

        // Achievements prüfen (G-R6-1)
        // Score + BestScore ZUERST speichern, damit GetLevelStars/GetTotalStars korrekt sind
        // Quick-Play: Kein Progress/Sterne/Achievements speichern (Spaß-Modus ohne Fortschritt)
        if (!_isQuickPlayMode)
        {
            _progressService.SetLevelBestScore(_currentLevelNumber, _player.Score);

            int stars = _progressService.GetLevelStars(_currentLevelNumber);
            _levelCompleteStars = stars;
            float timeUsed = _currentLevel!.TimeLimit - _timer.RemainingTime;

            // Tracking: Level-Complete (Achievement + Liga + BattlePass + Missionen)
            _tracking.OnStoryLevelCompleted(
                _currentLevelNumber, _player.Score, stars, _bombsUsed,
                _timer.RemainingTime, timeUsed, !_playerDamagedThisLevel,
                _progressService.GetTotalStars(), _isDailyChallenge);

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
        }

        // Tracking: Quick-Play (Achievement + Missionen)
        if (_isQuickPlayMode)
            _tracking.OnQuickPlayCompleted(_quickPlayDifficulty);
    }

    private void UpdateLevelComplete(float deltaTime)
    {
        _stateTimer += deltaTime;

        if (_stateTimer >= LEVEL_COMPLETE_DELAY && !_levelCompleteHandled)
        {
            _levelCompleteHandled = true;

            // Fortschritt speichern (BestScore bereits in CompleteLevel() gesetzt)
            // Quick-Play: Kein Progress speichern
            if (!_isQuickPlayMode)
            {
                _progressService.CompleteLevel(_currentLevelNumber);
            }

            _tracking.FlushIfDirty();
            OnLevelComplete?.Invoke();
        }
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

            // Coins wurden bereits in CompleteLevel (Level 50) gutgeschrieben → kein Doppel-Credit
            OnVictory?.Invoke();
        }
    }

    private void OnTimeWarning()
    {
        _soundManager.PlaySound(SoundManager.SFX_TIME_WARNING);
    }

    private void OnTimeExpired()
    {
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

        // Vorwarnung: Position vorberechnen wenn Timer unter Warnschwelle fällt
        if (!_pontanWarningActive && _pontanSpawnTimer <= PONTAN_WARNING_TIME && _pontanSpawnTimer > 0)
        {
            PreCalculateNextPontanSpawn();
        }

        if (_pontanSpawnTimer > 0)
            return;

        _pontanSpawnTimer = GetPontanSpawnInterval();
        _pontanWarningActive = false;

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
            int x = _pontanRandom.Next(3, GameGrid.WIDTH - 1);
            int y = _pontanRandom.Next(3, GameGrid.HEIGHT - 1);

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
            _soundManager.PlaySound(SoundManager.SFX_LEVEL_COMPLETE);
            return;
        }
        _currentLevel = LevelGenerator.GenerateLevel(_currentLevelNumber, _progressService.HighestCompletedLevel);

        // Welt-Ankündigung bei neuem Welt-Start (Level 11, 21, 31, 41)
        if ((_currentLevelNumber - 1) % 10 == 0)
        {
            int world = (_currentLevelNumber - 1) / 10 + 1;
            _worldAnnouncementText = $"WORLD {world}";
            _worldAnnouncementTimer = 2.0f;
        }

        // Upgrades erneut anwenden (Leben, Schild, Spezial-Bomben zurücksetzen)
        ApplyUpgrades();

        await LoadLevelAsync();
    }
}
