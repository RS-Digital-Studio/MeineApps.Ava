using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Wand-Rechteck mit semi-transparenten Farbschichten pro Anstrich, Flächen-Maßlinie.
/// </summary>
public static class PaintVisualization
{
    private static readonly SKPaint _wallFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _wallStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _coatPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    public static void Render(SKCanvas canvas, SKRect bounds,
        float areaSqm, int numberOfCoats, float litersNeeded, bool hasResult)
    {
        if (!hasResult || areaSqm <= 0) return;

        SkiaBlueprintCanvas.DrawGrid(canvas, bounds, 20f);

        // Wand als Quadrat/Rechteck darstellen (Seitenverhältnis aus Fläche ableiten)
        float wallW = MathF.Sqrt(areaSqm * 1.5f); // Querformat
        float wallH = areaSqm / wallW;

        var fit = SkiaBlueprintCanvas.FitToCanvas(bounds, wallW, wallH, 40f);
        float scale = fit.Scale;
        float ox = fit.OffsetX;
        float oy = fit.OffsetY;

        float rw = wallW * scale;
        float rh = wallH * scale;

        // Basis-Wand (grau)
        _wallFill.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Card, 200);
        canvas.DrawRect(ox, oy, rw, rh, _wallFill);

        // Farbschichten übereinander (semi-transparent)
        int coats = Math.Clamp(numberOfCoats, 1, 5);
        for (int i = 0; i < coats; i++)
        {
            // Jede Schicht leicht versetzt (von unten nach oben wachsend)
            float coverage = (i + 1f) / coats;
            float layerH = rh * coverage;
            float layerY = oy + rh - layerH;

            // Deckung steigt pro Anstrich
            byte alpha = (byte)Math.Min(255, 30 + i * 40);
            _coatPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Accent, alpha);
            canvas.DrawRect(ox, layerY, rw, layerH, _coatPaint);

            // Schicht-Nummer am rechten Rand
            float labelY = layerY + 12f;
            if (labelY > oy + 8f && labelY < oy + rh - 4f)
            {
                SkiaBlueprintCanvas.DrawMeasurementText(canvas,
                    $"{i + 1}×",
                    new SKPoint(ox + rw - 16f, labelY),
                    SkiaThemeHelper.TextSecondary, 8f);
            }
        }

        // Wand-Umriss
        _wallStroke.Color = SkiaThemeHelper.TextPrimary;
        canvas.DrawRect(ox, oy, rw, rh, _wallStroke);

        // Flächen-Maßlinie
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{areaSqm:F1} m²",
            new SKPoint(ox + rw / 2f, oy + rh / 2f),
            SkiaThemeHelper.TextPrimary, 13f);

        // Anstriche-Info
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{numberOfCoats} Anstriche = {litersNeeded:F1} L",
            new SKPoint(ox + rw / 2f, oy + rh + 12f),
            SkiaThemeHelper.TextSecondary, 9f);
    }
}
