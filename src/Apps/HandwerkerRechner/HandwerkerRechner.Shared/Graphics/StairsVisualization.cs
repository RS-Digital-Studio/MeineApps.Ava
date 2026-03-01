using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Seitenansicht Treppenprofil: Stufen-Linie, Winkel-Arc, DIN-Farbcode, Maßlinien.
/// </summary>
public static class StairsVisualization
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

    private static readonly SKPaint _stairFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _stairStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _dinPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // DIN-Farben
    private static readonly SKColor _dinGreen = new(0x22, 0xC5, 0x5E);
    private static readonly SKColor _dinYellow = new(0xF5, 0x9E, 0x0B);
    private static readonly SKColor _dinRed = new(0xEF, 0x44, 0x44);

    public static void Render(SKCanvas canvas, SKRect bounds,
        int stepCount, float stepHeightCm, float treadDepthCm, float floorHeightCm,
        float angleDeg, bool isDinCompliant, bool isComfortable, bool hasResult)
    {
        if (!hasResult || stepCount <= 0 || stepHeightCm <= 0 || treadDepthCm <= 0) return;

        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        SkiaBlueprintCanvas.DrawGrid(canvas, bounds, 20f);

        float totalRunCm = stepCount * treadDepthCm;
        float totalRiseCm = floorHeightCm;

        var fit = SkiaBlueprintCanvas.FitToCanvas(bounds, totalRunCm, totalRiseCm, 45f);
        float scale = fit.Scale;
        float ox = fit.OffsetX;
        float oy = fit.OffsetY;

        float totalW = totalRunCm * scale;
        float totalH = totalRiseCm * scale;

        float stepW = treadDepthCm * scale;
        float stepH = stepHeightCm * scale;

        // DIN-Farbe bestimmen
        SKColor dinColor = isDinCompliant ? _dinGreen : (isComfortable ? _dinYellow : _dinRed);

        // Animation: Stufen bauen sich von unten auf
        int visibleSteps = (int)(stepCount * progress);

        // Stufen-Profil zeichnen (von unten-links nach oben-rechts)
        using var stairPath = new SKPath();
        float baseY = oy + totalH; // Unten

        stairPath.MoveTo(ox, baseY);

        for (int i = 0; i < visibleSteps; i++)
        {
            float x = ox + i * stepW;
            float y = baseY - (i + 1) * stepH;

            // Vertikale (Setzstufe)
            stairPath.LineTo(x, y + stepH);
            stairPath.LineTo(x, y);

            // Horizontale (Trittstufe)
            stairPath.LineTo(x + stepW, y);
        }

        // Nach unten schließen
        if (visibleSteps > 0)
            stairPath.LineTo(ox + visibleSteps * stepW, baseY);
        stairPath.Close();

        // Füllung mit DIN-Farbe
        _stairFill.Color = SkiaThemeHelper.WithAlpha(dinColor, 40);
        canvas.DrawPath(stairPath, _stairFill);

        // Stufen-Umriss
        _stairStroke.Color = SkiaThemeHelper.TextPrimary;
        canvas.DrawPath(stairPath, _stairStroke);

        // Einzelne Stufen-Linien (nur sichtbare Stufen)
        _stairStroke.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextSecondary, 100);
        _stairStroke.StrokeWidth = 1f;
        for (int i = 0; i < visibleSteps; i++)
        {
            float x = ox + i * stepW;
            float y = baseY - (i + 1) * stepH;
            canvas.DrawLine(x, y, x + stepW, y, _stairStroke);
            canvas.DrawLine(x, y, x, y + stepH, _stairStroke);
        }
        _stairStroke.StrokeWidth = 2f;

        // Steigungslinie (diagonal)
        SkiaBlueprintCanvas.DrawDashedLine(canvas,
            new SKPoint(ox, baseY), new SKPoint(ox + totalW, oy),
            SkiaThemeHelper.WithAlpha(dinColor, 150), 6f, 4f);

        // Winkel-Arc
        float arcRadius = Math.Min(totalW, totalH) * 0.25f;
        SkiaBlueprintCanvas.DrawAngleArc(canvas,
            new SKPoint(ox, baseY), arcRadius,
            -angleDeg, angleDeg,
            $"{angleDeg:F1}°", dinColor);

        // Maßlinien
        // Geschosshöhe (links)
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox - 4f, baseY), new SKPoint(ox - 4f, oy),
            $"{floorHeightCm:F0} cm", offset: -14f);

        // Lauflänge (unten)
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox, baseY + 4f), new SKPoint(ox + totalW, baseY + 4f),
            $"{totalRunCm:F0} cm", offset: 14f);

        // Steigung einer Stufe (bei Stufe 2)
        if (stepCount >= 2 && stepW > 20f && stepH > 10f)
        {
            float sx = ox + stepW;
            float sy = baseY - 2 * stepH;
            SkiaBlueprintCanvas.DrawMeasurementText(canvas,
                $"{stepHeightCm:F1}/{treadDepthCm:F1}",
                new SKPoint(sx + stepW / 2f, sy + stepH / 2f),
                SkiaThemeHelper.TextMuted, 8f);
        }

        // DIN-Status
        string dinText = isDinCompliant ? "DIN OK" : (isComfortable ? "Grenzwertig" : "Nicht DIN-konform");
        _dinPaint.Color = dinColor;
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            dinText,
            new SKPoint(ox + totalW - 30f, oy + 10f),
            dinColor, 10f);
    }
}
