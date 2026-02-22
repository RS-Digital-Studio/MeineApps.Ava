using BomberBlast.Graphics;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using SkiaSharp;

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

        // Power-Bomb: Einzelne Mega-Bombe mit maximaler Reichweite, verbraucht alle Slots
        if (_player.HasPowerBomb && _player.ActiveBombs == 0)
        {
            PlacePowerBomb(gridX, gridY, cell);
            return;
        }

        // Line-Bomb: Alle verfügbaren Bomben in einer Linie platzieren
        if (_player.HasLineBomb && _player.ActiveBombs == 0)
        {
            PlaceLineBombs(gridX, gridY);
            return;
        }

        // Normale Bombe erstellen
        var bomb = Bomb.CreateAtGrid(gridX, gridY, _player);

        // Karten-Deck: Aktive Karte als Bomben-Typ setzen wenn Uses vorhanden
        var activeCard = _player.ActiveCard;
        if (activeCard != null && activeCard.HasUsesLeft)
        {
            bomb.Type = activeCard.BombType;
            activeCard.RemainingUses--;

            // Achievement: Spezial-Bombe platziert
            _achievementService.OnSpecialBombUsed();

            // Wöchentliche/Tägliche Challenge: Spezial-Bombe tracken
            _weeklyService.TrackProgress(WeeklyMissionType.UseSpecialBombs);
            _dailyMissionService.TrackProgress(WeeklyMissionType.UseSpecialBombs);

            // Wenn keine Uses mehr → automatisch auf Normal zurückschalten
            if (!activeCard.HasUsesLeft)
            {
                _player.ActiveCardSlot = -1;
            }
        }

        _bombs.Add(bomb);
        cell.Bomb = bomb;
        _player.ActiveBombs++;
        _bombsUsed++;

        _soundManager.PlaySound(SoundManager.SFX_PLACE_BOMB);
        _soundManager.PlaySound(SoundManager.SFX_FUSE);
    }

    /// <summary>
    /// Power-Bomb: Eine einzelne Bombe die alle Slots verbraucht und maximale Reichweite hat
    /// </summary>
    private void PlacePowerBomb(int gridX, int gridY, Cell cell)
    {
        // Reichweite = FireRange + (MaxBombs - 1), mindestens FireRange
        int megaRange = _player.FireRange + _player.MaxBombs - 1;
        var bomb = new Bomb(
            gridX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f,
            gridY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f,
            _player, megaRange, _player.HasDetonator);
        bomb.SlotsConsumed = _player.MaxBombs; // Alle Slots belegt, alle bei Explosion freigeben
        _bombs.Add(bomb);
        cell.Bomb = bomb;
        _player.ActiveBombs = _player.MaxBombs; // Alle Slots belegt
        _bombsUsed++;

        _soundManager.PlaySound(SoundManager.SFX_PLACE_BOMB);
        _soundManager.PlaySound(SoundManager.SFX_FUSE);

        // Achievement: Power-Bomb Einsatz zählen
        _achievementService.OnPowerBombUsed();
    }

    /// <summary>
    /// Line-Bomb: Bomben in Blickrichtung auf leeren Zellen platzieren
    /// </summary>
    private void PlaceLineBombs(int startX, int startY)
    {
        int dx = _player.FacingDirection.GetDeltaX();
        int dy = _player.FacingDirection.GetDeltaY();
        if (dx == 0 && dy == 0) { dx = 0; dy = 1; } // Fallback: nach unten

        int placed = 0;
        int maxBombs = _player.MaxBombs;

        for (int i = 0; i < maxBombs; i++)
        {
            int gx = startX + dx * i;
            int gy = startY + dy * i;

            var cell = _grid.TryGetCell(gx, gy);
            if (cell == null || cell.Type != CellType.Empty || cell.Bomb != null)
                break;

            var bomb = Bomb.CreateAtGrid(gx, gy, _player);
            _bombs.Add(bomb);
            cell.Bomb = bomb;
            _player.ActiveBombs++;
            _bombsUsed++;
            placed++;
        }

        if (placed > 0)
        {
            _soundManager.PlaySound(SoundManager.SFX_PLACE_BOMB);
            _soundManager.PlaySound(SoundManager.SFX_FUSE);

            // Achievement: Line-Bomb eingesetzt
            _achievementService.OnLineBombUsed();
        }
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
            // TimeWarp: Bomben auf zeitverzerrten Zellen ticken 50% langsamer
            float bombDt = deltaTime;
            var bombCell = _grid.TryGetCell(bomb.GridX, bomb.GridY);
            if (bombCell != null && bombCell.IsTimeWarped)
                bombDt *= 0.5f;

            bomb.Update(bombDt);

            // Kick-Sliding: Bombe gleitet in Richtung bis Hindernis
            if (bomb.IsSliding && !bomb.HasExploded)
            {
                UpdateBombSlide(bomb, deltaTime);
            }

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
                        int cellX = (int)MathF.Floor(cx / GameGrid.CELL_SIZE);
                        int cellY = (int)MathF.Floor(cy / GameGrid.CELL_SIZE);
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

    /// <summary>
    /// Gleitende Bombe aktualisieren (Kick-Mechanik)
    /// </summary>
    private void UpdateBombSlide(Bomb bomb, float deltaTime)
    {
        float dx = bomb.SlideDirection.GetDeltaX() * Bomb.SLIDE_SPEED * deltaTime;
        float dy = bomb.SlideDirection.GetDeltaY() * Bomb.SLIDE_SPEED * deltaTime;

        float newX = bomb.X + dx;
        float newY = bomb.Y + dy;

        // Ziel-Grid-Position berechnen
        int targetGridX = (int)MathF.Floor(newX / GameGrid.CELL_SIZE);
        int targetGridY = (int)MathF.Floor(newY / GameGrid.CELL_SIZE);

        // Prüfen ob Zielzelle blockiert ist
        var targetCell = _grid.TryGetCell(targetGridX, targetGridY);
        if (targetCell == null || targetCell.Type != CellType.Empty ||
            (targetCell.Bomb != null && targetCell.Bomb != bomb))
        {
            // Hindernis: Bombe stoppen und an aktuelle Zellenmitte einrasten
            bomb.StopSlide();
            // Bombe in aktuelle Grid-Zelle registrieren
            var snapCell = _grid.TryGetCell(bomb.GridX, bomb.GridY);
            if (snapCell != null) snapCell.Bomb = bomb;
            return;
        }

        // Prüfen ob ein Gegner auf der Zielzelle steht
        foreach (var enemy in _enemies)
        {
            if (enemy.IsActive && !enemy.IsDying &&
                enemy.GridX == targetGridX && enemy.GridY == targetGridY)
            {
                bomb.StopSlide();
                var snapCell = _grid.TryGetCell(bomb.GridX, bomb.GridY);
                if (snapCell != null) snapCell.Bomb = bomb;
                return;
            }
        }

        // Alte Grid-Zelle freiräumen
        var oldCell = _grid.TryGetCell(bomb.GridX, bomb.GridY);
        if (oldCell != null && oldCell.Bomb == bomb) oldCell.Bomb = null;

        // Bombe bewegen
        bomb.X = newX;
        bomb.Y = newY;

        // Neue Grid-Zelle setzen
        var newCell = _grid.TryGetCell(bomb.GridX, bomb.GridY);
        if (newCell != null) newCell.Bomb = bomb;
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

        // Game-Feel: Screen-Shake und Partikel eskalieren mit Kettenreaktions-Tiefe
        int depth = bomb.ChainDepth;
        float shakeIntensity = 3f + depth * 1.5f;
        float shakeDuration = 0.2f + depth * 0.05f;
        _screenShake.Trigger(shakeIntensity, shakeDuration);

        float px = bomb.X;
        float py = bomb.Y;
        int sparkCount = 12 + depth * 4;  // Mehr Funken bei Kettenreaktion
        int emberCount = 6 + depth * 2;

        // Funken die nach außen fliegen (schnell, leuchtend, elongiert)
        _particleSystem.EmitExplosionSparks(px, py, sparkCount, ParticleColors.ExplosionSpark, 160f + depth * 20f);

        // Glut-Partikel die langsam aufsteigen (glühend, Glow)
        _particleSystem.EmitEmbers(px, py, emberCount, ParticleColors.ExplosionEmber);
        _particleSystem.EmitEmbers(px, py, 3 + depth, ParticleColors.ExplosionEmberBright);

        // Klassische Partikel für Volumen
        _particleSystem.EmitShaped(px, py, 6 + depth * 2, ParticleColors.Explosion,
            ParticleShape.Circle, 80f + depth * 15f, 0.5f, 2.5f, hasGlow: true);
        _particleSystem.EmitShaped(px, py, 4 + depth, ParticleColors.ExplosionLight,
            ParticleShape.Circle, 50f, 0.3f, 2f);

        // Explosionseffekte sofort verarbeiten
        ProcessExplosion(explosion);

        // Spezial-Bomben-Effekte
        switch (bomb.Type)
        {
            case BombType.Ice:
                HandleIceExplosion(explosion);
                break;
            case BombType.Fire:
                HandleFireExplosion(bomb, explosion);
                break;
            case BombType.Sticky:
                HandleStickyExplosion(explosion);
                break;
            case BombType.Smoke:
                HandleSmokeExplosion(explosion);
                break;
            case BombType.Lightning:
                HandleLightningExplosion(bomb);
                break;
            case BombType.Gravity:
                HandleGravityExplosion(bomb, explosion);
                break;
            case BombType.Poison:
                HandlePoisonExplosion(explosion);
                break;
            case BombType.TimeWarp:
                HandleTimeWarpExplosion(explosion);
                break;
            case BombType.Mirror:
                HandleMirrorExplosion(bomb, explosion);
                break;
            case BombType.Vortex:
                HandleVortexExplosion(bomb);
                break;
            case BombType.Phantom:
                HandlePhantomExplosion(bomb, explosion);
                break;
            case BombType.Nova:
                HandleNovaExplosion(bomb);
                break;
            case BombType.BlackHole:
                HandleBlackHoleExplosion(bomb, explosion);
                break;
        }
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

            // Kettenreaktion mit anderen Bomben (Tiefe propagieren)
            if (gridCell.Bomb != null && !gridCell.Bomb.HasExploded)
            {
                gridCell.Bomb.ChainDepth = (explosion.SourceBomb?.ChainDepth ?? 0) + 1;
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

                    // Gegner-AI: Pfad-Cache invalidieren → sofortige Neuberechnung
                    // Neue Wege sind jetzt möglich, alle Timer auf 0 setzen
                    InvalidateEnemyPaths();

                    // Block-Zerstörungs-Partikel
                    float bpx = cell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                    float bpy = cell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                    _particleSystem.Emit(bpx, bpy, 5, ParticleColors.BlockDestroy, 50f, 0.4f);
                    _particleSystem.Emit(bpx, bpy, 3, ParticleColors.BlockDestroyLight, 30f, 0.3f);

                    // Exit aufdecken wenn unter diesem Block versteckt (klassisches Bomberman)
                    if (cell.HasHiddenExit)
                    {
                        cell.HasHiddenExit = false;
                        cell.Type = CellType.Exit;
                        _exitRevealed = true;
                        _exitCell = cell;
                        _soundManager.PlaySound(SoundManager.SFX_EXIT_APPEAR);

                        // Exit-Reveal Partikel (grün)
                        _particleSystem.Emit(bpx, bpy, 12, ParticleColors.ExitReveal, 60f, 0.8f);
                        _particleSystem.Emit(bpx, bpy, 6, ParticleColors.ExitRevealLight, 40f, 0.5f);
                    }
                    // PowerUp anzeigen wenn versteckt (mit Pop-Out Animation)
                    else if (cell.HiddenPowerUp.HasValue)
                    {
                        var powerUp = PowerUp.CreateAtGrid(cell.X, cell.Y, cell.HiddenPowerUp.Value);
                        powerUp.BirthTimer = Models.Entities.PowerUp.BIRTH_DURATION;
                        _powerUps.Add(powerUp);
                        cell.PowerUp = powerUp;
                        cell.HiddenPowerUp = null;

                        // Gold-Partikel-Burst bei PowerUp-Erscheinung
                        _particleSystem.Emit(bpx, bpy, 8, new SKColor(255, 215, 0), 50f, 0.4f);
                        _particleSystem.Emit(bpx, bpy, 4, new SKColor(255, 255, 200), 30f, 0.3f);

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

        // Nachglüh-Timer der Zellen aktualisieren
        UpdateAfterglow(deltaTime);
    }

    private void UpdateAfterglow(float deltaTime)
    {
        for (int y = 0; y < _grid.Height; y++)
        {
            for (int x = 0; x < _grid.Width; x++)
            {
                var cell = _grid[x, y];
                if (cell.AfterglowTimer > 0)
                {
                    cell.AfterglowTimer -= deltaTime;
                    if (cell.AfterglowTimer < 0)
                        cell.AfterglowTimer = 0;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPEZIAL-BOMBEN-EFFEKTE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Eis-Explosion: Betroffene Zellen einfrieren (verlangsamt Gegner für 3s)
    /// </summary>
    private void HandleIceExplosion(Explosion explosion)
    {
        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = _grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null) continue;

            // Zelle einfrieren
            gridCell.IsFrozen = true;
            gridCell.FreezeTimer = 3.0f;

            // Blaue Frost-Partikel
            float px = cell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float py = cell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            _particleSystem.EmitExplosionSparks(px, py, 8, new SKColor(100, 200, 255), 100f);
        }

        // Floating Text über dem Explosions-Zentrum
        float centerX = explosion.X;
        float centerY = explosion.Y;
        string frozenText = _localizationService.GetString("FrozenEffect") ?? "EINFRIEREN!";
        _floatingText.Spawn(centerX, centerY - 16, frozenText, new SKColor(100, 200, 255), 16f, 1.5f);
    }

    /// <summary>
    /// Feuer-Explosion: Lava-Nachwirkung auf betroffenen Zellen (3s Schaden bei Betreten)
    /// </summary>
    private void HandleFireExplosion(Bomb bomb, Explosion explosion)
    {
        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = _grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null) continue;

            // Lava-Nachwirkung aktivieren
            gridCell.IsLavaActive = true;
            gridCell.LavaTimer = 3.0f;

            // Rote/orange Glut-Partikel
            float px = cell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float py = cell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            _particleSystem.EmitEmbers(px, py, 10, new SKColor(255, 100, 0));
        }

        // Floating Text
        float centerX = explosion.X;
        float centerY = explosion.Y;
        _floatingText.Spawn(centerX, centerY - 16, "LAVA!", new SKColor(255, 100, 0), 16f, 1.5f);
    }

    /// <summary>
    /// Klebe-Explosion: Kettenreaktionen + Verlangsamung (Klebe-Effekt auf betroffenen Zellen)
    /// </summary>
    private void HandleStickyExplosion(Explosion explosion)
    {
        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = _grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null) continue;

            // Alle Bomben in betroffenen Zellen sofort zur Kettenreaktion auslösen
            if (gridCell.Bomb != null && !gridCell.Bomb.HasExploded)
            {
                gridCell.Bomb.ChainDepth = (explosion.SourceBomb?.ChainDepth ?? 0) + 1;
                gridCell.Bomb.TriggerChainReaction();
            }

            // Klebe-Verlangsamung: Zelle einfrieren mit kürzerem Timer (1.5s)
            gridCell.IsFrozen = true;
            gridCell.FreezeTimer = 1.5f;

            // Grüne Partikel
            float px = cell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float py = cell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            _particleSystem.EmitShaped(px, py, 8, new SKColor(50, 205, 50),
                ParticleShape.Circle, 60f, 0.6f, 3f);
        }

        // Floating Text
        float centerX = explosion.X;
        float centerY = explosion.Y;
        string stuckText = _localizationService.GetString("StickyEffect") ?? "KLEBEN!";
        _floatingText.Spawn(centerX, centerY - 16, stuckText, new SKColor(50, 205, 50), 16f, 1.5f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NEUE BOMBEN-EFFEKTE (Phase 1)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rauch-Explosion: 3x3 Nebelwolke, Gegner-AI läuft 4s zufällig
    /// </summary>
    private void HandleSmokeExplosion(Explosion explosion)
    {
        int centerX = explosion.SourceBomb?.GridX ?? 0;
        int centerY = explosion.SourceBomb?.GridY ?? 0;

        // 3x3 Rauchwolke um Zentrum
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                var gridCell = _grid.TryGetCell(centerX + dx, centerY + dy);
                if (gridCell == null || gridCell.Type == CellType.Wall) continue;

                gridCell.IsSmokeCloud = true;
                gridCell.SmokeTimer = 4.0f;

                // Graue Rauchpartikel
                float px = gridCell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                float py = gridCell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                _particleSystem.EmitShaped(px, py, 6, new SKColor(160, 160, 160),
                    ParticleShape.Circle, 40f, 0.8f, 4f);
            }
        }

        float cx = centerX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        float cy = centerY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        string smokeText = _localizationService.GetString("SmokeEffect") ?? "RAUCH!";
        _floatingText.Spawn(cx, cy - 16, smokeText, new SKColor(160, 160, 160), 16f, 1.5f);
    }

    /// <summary>
    /// Blitz-Explosion: Blitz springt zu 3 nächsten Gegnern, ignoriert Wände
    /// </summary>
    private void HandleLightningExplosion(Bomb bomb)
    {
        float bx = bomb.X;
        float by = bomb.Y;

        // Die 3 nächsten aktiven Gegner finden
        var targets = new List<Enemy>();
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || enemy.IsDying) continue;
            targets.Add(enemy);
        }

        // Nach Distanz sortieren
        targets.Sort((a, b) =>
        {
            float distA = MathF.Abs(a.X - bx) + MathF.Abs(a.Y - by);
            float distB = MathF.Abs(b.X - bx) + MathF.Abs(b.Y - by);
            return distA.CompareTo(distB);
        });

        int hits = Math.Min(3, targets.Count);
        for (int i = 0; i < hits; i++)
        {
            var target = targets[i];

            // Blitz-Partikel von Bombe zum Gegner
            _particleSystem.EmitExplosionSparks(target.X, target.Y, 10,
                new SKColor(255, 255, 100), 120f);

            // Gegner direkt töten (normaler Todes-Prozess via KillEnemy)
            if (target.TakeDamage())
            {
                KillEnemy(target);
            }

            // Blitz-Verbindungslinie als Partikel-Spur
            float startX = (i == 0) ? bx : targets[i - 1].X;
            float startY = (i == 0) ? by : targets[i - 1].Y;
            int sparkSteps = 5;
            for (int s = 0; s < sparkSteps; s++)
            {
                float t = s / (float)sparkSteps;
                float sx = startX + (target.X - startX) * t;
                float sy = startY + (target.Y - startY) * t;
                _particleSystem.EmitShaped(sx, sy, 2, new SKColor(200, 200, 255),
                    ParticleShape.Spark, 30f, 0.3f, 1.5f, hasGlow: true);
            }
        }

        string lightningText = _localizationService.GetString("LightningEffect") ?? "BLITZ!";
        _floatingText.Spawn(bx, by - 16, lightningText, new SKColor(255, 255, 100), 16f, 1.5f);
    }

    /// <summary>
    /// Gravitations-Explosion: Zieht alle Gegner im 3-Zellen-Radius 1 Zelle zum Zentrum
    /// </summary>
    private void HandleGravityExplosion(Bomb bomb, Explosion explosion)
    {
        int centerX = bomb.GridX;
        int centerY = bomb.GridY;
        float bx = bomb.X;
        float by = bomb.Y;

        // Gravity-Well auf betroffenen Zellen setzen
        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = _grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null) continue;

            gridCell.IsGravityWell = true;
            gridCell.GravityTimer = 2.0f;
        }

        // Gegner im 3-Zellen-Radius 1 Zelle zum Zentrum ziehen
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || enemy.IsDying) continue;

            int distX = Math.Abs(enemy.GridX - centerX);
            int distY = Math.Abs(enemy.GridY - centerY);
            if (distX > 3 || distY > 3) continue;

            // Richtung zum Zentrum berechnen
            float dx = centerX - enemy.GridX;
            float dy = centerY - enemy.GridY;

            // 1 Zelle in Richtung Zentrum verschieben (wenn Zielzelle begehbar)
            int moveX = dx != 0 ? Math.Sign(dx) : 0;
            int moveY = dy != 0 ? Math.Sign(dy) : 0;
            int targetX = enemy.GridX + moveX;
            int targetY = enemy.GridY + moveY;

            var targetCell = _grid.TryGetCell(targetX, targetY);
            if (targetCell != null && targetCell.IsWalkable())
            {
                enemy.X = targetX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                enemy.Y = targetY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            }
        }

        // Violette Gravitations-Partikel (spiralförmig zum Zentrum)
        _particleSystem.EmitShaped(bx, by, 15, new SKColor(180, 100, 255),
            ParticleShape.Circle, 80f, 0.6f, 3f, hasGlow: true);

        string gravityText = _localizationService.GetString("GravityEffect") ?? "GRAVITATION!";
        _floatingText.Spawn(bx, by - 16, gravityText, new SKColor(180, 100, 255), 16f, 1.5f);
    }

    /// <summary>
    /// Gift-Explosion: Gift-Zellen (3s), Gegner verlieren HP beim Betreten
    /// </summary>
    private void HandlePoisonExplosion(Explosion explosion)
    {
        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = _grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null) continue;

            gridCell.IsPoisoned = true;
            gridCell.PoisonTimer = 3.0f;

            // Grüne Gift-Partikel
            float px = cell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float py = cell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            _particleSystem.EmitShaped(px, py, 5, new SKColor(0, 200, 0),
                ParticleShape.Circle, 30f, 0.5f, 3.5f);
        }

        float cx = explosion.X;
        float cy = explosion.Y;
        string poisonText = _localizationService.GetString("PoisonEffect") ?? "GIFT!";
        _floatingText.Spawn(cx, cy - 16, poisonText, new SKColor(0, 200, 0), 16f, 1.5f);
    }

    /// <summary>
    /// Zeitverzerrung: Alles im Radius 5s auf 50% verlangsamt inkl. Bomben-Timer
    /// </summary>
    private void HandleTimeWarpExplosion(Explosion explosion)
    {
        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = _grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null) continue;

            gridCell.IsTimeWarped = true;
            gridCell.TimeWarpTimer = 5.0f;
        }

        // Blaue/violette Zeitverzerrungspartikel
        float cx = explosion.X;
        float cy = explosion.Y;
        _particleSystem.EmitShaped(cx, cy, 20, new SKColor(100, 150, 255),
            ParticleShape.Circle, 60f, 1.0f, 4f, hasGlow: true);
        _particleSystem.EmitExplosionSparks(cx, cy, 12, new SKColor(200, 200, 255), 80f);

        string timeText = _localizationService.GetString("TimeWarpEffect") ?? "ZEITSTOP!";
        _floatingText.Spawn(cx, cy - 16, timeText, new SKColor(100, 150, 255), 16f, 1.5f);
    }

    /// <summary>
    /// Spiegel-Explosion: Explosion kopiert sich in Gegenrichtung (doppelte Reichweite effektiv)
    /// </summary>
    private void HandleMirrorExplosion(Bomb bomb, Explosion explosion)
    {
        // Zusätzliche Explosion mit doppeltem Range erzeugen
        int mirrorRange = bomb.Range * 2;
        var mirrorExplosion = new Explosion(bomb);
        mirrorExplosion.CalculateSpread(_grid, mirrorRange);
        _explosions.Add(mirrorExplosion);
        ProcessExplosion(mirrorExplosion);

        // Silberne Spiegel-Partikel
        float cx = bomb.X;
        float cy = bomb.Y;
        _particleSystem.EmitExplosionSparks(cx, cy, 16, new SKColor(220, 220, 240), 140f);
        _particleSystem.EmitShaped(cx, cy, 8, new SKColor(200, 200, 255),
            ParticleShape.Circle, 100f, 0.4f, 2f, hasGlow: true);

        string mirrorText = _localizationService.GetString("MirrorEffect") ?? "SPIEGEL!";
        _floatingText.Spawn(cx, cy - 16, mirrorText, new SKColor(220, 220, 240), 16f, 1.5f);
    }

    /// <summary>
    /// Wirbel-Explosion: Spiralförmige Explosion, trifft mehr Zellen als lineare Ausbreitung
    /// </summary>
    private void HandleVortexExplosion(Bomb bomb)
    {
        int cx = bomb.GridX;
        int cy = bomb.GridY;
        int range = bomb.Range;

        // Spiralförmig alle Zellen im Radius markieren (mehr als 4-Richtungen)
        for (int r = 1; r <= range; r++)
        {
            // 8 Richtungen (inkl. Diagonalen) pro Ring
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) > range + 1) continue;
                    if (dx == 0 && dy == 0) continue;

                    var gridCell = _grid.TryGetCell(cx + dx, cy + dy);
                    if (gridCell == null || gridCell.Type == CellType.Wall) continue;

                    // Block zerstören
                    if (gridCell.Type == CellType.Block && !gridCell.IsDestroying)
                    {
                        DestroyBlock(gridCell);
                        continue; // Blöcke stoppen diesen Pfad
                    }

                    // Zelle als explodierend markieren
                    gridCell.IsExploding = true;
                    gridCell.ExplosionProgress = 0f;
                    gridCell.AfterglowTimer = 0.4f;

                    // Kettenreaktion
                    if (gridCell.Bomb != null && !gridCell.Bomb.HasExploded)
                    {
                        gridCell.Bomb.ChainDepth = (bomb.ChainDepth) + 1;
                        gridCell.Bomb.TriggerChainReaction();
                    }
                }
            }
        }

        // Violette Wirbelpartikel
        float px = bomb.X;
        float py = bomb.Y;
        _particleSystem.EmitShaped(px, py, 24, new SKColor(148, 0, 211),
            ParticleShape.Circle, 100f, 0.6f, 3f, hasGlow: true);
        _particleSystem.EmitExplosionSparks(px, py, 16, new SKColor(200, 100, 255), 120f);

        string vortexText = _localizationService.GetString("VortexEffect") ?? "WIRBEL!";
        _floatingText.Spawn(px, py - 16, vortexText, new SKColor(148, 0, 211), 16f, 1.5f);
    }

    /// <summary>
    /// Phantom-Explosion: Explosion durchdringt 1 unzerstörbare Wand
    /// </summary>
    private void HandlePhantomExplosion(Bomb bomb, Explosion explosion)
    {
        // Zusätzliche Explosion erzeugen die 1 Wand durchdringt
        // Für jede Richtung: Wenn Wand gefunden, dahinter weiter explodieren
        int cx = bomb.GridX;
        int cy = bomb.GridY;
        int range = bomb.Range;

        var deltas = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        foreach (var (dx, dy) in deltas)
        {
            bool wallPassed = false;
            for (int i = 1; i <= range + 1; i++)
            {
                int gx = cx + dx * i;
                int gy = cy + dy * i;
                var cell = _grid.TryGetCell(gx, gy);
                if (cell == null) break;

                if (cell.Type == CellType.Wall && !wallPassed)
                {
                    wallPassed = true;
                    // Geister-Partikel an der Wand
                    float wx = gx * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                    float wy = gy * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                    _particleSystem.EmitShaped(wx, wy, 6, new SKColor(200, 200, 255, 128),
                        ParticleShape.Circle, 40f, 0.4f, 2f, hasGlow: true);
                    continue;
                }

                if (wallPassed && cell.Type == CellType.Wall) break; // Nur 1 Wand durchdringen

                if (wallPassed)
                {
                    // Explosion hinter der Wand
                    if (cell.Type == CellType.Block && !cell.IsDestroying)
                    {
                        DestroyBlock(cell);
                        break;
                    }

                    cell.IsExploding = true;
                    cell.ExplosionProgress = 0f;
                    cell.AfterglowTimer = 0.4f;

                    if (cell.Bomb != null && !cell.Bomb.HasExploded)
                    {
                        cell.Bomb.ChainDepth = bomb.ChainDepth + 1;
                        cell.Bomb.TriggerChainReaction();
                    }
                }
            }
        }

        // Geister-Partikel (halbtransparent weiß-blau)
        _particleSystem.EmitShaped(bomb.X, bomb.Y, 12, new SKColor(200, 220, 255, 180),
            ParticleShape.Circle, 80f, 0.5f, 2.5f, hasGlow: true);

        string phantomText = _localizationService.GetString("PhantomEffect") ?? "PHANTOM!";
        _floatingText.Spawn(bomb.X, bomb.Y - 16, phantomText, new SKColor(200, 220, 255), 16f, 1.5f);
    }

    /// <summary>
    /// Nova-Explosion: 360-Grad Explosion (ALLE Zellen im Range), lässt PowerUp fallen
    /// </summary>
    private void HandleNovaExplosion(Bomb bomb)
    {
        int cx = bomb.GridX;
        int cy = bomb.GridY;
        int range = bomb.Range;

        // ALLE Zellen im quadratischen Bereich explodieren
        for (int dy = -range; dy <= range; dy++)
        {
            for (int dx = -range; dx <= range; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                var cell = _grid.TryGetCell(cx + dx, cy + dy);
                if (cell == null || cell.Type == CellType.Wall) continue;

                if (cell.Type == CellType.Block && !cell.IsDestroying)
                {
                    DestroyBlock(cell);
                    continue;
                }

                cell.IsExploding = true;
                cell.ExplosionProgress = 0f;
                cell.AfterglowTimer = 0.4f;

                if (cell.Bomb != null && !cell.Bomb.HasExploded)
                {
                    cell.Bomb.ChainDepth = bomb.ChainDepth + 1;
                    cell.Bomb.TriggerChainReaction();
                }
            }
        }

        // Zufälliges PowerUp an der Explosionsstelle droppen
        var centerCell = _grid.TryGetCell(cx, cy);
        if (centerCell != null && centerCell.PowerUp == null)
        {
            var rng = new Random();
            var types = new[] { PowerUpType.BombUp, PowerUpType.Fire, PowerUpType.Speed,
                               PowerUpType.Kick, PowerUpType.Detonator, PowerUpType.Bombpass };
            var randomType = types[rng.Next(types.Length)];
            var powerUp = PowerUp.CreateAtGrid(cx, cy, randomType);
            powerUp.BirthTimer = Models.Entities.PowerUp.BIRTH_DURATION;
            _powerUps.Add(powerUp);
            centerCell.PowerUp = powerUp;
        }

        // Goldene Nova-Partikel-Explosion
        _particleSystem.EmitShaped(bomb.X, bomb.Y, 30, new SKColor(255, 215, 0),
            ParticleShape.Circle, 120f, 0.6f, 3f, hasGlow: true);
        _particleSystem.EmitExplosionSparks(bomb.X, bomb.Y, 20, new SKColor(255, 255, 200), 160f);
        _particleSystem.EmitEmbers(bomb.X, bomb.Y, 12, new SKColor(255, 200, 50));

        string novaText = _localizationService.GetString("NovaEffect") ?? "NOVA!";
        _floatingText.Spawn(bomb.X, bomb.Y - 16, novaText, new SKColor(255, 215, 0), 18f, 2f);
    }

    /// <summary>
    /// Schwarzes-Loch: Saugt Gegner 3s ein, dann Explosion. Markiert Zellen mit BlackHole-Effekt.
    /// </summary>
    private void HandleBlackHoleExplosion(Bomb bomb, Explosion explosion)
    {
        int cx = bomb.GridX;
        int cy = bomb.GridY;

        // BlackHole-Zellen im Explosionsbereich setzen
        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = _grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null) continue;

            gridCell.IsBlackHole = true;
            gridCell.BlackHoleTimer = 3.0f;
        }

        // Zentrale Zelle als Anker
        var centerCell = _grid.TryGetCell(cx, cy);
        if (centerCell != null)
        {
            centerCell.IsBlackHole = true;
            centerCell.BlackHoleTimer = 3.0f;
        }

        // Dunkle Partikel die zum Zentrum gezogen werden
        _particleSystem.EmitShaped(bomb.X, bomb.Y, 20, new SKColor(30, 0, 60),
            ParticleShape.Circle, 40f, 1.0f, 3f, hasGlow: true);
        _particleSystem.EmitExplosionSparks(bomb.X, bomb.Y, 10, new SKColor(100, 0, 200), 60f);

        string bhText = _localizationService.GetString("BlackHoleEffect") ?? "SCHWARZES LOCH!";
        _floatingText.Spawn(bomb.X, bomb.Y - 16, bhText, new SKColor(100, 0, 200), 16f, 2f);
    }

    /// <summary>
    /// Spezial-Bomben-Zellen-Effekte aktualisieren (Frost- und Lava-Timer)
    /// </summary>
    private void UpdateSpecialBombEffects(float deltaTime)
    {
        for (int y = 0; y < _grid.Height; y++)
        {
            for (int x = 0; x < _grid.Width; x++)
            {
                var cell = _grid[x, y];

                // Frost-Timer abbauen
                if (cell.IsFrozen)
                {
                    cell.FreezeTimer -= deltaTime;
                    if (cell.FreezeTimer <= 0)
                    {
                        cell.IsFrozen = false;
                        cell.FreezeTimer = 0;
                    }
                }

                // Lava-Timer abbauen (Spezial-Bomben-Lava, nicht LavaCrack-Mechanik)
                if (cell.IsLavaActive)
                {
                    cell.LavaTimer -= deltaTime;
                    if (cell.LavaTimer <= 0)
                    {
                        cell.IsLavaActive = false;
                        cell.LavaTimer = 0;
                    }
                }

                // Rauch-Timer abbauen
                if (cell.IsSmokeCloud)
                {
                    cell.SmokeTimer -= deltaTime;
                    if (cell.SmokeTimer <= 0)
                    {
                        cell.IsSmokeCloud = false;
                        cell.SmokeTimer = 0;
                    }
                }

                // Gift-Timer abbauen
                if (cell.IsPoisoned)
                {
                    cell.PoisonTimer -= deltaTime;
                    if (cell.PoisonTimer <= 0)
                    {
                        cell.IsPoisoned = false;
                        cell.PoisonTimer = 0;
                    }
                }

                // Gravitations-Timer abbauen
                if (cell.IsGravityWell)
                {
                    cell.GravityTimer -= deltaTime;
                    if (cell.GravityTimer <= 0)
                    {
                        cell.IsGravityWell = false;
                        cell.GravityTimer = 0;
                    }
                }

                // TimeWarp-Timer abbauen
                if (cell.IsTimeWarped)
                {
                    cell.TimeWarpTimer -= deltaTime;
                    if (cell.TimeWarpTimer <= 0)
                    {
                        cell.IsTimeWarped = false;
                        cell.TimeWarpTimer = 0;
                    }
                }

                // BlackHole-Timer abbauen + Gegner-Sog
                if (cell.IsBlackHole)
                {
                    cell.BlackHoleTimer -= deltaTime;
                    if (cell.BlackHoleTimer <= 0)
                    {
                        cell.IsBlackHole = false;
                        cell.BlackHoleTimer = 0;
                    }
                }
            }
        }

        // BlackHole Sog-Effekt: Gegner auf BlackHole-Zellen zum Zentrum ziehen
        UpdateBlackHolePull(deltaTime);

        // Poison Schaden: Gegner auf Gift-Zellen nehmen Schaden
        UpdatePoisonDamage(deltaTime);
    }

    /// <summary>
    /// BlackHole-Sog: Gegner auf/nahe BlackHole-Zellen langsam zum Zentrum ziehen
    /// </summary>
    private void UpdateBlackHolePull(float deltaTime)
    {
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || enemy.IsDying) continue;

            var enemyCell = _grid.TryGetCell(enemy.GridX, enemy.GridY);
            if (enemyCell == null || !enemyCell.IsBlackHole) continue;

            // Nächstes BlackHole-Zentrum finden (Zelle mit höchstem Timer = frischestes)
            float pullStrength = 30f * deltaTime; // Pixel pro Frame

            // Einfacher Sog: Gegner werden verlangsamt (50% Speed-Reduktion passiert in UpdateEnemies)
            // Visuelle Rückmeldung: kleine dunkle Partikel
            if (_pontanRandom.Next(10) < 2)
            {
                _particleSystem.EmitShaped(enemy.X, enemy.Y, 1, new SKColor(60, 0, 100),
                    ParticleShape.Circle, 15f, 0.3f, 1f);
            }
        }
    }

    /// <summary>
    /// Gift-Schaden: Gegner auf vergifteten Zellen nehmen periodisch Schaden
    /// </summary>
    private void UpdatePoisonDamage(float deltaTime)
    {
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || enemy.IsDying) continue;

            var enemyCell = _grid.TryGetCell(enemy.GridX, enemy.GridY);
            if (enemyCell == null || !enemyCell.IsPoisoned) continue;

            // Schaden alle 1s (über PoisonTimer mod 1.0 gesteuert)
            float timerMod = enemyCell.PoisonTimer % 1.0f;
            if (timerMod > 0.95f || timerMod < deltaTime)
            {
                if (enemy.TakeDamage())
                {
                    KillEnemy(enemy);
                }
                else
                {
                    // Gift-Schadenspartikel
                    _particleSystem.EmitShaped(enemy.X, enemy.Y, 4, new SKColor(0, 200, 0),
                        ParticleShape.Circle, 20f, 0.3f, 1.5f);
                }
            }
        }
    }

}
