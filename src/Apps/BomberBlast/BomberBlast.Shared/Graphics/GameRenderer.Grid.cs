using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Models.Levels;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Graphics;

// v2.0.37 (Plan Task 2.1): Grid.cs ist jetzt die Orchestrierungs-Datei.
// Felder + Floor-Cache + FogOverlay + RenderGrid + Tile-Transitions + Afterglow + Danger-Warning bleiben hier.
// Tile-Renderer (Floor/Wall/Welt-Mechanik) → GameRenderer.Grid.Tiles.cs.
// Block-Renderer (RenderBlockTile/Destruction) → GameRenderer.Grid.Blocks.cs.
// Special-Bomb-Cell-Effects (Eis/Lava/Smoke/Poison/Gravity/TimeWarp/BlackHole) → GameRenderer.Grid.GridFx.cs.
public sealed partial class GameRenderer
{
    // ═══════════════════════════════════════════════════════════════════════
    // BODEN-CACHE (statische Floor-Texturen als SKBitmap)
    // ═══════════════════════════════════════════════════════════════════════

    private SKBitmap? _floorCacheBitmap;
    private int _floorCacheWorldIndex = -1;
    private GameVisualStyle _floorCacheStyle = (GameVisualStyle)(-1);

    // Gepoolter SKPath für Wand/Block-Tile-Details (Kristalle, Ecken, Edelsteine)
    // Vermeidet ~50-100 native SKPath-Allokationen pro Frame
    private readonly SKPath _tilePath = new();

    // Block-Destroy-Animation Phase-2: Konstante Multiplikatoren fuer die 4 Fragmente
    // (oben-links, oben-rechts, unten-links, unten-rechts). Werden mit spread/p2
    // zur Laufzeit multipliziert, statt pro Frame neue float[]-Arrays zu allozieren.
    private static readonly float[] BlockFragSpreadMulX = { -1f, 1f, -0.8f, 0.8f };
    private static readonly float[] BlockFragSpreadMulY = { -1f, -0.7f, 0.6f, 1f };
    private static readonly float[] BlockFragRotMul = { -25f, 20f, 15f, -30f };

    /// <summary>
    /// Boden-Cache invalidieren (bei Welt- oder Style-Wechsel).
    /// Wird automatisch in SetWorldTheme() aufgerufen.
    /// </summary>
    private void InvalidateFloorCache()
    {
        _floorCacheWorldIndex = -1;
    }

    /// <summary>
    /// Alle Floor-Tiles einmalig in ein SKBitmap rendern (Schachbrett + Welt-Details + Grid-Linien).
    /// Spart ~750 Draw-Calls pro Frame → 1 DrawBitmap.
    /// </summary>
    private void RebuildFloorCache(GameGrid grid, bool isNeon)
    {
        int w = grid.PixelWidth;
        int h = grid.PixelHeight;

        // Altes Bitmap freigeben falls vorhanden
        if (_floorCacheBitmap != null && (_floorCacheBitmap.Width != w || _floorCacheBitmap.Height != h))
        {
            _floorCacheBitmap.Dispose();
            _floorCacheBitmap = null;
        }

        _floorCacheBitmap ??= new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);

        using var cacheCanvas = new SKCanvas(_floorCacheBitmap);
        cacheCanvas.Clear(SKColors.Transparent);

        int cs = GameGrid.CELL_SIZE;
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                float px = x * cs;
                float py = y * cs;
                RenderFloorTile(cacheCanvas, px, py, cs, x, y, isNeon);
            }
        }

        _floorCacheWorldIndex = _currentWorldIndex;
        _floorCacheStyle = _styleService.CurrentStyle;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FOG OVERLAY (Welt 10: Schattenwelt)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Nebel: Schwarzes Overlay mit kreisförmigem Sichtbereich um den Spieler (5 Zellen Radius)
    /// </summary>
    private void RenderFogOverlay(SKCanvas canvas, GameGrid grid, float playerX, float playerY)
    {
        float fogRadius = GameGrid.CELL_SIZE * 5.5f; // 5 Zellen + halbe Zelle Puffer

        // Sichtbereich als Kreis ausschneiden
        canvas.Save();

        // Gesamte Spielfläche schwarz (alles AUSSER dem Kreis um den Spieler)
        _fillPaint.Color = new SKColor(0, 0, 0, 200);
        _fillPaint.MaskFilter = null;

        _bgPath.Rewind();
        _bgPath.AddRect(new SKRect(0, 0, grid.PixelWidth, grid.PixelHeight));
        _bgPath.AddCircle(playerX, playerY, fogRadius);
        _bgPath.FillType = SKPathFillType.EvenOdd;

        canvas.ClipPath(_bgPath);
        canvas.DrawRect(0, 0, grid.PixelWidth, grid.PixelHeight, _fillPaint);
        canvas.Restore();

        // FillType nach EvenOdd-Nutzung auf Default zuruecksetzen, damit
        // Atmosphere-Rendering im naechsten Frame mit Winding-FillType arbeitet.
        // Rewind() behaelt FillType bei, deshalb explizit reset noetig.
        _bgPath.FillType = SKPathFillType.Winding;

        // Weicher Rand am Sichtkreis: 2-Ring-Approximation statt RadialGradient + SKPaint-Allokation
        _fillPaint.Shader = null;
        _fillPaint.MaskFilter = null;
        _fillPaint.Color = new SKColor(0, 0, 0, 40);
        _fillPaint.Style = SKPaintStyle.Stroke;
        _fillPaint.StrokeWidth = fogRadius * 0.15f;
        canvas.DrawCircle(playerX, playerY, fogRadius * 0.92f, _fillPaint);
        _fillPaint.Color = new SKColor(0, 0, 0, 80);
        _fillPaint.StrokeWidth = fogRadius * 0.1f;
        canvas.DrawCircle(playerX, playerY, fogRadius * 0.98f, _fillPaint);
        _fillPaint.Style = SKPaintStyle.Fill;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GRID RENDERING
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderGrid(SKCanvas canvas, GameGrid grid)
    {
        int cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        // Boden-Cache: Alle Floor-Tiles einmalig als Bitmap rendern
        if (_floorCacheWorldIndex != _currentWorldIndex || _floorCacheStyle != _styleService.CurrentStyle)
            RebuildFloorCache(grid, isNeon);

        // Gecachten Boden als einzelnes Bitmap zeichnen (statt ~750 Draw-Calls)
        if (_floorCacheBitmap != null)
            canvas.DrawBitmap(_floorCacheBitmap, 0, 0);

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                var cell = grid[x, y];
                float px = x * cs;
                float py = y * cs;

                switch (cell.Type)
                {
                    case CellType.Wall:
                        RenderWallTile(canvas, px, py, cs, x, y, isNeon);
                        break;

                    case CellType.Block:
                        if (cell.IsDestroying)
                        {
                            RenderBlockDestruction(canvas, px, py, cs, cell.DestructionProgress, isNeon);
                        }
                        else if (ActiveMutator == LevelMutator.InvisibleBlocks)
                        {
                            // InvisibleBlocks-Mutator: Blöcke nur sichtbar wenn Spieler direkt daneben (1 Zelle)
                            int dx = Math.Abs(x - PlayerGridX);
                            int dy = Math.Abs(y - PlayerGridY);
                            if (dx + dy <= 1)
                                RenderBlockTile(canvas, px, py, cs, x, y, isNeon);
                            // Sonst unsichtbar (normaler Boden wird darunter gerendert)
                        }
                        else
                        {
                            RenderBlockTile(canvas, px, py, cs, x, y, isNeon);
                        }
                        break;

                    case CellType.Ice:
                        RenderIceTile(canvas, px, py, cs, x, y, isNeon);
                        break;

                    case CellType.Conveyor:
                        RenderConveyorTile(canvas, px, py, cs, cell, isNeon);
                        break;

                    case CellType.Teleporter:
                        RenderTeleporterTile(canvas, px, py, cs, cell, isNeon);
                        break;

                    case CellType.LavaCrack:
                        RenderLavaCrackTile(canvas, px, py, cs, cell, isNeon);
                        break;

                    case CellType.PlatformGap:
                        RenderPlatformGapTile(canvas, px, py, cs, isNeon);
                        break;
                }

                // Übergangs-Blending: Weiche Übergänge zwischen Mechanik-Zellen und normalem Boden
                if (cell.Type is CellType.Ice or CellType.LavaCrack or CellType.Conveyor or CellType.Teleporter)
                    RenderTileTransition(canvas, grid, px, py, cs, x, y, cell.Type);
            }
        }
    }

    /// <summary>
    /// Weiche Übergänge zwischen Mechanik-Zellen und normalem Boden.
    /// Zeichnet typ-spezifische Rand-Effekte an Grenzen zu normalen Zellen.
    /// </summary>
    private void RenderTileTransition(SKCanvas canvas, GameGrid grid,
        float px, float py, int cs, int gx, int gy, CellType cellType)
    {
        // Halbe Übergangsbreite
        float tw = cs * 0.2f;

        // 4 Nachbarn prüfen (oben, unten, links, rechts)
        bool topNormal = gy > 0 && IsNormalFloor(grid[gx, gy - 1].Type);
        bool botNormal = gy < grid.Height - 1 && IsNormalFloor(grid[gx, gy + 1].Type);
        bool leftNormal = gx > 0 && IsNormalFloor(grid[gx - 1, gy].Type);
        bool rightNormal = gx < grid.Width - 1 && IsNormalFloor(grid[gx + 1, gy].Type);

        if (!topNormal && !botNormal && !leftNormal && !rightNormal) return;

        _fillPaint.MaskFilter = null;
        _fillPaint.Style = SKPaintStyle.Fill;

        switch (cellType)
        {
            case CellType.Ice:
                RenderIceTransitionEdges(canvas, px, py, cs, tw, topNormal, botNormal, leftNormal, rightNormal);
                break;
            case CellType.LavaCrack:
                RenderLavaTransitionEdges(canvas, px, py, cs, tw, topNormal, botNormal, leftNormal, rightNormal);
                break;
            case CellType.Conveyor:
                RenderConveyorTransitionEdges(canvas, px, py, cs, tw, topNormal, botNormal, leftNormal, rightNormal);
                break;
            case CellType.Teleporter:
                RenderTeleporterTransitionEdges(canvas, px, py, cs, tw, topNormal, botNormal, leftNormal, rightNormal);
                break;
        }
    }

    private static bool IsNormalFloor(CellType type) =>
        type is CellType.Empty or CellType.Exit;

    /// <summary>
    /// Eis-Übergang: Frost-Rand mit Kristall-Fading
    /// </summary>
    private void RenderIceTransitionEdges(SKCanvas canvas,
        float px, float py, int cs, float tw,
        bool top, bool bot, bool left, bool right)
    {
        // Frost-Fade: 2-Schritt Alpha statt pro-Zelle Shader (eliminiert native Allokationen)
        float halfTw = tw * 0.5f;
        _fillPaint.Shader = null;
        _fillPaint.MaskFilter = null;

        if (top)
        {
            _fillPaint.Color = new SKColor(180, 220, 255, 50);
            canvas.DrawRect(px, py - halfTw, cs, halfTw, _fillPaint);
            _fillPaint.Color = new SKColor(180, 220, 255, 20);
            canvas.DrawRect(px, py - tw, cs, halfTw, _fillPaint);
        }
        if (bot)
        {
            _fillPaint.Color = new SKColor(180, 220, 255, 50);
            canvas.DrawRect(px, py + cs, cs, halfTw, _fillPaint);
            _fillPaint.Color = new SKColor(180, 220, 255, 20);
            canvas.DrawRect(px, py + cs + halfTw, cs, halfTw, _fillPaint);
        }
        if (left)
        {
            _fillPaint.Color = new SKColor(180, 220, 255, 50);
            canvas.DrawRect(px - halfTw, py, halfTw, cs, _fillPaint);
            _fillPaint.Color = new SKColor(180, 220, 255, 20);
            canvas.DrawRect(px - tw, py, halfTw, cs, _fillPaint);
        }
        if (right)
        {
            _fillPaint.Color = new SKColor(180, 220, 255, 50);
            canvas.DrawRect(px + cs, py, halfTw, cs, _fillPaint);
            _fillPaint.Color = new SKColor(180, 220, 255, 20);
            canvas.DrawRect(px + cs + halfTw, py, halfTw, cs, _fillPaint);
        }

        // Eis-Kristall-Punkte am Rand
        _fillPaint.Color = new SKColor(220, 240, 255, 35);
        float half = cs / 2f;
        if (top) canvas.DrawCircle(px + half, py - tw * 0.3f, 2f, _fillPaint);
        if (bot) canvas.DrawCircle(px + half, py + cs + tw * 0.3f, 2f, _fillPaint);
        if (left) canvas.DrawCircle(px - tw * 0.3f, py + half, 2f, _fillPaint);
        if (right) canvas.DrawCircle(px + cs + tw * 0.3f, py + half, 2f, _fillPaint);
    }

    /// <summary>
    /// Lava-Übergang: Versengte Kante mit Glutrissen
    /// </summary>
    private void RenderLavaTransitionEdges(SKCanvas canvas,
        float px, float py, int cs, float tw,
        bool top, bool bot, bool left, bool right)
    {
        // Lava-Fade: 2-Schritt Alpha statt pro-Zelle Shader (eliminiert native Allokationen)
        float halfTw = tw * 0.5f;
        _fillPaint.Shader = null;
        _fillPaint.MaskFilter = null;

        if (top)
        {
            _fillPaint.Color = new SKColor(255, 120, 30, 50);
            canvas.DrawRect(px, py - halfTw, cs, halfTw, _fillPaint);
            _fillPaint.Color = new SKColor(80, 30, 10, 15);
            canvas.DrawRect(px, py - tw, cs, halfTw, _fillPaint);
        }
        if (bot)
        {
            _fillPaint.Color = new SKColor(255, 120, 30, 50);
            canvas.DrawRect(px, py + cs, cs, halfTw, _fillPaint);
            _fillPaint.Color = new SKColor(80, 30, 10, 15);
            canvas.DrawRect(px, py + cs + halfTw, cs, halfTw, _fillPaint);
        }
        if (left)
        {
            _fillPaint.Color = new SKColor(255, 120, 30, 50);
            canvas.DrawRect(px - halfTw, py, halfTw, cs, _fillPaint);
            _fillPaint.Color = new SKColor(80, 30, 10, 15);
            canvas.DrawRect(px - tw, py, halfTw, cs, _fillPaint);
        }
        if (right)
        {
            _fillPaint.Color = new SKColor(255, 120, 30, 50);
            canvas.DrawRect(px + cs, py, halfTw, cs, _fillPaint);
            _fillPaint.Color = new SKColor(80, 30, 10, 15);
            canvas.DrawRect(px + cs + halfTw, py, halfTw, cs, _fillPaint);
        }
    }

    /// <summary>
    /// Conveyor-Übergang: Metallische Einfassung
    /// </summary>
    private void RenderConveyorTransitionEdges(SKCanvas canvas,
        float px, float py, int cs, float tw,
        bool top, bool bot, bool left, bool right)
    {
        // Dünne metallische Kante (Grau → Transparent)
        float edgeW = tw * 0.4f;
        _fillPaint.Color = new SKColor(160, 165, 170, 40);

        if (top) canvas.DrawRect(px, py - edgeW, cs, edgeW, _fillPaint);
        if (bot) canvas.DrawRect(px, py + cs, cs, edgeW, _fillPaint);
        if (left) canvas.DrawRect(px - edgeW, py, edgeW, cs, _fillPaint);
        if (right) canvas.DrawRect(px + cs, py, edgeW, cs, _fillPaint);

        // Nieten-Punkte an den Ecken
        _fillPaint.Color = new SKColor(200, 200, 205, 50);
        float r = 1.5f;
        if (top && left) canvas.DrawCircle(px, py, r, _fillPaint);
        if (top && right) canvas.DrawCircle(px + cs, py, r, _fillPaint);
        if (bot && left) canvas.DrawCircle(px, py + cs, r, _fillPaint);
        if (bot && right) canvas.DrawCircle(px + cs, py + cs, r, _fillPaint);
    }

    /// <summary>
    /// Teleporter-Übergang: Energetischer Rand-Glow
    /// </summary>
    private void RenderTeleporterTransitionEdges(SKCanvas canvas,
        float px, float py, int cs, float tw,
        bool top, bool bot, bool left, bool right)
    {
        // Teleporter-Fade: 2-Schritt Alpha statt pro-Zelle Shader (eliminiert native Allokationen)
        float pulse = 0.7f + MathF.Sin(_globalTimer * 3f + px * 0.1f) * 0.3f;
        byte alphaInner = (byte)(40 * pulse);
        byte alphaOuter = (byte)(15 * pulse);
        float edgeTw = tw * 0.7f;
        float halfEdge = edgeTw * 0.5f;
        _fillPaint.Shader = null;
        _fillPaint.MaskFilter = null;

        if (top)
        {
            _fillPaint.Color = new SKColor(180, 100, 255, alphaInner);
            canvas.DrawRect(px, py - halfEdge, cs, halfEdge, _fillPaint);
            _fillPaint.Color = new SKColor(180, 100, 255, alphaOuter);
            canvas.DrawRect(px, py - edgeTw, cs, halfEdge, _fillPaint);
        }
        if (bot)
        {
            _fillPaint.Color = new SKColor(180, 100, 255, alphaInner);
            canvas.DrawRect(px, py + cs, cs, halfEdge, _fillPaint);
            _fillPaint.Color = new SKColor(180, 100, 255, alphaOuter);
            canvas.DrawRect(px, py + cs + halfEdge, cs, halfEdge, _fillPaint);
        }
        if (left)
        {
            _fillPaint.Color = new SKColor(180, 100, 255, alphaInner);
            canvas.DrawRect(px - halfEdge, py, halfEdge, cs, _fillPaint);
            _fillPaint.Color = new SKColor(180, 100, 255, alphaOuter);
            canvas.DrawRect(px - edgeTw, py, halfEdge, cs, _fillPaint);
        }
        if (right)
        {
            _fillPaint.Color = new SKColor(180, 100, 255, alphaInner);
            canvas.DrawRect(px + cs, py, halfEdge, cs, _fillPaint);
            _fillPaint.Color = new SKColor(180, 100, 255, alphaOuter);
            canvas.DrawRect(px + cs + halfEdge, py, halfEdge, cs, _fillPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AFTERGLOW + DANGER-WARNING
    // (RenderSpecialBombCellEffects → Grid.GridFx.cs)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Nachglühen auf Zellen nach Explosionsende (warmer Schimmer + Glut-Glow)</summary>
    private void RenderAfterglow(SKCanvas canvas, GameGrid grid)
    {
        int cs = GameGrid.CELL_SIZE;
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                var cell = grid[x, y];
                if (cell.AfterglowTimer <= 0)
                    continue;

                float intensity = cell.AfterglowTimer / Models.Entities.Explosion.AFTERGLOW_DURATION;

                // Basis-Glow (orange, weicher Rand)
                byte alpha = (byte)(100 * intensity);
                _fillPaint.Color = _explOuter.WithAlpha(alpha);
                _fillPaint.MaskFilter = _outerGlow;
                canvas.DrawRect(x * cs - 1, y * cs - 1, cs + 2, cs + 2, _fillPaint);
                _fillPaint.MaskFilter = null;

                // Innerer heller Kern (verblasst schneller)
                if (intensity > 0.4f)
                {
                    float coreAlpha = (intensity - 0.4f) / 0.6f;
                    _fillPaint.Color = _explInner.WithAlpha((byte)(40 * coreAlpha));
                    canvas.DrawRect(x * cs + cs * 0.2f, y * cs + cs * 0.2f, cs * 0.6f, cs * 0.6f, _fillPaint);
                }
            }
        }
    }

    /// <summary>
    /// Gefahrenzone: Subtiler roter Boden-Schimmer bei Bomben kurz vor Explosion
    /// </summary>
    private void RenderDangerWarning(SKCanvas canvas, GameGrid grid, IEnumerable<Bomb> bombs)
    {
        int cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        foreach (var bomb in bombs)
        {
            if (!bomb.IsActive || bomb.HasExploded) continue;
            // Nur warnen wenn Zünder < 0.8s (und nicht bei manueller Detonation ohne ablaufenden Timer)
            if (bomb.IsManualDetonation || bomb.FuseTimer > 0.8f) continue;

            // Intensität steigt je näher an Explosion (0 bei 0.8s → 1 bei 0s)
            float intensity = 1f - (bomb.FuseTimer / 0.8f);
            // Pulsieren (schneller bei weniger Zeit)
            float pulse = MathF.Sin(_globalTimer * (10f + intensity * 5f)) * 0.3f + 0.7f;
            byte alpha = (byte)(50 * intensity * pulse);
            if (alpha < 5) continue;

            var warningColor = isNeon ? new SKColor(255, 40, 80, alpha) : new SKColor(255, 60, 30, alpha);

            int centerX = bomb.GridX;
            int centerY = bomb.GridY;
            int range = bomb.Range;

            // Zentrum markieren
            _fillPaint.Color = warningColor;
            _fillPaint.MaskFilter = null;
            canvas.DrawRect(centerX * cs, centerY * cs, cs, cs, _fillPaint);

            // 4 Richtungen (wie Explosion.CalculateSpread, aber read-only)
            RenderDangerLine(canvas, grid, centerX, centerY, range, -1, 0, cs, warningColor);
            RenderDangerLine(canvas, grid, centerX, centerY, range, 1, 0, cs, warningColor);
            RenderDangerLine(canvas, grid, centerX, centerY, range, 0, -1, cs, warningColor);
            RenderDangerLine(canvas, grid, centerX, centerY, range, 0, 1, cs, warningColor);
        }
    }

    private void RenderDangerLine(SKCanvas canvas, GameGrid grid, int startX, int startY,
        int range, int dx, int dy, int cs, SKColor color)
    {
        for (int i = 1; i <= range; i++)
        {
            int x = startX + dx * i;
            int y = startY + dy * i;

            var cell = grid.TryGetCell(x, y);
            if (cell == null || cell.Type == CellType.Wall) break;

            _fillPaint.Color = color;
            _fillPaint.MaskFilter = null;
            canvas.DrawRect(x * cs, y * cs, cs, cs, _fillPaint);

            // Blöcke stoppen die Warnung (wie echte Explosionen)
            if (cell.Type == CellType.Block) break;
        }
    }
}
