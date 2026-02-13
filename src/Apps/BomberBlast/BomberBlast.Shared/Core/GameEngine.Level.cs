using BomberBlast.Graphics;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Models.Levels;
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
        _isArcadeMode = false;
        _currentLevelNumber = levelNumber;
        _currentLevel = LevelGenerator.GenerateLevel(levelNumber);
        _continueUsed = false;

        _player.ResetForNewGame();
        ApplyUpgrades();
        await LoadLevelAsync();

        _soundManager.PlayMusic(_currentLevel.MusicTrack == "boss"
            ? SoundManager.MUSIC_BOSS
            : SoundManager.MUSIC_GAMEPLAY);

        // Welt-Ankündigung bei Level 1 (immer "World 1")
        int world = (_currentLevelNumber - 1) / 10 + 1;
        _worldAnnouncementText = $"WORLD {world}";
        _worldAnnouncementTimer = 2.0f;
    }

    /// <summary>
    /// Arcade-Modus starten
    /// </summary>
    public async Task StartArcadeModeAsync()
    {
        _isArcadeMode = true;
        _arcadeWave = 1;
        _currentLevelNumber = 1;
        _currentLevel = LevelGenerator.GenerateArcadeLevel(1);
        _continueUsed = false;

        _player.ResetForNewGame();
        ApplyUpgrades(); // GetStartLives(isArcade=true) gibt bereits 1 zurück
        await LoadLevelAsync();

        _soundManager.PlayMusic(SoundManager.MUSIC_GAMEPLAY);
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
        _particleSystem.Clear();
        _floatingText.Clear();
        _screenShake.Reset();
        _hitPauseTimer = 0;
        _comboCount = 0;
        _comboTimer = 0;
        _pontanPunishmentActive = false;
        _pontanSpawned = 0;
        _defeatAllCooldown = 0;

        // Grid aufbauen
        _grid.Reset();
        _grid.SetupClassicPattern();

        var random = new Random(_currentLevel.Seed ?? Environment.TickCount);

        // Blöcke platzieren
        _grid.PlaceBlocks(_currentLevel.BlockDensity, random);

        // Spieler spawnen bei (1,1)
        _player.SetGridPosition(1, 1);
        _player.MovementDirection = Direction.None;
        _inputManager.Reset(); // Input-State zurücksetzen (verhindert Geister-Bewegung im nächsten Level)

        // PowerUps in Blöcken platzieren
        PlacePowerUps(random);

        // Exit unter einem Block platzieren
        PlaceExit(random);

        // Gegner spawnen
        SpawnEnemies(random);

        // Welt-Theme setzen (basierend auf Level-Nummer)
        int worldIndex = (_currentLevelNumber - 1) / 10;
        _renderer.SetWorldTheme(worldIndex);

        // Timer zurücksetzen
        _timer.Reset(_currentLevel.TimeLimit);

        // Spieler aktivieren
        _player.IsActive = true;

        // Sprites laden falls nötig
        if (!_spriteSheet.IsLoaded)
        {
            await _spriteSheet.LoadAsync();
        }

        // Tutorial starten bei Level 1 wenn noch nicht abgeschlossen
        if (_currentLevelNumber == 1 && !_isArcadeMode && !_tutorialService.IsCompleted)
        {
            _tutorialService.Start();
            _tutorialWarningTimer = 0;
        }
    }

    /// <summary>
    /// Shop-Upgrades auf den Spieler anwenden
    /// </summary>
    private void ApplyUpgrades()
    {
        _player.MaxBombs = _shopService.GetStartBombs();
        _player.FireRange = _shopService.GetStartFire();
        _player.HasSpeed = _shopService.HasStartSpeed();
        _player.Lives = _shopService.GetStartLives(_isArcadeMode);
        _player.HasShield = _shopService.Upgrades.GetLevel(UpgradeType.ShieldStart) >= 1;
    }

    private void PlacePowerUps(Random random)
    {
        var blocks = _grid.GetCellsOfType(CellType.Block).ToList();
        if (blocks.Count == 0 || _currentLevel?.PowerUps == null)
            return;

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
        var blocks = _grid.GetCellsOfType(CellType.Block)
            .Where(c => c.HiddenPowerUp == null)
            .ToList();

        if (blocks.Count == 0)
            return;

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
                    // Fallback: Zufällige Position mit Validierung (keine Wand/Block)
                    pos = (random.Next(5, GameGrid.WIDTH - 2), random.Next(5, GameGrid.HEIGHT - 2));
                    var fallbackCell = _grid.TryGetCell(pos.x, pos.y);
                    if (fallbackCell == null || fallbackCell.Type != CellType.Empty)
                        continue; // Ungültige Position überspringen
                }

                var enemy = Enemy.CreateAtGrid(pos.x, pos.y, spawn.Type);
                _enemies.Add(enemy);
            }
        }
    }

    private void CheckExitReveal()
    {
        if (_exitRevealed)
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
            if (!enemy.IsActive && !enemy.IsDying)
                continue;

            if (enemy.IsActive && !enemy.IsDying)
            {
                _enemyAI.Update(enemy, _player, deltaTime);
            }

            enemy.Update(deltaTime);
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

        // Enemy-Kill-Punkte merken (nur Level-Score, nicht kumulierter Gesamtscore)
        LastEnemyKillPoints = _player.Score - _scoreAtLevelStart;

        // Bonusberechnung mit Shop-Upgrades
        int timeBonusMultiplier = _shopService.GetTimeBonusMultiplier();
        int timeBonus = (int)_timer.RemainingTime * timeBonusMultiplier;

        // Gestufter Effizienzbonus (skaliert nach Welt)
        int world = (_currentLevelNumber - 1) / 10; // 0-4
        int efficiencyBonus = 0;
        if (_bombsUsed <= 5)
            efficiencyBonus = world switch { 0 => 3000, 1 => 5000, 2 => 8000, 3 => 12000, _ => 15000 };
        else if (_bombsUsed <= 8)
            efficiencyBonus = world switch { 0 => 2000, 1 => 3000, 2 => 5000, 3 => 8000, _ => 10000 };
        else if (_bombsUsed <= 12)
            efficiencyBonus = world switch { 0 => 1000, 1 => 1500, 2 => 2500, 3 => 4000, _ => 5000 };

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

        // Sterne-Anzeige: Arcade hat keine Sterne
        if (_isArcadeMode)
            _levelCompleteStars = 0;

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
            coins *= 2;
        OnCoinsEarned?.Invoke(coins, _player.Score, true);

        // Coin-Floating-Text über dem Exit (gold, groß)
        if (coins > 0 && _exitCell != null)
        {
            float coinX = _exitCell.X * Models.Grid.GameGrid.CELL_SIZE + Models.Grid.GameGrid.CELL_SIZE / 2f;
            float coinY = _exitCell.Y * Models.Grid.GameGrid.CELL_SIZE;
            _floatingText.Spawn(coinX, coinY, $"+{coins} Coins", new SKColor(255, 215, 0), 18f, 1.5f);
        }

        // Achievements prüfen (G-R6-1)
        // Score + BestScore ZUERST speichern, damit GetLevelStars/GetTotalStars korrekt sind
        if (!_isArcadeMode)
        {
            _progressService.SetLevelBestScore(_currentLevelNumber, _player.Score);

            int stars = _progressService.GetLevelStars(_currentLevelNumber);
            _levelCompleteStars = stars;
            float timeUsed = _currentLevel!.TimeLimit - _timer.RemainingTime;
            _achievementService.OnLevelCompleted(
                _currentLevelNumber, _player.Score, stars, _bombsUsed,
                _timer.RemainingTime, timeUsed, !_playerDamagedThisLevel);

            // Stern-Fortschritt aktualisieren (jetzt mit aktuellem Score)
            _achievementService.OnStarsUpdated(_progressService.GetTotalStars());
        }
    }

    private void UpdateLevelComplete(float deltaTime)
    {
        _stateTimer += deltaTime;

        if (_stateTimer >= LEVEL_COMPLETE_DELAY && !_levelCompleteHandled)
        {
            _levelCompleteHandled = true;

            // Fortschritt speichern (BestScore bereits in CompleteLevel() gesetzt)
            if (!_isArcadeMode)
            {
                _progressService.CompleteLevel(_currentLevelNumber);
            }

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
                _highScoreService.AddScore("PLAYER", _player.Score, 50);
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
        // Gestaffeltes Pontan-Spawning starten (1 alle 3s statt alle 4 auf einmal)
        _pontanPunishmentActive = true;
        _pontanSpawned = 0;
        _pontanSpawnTimer = 0; // Ersten sofort spawnen
    }

    /// <summary>
    /// Gestaffeltes Pontan-Spawning (weniger unfair als 4 auf einmal)
    /// </summary>
    private void UpdatePontanPunishment(float deltaTime)
    {
        if (!_pontanPunishmentActive || _pontanSpawned >= PONTAN_MAX_COUNT)
        {
            _pontanPunishmentActive = false;
            return;
        }

        _pontanSpawnTimer -= deltaTime;
        if (_pontanSpawnTimer > 0)
            return;

        _pontanSpawnTimer = PONTAN_SPAWN_INTERVAL;

        // Einzelnen Pontan spawnen
        int playerCellX = _player.GridX;
        int playerCellY = _player.GridY;

        for (int attempts = 0; attempts < 40; attempts++)
        {
            int x = _pontanRandom.Next(3, GameGrid.WIDTH - 1);
            int y = _pontanRandom.Next(3, GameGrid.HEIGHT - 1);

            // Mindestabstand zum Spieler
            if (Math.Abs(x - playerCellX) + Math.Abs(y - playerCellY) < PONTAN_MIN_DISTANCE)
                continue;

            var cell = _grid.TryGetCell(x, y);
            if (cell == null || cell.Type != CellType.Empty)
                continue;

            if (cell.Bomb != null || cell.PowerUp != null)
                continue;

            // Kein anderer Enemy auf der Zelle
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

            var enemy = Enemy.CreateAtGrid(x, y, EnemyType.Pontan);
            _enemies.Add(enemy);
            _pontanSpawned++;

            // Warn-Partikel am Spawn-Punkt
            float epx = x * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float epy = y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            _particleSystem.Emit(epx, epy, 8, new SkiaSharp.SKColor(255, 0, 80), 60f, 0.5f);
            break;
        }
    }

    /// <summary>
    /// Zum nächsten Level wechseln
    /// </summary>
    public async Task NextLevelAsync()
    {
        if (_isArcadeMode)
        {
            _arcadeWave++;

            // Wave-Milestone Bonus (Wave 5/10/15/20/25)
            if (_arcadeWave % 5 == 0)
            {
                int bonusCoins = _arcadeWave * 100;
                OnWaveMilestone?.Invoke(_arcadeWave, bonusCoins);
            }

            // Arcade-Achievement prüfen
            _achievementService.OnArcadeWaveReached(_arcadeWave);

            _currentLevel = LevelGenerator.GenerateArcadeLevel(_arcadeWave);

            // Wave-Ankündigung bei Meilensteinen (5/10/15/20/25)
            if (_arcadeWave % 5 == 0)
            {
                _worldAnnouncementText = $"WAVE {_arcadeWave}!";
                _worldAnnouncementTimer = 2.0f;
            }
        }
        else
        {
            _currentLevelNumber++;
            if (_currentLevelNumber > 50)
            {
                _state = GameState.Victory;
                _victoryTimer = 0;
                _victoryHandled = false;
                _timer.Pause();
                _soundManager.PlaySound(SoundManager.SFX_LEVEL_COMPLETE);
                return;
            }
            _currentLevel = LevelGenerator.GenerateLevel(_currentLevelNumber);

            // Welt-Ankündigung bei neuem Welt-Start (Level 11, 21, 31, 41)
            if ((_currentLevelNumber - 1) % 10 == 0)
            {
                int world = (_currentLevelNumber - 1) / 10 + 1;
                _worldAnnouncementText = $"WORLD {world}";
                _worldAnnouncementTimer = 2.0f;
            }
        }

        await LoadLevelAsync();
    }
}
