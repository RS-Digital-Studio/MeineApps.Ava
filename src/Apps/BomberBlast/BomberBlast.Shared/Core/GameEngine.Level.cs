using BomberBlast.Graphics;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Models.Levels;

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
        ApplyUpgrades();
        _player.Lives = 1; // Arcade: immer 1 Leben
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

        // Entities leeren
        _enemies.Clear();
        _bombs.Clear();
        _explosions.Clear();
        _powerUps.Clear();
        _particleSystem.Clear();
        _screenShake.Reset();
        _hitPauseTimer = 0;

        // Grid aufbauen
        _grid.Reset();
        _grid.SetupClassicPattern();

        var random = new Random(_currentLevel.Seed ?? DateTime.Now.Millisecond);

        // Blöcke platzieren
        _grid.PlaceBlocks(_currentLevel.BlockDensity, random);

        // Spieler spawnen bei (1,1)
        _player.SetGridPosition(1, 1);
        _player.MovementDirection = Direction.None;

        // PowerUps in Blöcken platzieren
        PlacePowerUps(random);

        // Exit unter einem Block platzieren
        PlaceExit(random);

        // Gegner spawnen
        SpawnEnemies(random);

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
            // Exit weit weg vom Spieler-Spawn platzieren (manuelles Maximum statt LINQ)
            exitCell = blocks[0];
            int maxDist = Math.Abs(exitCell.X - 1) + Math.Abs(exitCell.Y - 1);
            for (int i = 1; i < blocks.Count; i++)
            {
                int dist = Math.Abs(blocks[i].X - 1) + Math.Abs(blocks[i].Y - 1);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    exitCell = blocks[i];
                }
            }
        }

        exitCell.HiddenPowerUp = null;
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
                    pos = (random.Next(5, GameGrid.WIDTH - 2), random.Next(5, GameGrid.HEIGHT - 2));
                }

                var enemy = Enemy.CreateAtGrid(pos.x, pos.y, spawn.Type);
                _enemies.Add(enemy);
            }
        }
    }

    private void CheckExitReveal()
    {
        if (!_exitRevealed && _enemies.All(e => !e.IsActive || e.IsDying))
        {
            RevealExit();
        }
    }

    private void RevealExit()
    {
        _exitRevealed = true;

        // Manuelles Maximum suchen (statt OrderByDescending + LINQ-Allokation)
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
            _soundManager.PlaySound(SoundManager.SFX_EXIT_APPEAR);

            // Exit-Reveal Partikel (grün)
            float epx = bestCell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float epy = bestCell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            _particleSystem.Emit(epx, epy, 12, ParticleColors.ExitReveal, 60f, 0.8f);
            _particleSystem.Emit(epx, epy, 6, ParticleColors.ExitRevealLight, 40f, 0.5f);
        }
    }

    private void UpdateEnemies(float deltaTime)
    {
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive && !enemy.IsDying)
                continue;

            if (enemy.IsActive && !enemy.IsDying)
            {
                _enemyAI.Update(enemy, _player, _bombs, deltaTime);
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

        // Enemy-Kill-Punkte merken (Score vor Bonus)
        LastEnemyKillPoints = _player.Score;

        // Bonusberechnung mit Shop-Upgrades
        int timeBonusMultiplier = _shopService.GetTimeBonusMultiplier();
        int timeBonus = (int)_timer.RemainingTime * timeBonusMultiplier;

        // Gestufter Effizienzbonus
        int efficiencyBonus = _bombsUsed <= 5 ? 15000 : _bombsUsed <= 8 ? 10000 : _bombsUsed <= 12 ? 5000 : 0;

        _player.Score += timeBonus + efficiencyBonus;

        // Score-Multiplikator anwenden
        float scoreMultiplier = _shopService.GetScoreMultiplier();
        if (scoreMultiplier > 1.0f)
        {
            _player.Score = (int)(_player.Score * scoreMultiplier);
        }

        // Score-Aufschlüsselung speichern
        LastTimeBonus = timeBonus;
        LastEfficiencyBonus = efficiencyBonus;
        LastScoreMultiplier = scoreMultiplier;

        _soundManager.PlaySound(SoundManager.SFX_LEVEL_COMPLETE);
        OnScoreChanged?.Invoke(_player.Score);

        // Coins (Premium: doppelt)
        int coins = _player.Score;
        if (_purchaseService.IsPremium)
            coins *= 2;
        OnCoinsEarned?.Invoke(coins, _player.Score, true);
    }

    private void UpdateLevelComplete(float deltaTime)
    {
        _stateTimer += deltaTime;

        if (_stateTimer >= LEVEL_COMPLETE_DELAY && !_levelCompleteHandled)
        {
            _levelCompleteHandled = true;

            // Fortschritt speichern
            if (!_isArcadeMode)
            {
                _progressService.CompleteLevel(_currentLevelNumber);
                _progressService.SetLevelBestScore(_currentLevelNumber, _player.Score);
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

            // Coins
            int coins = _player.Score;
            if (_purchaseService.IsPremium) coins *= 2;
            OnCoinsEarned?.Invoke(coins, _player.Score, true);

            OnVictory?.Invoke();
        }
    }

    private void OnTimeWarning()
    {
        _soundManager.PlaySound(SoundManager.SFX_TIME_WARNING);
    }

    private void OnTimeExpired()
    {
        SpawnPontanPunishment();
    }

    private void SpawnPontanPunishment()
    {
        var random = new Random();
        int spawned = 0;
        int maxAttempts = 80;
        int attempts = 0;
        int playerCellX = _player.GridX;
        int playerCellY = _player.GridY;

        while (spawned < 4 && attempts < maxAttempts)
        {
            attempts++;
            int x = random.Next(3, GameGrid.WIDTH - 1);
            int y = random.Next(3, GameGrid.HEIGHT - 1);

            // Mindestabstand 4 Zellen zum Spieler (erhöht für Fairness)
            if (Math.Abs(x - playerCellX) + Math.Abs(y - playerCellY) < 4)
                continue;

            var cell = _grid.TryGetCell(x, y);
            if (cell == null || cell.Type != CellType.Empty)
                continue;

            // Keine Bomben oder PowerUps auf der Zelle
            if (cell.Bomb != null || cell.PowerUp != null)
                continue;

            // Kein anderer Enemy bereits auf der Zelle
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
            spawned++;
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

            _currentLevel = LevelGenerator.GenerateArcadeLevel(_arcadeWave);
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
        }

        await LoadLevelAsync();
    }
}
