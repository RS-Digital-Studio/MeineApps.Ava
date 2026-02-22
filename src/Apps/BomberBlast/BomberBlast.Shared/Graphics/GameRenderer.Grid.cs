using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Graphics;

public partial class GameRenderer
{
    // ═══════════════════════════════════════════════════════════════════════
    // BODEN-CACHE (statische Floor-Texturen als SKBitmap)
    // ═══════════════════════════════════════════════════════════════════════

    private SKBitmap? _floorCacheBitmap;
    private int _floorCacheWorldIndex = -1;
    private GameVisualStyle _floorCacheStyle = (GameVisualStyle)(-1);

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

        using var clipPath = new SKPath();
        clipPath.AddRect(new SKRect(0, 0, grid.PixelWidth, grid.PixelHeight));
        clipPath.AddCircle(playerX, playerY, fogRadius);
        clipPath.FillType = SKPathFillType.EvenOdd;

        canvas.ClipPath(clipPath);
        canvas.DrawRect(0, 0, grid.PixelWidth, grid.PixelHeight, _fillPaint);
        canvas.Restore();

        // Weicher Rand am Sichtkreis (Gradient für natürlichen Übergang)
        using var gradientPaint = new SKPaint { IsAntialias = true };
        gradientPaint.Shader = SKShader.CreateRadialGradient(
            new SKPoint(playerX, playerY),
            fogRadius,
            [new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 120)],
            [0.7f, 1.0f],
            SKShaderTileMode.Clamp);
        canvas.DrawCircle(playerX, playerY, fogRadius, gradientPaint);
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
                            RenderBlockDestruction(canvas, px, py, cs, cell.DestructionProgress, isNeon);
                        else
                            RenderBlockTile(canvas, px, py, cs, x, y, isNeon);
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
        // Frost-Gradient: Hellblau → Transparent an den Rändern zur normalen Zelle
        if (top)
        {
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(px + cs / 2f, py),
                new SKPoint(px + cs / 2f, py - tw),
                new[] { new SKColor(180, 220, 255, 50), SKColors.Transparent },
                SKShaderTileMode.Clamp);
            _fillPaint.Shader = shader;
            canvas.DrawRect(px, py - tw, cs, tw, _fillPaint);
            _fillPaint.Shader = null;
        }
        if (bot)
        {
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(px + cs / 2f, py + cs),
                new SKPoint(px + cs / 2f, py + cs + tw),
                new[] { new SKColor(180, 220, 255, 50), SKColors.Transparent },
                SKShaderTileMode.Clamp);
            _fillPaint.Shader = shader;
            canvas.DrawRect(px, py + cs, cs, tw, _fillPaint);
            _fillPaint.Shader = null;
        }
        if (left)
        {
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(px, py + cs / 2f),
                new SKPoint(px - tw, py + cs / 2f),
                new[] { new SKColor(180, 220, 255, 50), SKColors.Transparent },
                SKShaderTileMode.Clamp);
            _fillPaint.Shader = shader;
            canvas.DrawRect(px - tw, py, tw, cs, _fillPaint);
            _fillPaint.Shader = null;
        }
        if (right)
        {
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(px + cs, py + cs / 2f),
                new SKPoint(px + cs + tw, py + cs / 2f),
                new[] { new SKColor(180, 220, 255, 50), SKColors.Transparent },
                SKShaderTileMode.Clamp);
            _fillPaint.Shader = shader;
            canvas.DrawRect(px + cs, py, tw, cs, _fillPaint);
            _fillPaint.Shader = null;
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
        // Glühendes Orange → Transparent
        if (top)
        {
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(px + cs / 2f, py),
                new SKPoint(px + cs / 2f, py - tw),
                new[] { new SKColor(255, 120, 30, 60), new SKColor(80, 30, 10, 15), SKColors.Transparent },
                new[] { 0f, 0.5f, 1f }, SKShaderTileMode.Clamp);
            _fillPaint.Shader = shader;
            canvas.DrawRect(px, py - tw, cs, tw, _fillPaint);
            _fillPaint.Shader = null;
        }
        if (bot)
        {
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(px + cs / 2f, py + cs),
                new SKPoint(px + cs / 2f, py + cs + tw),
                new[] { new SKColor(255, 120, 30, 60), new SKColor(80, 30, 10, 15), SKColors.Transparent },
                new[] { 0f, 0.5f, 1f }, SKShaderTileMode.Clamp);
            _fillPaint.Shader = shader;
            canvas.DrawRect(px, py + cs, cs, tw, _fillPaint);
            _fillPaint.Shader = null;
        }
        if (left)
        {
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(px, py + cs / 2f),
                new SKPoint(px - tw, py + cs / 2f),
                new[] { new SKColor(255, 120, 30, 60), new SKColor(80, 30, 10, 15), SKColors.Transparent },
                new[] { 0f, 0.5f, 1f }, SKShaderTileMode.Clamp);
            _fillPaint.Shader = shader;
            canvas.DrawRect(px - tw, py, tw, cs, _fillPaint);
            _fillPaint.Shader = null;
        }
        if (right)
        {
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(px + cs, py + cs / 2f),
                new SKPoint(px + cs + tw, py + cs / 2f),
                new[] { new SKColor(255, 120, 30, 60), new SKColor(80, 30, 10, 15), SKColors.Transparent },
                new[] { 0f, 0.5f, 1f }, SKShaderTileMode.Clamp);
            _fillPaint.Shader = shader;
            canvas.DrawRect(px + cs, py, tw, cs, _fillPaint);
            _fillPaint.Shader = null;
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
        // Magischer Glow in Teleporter-Farbe (Violett → Transparent)
        float pulse = 0.7f + MathF.Sin(_globalTimer * 3f + px * 0.1f) * 0.3f;
        byte alpha = (byte)(45 * pulse);

        if (top)
        {
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(px + cs / 2f, py),
                new SKPoint(px + cs / 2f, py - tw * 0.7f),
                new[] { new SKColor(180, 100, 255, alpha), SKColors.Transparent },
                SKShaderTileMode.Clamp);
            _fillPaint.Shader = shader;
            canvas.DrawRect(px, py - tw * 0.7f, cs, tw * 0.7f, _fillPaint);
            _fillPaint.Shader = null;
        }
        if (bot)
        {
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(px + cs / 2f, py + cs),
                new SKPoint(px + cs / 2f, py + cs + tw * 0.7f),
                new[] { new SKColor(180, 100, 255, alpha), SKColors.Transparent },
                SKShaderTileMode.Clamp);
            _fillPaint.Shader = shader;
            canvas.DrawRect(px, py + cs, cs, tw * 0.7f, _fillPaint);
            _fillPaint.Shader = null;
        }
        if (left)
        {
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(px, py + cs / 2f),
                new SKPoint(px - tw * 0.7f, py + cs / 2f),
                new[] { new SKColor(180, 100, 255, alpha), SKColors.Transparent },
                SKShaderTileMode.Clamp);
            _fillPaint.Shader = shader;
            canvas.DrawRect(px - tw * 0.7f, py, tw * 0.7f, cs, _fillPaint);
            _fillPaint.Shader = null;
        }
        if (right)
        {
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(px + cs, py + cs / 2f),
                new SKPoint(px + cs + tw * 0.7f, py + cs / 2f),
                new[] { new SKColor(180, 100, 255, alpha), SKColors.Transparent },
                SKShaderTileMode.Clamp);
            _fillPaint.Shader = shader;
            canvas.DrawRect(px + cs, py, tw * 0.7f, cs, _fillPaint);
            _fillPaint.Shader = null;
        }
    }

    /// <summary>
    /// Spezial-Bomben-Zelleffekte: Eingefrorene Zellen (Eis-Bombe) und Lava-Zellen (Feuer-Bombe)
    /// </summary>
    private void RenderSpecialBombCellEffects(SKCanvas canvas, GameGrid grid)
    {
        int cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                var cell = grid[x, y];
                float px = x * cs;
                float py = y * cs;

                // --- Eingefrorene Zelle (Eis-Bombe) ---
                if (cell.IsFrozen)
                {
                    // Frost-Intensität basierend auf verbleibender Zeit (ausblenden am Ende)
                    float frostIntensity = Math.Min(1f, cell.FreezeTimer / 0.5f); // Letzten 0.5s ausblenden

                    // Halbtransparenter hellblauer Overlay
                    byte iceAlpha = (byte)(90 * frostIntensity);
                    _fillPaint.Color = isNeon
                        ? new SKColor(0, 180, 255, iceAlpha)
                        : new SKColor(160, 220, 255, iceAlpha);
                    _fillPaint.MaskFilter = null;
                    canvas.DrawRect(px, py, cs, cs, _fillPaint);

                    // Eis-Kristall-Linien (2-3 dünne weiße Diagonalen)
                    byte lineAlpha = (byte)(120 * frostIntensity);
                    _strokePaint.Color = new SKColor(220, 240, 255, lineAlpha);
                    _strokePaint.StrokeWidth = 0.8f;
                    _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
                    // Diagonale 1: oben-links nach mitte
                    canvas.DrawLine(px + cs * 0.15f, py + cs * 0.2f, px + cs * 0.5f, py + cs * 0.55f, _strokePaint);
                    // Diagonale 2: oben-rechts nach mitte-unten
                    canvas.DrawLine(px + cs * 0.8f, py + cs * 0.15f, px + cs * 0.45f, py + cs * 0.6f, _strokePaint);
                    // Diagonale 3: mitte nach unten-rechts
                    canvas.DrawLine(px + cs * 0.35f, py + cs * 0.45f, px + cs * 0.75f, py + cs * 0.85f, _strokePaint);
                    _strokePaint.MaskFilter = null;

                    // Shimmer-Effekt (pulsierender weißer Punkt)
                    float shimmerPulse = (MathF.Sin(_globalTimer * 4f + x * 1.3f + y * 0.9f) + 1f) * 0.5f;
                    byte shimmerAlpha = (byte)(60 * shimmerPulse * frostIntensity);
                    if (shimmerAlpha > 10)
                    {
                        _fillPaint.Color = new SKColor(255, 255, 255, shimmerAlpha);
                        _fillPaint.MaskFilter = _smallGlow;
                        canvas.DrawCircle(px + cs * 0.4f + MathF.Sin(_globalTimer + x) * 3f,
                            py + cs * 0.35f + MathF.Cos(_globalTimer * 0.7f + y) * 2f,
                            2.5f, _fillPaint);
                        _fillPaint.MaskFilter = null;
                    }
                }

                // --- Lava-Zelle (Feuer-Bombe) ---
                if (cell.IsLavaActive)
                {
                    // Lava-Intensität basierend auf verbleibender Zeit
                    float lavaIntensity = Math.Min(1f, cell.LavaTimer / 0.5f);

                    // Pulsierender Glow (Intensität schwankt mit sin(timer))
                    float lavaPulse = 0.7f + MathF.Sin(_globalTimer * 3.5f + x * 0.8f + y * 1.2f) * 0.3f;

                    // Rot-orange Overlay
                    byte lavaAlpha = (byte)(120 * lavaIntensity * lavaPulse);
                    _fillPaint.Color = isNeon
                        ? new SKColor(255, 60, 0, lavaAlpha)
                        : new SKColor(255, 100, 20, lavaAlpha);
                    _fillPaint.MaskFilter = null;
                    canvas.DrawRect(px, py, cs, cs, _fillPaint);

                    // Innerer Glow (heller, pulsierend)
                    byte innerAlpha = (byte)(60 * lavaIntensity * lavaPulse);
                    _fillPaint.Color = new SKColor(255, 200, 50, innerAlpha);
                    _fillPaint.MaskFilter = _smallGlow;
                    canvas.DrawRect(px + cs * 0.15f, py + cs * 0.15f, cs * 0.7f, cs * 0.7f, _fillPaint);
                    _fillPaint.MaskFilter = null;

                    // Lava-Blasen (2-3 kleine orange Kreise die periodisch erscheinen/verschwinden)
                    for (int b = 0; b < 3; b++)
                    {
                        // Jede Blase hat eigenen Phasen-Offset
                        float bubblePhase = (_globalTimer * 1.5f + b * 1.1f + x * 0.7f + y * 0.5f) % 2f;
                        if (bubblePhase < 1.2f) // Sichtbar für 1.2s von 2s Zyklus
                        {
                            float bubbleLife = bubblePhase / 1.2f; // 0→1
                            // Blase wächst, steigt auf, platzt
                            float bubbleSize = MathF.Sin(bubbleLife * MathF.PI) * 2.5f;
                            float bubbleX = px + cs * (0.25f + b * 0.25f) + MathF.Sin(_globalTimer * 0.8f + b) * 2f;
                            float bubbleY = py + cs * 0.7f - bubbleLife * cs * 0.3f;

                            if (bubbleSize > 0.5f)
                            {
                                byte bubbleAlpha = (byte)(150 * MathF.Sin(bubbleLife * MathF.PI) * lavaIntensity);
                                _fillPaint.Color = new SKColor(255, 160, 30, bubbleAlpha);
                                _fillPaint.MaskFilter = null;
                                canvas.DrawCircle(bubbleX, bubbleY, bubbleSize, _fillPaint);

                                // Heller Kern
                                if (bubbleSize > 1.5f)
                                {
                                    _fillPaint.Color = new SKColor(255, 220, 80, (byte)(bubbleAlpha * 0.6f));
                                    canvas.DrawCircle(bubbleX, bubbleY, bubbleSize * 0.5f, _fillPaint);
                                }
                            }
                        }
                    }
                }

                // --- Rauchwolke (Smoke-Bombe) ---
                if (cell.IsSmokeCloud)
                {
                    float smokeIntensity = Math.Min(1f, cell.SmokeTimer / 0.5f);
                    byte smokeAlpha = (byte)(100 * smokeIntensity);

                    // Grauer halbtransparenter Nebel
                    _fillPaint.Color = new SKColor(140, 140, 140, smokeAlpha);
                    _fillPaint.MaskFilter = _smallGlow;
                    canvas.DrawRect(px, py, cs, cs, _fillPaint);
                    _fillPaint.MaskFilter = null;

                    // Wirbelnde Rauchschwaden (2-3 helle Kreise)
                    for (int s = 0; s < 3; s++)
                    {
                        float angle = _globalTimer * 0.5f + s * 2.094f + x * 0.7f;
                        float sx = px + cs * 0.5f + MathF.Cos(angle) * cs * 0.2f;
                        float sy = py + cs * 0.5f + MathF.Sin(angle) * cs * 0.15f;
                        byte sAlpha = (byte)(50 * smokeIntensity + MathF.Sin(_globalTimer * 2f + s) * 20);
                        _fillPaint.Color = new SKColor(200, 200, 200, sAlpha);
                        _fillPaint.MaskFilter = null;
                        canvas.DrawCircle(sx, sy, cs * 0.15f, _fillPaint);
                    }
                }

                // --- Gift-Zelle (Poison-Bombe) ---
                if (cell.IsPoisoned)
                {
                    float poisonIntensity = Math.Min(1f, cell.PoisonTimer / 0.5f);
                    float poisonPulse = 0.7f + MathF.Sin(_globalTimer * 2.5f + x * 1.1f + y * 0.9f) * 0.3f;
                    byte poisonAlpha = (byte)(80 * poisonIntensity * poisonPulse);

                    // Grüner halbtransparenter Overlay
                    _fillPaint.Color = new SKColor(0, 180, 0, poisonAlpha);
                    _fillPaint.MaskFilter = null;
                    canvas.DrawRect(px, py, cs, cs, _fillPaint);

                    // Gift-Blasen
                    for (int b = 0; b < 2; b++)
                    {
                        float bPhase = (_globalTimer * 1.2f + b * 0.8f + x * 0.5f) % 1.5f;
                        float bx = px + cs * (0.3f + b * 0.4f);
                        float by = py + cs * 0.8f - bPhase * cs * 0.4f;
                        float bSize = MathF.Sin(bPhase / 1.5f * MathF.PI) * 2f;
                        if (bSize > 0.3f)
                        {
                            _fillPaint.Color = new SKColor(0, 220, 0, (byte)(100 * poisonIntensity));
                            canvas.DrawCircle(bx, by, bSize, _fillPaint);
                        }
                    }
                }

                // --- Gravitationsfeld (Gravity-Bombe) ---
                if (cell.IsGravityWell)
                {
                    float gravIntensity = Math.Min(1f, cell.GravityTimer / 0.3f);
                    byte gravAlpha = (byte)(50 * gravIntensity);

                    // Violetter konzentrischer Ring
                    _strokePaint.Color = new SKColor(150, 80, 220, gravAlpha);
                    _strokePaint.StrokeWidth = 1f;
                    _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
                    float gravPulse = 0.5f + MathF.Sin(_globalTimer * 4f + x + y) * 0.3f;
                    canvas.DrawCircle(px + cs * 0.5f, py + cs * 0.5f, cs * 0.4f * gravPulse, _strokePaint);
                    _strokePaint.MaskFilter = null;
                }

                // --- Zeitverzerrung (TimeWarp-Bombe) ---
                if (cell.IsTimeWarped)
                {
                    float twIntensity = Math.Min(1f, cell.TimeWarpTimer / 0.5f);
                    byte twAlpha = (byte)(50 * twIntensity);

                    // Blauer Schimmer
                    _fillPaint.Color = new SKColor(80, 130, 255, twAlpha);
                    _fillPaint.MaskFilter = _smallGlow;
                    canvas.DrawRect(px, py, cs, cs, _fillPaint);
                    _fillPaint.MaskFilter = null;

                    // Uhrzeiger-Kreuz (langsam rotierend)
                    float twAngle = _globalTimer * 0.3f; // Langsame Rotation = Zeitverlangsamung
                    _strokePaint.Color = new SKColor(150, 180, 255, (byte)(80 * twIntensity));
                    _strokePaint.StrokeWidth = 0.8f;
                    _strokePaint.MaskFilter = null;
                    float cx = px + cs * 0.5f;
                    float cy = py + cs * 0.5f;
                    float handLen = cs * 0.3f;
                    canvas.DrawLine(cx, cy,
                        cx + MathF.Cos(twAngle) * handLen,
                        cy + MathF.Sin(twAngle) * handLen, _strokePaint);
                    canvas.DrawLine(cx, cy,
                        cx + MathF.Cos(twAngle + MathF.PI * 0.5f) * handLen * 0.6f,
                        cy + MathF.Sin(twAngle + MathF.PI * 0.5f) * handLen * 0.6f, _strokePaint);
                }

                // --- Schwarzes Loch (BlackHole-Bombe) ---
                if (cell.IsBlackHole)
                {
                    float bhIntensity = Math.Min(1f, cell.BlackHoleTimer / 0.5f);

                    // Dunkler Overlay
                    byte bhAlpha = (byte)(80 * bhIntensity);
                    _fillPaint.Color = new SKColor(20, 0, 40, bhAlpha);
                    _fillPaint.MaskFilter = null;
                    canvas.DrawRect(px, py, cs, cs, _fillPaint);

                    // Violetter Sog-Ring (nach innen pulsierend)
                    float sogPhase = (_globalTimer * 2f) % 1f;
                    float sogRadius = cs * 0.4f * (1f - sogPhase * 0.5f);
                    byte sogAlpha = (byte)(60 * bhIntensity * (1f - sogPhase));
                    _strokePaint.Color = new SKColor(100, 0, 200, sogAlpha);
                    _strokePaint.StrokeWidth = 1.5f;
                    _strokePaint.MaskFilter = _smallGlow;
                    canvas.DrawCircle(px + cs * 0.5f, py + cs * 0.5f, sogRadius, _strokePaint);
                    _strokePaint.MaskFilter = null;
                }
            }
        }
    }

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

    private void RenderFloorTile(SKCanvas canvas, float px, float py, int cs, int gx, int gy, bool isNeon)
    {
        // Schachbrett-Basis (subtile Farbvariation zwischen Zellen)
        bool alt = (gx + gy) % 2 == 0;
        _fillPaint.Color = alt ? _palette.FloorBase : _palette.FloorAlt;
        _fillPaint.MaskFilter = null;
        canvas.DrawRect(px, py, cs, cs, _fillPaint);

        // Welt-spezifische Boden-Details (nur Classic/Retro, bei Neon subtilere Variante)
        if (_worldPalette != null)
        {
            switch (_currentWorldIndex)
            {
                case 0: // Forest: Grashalme + leichte Farbvariation
                    if (!isNeon)
                        ProceduralTextures.DrawGrassBlades(canvas, _fillPaint, px, py, cs, gx, gy, _globalTimer, 50);
                    else
                    {
                        // Neon: Subtile grüne Leuchtpunkte
                        float gp = ProceduralTextures.CellRandom(gx, gy, 10);
                        if (gp < 0.3f)
                        {
                            _fillPaint.Color = new SKColor(0, 200, 80, 15);
                            canvas.DrawCircle(px + gp * cs + 5, py + cs * 0.7f, 1.5f, _fillPaint);
                        }
                    }
                    break;

                case 1: // Industrial: Metallplatten-Rillen + Nieten
                    if (!isNeon)
                    {
                        // Horizontale Rillen
                        _strokePaint.Color = new SKColor(140, 140, 150, 35);
                        _strokePaint.StrokeWidth = 0.5f;
                        _strokePaint.MaskFilter = null;
                        canvas.DrawLine(px + 2, py + cs * 0.33f, px + cs - 2, py + cs * 0.33f, _strokePaint);
                        canvas.DrawLine(px + 2, py + cs * 0.66f, px + cs - 2, py + cs * 0.66f, _strokePaint);
                        // Nieten nur auf manchen Zellen
                        if (ProceduralTextures.CellRandom(gx, gy, 11) < 0.35f)
                            ProceduralTextures.DrawMetalRivets(canvas, _fillPaint, px, py, cs, new SKColor(180, 180, 190), 50);
                    }
                    else
                    {
                        // Neon: Blaue Gitter-Highlights
                        if ((gx + gy) % 3 == 0)
                        {
                            _strokePaint.Color = new SKColor(0, 140, 255, 20);
                            _strokePaint.StrokeWidth = 0.5f;
                            _strokePaint.MaskFilter = null;
                            canvas.DrawLine(px + 3, py + cs * 0.5f, px + cs - 3, py + cs * 0.5f, _strokePaint);
                        }
                    }
                    break;

                case 2: // Cavern: Risse + dunkle Flecken
                    if (!isNeon)
                    {
                        if (ProceduralTextures.CellRandom(gx, gy, 12) < 0.4f)
                            ProceduralTextures.DrawCracks(canvas, _strokePaint, px, py, cs, gx, gy, new SKColor(120, 100, 140), 30);
                        // Dunkle Feuchtigkeitsflecken
                        if (ProceduralTextures.CellRandom(gx, gy, 13) < 0.2f)
                        {
                            _fillPaint.Color = new SKColor(0, 0, 0, 15);
                            float fx = px + ProceduralTextures.CellRandom(gx, gy, 14) * cs;
                            float fy = py + ProceduralTextures.CellRandom(gx, gy, 15) * cs;
                            canvas.DrawOval(fx, fy, cs * 0.2f, cs * 0.15f, _fillPaint);
                        }
                    }
                    else
                    {
                        // Neon: Lila Kristallschimmer
                        if (ProceduralTextures.CellRandom(gx, gy, 16) < 0.25f)
                        {
                            float shimmer = MathF.Sin(_globalTimer * 1.5f + gx * 1.1f + gy * 0.7f) * 0.5f + 0.5f;
                            _fillPaint.Color = new SKColor(180, 0, 255, (byte)(12 * shimmer));
                            canvas.DrawCircle(px + cs * 0.5f, py + cs * 0.5f, cs * 0.12f, _fillPaint);
                        }
                    }
                    break;

                case 3: // Sky: Noise-basierte Wolkenmuster
                {
                    float noise = ProceduralTextures.Noise2D(gx * 0.4f, gy * 0.4f + _globalTimer * 0.05f);
                    byte cloudAlpha = (byte)(noise * (isNeon ? 12 : 20));
                    if (cloudAlpha > 3)
                    {
                        _fillPaint.Color = isNeon
                            ? new SKColor(0, 220, 255, cloudAlpha)
                            : new SKColor(255, 255, 255, cloudAlpha);
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.5f, cs * 0.4f, cs * 0.25f, _fillPaint);
                    }
                    break;
                }

                case 4: // Inferno: Glutrisse + Ascheflecken
                    if (!isNeon)
                    {
                        if (ProceduralTextures.CellRandom(gx, gy, 17) < 0.35f)
                            ProceduralTextures.DrawEmberCracks(canvas, _strokePaint, px, py, cs, gx, gy, _globalTimer, 40);
                        if (ProceduralTextures.CellRandom(gx, gy, 18) < 0.3f)
                            ProceduralTextures.DrawSandGrain(canvas, _fillPaint, px, py, cs, gx, gy, new SKColor(60, 50, 45), 20);
                    }
                    else
                    {
                        // Neon: Pulsierende rote Risslinien
                        if (ProceduralTextures.CellRandom(gx, gy, 19) < 0.3f)
                        {
                            float pulse = MathF.Sin(_globalTimer * 2.5f + gx + gy * 0.8f) * 0.5f + 0.5f;
                            _strokePaint.Color = new SKColor(255, 40, 0, (byte)(25 * pulse));
                            _strokePaint.StrokeWidth = 0.8f;
                            _strokePaint.MaskFilter = null;
                            float sx = px + ProceduralTextures.CellRandom(gx, gy, 20) * cs;
                            float sy = py + ProceduralTextures.CellRandom(gx, gy, 21) * cs * 0.3f;
                            canvas.DrawLine(sx, sy, sx + cs * 0.4f, sy + cs * 0.5f, _strokePaint);
                        }
                    }
                    break;

                case 5: // Ruins: Erodierter Sandstein + Risslinien
                    if (!isNeon)
                    {
                        ProceduralTextures.DrawSandGrain(canvas, _fillPaint, px, py, cs, gx, gy, new SKColor(190, 170, 130), 20);
                        if (ProceduralTextures.CellRandom(gx, gy, 22) < 0.3f)
                            ProceduralTextures.DrawCracks(canvas, _strokePaint, px, py, cs, gx, gy, new SKColor(150, 130, 100), 25);
                    }
                    else
                    {
                        // Neon: Goldene Sandkörner
                        if (ProceduralTextures.CellRandom(gx, gy, 23) < 0.3f)
                        {
                            _fillPaint.Color = new SKColor(255, 200, 0, 10);
                            float dx = px + ProceduralTextures.CellRandom(gx, gy, 24) * cs;
                            float dy = py + ProceduralTextures.CellRandom(gx, gy, 25) * cs;
                            canvas.DrawCircle(dx, dy, 0.8f, _fillPaint);
                        }
                    }
                    break;

                case 6: // Ocean: Kaustik-Muster + Wellenlinien
                {
                    float noise = ProceduralTextures.Noise2D(gx * 0.5f + _globalTimer * 0.15f, gy * 0.5f + _globalTimer * 0.1f);
                    byte caustAlpha = (byte)(noise * (isNeon ? 15 : 25));
                    if (caustAlpha > 3)
                    {
                        _fillPaint.Color = isNeon
                            ? new SKColor(0, 200, 220, caustAlpha)
                            : new SKColor(180, 230, 255, caustAlpha);
                        canvas.DrawCircle(px + cs * 0.5f + noise * 3f, py + cs * 0.5f, cs * 0.15f, _fillPaint);
                    }
                    // Wellenlinien
                    if (!isNeon && ProceduralTextures.CellRandom(gx, gy, 26) < 0.25f)
                    {
                        _strokePaint.Color = new SKColor(100, 180, 220, 18);
                        _strokePaint.StrokeWidth = 0.5f;
                        _strokePaint.MaskFilter = null;
                        float wy = py + cs * 0.4f + MathF.Sin(_globalTimer * 0.8f + gx * 0.6f) * 2f;
                        canvas.DrawLine(px + 2, wy, px + cs - 2, wy + 1.5f, _strokePaint);
                    }
                    break;
                }

                case 7: // Volcano: Glänzender Obsidian mit Rissen
                    if (!isNeon)
                    {
                        // Glanz-Highlight
                        float gloss = ProceduralTextures.CellRandom(gx, gy, 27);
                        if (gloss < 0.25f)
                        {
                            _fillPaint.Color = new SKColor(255, 255, 255, 12);
                            canvas.DrawOval(px + cs * gloss + cs * 0.3f, py + cs * 0.35f, cs * 0.15f, cs * 0.08f, _fillPaint);
                        }
                        if (ProceduralTextures.CellRandom(gx, gy, 28) < 0.3f)
                            ProceduralTextures.DrawCracks(canvas, _strokePaint, px, py, cs, gx, gy, new SKColor(40, 25, 20), 25);
                    }
                    else
                    {
                        // Neon: Orange Adern
                        if (ProceduralTextures.CellRandom(gx, gy, 29) < 0.2f)
                        {
                            float pulse = MathF.Sin(_globalTimer * 1.8f + gx * 0.9f + gy) * 0.4f + 0.6f;
                            _strokePaint.Color = new SKColor(255, 120, 0, (byte)(18 * pulse));
                            _strokePaint.StrokeWidth = 0.6f;
                            _strokePaint.MaskFilter = null;
                            canvas.DrawLine(px + 3, py + cs * 0.6f, px + cs - 3, py + cs * 0.4f, _strokePaint);
                        }
                    }
                    break;

                case 8: // SkyFortress: Marmor-Adern
                    if (!isNeon)
                    {
                        if (ProceduralTextures.CellRandom(gx, gy, 30) < 0.4f)
                            ProceduralTextures.DrawMarbleVeins(canvas, _strokePaint, px, py, cs, gx, gy, new SKColor(180, 170, 150), 20);
                    }
                    else
                    {
                        // Neon: Goldene Lichtpunkte
                        float sparkle = MathF.Sin(_globalTimer * 3f + gx * 2.1f + gy * 1.3f) * 0.5f + 0.5f;
                        if (sparkle > 0.7f && ProceduralTextures.CellRandom(gx, gy, 31) < 0.2f)
                        {
                            _fillPaint.Color = new SKColor(255, 230, 100, (byte)(15 * sparkle));
                            canvas.DrawCircle(px + cs * 0.5f, py + cs * 0.5f, 1f, _fillPaint);
                        }
                    }
                    break;

                case 9: // ShadowRealm: Nebelschleier + subtile Augen
                {
                    float mist = ProceduralTextures.Noise2D(gx * 0.3f + _globalTimer * 0.03f, gy * 0.3f);
                    byte mistAlpha = (byte)(mist * (isNeon ? 15 : 20));
                    if (mistAlpha > 3)
                    {
                        _fillPaint.Color = isNeon
                            ? new SKColor(100, 20, 180, mistAlpha)
                            : new SKColor(20, 10, 35, mistAlpha);
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.5f, cs * 0.35f, cs * 0.2f, _fillPaint);
                    }
                    // Subtile Augen (sehr selten)
                    if (ProceduralTextures.CellRandom(gx, gy, 32) < 0.03f)
                    {
                        float blink = (_globalTimer * 0.2f + gx * 1.7f) % 6f;
                        if (blink < 5.5f) // Offen
                        {
                            byte eyeAlpha = (byte)(isNeon ? 20 : 15);
                            _fillPaint.Color = isNeon
                                ? new SKColor(200, 60, 255, eyeAlpha)
                                : new SKColor(160, 80, 200, eyeAlpha);
                            canvas.DrawOval(px + cs * 0.35f, py + cs * 0.5f, 1.5f, 1f, _fillPaint);
                            canvas.DrawOval(px + cs * 0.65f, py + cs * 0.5f, 1.5f, 1f, _fillPaint);
                        }
                    }
                    break;
                }
            }
        }

        // Gitter-Linien (subtil)
        _strokePaint.Color = _palette.FloorLine;
        _strokePaint.StrokeWidth = isNeon ? 0.5f : 1f;
        _strokePaint.MaskFilter = null;
        canvas.DrawLine(px, py, px + cs, py, _strokePaint);
        canvas.DrawLine(px, py, px, py + cs, _strokePaint);
    }

    private void RenderWallTile(SKCanvas canvas, float px, float py, int cs, int gx, int gy, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        if (isNeon)
        {
            // Dunkler Stahlblock
            _fillPaint.Color = _palette.WallBase;
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Neon-Kanten-Glow (Welt-Akzentfarbe)
            _strokePaint.Color = _palette.WallEdge;
            _strokePaint.StrokeWidth = 1.5f;
            _strokePaint.MaskFilter = _smallGlow;
            canvas.DrawRect(px + 1, py + 1, cs - 2, cs - 2, _strokePaint);
            _strokePaint.MaskFilter = null;
        }
        else
        {
            // 3D Steinblock
            _fillPaint.Color = _palette.WallBase;
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Highlight (oben + links)
            _fillPaint.Color = _palette.WallHighlight;
            canvas.DrawRect(px, py, cs, 3, _fillPaint);
            canvas.DrawRect(px, py, 3, cs, _fillPaint);

            // Schatten (unten + rechts)
            _fillPaint.Color = _palette.WallShadow;
            canvas.DrawRect(px, py + cs - 3, cs, 3, _fillPaint);
            canvas.DrawRect(px + cs - 3, py, 3, cs, _fillPaint);
        }

        // Welt-spezifische Wand-Details
        if (_worldPalette != null)
        {
            switch (_currentWorldIndex)
            {
                case 0: // Forest: Bemooste Steine
                    if (!isNeon)
                        ProceduralTextures.DrawMossPatches(canvas, _fillPaint, px, py, cs, gx, gy, 35);
                    else if (ProceduralTextures.CellRandom(gx, gy, 50) < 0.3f)
                    {
                        _fillPaint.Color = new SKColor(0, 180, 60, 15);
                        canvas.DrawOval(px + cs * 0.3f, py + cs - 3, cs * 0.2f, 2f, _fillPaint);
                    }
                    break;

                case 1: // Industrial: Stahlplatten mit Nieten + Naht
                    if (!isNeon)
                    {
                        ProceduralTextures.DrawMetalRivets(canvas, _fillPaint, px, py, cs, new SKColor(140, 145, 155), 60);
                        // Horizontale Naht
                        _strokePaint.Color = new SKColor(50, 55, 65, 50);
                        _strokePaint.StrokeWidth = 0.8f;
                        _strokePaint.MaskFilter = null;
                        canvas.DrawLine(px + 4, py + cs * 0.5f, px + cs - 4, py + cs * 0.5f, _strokePaint);
                    }
                    break;

                case 2: // Cavern: Kristall-Einschlüsse
                    if (ProceduralTextures.CellRandom(gx, gy, 51) < 0.35f)
                    {
                        float cx = px + ProceduralTextures.CellRandom(gx, gy, 52) * cs * 0.6f + cs * 0.2f;
                        float cy = py + ProceduralTextures.CellRandom(gx, gy, 53) * cs * 0.6f + cs * 0.2f;
                        float crystalSize = 2f + ProceduralTextures.CellRandom(gx, gy, 54) * 2f;
                        byte crystalAlpha = isNeon ? (byte)40 : (byte)50;
                        _fillPaint.Color = new SKColor(160, 100, 220, crystalAlpha);
                        // Raute (Kristall-Form)
                        using var path = new SKPath();
                        path.MoveTo(cx, cy - crystalSize);
                        path.LineTo(cx + crystalSize * 0.6f, cy);
                        path.LineTo(cx, cy + crystalSize);
                        path.LineTo(cx - crystalSize * 0.6f, cy);
                        path.Close();
                        canvas.DrawPath(path, _fillPaint);
                        if (isNeon)
                        {
                            _fillPaint.Color = new SKColor(200, 100, 255, 20);
                            _fillPaint.MaskFilter = _smallGlow;
                            canvas.DrawPath(path, _fillPaint);
                            _fillPaint.MaskFilter = null;
                        }
                    }
                    break;

                case 3: // Sky: Weiche Wolkensäulen
                    if (!isNeon)
                    {
                        _fillPaint.Color = new SKColor(255, 255, 255, 15);
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.3f, cs * 0.35f, cs * 0.2f, _fillPaint);
                        canvas.DrawOval(px + cs * 0.4f, py + cs * 0.6f, cs * 0.3f, cs * 0.15f, _fillPaint);
                    }
                    else
                    {
                        _fillPaint.Color = new SKColor(0, 220, 255, 8);
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.4f, cs * 0.3f, cs * 0.15f, _fillPaint);
                    }
                    break;

                case 4: // Inferno: Glut-durchzogener Obsidian
                    ProceduralTextures.DrawEmberCracks(canvas, _strokePaint, px, py, cs, gx, gy, _globalTimer,
                        isNeon ? (byte)50 : (byte)45);
                    break;

                case 5: // Ruins: Sandstein mit Risslinien + abgebrochene Ecken
                    if (!isNeon)
                    {
                        ProceduralTextures.DrawCracks(canvas, _strokePaint, px, py, cs, gx, gy,
                            new SKColor(120, 100, 70), 35);
                        // Abgebrochene Ecke (einzelne Zellen)
                        if (ProceduralTextures.CellRandom(gx, gy, 55) < 0.2f)
                        {
                            _fillPaint.Color = _palette.FloorBase.WithAlpha(180);
                            using var chip = new SKPath();
                            chip.MoveTo(px + cs, py);
                            chip.LineTo(px + cs - 4, py);
                            chip.LineTo(px + cs, py + 4);
                            chip.Close();
                            canvas.DrawPath(chip, _fillPaint);
                        }
                    }
                    break;

                case 6: // Ocean: Korallen-bewachsen
                    if (!isNeon)
                        ProceduralTextures.DrawCoralGrowth(canvas, _fillPaint, px, py, cs, gx, gy,
                            new SKColor(200, 100, 120), 40);
                    else if (ProceduralTextures.CellRandom(gx, gy, 56) < 0.3f)
                    {
                        _fillPaint.Color = new SKColor(0, 200, 220, 15);
                        float cx = px + cs * 0.5f;
                        float cy = py + cs * 0.5f;
                        canvas.DrawCircle(cx, cy, 2.5f, _fillPaint);
                    }
                    break;

                case 7: // Volcano: Basalt-Säulen (vertikale Linien)
                {
                    byte lineAlpha = isNeon ? (byte)25 : (byte)30;
                    _strokePaint.Color = isNeon
                        ? new SKColor(255, 80, 0, lineAlpha)
                        : new SKColor(60, 45, 40, lineAlpha);
                    _strokePaint.StrokeWidth = 0.6f;
                    _strokePaint.MaskFilter = null;
                    int lines = 3 + (int)(ProceduralTextures.CellRandom(gx, gy, 57) * 2);
                    for (int i = 0; i < lines; i++)
                    {
                        float lx = px + cs * (0.15f + i * 0.7f / lines);
                        canvas.DrawLine(lx, py + 3, lx, py + cs - 3, _strokePaint);
                    }
                    break;
                }

                case 8: // SkyFortress: Marmor-Säulen mit Glanz
                    if (!isNeon)
                    {
                        ProceduralTextures.DrawMarbleVeins(canvas, _strokePaint, px, py, cs, gx, gy,
                            new SKColor(200, 185, 150), 20);
                        // Glanz-Highlight
                        _fillPaint.Color = new SKColor(255, 255, 240, 15);
                        canvas.DrawRect(px + 3, py + 2, 2, cs - 4, _fillPaint);
                    }
                    else
                    {
                        // Neon: Goldene Adern
                        if (ProceduralTextures.CellRandom(gx, gy, 58) < 0.4f)
                        {
                            _strokePaint.Color = new SKColor(255, 230, 100, 20);
                            _strokePaint.StrokeWidth = 0.5f;
                            _strokePaint.MaskFilter = _smallGlow;
                            float vy = py + ProceduralTextures.CellRandom(gx, gy, 59) * cs;
                            canvas.DrawLine(px + 3, vy, px + cs - 3, vy + 2, _strokePaint);
                            _strokePaint.MaskFilter = null;
                        }
                    }
                    break;

                case 9: // ShadowRealm: Pulsierende dunkle Masse
                {
                    float pulse = MathF.Sin(_globalTimer * 1.5f + gx * 0.9f + gy * 1.1f) * 0.4f + 0.6f;
                    byte darkAlpha = (byte)(isNeon ? 20 * pulse : 15 * pulse);
                    _fillPaint.Color = isNeon
                        ? new SKColor(150, 30, 220, darkAlpha)
                        : new SKColor(40, 15, 60, darkAlpha);
                    canvas.DrawRect(px + 2, py + 2, cs - 4, cs - 4, _fillPaint);
                    break;
                }
            }
        }
    }

    private void RenderBlockTile(SKCanvas canvas, float px, float py, int cs, int gx, int gy, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        if (isNeon)
        {
            // Dunkler Block mit sichtbarem Rand
            _fillPaint.Color = _palette.BlockBase;
            canvas.DrawRect(px + 1, py + 1, cs - 2, cs - 2, _fillPaint);

            // Heller Rand oben/links für 3D-Effekt
            _fillPaint.Color = _palette.BlockHighlight;
            canvas.DrawRect(px + 1, py + 1, cs - 2, 2, _fillPaint);
            canvas.DrawRect(px + 1, py + 1, 2, cs - 2, _fillPaint);

            // Dunkler Rand unten/rechts
            _fillPaint.Color = _palette.BlockShadow;
            canvas.DrawRect(px + 1, py + cs - 3, cs - 2, 2, _fillPaint);
            canvas.DrawRect(px + cs - 3, py + 1, 2, cs - 2, _fillPaint);

            // Glow-Riss-Muster
            _strokePaint.Color = _palette.BlockMortar;
            _strokePaint.StrokeWidth = 1.5f;
            _strokePaint.MaskFilter = _smallGlow;

            // Horizontaler Riss
            canvas.DrawLine(px + 4, py + cs / 2f, px + cs - 4, py + cs / 2f, _strokePaint);
            // Vertikaler Riss (versetzt pro Spalte)
            float vx = (gx % 2 == 0) ? px + cs / 2f : px + cs / 3f;
            canvas.DrawLine(vx, py + 4, vx, py + cs / 2f, _strokePaint);

            // Zusätzlicher diagonaler Riss
            float vx2 = (gx % 2 == 0) ? px + cs * 0.65f : px + cs * 0.6f;
            canvas.DrawLine(vx2, py + cs / 2f + 2, vx2 - cs * 0.15f, py + cs - 4, _strokePaint);
            _strokePaint.MaskFilter = null;
        }
        else
        {
            // 3D Ziegel mit Mörtellinien
            _fillPaint.Color = _palette.BlockBase;
            canvas.DrawRect(px + 2, py + 2, cs - 4, cs - 4, _fillPaint);

            // Highlight (oben + links)
            _fillPaint.Color = _palette.BlockHighlight;
            canvas.DrawRect(px + 2, py + 2, cs - 4, 2, _fillPaint);
            canvas.DrawRect(px + 2, py + 2, 2, cs - 4, _fillPaint);

            // Schatten (unten + rechts)
            _fillPaint.Color = _palette.BlockShadow;
            canvas.DrawRect(px + 2, py + cs - 4, cs - 4, 2, _fillPaint);
            canvas.DrawRect(px + cs - 4, py + 2, 2, cs - 4, _fillPaint);

            // Mörtel-Kreuzlinien
            _strokePaint.Color = _palette.BlockMortar;
            _strokePaint.StrokeWidth = 1f;
            _strokePaint.MaskFilter = null;
            canvas.DrawLine(px + 2, py + cs / 2f, px + cs - 2, py + cs / 2f, _strokePaint);
            float vx = (gx % 2 == 0) ? px + cs / 2f : px + cs / 3f;
            canvas.DrawLine(vx, py + 2, vx, py + cs / 2f, _strokePaint);
        }

        // Welt-spezifische Block-Details (intensiviert)
        if (_worldPalette != null)
        {
            switch (_currentWorldIndex)
            {
                case 0: // Forest: Holzkiste mit kräftiger Maserung + Metallbändern + Nägeln
                    if (!isNeon)
                    {
                        // Kräftigere Holzmaserung
                        ProceduralTextures.DrawWoodGrain(canvas, _strokePaint, px + 3, py + 3, cs - 6,
                            gx, gy, new SKColor(90, 60, 30), 25);
                        // Horizontales Metallband (Kisten-Look)
                        _fillPaint.Color = new SKColor(100, 95, 85, 100);
                        canvas.DrawRect(px + 2, py + cs / 2f - 1.5f, cs - 4, 3, _fillPaint);
                        // Nagelköpfe (4 Ecken + 2 auf dem Band)
                        _fillPaint.Color = new SKColor(140, 130, 115, 140);
                        canvas.DrawCircle(px + 5, py + 5, 1.5f, _fillPaint);
                        canvas.DrawCircle(px + cs - 5, py + 5, 1.5f, _fillPaint);
                        canvas.DrawCircle(px + 5, py + cs - 5, 1.5f, _fillPaint);
                        canvas.DrawCircle(px + cs - 5, py + cs - 5, 1.5f, _fillPaint);
                        // Nägel auf dem Band
                        _fillPaint.Color = new SKColor(160, 150, 130, 160);
                        canvas.DrawCircle(px + cs * 0.25f, py + cs / 2f, 1.2f, _fillPaint);
                        canvas.DrawCircle(px + cs * 0.75f, py + cs / 2f, 1.2f, _fillPaint);
                        // Holzfarb-Gradient (dunkler unten)
                        _fillPaint.Color = new SKColor(40, 25, 10, 30);
                        canvas.DrawRect(px + 3, py + cs * 0.7f, cs - 6, cs * 0.27f, _fillPaint);
                    }
                    else
                    {
                        // Neon: Grüne Holz-Silhouette + Nagelglow
                        _strokePaint.Color = new SKColor(0, 180, 60, 30);
                        _strokePaint.StrokeWidth = 0.7f;
                        _strokePaint.MaskFilter = null;
                        canvas.DrawLine(px + 4, py + cs * 0.3f, px + cs - 4, py + cs * 0.35f, _strokePaint);
                        canvas.DrawLine(px + 4, py + cs * 0.6f, px + cs - 4, py + cs * 0.65f, _strokePaint);
                        _fillPaint.Color = new SKColor(0, 200, 80, 25);
                        _fillPaint.MaskFilter = _smallGlow;
                        canvas.DrawCircle(px + 5, py + 5, 1.2f, _fillPaint);
                        canvas.DrawCircle(px + cs - 5, py + cs - 5, 1.2f, _fillPaint);
                        _fillPaint.MaskFilter = null;
                    }
                    break;

                case 1: // Industrial: Metall-Container mit Warnstreifen + Bolzen + Rost
                    if (!isNeon)
                    {
                        // Diagonale Warnstreifen (gelb/schwarz)
                        _strokePaint.Color = new SKColor(200, 180, 40, 50);
                        _strokePaint.StrokeWidth = 2f;
                        _strokePaint.MaskFilter = null;
                        canvas.DrawLine(px + 3, py + cs - 3, px + cs * 0.35f, py + 3, _strokePaint);
                        canvas.DrawLine(px + cs * 0.35f, py + cs - 3, px + cs * 0.65f, py + 3, _strokePaint);
                        canvas.DrawLine(px + cs * 0.65f, py + cs - 3, px + cs - 3, py + 3, _strokePaint);
                        // Container-Rillen
                        _strokePaint.Color = new SKColor(80, 85, 95, 60);
                        _strokePaint.StrokeWidth = 0.8f;
                        canvas.DrawLine(px + 3, py + cs * 0.3f, px + cs - 3, py + cs * 0.3f, _strokePaint);
                        canvas.DrawLine(px + 3, py + cs * 0.7f, px + cs - 3, py + cs * 0.7f, _strokePaint);
                        // Bolzen an den Ecken
                        ProceduralTextures.DrawMetalRivets(canvas, _fillPaint, px + 3, py + 3, cs - 6,
                            new SKColor(140, 140, 155), 100);
                        // Rost-Flecken (kräftiger)
                        if (ProceduralTextures.CellRandom(gx, gy, 61) < 0.45f)
                        {
                            float rx = px + ProceduralTextures.CellRandom(gx, gy, 62) * cs * 0.4f + cs * 0.25f;
                            float ry = py + ProceduralTextures.CellRandom(gx, gy, 63) * cs * 0.4f + cs * 0.25f;
                            _fillPaint.Color = new SKColor(160, 80, 30, 55);
                            canvas.DrawOval(rx, ry, cs * 0.12f, cs * 0.1f, _fillPaint);
                        }
                    }
                    else
                    {
                        // Neon: Gelbe Warnstreifen-Glow
                        _strokePaint.Color = new SKColor(255, 200, 0, 25);
                        _strokePaint.StrokeWidth = 1.5f;
                        _strokePaint.MaskFilter = _smallGlow;
                        canvas.DrawLine(px + 4, py + cs - 4, px + cs - 4, py + 4, _strokePaint);
                        _strokePaint.MaskFilter = null;
                        // Nieten-Glow
                        _fillPaint.Color = new SKColor(200, 200, 220, 20);
                        _fillPaint.MaskFilter = _smallGlow;
                        canvas.DrawCircle(px + 5, py + 5, 1.5f, _fillPaint);
                        canvas.DrawCircle(px + cs - 5, py + cs - 5, 1.5f, _fillPaint);
                        _fillPaint.MaskFilter = null;
                    }
                    break;

                case 2: // Cavern: Kristall-Gestein mit farbigen Facetten + Reflexen
                {
                    float rng = ProceduralTextures.CellRandom(gx, gy, 64);
                    // Kristall-Einschluss (immer, nicht nur 40%)
                    byte crystalAlpha = isNeon ? (byte)50 : (byte)70;
                    // Kristall-Farbe variiert pro Zelle (Amethyst, Smaragd, Saphir)
                    int colorIdx = (int)(ProceduralTextures.CellRandom(gx, gy, 74) * 3);
                    var crystalColor = colorIdx switch
                    {
                        0 => new SKColor(180, 100, 240, crystalAlpha),
                        1 => new SKColor(100, 220, 140, crystalAlpha),
                        _ => new SKColor(100, 140, 240, crystalAlpha)
                    };
                    _fillPaint.Color = crystalColor;
                    using (var facet = new SKPath())
                    {
                        float fx = px + rng * cs * 0.25f + cs * 0.2f;
                        facet.MoveTo(fx, py + 4);
                        facet.LineTo(fx + cs * 0.35f, py + cs * 0.45f);
                        facet.LineTo(fx + cs * 0.15f, py + cs * 0.55f);
                        facet.LineTo(fx - cs * 0.1f, py + cs * 0.4f);
                        facet.Close();
                        canvas.DrawPath(facet, _fillPaint);
                    }
                    // Glanz-Highlight auf dem Kristall
                    _fillPaint.Color = new SKColor(255, 255, 255, 40);
                    float gx2 = px + rng * cs * 0.25f + cs * 0.25f;
                    canvas.DrawCircle(gx2, py + cs * 0.25f, 2f, _fillPaint);
                    if (isNeon)
                    {
                        // Neon: Kristall-Glow
                        _fillPaint.Color = crystalColor.WithAlpha(25);
                        _fillPaint.MaskFilter = _smallGlow;
                        canvas.DrawCircle(gx2 + cs * 0.1f, py + cs * 0.35f, cs * 0.15f, _fillPaint);
                        _fillPaint.MaskFilter = null;
                    }
                    // Zweite kleine Kristall-Facette (andere Ecke)
                    if (ProceduralTextures.CellRandom(gx, gy, 75) < 0.5f)
                    {
                        _fillPaint.Color = crystalColor.WithAlpha((byte)(crystalAlpha * 0.6f));
                        using var facet2 = new SKPath();
                        float fx2 = px + cs * 0.55f;
                        facet2.MoveTo(fx2, py + cs * 0.55f);
                        facet2.LineTo(fx2 + cs * 0.2f, py + cs * 0.7f);
                        facet2.LineTo(fx2 + cs * 0.05f, py + cs - 4);
                        facet2.Close();
                        canvas.DrawPath(facet2, _fillPaint);
                    }
                    break;
                }

                case 3: // Sky: Wolken-Blöcke (weiche Kanten, volumetrischer Wolken-Look)
                    if (!isNeon)
                    {
                        // Mehrere überlappende Wolkenformen für 3D-Volumen
                        _fillPaint.Color = new SKColor(255, 255, 255, 35);
                        canvas.DrawOval(px + cs * 0.35f, py + cs * 0.3f, cs * 0.3f, cs * 0.2f, _fillPaint);
                        canvas.DrawOval(px + cs * 0.6f, py + cs * 0.35f, cs * 0.25f, cs * 0.18f, _fillPaint);
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.55f, cs * 0.35f, cs * 0.22f, _fillPaint);
                        // Heller Rand oben (Sonnenlicht)
                        _fillPaint.Color = new SKColor(255, 255, 240, 45);
                        canvas.DrawRect(px + 3, py + 2, cs - 6, 3, _fillPaint);
                        // Weicher dunkler Schatten unten
                        _fillPaint.Color = new SKColor(100, 120, 160, 25);
                        canvas.DrawRect(px + 3, py + cs - 5, cs - 6, 3, _fillPaint);
                    }
                    else
                    {
                        // Neon: Cyan-Wolken-Glow
                        _fillPaint.Color = new SKColor(0, 220, 255, 18);
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.4f, cs * 0.3f, cs * 0.2f, _fillPaint);
                        _strokePaint.Color = new SKColor(0, 200, 255, 20);
                        _strokePaint.StrokeWidth = 0.6f;
                        _strokePaint.MaskFilter = _smallGlow;
                        canvas.DrawRoundRect(px + 3, py + 3, cs - 6, cs - 6, 5, 5, _strokePaint);
                        _strokePaint.MaskFilter = null;
                    }
                    break;

                case 4: // Inferno: Verkohltes Holz mit Glutrissen + Asche
                    if (!isNeon)
                    {
                        // Kohleschicht (dunkler Overlay)
                        _fillPaint.Color = new SKColor(15, 10, 5, 50);
                        canvas.DrawRect(px + 3, py + 3, cs - 6, cs - 6, _fillPaint);
                        // Mehrere Brandspuren (Verkohlungen)
                        _fillPaint.Color = new SKColor(25, 18, 10, 60);
                        float bx = px + ProceduralTextures.CellRandom(gx, gy, 66) * cs * 0.4f + cs * 0.15f;
                        float by = py + ProceduralTextures.CellRandom(gx, gy, 67) * cs * 0.3f + cs * 0.2f;
                        canvas.DrawOval(bx, by, cs * 0.18f, cs * 0.14f, _fillPaint);
                        // Glühende Risse (kräftiger)
                        ProceduralTextures.DrawEmberCracks(canvas, _strokePaint, px + 3, py + 3, cs - 6, gx, gy,
                            _globalTimer, 30);
                        // Zusätzlicher Glutriss quer
                        float pulse = MathF.Sin(_globalTimer * 2f + gx * 0.9f + gy * 1.3f) * 0.3f + 0.7f;
                        _strokePaint.Color = new SKColor(255, 100, 20, (byte)(40 * pulse));
                        _strokePaint.StrokeWidth = 0.8f;
                        _strokePaint.MaskFilter = null;
                        canvas.DrawLine(px + 4, py + cs * 0.6f, px + cs - 4, py + cs * 0.55f, _strokePaint);
                        // Asche-Flecken (graue Punkte)
                        _fillPaint.Color = new SKColor(80, 75, 70, 40);
                        canvas.DrawCircle(px + cs * 0.3f, py + cs * 0.8f, 1.5f, _fillPaint);
                        canvas.DrawCircle(px + cs * 0.7f, py + cs * 0.75f, 1f, _fillPaint);
                    }
                    else
                    {
                        // Neon: Pulsierende rote Glut-Kanten (intensiver)
                        float pulse = MathF.Sin(_globalTimer * 2f + gx * 0.7f + gy * 1.1f) * 0.4f + 0.6f;
                        _strokePaint.Color = new SKColor(255, 60, 0, (byte)(45 * pulse));
                        _strokePaint.StrokeWidth = 1f;
                        _strokePaint.MaskFilter = _smallGlow;
                        canvas.DrawRect(px + 3, py + 3, cs - 6, cs - 6, _strokePaint);
                        // Innere Glut-Linie
                        _strokePaint.Color = new SKColor(255, 120, 0, (byte)(25 * pulse));
                        canvas.DrawLine(px + 5, py + cs * 0.5f, px + cs - 5, py + cs * 0.45f, _strokePaint);
                        _strokePaint.MaskFilter = null;
                    }
                    break;

                case 5: // Ruins: Ziegelwand mit sichtbaren Ziegeln + Efeu-Ranken + Risse
                    if (!isNeon)
                    {
                        // Kräftigere Ziegellinien
                        ProceduralTextures.DrawBrickPattern(canvas, _strokePaint, px + 3, py + 3, cs - 6,
                            new SKColor(150, 110, 80), 30);
                        // Abgebrochene Ecke (zufällig, 30% der Blöcke)
                        if (ProceduralTextures.CellRandom(gx, gy, 76) < 0.3f)
                        {
                            // Dunkles Dreieck in einer Ecke (abgebröckelt)
                            _fillPaint.Color = new SKColor(60, 50, 40, 50);
                            using var chip = new SKPath();
                            chip.MoveTo(px + cs - 3, py + 3);
                            chip.LineTo(px + cs - 3, py + cs * 0.25f);
                            chip.LineTo(px + cs * 0.75f, py + 3);
                            chip.Close();
                            canvas.DrawPath(chip, _fillPaint);
                        }
                        // Efeu/Moos an Kanten (grüne Flecken)
                        if (ProceduralTextures.CellRandom(gx, gy, 77) < 0.4f)
                        {
                            ProceduralTextures.DrawMossPatches(canvas, _fillPaint, px + 2, py + cs * 0.6f, cs - 4,
                                gx, gy, 50);
                        }
                        // Risslinien
                        ProceduralTextures.DrawCracks(canvas, _strokePaint, px + 3, py + 3, cs - 6, gx, gy,
                            new SKColor(100, 80, 60), 40);
                    }
                    else
                    {
                        // Neon: Goldene Ziegel-Umrisse (kräftiger)
                        _strokePaint.Color = new SKColor(255, 200, 80, 25);
                        _strokePaint.StrokeWidth = 0.7f;
                        _strokePaint.MaskFilter = _smallGlow;
                        canvas.DrawLine(px + 4, py + cs * 0.35f, px + cs - 4, py + cs * 0.35f, _strokePaint);
                        canvas.DrawLine(px + 4, py + cs * 0.65f, px + cs - 4, py + cs * 0.65f, _strokePaint);
                        float brickOff = (gx % 2 == 0) ? cs * 0.45f : cs * 0.3f;
                        canvas.DrawLine(px + brickOff, py + 4, px + brickOff, py + cs * 0.35f, _strokePaint);
                        float brickOff2 = (gx % 2 == 0) ? cs * 0.65f : cs * 0.5f;
                        canvas.DrawLine(px + brickOff2, py + cs * 0.35f, px + brickOff2, py + cs * 0.65f, _strokePaint);
                        _strokePaint.MaskFilter = null;
                    }
                    break;

                case 6: // Ocean: Versunkene Truhe mit Metallbändern + Algen + Seepocken
                    if (!isNeon)
                    {
                        // Holzmaserung
                        ProceduralTextures.DrawWoodGrain(canvas, _strokePaint, px + 3, py + 3, cs - 6,
                            gx, gy, new SKColor(80, 70, 50), 20);
                        // Metallband (horizontal)
                        _fillPaint.Color = new SKColor(90, 100, 80, 90);
                        canvas.DrawRect(px + 2, py + cs * 0.4f - 1, cs - 4, 2.5f, _fillPaint);
                        // Metallband Nieten
                        _fillPaint.Color = new SKColor(120, 130, 110, 100);
                        canvas.DrawCircle(px + 5, py + cs * 0.4f, 1f, _fillPaint);
                        canvas.DrawCircle(px + cs - 5, py + cs * 0.4f, 1f, _fillPaint);
                        // Algen-Bewuchs (kräftiger, mehrere)
                        _fillPaint.Color = new SKColor(30, 140, 50, 65);
                        float ax = px + ProceduralTextures.CellRandom(gx, gy, 69) * cs * 0.3f + 4;
                        canvas.DrawOval(ax, py + cs - 4, cs * 0.1f, 3.5f, _fillPaint);
                        canvas.DrawOval(ax + cs * 0.25f, py + cs - 3, cs * 0.08f, 3f, _fillPaint);
                        _fillPaint.Color = new SKColor(40, 160, 60, 50);
                        canvas.DrawOval(ax + cs * 0.4f, py + cs - 5, cs * 0.07f, 4f, _fillPaint);
                        // Seepocken (kleine weiße Kreise)
                        if (ProceduralTextures.CellRandom(gx, gy, 78) < 0.4f)
                        {
                            _fillPaint.Color = new SKColor(200, 200, 190, 50);
                            canvas.DrawCircle(px + cs * 0.7f, py + cs * 0.25f, 1.5f, _fillPaint);
                            canvas.DrawCircle(px + cs * 0.8f, py + cs * 0.3f, 1f, _fillPaint);
                        }
                    }
                    else
                    {
                        // Neon: Cyan Wasser-Reflexion + Algen-Glow
                        _fillPaint.Color = new SKColor(0, 180, 220, 18);
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.55f, cs * 0.25f, cs * 0.12f, _fillPaint);
                        _fillPaint.Color = new SKColor(0, 255, 100, 15);
                        _fillPaint.MaskFilter = _smallGlow;
                        canvas.DrawOval(px + cs * 0.3f, py + cs - 5, cs * 0.08f, 3f, _fillPaint);
                        _fillPaint.MaskFilter = null;
                    }
                    break;

                case 7: // Volcano: Lava-Gestein mit tiefen Rissen + Magma-Glow + Obsidian-Glanz
                    if (!isNeon)
                    {
                        // Tiefe Risse
                        ProceduralTextures.DrawCracks(canvas, _strokePaint, px + 3, py + 3, cs - 6, gx, gy,
                            new SKColor(40, 25, 15), 35);
                        // Magma-Glow in den Rissen (kräftiger, immer sichtbar)
                        float pulse = MathF.Sin(_globalTimer * 1.5f + gx * 0.8f + gy) * 0.3f + 0.7f;
                        _strokePaint.Color = new SKColor(255, 120, 20, (byte)(55 * pulse));
                        _strokePaint.StrokeWidth = 1.2f;
                        _strokePaint.MaskFilter = null;
                        float cx1 = px + cs * 0.25f;
                        float cy1 = py + cs * 0.35f;
                        canvas.DrawLine(cx1, cy1, cx1 + cs * 0.45f, cy1 + cs * 0.35f, _strokePaint);
                        // Zweiter Riss diagonal
                        _strokePaint.Color = new SKColor(255, 80, 10, (byte)(40 * pulse));
                        float cx2 = px + cs * 0.6f;
                        canvas.DrawLine(cx2, py + 4, cx2 - cs * 0.2f, py + cs * 0.4f, _strokePaint);
                        // Obsidian-Glanz (heller Fleck)
                        _fillPaint.Color = new SKColor(255, 255, 255, 20);
                        canvas.DrawCircle(px + cs * 0.35f, py + cs * 0.25f, 2f, _fillPaint);
                    }
                    else
                    {
                        // Neon: Leuchtende Lava-Adern (intensiver)
                        ProceduralTextures.DrawEmberCracks(canvas, _strokePaint, px + 3, py + 3, cs - 6, gx, gy,
                            _globalTimer, 35);
                        // Extra Magma-Glow
                        float pulse = MathF.Sin(_globalTimer * 1.5f + gx * 0.8f + gy) * 0.3f + 0.7f;
                        _fillPaint.Color = new SKColor(255, 80, 0, (byte)(15 * pulse));
                        _fillPaint.MaskFilter = _smallGlow;
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.5f, cs * 0.2f, cs * 0.15f, _fillPaint);
                        _fillPaint.MaskFilter = null;
                    }
                    break;

                case 8: // SkyFortress: Goldverzierte Steinblöcke mit Edelstein + Relief
                    if (!isNeon)
                    {
                        // Goldener Rahmen (kräftiger)
                        _strokePaint.Color = new SKColor(200, 170, 60, 80);
                        _strokePaint.StrokeWidth = 1.2f;
                        _strokePaint.MaskFilter = null;
                        canvas.DrawRoundRect(px + 4, py + 4, cs - 8, cs - 8, 2, 2, _strokePaint);
                        // Goldene Eckpunkte (Nieten)
                        _fillPaint.Color = new SKColor(230, 200, 80, 120);
                        canvas.DrawCircle(px + 5, py + 5, 2f, _fillPaint);
                        canvas.DrawCircle(px + cs - 5, py + 5, 2f, _fillPaint);
                        canvas.DrawCircle(px + 5, py + cs - 5, 2f, _fillPaint);
                        canvas.DrawCircle(px + cs - 5, py + cs - 5, 2f, _fillPaint);
                        // Edelstein in der Mitte (blau oder rot, zufällig)
                        int gemColor = (int)(ProceduralTextures.CellRandom(gx, gy, 79) * 3);
                        var gemClr = gemColor switch
                        {
                            0 => new SKColor(80, 120, 255, 90),  // Saphir
                            1 => new SKColor(220, 60, 60, 90),   // Rubin
                            _ => new SKColor(80, 200, 120, 90)   // Smaragd
                        };
                        _fillPaint.Color = gemClr;
                        using (var gem = new SKPath())
                        {
                            float gcx = px + cs * 0.5f, gcy = py + cs * 0.5f;
                            gem.MoveTo(gcx, gcy - 3);
                            gem.LineTo(gcx + 3, gcy);
                            gem.LineTo(gcx, gcy + 3);
                            gem.LineTo(gcx - 3, gcy);
                            gem.Close();
                            canvas.DrawPath(gem, _fillPaint);
                        }
                        // Glanz auf dem Edelstein
                        _fillPaint.Color = new SKColor(255, 255, 255, 50);
                        canvas.DrawCircle(px + cs * 0.48f, py + cs * 0.47f, 1.2f, _fillPaint);
                    }
                    else
                    {
                        // Neon: Goldener Glüh-Rahmen (kräftiger)
                        float sparkle = MathF.Sin(_globalTimer * 2.5f + gx * 1.3f + gy * 0.9f) * 0.3f + 0.7f;
                        _strokePaint.Color = new SKColor(255, 220, 80, (byte)(35 * sparkle));
                        _strokePaint.StrokeWidth = 1.2f;
                        _strokePaint.MaskFilter = _smallGlow;
                        canvas.DrawRoundRect(px + 4, py + 4, cs - 8, cs - 8, 2, 2, _strokePaint);
                        // Edelstein-Glow in der Mitte
                        _fillPaint.Color = new SKColor(100, 180, 255, (byte)(20 * sparkle));
                        _fillPaint.MaskFilter = _smallGlow;
                        canvas.DrawCircle(px + cs * 0.5f, py + cs * 0.5f, 3f, _fillPaint);
                        _fillPaint.MaskFilter = null;
                        _strokePaint.MaskFilter = null;
                    }
                    break;

                case 9: // ShadowRealm: Schattensubstanz mit lila Glow-Rissen + pulsierendem Auge
                {
                    float pulse = MathF.Sin(_globalTimer * 1.8f + gx * 1.1f + gy * 0.9f) * 0.4f + 0.6f;
                    if (isNeon)
                    {
                        // Pulsierende lila Glow-Risse (intensiver)
                        _strokePaint.Color = new SKColor(180, 40, 255, (byte)(45 * pulse));
                        _strokePaint.StrokeWidth = 1.2f;
                        _strokePaint.MaskFilter = _smallGlow;
                        float crx = px + ProceduralTextures.CellRandom(gx, gy, 72) * cs * 0.3f + cs * 0.2f;
                        canvas.DrawLine(crx, py + 4, crx + cs * 0.3f, py + cs - 4, _strokePaint);
                        // Zweiter Riss
                        float crx2 = px + cs * 0.6f;
                        canvas.DrawLine(crx2, py + cs * 0.3f, crx2 - cs * 0.15f, py + cs * 0.7f, _strokePaint);
                        _strokePaint.MaskFilter = null;
                    }
                    else
                    {
                        // Dunkler pulsierender Kern (intensiver)
                        _fillPaint.Color = new SKColor(30, 10, 50, (byte)(35 * pulse));
                        canvas.DrawRect(px + 3, py + 3, cs - 6, cs - 6, _fillPaint);
                        // Lila Risse (kräftiger, immer sichtbar)
                        _strokePaint.Color = new SKColor(150, 60, 220, (byte)(50 * pulse));
                        _strokePaint.StrokeWidth = 0.8f;
                        _strokePaint.MaskFilter = null;
                        float sx = px + cs * 0.2f;
                        float sy = py + cs * 0.3f;
                        canvas.DrawLine(sx, sy, sx + cs * 0.5f, sy + cs * 0.4f, _strokePaint);
                        canvas.DrawLine(sx + cs * 0.35f, py + 4, sx + cs * 0.2f, sy + cs * 0.15f, _strokePaint);
                        // Pulsierendes Auge (auf 25% der Blöcke)
                        if (ProceduralTextures.CellRandom(gx, gy, 80) < 0.25f)
                        {
                            float eyeX = px + cs * 0.5f, eyeY = py + cs * 0.5f;
                            _fillPaint.Color = new SKColor(160, 40, 200, (byte)(50 * pulse));
                            canvas.DrawOval(eyeX, eyeY, cs * 0.12f, cs * 0.08f, _fillPaint);
                            // Pupille
                            _fillPaint.Color = new SKColor(255, 255, 100, (byte)(40 * pulse));
                            canvas.DrawCircle(eyeX, eyeY, 1.5f, _fillPaint);
                        }
                    }
                    break;
                }
            }
        }
    }

    private void RenderBlockDestruction(SKCanvas canvas, float px, float py, int cs, float progress, bool isNeon)
    {
        _fillPaint.MaskFilter = null;
        float cx = px + cs * 0.5f;
        float cy = py + cs * 0.5f;

        // Phase 1 (0-0.3): Risse erscheinen, Block vibriert
        // Phase 2 (0.3-0.7): Block zerbricht, Fragmente fliegen
        // Phase 3 (0.7-1.0): Fragmente verblassen

        if (progress < 0.3f)
        {
            // Phase 1: Block mit zunehmenden Rissen + Vibration
            float p1 = progress / 0.3f; // 0→1
            float vibrate = MathF.Sin(p1 * 40f) * p1 * 2f;
            byte alpha = 255;

            _fillPaint.Color = _palette.BlockBase.WithAlpha(alpha);
            canvas.DrawRect(px + vibrate, py, cs, cs, _fillPaint);

            // Risse werden stärker
            _strokePaint.Color = new SKColor(40, 30, 20, (byte)(180 * p1));
            _strokePaint.StrokeWidth = 1f + p1;
            // Diagonaler Hauptriss
            canvas.DrawLine(px + cs * 0.2f + vibrate, py + cs * 0.1f,
                px + cs * 0.8f + vibrate, py + cs * 0.9f, _strokePaint);
            // Querriss
            if (p1 > 0.4f)
            {
                canvas.DrawLine(px + cs * 0.1f + vibrate, py + cs * 0.6f,
                    px + cs * 0.7f + vibrate, py + cs * 0.3f, _strokePaint);
            }
            // Dritter Riss
            if (p1 > 0.7f)
            {
                canvas.DrawLine(px + cs * 0.5f + vibrate, py,
                    px + cs * 0.3f + vibrate, py + cs, _strokePaint);
            }
            _strokePaint.StrokeWidth = 1f;
        }
        else if (progress < 0.7f)
        {
            // Phase 2: 4 Fragmente fliegen auseinander + rotieren
            float p2 = (progress - 0.3f) / 0.4f; // 0→1
            byte alpha = (byte)(255 * (1f - p2 * 0.6f));
            float spread = p2 * cs * 0.5f;
            float halfCs = cs * 0.5f;
            float fragSize = halfCs * (1f - p2 * 0.3f);

            // 4 Fragmente (oben-links, oben-rechts, unten-links, unten-rechts)
            float[] dx = { -spread, spread, -spread * 0.8f, spread * 0.8f };
            float[] dy = { -spread, -spread * 0.7f, spread * 0.6f, spread };
            float[] rot = { -p2 * 25f, p2 * 20f, p2 * 15f, -p2 * 30f };

            for (int i = 0; i < 4; i++)
            {
                canvas.Save();
                float fx = cx + dx[i];
                float fy = cy + dy[i];
                canvas.Translate(fx, fy);
                canvas.RotateDegrees(rot[i]);

                _fillPaint.Color = _palette.BlockBase.WithAlpha(alpha);
                canvas.DrawRect(-fragSize * 0.5f, -fragSize * 0.5f, fragSize, fragSize, _fillPaint);

                // Highlight-Kante auf Fragment
                byte hAlpha = (byte)(100 * (1f - p2));
                _fillPaint.Color = _palette.BlockHighlight.WithAlpha(hAlpha);
                canvas.DrawRect(-fragSize * 0.5f, -fragSize * 0.5f, fragSize, 1.5f, _fillPaint);

                canvas.Restore();
            }

            // Staubwolke in der Mitte
            byte dustAlpha = (byte)(60 * (1f - p2));
            _fillPaint.Color = new SKColor(180, 170, 150, dustAlpha);
            float dustR = cs * 0.3f + p2 * cs * 0.4f;
            canvas.DrawOval(cx, cy, dustR, dustR * 0.6f, _fillPaint);
        }
        else
        {
            // Phase 3: Kleine Trümmer verblassen + fallen
            float p3 = (progress - 0.7f) / 0.3f; // 0→1
            byte alpha = (byte)(100 * (1f - p3));
            if (alpha < 5) return;

            float gravity = p3 * cs * 0.4f;
            float spread = cs * 0.5f + p3 * cs * 0.3f;

            // 6 kleine Trümmer-Punkte
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f + 15f;
                float rad = angle * MathF.PI / 180f;
                float tx = cx + MathF.Cos(rad) * spread * (0.5f + i * 0.1f);
                float ty = cy + MathF.Sin(rad) * spread * 0.4f + gravity;
                float size = 2f - p3 * 1.5f;
                if (size < 0.5f) continue;

                _fillPaint.Color = _palette.BlockBase.WithAlpha(alpha);
                canvas.DrawRect(tx - size, ty - size, size * 2, size * 2, _fillPaint);
            }

            // Verblassende Staubwolke
            byte dustAlpha = (byte)(40 * (1f - p3));
            _fillPaint.Color = new SKColor(180, 170, 150, dustAlpha);
            canvas.DrawOval(cx, cy + gravity * 0.3f, cs * 0.5f, cs * 0.2f, _fillPaint);
        }

        if (isNeon)
        {
            // Neon: Energie-Burst bei Zerstörung
            float burstAlpha = progress < 0.5f ? (1f - progress * 2f) : 0f;
            if (burstAlpha > 0.05f)
            {
                _glowPaint.Color = _palette.BlockMortar.WithAlpha((byte)(120 * burstAlpha));
                _glowPaint.MaskFilter = _mediumGlow;
                float burstR = cs * 0.4f + progress * cs * 0.6f;
                canvas.DrawCircle(cx, cy, burstR, _glowPaint);
                _glowPaint.MaskFilter = null;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WELT-MECHANIK-TILES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Eis-Boden: Hellblauer reflektiver Glanz mit Schimmer-Animation</summary>
    private void RenderIceTile(SKCanvas canvas, float px, float py, int cs, int gx, int gy, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        // Basis-Eis-Farbe (Schachbrett-Variation)
        bool alt = (gx + gy) % 2 == 0;
        if (isNeon)
        {
            _fillPaint.Color = alt ? new SKColor(40, 60, 80) : new SKColor(35, 55, 75);
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Neon-Glow-Linien (Riss-Muster)
            _strokePaint.Color = new SKColor(100, 200, 255, 80);
            _strokePaint.StrokeWidth = 0.8f;
            _strokePaint.MaskFilter = _smallGlow;
            canvas.DrawLine(px + 3, py + cs * 0.3f, px + cs - 5, py + cs * 0.6f, _strokePaint);
            canvas.DrawLine(px + cs * 0.4f, py + 2, px + cs * 0.7f, py + cs - 3, _strokePaint);
            _strokePaint.MaskFilter = null;
        }
        else
        {
            _fillPaint.Color = alt ? new SKColor(180, 210, 235) : new SKColor(170, 200, 225);
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Riss-Linien
            _strokePaint.Color = new SKColor(200, 230, 250, 120);
            _strokePaint.StrokeWidth = 0.8f;
            _strokePaint.MaskFilter = null;
            canvas.DrawLine(px + 3, py + cs * 0.3f, px + cs - 5, py + cs * 0.6f, _strokePaint);
            canvas.DrawLine(px + cs * 0.4f, py + 2, px + cs * 0.7f, py + cs - 3, _strokePaint);
        }

        // Wandernder Glanz-Highlight (Lichtreflexion)
        float shimmerX = (MathF.Sin(_globalTimer * 1.5f + gx * 0.5f) * 0.5f + 0.5f) * cs;
        float shimmerY = (MathF.Cos(_globalTimer * 1.2f + gy * 0.7f) * 0.5f + 0.5f) * cs;
        byte shimmerAlpha = isNeon ? (byte)60 : (byte)90;
        _fillPaint.Color = new SKColor(255, 255, 255, shimmerAlpha);
        canvas.DrawCircle(px + shimmerX, py + shimmerY, cs * 0.15f, _fillPaint);

        // Grid-Linie
        _strokePaint.Color = isNeon ? new SKColor(80, 160, 220, 40) : new SKColor(150, 190, 215);
        _strokePaint.StrokeWidth = 0.5f;
        _strokePaint.MaskFilter = null;
        canvas.DrawLine(px, py, px + cs, py, _strokePaint);
        canvas.DrawLine(px, py, px, py + cs, _strokePaint);
    }

    /// <summary>Förderband: Animierte Pfeile in Förderrichtung</summary>
    private void RenderConveyorTile(SKCanvas canvas, float px, float py, int cs, Cell cell, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        // Basis (metallisch-grauer Boden)
        if (isNeon)
        {
            _fillPaint.Color = new SKColor(45, 45, 55);
            canvas.DrawRect(px, py, cs, cs, _fillPaint);
        }
        else
        {
            _fillPaint.Color = new SKColor(160, 165, 175);
            canvas.DrawRect(px, py, cs, cs, _fillPaint);
        }

        // Seitenleisten (metallische Ränder)
        bool horizontal = cell.ConveyorDirection is Models.Entities.Direction.Left or Models.Entities.Direction.Right;
        _fillPaint.Color = isNeon ? new SKColor(60, 60, 75) : new SKColor(130, 135, 145);
        if (horizontal)
        {
            canvas.DrawRect(px, py, cs, 3, _fillPaint);
            canvas.DrawRect(px, py + cs - 3, cs, 3, _fillPaint);
        }
        else
        {
            canvas.DrawRect(px, py, 3, cs, _fillPaint);
            canvas.DrawRect(px + cs - 3, py, 3, cs, _fillPaint);
        }

        // Animierte Pfeil-Chevrons (3 Stück, wandern in Förderrichtung)
        float animOffset = (_globalTimer * 40f) % cs; // Pixel-Offset Animation

        var arrowColor = isNeon ? new SKColor(255, 200, 0, 180) : new SKColor(220, 180, 40, 200);
        _strokePaint.Color = arrowColor;
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;

        float cx = px + cs / 2f;
        float cy = py + cs / 2f;

        for (int i = 0; i < 3; i++)
        {
            float offset = (i * cs / 3f + animOffset) % cs - cs / 2f;
            float chevronSize = cs * 0.2f;

            switch (cell.ConveyorDirection)
            {
                case Models.Entities.Direction.Right:
                    canvas.DrawLine(cx + offset - chevronSize, cy - chevronSize, cx + offset, cy, _strokePaint);
                    canvas.DrawLine(cx + offset, cy, cx + offset - chevronSize, cy + chevronSize, _strokePaint);
                    break;
                case Models.Entities.Direction.Left:
                    canvas.DrawLine(cx - offset + chevronSize, cy - chevronSize, cx - offset, cy, _strokePaint);
                    canvas.DrawLine(cx - offset, cy, cx - offset + chevronSize, cy + chevronSize, _strokePaint);
                    break;
                case Models.Entities.Direction.Down:
                    canvas.DrawLine(cx - chevronSize, cy + offset - chevronSize, cx, cy + offset, _strokePaint);
                    canvas.DrawLine(cx, cy + offset, cx + chevronSize, cy + offset - chevronSize, _strokePaint);
                    break;
                case Models.Entities.Direction.Up:
                    canvas.DrawLine(cx - chevronSize, cy - offset + chevronSize, cx, cy - offset, _strokePaint);
                    canvas.DrawLine(cx, cy - offset, cx + chevronSize, cy - offset + chevronSize, _strokePaint);
                    break;
            }
        }
        _strokePaint.MaskFilter = null;
    }

    /// <summary>Teleporter: Leuchtender pulsierender Ring mit Farb-ID</summary>
    private void RenderTeleporterTile(SKCanvas canvas, float px, float py, int cs, Cell cell, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        // Boden (Basis)
        bool alt = (cell.X + cell.Y) % 2 == 0;
        _fillPaint.Color = alt ? _palette.FloorBase : _palette.FloorAlt;
        canvas.DrawRect(px, py, cs, cs, _fillPaint);

        // Portal-Farbe basierend auf ColorId
        SKColor portalColor = cell.TeleporterColorId switch
        {
            0 => new SKColor(50, 150, 255),  // Blau
            1 => new SKColor(50, 255, 120),  // Grün
            2 => new SKColor(255, 150, 50),  // Orange
            _ => new SKColor(200, 100, 255)  // Lila
        };

        float cx = px + cs / 2f;
        float cy = py + cs / 2f;
        float pulse = MathF.Sin(_globalTimer * 4f + cell.X * 0.5f) * 0.15f + 0.85f;
        float cooldownFade = cell.TeleporterCooldown > 0 ? 0.3f : 1f;

        // Äußerer Glow
        _glowPaint.Color = portalColor.WithAlpha((byte)(80 * pulse * cooldownFade));
        _glowPaint.MaskFilter = _mediumGlow;
        canvas.DrawCircle(cx, cy, cs * 0.45f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Rotierender Ring
        float rotation = _globalTimer * 90f; // 90° pro Sekunde
        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees(rotation);

        // Ring zeichnen (4 Arcs)
        _strokePaint.Color = portalColor.WithAlpha((byte)(220 * cooldownFade));
        _strokePaint.StrokeWidth = 2.5f;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;

        float r = cs * 0.35f * pulse;
        var arcRect = new SKRect(-r, -r, r, r);

        // 3 Arcs für rotierenden Portal-Ring (wiederverwendeter _fusePath)
        _fusePath.Reset();
        _fusePath.AddArc(arcRect, 0, 80);
        canvas.DrawPath(_fusePath, _strokePaint);
        _fusePath.Reset();
        _fusePath.AddArc(arcRect, 120, 80);
        canvas.DrawPath(_fusePath, _strokePaint);
        _fusePath.Reset();
        _fusePath.AddArc(arcRect, 240, 80);
        canvas.DrawPath(_fusePath, _strokePaint);
        _strokePaint.MaskFilter = null;

        canvas.Restore();

        // Innerer Punkt (Kern des Portals)
        _fillPaint.Color = portalColor.WithAlpha((byte)(180 * pulse * cooldownFade));
        _fillPaint.MaskFilter = isNeon ? _smallGlow : null;
        canvas.DrawCircle(cx, cy, cs * 0.1f, _fillPaint);
        _fillPaint.MaskFilter = null;
    }

    /// <summary>Lava-Riss: Pulsierender roter Riss, gefährlich wenn aktiv</summary>
    private void RenderLavaCrackTile(SKCanvas canvas, float px, float py, int cs, Cell cell, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        // Boden (dunkler als normal, vulkanisch)
        _fillPaint.Color = isNeon ? new SKColor(45, 20, 20) : new SKColor(100, 65, 55);
        canvas.DrawRect(px, py, cs, cs, _fillPaint);

        float cx = px + cs / 2f;
        float cy = py + cs / 2f;

        bool isActive = cell.IsLavaCrackActive;
        float timerMod = cell.LavaCrackTimer % 4f;

        // Riss-Muster (immer sichtbar, auch wenn inaktiv)
        byte crackAlpha = isActive ? (byte)255 : (byte)100;
        var crackColor = isActive
            ? (isNeon ? new SKColor(255, 60, 0, crackAlpha) : new SKColor(255, 80, 20, crackAlpha))
            : (isNeon ? new SKColor(200, 80, 40, crackAlpha) : new SKColor(180, 90, 50, crackAlpha));

        _strokePaint.Color = crackColor;
        _strokePaint.StrokeWidth = isActive ? 2.5f : 1.5f;
        _strokePaint.MaskFilter = isActive && isNeon ? _smallGlow : null;

        // Zickzack-Riss
        _fusePath.Reset();
        _fusePath.MoveTo(px + cs * 0.2f, py + 2);
        _fusePath.LineTo(px + cs * 0.45f, py + cs * 0.35f);
        _fusePath.LineTo(px + cs * 0.3f, py + cs * 0.5f);
        _fusePath.LineTo(px + cs * 0.6f, py + cs * 0.65f);
        _fusePath.LineTo(px + cs * 0.5f, py + cs - 2);
        canvas.DrawPath(_fusePath, _strokePaint);

        // Zweiter kleinerer Riss
        _fusePath.Reset();
        _fusePath.MoveTo(px + cs * 0.7f, py + 4);
        _fusePath.LineTo(px + cs * 0.55f, py + cs * 0.4f);
        _fusePath.LineTo(px + cs * 0.8f, py + cs * 0.7f);
        canvas.DrawPath(_fusePath, _strokePaint);
        _strokePaint.MaskFilter = null;

        // Aktiver Zustand: Glühende Lava-Füllung
        if (isActive)
        {
            float intensity = (timerMod - 2.5f) / 1.5f; // 0→1 während aktiver Phase
            byte lavaAlpha = (byte)(120 + 80 * MathF.Sin(_globalTimer * 8f));

            // Roter/orangener Glow über die ganze Zelle
            _glowPaint.Color = isNeon
                ? new SKColor(255, 40, 0, lavaAlpha)
                : new SKColor(255, 100, 20, lavaAlpha);
            _glowPaint.MaskFilter = _smallGlow;
            canvas.DrawRect(px + 2, py + 2, cs - 4, cs - 4, _glowPaint);
            _glowPaint.MaskFilter = null;

            // Gefahren-Indikator: Pulsierendes X in der Mitte
            _strokePaint.Color = new SKColor(255, 255, 200, (byte)(200 * intensity));
            _strokePaint.StrokeWidth = 2f;
            float xSize = cs * 0.15f;
            canvas.DrawLine(cx - xSize, cy - xSize, cx + xSize, cy + xSize, _strokePaint);
            canvas.DrawLine(cx + xSize, cy - xSize, cx - xSize, cy + xSize, _strokePaint);
        }
        else
        {
            // Inaktiv: Schwacher Warn-Glow wenn fast aktiv (timerMod > 2.0)
            if (timerMod > 2.0f)
            {
                float warnIntensity = (timerMod - 2.0f) / 0.5f;
                byte warnAlpha = (byte)(40 * warnIntensity);
                _fillPaint.Color = isNeon
                    ? new SKColor(255, 60, 0, warnAlpha)
                    : new SKColor(255, 100, 20, warnAlpha);
                canvas.DrawRect(px + 2, py + 2, cs - 4, cs - 4, _fillPaint);
            }
        }
    }

    /// <summary>Plattform-Lücke (Welt 9: Himmelsfestung) - dunkle Lücke mit Tiefeneffekt</summary>
    private void RenderPlatformGapTile(SKCanvas canvas, float px, float py, int cs, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        // Dunkler Abgrund
        _fillPaint.Color = isNeon ? new SKColor(10, 5, 20) : new SKColor(20, 15, 25);
        canvas.DrawRect(px, py, cs, cs, _fillPaint);

        // Innerer noch dunklerer Kern (Tiefeneffekt)
        _fillPaint.Color = isNeon ? new SKColor(5, 0, 15) : new SKColor(10, 8, 15);
        canvas.DrawRect(px + 3, py + 3, cs - 6, cs - 6, _fillPaint);

        // Subtile Kanten (heller Rand fuer Kontrast zum Boden)
        _strokePaint.Color = isNeon ? new SKColor(80, 60, 140, 100) : new SKColor(100, 80, 120, 80);
        _strokePaint.StrokeWidth = 1f;
        _strokePaint.MaskFilter = null;
        canvas.DrawRect(px + 1, py + 1, cs - 2, cs - 2, _strokePaint);

        // Warnendes Pulsieren (subtil)
        float pulse = MathF.Sin(_globalTimer * 2f) * 0.3f + 0.5f;
        byte warningAlpha = (byte)(30 * pulse);
        _fillPaint.Color = isNeon
            ? new SKColor(200, 50, 50, warningAlpha)
            : new SKColor(180, 40, 40, warningAlpha);
        canvas.DrawRect(px + 4, py + 4, cs - 8, cs - 8, _fillPaint);
    }
}
