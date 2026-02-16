using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// 2D-Grundriss mit Fliesengitter, angeschnittene Randfliesen rot schraffiert, Maßlinien.
/// </summary>
public static class TileVisualization
{
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

            for (int row = 0; row < rows; row++)
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

        // Verschnitt-Info
        if (wastePercent > 0)
        {
            SkiaBlueprintCanvas.DrawMeasurementText(canvas,
                $"+{wastePercent:F0}%",
                new SKPoint(ox + rw - 20f, oy + rh - 8f),
                SkiaThemeHelper.Error, 9f);
        }
    }
}
