using BomberBlast.Graphics;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using SkiaSharp;

namespace BomberBlast.Core;

/// <summary>
/// Kollisionserkennung: Spieler/Gegner/Explosionen
/// </summary>
public partial class GameEngine
{
    // Positions-Cache für Gegner (vermeidet O(n) Iteration pro Explosionszelle → O(1) Lookup)
    private readonly Dictionary<(int, int), List<Enemy>> _enemyPositionCache = new();

    // Poison-Schaden-Cooldown (periodisch statt sofort-Kill)
    private float _poisonDamageTimer;
    private const float POISON_DAMAGE_COOLDOWN = 2.0f;

    // Ursprüngliche Gegner-Anzahl bei Level-Start (für Slow-Motion-Bedingung)
    private int _originalEnemyCount;

    // Zähler für periodische Dict-Bereinigung (verwaiste Keys entfernen)
    private int _cacheCleanupCounter;

    /// <summary>
    /// Baut den Positions-Cache für alle aktiven Gegner auf.
    /// Lists werden wiederverwendet statt pro Frame neu allokiert.
    /// Bosse belegen mehrere Zellen und werden unter jeder registriert.
    /// </summary>
    private void RebuildEnemyPositionCache()
    {
        // Lists behalten und nur leeren (keine neue Allokation pro Frame)
        foreach (var list in _enemyPositionCache.Values)
            list.Clear();

        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || enemy.IsDying)
                continue;

            if (enemy is BossEnemy boss)
            {
                for (int bx = 0; bx < boss.BossSize; bx++)
                for (int by = 0; by < boss.BossSize; by++)
                    AddEnemyToCache((boss.GridX + bx, boss.GridY + by), boss);
            }
            else
            {
                AddEnemyToCache((enemy.GridX, enemy.GridY), enemy);
            }
        }

        // Alle 120 Frames verwaiste Keys entfernen (Dict wächst sonst unbegrenzt)
        if (++_cacheCleanupCounter >= 120)
        {
            _cacheCleanupCounter = 0;
            List<(int, int)>? keysToRemove = null;
            foreach (var kvp in _enemyPositionCache)
            {
                if (kvp.Value.Count == 0)
                {
                    keysToRemove ??= new List<(int, int)>();
                    keysToRemove.Add(kvp.Key);
                }
            }
            if (keysToRemove != null)
                foreach (var key in keysToRemove)
                    _enemyPositionCache.Remove(key);
        }
    }

    private void AddEnemyToCache((int, int) key, Enemy enemy)
    {
        if (!_enemyPositionCache.TryGetValue(key, out var list))
        {
            list = new List<Enemy>(4);
            _enemyPositionCache[key] = list;
        }
        list.Add(enemy);
    }

    private void CheckCollisions()
    {
        // Spieler-Kollision mit Explosionen
        foreach (var explosion in _explosions)
        {
            if (!explosion.IsActive)
                continue;

            foreach (var cell in explosion.AffectedCells)
            {
                if (_player.GridX == cell.X && _player.GridY == cell.Y)
                {
                    if (!_player.HasFlamepass && !_player.IsInvincible && !_player.HasSpawnProtection)
                    {
                        KillPlayer();
                    }
                }
            }
        }

        // Spieler-Kollision mit Gegnern (nicht mehr prüfen wenn Spieler bereits stirbt)
        if (!_player.IsDying)
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || enemy.IsDying)
                continue;

            // Unsichtbare Ghosts können den Spieler nicht berühren
            if (enemy.IsInvisible)
                continue;

            // Getarnte Mimics sind inaktiv und harmlos
            if (enemy.IsDisguised)
                continue;

            if (_player.CollidesWith(enemy))
            {
                if (!_player.IsInvincible && !_player.HasSpawnProtection)
                {
                    // Schutzschild absorbiert 1 Gegnerkontakt (nicht Explosionen)
                    if (_player.HasShield)
                    {
                        AbsorbShield(new SKColor(0, 229, 255));
                    }
                    else
                    {
                        KillPlayer();
                    }
                }
            }
        }

        // Spieler-Schaden durch Boss-Spezial-Angriff (AttackTargetCells)
        if (!_player.IsDying && !_player.IsInvincible && !_player.HasSpawnProtection)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy is not BossEnemy boss || !boss.IsActive || boss.IsDying)
                    continue;

                if (!boss.IsAttacking || boss.AttackTargetCells.Count == 0)
                    continue;

                // Spieler auf einer Angriffszelle?
                foreach (var (ax, ay) in boss.AttackTargetCells)
                {
                    if (_player.GridX == ax && _player.GridY == ay)
                    {
                        if (_player.HasShield)
                        {
                            AbsorbShield(new SKColor(255, 80, 0));
                        }
                        else
                        {
                            KillPlayer();
                        }
                        break;
                    }
                }
                if (_player.IsDying) break;
            }
        }

        // Spieler-Schaden durch Spezial-Bomben-Lava (IsLavaActive auf Zelle)
        // Elementar-Synergy: Lava verlangsamt Gegner statt Spieler zu schaden
        if (!_player.IsDying && !_player.IsInvincible && !_player.HasSpawnProtection)
        {
            var playerCell = _grid.TryGetCell(_player.GridX, _player.GridY);
            if (playerCell != null && playerCell.IsLavaActive && !_player.HasFlamepass
                && !_synergyElementalActive)
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

        // Spieler-Schaden durch Gift-Zellen (Poison-Bombe) - periodisch statt sofort
        if (!_player.IsDying && !_player.IsInvincible && !_player.HasSpawnProtection)
        {
            var playerPoisonCell = _grid.TryGetCell(_player.GridX, _player.GridY);
            if (playerPoisonCell != null && playerPoisonCell.IsPoisoned && !_player.HasFlamepass)
            {
                if (_poisonDamageTimer <= 0)
                {
                    if (_player.HasShield)
                    {
                        AbsorbShield(new SKColor(0, 200, 0), particleCount: 12, spread: 60f, playSound: false);
                    }
                    else
                    {
                        KillPlayer();
                    }
                    _poisonDamageTimer = POISON_DAMAGE_COOLDOWN;
                }
            }
        }

        // Spieler-Kollision mit PowerUps (Rückwärts-Iteration statt .ToList())
        for (int i = _powerUps.Count - 1; i >= 0; i--)
        {
            var powerUp = _powerUps[i];
            if (!powerUp.IsActive || powerUp.IsMarkedForRemoval || powerUp.IsBeingCollected)
                continue;

            if (_player.GridX == powerUp.GridX && _player.GridY == powerUp.GridY)
            {
                _player.CollectPowerUp(powerUp);

                // Einsammel-Animation starten (nicht sofort entfernen)
                powerUp.IsBeingCollected = true;
                powerUp.CollectTimer = Models.Entities.PowerUp.COLLECT_DURATION;

                var gridCell = _grid.TryGetCell(powerUp.GridX, powerUp.GridY);
                if (gridCell != null)
                {
                    gridCell.PowerUp = null;
                }

                // PowerUp-Collect Partikel (gold)
                _particleSystem.Emit(powerUp.X, powerUp.Y, 6, ParticleColors.PowerUpCollect, 40f, 0.4f);

                // PowerUp-Collect Floating Text
                string powerUpName = GetPowerUpShortName(powerUp.Type);
                var powerUpColor = GetPowerUpTextColor(powerUp.Type);
                _floatingText.Spawn(powerUp.X, powerUp.Y, powerUpName, powerUpColor, 13f, 1.0f);

                // Discovery-Hint bei Erstentdeckung (nur für nicht-triviale PowerUps)
                if (powerUp.Type.GetUnlockLevel() > 1)
                {
                    TryShowDiscoveryHint("powerup_" + powerUp.Type.ToString().ToLower());
                }

                // Tutorial: PowerUp-Schritt abgeschlossen
                _tutorialService.CheckStepCompletion(TutorialStepType.CollectPowerUp);

                _soundManager.PlaySound(SoundManager.SFX_POWERUP);
                _vibration.VibrateLight();
                ScoreChanged?.Invoke(_player.Score);

                // Tracking: PowerUp eingesammelt (Missionen + Sammlung)
                _tracking.OnPowerUpCollected(powerUp.Type.ToString());
            }
        }

        // Spieler-Kollision mit Exit (gecachte Position statt Grid-Iteration)
        if (_exitRevealed && _exitCell != null)
        {
            if (_player.GridX == _exitCell.X && _player.GridY == _exitCell.Y)
            {
                // Sicherstellen dass ALLE Gegner besiegt sind (inkl. nachträglich gespawnter Pontans)
                bool allEnemiesDead = true;
                foreach (var enemy in _enemies)
                {
                    if (enemy.IsActive && !enemy.IsDying)
                    {
                        allEnemiesDead = false;
                        break;
                    }
                }

                if (allEnemiesDead)
                {
                    // Tutorial: Exit-Schritt abgeschlossen
                    _tutorialService.CheckStepCompletion(TutorialStepType.FindExit);
                    CompleteLevel();
                }
                else
                {
                    // Feedback: Spieler steht auf Exit, aber Gegner leben noch (mit Cooldown)
                    if (_defeatAllCooldown <= 0)
                    {
                        _floatingText.Spawn(_player.X, _player.Y - 16, "DEFEAT ALL!", SKColors.Red, 14f, 1.5f);
                        _defeatAllCooldown = 2f; // Nur alle 2 Sekunden anzeigen
                    }
                }
            }
        }

        // Gegner-Kollision mit Explosionen (Position-Cache → O(1) Lookup statt O(n) pro Zelle)
        RebuildEnemyPositionCache();
        foreach (var explosion in _explosions)
        {
            if (!explosion.IsActive)
                continue;

            foreach (var cell in explosion.AffectedCells)
            {
                if (!_enemyPositionCache.TryGetValue((cell.X, cell.Y), out var enemiesAtCell))
                    continue;

                for (int i = enemiesAtCell.Count - 1; i >= 0; i--)
                {
                    var enemy = enemiesAtCell[i];
                    if (!enemy.IsActive || enemy.IsDying)
                        continue;

                    // Ghost: Unsichtbare Ghosts sind immun gegen Explosionen
                    if (enemy.IsInvisible)
                        continue;

                    // Tanker/Boss: Multi-Hit - TakeDamage gibt true zurück wenn tot
                    if (enemy.HitPoints > 1)
                    {
                        if (enemy.TakeDamage())
                        {
                            KillEnemy(enemy);
                        }
                        else
                        {
                            // Überlebt - visuelles Feedback
                            var (hr, hg, hb) = enemy.Type.GetColor();
                            _particleSystem.Emit(enemy.X, enemy.Y, 6, new SKColor(hr, hg, hb), 60f, 0.3f);

                            // Boss: HP-Anzeige + stärkerer Shake
                            if (enemy is BossEnemy bossHit)
                            {
                                _floatingText.Spawn(enemy.X, enemy.Y - 16,
                                    $"HIT! {bossHit.HitPoints}/{bossHit.MaxHitPoints}",
                                    new SKColor(255, 100, 50), 16f, 1.2f);
                                _screenShake.Trigger(3f, 0.2f);
                                    _vibration.VibrateMedium();

                                // Enrage-Warnung bei 50% HP
                                if (bossHit.IsEnraged && bossHit.HitPoints == bossHit.MaxHitPoints / 2)
                                {
                                    _floatingText.Spawn(enemy.X, enemy.Y - 32,
                                        "ENRAGED!", new SKColor(255, 0, 0), 18f, 1.5f);
                                    _particleSystem.Emit(enemy.X, enemy.Y, 12,
                                        new SKColor(255, 50, 0), 100f, 0.6f);
                                }
                            }
                            else
                            {
                                _floatingText.Spawn(enemy.X, enemy.Y - 12, "HIT!", new SKColor(255, 200, 50), 14f, 1.0f);
                                _screenShake.Trigger(2f, 0.15f);
                            }
                        }
                    }
                    else
                    {
                        KillEnemy(enemy);
                    }
                }
            }
        }
    }

    private void KillPlayer()
    {
        if (_player.IsDying)
            return;

        _playerDamagedThisLevel = true;
        _fortressRegenTimer = 0; // Festungs-Synergy: Timer bei Schaden zurücksetzen
        _player.Kill();
        _timer.Pause();
        _state = GameState.PlayerDied;
        _stateTimer = 0;

        // Game-Feel: Stärkerer Shake + Hit-Pause + Vibration bei Spieler-Tod
        _screenShake.Trigger(5f, 0.3f);
        _hitPauseTimer = 0.1f;
        _vibration.VibrateHeavy();

        // Tod-Partikel: Burst nach außen (orange/rot)
        _particleSystem.EmitShaped(_player.X, _player.Y, 16, new SKColor(255, 100, 30),
            ParticleShape.Circle, 120f, 0.6f, 3f, hasGlow: true);
        _particleSystem.EmitShaped(_player.X, _player.Y, 8, new SKColor(255, 50, 0),
            ParticleShape.Circle, 80f, 0.4f, 2.5f);
        _particleSystem.EmitExplosionSparks(_player.X, _player.Y, 10, new SKColor(255, 180, 50), 140f);

        _soundManager.PlaySound(SoundManager.SFX_PLAYER_DEATH);
    }

    private void KillEnemy(Enemy enemy)
    {
        enemy.Kill();
        _enemiesKilled++;

        // Boss: Eigene Punkte statt EnemyType-Points
        int points = enemy is BossEnemy bossKilled ? bossKilled.BossPoints : enemy.Points;
        _player.Score += points;

        // Boss-Tod: Extra Celebration
        if (enemy is BossEnemy deadBoss)
        {
            _floatingText.Spawn(enemy.X, enemy.Y, $"BOSS DEFEATED! +{points}",
                new SKColor(255, 215, 0), 20f, 2.0f);
            _screenShake.Trigger(6f, 0.4f);
            _particleSystem.EmitShaped(enemy.X, enemy.Y, 24, new SKColor(255, 215, 0),
                ParticleShape.Circle, 140f, 1.0f, 3.5f, hasGlow: true);
            _particleSystem.EmitExplosionSparks(enemy.X, enemy.Y, 16, new SKColor(255, 200, 50), 180f);
            _soundManager.PlaySound(SoundManager.SFX_LEVEL_COMPLETE);

            // Tracking: Boss-Kill (Achievement + Liga + BattlePass + Collection + Missionen)
            var bossFlag = deadBoss.BossKind switch
            {
                BossType.StoneGolem => 1,
                BossType.IceDragon => 2,
                BossType.FireDemon => 4,
                BossType.ShadowMaster => 8,
                BossType.FinalBoss => 16,
                _ => 0
            };
            float bossTime = _currentLevel?.TimeLimit - _timer.RemainingTime ?? 0f;
            _tracking.OnBossKilled(deadBoss.BossKind, bossFlag, bossTime);
        }
        else
        {
            // Score-Popup über dem Gegner (normal)
            _floatingText.Spawn(enemy.X, enemy.Y, $"+{points}", new SKColor(255, 215, 0), 14f);
        }

        // Combo-System: Kills innerhalb des Zeitfensters zählen
        if (_comboTimer > 0)
        {
            _comboCount++;
        }
        else
        {
            _comboCount = 1;
        }
        _comboTimer = COMBO_WINDOW;

        // Combo-Bonus bei Mehrfach-Kills
        if (_comboCount >= 2)
        {
            int comboBonus = _comboCount switch
            {
                2 => 200,
                3 => 500,
                4 => 1000,
                _ => 2000
            };

            // Kettenreaktions-Bonus: Bei 3+ Combo wahrscheinlich eine Kette → 50% extra
            bool isChainKill = _comboCount >= 3;
            if (isChainKill)
            {
                comboBonus = (int)(comboBonus * 1.5f);
            }

            _player.Score += comboBonus;

            // Kettenreaktions-Text hat Vorrang vor normalem Combo-Text
            string comboText;
            SKColor comboColor;
            if (isChainKill)
            {
                comboText = $"CHAIN x{_comboCount}!";
                comboColor = new SKColor(255, 200, 0); // Gold für Chain-Kills
            }
            else
            {
                comboText = $"x{_comboCount}!";
                comboColor = new SKColor(255, 150, 0); // Orange für niedrige Combos
            }
            if (_comboCount >= 5) comboText = $"MEGA x{_comboCount}!";
            if (_comboCount >= 4) comboColor = new SKColor(255, 50, 0); // Rot für hohe Combos

            _floatingText.Spawn(enemy.X, enemy.Y - 12, comboText, comboColor, 18f, 1.5f);

            // Tracking: Combo erreicht (Achievement + Missionen)
            _tracking.OnComboReached(_comboCount);
        }

        // Midas-Synergy: Gegner droppen Mini-Coins bei Tod
        if (_synergyMidasActive && _isDungeonRun)
        {
            int midasCoins = enemy is BossEnemy ? 50 : 10;
            _player.Score += midasCoins;
            _floatingText.Spawn(enemy.X + 8, enemy.Y + 8, $"+{midasCoins}",
                new SKColor(255, 215, 0), 11f, 0.8f);
            _particleSystem.Emit(enemy.X, enemy.Y, 4, new SKColor(255, 215, 0), 40f, 0.4f);
        }

        // Game-Feel: Hit-Pause + Partikel bei Enemy-Kill
        _hitPauseTimer = 0.05f;
        var (r, g, b) = enemy.Type.GetColor();
        _particleSystem.Emit(enemy.X, enemy.Y, 10, new SKColor(r, g, b), 80f, 0.5f);
        _particleSystem.Emit(enemy.X, enemy.Y, 4, ParticleColors.EnemyDeathLight, 50f, 0.3f);

        _soundManager.PlaySound(SoundManager.SFX_ENEMY_DEATH);
        ScoreChanged?.Invoke(_player.Score);

        // Tracking: Enemy-Kill (Achievement + Collection + Missionen)
        _tracking.OnEnemyKilled(enemy.Type, _isSurvivalMode);

        // Slow-Motion bei letztem Kill (nur bei ≥4 Gegnern, Boss-Level oder Survival) oder hohem Combo (x4+)
        bool isLastEnemy = true;
        foreach (var e in _enemies)
        {
            if (e != enemy && e.IsActive && !e.IsDying)
            {
                isLastEnemy = false;
                break;
            }
        }
        bool isBossLevel = _currentLevel?.BossKind != null;
        bool slowMotionWorthy = isLastEnemy && (_originalEnemyCount >= 4 || isBossLevel || _isSurvivalMode);
        if ((slowMotionWorthy || _comboCount >= 4) && !_inputManager.ReducedEffects)
        {
            _slowMotionTimer = SLOW_MOTION_DURATION;
        }

        // Splitter-Logik: Beim Tod 2 Mini-Splitter spawnen
        if (enemy.Type.SplitsOnDeath() && !enemy.IsMiniSplitter)
        {
            var offsets = new[] { (-1, 0), (1, 0), (0, -1), (0, 1) };
            int spawned = 0;
            foreach (var (ox, oy) in offsets)
            {
                if (spawned >= 2) break;
                int nx = enemy.GridX + ox;
                int ny = enemy.GridY + oy;
                var splitCell = _grid.TryGetCell(nx, ny);
                if (splitCell != null && splitCell.Type != CellType.Wall && splitCell.Type != CellType.Block && splitCell.Bomb == null)
                {
                    var mini = Enemy.CreateMiniSplitterAtGrid(nx, ny);
                    _enemies.Add(mini);
                    spawned++;

                    // Spawn-Partikel
                    var (sr, sg, sb) = EnemyType.Splitter.GetColor();
                    _particleSystem.Emit(mini.X, mini.Y, 6, new SKColor(sr, sg, sb), 60f, 0.3f);
                }
            }
        }

        // Prüfen ob alle Gegner besiegt
        CheckExitReveal();

        // Tutorial: DefeatEnemies-Schritt abgeschlossen wenn letzter Gegner besiegt
        if (isLastEnemy)
        {
            _tutorialService.CheckStepCompletion(TutorialStepType.DefeatEnemies);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // POWERUP FLOATING TEXT HELPER
    // ═══════════════════════════════════════════════════════════════════════

    private static string GetPowerUpShortName(PowerUpType type) => type switch
    {
        PowerUpType.BombUp => "+BOMB",
        PowerUpType.Fire => "+FIRE",
        PowerUpType.Speed => "+SPEED",
        PowerUpType.Wallpass => "+WALL",
        PowerUpType.Detonator => "+DET",
        PowerUpType.Bombpass => "+BPASS",
        PowerUpType.Flamepass => "+FLAME",
        PowerUpType.Mystery => "+INVINCIBLE",
        PowerUpType.Kick => "+KICK",
        PowerUpType.LineBomb => "+LINE",
        PowerUpType.PowerBomb => "+POWER",
        PowerUpType.Skull => "CURSED!",
        _ => "+???"
    };

    private static SKColor GetPowerUpTextColor(PowerUpType type) => type switch
    {
        PowerUpType.BombUp => new SKColor(100, 150, 255),
        PowerUpType.Fire => new SKColor(255, 130, 50),
        PowerUpType.Speed => new SKColor(80, 255, 100),
        PowerUpType.Wallpass => new SKColor(200, 150, 80),
        PowerUpType.Detonator => new SKColor(255, 80, 80),
        PowerUpType.Bombpass => new SKColor(120, 120, 255),
        PowerUpType.Flamepass => new SKColor(255, 220, 60),
        PowerUpType.Mystery => new SKColor(200, 120, 255),
        PowerUpType.Kick => new SKColor(255, 165, 0),
        PowerUpType.LineBomb => new SKColor(0, 200, 255),
        PowerUpType.PowerBomb => new SKColor(255, 50, 50),
        PowerUpType.Skull => new SKColor(100, 0, 100),
        _ => SKColors.White
    };
}
