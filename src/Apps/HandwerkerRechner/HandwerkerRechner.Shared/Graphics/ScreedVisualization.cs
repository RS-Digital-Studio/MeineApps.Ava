using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Bodenquerschnitt mit Estrichschicht-Profil - Schichthöhe proportional, animiert.
/// Untergrund (dunkelgrau), darüber Estrichschicht (farblich nach Typ), Bemaßung, Sack-Info.
/// </summary>
public static class ScreedVisualization
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
    private static readonly SKPaint _subfloorFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _screedFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _texturePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _layerPaint = new() { IsAntialias = false };
    private static readonly Random _rng = new(42);

    // Farben: Untergrund
    private static readonly SKColor _subfloorColor = new(0x4B, 0x55, 0x63);    // Dunkelgrau (Rohboden)
    private static readonly SKColor _gravelColor = new(0x6B, 0x72, 0x80);      // Schotter/Kies

    // Farben: Estrich-Typen
    private static readonly SKColor _screedZement = new(0x9C, 0xA3, 0xAF);     // Grau (Zementestrich)
    private static readonly SKColor _screedFlow = new(0xD1, 0xD5, 0xDB);       // Hellgrau/Glatt (Fließestrich)
    private static readonly SKColor _screedAnhydrit = new(0xD4, 0xC8, 0xA8);   // Beige (Anhydritestrich)

    public static void Render(SKCanvas canvas, SKRect bounds,
        float areaSqm, float thicknessCm, int screedType, int bagsNeeded)
    {
        if (areaSqm <= 0 || thicknessCm <= 0) return;

        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        SkiaBlueprintCanvas.DrawGrid(canvas, bounds, 20f);

        // Global Alpha Fade-In
        _layerPaint.Color = SKColors.White.WithAlpha((byte)(255 * progress));
        canvas.SaveLayer(_layerPaint);

        float margin = 40f;
        float availW = bounds.Width - 2 * margin;
        float availH = bounds.Height - 2 * margin;

        // Gesamtschnitt-Bereich (Seitenansicht des Bodenaufbaus)
        float sectionW = availW * 0.75f;
        float sectionH = availH * 0.7f;
        float sectionX = bounds.Left + margin + (availW - sectionW) * 0.5f;
        float sectionY = bounds.Top + margin + (availH - sectionH) * 0.35f;

        // Schichthöhen: Untergrund (Kies) = 40%, Estrich proportional zur Dicke (max 60%)
        float subfloorH = sectionH * 0.4f;
        float maxScreedH = sectionH * 0.6f;
        // Dicke normalisiert: 3cm = klein, 10cm = max
        float normalizedThickness = Math.Clamp(thicknessCm / 10f, 0.15f, 1f);
        float screedH = maxScreedH * normalizedThickness * progress;

        float subfloorY = sectionY + sectionH - subfloorH;
        float screedY = subfloorY - screedH;

        // Untergrund/Kies zeichnen
        _subfloorFill.Color = SkiaThemeHelper.WithAlpha(_subfloorColor, 180);
        canvas.DrawRect(sectionX, subfloorY, sectionW, subfloorH, _subfloorFill);

        // Kies-Textur (kleine Kreise im Untergrund)
        _texturePaint.Color = SkiaThemeHelper.WithAlpha(_gravelColor, 120);
        for (int i = 0; i < 20; i++)
        {
            float gx = sectionX + (float)_rng.NextDouble() * sectionW;
            float gy = subfloorY + 4f + (float)_rng.NextDouble() * (subfloorH - 8f);
            float gr = 2f + (float)_rng.NextDouble() * 3f;
            canvas.DrawCircle(gx, gy, gr, _texturePaint);
        }

        // Estrichschicht zeichnen
        SKColor screedColor = screedType switch
        {
            1 => _screedFlow,
            2 => _screedAnhydrit,
            _ => _screedZement
        };
        _screedFill.Color = SkiaThemeHelper.WithAlpha(screedColor, 210);
        canvas.DrawRect(sectionX, screedY, sectionW, screedH, _screedFill);

        // Bei Fließestrich: Glatte Oberfläche andeuten (horizontale helle Linie oben)
        if (screedType == 1 && screedH > 5f)
        {
            _strokePaint.Color = SkiaThemeHelper.WithAlpha(SKColors.White, 80);
            canvas.DrawLine(sectionX + 4f, screedY + 2f, sectionX + sectionW - 4f, screedY + 2f, _strokePaint);
        }

        // Umrisse
        _strokePaint.Color = SkiaThemeHelper.TextSecondary;
        canvas.DrawRect(sectionX, screedY, sectionW, screedH + subfloorH, _strokePaint);

        // Trennlinie zwischen Estrich und Untergrund
        canvas.DrawLine(sectionX, subfloorY, sectionX + sectionW, subfloorY, _strokePaint);

        // Bemaßung Estrichdicke (rechts)
        if (screedH > 4f)
        {
            SkiaBlueprintCanvas.DrawDimensionLine(canvas,
                new SKPoint(sectionX + sectionW + 8f, screedY),
                new SKPoint(sectionX + sectionW + 8f, subfloorY),
                $"{thicknessCm:F0} cm", offset: 14f);
        }

        // Info-Text unten: Sack-Info
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{bagsNeeded} \u00d7 40 kg",
            new SKPoint(bounds.MidX, bounds.Bottom - margin + 10f),
            SkiaThemeHelper.TextSecondary, 10f);

        canvas.Restore();
    }
}
