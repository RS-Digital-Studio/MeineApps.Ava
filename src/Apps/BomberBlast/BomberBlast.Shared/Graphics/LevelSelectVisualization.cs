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

    // Welt-Farben (5 Welten)
    private static readonly SKColor[] WorldColors =
    {
        new(0x2E, 0x7D, 0x32), // Forest (Grün)
        new(0x37, 0x47, 0x4F), // Industrial (Grau-Blau)
        new(0x4A, 0x14, 0x8C), // Cavern (Violett)
        new(0x02, 0x77, 0xBD), // Sky (Blau)
        new(0xB7, 0x1C, 0x1C), // Inferno (Rot)
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
