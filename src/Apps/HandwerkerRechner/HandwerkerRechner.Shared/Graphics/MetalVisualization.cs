using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// 2 Sub: Profil-Querschnitte maßstabsgetreu, Gewindebohrung als konzentrische Kreise.
/// </summary>
public static class MetalVisualization
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

    private static readonly SKPaint _metalFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _metalStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _holeFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _layerPaint = new() { IsAntialias = false };

    // Metall-Farben
    private static readonly SKColor _steelColor = new(0xA0, 0xA0, 0xAA);
    private static readonly SKColor _aluminumColor = new(0xC8, 0xC8, 0xD0);
    private static readonly SKColor _copperColor = new(0xD4, 0x8A, 0x54);
    private static readonly SKColor _brassColor = new(0xD4, 0xAA, 0x44);

    /// <summary>
    /// subType: 0=Profil-Querschnitt, 1=Gewindebohrung
    /// profileType: 0=Rundstab, 1=Flachstab, 2=Vierkantstab, 3=Rundrohr, 4=Vierkantrohr, 5=Winkel
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds, int subType,
        int metalType, int profileType, float dim1Mm, float dim2Mm, float wallThickMm,
        string threadSize, float drillSizeMm,
        float weightKg, bool hasResult)
    {
        if (!hasResult) return;

        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        // Global Alpha Fade-In
        _layerPaint.Color = SKColors.White.WithAlpha((byte)(255 * progress));
        canvas.SaveLayer(_layerPaint);

        switch (subType)
        {
            case 0: RenderProfile(canvas, bounds, metalType, profileType, dim1Mm, dim2Mm, wallThickMm, weightKg); break;
            case 1: RenderThread(canvas, bounds, threadSize, drillSizeMm); break;
        }

        canvas.Restore();
    }

    private static SKColor GetMetalColor(int metalType)
    {
        return metalType switch
        {
            0 or 1 => _steelColor,
            2 => _aluminumColor,
            3 => _copperColor,
            4 or 5 => _brassColor,
            _ => _steelColor
        };
    }

    private static void RenderProfile(SKCanvas canvas, SKRect bounds,
        int metalType, int profileType, float dim1, float dim2, float wallThick, float weightKg)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float maxDim = Math.Max(dim1, dim2);
        if (maxDim <= 0) return;

        float availSize = Math.Min(bounds.Width, bounds.Height) - 80f;
        float scale = availSize / maxDim;
        scale = Math.Min(scale, 4f); // Nicht zu groß

        SKColor color = GetMetalColor(metalType);
        _metalFill.Color = SkiaThemeHelper.WithAlpha(color, 160);
        _metalStroke.Color = color;

        switch (profileType)
        {
            case 0: // Rundstab
                float r = dim1 / 2f * scale;
                canvas.DrawCircle(cx, cy, r, _metalFill);
                canvas.DrawCircle(cx, cy, r, _metalStroke);
                SkiaBlueprintCanvas.DrawDimensionLine(canvas,
                    new SKPoint(cx - r, cy + r + 8f), new SKPoint(cx + r, cy + r + 8f),
                    $"Ø {dim1:F1} mm", offset: 10f);
                break;

            case 1: // Flachstab
                float fw = dim1 * scale;
                float fh = dim2 * scale;
                canvas.DrawRect(cx - fw / 2f, cy - fh / 2f, fw, fh, _metalFill);
                canvas.DrawRect(cx - fw / 2f, cy - fh / 2f, fw, fh, _metalStroke);
                SkiaBlueprintCanvas.DrawDimensionLine(canvas,
                    new SKPoint(cx - fw / 2f, cy + fh / 2f + 4f),
                    new SKPoint(cx + fw / 2f, cy + fh / 2f + 4f),
                    $"{dim1:F1}", offset: 10f);
                SkiaBlueprintCanvas.DrawDimensionLine(canvas,
                    new SKPoint(cx + fw / 2f + 4f, cy - fh / 2f),
                    new SKPoint(cx + fw / 2f + 4f, cy + fh / 2f),
                    $"{dim2:F1}", offset: 10f);
                break;

            case 2: // Vierkantstab
                float sq = dim1 * scale;
                canvas.DrawRect(cx - sq / 2f, cy - sq / 2f, sq, sq, _metalFill);
                canvas.DrawRect(cx - sq / 2f, cy - sq / 2f, sq, sq, _metalStroke);
                SkiaBlueprintCanvas.DrawDimensionLine(canvas,
                    new SKPoint(cx - sq / 2f, cy + sq / 2f + 4f),
                    new SKPoint(cx + sq / 2f, cy + sq / 2f + 4f),
                    $"{dim1:F1} mm", offset: 10f);
                break;

            case 3: // Rundrohr
                float outerR = dim1 / 2f * scale;
                float innerR = (dim1 / 2f - wallThick) * scale;
                innerR = Math.Max(innerR, 1f);
                canvas.DrawCircle(cx, cy, outerR, _metalFill);
                _holeFill.Color = SkiaThemeHelper.Background;
                canvas.DrawCircle(cx, cy, innerR, _holeFill);
                canvas.DrawCircle(cx, cy, outerR, _metalStroke);
                canvas.DrawCircle(cx, cy, innerR, _metalStroke);
                SkiaBlueprintCanvas.DrawDimensionLine(canvas,
                    new SKPoint(cx - outerR, cy + outerR + 8f),
                    new SKPoint(cx + outerR, cy + outerR + 8f),
                    $"Ø {dim1:F1} mm", offset: 10f);
                break;

            case 4: // Vierkantrohr
                float outerW = dim1 * scale;
                float innerW = (dim1 - 2 * wallThick) * scale;
                innerW = Math.Max(innerW, 1f);
                canvas.DrawRect(cx - outerW / 2f, cy - outerW / 2f, outerW, outerW, _metalFill);
                _holeFill.Color = SkiaThemeHelper.Background;
                canvas.DrawRect(cx - innerW / 2f, cy - innerW / 2f, innerW, innerW, _holeFill);
                canvas.DrawRect(cx - outerW / 2f, cy - outerW / 2f, outerW, outerW, _metalStroke);
                canvas.DrawRect(cx - innerW / 2f, cy - innerW / 2f, innerW, innerW, _metalStroke);
                SkiaBlueprintCanvas.DrawDimensionLine(canvas,
                    new SKPoint(cx - outerW / 2f, cy + outerW / 2f + 4f),
                    new SKPoint(cx + outerW / 2f, cy + outerW / 2f + 4f),
                    $"{dim1:F1} mm", offset: 10f);
                break;

            case 5: // Winkel-Profil (L)
            {
                float aw = dim1 * scale;
                float ah = dim2 * scale;
                float at = wallThick * scale;
                at = Math.Max(at, 2f);

                using var anglePath = new SKPath();
                anglePath.MoveTo(cx - aw / 2f, cy + ah / 2f);
                anglePath.LineTo(cx - aw / 2f, cy - ah / 2f);
                anglePath.LineTo(cx - aw / 2f + at, cy - ah / 2f);
                anglePath.LineTo(cx - aw / 2f + at, cy + ah / 2f - at);
                anglePath.LineTo(cx + aw / 2f, cy + ah / 2f - at);
                anglePath.LineTo(cx + aw / 2f, cy + ah / 2f);
                anglePath.Close();

                canvas.DrawPath(anglePath, _metalFill);
                canvas.DrawPath(anglePath, _metalStroke);
                SkiaBlueprintCanvas.DrawDimensionLine(canvas,
                    new SKPoint(cx - aw / 2f, cy + ah / 2f + 4f),
                    new SKPoint(cx + aw / 2f, cy + ah / 2f + 4f),
                    $"{dim1:F1}", offset: 10f);
                break;
            }
        }

        // Gewicht
        if (weightKg > 0)
        {
            SkiaBlueprintCanvas.DrawMeasurementText(canvas,
                $"{weightKg:F2} kg",
                new SKPoint(cx, bounds.Bottom - 10f),
                SkiaThemeHelper.Accent, 11f);
        }
    }

    private static void RenderThread(SKCanvas canvas, SKRect bounds,
        string threadSize, float drillSizeMm)
    {
        if (drillSizeMm <= 0) return;

        float cx = bounds.MidX;
        float cy = bounds.MidY;

        // Gewinde-Außendurchmesser aus ThreadSize extrahieren (M6 → 6mm)
        float threadDiameter = drillSizeMm * 1.2f; // Approximation

        float availSize = Math.Min(bounds.Width, bounds.Height) - 80f;
        float scale = availSize / (threadDiameter * 2f);
        scale = Math.Min(scale, 8f);

        float outerR = threadDiameter / 2f * scale;
        float innerR = drillSizeMm / 2f * scale;

        // Außenkreis (Gewinde)
        _metalFill.Color = SkiaThemeHelper.WithAlpha(_steelColor, 80);
        canvas.DrawCircle(cx, cy, outerR, _metalFill);
        _metalStroke.Color = _steelColor;
        canvas.DrawCircle(cx, cy, outerR, _metalStroke);

        // Gewinde-Schraffur (konzentrische gestrichelte Kreise)
        _metalStroke.StrokeWidth = 0.8f;
        float step = (outerR - innerR) / 4f;
        for (float r = innerR + step; r < outerR; r += step)
        {
            _metalStroke.Color = SkiaThemeHelper.WithAlpha(_steelColor, 60);
            canvas.DrawCircle(cx, cy, r, _metalStroke);
        }
        _metalStroke.StrokeWidth = 2f;

        // Kernloch (innerer Kreis)
        _holeFill.Color = SkiaThemeHelper.Background;
        canvas.DrawCircle(cx, cy, innerR, _holeFill);
        _metalStroke.Color = SkiaThemeHelper.Accent;
        canvas.DrawCircle(cx, cy, innerR, _metalStroke);

        // Fadenkreuz
        _metalStroke.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 60);
        _metalStroke.StrokeWidth = 0.5f;
        canvas.DrawLine(cx - outerR - 5f, cy, cx + outerR + 5f, cy, _metalStroke);
        canvas.DrawLine(cx, cy - outerR - 5f, cx, cy + outerR + 5f, _metalStroke);
        _metalStroke.StrokeWidth = 2f;

        // Maßlinien
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(cx - outerR, cy + outerR + 10f),
            new SKPoint(cx + outerR, cy + outerR + 10f),
            threadSize, offset: 10f);

        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(cx - innerR, cy - outerR - 10f),
            new SKPoint(cx + innerR, cy - outerR - 10f),
            $"Ø {drillSizeMm:F1} mm", offset: -10f);
    }
}
