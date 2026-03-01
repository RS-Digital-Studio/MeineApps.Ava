using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Wandquerschnitt mit sichtbarer Putzschicht - Dicke proportional zum Einstellwert, animiert.
/// </summary>
public static class PlasterVisualization
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

    // Gecachte Paints
    private static readonly SKPaint _wallFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _plasterFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _layerPaint = new() { IsAntialias = false };

    // Farben
    private static readonly SKColor _wallColor = new(0x9C, 0x8B, 0x7A);    // Mauerwerk
    private static readonly SKColor _plasterInnen = new(0xF5, 0xF0, 0xE8);  // Innenputz (hell)
    private static readonly SKColor _plasterAußen = new(0xD4, 0xC8, 0xB8);  // Außenputz (dunkler)
    private static readonly SKColor _plasterKalk = new(0xFA, 0xF7, 0xF0);   // Kalkputz (weiß)
    private static readonly SKColor _plasterGips = new(0xF0, 0xE8, 0xE0);   // Gipsputz

    public static void Render(SKCanvas canvas, SKRect bounds,
        float areaSqm, float thicknessMm, int plasterType, int bagsNeeded)
    {
        if (areaSqm <= 0 || thicknessMm <= 0) return;

        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        SkiaBlueprintCanvas.DrawGrid(canvas, bounds, 20f);

        // Global Alpha Fade-In
        _layerPaint.Color = SKColors.White.WithAlpha((byte)(255 * progress));
        canvas.SaveLayer(_layerPaint);

        float margin = 40f;
        float availW = bounds.Width - 2 * margin;
        float availH = bounds.Height - 2 * margin;

        // Wand-Querschnitt (Seitenansicht)
        float wallW = availW * 0.5f;
        float wallH = availH * 0.8f;
        float wallX = bounds.Left + margin + (availW - wallW) * 0.3f;
        float wallY = bounds.Top + margin + (availH - wallH) * 0.5f;

        // Mauerwerk zeichnen
        _wallFill.Color = SkiaThemeHelper.WithAlpha(_wallColor, 180);
        canvas.DrawRect(wallX, wallY, wallW, wallH, _wallFill);

        // Mauerwerk-Textur (horizontale Fugenlinien)
        _strokePaint.Color = SkiaThemeHelper.WithAlpha(new SKColor(0x80, 0x70, 0x60), 100);
        float brickH = wallH / 8f;
        for (int i = 1; i < 8; i++)
        {
            float y = wallY + i * brickH;
            canvas.DrawLine(wallX, y, wallX + wallW, y, _strokePaint);

            // Versetzte vertikale Fugen (Mauerwerk-Verband)
            float offset = (i % 2 == 0) ? 0 : wallW * 0.25f;
            for (float x = wallX + offset; x < wallX + wallW; x += wallW * 0.5f)
            {
                float prevY = wallY + (i - 1) * brickH;
                canvas.DrawLine(x, prevY, x, y, _strokePaint);
            }
        }

        // Putzschicht rechts auf der Wand
        float maxPlasterW = availW * 0.25f;
        float plasterW = Math.Min(maxPlasterW, thicknessMm / 50f * maxPlasterW) * progress;
        SKColor plasterColor = plasterType switch
        {
            1 => _plasterAußen,
            2 => _plasterKalk,
            3 => _plasterGips,
            _ => _plasterInnen
        };
        _plasterFill.Color = SkiaThemeHelper.WithAlpha(plasterColor, 200);
        canvas.DrawRect(wallX + wallW, wallY, plasterW, wallH, _plasterFill);

        // Umriss
        _strokePaint.Color = SkiaThemeHelper.TextSecondary;
        canvas.DrawRect(wallX, wallY, wallW + plasterW, wallH, _strokePaint);

        // Trennlinie Wand/Putz
        canvas.DrawLine(wallX + wallW, wallY, wallX + wallW, wallY + wallH, _strokePaint);

        // Bemaßung Putzdicke
        if (plasterW > 2f)
        {
            SkiaBlueprintCanvas.DrawDimensionLine(canvas,
                new SKPoint(wallX + wallW, wallY - 4f),
                new SKPoint(wallX + wallW + plasterW, wallY - 4f),
                $"{thicknessMm:F0} mm", offset: 14f);
        }

        // Info-Text unten
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{bagsNeeded} \u00d7 30 kg",
            new SKPoint(bounds.MidX, bounds.Bottom - margin + 10f),
            SkiaThemeHelper.TextSecondary, 10f);

        canvas.Restore();
    }
}
