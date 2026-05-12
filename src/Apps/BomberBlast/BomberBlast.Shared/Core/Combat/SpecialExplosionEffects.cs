using BomberBlast.Graphics;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using MeineApps.Core.Ava.Localization;
using SkiaSharp;

namespace BomberBlast.Core.Combat;

/// <summary>
/// Spezial-Bomben-Effekt-Anwender (v2.0.30+ Extract aus GameEngine.Explosion.cs).
///
/// Enthaelt die 13 Handle*-Methoden fuer alle nicht-normalen Bomben-Typen
/// (Ice, Fire, Sticky, Smoke, Lightning, Gravity, Poison, TimeWarp, Mirror, Vortex,
/// Phantom, Nova, BlackHole). Jede Methode mutiert ausschliesslich Felder ueber den
/// <see cref="ExplosionEffectsContext"/>, keine direkten Engine-Zugriffe.
///
/// Aufruf-Seiteneffekte (KillEnemy/DestroyBlock/ProcessExplosion) werden ueber
/// Callbacks im Context an GameEngine zurueckgereicht, damit Engine-Invarianten
/// (Score/Events/State-Machine) erhalten bleiben.
///
/// Zustandslos (Static). Keine eigenen Felder, keine Allokationen ausser
/// denen die die Originale auch hatten.
/// </summary>
public static class SpecialExplosionEffects
{
    /// <summary>Eis-Explosion: Betroffene Zellen einfrieren (verlangsamt Gegner fuer 3s).</summary>
    public static void HandleIce(ExplosionEffectsContext ctx, Explosion explosion)
    {
        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = ctx.Grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null) continue;

            gridCell.IsFrozen = true;
            gridCell.FreezeTimer = 3.0f;
            ctx.SpecialEffectCells.Add(gridCell);

            float px = cell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float py = cell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            ctx.ParticleSystem.EmitExplosionSparks(px, py, 8, new SKColor(100, 200, 255), 100f);
        }

        float centerX = explosion.X;
        float centerY = explosion.Y;
        string frozenText = ctx.LocalizationService.GetString("FrozenEffect") ?? "FROZEN!";
        ctx.FloatingText.Spawn(centerX, centerY - 16, frozenText, new SKColor(100, 200, 255), 16f, 1.5f);
    }

    /// <summary>Feuer-Explosion: Lava-Nachwirkung auf betroffenen Zellen (3s Schaden bei Betreten).</summary>
    public static void HandleFire(ExplosionEffectsContext ctx, Bomb bomb, Explosion explosion)
    {
        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = ctx.Grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null) continue;

            gridCell.IsLavaActive = true;
            gridCell.LavaTimer = 3.0f;
            ctx.SpecialEffectCells.Add(gridCell);

            float px = cell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float py = cell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            ctx.ParticleSystem.EmitEmbers(px, py, 10, new SKColor(255, 100, 0));
        }

        float centerX = explosion.X;
        float centerY = explosion.Y;
        ctx.FloatingText.Spawn(centerX, centerY - 16,
            ctx.LocalizationService.GetString("FloatLava") ?? "LAVA!",
            new SKColor(255, 100, 0), 16f, 1.5f);
    }

    /// <summary>Klebe-Explosion: Kettenreaktionen + Verlangsamung.</summary>
    public static void HandleSticky(ExplosionEffectsContext ctx, Explosion explosion)
    {
        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = ctx.Grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null) continue;

            if (gridCell.Bomb != null && !gridCell.Bomb.HasExploded)
            {
                gridCell.Bomb.ChainDepth = (explosion.SourceBomb?.ChainDepth ?? 0) + 1;
                gridCell.Bomb.TriggerChainReaction();
            }

            gridCell.IsFrozen = true;
            gridCell.FreezeTimer = 1.5f;
            ctx.SpecialEffectCells.Add(gridCell);

            float px = cell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float py = cell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            ctx.ParticleSystem.EmitShaped(px, py, 8, new SKColor(50, 205, 50),
                ParticleShape.Circle, 60f, 0.6f, 3f);
        }

        float centerX = explosion.X;
        float centerY = explosion.Y;
        string stuckText = ctx.LocalizationService.GetString("StickyEffect") ?? "STUCK!";
        ctx.FloatingText.Spawn(centerX, centerY - 16, stuckText, new SKColor(50, 205, 50), 16f, 1.5f);
    }

    /// <summary>Rauch-Explosion: 3x3 Nebelwolke, Gegner-AI laeuft 4s zufaellig.</summary>
    public static void HandleSmoke(ExplosionEffectsContext ctx, Explosion explosion)
    {
        int centerX = explosion.SourceBomb?.GridX ?? 0;
        int centerY = explosion.SourceBomb?.GridY ?? 0;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                var gridCell = ctx.Grid.TryGetCell(centerX + dx, centerY + dy);
                if (gridCell == null || gridCell.Type == CellType.Wall) continue;

                gridCell.IsSmokeCloud = true;
                gridCell.SmokeTimer = 4.0f;
                ctx.SpecialEffectCells.Add(gridCell);

                float px = gridCell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                float py = gridCell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                ctx.ParticleSystem.EmitShaped(px, py, 6, new SKColor(160, 160, 160),
                    ParticleShape.Circle, 40f, 0.8f, 4f);
            }
        }

        float cx = centerX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        float cy = centerY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
        string smokeText = ctx.LocalizationService.GetString("SmokeEffect") ?? "SMOKE!";
        ctx.FloatingText.Spawn(cx, cy - 16, smokeText, new SKColor(160, 160, 160), 16f, 1.5f);
    }

    /// <summary>Blitz-Explosion: Blitz springt zu 3 naechsten Gegnern, ignoriert Waende.</summary>
    public static void HandleLightning(ExplosionEffectsContext ctx, Bomb bomb)
    {
        float bx = bomb.X;
        float by = bomb.Y;

        var targets = new List<Enemy>();
        foreach (var enemy in ctx.Enemies)
        {
            if (!enemy.IsActive || enemy.IsDying) continue;
            targets.Add(enemy);
        }

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

            ctx.ParticleSystem.EmitExplosionSparks(target.X, target.Y, 10,
                new SKColor(255, 255, 100), 120f);

            if (target.TakeDamage())
            {
                ctx.KillEnemy(target);
            }

            float startX = (i == 0) ? bx : targets[i - 1].X;
            float startY = (i == 0) ? by : targets[i - 1].Y;
            int sparkSteps = 5;
            for (int s = 0; s < sparkSteps; s++)
            {
                float t = s / (float)sparkSteps;
                float sx = startX + (target.X - startX) * t;
                float sy = startY + (target.Y - startY) * t;
                ctx.ParticleSystem.EmitShaped(sx, sy, 2, new SKColor(200, 200, 255),
                    ParticleShape.Spark, 30f, 0.3f, 1.5f, hasGlow: true);
            }
        }

        string lightningText = ctx.LocalizationService.GetString("LightningEffect") ?? "ZAP!";
        ctx.FloatingText.Spawn(bx, by - 16, lightningText, new SKColor(255, 255, 100), 16f, 1.5f);
    }

    /// <summary>Gravitations-Explosion: Zieht alle Gegner im 3-Zellen-Radius 1 Zelle zum Zentrum.</summary>
    public static void HandleGravity(ExplosionEffectsContext ctx, Bomb bomb, Explosion explosion)
    {
        int centerX = bomb.GridX;
        int centerY = bomb.GridY;
        float bx = bomb.X;
        float by = bomb.Y;

        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = ctx.Grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null) continue;

            gridCell.IsGravityWell = true;
            gridCell.GravityTimer = 2.0f;
            ctx.SpecialEffectCells.Add(gridCell);
        }

        foreach (var enemy in ctx.Enemies)
        {
            if (!enemy.IsActive || enemy.IsDying) continue;

            int distX = Math.Abs(enemy.GridX - centerX);
            int distY = Math.Abs(enemy.GridY - centerY);
            if (distX > 3 || distY > 3) continue;

            float dx = centerX - enemy.GridX;
            float dy = centerY - enemy.GridY;

            int moveX = dx != 0 ? Math.Sign(dx) : 0;
            int moveY = dy != 0 ? Math.Sign(dy) : 0;
            int targetX = enemy.GridX + moveX;
            int targetY = enemy.GridY + moveY;

            var targetCell = ctx.Grid.TryGetCell(targetX, targetY);
            if (targetCell != null && targetCell.IsWalkable())
            {
                enemy.X = targetX * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                enemy.Y = targetY * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            }
        }

        ctx.ParticleSystem.EmitShaped(bx, by, 15, new SKColor(180, 100, 255),
            ParticleShape.Circle, 80f, 0.6f, 3f, hasGlow: true);

        string gravityText = ctx.LocalizationService.GetString("GravityEffect") ?? "PULL!";
        ctx.FloatingText.Spawn(bx, by - 16, gravityText, new SKColor(180, 100, 255), 16f, 1.5f);
    }

    /// <summary>Gift-Explosion: Gift-Zellen (3s).</summary>
    public static void HandlePoison(ExplosionEffectsContext ctx, Explosion explosion)
    {
        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = ctx.Grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null) continue;

            gridCell.IsPoisoned = true;
            gridCell.PoisonTimer = 3.0f;
            ctx.SpecialEffectCells.Add(gridCell);

            float px = cell.X * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            float py = cell.Y * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
            ctx.ParticleSystem.EmitShaped(px, py, 5, new SKColor(0, 200, 0),
                ParticleShape.Circle, 30f, 0.5f, 3.5f);
        }

        float cx = explosion.X;
        float cy = explosion.Y;
        string poisonText = ctx.LocalizationService.GetString("PoisonEffect") ?? "POISON!";
        ctx.FloatingText.Spawn(cx, cy - 16, poisonText, new SKColor(0, 200, 0), 16f, 1.5f);
    }

    /// <summary>Zeitverzerrung: 5s auf 50% verlangsamt.</summary>
    public static void HandleTimeWarp(ExplosionEffectsContext ctx, Explosion explosion)
    {
        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = ctx.Grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null) continue;

            gridCell.IsTimeWarped = true;
            gridCell.TimeWarpTimer = 5.0f;
            ctx.SpecialEffectCells.Add(gridCell);
        }

        float cx = explosion.X;
        float cy = explosion.Y;
        ctx.ParticleSystem.EmitShaped(cx, cy, 20, new SKColor(100, 150, 255),
            ParticleShape.Circle, 60f, 1.0f, 4f, hasGlow: true);
        ctx.ParticleSystem.EmitExplosionSparks(cx, cy, 12, new SKColor(200, 200, 255), 80f);

        string timeText = ctx.LocalizationService.GetString("TimeWarpEffect") ?? "SLOW!";
        ctx.FloatingText.Spawn(cx, cy - 16, timeText, new SKColor(100, 150, 255), 16f, 1.5f);
    }

    /// <summary>Spiegel-Explosion: doppelte Reichweite via zweiter Explosion.</summary>
    public static void HandleMirror(ExplosionEffectsContext ctx, Bomb bomb, Explosion explosion)
    {
        int mirrorRange = bomb.Range * 2;
        var mirrorExplosion = new Explosion(bomb);
        mirrorExplosion.CalculateSpread(ctx.Grid, mirrorRange);
        ctx.Explosions.Add(mirrorExplosion);
        ctx.ProcessExplosion(mirrorExplosion);

        float cx = bomb.X;
        float cy = bomb.Y;
        ctx.ParticleSystem.EmitExplosionSparks(cx, cy, 16, new SKColor(220, 220, 240), 140f);
        ctx.ParticleSystem.EmitShaped(cx, cy, 8, new SKColor(200, 200, 255),
            ParticleShape.Circle, 100f, 0.4f, 2f, hasGlow: true);

        string mirrorText = ctx.LocalizationService.GetString("MirrorEffect") ?? "MIRROR!";
        ctx.FloatingText.Spawn(cx, cy - 16, mirrorText, new SKColor(220, 220, 240), 16f, 1.5f);
    }

    /// <summary>Wirbel-Explosion: Spiralfoermige Explosion mit 8 Richtungen inkl. Diagonalen.</summary>
    public static void HandleVortex(ExplosionEffectsContext ctx, Bomb bomb)
    {
        int cx = bomb.GridX;
        int cy = bomb.GridY;
        int range = bomb.Range;

        for (int r = 1; r <= range; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) > range + 1) continue;
                    if (dx == 0 && dy == 0) continue;

                    var gridCell = ctx.Grid.TryGetCell(cx + dx, cy + dy);
                    if (gridCell == null || gridCell.Type == CellType.Wall) continue;

                    if (gridCell.Type == CellType.Block && !gridCell.IsDestroying)
                    {
                        ctx.DestroyBlock(gridCell);
                        continue;
                    }

                    gridCell.IsExploding = true;
                    gridCell.ExplosionProgress = 0f;
                    gridCell.AfterglowTimer = 0.4f;
                    ctx.AfterglowCells.Add(gridCell);

                    if (gridCell.Bomb != null && !gridCell.Bomb.HasExploded)
                    {
                        gridCell.Bomb.ChainDepth = (bomb.ChainDepth) + 1;
                        gridCell.Bomb.TriggerChainReaction();
                    }
                }
            }
        }

        float px = bomb.X;
        float py = bomb.Y;
        ctx.ParticleSystem.EmitShaped(px, py, 24, new SKColor(148, 0, 211),
            ParticleShape.Circle, 100f, 0.6f, 3f, hasGlow: true);
        ctx.ParticleSystem.EmitExplosionSparks(px, py, 16, new SKColor(200, 100, 255), 120f);

        string vortexText = ctx.LocalizationService.GetString("VortexEffect") ?? "VORTEX!";
        ctx.FloatingText.Spawn(px, py - 16, vortexText, new SKColor(148, 0, 211), 16f, 1.5f);
    }

    /// <summary>Phantom-Explosion: durchdringt 1 unzerstoerbare Wand.</summary>
    public static void HandlePhantom(ExplosionEffectsContext ctx, Bomb bomb, Explosion explosion)
    {
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
                var cell = ctx.Grid.TryGetCell(gx, gy);
                if (cell == null) break;

                if (cell.Type == CellType.Wall && !wallPassed)
                {
                    wallPassed = true;
                    float wx = gx * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                    float wy = gy * GameGrid.CELL_SIZE + GameGrid.CELL_SIZE / 2f;
                    ctx.ParticleSystem.EmitShaped(wx, wy, 6, new SKColor(200, 200, 255, 128),
                        ParticleShape.Circle, 40f, 0.4f, 2f, hasGlow: true);
                    continue;
                }

                if (wallPassed && cell.Type == CellType.Wall) break;

                if (wallPassed)
                {
                    if (cell.Type == CellType.Block && !cell.IsDestroying)
                    {
                        ctx.DestroyBlock(cell);
                        break;
                    }

                    cell.IsExploding = true;
                    cell.ExplosionProgress = 0f;
                    cell.AfterglowTimer = 0.4f;
                    ctx.AfterglowCells.Add(cell);

                    if (cell.Bomb != null && !cell.Bomb.HasExploded)
                    {
                        cell.Bomb.ChainDepth = bomb.ChainDepth + 1;
                        cell.Bomb.TriggerChainReaction();
                    }
                }
            }
        }

        ctx.ParticleSystem.EmitShaped(bomb.X, bomb.Y, 12, new SKColor(200, 220, 255, 180),
            ParticleShape.Circle, 80f, 0.5f, 2.5f, hasGlow: true);

        string phantomText = ctx.LocalizationService.GetString("PhantomEffect") ?? "PHANTOM!";
        ctx.FloatingText.Spawn(bomb.X, bomb.Y - 16, phantomText, new SKColor(200, 220, 255), 16f, 1.5f);
    }

    /// <summary>Nova-Explosion: 360-Grad Explosion, laesst PowerUp fallen.</summary>
    public static void HandleNova(ExplosionEffectsContext ctx, Bomb bomb)
    {
        int cx = bomb.GridX;
        int cy = bomb.GridY;
        int range = bomb.Range;

        for (int dy = -range; dy <= range; dy++)
        {
            for (int dx = -range; dx <= range; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                var cell = ctx.Grid.TryGetCell(cx + dx, cy + dy);
                if (cell == null || cell.Type == CellType.Wall) continue;

                if (cell.Type == CellType.Block && !cell.IsDestroying)
                {
                    ctx.DestroyBlock(cell);
                    continue;
                }

                cell.IsExploding = true;
                cell.ExplosionProgress = 0f;
                cell.AfterglowTimer = 0.4f;
                ctx.AfterglowCells.Add(cell);

                if (cell.Bomb != null && !cell.Bomb.HasExploded)
                {
                    cell.Bomb.ChainDepth = bomb.ChainDepth + 1;
                    cell.Bomb.TriggerChainReaction();
                }
            }
        }

        var centerCell = ctx.Grid.TryGetCell(cx, cy);
        if (centerCell != null && centerCell.PowerUp == null)
        {
            var types = new[] { PowerUpType.BombUp, PowerUpType.Fire, PowerUpType.Speed,
                               PowerUpType.Kick, PowerUpType.Detonator, PowerUpType.Bombpass };
            var randomType = types[ctx.PontanRandom.Next(types.Length)];
            var powerUp = PowerUp.CreateAtGrid(cx, cy, randomType);
            powerUp.BirthTimer = Models.Entities.PowerUp.BIRTH_DURATION;
            ctx.PowerUps.Add(powerUp);
            centerCell.PowerUp = powerUp;
        }

        ctx.ParticleSystem.EmitShaped(bomb.X, bomb.Y, 30, BomberBlastColors.Gold,
            ParticleShape.Circle, 120f, 0.6f, 3f, hasGlow: true);
        ctx.ParticleSystem.EmitExplosionSparks(bomb.X, bomb.Y, 20, new SKColor(255, 255, 200), 160f);
        ctx.ParticleSystem.EmitEmbers(bomb.X, bomb.Y, 12, new SKColor(255, 200, 50));

        string novaText = ctx.LocalizationService.GetString("NovaEffect") ?? "NOVA!";
        ctx.FloatingText.Spawn(bomb.X, bomb.Y - 16, novaText, BomberBlastColors.Gold, 18f, 2f);
    }

    /// <summary>Schwarzes-Loch: Saugt Gegner 3s ein, dann Explosion.</summary>
    public static void HandleBlackHole(ExplosionEffectsContext ctx, Bomb bomb, Explosion explosion)
    {
        int cx = bomb.GridX;
        int cy = bomb.GridY;

        foreach (var cell in explosion.AffectedCells)
        {
            var gridCell = ctx.Grid.TryGetCell(cell.X, cell.Y);
            if (gridCell == null) continue;

            gridCell.IsBlackHole = true;
            gridCell.BlackHoleTimer = 3.0f;
            ctx.SpecialEffectCells.Add(gridCell);
        }

        var centerCell = ctx.Grid.TryGetCell(cx, cy);
        if (centerCell != null)
        {
            centerCell.IsBlackHole = true;
            centerCell.BlackHoleTimer = 3.0f;
            ctx.SpecialEffectCells.Add(centerCell);
        }

        ctx.ParticleSystem.EmitShaped(bomb.X, bomb.Y, 20, new SKColor(30, 0, 60),
            ParticleShape.Circle, 40f, 1.0f, 3f, hasGlow: true);
        ctx.ParticleSystem.EmitExplosionSparks(bomb.X, bomb.Y, 10, new SKColor(100, 0, 200), 60f);

        string bhText = ctx.LocalizationService.GetString("BlackHoleEffect") ?? "VOID!";
        ctx.FloatingText.Spawn(bomb.X, bomb.Y - 16, bhText, new SKColor(100, 0, 200), 16f, 2f);
    }
}

/// <summary>
/// Context-Record fuer <see cref="SpecialExplosionEffects"/>. Enthaelt alle Dependencies
/// + Callbacks an GameEngine (DestroyBlock/KillEnemy/ProcessExplosion). Wird einmal
/// beim GameEngine-Setup gebaut (alle Referenzen sind stabil, keine pro-Frame-Allokation).
/// </summary>
public sealed class ExplosionEffectsContext
{
    public required GameGrid Grid { get; init; }
    public required List<Cell> SpecialEffectCells { get; init; }
    public required List<Cell> AfterglowCells { get; init; }
    public required List<PowerUp> PowerUps { get; init; }
    public required List<Enemy> Enemies { get; init; }
    public required List<Explosion> Explosions { get; init; }
    public required ParticleSystem ParticleSystem { get; init; }
    public required GameFloatingTextSystem FloatingText { get; init; }
    public required ILocalizationService LocalizationService { get; init; }
    public required Random PontanRandom { get; init; }

    // Callbacks an Engine fuer Side-Effects die den Engine-State betreffen (Score/Events/State-Machine)
    public required Action<Cell> DestroyBlock { get; init; }
    public required Action<Enemy> KillEnemy { get; init; }
    public required Action<Explosion> ProcessExplosion { get; init; }
}
