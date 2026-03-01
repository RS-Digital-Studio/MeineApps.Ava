using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Wandschnitt mit CW/UW-Ständerwerk + Plattenaufteilung, optional doppelt beplankt.
/// </summary>
public static class DrywallVisualization
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

    private static readonly SKPaint _plateFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _profileFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _profileStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _wallStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _layerPaint = new() { IsAntialias = false };

    // Profile-Farben
    private static readonly SKColor _cwColor = new(0x78, 0x90, 0xA8); // CW = stahlblau
    private static readonly SKColor _uwColor = new(0x60, 0x80, 0x60); // UW = stahlgrün
    private static readonly SKColor _plateColor = new(0xE8, 0xE0, 0xD0); // Gipskarton

    public static void Render(SKCanvas canvas, SKRect bounds,
        float wallLengthM, float wallHeightM, bool doublePlated,
        int cwProfiles, int plates, bool hasResult)
    {
        if (!hasResult || wallLengthM <= 0 || wallHeightM <= 0) return;

        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        SkiaBlueprintCanvas.DrawGrid(canvas, bounds, 20f);

        // Global Alpha Fade-In
        _layerPaint.Color = SKColors.White.WithAlpha((byte)(255 * progress));
        canvas.SaveLayer(_layerPaint);

        var fit = SkiaBlueprintCanvas.FitToCanvas(bounds, wallLengthM, wallHeightM, 40f);
        float scale = fit.Scale;
        float ox = fit.OffsetX;
        float oy = fit.OffsetY;

        float ww = wallLengthM * scale;
        float wh = wallHeightM * scale;

        // CW-Profile (alle 62.5cm = 0.625m)
        float cwSpacing = 0.625f * scale;
        float cwWidth = 6f; // Darstellungsbreite
        float plateThick = doublePlated ? 8f : 5f;

        // UW-Profile oben und unten
        _profileFill.Color = SkiaThemeHelper.WithAlpha(_uwColor, 150);
        float uwHeight = 4f;

        // UW oben
        canvas.DrawRect(ox, oy, ww, uwHeight, _profileFill);
        _profileStroke.Color = _uwColor;
        canvas.DrawRect(ox, oy, ww, uwHeight, _profileStroke);

        // UW unten
        canvas.DrawRect(ox, oy + wh - uwHeight, ww, uwHeight, _profileFill);
        canvas.DrawRect(ox, oy + wh - uwHeight, ww, uwHeight, _profileStroke);

        // CW-Profile (vertikal)
        _profileFill.Color = SkiaThemeHelper.WithAlpha(_cwColor, 150);
        _profileStroke.Color = _cwColor;

        int cwCount = Math.Max(2, (int)(ww / cwSpacing) + 1);
        for (int i = 0; i < cwCount; i++)
        {
            float x = ox + i * cwSpacing;
            if (x > ox + ww - cwWidth / 2f) break;

            canvas.DrawRect(x - cwWidth / 2f, oy + uwHeight, cwWidth, wh - 2 * uwHeight, _profileFill);
            canvas.DrawRect(x - cwWidth / 2f, oy + uwHeight, cwWidth, wh - 2 * uwHeight, _profileStroke);
        }

        // Gipskarton-Platten (125cm = 1.25m Breite)
        float plateSpacing = 1.25f * scale;
        int plateCount = (int)Math.Ceiling(ww / plateSpacing);

        // Vordere Beplankung (rechts der Profile dargestellt als Streifen)
        _plateFill.Color = SkiaThemeHelper.WithAlpha(_plateColor, 80);
        canvas.DrawRect(ox - plateThick, oy, plateThick, wh, _plateFill);

        // Plattentrennlinien
        for (int i = 1; i < plateCount; i++)
        {
            float y = oy + i * plateSpacing;
            if (y > oy + wh) break;
            // Vertikale Plattengrenze als gestrichelte Linie
            float px = ox + i * plateSpacing;
            if (px < ox + ww)
            {
                SkiaBlueprintCanvas.DrawDashedLine(canvas,
                    new SKPoint(px, oy), new SKPoint(px, oy + wh),
                    SkiaThemeHelper.WithAlpha(_plateColor, 180), 4f, 3f);
            }
        }

        // Hintere Beplankung
        _plateFill.Color = SkiaThemeHelper.WithAlpha(_plateColor, 60);
        canvas.DrawRect(ox + ww, oy, plateThick, wh, _plateFill);

        if (doublePlated)
        {
            // Zweite Schicht
            _plateFill.Color = SkiaThemeHelper.WithAlpha(_plateColor, 50);
            canvas.DrawRect(ox - plateThick * 2f, oy, plateThick, wh, _plateFill);
            canvas.DrawRect(ox + ww + plateThick, oy, plateThick, wh, _plateFill);
        }

        // Wand-Umriss
        _wallStroke.Color = SkiaThemeHelper.TextPrimary;
        canvas.DrawRect(ox, oy, ww, wh, _wallStroke);

        // Maßlinien
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox, oy + wh + 4f), new SKPoint(ox + ww, oy + wh + 4f),
            $"{wallLengthM:F2} m", offset: 14f);

        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox - plateThick - (doublePlated ? plateThick : 0) - 4f, oy),
            new SKPoint(ox - plateThick - (doublePlated ? plateThick : 0) - 4f, oy + wh),
            $"{wallHeightM:F2} m", offset: -14f);

        // Legende
        float legendY = oy - 8f;
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{cwProfiles} CW | {plates} Platten" + (doublePlated ? " (2×)" : ""),
            new SKPoint(ox + ww / 2f, legendY),
            SkiaThemeHelper.TextSecondary, 9f);

        canvas.Restore();
    }
}
