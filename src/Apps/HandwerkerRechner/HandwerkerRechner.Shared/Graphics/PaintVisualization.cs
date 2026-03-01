using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Wand-Rechteck mit semi-transparenten Farbschichten pro Anstrich, Flächen-Maßlinie.
/// </summary>
public static class PaintVisualization
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

    private static readonly SKPaint _wallFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _wallStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _coatPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    public static void Render(SKCanvas canvas, SKRect bounds,
        float areaSqm, int numberOfCoats, float litersNeeded, bool hasResult)
    {
        if (!hasResult || areaSqm <= 0) return;

        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

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
            // Jede Schicht leicht versetzt (von unten nach oben wachsend, Animation: Höhe mit progress)
            float coverage = (i + 1f) / coats;
            float layerH = rh * coverage * progress;
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

        // Farbkannen-Icons (unterhalb der Wand)
        if (litersNeeded > 0)
        {
            DrawPaintCans(canvas, bounds, ox, oy + rh + 24f, rw, litersNeeded);
        }
    }

    private static readonly SKPaint _canPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _canStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f };
    private static readonly SKFont _canFont = new() { Size = 8f };

    /// <summary>
    /// Zeichnet Farbkannen-Icons für den Gesamtbedarf.
    /// Pro Kanne ~2.5L (Standardgröße), maximal 10 Icons angezeigt.
    /// </summary>
    private static void DrawPaintCans(SKCanvas canvas, SKRect bounds,
        float startX, float canY, float availWidth, float litersNeeded)
    {
        float canSize = 18f;
        float spacing = 4f;
        float canLiters = 2.5f; // Standard-Kanister

        int cansNeeded = (int)Math.Ceiling(litersNeeded / canLiters);
        int maxCans = Math.Min(cansNeeded, 10);

        // Zentriert
        float totalW = maxCans * canSize + (maxCans - 1) * spacing;
        float cx = startX + availWidth / 2f - totalW / 2f;

        var canColor = SkiaThemeHelper.Accent;
        _canStroke.Color = SkiaThemeHelper.AdjustBrightness(canColor, 0.6f);

        for (int i = 0; i < maxCans; i++)
        {
            float x = cx + i * (canSize + spacing);
            float y = canY;

            // Kannen-Körper (Rechteck mit abgerundeten Ecken)
            float bodyW = canSize * 0.75f;
            float bodyH = canSize * 0.85f;
            float bodyX = x + (canSize - bodyW) / 2f;
            float bodyY = y + canSize * 0.15f;

            _canPaint.Color = canColor;
            canvas.DrawRoundRect(new SKRect(bodyX, bodyY, bodyX + bodyW, bodyY + bodyH),
                2f, 2f, _canPaint);
            canvas.DrawRoundRect(new SKRect(bodyX, bodyY, bodyX + bodyW, bodyY + bodyH),
                2f, 2f, _canStroke);

            // Henkel oben
            var henkelRect = new SKRect(
                x + canSize * 0.3f, y,
                x + canSize * 0.7f, y + canSize * 0.25f);
            using var henkelPath = new SKPath();
            henkelPath.AddArc(henkelRect, 180f, 180f);
            canvas.DrawPath(henkelPath, _canStroke);

            // Farbspiegel (halbtransparent, obere Hälfte heller)
            _canPaint.Color = SkiaThemeHelper.AdjustBrightness(canColor, 1.3f).WithAlpha(80);
            canvas.DrawRect(bodyX + 1f, bodyY + 1f, bodyW - 2f, bodyH * 0.35f, _canPaint);
        }

        // "×N" Anzeige wenn mehr als 10
        if (cansNeeded > 10)
        {
            float labelX = cx + totalW + 6f;
            _textPaint.Color = SkiaThemeHelper.TextSecondary;
            _canFont.Size = 9f;
            canvas.DrawText($"... ×{cansNeeded}", labelX, canY + canSize / 2f + 3f,
                SKTextAlign.Left, _canFont, _textPaint);
        }

        // Kannengrößen-Info
        _textPaint.Color = SkiaThemeHelper.TextMuted;
        _canFont.Size = 7f;
        canvas.DrawText($"({canLiters:F1}L/Kanne)", startX + availWidth / 2f, canY + canSize + 10f,
            SKTextAlign.Center, _canFont, _textPaint);
    }
}
