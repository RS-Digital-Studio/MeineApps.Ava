using BomberBlast.Graphics;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;

namespace BomberBlast.Core;

/// <summary>
/// Bomben, Explosionen und Block-Zerstörung
/// </summary>
public partial class GameEngine
{
    private void PlaceBomb()
    {
        int gridX = _player.GridX;
        int gridY = _player.GridY;

        var cell = _grid[gridX, gridY];

        // Prüfen ob schon eine Bombe hier liegt
        if (cell.Bomb != null)
            return;

        // Bombe erstellen
        var bomb = Bomb.CreateAtGrid(gridX, gridY, _player);
        _bombs.Add(bomb);
        cell.Bomb = bomb;
        _player.ActiveBombs++;
        _bombsUsed++;

        _soundManager.PlaySound(SoundManager.SFX_PLACE_BOMB);
    }

    private void DetonateAllBombs()
    {
        foreach (var bomb in _bombs)
        {
            if (bomb.IsManualDetonation && bomb.IsActive && !bomb.HasExploded)
            {
                bomb.ShouldExplode = true;
            }
        }
    }

    private void UpdateBombs(float deltaTime)
    {
        foreach (var bomb in _bombs)
        {
            bomb.Update(deltaTime);

            // Prüfen ob Spieler komplett von Bombe runtergelaufen ist
            if (bomb.PlayerOnTop)
            {
                float size = GameGrid.CELL_SIZE * 0.35f;

                bool stillOnBomb = false;
                float[] cornersX = { _player.X - size, _player.X + size };
                float[] cornersY = { _player.Y - size, _player.Y + size };

                foreach (float cx in cornersX)
                {
                    foreach (float cy in cornersY)
                    {
                        int cellX = (int)(cx / GameGrid.CELL_SIZE);
                        int cellY = (int)(cy / GameGrid.CELL_SIZE);
                        if (cellX == bomb.GridX && cellY == bomb.GridY)
                        {
                            stillOnBomb = true;
                            break;
                        }
                    }
                    if (stillOnBomb) break;
                }

                if (!stillOnBomb)
                {
                    bomb.PlayerOnTop = false;
                }
            }

            // Explosion auslösen wenn fällig
            if (bomb.ShouldExplode && !bomb.HasExploded)
            {
                TriggerExplosion(bomb);
            }
        }
    }

    private void TriggerExplosion(Bomb bomb)
    {
        bomb.Explode();

        // Bombe aus Grid entfernen
        var cell = _grid.TryGetCell(bomb.GridX, bomb.GridY);
        if (cell != null)
        {
            cell.Bomb = null;
        }

        // Explosion erstellen
        var explosion = new Explosion(bomb);
        explosion.CalculateSpread(_grid, bomb.Range);
        _explosions.Add(explosion);

        _soundManager.PlaySound(SoundManager.SFX_EXPLOSION);

        // Game-Feel: Screen-Shake und Explosions-Partikel
        _screenShake.Trigger(3f, 0.2f);
        float px = bomb.X;
        float py = bomb.Y;
        _particleSystem.Emit(px, py, 8, ParticleColors.Explosion, 100f, 0.5f);
        _particleSystem.Emit(px, py, 4, ParticleColors.ExplosionLight, 60f, 0.3f);

        // Explosionseffekte sofort verarbeiten
        ProcessExplosion(explosion);
    }

    private void ProcessExplosion(Explosion explosion)
    {
        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = _grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null)
                continue;

            // Blöcke zerstören
            if (gridCell.Type == CellType.Block && !gridCell.IsDestroying)
            {
                DestroyBlock(gridCell);
            }

            // Kettenreaktion mit anderen Bomben
            if (gridCell.Bomb != null && !gridCell.Bomb.HasExploded)
            {
                gridCell.Bomb.TriggerChainReaction();
            }

            // PowerUps auf dem Boden zerstören
            if (gridCell.PowerUp != null)
            {
                gridCell.PowerUp.IsMarkedForRemoval = true;
                gridCell.PowerUp = null;
            }
        }
    }

    private const float BLOCK_DESTROY_DURATION = 0.3f;

    private void DestroyBlock(Cell cell)
    {
        cell.IsDestroying = true;
        cell.DestructionProgress = 0f;
    }

    /// <summary>
    /// Timer-basierte Block-Zerstörung (statt Dispatcher.Post + Task.Delay)
    /// </summary>
    private void UpdateDestroyingBlocks(float deltaTime)
    {
        for (int y = 0; y < _grid.Height; y++)
        {
            for (int x = 0; x < _grid.Width; x++)
            {
                var cell = _grid[x, y];
                if (!cell.IsDestroying)
                    continue;

                cell.DestructionProgress += deltaTime / BLOCK_DESTROY_DURATION;

                if (cell.DestructionProgress >= 1f)
                {
                    cell.Type = CellType.Empty;
                    cell.IsDestroying = false;
                    cell.DestructionProgress = 0f;

                    // Block-Zerstörungs-Partikel
                    float bpx = cell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                    float bpy = cell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                    _particleSystem.Emit(bpx, bpy, 5, ParticleColors.BlockDestroy, 50f, 0.4f);
                    _particleSystem.Emit(bpx, bpy, 3, ParticleColors.BlockDestroyLight, 30f, 0.3f);

                    // PowerUp anzeigen wenn versteckt
                    if (cell.HiddenPowerUp.HasValue)
                    {
                        var powerUp = PowerUp.CreateAtGrid(cell.X, cell.Y, cell.HiddenPowerUp.Value);
                        _powerUps.Add(powerUp);
                        cell.PowerUp = powerUp;
                        cell.HiddenPowerUp = null;

                        _soundManager.PlaySound(SoundManager.SFX_POWERUP);
                    }

                    CheckExitReveal();
                }
            }
        }
    }

    private void UpdateExplosions(float deltaTime)
    {
        foreach (var explosion in _explosions)
        {
            explosion.Update(deltaTime);

            if (explosion.IsMarkedForRemoval)
            {
                explosion.ClearFromGrid(_grid);
            }
        }
    }
}
