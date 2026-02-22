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
                        _player.HasShield = false;
                        // Partikel-Burst bei Shield-Absorption (Cyan)
                        _particleSystem.Emit(_player.X, _player.Y, 16,
                            new SKColor(0, 229, 255), 80f, 0.6f);
                        _floatingText.Spawn(_player.X, _player.Y - 16,
                            "SHIELD!", new SKColor(0, 229, 255), 16f, 1.2f);
                        _soundManager.PlaySound(SoundManager.SFX_POWERUP);
                        // Kurze Unverwundbarkeit nach Shield-Verbrauch (0.5s)
                        _player.ActivateInvincibility(0.5f);
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
                            _player.HasShield = false;
                            _particleSystem.Emit(_player.X, _player.Y, 16,
                                new SKColor(255, 80, 0), 80f, 0.6f);
                            _floatingText.Spawn(_player.X, _player.Y - 16,
                                "SHIELD!", new SKColor(0, 229, 255), 16f, 1.2f);
                            _soundManager.PlaySound(SoundManager.SFX_POWERUP);
                            _player.ActivateInvincibility(0.5f);
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
        if (!_player.IsDying && !_player.IsInvincible && !_player.HasSpawnProtection)
        {
            var playerCell = _grid.TryGetCell(_player.GridX, _player.GridY);
            if (playerCell != null && playerCell.IsLavaActive && !_player.HasFlamepass)
            {
                if (_player.HasShield)
                {
                    _player.HasShield = false;
                    _particleSystem.Emit(_player.X, _player.Y, 12,
                        new SKColor(255, 80, 0), 60f, 0.5f);
                    _floatingText.Spawn(_player.X, _player.Y - 16,
                        "SHIELD!", new SKColor(0, 229, 255), 16f, 1.2f);
                    _player.ActivateInvincibility(0.5f);
                }
                else
                {
                    KillPlayer();
                }
            }
        }

        // Spieler-Schaden durch Gift-Zellen (Poison-Bombe)
        if (!_player.IsDying && !_player.IsInvincible && !_player.HasSpawnProtection)
        {
            var playerPoisonCell = _grid.TryGetCell(_player.GridX, _player.GridY);
            if (playerPoisonCell != null && playerPoisonCell.IsPoisoned && !_player.HasFlamepass)
            {
                if (_player.HasShield)
                {
                    _player.HasShield = false;
                    _particleSystem.Emit(_player.X, _player.Y, 12,
                        new SKColor(0, 200, 0), 60f, 0.5f);
                    _floatingText.Spawn(_player.X, _player.Y - 16,
                        "SHIELD!", new SKColor(0, 229, 255), 16f, 1.2f);
                    _player.ActivateInvincibility(0.5f);
                }
                else
                {
                    KillPlayer();
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
                OnScoreChanged?.Invoke(_player.Score);

                // Wöchentliche Challenge: PowerUp-Collect tracken
                _weeklyService.TrackProgress(WeeklyMissionType.CollectPowerUps);
                _dailyMissionService.TrackProgress(WeeklyMissionType.CollectPowerUps);

                // Sammlungs-Album: PowerUp als eingesammelt melden
                _collectionService.RecordPowerUpCollected(powerUp.Type.ToString());
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

        // Gegner-Kollision mit Explosionen (Grid-Lookup statt Triple-Loop)
        foreach (var explosion in _explosions)
        {
            if (!explosion.IsActive)
                continue;

            foreach (var cell in explosion.AffectedCells)
            {
                // Rückwärts iterieren, da KillEnemy den Zustand ändert
                for (int i = _enemies.Count - 1; i >= 0; i--)
                {
                    var enemy = _enemies[i];
                    if (!enemy.IsActive || enemy.IsDying)
                        continue;

                    // Boss: Multi-Cell Kollision (OccupiesCell statt einzelner GridX/GridY)
                    bool hitByExplosion;
                    if (enemy is BossEnemy bossTarget)
                        hitByExplosion = bossTarget.OccupiesCell(cell.X, cell.Y);
                    else
                        hitByExplosion = enemy.GridX == cell.X && enemy.GridY == cell.Y;

                    if (hitByExplosion)
                    {
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
    }

    private void KillPlayer()
    {
        if (_player.IsDying)
            return;

        _playerDamagedThisLevel = true;
        _player.Kill();
        _timer.Pause();
        _state = GameState.PlayerDied;
        _stateTimer = 0;

        // Game-Feel: Stärkerer Shake + Hit-Pause bei Spieler-Tod
        _screenShake.Trigger(5f, 0.3f);
        _hitPauseTimer = 0.1f;

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

            // Achievement: Boss-Typ zu Bit-Flag konvertieren
            var bossFlag = deadBoss.BossKind switch
            {
                BossType.StoneGolem => 1,
                BossType.IceDragon => 2,
                BossType.FireDemon => 4,
                BossType.ShadowMaster => 8,
                BossType.FinalBoss => 16,
                _ => 0
            };
            _achievementService.OnBossDefeated(bossFlag);

            // Liga-Punkte für Boss-Kill
            _leagueService.AddPoints(25);

            // Battle Pass XP für Boss-Kill
            _battlePassService.AddXp(200, "boss_kill");
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
            _player.Score += comboBonus;

            string comboText = _comboCount >= 5 ? $"MEGA x{_comboCount}!" : $"x{_comboCount}!";
            var comboColor = _comboCount >= 4
                ? new SKColor(255, 50, 0)   // Rot für hohe Combos
                : new SKColor(255, 150, 0); // Orange für niedrige Combos
            _floatingText.Spawn(enemy.X, enemy.Y - 12, comboText, comboColor, 18f, 1.5f);

            // Achievement: Combo-Schwellen prüfen
            _achievementService.OnComboReached(_comboCount);

            // Wöchentliche Challenge: Combo tracken (ab x2)
            _weeklyService.TrackProgress(WeeklyMissionType.AchieveCombo);
            _dailyMissionService.TrackProgress(WeeklyMissionType.AchieveCombo);
        }

        // Game-Feel: Hit-Pause + Partikel bei Enemy-Kill
        _hitPauseTimer = 0.05f;
        var (r, g, b) = enemy.Type.GetColor();
        _particleSystem.Emit(enemy.X, enemy.Y, 10, new SKColor(r, g, b), 80f, 0.5f);
        _particleSystem.Emit(enemy.X, enemy.Y, 4, ParticleColors.EnemyDeathLight, 50f, 0.3f);

        _soundManager.PlaySound(SoundManager.SFX_ENEMY_DEATH);
        OnScoreChanged?.Invoke(_player.Score);

        // Achievement: Kumulative Kills aktualisieren
        _achievementService.OnEnemyKilled(_achievementService.TotalEnemyKills + 1);

        // Sammlungs-Album: Gegner als gesichtet + besiegt melden
        _collectionService.RecordEnemyEncounter(enemy.Type);
        _collectionService.RecordEnemyDefeat(enemy.Type);
        if (enemy is BossEnemy defeatedBoss)
        {
            float bossTime = _currentLevel?.TimeLimit - _timer.RemainingTime ?? 0f;
            _collectionService.RecordBossDefeat(defeatedBoss.BossKind, bossTime);
        }

        // Wöchentliche Challenge: Enemy-Kill + ggf. Boss-Kill tracken
        _weeklyService.TrackProgress(WeeklyMissionType.DefeatEnemies);
        if (_isSurvivalMode)
            _weeklyService.TrackProgress(WeeklyMissionType.SurvivalKills);
        if (enemy is BossEnemy)
            _weeklyService.TrackProgress(WeeklyMissionType.WinBossFights);

        // Tägliche Mission: Enemy-Kill + ggf. Boss-Kill tracken
        _dailyMissionService.TrackProgress(WeeklyMissionType.DefeatEnemies);
        if (_isSurvivalMode)
            _dailyMissionService.TrackProgress(WeeklyMissionType.SurvivalKills);
        if (enemy is BossEnemy)
            _dailyMissionService.TrackProgress(WeeklyMissionType.WinBossFights);

        // Slow-Motion bei letztem Kill oder hohem Combo (x4+)
        bool isLastEnemy = true;
        foreach (var e in _enemies)
        {
            if (e != enemy && e.IsActive && !e.IsDying)
            {
                isLastEnemy = false;
                break;
            }
        }
        if ((isLastEnemy || _comboCount >= 4) && !_inputManager.ReducedEffects)
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
