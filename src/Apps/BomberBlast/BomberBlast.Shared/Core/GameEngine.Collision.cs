using BomberBlast.Graphics;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;

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
                    KillPlayer();
                }
            }
        }

        // Spieler-Kollision mit PowerUps (Rückwärts-Iteration statt .ToList())
        for (int i = _powerUps.Count - 1; i >= 0; i--)
        {
            var powerUp = _powerUps[i];
            if (!powerUp.IsActive || powerUp.IsMarkedForRemoval)
                continue;

            if (_player.GridX == powerUp.GridX && _player.GridY == powerUp.GridY)
            {
                _player.CollectPowerUp(powerUp);
                powerUp.IsMarkedForRemoval = true;

                var gridCell = _grid.TryGetCell(powerUp.GridX, powerUp.GridY);
                if (gridCell != null)
                {
                    gridCell.PowerUp = null;
                }

                // PowerUp-Collect Partikel (gold)
                _particleSystem.Emit(powerUp.X, powerUp.Y, 6, ParticleColors.PowerUpCollect, 40f, 0.4f);

                // Tutorial: PowerUp-Schritt abgeschlossen
                _tutorialService.CheckStepCompletion(TutorialStepType.CollectPowerUp);

                _soundManager.PlaySound(SoundManager.SFX_POWERUP);
                OnScoreChanged?.Invoke(_player.Score);
            }
        }

        // Spieler-Kollision mit Exit
        if (_exitRevealed)
        {
            foreach (var cell in _grid.GetCellsOfType(CellType.Exit))
            {
                if (_player.GridX == cell.X && _player.GridY == cell.Y)
                {
                    // Tutorial: Exit-Schritt abgeschlossen
                    _tutorialService.CheckStepCompletion(TutorialStepType.FindExit);
                    CompleteLevel();
                }
            }
        }

        // Gegner-Kollision mit Explosionen
        foreach (var explosion in _explosions)
        {
            if (!explosion.IsActive)
                continue;

            foreach (var cell in explosion.AffectedCells)
            {
                foreach (var enemy in _enemies)
                {
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

        // Game-Feel: Hit-Pause + Partikel bei Enemy-Kill
        _hitPauseTimer = 0.05f;
        var (r, g, b) = enemy.Type.GetColor();
        _particleSystem.Emit(enemy.X, enemy.Y, 10, new SkiaSharp.SKColor(r, g, b), 80f, 0.5f);
        _particleSystem.Emit(enemy.X, enemy.Y, 4, ParticleColors.EnemyDeathLight, 50f, 0.3f);

        _soundManager.PlaySound(SoundManager.SFX_ENEMY_DEATH);
        OnScoreChanged?.Invoke(_player.Score);

        // Prüfen ob alle Gegner besiegt
        CheckExitReveal();
    }

    private void CheckWinCondition()
    {
        // Wird über CheckExitReveal und Player-Exit-Kollision abgedeckt
    }
}
