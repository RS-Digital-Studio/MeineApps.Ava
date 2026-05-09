using BomberBlast.Graphics;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using SkiaSharp;

namespace BomberBlast.Core;

/// <summary>
/// Bomben, Explosionen und Block-Zerstörung
/// </summary>
public sealed partial class GameEngine
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

            // Tracking: Spezial-Bombe (Achievement + Missionen)
            _tracking.OnSpecialBombUsed();

            // Deck-Balancing-Telemetrie: Used++ pro Platzierung (Plays/Wins werden bei
            // CompleteLevel/GameOver aus _specialBombTypesUsedInLevel gebündelt gemeldet).
            _deckTelemetry.RecordBombPlaced(bomb.Type);
            _specialBombTypesUsedInLevel.Add(bomb.Type);

            // Wenn keine Uses mehr → automatisch auf Normal zurückschalten
            if (!activeCard.HasUsesLeft)
            {
                _player.ActiveCardSlot = -1;
            }
        }

        // Dungeon-Zündschnur-Reduktion (BombTimer-Buff + Blitzkrieg-Synergy)
        if (_dungeonBombFuseReduction > 0)
            bomb.ReduceFuse(_dungeonBombFuseReduction);

        _bombs.Add(bomb);
        cell.Bomb = bomb;
        _player.ActiveBombs++;
        _bombsUsed++;

        _soundManager.PlaySound(SoundManager.SFX_PLACE_BOMB);
        _soundManager.PlaySound(SoundManager.SFX_FUSE);

        // Haptisches Feedback bei Bomben-Platzierung (v2.0.45: differenzierter Doppel-Tick)
        _vibration.VibrateBombPlant();
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

        // Haptisches Feedback bei Power-Bomb-Platzierung (v2.0.45: starker Triple-Tap)
        _vibration.VibrateSpecialBomb();

        // Tracking: Power-Bomb (Achievement + Missionen)
        _tracking.OnPowerBombUsed();
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

            // Dungeon-Zündschnur-Reduktion (BombTimer-Buff + Blitzkrieg-Synergy)
            if (_dungeonBombFuseReduction > 0)
                bomb.ReduceFuse(_dungeonBombFuseReduction);

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

            // Haptisches Feedback bei Line-Bomb-Platzierung (v2.0.45: Special-Bomb-Pattern)
            _vibration.VibrateSpecialBomb();

            // Tracking: Line-Bomb (Achievement + Missionen)
            _tracking.OnLineBombUsed();
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

                // Direkte Variablen statt Array-Allokation pro Frame/Bombe
                float cxMin = _player.X - size;
                float cxMax = _player.X + size;
                float cyMin = _player.Y - size;
                float cyMax = _player.Y + size;
                int bGx = bomb.GridX;
                int bGy = bomb.GridY;

                bool stillOnBomb =
                    ((int)MathF.Floor(cxMin / GameGrid.CELL_SIZE) == bGx && (int)MathF.Floor(cyMin / GameGrid.CELL_SIZE) == bGy) ||
                    ((int)MathF.Floor(cxMax / GameGrid.CELL_SIZE) == bGx && (int)MathF.Floor(cyMin / GameGrid.CELL_SIZE) == bGy) ||
                    ((int)MathF.Floor(cxMin / GameGrid.CELL_SIZE) == bGx && (int)MathF.Floor(cyMax / GameGrid.CELL_SIZE) == bGy) ||
                    ((int)MathF.Floor(cxMax / GameGrid.CELL_SIZE) == bGx && (int)MathF.Floor(cyMax / GameGrid.CELL_SIZE) == bGy);

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

        // Prüfen ob ein Gegner auf der Zielzelle steht (O(1) Lookup statt N-Iteration)
        if (_enemyPositionHashSet.Contains((targetGridX, targetGridY)))
        {
            bomb.StopSlide();
            var snapCell = _grid.TryGetCell(bomb.GridX, bomb.GridY);
            if (snapCell != null) snapCell.Bomb = bomb;
            return;
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

        // Spezial-Bomben: Differenzierter Sound (Layering), Normal: Standard-Explosion
        // Stereo-Pan basierend auf Bomben-Grid-Position relativ zum Spielfeld-Mittelpunkt:
        // Bombe links = Pan -1, rechts = Pan +1 → räumliche Verortung
        float bombPan = Math.Clamp((bomb.GridX / (float)Math.Max(1, _grid.Width - 1)) * 2f - 1f, -1f, 1f);
        if (bomb.Type != BombType.Normal)
            _soundManager.PlayBombExplosion(bomb.Type);
        else
            _soundManager.PlaySoundPanned(SoundManager.SFX_EXPLOSION, bombPan);

        // Game-Feel: Trauma-basierter Shake mit Distanz-Skalierung.
        // Manhattan-Distanz zum Spieler in Grid-Zellen → Bomben weit weg shaken weniger.
        int depth = bomb.ChainDepth;
        float baseTrauma = 0.35f + depth * 0.15f;          // 0.35 → 0.95 bei depth=4
        float distanceCells = MathF.Abs(bomb.GridX - _player.GridX) + MathF.Abs(bomb.GridY - _player.GridY);
        _screenShake.TriggerAt(baseTrauma, distanceCells, falloffCells: 4f);
        _vibration.VibrateMedium();

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

        // Spezial-Bomben-Effekte — delegiert an SpecialExplosionEffects (v2.0.30+ Extract)
        var ectx = GetExplosionContext();
        switch (bomb.Type)
        {
            case BombType.Ice:
                BomberBlast.Core.Combat.SpecialExplosionEffects.HandleIce(ectx, explosion);
                break;
            case BombType.Fire:
                BomberBlast.Core.Combat.SpecialExplosionEffects.HandleFire(ectx, bomb, explosion);
                break;
            case BombType.Sticky:
                BomberBlast.Core.Combat.SpecialExplosionEffects.HandleSticky(ectx, explosion);
                break;
            case BombType.Smoke:
                BomberBlast.Core.Combat.SpecialExplosionEffects.HandleSmoke(ectx, explosion);
                break;
            case BombType.Lightning:
                BomberBlast.Core.Combat.SpecialExplosionEffects.HandleLightning(ectx, bomb);
                break;
            case BombType.Gravity:
                BomberBlast.Core.Combat.SpecialExplosionEffects.HandleGravity(ectx, bomb, explosion);
                break;
            case BombType.Poison:
                BomberBlast.Core.Combat.SpecialExplosionEffects.HandlePoison(ectx, explosion);
                break;
            case BombType.TimeWarp:
                BomberBlast.Core.Combat.SpecialExplosionEffects.HandleTimeWarp(ectx, explosion);
                break;
            case BombType.Mirror:
                BomberBlast.Core.Combat.SpecialExplosionEffects.HandleMirror(ectx, bomb, explosion);
                break;
            case BombType.Vortex:
                BomberBlast.Core.Combat.SpecialExplosionEffects.HandleVortex(ectx, bomb);
                break;
            case BombType.Phantom:
                BomberBlast.Core.Combat.SpecialExplosionEffects.HandlePhantom(ectx, bomb, explosion);
                break;
            case BombType.Nova:
                BomberBlast.Core.Combat.SpecialExplosionEffects.HandleNova(ectx, bomb);
                break;
            case BombType.BlackHole:
                BomberBlast.Core.Combat.SpecialExplosionEffects.HandleBlackHole(ectx, bomb, explosion);
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
        _destroyingCells.Add(cell);
    }

    /// <summary>
    /// Timer-basierte Block-Zerstörung (Dirty-Liste statt kompletter Grid-Iteration)
    /// </summary>
    private void UpdateDestroyingBlocks(float deltaTime)
    {
        for (int i = _destroyingCells.Count - 1; i >= 0; i--)
        {
            var cell = _destroyingCells[i];

            cell.DestructionProgress += deltaTime / BLOCK_DESTROY_DURATION;

            if (cell.DestructionProgress >= 1f)
            {
                cell.Type = CellType.Empty;
                cell.IsDestroying = false;
                cell.DestructionProgress = 0f;
                _destroyingCells.RemoveAt(i);

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

    private void UpdateExplosions(float deltaTime)
    {
        foreach (var explosion in _explosions)
        {
            explosion.Update(deltaTime);

            if (explosion.IsMarkedForRemoval)
            {
                explosion.ClearFromGrid(_grid);

                // Betroffene Zellen in Afterglow-Dirty-Liste eintragen
                foreach (var cell in explosion.AffectedCells)
                {
                    var gridCell = _grid.TryGetCell(cell.X, cell.Y);
                    if (gridCell != null && gridCell.AfterglowTimer > 0)
                        _afterglowCells.Add(gridCell);
                }
            }
        }

        // Nachglüh-Timer der Zellen aktualisieren (nur Dirty-Liste)
        UpdateAfterglow(deltaTime);
    }

    private void UpdateAfterglow(float deltaTime)
    {
        for (int i = _afterglowCells.Count - 1; i >= 0; i--)
        {
            var cell = _afterglowCells[i];
            cell.AfterglowTimer -= deltaTime;
            if (cell.AfterglowTimer <= 0)
            {
                cell.AfterglowTimer = 0;
                _afterglowCells.RemoveAt(i);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPEZIAL-BOMBEN-EFFEKTE — extrahiert in Core/Combat/SpecialExplosionEffects.cs
    // (v2.0.30+). GameEngine delegiert via GetExplosionContext() + Handle*(ctx,...).
    // Nur die 13 Handle*-Methoden wurden extrahiert; Infrastruktur (ProcessExplosion,
    // UpdateSpecialBombEffects, DestroyBlock, Chain-Reaction) bleibt in GameEngine,
    // da sie engine-interne Invarianten (Score, Events, State-Machine) mutiert.
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Spezial-Bomben-Zellen-Effekte aktualisieren (Dirty-Liste statt kompletter Grid-Iteration).
    /// Nur Zellen mit aktivem Spezial-Effekt werden geprüft und ggf. entfernt.
    /// </summary>
    private void UpdateSpecialBombEffects(float deltaTime)
    {
        for (int i = _specialEffectCells.Count - 1; i >= 0; i--)
        {
            var cell = _specialEffectCells[i];
            bool stillActive = false;

            // Frost-Timer abbauen
            if (cell.IsFrozen)
            {
                cell.FreezeTimer -= deltaTime;
                if (cell.FreezeTimer <= 0)
                {
                    cell.IsFrozen = false;
                    cell.FreezeTimer = 0;
                }
                else
                    stillActive = true;
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
                else
                    stillActive = true;
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
                else
                    stillActive = true;
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
                else
                    stillActive = true;
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
                else
                    stillActive = true;
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
                else
                    stillActive = true;
            }

            // BlackHole-Timer abbauen
            if (cell.IsBlackHole)
            {
                cell.BlackHoleTimer -= deltaTime;
                if (cell.BlackHoleTimer <= 0)
                {
                    cell.IsBlackHole = false;
                    cell.BlackHoleTimer = 0;
                }
                else
                    stillActive = true;
            }

            // Zelle aus Dirty-Liste entfernen wenn kein Effekt mehr aktiv
            if (!stillActive)
                _specialEffectCells.RemoveAt(i);
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
        // Skip wenn keine Special-Effect-Zellen aktiv (BlackHole + Poison teilen sich
        // _specialEffectCells). Spart in ~99% der Frames den Iteration ueber alle Gegner.
        if (_specialEffectCells.Count == 0) return;

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
        // Skip wenn keine Special-Effect-Zellen aktiv. Siehe UpdateBlackHolePull.
        if (_specialEffectCells.Count == 0) return;

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
