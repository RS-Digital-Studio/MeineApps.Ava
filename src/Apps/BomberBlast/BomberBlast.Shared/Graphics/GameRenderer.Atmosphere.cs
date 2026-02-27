using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Atmosphärische Effekte: Hintergrund, Vignette, Schatten, Stimmungsbeleuchtung, Fackeln
/// </summary>
public partial class GameRenderer
{
    // Welt-Index für atmosphärische Effekte (0-9)
    private int _currentWorldIndex;

    /// <summary>
    /// Hintergrund mit Welt-spezifischem Gradient rendern (statt canvas.Clear(farbe))
    /// </summary>
    private void RenderBackground(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        // Gecachter Shader (nur neu erstellen bei Welt-Wechsel oder Höhenänderung)
        if (_bgShader == null || _bgShaderWorldIndex != _currentWorldIndex ||
            MathF.Abs(_bgShaderHeight - screenHeight) > 1f)
        {
            _bgShader?.Dispose();
            var (topColor, bottomColor) = GetWorldGradientColors();
            _bgShader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, screenHeight),
                [topColor, bottomColor],
                SKShaderTileMode.Clamp);
            _bgShaderWorldIndex = _currentWorldIndex;
            _bgShaderHeight = screenHeight;
        }

        _fillPaint.Shader = _bgShader;
        _fillPaint.MaskFilter = null;
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _fillPaint);
        _fillPaint.Shader = null;
    }

    /// <summary>
    /// Welt-spezifische Gradient-Farben (oben → unten)
    /// </summary>
    private (SKColor top, SKColor bottom) GetWorldGradientColors()
    {
        return _currentWorldIndex switch
        {
            0 => (new SKColor(25, 45, 20), new SKColor(15, 30, 10)),       // Forest: Dunkelgrün
            1 => (new SKColor(35, 38, 48), new SKColor(22, 24, 32)),       // Industrial: Stahlgrau
            2 => (new SKColor(30, 22, 45), new SKColor(18, 12, 30)),       // Cavern: Dunkelviolett
            3 => (new SKColor(70, 120, 170), new SKColor(140, 180, 220)),  // Sky: Hellblau oben → Weißblau
            4 => (new SKColor(20, 8, 5), new SKColor(60, 15, 5)),          // Inferno: Schwarz → Dunkelrot
            5 => (new SKColor(55, 45, 30), new SKColor(35, 28, 18)),       // Ruins: Sandbraun
            6 => (new SKColor(10, 30, 55), new SKColor(5, 18, 40)),        // Ocean: Tiefblau
            7 => (new SKColor(25, 10, 5), new SKColor(50, 18, 8)),         // Volcano: Dunkelschwarz → Dunkelrot
            8 => (new SKColor(60, 55, 45), new SKColor(40, 35, 25)),       // SkyFortress: Gold-Dunkel
            9 => (new SKColor(12, 5, 20), new SKColor(25, 10, 40)),        // ShadowRealm: Fast-Schwarz → Violett
            _ => (new SKColor(40, 40, 45), new SKColor(30, 30, 35))        // Fallback
        };
    }

    /// <summary>
    /// Vignette-Effekt: Dunkler Rand um den Bildschirm
    /// </summary>
    private void RenderVignette(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        // Gecachter Shader (nur neu erstellen bei Welt-Wechsel oder Größenänderung)
        if (_vignetteShader == null || _vignetteShaderWorldIndex != _currentWorldIndex ||
            MathF.Abs(_vignetteShaderW - screenWidth) > 1f || MathF.Abs(_vignetteShaderH - screenHeight) > 1f)
        {
            _vignetteShader?.Dispose();
            byte vignetteAlpha = _currentWorldIndex switch
            {
                2 => 100,  // Cavern
                4 => 110,  // Inferno
                7 => 100,  // Volcano
                9 => 120,  // ShadowRealm
                _ => 60
            };

            float cx = screenWidth / 2f;
            float cy = screenHeight / 2f;
            float radius = MathF.Sqrt(cx * cx + cy * cy);

            _vignetteShader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy),
                radius,
                [new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, vignetteAlpha)],
                [0.5f, 1.0f],
                SKShaderTileMode.Clamp);
            _vignetteShaderW = screenWidth;
            _vignetteShaderH = screenHeight;
            _vignetteShaderWorldIndex = _currentWorldIndex;
        }

        _fillPaint.Shader = _vignetteShader;
        _fillPaint.MaskFilter = null;
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _fillPaint);
        _fillPaint.Shader = null;
    }

    /// <summary>
    /// Stimmungs-Beleuchtung: Subtiler Farb-Overlay pro Welt als Post-Processing
    /// </summary>
    private void RenderMoodLighting(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        var (moodColor, alpha) = _currentWorldIndex switch
        {
            0 => (new SKColor(255, 220, 150), (byte)15),  // Forest: Warm-Gold
            2 => (new SKColor(100, 140, 220), (byte)12),  // Cavern: Kühl-Blau
            4 => (new SKColor(255, 80, 20), (byte)18),    // Inferno: Rot-Orange
            6 => (new SKColor(50, 120, 180), (byte)15),   // Ocean: Blau-Grün
            9 => (new SKColor(120, 40, 180), (byte)20),   // ShadowRealm: Violett
            _ => (SKColors.Transparent, (byte)0)
        };

        if (alpha == 0) return;

        _fillPaint.Color = moodColor.WithAlpha(alpha);
        _fillPaint.MaskFilter = null;
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _fillPaint);
    }

    /// <summary>
    /// Animierte Hintergrund-Elemente zwischen Grid-Rand und Bildschirmrand
    /// </summary>
    private void RenderBackgroundElements(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        // Hintergrund-Elemente nur außerhalb des Spielfelds
        float gridLeft = _offsetX;
        float gridTop = _offsetY;

        switch (_currentWorldIndex)
        {
            case 0: RenderForestBackground(canvas, screenWidth, screenHeight, gridLeft, gridTop); break;
            case 1: RenderIndustrialBackground(canvas, screenWidth, screenHeight, gridLeft, gridTop); break;
            case 2: RenderCavernBackground(canvas, screenWidth, screenHeight, gridLeft, gridTop); break;
            case 3: RenderSkyBackground(canvas, screenWidth, screenHeight, gridLeft, gridTop); break;
            case 4: RenderInfernoBackground(canvas, screenWidth, screenHeight, gridLeft, gridTop); break;
            case 5: RenderRuinsBackground(canvas, screenWidth, screenHeight, gridLeft, gridTop); break;
            case 6: RenderOceanBackground(canvas, screenWidth, screenHeight, gridLeft, gridTop); break;
            case 7: RenderVolcanoBackground(canvas, screenWidth, screenHeight, gridLeft, gridTop); break;
            case 8: RenderSkyFortressBackground(canvas, screenWidth, screenHeight, gridLeft, gridTop); break;
            case 9: RenderShadowRealmBackground(canvas, screenWidth, screenHeight, gridLeft, gridTop); break;
        }
    }

    // --- Welt-spezifische Hintergrund-Renderer ---

    private void RenderForestBackground(SKCanvas canvas, float sw, float sh, float gl, float gt)
    {
        // Bäume die im Wind wippen (sin-basierte Neigung)
        _fillPaint.MaskFilter = null;
        for (int i = 0; i < 5; i++)
        {
            float x = gl * 0.15f + i * gl * 0.2f;
            if (x < 2 || x > gl - 10) continue;

            float windSway = MathF.Sin(_globalTimer * 0.8f + i * 1.5f) * 3f;
            float treeHeight = sh * (0.3f + i * 0.05f);
            float treeBottom = sh;

            // Stamm
            _fillPaint.Color = new SKColor(60, 40, 20, 120);
            canvas.DrawRect(x - 3 + windSway * 0.3f, treeBottom - treeHeight, 6, treeHeight, _fillPaint);

            // Baumkrone (3 überlappende Dreiecke)
            _fillPaint.Color = new SKColor(30, 80, 25, 100);
            for (int j = 0; j < 3; j++)
            {
                float crownY = treeBottom - treeHeight + j * treeHeight * 0.15f;
                float crownSize = 15f + j * 8f;
                using var path = new SKPath();
                path.MoveTo(x + windSway, crownY);
                path.LineTo(x - crownSize + windSway * 0.5f, crownY + crownSize * 1.2f);
                path.LineTo(x + crownSize + windSway * 0.5f, crownY + crownSize * 1.2f);
                path.Close();
                canvas.DrawPath(path, _fillPaint);
            }
        }
    }

    private void RenderIndustrialBackground(SKCanvas canvas, float sw, float sh, float gl, float gt)
    {
        // Rotierende Zahnräder
        _strokePaint.MaskFilter = null;
        for (int i = 0; i < 3; i++)
        {
            float x = gl * (0.2f + i * 0.3f);
            if (x < 5 || x > gl - 5) continue;
            float y = sh * 0.7f - i * 30f;
            float radius = 12f + i * 5f;
            float angle = _globalTimer * (0.5f + i * 0.3f) * (i % 2 == 0 ? 1 : -1);

            _strokePaint.Color = new SKColor(80, 85, 95, 80);
            _strokePaint.StrokeWidth = 2f;

            // Zahnrad-Kreis
            canvas.DrawCircle(x, y, radius, _strokePaint);

            // Zähne
            for (int t = 0; t < 8; t++)
            {
                float toothAngle = angle + t * MathF.PI / 4f;
                float tx = x + MathF.Cos(toothAngle) * (radius + 3);
                float ty = y + MathF.Sin(toothAngle) * (radius + 3);
                canvas.DrawCircle(tx, ty, 2.5f, _strokePaint);
            }
        }
    }

    private void RenderCavernBackground(SKCanvas canvas, float sw, float sh, float gl, float gt)
    {
        // Stalaktiten von oben
        _fillPaint.Color = new SKColor(50, 35, 65, 80);
        _fillPaint.MaskFilter = null;
        for (int i = 0; i < 6; i++)
        {
            float x = i * gl * 0.18f + 5;
            if (x > gl - 5) continue;
            float length = 20f + MathF.Sin(i * 2.3f) * 15f;
            using var path = new SKPath();
            path.MoveTo(x - 4, 0);
            path.LineTo(x + 4, 0);
            path.LineTo(x, length);
            path.Close();
            canvas.DrawPath(path, _fillPaint);
        }
    }

    private void RenderSkyBackground(SKCanvas canvas, float sw, float sh, float gl, float gt)
    {
        // Wolken die langsam ziehen
        _fillPaint.MaskFilter = null;
        for (int i = 0; i < 4; i++)
        {
            float baseX = ((_globalTimer * (5f + i * 2f) + i * 80f) % (sw + 100)) - 50;
            float y = 20f + i * 25f;
            byte alpha = (byte)(40 + i * 10);

            _fillPaint.Color = new SKColor(255, 255, 255, alpha);
            canvas.DrawOval(baseX, y, 25 + i * 5, 8 + i * 2, _fillPaint);
            canvas.DrawOval(baseX + 15, y - 5, 20, 10, _fillPaint);
            canvas.DrawOval(baseX - 10, y + 2, 18, 7, _fillPaint);
        }
    }

    private void RenderInfernoBackground(SKCanvas canvas, float sw, float sh, float gl, float gt)
    {
        // Flammen-Silhouetten am unteren Rand die flackern
        _fillPaint.MaskFilter = null;
        for (int i = 0; i < 8; i++)
        {
            float x = i * sw * 0.13f;
            float flicker = MathF.Sin(_globalTimer * 3f + i * 1.7f) * 8f;
            float height = 25f + flicker + MathF.Sin(i * 1.1f) * 10f;

            _fillPaint.Color = new SKColor(200, 60, 10, 40);
            using var path = new SKPath();
            path.MoveTo(x - 8, sh);
            path.LineTo(x + MathF.Sin(_globalTimer * 2f + i) * 3f, sh - height);
            path.LineTo(x + 8, sh);
            path.Close();
            canvas.DrawPath(path, _fillPaint);
        }
    }

    private void RenderRuinsBackground(SKCanvas canvas, float sw, float sh, float gl, float gt)
    {
        // Säulenstümpfe
        _fillPaint.Color = new SKColor(140, 120, 90, 60);
        _fillPaint.MaskFilter = null;
        for (int i = 0; i < 3; i++)
        {
            float x = gl * (0.15f + i * 0.35f);
            if (x > gl - 5) continue;
            float height = 40f + i * 15f;
            canvas.DrawRect(x - 5, sh - height, 10, height, _fillPaint);

            // Abgebrochene Kante oben
            _fillPaint.Color = new SKColor(160, 140, 100, 50);
            canvas.DrawRect(x - 7, sh - height, 14, 4, _fillPaint);
            _fillPaint.Color = new SKColor(140, 120, 90, 60);
        }
    }

    private void RenderOceanBackground(SKCanvas canvas, float sw, float sh, float gl, float gt)
    {
        // Aufsteigende Blasen
        _strokePaint.MaskFilter = null;
        for (int i = 0; i < 6; i++)
        {
            float x = gl * (0.1f + i * 0.15f) + MathF.Sin(_globalTimer * 0.5f + i) * 5f;
            if (x > gl - 3) continue;
            float y = (sh - ((_globalTimer * 15f + i * 50f) % (sh + 20)));
            float radius = 2f + MathF.Sin(i * 1.3f) * 1.5f;

            _strokePaint.Color = new SKColor(120, 200, 240, 50);
            _strokePaint.StrokeWidth = 1f;
            canvas.DrawCircle(x, y, radius, _strokePaint);
        }
    }

    private void RenderVolcanoBackground(SKCanvas canvas, float sw, float sh, float gl, float gt)
    {
        // Asche-Wolken am oberen Rand
        _fillPaint.MaskFilter = null;
        for (int i = 0; i < 4; i++)
        {
            float x = ((_globalTimer * 3f + i * 60f) % (sw + 80)) - 40;
            float y = 10f + i * 15f;
            _fillPaint.Color = new SKColor(50, 30, 20, 30);
            canvas.DrawOval(x, y, 30, 10, _fillPaint);
        }
    }

    private void RenderSkyFortressBackground(SKCanvas canvas, float sw, float sh, float gl, float gt)
    {
        // Goldene Lichtstrahlen (diagonale helle Streifen)
        _fillPaint.MaskFilter = null;
        for (int i = 0; i < 3; i++)
        {
            float x = gl * (0.2f + i * 0.3f);
            if (x > gl - 3) continue;
            float shimmer = MathF.Sin(_globalTimer * 0.4f + i * 2f) * 0.5f + 0.5f;
            byte alpha = (byte)(15 * shimmer);
            if (alpha < 3) continue;

            _fillPaint.Color = new SKColor(255, 220, 100, alpha);
            canvas.Save();
            canvas.RotateDegrees(-15, x, 0);
            canvas.DrawRect(x - 3, 0, 6, sh, _fillPaint);
            canvas.Restore();
        }
    }

    private void RenderShadowRealmBackground(SKCanvas canvas, float sw, float sh, float gl, float gt)
    {
        // Leuchtende Augenpaare die blinzeln
        _fillPaint.MaskFilter = _smallGlow;
        for (int i = 0; i < 4; i++)
        {
            float x = gl * (0.1f + i * 0.25f);
            if (x > gl - 10) continue;
            float y = sh * (0.3f + i * 0.15f);

            // Blinken (geschlossen für kurze Perioden)
            float blinkCycle = (_globalTimer * 0.3f + i * 2.5f) % 5f;
            if (blinkCycle > 4.7f) continue; // Geschlossen

            float eyeAlpha = MathF.Sin(blinkCycle * 0.5f) * 0.5f + 0.5f;
            byte alpha = (byte)(40 * eyeAlpha);
            if (alpha < 5) continue;

            _fillPaint.Color = new SKColor(180, 60, 255, alpha);
            canvas.DrawOval(x, y, 3, 2, _fillPaint);
            canvas.DrawOval(x + 8, y, 3, 2, _fillPaint);
        }
        _fillPaint.MaskFilter = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DYNAMISCHE BELEUCHTUNG - Lichtquellen sammeln
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sammelt alle Lichtquellen für den aktuellen Frame:
    /// Bomben, Explosionen, Lava/Eis-Zellen, Exit-Portal, Schild, Bosse, Fackeln, PowerUps
    /// </summary>
    private void CollectLightSources(SKCanvas canvas, GameGrid grid,
        List<Bomb> bombs, List<Explosion> explosions,
        List<Enemy> enemies, List<PowerUp> powerUps,
        Player? player, Cell? exitCell)
    {
        int cs = GameGrid.CELL_SIZE;

        // 1) Bomben: Pulsierender roter Glow
        foreach (var bomb in bombs)
        {
            if (!bomb.IsActive) continue;
            float bx = bomb.X + cs * 0.5f;
            float by = bomb.Y + cs * 0.5f;
            _dynamicLighting.AddBombLight(bx, by, bomb.FuseTimer, Bomb.DEFAULT_FUSE_TIME, cs, _globalTimer);
        }

        // 2) Explosionen: Helles warm-oranges Licht
        foreach (var explosion in explosions)
        {
            if (!explosion.IsActive || explosion.AffectedCells.Count == 0) continue;
            float progress = 1f - (explosion.Timer / Explosion.DURATION);
            float ex = explosion.X + cs * 0.5f;
            float ey = explosion.Y + cs * 0.5f;
            int range = explosion.SourceBomb?.Range ?? 2;
            _dynamicLighting.AddExplosionLight(ex, ey, range, progress, cs);
        }

        // 3) Grid-Zellen: Lava + Eis + Exit
        for (int gy = 0; gy < grid.Height; gy++)
        {
            for (int gx = 0; gx < grid.Width; gx++)
            {
                var cell = grid[gx, gy];
                float cx = gx * cs + cs * 0.5f;
                float cy = gy * cs + cs * 0.5f;

                if (cell.IsLavaActive)
                    _dynamicLighting.AddLavaLight(cx, cy, cs, _globalTimer, gx, gy);

                if (cell.IsFrozen)
                    _dynamicLighting.AddIceLight(cx, cy, cs);

                if (cell.Type == CellType.Exit)
                    _dynamicLighting.AddExitLight(cx, cy, cs, _globalTimer);
            }
        }

        // 4) Exit-Portal: Goldener Glow (auch wenn Cell via exitCell-Cache übergeben)
        if (exitCell != null && exitCell.Type == CellType.Exit)
        {
            float epx = exitCell.X * cs + cs * 0.5f;
            float epy = exitCell.Y * cs + cs * 0.5f;
            _dynamicLighting.AddExitLight(epx, epy, cs, _globalTimer);
        }

        // 5) Spieler-Schild: Cyan Glow
        if (player is { HasShield: true, IsActive: true })
            _dynamicLighting.AddShieldLight(player.X + cs * 0.5f, player.Y + cs * 0.5f, cs);

        // 6) Bosse: Enrage roter Puls
        foreach (var enemy in enemies)
        {
            if (enemy is BossEnemy { IsEnraged: true, IsActive: true } boss)
            {
                float bossX = boss.X + cs;    // Boss ist 2x2/3x3, Mitte ungefähr
                float bossY = boss.Y + cs;
                _dynamicLighting.AddBossEnrageLight(bossX, bossY, cs, _globalTimer);
            }
        }

        // 7) PowerUps: Leichter farbiger Glow
        foreach (var pu in powerUps)
        {
            if (!pu.IsActive || !pu.IsVisible) continue;
            float px = pu.X + cs * 0.5f;
            float py = pu.Y + cs * 0.5f;
            var color = GetPowerUpLightColor(pu.Type);
            _dynamicLighting.AddPowerUpLight(px, py, cs, color);
        }

        // 8) Wand-Fackeln: Warmes flackerndes Licht + Flammen-Rendering
        _dynamicLighting.AddTorchesFromGrid(grid, cs, _globalTimer, canvas, _fillPaint, _currentWorldIndex);
    }

    /// <summary>
    /// Lichtfarbe pro PowerUp-Typ
    /// </summary>
    private static SKColor GetPowerUpLightColor(PowerUpType type) => type switch
    {
        PowerUpType.BombUp => new SKColor(80, 140, 255),      // Blau
        PowerUpType.Fire => new SKColor(255, 140, 40),         // Orange
        PowerUpType.Speed => new SKColor(255, 220, 60),        // Gelb
        PowerUpType.Wallpass => new SKColor(180, 100, 255),    // Violett
        PowerUpType.Detonator => new SKColor(255, 60, 60),     // Rot
        PowerUpType.Bombpass => new SKColor(100, 200, 255),    // Hellblau
        PowerUpType.Flamepass => new SKColor(255, 180, 80),    // Warmgelb
        PowerUpType.Mystery => new SKColor(255, 255, 255),     // Weiß
        PowerUpType.Kick => new SKColor(80, 220, 80),          // Grün
        PowerUpType.LineBomb => new SKColor(0, 200, 255),      // Cyan
        PowerUpType.PowerBomb => new SKColor(255, 80, 200),    // Pink
        PowerUpType.Skull => new SKColor(160, 60, 200),        // Dunkelviolett
        _ => new SKColor(200, 200, 200)
    };
}
