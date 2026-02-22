using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Mini-Vorschau pro Level im LevelSelect: Grid-Thumbnail mit Block-Typen farbig,
/// Gold-Shimmer Sterne bei erreichten, Grau-Schloss bei gesperrten.
/// Kann als SKCanvasView-Alternative oder Ergänzung verwendet werden.
/// </summary>
public static class LevelSelectVisualization
{
    private static readonly SKPaint _cellPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _starPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _starStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private static readonly SKPaint _lockPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _levelFont = new() { Size = 16f };
    private static readonly SKFont _smallFont = new() { Size = 9f };

    // Welt-Farben (10 Welten)
    private static readonly SKColor[] WorldColors =
    {
        new(0x2E, 0x7D, 0x32), // 0 Forest (Grün)
        new(0x37, 0x47, 0x4F), // 1 Industrial (Grau-Blau)
        new(0x4A, 0x14, 0x8C), // 2 Cavern (Violett)
        new(0x02, 0x77, 0xBD), // 3 Sky (Blau)
        new(0xB7, 0x1C, 0x1C), // 4 Inferno (Rot)
        new(0x8D, 0x6E, 0x63), // 5 Ruins (Braun)
        new(0x00, 0x69, 0x7C), // 6 Ocean (Tief-Teal)
        new(0xBF, 0x36, 0x0C), // 7 Volcano (Orange-Rot)
        new(0xC6, 0xA7, 0x00), // 8 SkyFortress (Gold)
        new(0x4A, 0x14, 0x8C), // 9 ShadowRealm (Dunkel-Violett)
    };

    // Stern-Farben
    private static readonly SKColor _starGold = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor _starEmpty = new(0x66, 0x66, 0x66);

    /// <summary>
    /// Daten für ein Level-Thumbnail.
    /// </summary>
    public struct LevelData
    {
        /// <summary>Level-Nummer (1-50).</summary>
        public int LevelNumber;

        /// <summary>Welt-Index (0-4).</summary>
        public int WorldIndex;

        /// <summary>Erreichte Sterne (0-3).</summary>
        public int Stars;

        /// <summary>Ob das Level freigeschaltet ist.</summary>
        public bool IsUnlocked;

        /// <summary>Ob das Level abgeschlossen wurde.</summary>
        public bool IsCompleted;
    }

    /// <summary>
    /// Rendert ein einzelnes Level-Thumbnail mit Nummer, Sternen und Lock-Overlay.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich des Thumbnails</param>
    /// <param name="level">Level-Daten</param>
    /// <param name="animTime">Animations-Zeit für Shimmer</param>
    public static void RenderLevelThumbnail(SKCanvas canvas, SKRect bounds,
        LevelData level, float animTime)
    {
        float cornerR = 8f;
        int wi = Math.Clamp(level.WorldIndex, 0, WorldColors.Length - 1);
        var worldColor = WorldColors[wi];

        if (!level.IsUnlocked)
        {
            // Gesperrtes Level: Grau + Schloss
            _cellPaint.Color = new SKColor(0x33, 0x33, 0x33);
            canvas.DrawRoundRect(bounds, cornerR, cornerR, _cellPaint);

            // Schloss-Symbol (vereinfacht)
            DrawLockIcon(canvas, bounds.MidX, bounds.MidY - 2f, 14f);

            return;
        }

        // Hintergrund: Welt-Farbe (dunkler wenn nicht abgeschlossen)
        _cellPaint.Color = level.IsCompleted ? worldColor : worldColor.WithAlpha(140);
        canvas.DrawRoundRect(bounds, cornerR, cornerR, _cellPaint);

        // Subtiler Gradient-Overlay (oben heller)
        _cellPaint.Color = SKColors.White.WithAlpha(25);
        canvas.Save();
        using var clipPath = new SKPath();
        clipPath.AddRoundRect(bounds, cornerR, cornerR);
        canvas.ClipPath(clipPath);
        canvas.DrawRect(bounds.Left, bounds.Top, bounds.Width, bounds.Height * 0.4f, _cellPaint);
        canvas.Restore();

        // Welt-spezifisches Muster
        DrawWorldPattern(canvas, bounds, wi, animTime);

        // Rand
        _starStroke.Color = SKColors.White.WithAlpha(60);
        canvas.DrawRoundRect(bounds, cornerR, cornerR, _starStroke);

        // Level-Nummer (zentriert)
        _textPaint.Color = SKColors.White;
        _levelFont.Size = Math.Min(bounds.Height * 0.35f, 20f);
        canvas.DrawText(level.LevelNumber.ToString(), bounds.MidX, bounds.MidY + _levelFont.Size * 0.15f,
            SKTextAlign.Center, _levelFont, _textPaint);

        // Sterne (unten, 3 Stück)
        if (level.IsCompleted)
        {
            DrawStars(canvas, bounds.MidX, bounds.Bottom - 10f, level.Stars, animTime);
        }
    }

    /// <summary>
    /// Zeichnet ein welt-spezifisches Mini-Muster als Wiedererkennungs-Element.
    /// </summary>
    private static void DrawWorldPattern(SKCanvas canvas, SKRect bounds, int worldIndex, float animTime)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY;

        switch (worldIndex)
        {
            case 0: // Forest: 2 kleine Baum-Dreiecke
                _cellPaint.Color = new SKColor(0x1B, 0x5E, 0x20, 40);
                for (int i = 0; i < 2; i++)
                {
                    float tx = bounds.Left + bounds.Width * (0.25f + i * 0.5f);
                    float ty = bounds.Bottom - 6f;
                    using var tree = new SKPath();
                    tree.MoveTo(tx, ty - 12f);
                    tree.LineTo(tx - 5f, ty);
                    tree.LineTo(tx + 5f, ty);
                    tree.Close();
                    canvas.DrawPath(tree, _cellPaint);
                }
                break;

            case 1: // Industrial: Zahnrad-Kreis
                _cellPaint.Color = new SKColor(0x78, 0x90, 0x9C, 35);
                float angle = animTime * 0.5f;
                for (int i = 0; i < 6; i++)
                {
                    float a = angle + i * MathF.PI / 3f;
                    float gx = cx + MathF.Cos(a) * 8f;
                    float gy = cy + MathF.Sin(a) * 8f;
                    canvas.DrawCircle(gx, gy, 2f, _cellPaint);
                }
                break;

            case 2: // Cavern: Kristall-Rauten
                _cellPaint.Color = new SKColor(0x7C, 0x4D, 0xFF, 35);
                for (int i = 0; i < 3; i++)
                {
                    float kx = bounds.Left + bounds.Width * (0.2f + i * 0.3f);
                    float ky = bounds.Top + bounds.Height * 0.3f + i * 4f;
                    using var crystal = new SKPath();
                    crystal.MoveTo(kx, ky - 4f);
                    crystal.LineTo(kx + 3f, ky);
                    crystal.LineTo(kx, ky + 4f);
                    crystal.LineTo(kx - 3f, ky);
                    crystal.Close();
                    canvas.DrawPath(crystal, _cellPaint);
                }
                break;

            case 3: // Sky: Wolken-Ovale
                _cellPaint.Color = SKColors.White.WithAlpha(30);
                canvas.DrawOval(bounds.Left + 8f, bounds.Top + 10f, 10f, 4f, _cellPaint);
                canvas.DrawOval(bounds.Right - 12f, bounds.Top + 16f, 8f, 3f, _cellPaint);
                break;

            case 4: // Inferno: Flammen-Dreiecke unten
                _cellPaint.Color = new SKColor(0xFF, 0x6F, 0x00, 35);
                for (int i = 0; i < 3; i++)
                {
                    float fx = bounds.Left + bounds.Width * (0.2f + i * 0.3f);
                    float fy = bounds.Bottom - 4f;
                    float fh = 6f + MathF.Sin(animTime * 3f + i) * 2f;
                    using var flame = new SKPath();
                    flame.MoveTo(fx, fy - fh);
                    flame.LineTo(fx - 3f, fy);
                    flame.LineTo(fx + 3f, fy);
                    flame.Close();
                    canvas.DrawPath(flame, _cellPaint);
                }
                break;

            case 5: // Ruins: Horizontale Riss-Linien
                _starStroke.Color = new SKColor(0xA1, 0x88, 0x7F, 35);
                _starStroke.StrokeWidth = 0.8f;
                canvas.DrawLine(bounds.Left + 4f, cy - 4f, bounds.Right - 8f, cy - 3f, _starStroke);
                canvas.DrawLine(bounds.Left + 8f, cy + 5f, bounds.Right - 4f, cy + 4f, _starStroke);
                _starStroke.StrokeWidth = 1f;
                break;

            case 6: // Ocean: Wellenlinien
                _starStroke.Color = new SKColor(0x00, 0xAC, 0xC1, 30);
                _starStroke.StrokeWidth = 0.8f;
                for (int w = 0; w < 2; w++)
                {
                    float wy = cy + (w - 0.5f) * 8f;
                    using var wave = new SKPath();
                    wave.MoveTo(bounds.Left + 3f, wy);
                    for (float wx = bounds.Left + 3f; wx < bounds.Right - 3f; wx += 6f)
                    {
                        wave.QuadTo(wx + 3f, wy - 3f, wx + 6f, wy);
                    }
                    canvas.DrawPath(wave, _starStroke);
                }
                _starStroke.StrokeWidth = 1f;
                break;

            case 7: // Volcano: Dreieck-Berg + Glow oben
                _cellPaint.Color = new SKColor(0x8B, 0x2E, 0x0F, 35);
                using (var mountain = new SKPath())
                {
                    mountain.MoveTo(cx, bounds.Top + 8f);
                    mountain.LineTo(cx - 12f, bounds.Bottom - 6f);
                    mountain.LineTo(cx + 12f, bounds.Bottom - 6f);
                    mountain.Close();
                    canvas.DrawPath(mountain, _cellPaint);
                }
                _cellPaint.Color = new SKColor(0xFF, 0x6F, 0x00, 25);
                canvas.DrawCircle(cx, bounds.Top + 10f, 4f, _cellPaint);
                break;

            case 8: // SkyFortress: Goldene Rauten-Deko
                _cellPaint.Color = new SKColor(0xFF, 0xD7, 0x00, 30);
                float shimmer = 0.7f + 0.3f * MathF.Sin(animTime * 2f);
                _cellPaint.Color = _cellPaint.Color.WithAlpha((byte)(30 * shimmer));
                canvas.DrawRect(cx - 2f, cy - 8f, 4f, 4f, _cellPaint);
                canvas.DrawRect(cx - 6f, cy - 4f, 4f, 4f, _cellPaint);
                canvas.DrawRect(cx + 2f, cy - 4f, 4f, 4f, _cellPaint);
                break;

            case 9: // ShadowRealm: Augen-Paar
                float blink = MathF.Sin(animTime * 0.8f);
                if (blink > -0.3f) // Nicht während Blinzeln
                {
                    _cellPaint.Color = new SKColor(0xAA, 0x00, 0xFF, 30);
                    float eyeH = 2f * Math.Clamp(blink + 0.3f, 0.3f, 1f);
                    canvas.DrawOval(cx - 5f, cy - 2f, 2.5f, eyeH, _cellPaint);
                    canvas.DrawOval(cx + 5f, cy - 2f, 2.5f, eyeH, _cellPaint);
                }
                break;
        }
    }

    /// <summary>
    /// Rendert 3 Sterne nebeneinander (gefüllte Gold + leere Grau).
    /// Gefüllte Sterne haben Gold-Shimmer Animation.
    /// </summary>
    public static void DrawStars(SKCanvas canvas, float cx, float cy,
        int earnedStars, float animTime)
    {
        float starSize = 7f;
        float spacing = starSize * 2.2f;
        float startX = cx - spacing;

        for (int i = 0; i < 3; i++)
        {
            float sx = startX + i * spacing;
            bool earned = i < earnedStars;

            if (earned)
            {
                // Gold-Shimmer: Helligkeit pulsiert leicht
                float shimmer = 0.85f + 0.15f * MathF.Sin(animTime * 3f + i * 0.5f);
                byte r = (byte)Math.Min(255, (int)(_starGold.Red * shimmer));
                byte g = (byte)Math.Min(255, (int)(_starGold.Green * shimmer));
                byte b = (byte)Math.Min(255, (int)(_starGold.Blue * shimmer));

                _starPaint.Color = new SKColor(r, g, b);
                DrawStarShape(canvas, sx, cy, starSize, _starPaint);

                // Glow
                _glowPaint.Color = _starGold.WithAlpha(40);
                _glowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);
                DrawStarShape(canvas, sx, cy, starSize * 1.1f, _glowPaint);
                _glowPaint.MaskFilter = null;
            }
            else
            {
                // Leerer Stern (Outline)
                _starStroke.Color = _starEmpty;
                _starStroke.StrokeWidth = 1f;
                DrawStarShapeStroke(canvas, sx, cy, starSize, _starStroke);
            }
        }
    }

    /// <summary>
    /// Zeichnet einen 5-zackigen Stern (gefüllt).
    /// </summary>
    private static void DrawStarShape(SKCanvas canvas, float cx, float cy, float r, SKPaint paint)
    {
        using var path = CreateStarPath(cx, cy, r);
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Zeichnet einen 5-zackigen Stern (Outline).
    /// </summary>
    private static void DrawStarShapeStroke(SKCanvas canvas, float cx, float cy, float r, SKPaint paint)
    {
        using var path = CreateStarPath(cx, cy, r);
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Erstellt den Pfad für einen 5-zackigen Stern.
    /// </summary>
    private static SKPath CreateStarPath(float cx, float cy, float outerR)
    {
        float innerR = outerR * 0.4f;
        var path = new SKPath();

        for (int i = 0; i < 10; i++)
        {
            float angle = (i * 36f - 90f) * MathF.PI / 180f;
            float radius = i % 2 == 0 ? outerR : innerR;
            float px = cx + radius * MathF.Cos(angle);
            float py = cy + radius * MathF.Sin(angle);

            if (i == 0) path.MoveTo(px, py);
            else path.LineTo(px, py);
        }

        path.Close();
        return path;
    }

    /// <summary>
    /// Zeichnet ein vereinfachtes Schloss-Symbol.
    /// </summary>
    private static void DrawLockIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float bodyW = size * 0.7f;
        float bodyH = size * 0.55f;
        float bodyY = cy;

        // Körper (Rechteck)
        _lockPaint.Color = new SKColor(0x88, 0x88, 0x88);
        var bodyRect = new SKRect(cx - bodyW / 2f, bodyY, cx + bodyW / 2f, bodyY + bodyH);
        canvas.DrawRoundRect(bodyRect, 2f, 2f, _lockPaint);

        // Bügel (Halbkreis oben)
        _starStroke.Color = new SKColor(0x88, 0x88, 0x88);
        _starStroke.StrokeWidth = 2f;
        float arcW = bodyW * 0.65f;
        float arcH = size * 0.4f;
        var arcRect = new SKRect(cx - arcW / 2f, bodyY - arcH, cx + arcW / 2f, bodyY);
        using var arcPath = new SKPath();
        arcPath.AddArc(arcRect, 180f, 180f);
        canvas.DrawPath(arcPath, _starStroke);
        _starStroke.StrokeWidth = 1f;

        // Schlüsselloch (kleiner Kreis + Dreieck)
        _lockPaint.Color = new SKColor(0x44, 0x44, 0x44);
        canvas.DrawCircle(cx, bodyY + bodyH * 0.35f, 2f, _lockPaint);
    }
}
