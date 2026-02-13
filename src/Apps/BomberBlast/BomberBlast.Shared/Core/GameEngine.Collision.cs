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

        // Spieler-Kollision mit Gegnern
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || enemy.IsDying)
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

                // Tutorial: PowerUp-Schritt abgeschlossen
                _tutorialService.CheckStepCompletion(TutorialStepType.CollectPowerUp);

                _soundManager.PlaySound(SoundManager.SFX_POWERUP);
                OnScoreChanged?.Invoke(_player.Score);
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

                    if (enemy.GridX == cell.X && enemy.GridY == cell.Y)
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
        _player.Kill();
        _timer.Pause();
        _state = GameState.PlayerDied;
        _stateTimer = 0;

        // Game-Feel: Stärkerer Shake + Hit-Pause bei Spieler-Tod
        _screenShake.Trigger(5f, 0.3f);
        _hitPauseTimer = 0.1f;

        _soundManager.PlaySound(SoundManager.SFX_PLAYER_DEATH);
    }

    private void KillEnemy(Enemy enemy)
    {
        enemy.Kill();
        _enemiesKilled++;
        _player.Score += enemy.Points;

        // Score-Popup über dem Gegner
        _floatingText.Spawn(enemy.X, enemy.Y, $"+{enemy.Points}", new SKColor(255, 215, 0), 14f);

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
        if (isLastEnemy || _comboCount >= 4)
        {
            _slowMotionTimer = SLOW_MOTION_DURATION;
        }

        // Prüfen ob alle Gegner besiegt
        CheckExitReveal();
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
