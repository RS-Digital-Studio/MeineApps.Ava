using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// SkiaSharp-Renderer für das Rohrleitungs-Puzzle.
/// Zeichnet Metall-Rohre auf Beton-Kacheln mit progressiver Wasser-Animation,
/// Blasen-Partikeln und Splash-Effekt beim Lösen.
/// </summary>
public class PipePuzzleRenderer
{
    // ═══════════════════════════════════════════════════════════════════════
    // FARBEN
    // ═══════════════════════════════════════════════════════════════════════

    // Kachel-Farben (Betonboden-Optik)
    private static readonly SKColor TileBg = new(0x37, 0x47, 0x4F);
    private static readonly SKColor TileBorder = new(0x26, 0x32, 0x38);
    private static readonly SKColor TileHighlight = new(0x45, 0x55, 0x5E);

    // Rohr-Metall-Farben
    private static readonly SKColor PipeColor = new(0x78, 0x90, 0x9C);
    private static readonly SKColor PipeHighlight = new(0x90, 0xA4, 0xAE);
    private static readonly SKColor PipeShadow = new(0x54, 0x6E, 0x7A);

    // Wasser-Farben
    private static readonly SKColor WaterColor = new(0x29, 0xB6, 0xF6);
    private static readonly SKColor WaterDark = new(0x03, 0x9B, 0xE5);
    private static readonly SKColor WaterLight = new(0x4F, 0xC3, 0xF7);
    private static readonly SKColor WaterDeep = new(0x01, 0x87, 0xBE);

    // Spezial-Indikator-Farben
    private static readonly SKColor SourceColor = new(0x00, 0xE6, 0x76);
    private static readonly SKColor SourceDark = new(0x00, 0xC8, 0x53);
    private static readonly SKColor DrainColor = new(0x44, 0x8A, 0xFF);
    private static readonly SKColor DrainDark = new(0x29, 0x62, 0xFF);
    private static readonly SKColor LockColor = new(0xFF, 0xC1, 0x07);

    private static readonly SKColor BackgroundColor = new(0x1A, 0x23, 0x27);

    // ═══════════════════════════════════════════════════════════════════════
    // ANIMATIONS-ZUSTAND
    // ═══════════════════════════════════════════════════════════════════════

    private float _waterAnimTime;

    // Progressive Wasser-Durchfluss-Animation
    private bool _prevIsSolved;
    private bool _flowStarted;
    private float _flowAnimTime;
    private const float FILL_DELAY = 0.18f;    // Sekunden pro Tile-Distanz
    private const float FILL_DURATION = 0.25f;  // Sekunden zum Füllen eines Tiles

    // Blasen-Partikel im Wasser
    private const int MAX_BUBBLES = 25;
    private readonly WaterBubble[] _bubbles = new WaterBubble[MAX_BUBBLES];

    // Splash-Partikel am Abfluss
    private const int MAX_SPLASH = 16;
    private readonly SplashDrop[] _splashDrops = new SplashDrop[MAX_SPLASH];
    private bool _splashFired;
    private float _splashTime;

    // Gecachte Filter
    private readonly SKMaskFilter _indicatorGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4);
    private readonly SKMaskFilter _waterGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3);

    // ═══════════════════════════════════════════════════════════════════════
    // HAUPT-RENDER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rendert das gesamte Puzzle-Grid mit Wasser-Animation.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, PipeTileData[] tiles, int cols, int rows,
        bool isPuzzleSolved, int maxDistance, float deltaTime)
    {
        _waterAnimTime += deltaTime;

        // Wasser-Durchfluss erkennen (Transition false→true)
        if (isPuzzleSolved && !_prevIsSolved)
        {
            _flowStarted = true;
            _flowAnimTime = 0;
            _splashFired = false;
            _splashTime = 0;
        }
        _prevIsSolved = isPuzzleSolved;

        if (_flowStarted)
            _flowAnimTime += deltaTime;

        // Layout berechnen
        float padding = 12;
        float tileSize = Math.Min(
            (bounds.Width - padding * 2) / cols,
            (bounds.Height - padding * 2) / rows);

        float gridWidth = cols * tileSize;
        float gridHeight = rows * tileSize;
        float startX = bounds.Left + (bounds.Width - gridWidth) / 2;
        float startY = bounds.Top + padding;

        // Hintergrund
        using var bgPaint = new SKPaint { Color = BackgroundColor, IsAntialias = false };
        canvas.DrawRect(bounds, bgPaint);

        // Grid-Schatten
        using var gridShadowPaint = new SKPaint { Color = new SKColor(0x00, 0x00, 0x00, 60), IsAntialias = false };
        canvas.DrawRect(startX + 3, startY + 3, gridWidth, gridHeight, gridShadowPaint);

        // Grid-Kacheln zeichnen
        for (int i = 0; i < tiles.Length && i < cols * rows; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float tx = startX + col * tileSize;
            float ty = startY + row * tileSize;

            float fillProgress = GetTileFillProgress(tiles[i].ConnectionDistance);
            DrawTile(canvas, tx, ty, tileSize, tiles[i], fillProgress);
        }

        // Wasser-Durchfluss-Overlay (Blasen + Splash)
        if (_flowStarted)
        {
            UpdateAndDrawBubbles(canvas, startX, startY, tileSize, tiles, cols, rows, deltaTime);

            // Splash am Abfluss prüfen
            if (!_splashFired)
            {
                for (int i = 0; i < tiles.Length; i++)
                {
                    if (tiles[i].IsDrain && tiles[i].ConnectionDistance >= 0)
                    {
                        float drainFillEnd = tiles[i].ConnectionDistance * FILL_DELAY + FILL_DURATION;
                        if (_flowAnimTime >= drainFillEnd)
                        {
                            int col = i % cols;
                            int row = i / cols;
                            FireSplash(startX + col * tileSize + tileSize / 2,
                                       startY + row * tileSize + tileSize / 2, tileSize);
                            _splashFired = true;
                        }
                    }
                }
            }

            if (_splashFired)
            {
                _splashTime += deltaTime;
                UpdateAndDrawSplash(canvas, deltaTime);
            }

            // Komplett-Glow wenn alle Tiles gefüllt
            float totalFillTime = maxDistance * FILL_DELAY + FILL_DURATION + 0.5f;
            if (_flowAnimTime > totalFillTime)
            {
                DrawCompletionGlow(canvas, startX, startY, gridWidth, gridHeight);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HIT-TEST
    // ═══════════════════════════════════════════════════════════════════════

    public int HitTest(SKRect bounds, float touchX, float touchY, int cols, int rows)
    {
        float padding = 12;
        float tileSize = Math.Min(
            (bounds.Width - padding * 2) / cols,
            (bounds.Height - padding * 2) / rows);

        float gridWidth = cols * tileSize;
        float startX = bounds.Left + (bounds.Width - gridWidth) / 2;
        float startY = bounds.Top + padding;

        if (touchX < startX || touchX >= startX + gridWidth) return -1;
        if (touchY < startY || touchY >= startY + rows * tileSize) return -1;

        int col = (int)((touchX - startX) / tileSize);
        int row = (int)((touchY - startY) / tileSize);

        if (col < 0 || col >= cols || row < 0 || row >= rows) return -1;
        return row * cols + col;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TILE RENDERING
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawTile(SKCanvas canvas, float x, float y, float size, PipeTileData tile, float fillProgress)
    {
        float margin = 2;
        float innerSize = size - margin * 2;
        float innerX = x + margin;
        float innerY = y + margin;

        // Kachel-Hintergrund (Betonboden)
        using var tilePaint = new SKPaint { Color = TileBg, IsAntialias = false };
        canvas.DrawRect(innerX, innerY, innerSize, innerSize, tilePaint);

        // Leichter Highlight-Streifen oben (3D-Effekt)
        using var highlightPaint = new SKPaint { Color = TileHighlight, IsAntialias = false };
        canvas.DrawRect(innerX, innerY, innerSize, 2, highlightPaint);

        // Kachel-Rand (blau bei verbunden)
        bool hasWater = tile.IsConnected || fillProgress > 0;
        using var borderPaint = new SKPaint
        {
            Color = hasWater ? WaterDark.WithAlpha(120) : TileBorder,
            IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1
        };
        canvas.DrawRect(innerX, innerY, innerSize, innerSize, borderPaint);

        // Rohre zeichnen
        var openings = GetOpenings(tile.PipeType, tile.Rotation);
        float center = innerSize / 2;
        float pipeWidth = innerSize * 0.28f;
        float halfPipe = pipeWidth / 2;

        // Rohr-Segmente: Bei Wasser-Durchfluss blaue Farbe
        bool useWaterColor = hasWater || fillProgress > 0;
        foreach (var dir in openings)
        {
            DrawPipeSegment(canvas, innerX, innerY, innerSize, center, halfPipe, dir, useWaterColor);
        }

        // Rohr-Mitte
        if (openings.Length >= 2)
        {
            DrawPipeCenter(canvas, innerX, innerY, center, halfPipe, useWaterColor);
        }

        // Wasser-Füll-Overlay bei progressivem Durchfluss
        if (fillProgress > 0 && fillProgress < 1)
        {
            DrawWaterFillFrontier(canvas, innerX, innerY, innerSize, center, halfPipe, openings, fillProgress);
        }

        // Source/Drain/Lock-Indikatoren
        if (tile.IsSource) DrawSourceIndicator(canvas, innerX, innerY, innerSize, center);
        if (tile.IsDrain) DrawDrainIndicator(canvas, innerX, innerY, innerSize, center, fillProgress);
        if (tile.IsLocked && !tile.IsSource && !tile.IsDrain) DrawLockIndicator(canvas, innerX, innerY, innerSize);

        // Subtiler Wasser-Puls auf verbundenen Rohren (während Gameplay)
        if (tile.IsConnected && !_flowStarted)
        {
            DrawWaterPulse(canvas, innerX, innerY, innerSize, x);
        }
    }

    /// <summary>
    /// Zeichnet die Wasser-Front am Rand des sich füllenden Tiles.
    /// Glühender weißer Streifen der sich durch das Rohr bewegt.
    /// </summary>
    private void DrawWaterFillFrontier(SKCanvas canvas, float tileX, float tileY, float tileSize,
        float center, float halfPipe, int[] openings, float fillProgress)
    {
        // Leuchtende Wasser-Front
        float frontierAlpha = 0.4f + 0.3f * MathF.Sin(_waterAnimTime * 8.0f);
        byte alpha = (byte)(frontierAlpha * fillProgress * 255);

        using var frontierPaint = new SKPaint
        {
            Color = WaterLight.WithAlpha(alpha),
            IsAntialias = true,
            MaskFilter = _waterGlow
        };

        float cx = tileX + center;
        float cy = tileY + center;

        // Wasser-Front als leuchtender Kreis in der Mitte
        float radius = halfPipe * 1.5f * fillProgress;
        canvas.DrawCircle(cx, cy, radius, frontierPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ROHR-SEGMENTE
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawPipeSegment(SKCanvas canvas, float tileX, float tileY, float tileSize,
        float center, float halfPipe, int direction, bool waterColor)
    {
        var mainColor = waterColor ? WaterColor : PipeColor;
        var shadowColor = waterColor ? WaterDark : PipeShadow;
        var lightColor = waterColor ? WaterLight : PipeHighlight;

        using var pipePaint = new SKPaint { Color = mainColor, IsAntialias = false };
        using var shadowPaint = new SKPaint { Color = shadowColor, IsAntialias = false };
        using var lightPaint = new SKPaint { Color = lightColor.WithAlpha(100), IsAntialias = false };

        switch (direction)
        {
            case 0: // Oben
                canvas.DrawRect(tileX + center - halfPipe, tileY, halfPipe * 2, center + halfPipe, pipePaint);
                canvas.DrawRect(tileX + center - halfPipe, tileY, 2, center + halfPipe, shadowPaint);
                canvas.DrawRect(tileX + center + halfPipe - 2, tileY, 2, center + halfPipe, lightPaint);
                canvas.DrawRect(tileX + center - halfPipe - 1, tileY, halfPipe * 2 + 2, 3, shadowPaint);
                break;
            case 1: // Unten
                canvas.DrawRect(tileX + center - halfPipe, tileY + center - halfPipe, halfPipe * 2, center + halfPipe, pipePaint);
                canvas.DrawRect(tileX + center - halfPipe, tileY + center - halfPipe, 2, center + halfPipe, shadowPaint);
                canvas.DrawRect(tileX + center + halfPipe - 2, tileY + center - halfPipe, 2, center + halfPipe, lightPaint);
                canvas.DrawRect(tileX + center - halfPipe - 1, tileY + tileSize - 3, halfPipe * 2 + 2, 3, shadowPaint);
                break;
            case 2: // Links
                canvas.DrawRect(tileX, tileY + center - halfPipe, center + halfPipe, halfPipe * 2, pipePaint);
                canvas.DrawRect(tileX, tileY + center - halfPipe, center + halfPipe, 2, shadowPaint);
                canvas.DrawRect(tileX, tileY + center + halfPipe - 2, center + halfPipe, 2, lightPaint);
                canvas.DrawRect(tileX, tileY + center - halfPipe - 1, 3, halfPipe * 2 + 2, shadowPaint);
                break;
            case 3: // Rechts
                canvas.DrawRect(tileX + center - halfPipe, tileY + center - halfPipe, center + halfPipe, halfPipe * 2, pipePaint);
                canvas.DrawRect(tileX + center - halfPipe, tileY + center - halfPipe, center + halfPipe, 2, shadowPaint);
                canvas.DrawRect(tileX + center - halfPipe, tileY + center + halfPipe - 2, center + halfPipe, 2, lightPaint);
                canvas.DrawRect(tileX + tileSize - 3, tileY + center - halfPipe - 1, 3, halfPipe * 2 + 2, shadowPaint);
                break;
        }
    }

    private static void DrawPipeCenter(SKCanvas canvas, float tileX, float tileY, float center, float halfPipe,
        bool waterColor)
    {
        var mainColor = waterColor ? WaterColor : PipeColor;
        var shadowColor = waterColor ? WaterDark : PipeShadow;
        var lightColor = waterColor ? WaterLight : PipeHighlight;

        using var centerPaint = new SKPaint { Color = mainColor, IsAntialias = false };
        canvas.DrawRect(tileX + center - halfPipe, tileY + center - halfPipe, halfPipe * 2, halfPipe * 2, centerPaint);

        // Niet-Detail in der Mitte
        using var nietPaint = new SKPaint { Color = shadowColor, IsAntialias = false };
        float nietSize = Math.Max(2, halfPipe * 0.35f);
        canvas.DrawRect(tileX + center - nietSize / 2, tileY + center - nietSize / 2, nietSize, nietSize, nietPaint);

        // Highlight oben
        using var lightPaint = new SKPaint { Color = lightColor.WithAlpha(80), IsAntialias = false };
        canvas.DrawRect(tileX + center - halfPipe, tileY + center - halfPipe, halfPipe * 2, 2, lightPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // QUELL-/ABFLUSS-INDIKATOREN
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawSourceIndicator(SKCanvas canvas, float tileX, float tileY, float tileSize, float center)
    {
        float cx = tileX + center;
        float cy = tileY + center;

        DrawIndicatorGlow(canvas, tileX, tileY, tileSize, SourceColor, 0f);

        using var tileBorderPaint = new SKPaint
        {
            Color = SourceColor, IsAntialias = true,
            Style = SKPaintStyle.Stroke, StrokeWidth = 3
        };
        canvas.DrawRect(tileX + 1, tileY + 1, tileSize - 2, tileSize - 2, tileBorderPaint);

        float iconRadius = tileSize * 0.25f;
        using var bgPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy), iconRadius,
                [SourceColor.WithAlpha(220), SourceDark.WithAlpha(200)],
                SKShaderTileMode.Clamp)
        };
        canvas.DrawCircle(cx, cy, iconRadius, bgPaint);

        // Wassertropfen-Symbol
        float dropH = iconRadius * 1.2f;
        float dropW = iconRadius * 0.7f;
        using var dropPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var dropPath = new SKPath();
        dropPath.MoveTo(cx, cy - dropH * 0.5f);
        dropPath.CubicTo(cx - dropW, cy, cx - dropW * 0.6f, cy + dropH * 0.4f, cx, cy + dropH * 0.35f);
        dropPath.CubicTo(cx + dropW * 0.6f, cy + dropH * 0.4f, cx + dropW, cy, cx, cy - dropH * 0.5f);
        dropPath.Close();
        canvas.DrawPath(dropPath, dropPaint);

        using var glanzPaint = new SKPaint { Color = SourceColor.WithAlpha(180), IsAntialias = true };
        canvas.DrawCircle(cx - dropW * 0.2f, cy - dropH * 0.1f, dropW * 0.2f, glanzPaint);

        DrawFlowArrows(canvas, tileX, tileY, tileSize, center, SourceColor, true);
    }

    private void DrawDrainIndicator(SKCanvas canvas, float tileX, float tileY, float tileSize, float center,
        float fillProgress)
    {
        float cx = tileX + center;
        float cy = tileY + center;

        DrawIndicatorGlow(canvas, tileX, tileY, tileSize, DrainColor, MathF.PI);

        using var tileBorderPaint = new SKPaint
        {
            Color = DrainColor, IsAntialias = true,
            Style = SKPaintStyle.Stroke, StrokeWidth = 3
        };
        canvas.DrawRect(tileX + 1, tileY + 1, tileSize - 2, tileSize - 2, tileBorderPaint);

        float iconRadius = tileSize * 0.25f;
        using var bgPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy), iconRadius,
                [DrainColor.WithAlpha(220), DrainDark.WithAlpha(200)],
                SKShaderTileMode.Clamp)
        };
        canvas.DrawCircle(cx, cy, iconRadius, bgPaint);

        // Trichter-Symbol
        float funnelW = iconRadius * 0.8f;
        float funnelH = iconRadius * 0.9f;
        using var funnelPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var funnelPath = new SKPath();
        funnelPath.MoveTo(cx - funnelW, cy - funnelH * 0.4f);
        funnelPath.LineTo(cx + funnelW, cy - funnelH * 0.4f);
        funnelPath.LineTo(cx + funnelW * 0.25f, cy + funnelH * 0.2f);
        funnelPath.LineTo(cx + funnelW * 0.25f, cy + funnelH * 0.4f);
        funnelPath.LineTo(cx - funnelW * 0.25f, cy + funnelH * 0.4f);
        funnelPath.LineTo(cx - funnelW * 0.25f, cy + funnelH * 0.2f);
        funnelPath.Close();
        canvas.DrawPath(funnelPath, funnelPaint);
        canvas.DrawCircle(cx, cy + funnelH * 0.55f, funnelW * 0.2f, funnelPaint);

        // Wenn Wasser angekommen: Wellen-Ringe am Abfluss
        if (fillProgress >= 1.0f)
        {
            float wave = _waterAnimTime * 2.0f;
            for (int w = 0; w < 3; w++)
            {
                float waveProgress = (wave + w * 0.8f) % 2.4f;
                if (waveProgress > 1.5f) continue;
                float waveRadius = iconRadius * (0.8f + waveProgress * 0.6f);
                byte waveAlpha = (byte)(Math.Max(0, 1.0f - waveProgress / 1.5f) * 100);
                using var wavePaint = new SKPaint
                {
                    Color = WaterLight.WithAlpha(waveAlpha),
                    IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2
                };
                canvas.DrawCircle(cx, cy, waveRadius, wavePaint);
            }
        }

        DrawFlowArrows(canvas, tileX, tileY, tileSize, center, DrainColor, false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INDIKATOREN + EFFEKTE
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawIndicatorGlow(SKCanvas canvas, float tileX, float tileY, float tileSize,
        SKColor color, float phaseOffset)
    {
        float pulse = 0.16f + 0.3f * MathF.Sin(_waterAnimTime * 3.0f + phaseOffset);
        byte alpha = (byte)(pulse * 255);

        using var glowPaint = new SKPaint
        {
            Color = color.WithAlpha(alpha), IsAntialias = true, MaskFilter = _indicatorGlow
        };
        canvas.DrawRect(tileX - 2, tileY - 2, tileSize + 4, tileSize + 4, glowPaint);
    }

    private void DrawFlowArrows(SKCanvas canvas, float tileX, float tileY, float tileSize,
        float center, SKColor color, bool outward)
    {
        float arrowAlpha = 0.4f + 0.4f * MathF.Sin(_waterAnimTime * 4.0f);
        byte alpha = (byte)(arrowAlpha * 255);
        float arrowSize = tileSize * 0.1f;
        float offset = tileSize * 0.08f;
        float cx = tileX + center;
        float cy = tileY + center;

        using var arrowPaint = new SKPaint
        {
            Color = color.WithAlpha(alpha), IsAntialias = true, Style = SKPaintStyle.Fill
        };

        float[][] positions =
        [
            [cx, tileY + offset], [cx, tileY + tileSize - offset],
            [tileX + offset, cy], [tileX + tileSize - offset, cy]
        ];
        float[][] dirs =
        [
            [0, outward ? -1 : 1], [0, outward ? 1 : -1],
            [outward ? -1 : 1, 0], [outward ? 1 : -1, 0]
        ];

        for (int i = 0; i < 4; i++)
        {
            float px = positions[i][0], py = positions[i][1];
            float dx = dirs[i][0], dy = dirs[i][1];
            using var path = new SKPath();
            path.MoveTo(px + dx * arrowSize, py + dy * arrowSize);
            path.LineTo(px - dy * arrowSize * 0.5f, py + dx * arrowSize * 0.5f);
            path.LineTo(px + dy * arrowSize * 0.5f, py - dx * arrowSize * 0.5f);
            path.Close();
            canvas.DrawPath(path, arrowPaint);
        }
    }

    private static void DrawLockIndicator(SKCanvas canvas, float tileX, float tileY, float tileSize)
    {
        float lockSize = 8;
        float lockX = tileX + tileSize - lockSize - 3;
        float lockY = tileY + 3;

        using var lockPaint = new SKPaint { Color = LockColor.WithAlpha(180), IsAntialias = false };
        canvas.DrawRect(lockX, lockY + 3, lockSize, lockSize - 3, lockPaint);

        using var buegelPaint = new SKPaint
        {
            Color = LockColor.WithAlpha(180), IsAntialias = false,
            Style = SKPaintStyle.Stroke, StrokeWidth = 2
        };
        canvas.DrawRect(lockX + 2, lockY, lockSize - 4, 4, buegelPaint);
    }

    private void DrawWaterPulse(SKCanvas canvas, float tileX, float tileY, float tileSize, float posX)
    {
        float pulse = 0.15f + 0.15f * MathF.Sin(_waterAnimTime * 3.5f + posX * 0.08f);
        byte alpha = (byte)(pulse * 255);

        using var waterGlow = new SKPaint { Color = WaterColor.WithAlpha(alpha), IsAntialias = false };
        canvas.DrawRect(tileX + 1, tileY + 1, tileSize - 2, tileSize - 2, waterGlow);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PROGRESSIVE WASSER-FÜLLUNG
    // ═══════════════════════════════════════════════════════════════════════

    private float GetTileFillProgress(int connectionDistance)
    {
        if (!_flowStarted || connectionDistance < 0) return 0;
        float fillStart = connectionDistance * FILL_DELAY;
        return Math.Clamp((_flowAnimTime - fillStart) / FILL_DURATION, 0, 1);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BLASEN-PARTIKEL
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateAndDrawBubbles(SKCanvas canvas, float startX, float startY, float tileSize,
        PipeTileData[] tiles, int cols, int rows, float deltaTime)
    {
        // Neue Blasen spawnen an gefüllten Tiles
        if (_flowAnimTime > 0.3f)
        {
            SpawnBubbles(startX, startY, tileSize, tiles, cols, rows);
        }

        using var bubblePaint = new SKPaint { IsAntialias = true };

        for (int i = 0; i < MAX_BUBBLES; i++)
        {
            ref var b = ref _bubbles[i];
            if (!b.Active) continue;

            b.Life += deltaTime;
            if (b.Life >= b.MaxLife)
            {
                b.Active = false;
                continue;
            }

            // Bewegung mit leichtem Wobble
            b.X += b.VX * deltaTime;
            b.Y += b.VY * deltaTime;
            b.X += MathF.Sin(b.Life * 6.0f + b.Y * 0.1f) * 0.3f; // Sinus-Wobble

            // Alpha: Fade-In + Fade-Out
            float lifeRatio = b.Life / b.MaxLife;
            float alpha;
            if (lifeRatio < 0.1f)
                alpha = lifeRatio / 0.1f;
            else if (lifeRatio > 0.7f)
                alpha = (1.0f - lifeRatio) / 0.3f;
            else
                alpha = 1.0f;

            bubblePaint.Color = WaterLight.WithAlpha((byte)(alpha * 180));
            canvas.DrawCircle(b.X, b.Y, b.Size, bubblePaint);

            // Kleiner Glanz-Punkt oben links
            if (b.Size > 2.5f)
            {
                bubblePaint.Color = SKColors.White.WithAlpha((byte)(alpha * 120));
                canvas.DrawCircle(b.X - b.Size * 0.25f, b.Y - b.Size * 0.25f, b.Size * 0.3f, bubblePaint);
            }
        }
    }

    private void SpawnBubbles(float startX, float startY, float tileSize,
        PipeTileData[] tiles, int cols, int rows)
    {
        // Spawn-Rate: ~2 Blasen pro Sekunde
        int spawnHash = (int)(_flowAnimTime * 40) ^ (int)(_flowAnimTime * 17);
        if (spawnHash % 3 != 0) return;

        // Zufälliges gefülltes Tile wählen
        int seed = (int)(_flowAnimTime * 1000);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int idx = ((seed + attempt * 7) & 0x7FFFFFFF) % tiles.Length;
            if (tiles[idx].ConnectionDistance < 0) continue;

            float fillProg = GetTileFillProgress(tiles[idx].ConnectionDistance);
            if (fillProg < 0.5f) continue;

            int col = idx % cols;
            int row = idx / cols;
            float cx = startX + col * tileSize + tileSize / 2;
            float cy = startY + row * tileSize + tileSize / 2;

            // Freien Slot finden
            for (int i = 0; i < MAX_BUBBLES; i++)
            {
                if (_bubbles[i].Active) continue;

                // Deterministischer Pseudo-Random basierend auf Zeit + Index
                float hash1 = MathF.Sin(seed * 0.1f + i * 3.14f);
                float hash2 = MathF.Cos(seed * 0.07f + i * 2.71f);

                _bubbles[i] = new WaterBubble
                {
                    X = cx + hash1 * tileSize * 0.2f,
                    Y = cy + hash2 * tileSize * 0.2f,
                    VX = hash1 * 8,
                    VY = -5 - MathF.Abs(hash2) * 10, // Blasen steigen auf
                    Size = 1.5f + MathF.Abs(hash1) * 2.5f,
                    Life = 0,
                    MaxLife = 0.8f + MathF.Abs(hash2) * 0.8f,
                    Active = true
                };
                break;
            }
            break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPLASH AM ABFLUSS
    // ═══════════════════════════════════════════════════════════════════════

    private void FireSplash(float cx, float cy, float tileSize)
    {
        for (int i = 0; i < MAX_SPLASH; i++)
        {
            float angle = i * (MathF.PI * 2 / MAX_SPLASH) + MathF.Sin(i * 1.3f) * 0.3f;
            float speed = 30 + MathF.Abs(MathF.Sin(i * 2.7f)) * 40;

            _splashDrops[i] = new SplashDrop
            {
                X = cx,
                Y = cy,
                VX = MathF.Cos(angle) * speed,
                VY = MathF.Sin(angle) * speed - 20, // Leicht nach oben
                Size = 2.0f + MathF.Abs(MathF.Sin(i * 1.5f)) * 3.0f,
                Life = 0,
                MaxLife = 0.6f + MathF.Abs(MathF.Cos(i * 1.8f)) * 0.4f,
                Active = true
            };
        }
    }

    private void UpdateAndDrawSplash(SKCanvas canvas, float deltaTime)
    {
        using var splashPaint = new SKPaint { IsAntialias = true };

        for (int i = 0; i < MAX_SPLASH; i++)
        {
            ref var d = ref _splashDrops[i];
            if (!d.Active) continue;

            d.Life += deltaTime;
            if (d.Life >= d.MaxLife)
            {
                d.Active = false;
                continue;
            }

            d.X += d.VX * deltaTime;
            d.Y += d.VY * deltaTime;
            d.VY += 60 * deltaTime; // Schwerkraft

            float lifeRatio = d.Life / d.MaxLife;
            float alpha = 1.0f - lifeRatio;
            float size = d.Size * (1.0f - lifeRatio * 0.5f);

            // Wasser-Tropfen: Blau mit hellem Kern
            splashPaint.Color = WaterColor.WithAlpha((byte)(alpha * 200));
            canvas.DrawCircle(d.X, d.Y, size, splashPaint);

            splashPaint.Color = WaterLight.WithAlpha((byte)(alpha * 120));
            canvas.DrawCircle(d.X - size * 0.2f, d.Y - size * 0.2f, size * 0.4f, splashPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KOMPLETT-EFFEKT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Leuchtender Glow um das gesamte Grid wenn alle Rohre gefüllt sind.
    /// </summary>
    private void DrawCompletionGlow(SKCanvas canvas, float startX, float startY, float gridWidth, float gridHeight)
    {
        float pulse = 0.08f + 0.06f * MathF.Sin(_waterAnimTime * 2.5f);
        byte alpha = (byte)(pulse * 255);

        using var glowPaint = new SKPaint
        {
            Color = WaterColor.WithAlpha(alpha),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8)
        };
        canvas.DrawRect(startX - 4, startY - 4, gridWidth + 8, gridHeight + 8, glowPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ROHR-ÖFFNUNGEN
    // ═══════════════════════════════════════════════════════════════════════

    private static int[] GetOpenings(int pipeType, int rotation)
    {
        int[] baseOpenings = pipeType switch
        {
            0 => [2, 3],          // Straight: Links, Rechts
            1 => [3, 1],          // Corner: Rechts, Unten
            2 => [3, 1, 2],       // TJunction: Rechts, Unten, Links
            3 => [0, 1, 2, 3],    // Cross: Alle 4
            _ => [2, 3]
        };

        int steps = (rotation / 90) % 4;
        if (steps == 0) return baseOpenings;

        var rotated = new int[baseOpenings.Length];
        for (int i = 0; i < baseOpenings.Length; i++)
        {
            int d = baseOpenings[i];
            for (int s = 0; s < steps; s++)
            {
                d = d switch
                {
                    0 => 3, 3 => 1, 1 => 2, 2 => 0, _ => d
                };
            }
            rotated[i] = d;
        }
        return rotated;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARTIKEL-STRUCTS
    // ═══════════════════════════════════════════════════════════════════════

    private struct WaterBubble
    {
        public float X, Y, VX, VY, Size, Life, MaxLife;
        public bool Active;
    }

    private struct SplashDrop
    {
        public float X, Y, VX, VY, Size, Life, MaxLife;
        public bool Active;
    }
}

/// <summary>
/// Vereinfachte Tile-Daten für den Renderer.
/// Wird aus dem ViewModel (PipeTile) befüllt.
/// </summary>
public struct PipeTileData
{
    /// <summary>0=Straight, 1=Corner, 2=TJunction, 3=Cross</summary>
    public int PipeType;
    /// <summary>Rotation in Grad: 0, 90, 180, 270</summary>
    public int Rotation;
    /// <summary>Ist dies die Wasserquelle?</summary>
    public bool IsSource;
    /// <summary>Ist dies der Abfluss/das Ziel?</summary>
    public bool IsDrain;
    /// <summary>Ist die Kachel gesperrt (nicht drehbar)?</summary>
    public bool IsLocked;
    /// <summary>Ist die Kachel mit der Quelle verbunden (Wasser fließt)?</summary>
    public bool IsConnected;
    /// <summary>BFS-Distanz von der Quelle (-1 wenn nicht verbunden). Für progressive Animation.</summary>
    public int ConnectionDistance;
}
