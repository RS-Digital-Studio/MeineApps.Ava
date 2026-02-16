using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Wand-Abwicklung: vertikale Tapetenbahnen, Rapport-Versatz als gestrichelte Linien.
/// </summary>
public static class WallpaperVisualization
{
    private static readonly SKPaint _wallFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _wallStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _stripFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _stripStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };

    public static void Render(SKCanvas canvas, SKRect bounds,
        float wallLengthM, float roomHeightM, float rollWidthCm, float patternRepeatCm,
        int stripsNeeded, bool hasResult)
    {
        if (!hasResult || wallLengthM <= 0 || roomHeightM <= 0) return;
        if (rollWidthCm <= 0) return;

        SkiaBlueprintCanvas.DrawGrid(canvas, bounds, 20f);

        float rollWidthM = rollWidthCm / 100f;
        float patternRepeatM = patternRepeatCm / 100f;

        var fit = SkiaBlueprintCanvas.FitToCanvas(bounds, wallLengthM, roomHeightM, 40f);
        float scale = fit.Scale;
        float ox = fit.OffsetX;
        float oy = fit.OffsetY;

        float ww = wallLengthM * scale;
        float wh = roomHeightM * scale;

        // Wand-Hintergrund
        _wallFill.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Surface, 80);
        canvas.DrawRect(ox, oy, ww, wh, _wallFill);

        // Tapetenbahnen zeichnen
        float stripW = rollWidthM * scale;
        if (stripW > 1f)
        {
            int strips = Math.Max(1, (int)Math.Ceiling(ww / stripW));
            var colors = new[] { SkiaThemeHelper.Accent, SkiaThemeHelper.Secondary };

            canvas.Save();
            canvas.ClipRect(new SKRect(ox, oy, ox + ww, oy + wh));

            for (int i = 0; i < strips; i++)
            {
                float x = ox + i * stripW;
                float w = Math.Min(stripW, ox + ww - x);

                // Abwechselnde Farben für Bahnen
                byte alpha = (byte)(i % 2 == 0 ? 40 : 60);
                _stripFill.Color = SkiaThemeHelper.WithAlpha(colors[i % 2], alpha);
                canvas.DrawRect(x, oy, w, wh, _stripFill);

                // Bahnen-Trennlinie
                if (i > 0)
                {
                    _stripStroke.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Accent, 120);
                    canvas.DrawLine(x, oy, x, oy + wh, _stripStroke);
                }
            }

            // Rapport-Linien (horizontal gestrichelt)
            if (patternRepeatM > 0.01f)
            {
                float patternH = patternRepeatM * scale;
                for (float y = patternH; y < wh; y += patternH)
                {
                    SkiaBlueprintCanvas.DrawDashedLine(canvas,
                        new SKPoint(ox, oy + y), new SKPoint(ox + ww, oy + y),
                        SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Warning, 100), 4f, 3f);
                }
            }

            canvas.Restore();
        }

        // Wand-Umriss
        _wallStroke.Color = SkiaThemeHelper.TextPrimary;
        canvas.DrawRect(ox, oy, ww, wh, _wallStroke);

        // Maßlinien
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox, oy), new SKPoint(ox + ww, oy),
            $"{wallLengthM:F2} m", offset: -14f);

        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox + ww, oy), new SKPoint(ox + ww, oy + wh),
            $"{roomHeightM:F2} m", offset: -14f);

        // Bahnen-Anzahl
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{stripsNeeded} Bahnen",
            new SKPoint(ox + ww / 2f, oy + wh + 12f),
            SkiaThemeHelper.TextSecondary, 9f);
    }
}
