using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// 2D-Grundriss mit Fliesengitter, angeschnittene Randfliesen rot schraffiert, Maßlinien.
/// </summary>
public static class TileVisualization
{
    // Einschwing-Animation
    private static readonly AnimatedVisualizationBase _animation = new()
    {
        AnimationDurationMs = 500f,
        EasingFunction = EasingFunctions.EaseOutCubic
    };

    /// <summary>Startet die Einschwing-Animation.</summary>
    public static void StartAnimation() => _animation.StartAnimation();

    /// <summary>True wenn noch animiert wird (für InvalidateSurface-Loop).</summary>
    public static bool NeedsRedraw => _animation.IsAnimating;

    private static readonly SKPaint _roomFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _roomStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _tileFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _tileStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f };

    /// <summary>
    /// Rendert die Fliesen-Visualisierung: Raum-Grundriss mit Fliesengitter und Maßlinien.
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds,
        float roomLengthM, float roomWidthM, float tileLengthCm, float tileWidthCm,
        float wastePercent, bool hasResult)
    {
        if (!hasResult || roomLengthM <= 0 || roomWidthM <= 0) return;
        if (tileLengthCm <= 0 || tileWidthCm <= 0) return;

        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        // Hintergrund-Raster
        SkiaBlueprintCanvas.DrawGrid(canvas, bounds, 20f);

        // Konvertierung: Fliesen von cm in m
        float tileLenM = tileLengthCm / 100f;
        float tileWidM = tileWidthCm / 100f;

        // Auto-Skalierung
        var fit = SkiaBlueprintCanvas.FitToCanvas(bounds, roomLengthM, roomWidthM, 40f);
        float scale = fit.Scale;
        float ox = fit.OffsetX;
        float oy = fit.OffsetY;

        float rw = roomLengthM * scale;
        float rh = roomWidthM * scale;

        // Raum-Hintergrund
        _roomFill.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Surface, 100);
        canvas.DrawRect(ox, oy, rw, rh, _roomFill);

        // Fliesengitter
        float tw = tileLenM * scale;
        float th = tileWidM * scale;

        if (tw > 2f && th > 2f)
        {
            int cols = (int)Math.Ceiling(rw / tw);
            int rows = (int)Math.Ceiling(rh / th);

            canvas.Save();
            canvas.ClipRect(new SKRect(ox, oy, ox + rw, oy + rh));

            // Animation: Fliesen legen sich Reihe für Reihe
            int visibleRows = (int)(rows * progress);
            for (int row = 0; row < visibleRows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    float x = ox + col * tw;
                    float y = oy + row * th;
                    float w = Math.Min(tw, ox + rw - x);
                    float h = Math.Min(th, oy + rh - y);

                    bool isCut = (col == cols - 1 && w < tw * 0.99f) ||
                                 (row == rows - 1 && h < th * 0.99f);

                    if (isCut)
                    {
                        // Verschnitt-Fliese: rot schraffiert
                        _tileFill.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Error, 40);
                        canvas.DrawRect(x, y, w, h, _tileFill);
                        SkiaBlueprintCanvas.DrawCrosshatch(canvas, new SKRect(x, y, x + w, y + h),
                            SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Error, 80), 6f);
                    }
                    else
                    {
                        // Normale Fliese: leicht abwechselnd
                        byte alpha = (byte)((row + col) % 2 == 0 ? 30 : 50);
                        _tileFill.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Accent, alpha);
                        canvas.DrawRect(x, y, w, h, _tileFill);
                    }

                    // Fliesen-Rahmen
                    _tileStroke.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Accent, 100);
                    canvas.DrawRect(x, y, w, h, _tileStroke);
                }
            }

            canvas.Restore();
        }

        // Raum-Umriss
        _roomStroke.Color = SkiaThemeHelper.TextPrimary;
        canvas.DrawRect(ox, oy, rw, rh, _roomStroke);

        // Maßlinien
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox, oy), new SKPoint(ox + rw, oy),
            $"{roomLengthM:F2} m", offset: -14f);

        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox, oy), new SKPoint(ox, oy + rh),
            $"{roomWidthM:F2} m", offset: -14f);

        // Verschnitt-Info-Box (unten rechts, semi-transparenter Hintergrund)
        if (wastePercent > 0)
        {
            DrawWasteInfoBox(canvas, ox, oy, rw, rh, wastePercent, tileLengthCm, tileWidthCm);
        }

        // Fliesen-Maß (Einzelfliese) als kleine Bemaßung
        if (tw > 20f && th > 20f)
        {
            // Erste volle Fliese bemaßen
            SkiaBlueprintCanvas.DrawDimensionLine(canvas,
                new SKPoint(ox, oy + rh + 20f), new SKPoint(ox + tw, oy + rh + 20f),
                $"{tileLengthCm:F0} cm", offset: 10f);
        }
    }

    private static readonly SKPaint _infoBoxBg = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _infoBoxBorder = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _infoTextPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _infoTitleFont = new() { Size = 11f };
    private static readonly SKFont _infoValueFont = new() { Size = 14f };
    private static readonly SKFont _infoDetailFont = new() { Size = 9f };

    /// <summary>
    /// Zeichnet eine Info-Box mit Verschnitt-Details (unten rechts im Raum).
    /// </summary>
    private static void DrawWasteInfoBox(SKCanvas canvas, float ox, float oy,
        float rw, float rh, float wastePercent, float tileLenCm, float tileWidCm)
    {
        float boxW = 90f;
        float boxH = 52f;
        float boxX = ox + rw - boxW - 6f;
        float boxY = oy + rh - boxH - 6f;

        // Hintergrund mit Blur-Effekt
        _infoBoxBg.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Background, 200);
        var boxRect = new SKRect(boxX, boxY, boxX + boxW, boxY + boxH);
        canvas.DrawRoundRect(boxRect, 6f, 6f, _infoBoxBg);

        // Rand in Fehlerfarbe
        _infoBoxBorder.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Error, 150);
        canvas.DrawRoundRect(boxRect, 6f, 6f, _infoBoxBorder);

        // Titel "Verschnitt"
        _infoTextPaint.Color = SkiaThemeHelper.Error;
        _infoTitleFont.Size = 9f;
        canvas.DrawText("Verschnitt", boxX + boxW / 2f, boxY + 12f,
            SKTextAlign.Center, _infoTitleFont, _infoTextPaint);

        // Großer Prozentwert
        _infoTextPaint.Color = SkiaThemeHelper.Error;
        _infoValueFont.Size = 16f;
        canvas.DrawText($"+{wastePercent:F0}%", boxX + boxW / 2f, boxY + 30f,
            SKTextAlign.Center, _infoValueFont, _infoTextPaint);

        // Fliesengröße
        _infoTextPaint.Color = SkiaThemeHelper.TextMuted;
        _infoDetailFont.Size = 8f;
        canvas.DrawText($"{tileLenCm:F0}×{tileWidCm:F0} cm", boxX + boxW / 2f, boxY + 42f,
            SKTextAlign.Center, _infoDetailFont, _infoTextPaint);
    }
}
