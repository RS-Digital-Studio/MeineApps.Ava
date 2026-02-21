using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Prozedurale Material-Texturen fuer das Handwerker-Theme.
/// Alle Methoden sind statisch und deterministisch (gleiche Eingabe = gleiches Ergebnis).
/// SKPaint-Objekte sind gecacht als static readonly Felder (keine GC-Allokationen im Render-Loop).
/// </summary>
public static class CraftTextures
{
    // --- Gecachte SKPaint-Objekte (GC-frei) ---

    private static readonly SKPaint _fillPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint _strokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f
    };

    private static readonly SKPaint _grainPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.5f
    };

    private static readonly SKPaint _thinStrokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f
    };

    private static readonly SKPaint _stitchPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.5f,
        PathEffect = SKPathEffect.CreateDash([2f, 3f], 0)
    };

    private static readonly SKPaint _crackPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round
    };

    // Gecachter MaskFilter fuer Stein-Schatten (lazy erstellt)
    private static SKMaskFilter? _stoneShadowFilter;
    private static SKMaskFilter StoneShadowFilter =>
        _stoneShadowFilter ??= SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 1.5f);

    // --- Deterministische Pseudo-Zufallszahl basierend auf Seed ---

    /// <summary>
    /// Deterministischer Hash fuer Zufalls-Positionen.
    /// Gibt einen Wert zwischen 0.0 und 1.0 zurueck.
    /// </summary>
    private static float DeterministicRandom(int seed, int index)
    {
        int hash = seed * 374761393 + index * 668265263;
        hash = (hash ^ (hash >> 13)) * 1274126177;
        hash ^= hash >> 16;
        return (hash & 0x7FFFFFFF) / (float)int.MaxValue;
    }

    /// <summary>
    /// Deterministischer Seed aus einer SKRect (stabile Position).
    /// </summary>
    private static int RectSeed(SKRect rect)
    {
        int x = (int)(rect.Left * 100);
        int y = (int)(rect.Top * 100);
        int w = (int)(rect.Width * 100);
        int h = (int)(rect.Height * 100);
        return x ^ (y * 397) ^ (w * 7919) ^ (h * 104729);
    }

    // --- Hilfsmethoden ---

    private static SKColor Lighten(SKColor color, float amount)
    {
        byte r = (byte)Math.Min(255, color.Red + (255 - color.Red) * amount);
        byte g = (byte)Math.Min(255, color.Green + (255 - color.Green) * amount);
        byte b = (byte)Math.Min(255, color.Blue + (255 - color.Blue) * amount);
        return new SKColor(r, g, b, color.Alpha);
    }

    private static SKColor Darken(SKColor color, float amount)
    {
        byte r = (byte)(color.Red * (1f - amount));
        byte g = (byte)(color.Green * (1f - amount));
        byte b = (byte)(color.Blue * (1f - amount));
        return new SKColor(r, g, b, color.Alpha);
    }

    // ========================================================================
    // 1. HOLZ-MASERUNG
    // ========================================================================

    /// <summary>
    /// Zeichnet eine Holz-Maserung mit horizontalen Streifen, Astloechern und vertikalem Gradient.
    /// </summary>
    public static void DrawWoodGrain(SKCanvas canvas, SKRect rect, SKColor baseColor, float grainIntensity = 0.5f)
    {
        canvas.Save();
        canvas.ClipRect(rect);

        int seed = RectSeed(rect);

        // Vertikaler Gradient (oben heller, unten dunkler)
        var topColor = Lighten(baseColor, 0.15f);
        var bottomColor = Darken(baseColor, 0.15f);

        using var gradientShader = SKShader.CreateLinearGradient(
            new SKPoint(rect.Left, rect.Top),
            new SKPoint(rect.Left, rect.Bottom),
            [topColor, bottomColor],
            SKShaderTileMode.Clamp);

        _fillPaint.Shader = gradientShader;
        canvas.DrawRect(rect, _fillPaint);
        _fillPaint.Shader = null;

        // 8-12 Maserungslinien (wellenfoermig mit Sinus)
        int lineCount = 8 + (int)(DeterministicRandom(seed, 0) * 5);
        float lineSpacing = rect.Height / (lineCount + 1);
        byte grainAlpha = (byte)(40 + grainIntensity * 80);

        _grainPaint.Color = Darken(baseColor, 0.25f).WithAlpha(grainAlpha);

        for (int i = 0; i < lineCount; i++)
        {
            float baseY = rect.Top + lineSpacing * (i + 1);
            float yOffset = (DeterministicRandom(seed, i + 10) - 0.5f) * lineSpacing * 0.3f;
            float amplitude = 1.5f + DeterministicRandom(seed, i + 20) * 2.5f;
            float frequency = 0.02f + DeterministicRandom(seed, i + 30) * 0.015f;

            using var path = new SKPath();
            path.MoveTo(rect.Left, baseY + yOffset);

            for (float x = rect.Left + 2; x <= rect.Right; x += 3)
            {
                float waveY = baseY + yOffset + MathF.Sin((x - rect.Left) * frequency + i * 0.7f) * amplitude;
                path.LineTo(x, waveY);
            }

            _grainPaint.StrokeWidth = 0.8f + DeterministicRandom(seed, i + 40) * 1.2f;
            canvas.DrawPath(path, _grainPaint);
        }

        // 2-3 Astloecher (dunkle Ovale)
        int knotCount = 2 + (int)(DeterministicRandom(seed, 100) * 2);
        var knotColor = Darken(baseColor, 0.4f);

        for (int i = 0; i < knotCount; i++)
        {
            float knotX = rect.Left + rect.Width * (0.15f + DeterministicRandom(seed, 110 + i) * 0.7f);
            float knotY = rect.Top + rect.Height * (0.2f + DeterministicRandom(seed, 120 + i) * 0.6f);
            float radiusX = 3f + DeterministicRandom(seed, 130 + i) * 4f;
            float radiusY = 2f + DeterministicRandom(seed, 140 + i) * 3f;

            _fillPaint.Color = knotColor.WithAlpha(120);
            canvas.DrawOval(knotX, knotY, radiusX + 1f, radiusY + 1f, _fillPaint);

            _fillPaint.Color = Darken(baseColor, 0.55f).WithAlpha(160);
            canvas.DrawOval(knotX, knotY, radiusX * 0.6f, radiusY * 0.6f, _fillPaint);
        }

        canvas.Restore();
    }

    // ========================================================================
    // 2. BACKSTEINMUSTER
    // ========================================================================

    /// <summary>
    /// Zeichnet ein Backsteinmuster mit versetzten Reihen und 3D-Effekt.
    /// </summary>
    public static void DrawBrickPattern(SKCanvas canvas, SKRect rect, SKColor brickColor, SKColor mortarColor)
    {
        canvas.Save();
        canvas.ClipRect(rect);

        // Moertel-Hintergrund
        _fillPaint.Color = mortarColor;
        canvas.DrawRect(rect, _fillPaint);

        // Ziegel-Groesse skaliert mit Rect-Breite (Basis: 40x20dp)
        float scale = rect.Width / 200f;
        scale = Math.Clamp(scale, 0.5f, 2.0f);
        float brickW = 40f * scale;
        float brickH = 20f * scale;
        float mortarW = 2f;

        var brickTopColor = Lighten(brickColor, 0.12f);
        var brickBottomColor = Darken(brickColor, 0.12f);

        int row = 0;
        for (float y = rect.Top; y < rect.Bottom; y += brickH + mortarW)
        {
            float xOffset = (row % 2 == 1) ? brickW * 0.5f : 0f;

            for (float x = rect.Left - brickW; x < rect.Right + brickW; x += brickW + mortarW)
            {
                float bx = x + xOffset;

                _fillPaint.Color = brickColor;
                canvas.DrawRect(bx, y, brickW, brickH, _fillPaint);

                // 3D: Oberkante heller
                _fillPaint.Color = brickTopColor.WithAlpha(80);
                canvas.DrawRect(bx, y, brickW, brickH * 0.25f, _fillPaint);

                // 3D: Unterkante dunkler
                _fillPaint.Color = brickBottomColor.WithAlpha(80);
                canvas.DrawRect(bx, y + brickH * 0.75f, brickW, brickH * 0.25f, _fillPaint);
            }
            row++;
        }

        canvas.Restore();
    }

    // ========================================================================
    // 3. GEBUERTSTETES METALL
    // ========================================================================

    /// <summary>
    /// Zeichnet eine gebuerstete Metall-Textur mit feinen Strichen und Highlight.
    /// </summary>
    public static void DrawMetalBrushed(SKCanvas canvas, SKRect rect, SKColor metalColor)
    {
        canvas.Save();
        canvas.ClipRect(rect);

        int seed = RectSeed(rect);

        var centerColor = Lighten(metalColor, 0.1f);
        using var radialShader = SKShader.CreateRadialGradient(
            new SKPoint(rect.MidX, rect.MidY),
            Math.Max(rect.Width, rect.Height) * 0.6f,
            [centerColor, metalColor],
            SKShaderTileMode.Clamp);

        _fillPaint.Shader = radialShader;
        canvas.DrawRect(rect, _fillPaint);
        _fillPaint.Shader = null;

        // 20-30 feine horizontale Striche
        int lineCount = 20 + (int)(DeterministicRandom(seed, 0) * 11);

        for (int i = 0; i < lineCount; i++)
        {
            float y = rect.Top + rect.Height * DeterministicRandom(seed, i + 10);
            byte alpha = (byte)(30 + DeterministicRandom(seed, i + 50) * 30);
            _thinStrokePaint.Color = new SKColor(255, 255, 255, alpha);
            _thinStrokePaint.StrokeWidth = 1f;
            canvas.DrawLine(rect.Left, y, rect.Right, y, _thinStrokePaint);
        }

        // Highlight-Streifen obere 30%
        float highlightBottom = rect.Top + rect.Height * 0.3f;
        using var highlightShader = SKShader.CreateLinearGradient(
            new SKPoint(rect.Left, rect.Top),
            new SKPoint(rect.Left, highlightBottom),
            [new SKColor(255, 255, 255, 35), new SKColor(255, 255, 255, 0)],
            SKShaderTileMode.Clamp);

        _fillPaint.Shader = highlightShader;
        canvas.DrawRect(rect.Left, rect.Top, rect.Width, highlightBottom - rect.Top, _fillPaint);
        _fillPaint.Shader = null;

        canvas.Restore();
    }

    // ========================================================================
    // 4. LEDER-TEXTUR
    // ========================================================================

    /// <summary>
    /// Zeichnet eine Leder-Textur mit Korn-Pattern, Naehten und Vignette.
    /// </summary>
    public static void DrawLeatherTexture(SKCanvas canvas, SKRect rect, SKColor leatherColor)
    {
        canvas.Save();
        canvas.ClipRect(rect);

        int seed = RectSeed(rect);

        _fillPaint.Color = leatherColor;
        canvas.DrawRect(rect, _fillPaint);

        // Vignette
        using var vignetteShader = SKShader.CreateRadialGradient(
            new SKPoint(rect.MidX, rect.MidY),
            Math.Max(rect.Width, rect.Height) * 0.55f,
            [SKColors.Transparent, new SKColor(0, 0, 0, 40)],
            SKShaderTileMode.Clamp);

        _fillPaint.Shader = vignetteShader;
        canvas.DrawRect(rect, _fillPaint);
        _fillPaint.Shader = null;

        // Korn-Pattern (30-50 Punkte)
        int dotCount = 30 + (int)(DeterministicRandom(seed, 0) * 21);
        for (int i = 0; i < dotCount; i++)
        {
            float dx = rect.Left + rect.Width * DeterministicRandom(seed, i + 10);
            float dy = rect.Top + rect.Height * DeterministicRandom(seed, i + 70);
            float radius = 1f + DeterministicRandom(seed, i + 130) * 1.5f;
            byte alpha = (byte)(15 + DeterministicRandom(seed, i + 190) * 10);

            _fillPaint.Color = Darken(leatherColor, 0.2f).WithAlpha(alpha);
            canvas.DrawCircle(dx, dy, radius, _fillPaint);
        }

        // Naehte am Rand
        float stitchInset = 4f;
        var stitchRect = new SKRect(
            rect.Left + stitchInset, rect.Top + stitchInset,
            rect.Right - stitchInset, rect.Bottom - stitchInset);

        _stitchPaint.Color = Darken(leatherColor, 0.35f).WithAlpha(120);
        canvas.DrawRect(stitchRect, _stitchPaint);

        canvas.Restore();
    }

    // ========================================================================
    // 5. BRUCHSTEIN-TEXTUR
    // ========================================================================

    /// <summary>
    /// Zeichnet eine Bruchstein-Textur mit unregelmaessigen Polygonen und Fugen.
    /// </summary>
    public static void DrawStoneTexture(SKCanvas canvas, SKRect rect, SKColor stoneColor)
    {
        canvas.Save();
        canvas.ClipRect(rect);

        int seed = RectSeed(rect);

        // Fugen-Hintergrund
        _fillPaint.Color = Darken(stoneColor, 0.3f);
        canvas.DrawRect(rect, _fillPaint);

        int stoneCount = 5 + (int)(DeterministicRandom(seed, 0) * 4);
        int cols = (int)MathF.Ceiling(MathF.Sqrt(stoneCount * rect.Width / rect.Height));
        int rows = (int)MathF.Ceiling((float)stoneCount / cols);
        if (cols < 2) cols = 2;
        if (rows < 2) rows = 2;

        float cellW = rect.Width / cols;
        float cellH = rect.Height / rows;
        int stoneIndex = 0;

        for (int r = 0; r < rows && stoneIndex < stoneCount; r++)
        {
            for (int c = 0; c < cols && stoneIndex < stoneCount; c++)
            {
                float cx = rect.Left + cellW * (c + 0.5f) +
                    (DeterministicRandom(seed, stoneIndex * 7 + 200) - 0.5f) * cellW * 0.3f;
                float cy = rect.Top + cellH * (r + 0.5f) +
                    (DeterministicRandom(seed, stoneIndex * 7 + 201) - 0.5f) * cellH * 0.3f;

                int vertexCount = 4 + (int)(DeterministicRandom(seed, stoneIndex * 7 + 202) * 3);
                float baseRadius = Math.Min(cellW, cellH) * 0.38f;

                using var path = new SKPath();
                for (int v = 0; v < vertexCount; v++)
                {
                    float angle = MathF.PI * 2f * v / vertexCount;
                    float radiusVar = baseRadius * (0.7f + DeterministicRandom(seed, stoneIndex * 7 + 203 + v) * 0.6f);
                    float px = cx + MathF.Cos(angle) * radiusVar;
                    float py = cy + MathF.Sin(angle) * radiusVar;

                    if (v == 0) path.MoveTo(px, py);
                    else path.LineTo(px, py);
                }
                path.Close();

                // Schatten
                canvas.Save();
                canvas.Translate(1f, 1f);
                _fillPaint.Color = new SKColor(0, 0, 0, 40);
                _fillPaint.MaskFilter = StoneShadowFilter;
                canvas.DrawPath(path, _fillPaint);
                _fillPaint.MaskFilter = null;
                canvas.Restore();

                // Stein-Fuellung
                float colorVar = (DeterministicRandom(seed, stoneIndex * 7 + 204) - 0.5f) * 0.15f;
                var thisStoneColor = colorVar > 0 ? Lighten(stoneColor, colorVar) : Darken(stoneColor, -colorVar);
                _fillPaint.Color = thisStoneColor;
                canvas.DrawPath(path, _fillPaint);

                // Fugen-Kontur
                _strokePaint.Color = Darken(stoneColor, 0.3f);
                _strokePaint.StrokeWidth = 2f;
                canvas.DrawPath(path, _strokePaint);

                stoneIndex++;
            }
        }

        canvas.Restore();
    }

    // ========================================================================
    // 6. BETON-TEXTUR
    // ========================================================================

    /// <summary>
    /// Zeichnet eine Beton-Textur mit Rissen, Noise und Flecken.
    /// </summary>
    public static void DrawConcreteTexture(SKCanvas canvas, SKRect rect)
    {
        canvas.Save();
        canvas.ClipRect(rect);

        int seed = RectSeed(rect);

        var concreteColor = new SKColor(0xB0, 0xBE, 0xC5);
        _fillPaint.Color = concreteColor;
        canvas.DrawRect(rect, _fillPaint);

        // Noise-Textur (30-40 Punkte)
        int noiseCount = 30 + (int)(DeterministicRandom(seed, 0) * 11);
        for (int i = 0; i < noiseCount; i++)
        {
            float nx = rect.Left + rect.Width * DeterministicRandom(seed, i + 10);
            float ny = rect.Top + rect.Height * DeterministicRandom(seed, i + 60);
            float radius = 0.8f + DeterministicRandom(seed, i + 110) * 1.2f;

            bool isLight = DeterministicRandom(seed, i + 160) > 0.5f;
            byte alpha = (byte)(15 + DeterministicRandom(seed, i + 210) * 20);
            _fillPaint.Color = isLight
                ? new SKColor(255, 255, 255, alpha)
                : new SKColor(0, 0, 0, alpha);

            canvas.DrawCircle(nx, ny, radius, _fillPaint);
        }

        // 2-3 Risse
        int crackCount = 2 + (int)(DeterministicRandom(seed, 300) * 2);
        for (int i = 0; i < crackCount; i++)
        {
            float startX = rect.Left + rect.Width * DeterministicRandom(seed, 310 + i * 5);
            float startY = rect.Top + rect.Height * DeterministicRandom(seed, 311 + i * 5);

            float angle = DeterministicRandom(seed, 312 + i * 5) * MathF.PI;
            float length = rect.Width * (0.15f + DeterministicRandom(seed, 313 + i * 5) * 0.25f);

            _crackPaint.Color = new SKColor(0x60, 0x60, 0x60, 140);
            _crackPaint.StrokeWidth = 1f + DeterministicRandom(seed, 314 + i * 5) * 0.5f;

            using var crackPath = new SKPath();
            crackPath.MoveTo(startX, startY);

            int segments = 3 + (int)DeterministicRandom(seed, 315 + i * 5);
            float segLen = length / segments;
            float cx = startX;
            float cy = startY;

            for (int s = 0; s < segments; s++)
            {
                float segAngle = angle + (DeterministicRandom(seed, 320 + i * 10 + s) - 0.5f) * 0.6f;
                cx += MathF.Cos(segAngle) * segLen;
                cy += MathF.Sin(segAngle) * segLen;
                crackPath.LineTo(cx, cy);
            }

            canvas.DrawPath(crackPath, _crackPaint);
        }

        // 3-4 Flecken
        int spotCount = 3 + (int)(DeterministicRandom(seed, 400) * 2);
        for (int i = 0; i < spotCount; i++)
        {
            float sx = rect.Left + rect.Width * DeterministicRandom(seed, 410 + i);
            float sy = rect.Top + rect.Height * DeterministicRandom(seed, 420 + i);
            float radius = 5f + DeterministicRandom(seed, 430 + i) * 10f;

            bool isDarker = DeterministicRandom(seed, 440 + i) > 0.5f;
            byte spotAlpha = (byte)(10 + DeterministicRandom(seed, 450 + i) * 15);
            _fillPaint.Color = isDarker
                ? new SKColor(0, 0, 0, spotAlpha)
                : new SKColor(255, 255, 255, spotAlpha);

            canvas.DrawCircle(sx, sy, radius, _fillPaint);
        }

        canvas.Restore();
    }
}
