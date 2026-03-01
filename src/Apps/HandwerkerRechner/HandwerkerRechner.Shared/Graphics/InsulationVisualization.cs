using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Wandquerschnitt mit Dämmschicht - Dicke proportional, farblich je nach Dämmstofftyp, animiert.
/// Links: Mauerwerk (braun/grau), rechts: Dämmschicht (EPS weiß, XPS blau, Mineralwolle gelb, Holzfaser braun).
/// </summary>
public static class InsulationVisualization
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
    private static readonly SKPaint _insulationFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _texturePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _layerPaint = new() { IsAntialias = false };
    private static readonly Random _rng = new(42);

    // Farben: Mauerwerk
    private static readonly SKColor _wallColor = new(0x9C, 0x8B, 0x7A);

    // Farben: Dämmstofftypen
    private static readonly SKColor _epsColor = new(0xF5, 0xF5, 0xF5);          // EPS (Styropor) - Weiß
    private static readonly SKColor _xpsColor = new(0xB3, 0xE5, 0xFC);          // XPS - Hellblau
    private static readonly SKColor _mineralWoolColor = new(0xFF, 0xF9, 0xC4);  // Mineralwolle - Gelb
    private static readonly SKColor _woodFiberColor = new(0xD7, 0xCC, 0xC8);    // Holzfaser - Braun

    public static void Render(SKCanvas canvas, SKRect bounds,
        float areaSqm, float thicknessCm, int insulationType, float lambda)
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

        // Wand-Querschnitt (Seitenansicht)
        float wallW = availW * 0.4f;
        float wallH = availH * 0.8f;
        float wallX = bounds.Left + margin + (availW - wallW) * 0.25f;
        float wallY = bounds.Top + margin + (availH - wallH) * 0.5f;

        // Mauerwerk zeichnen
        _wallFill.Color = SkiaThemeHelper.WithAlpha(_wallColor, 180);
        canvas.DrawRect(wallX, wallY, wallW, wallH, _wallFill);

        // Mauerwerk-Textur (horizontale Fugenlinien + versetzte vertikale Fugen)
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

        // Dämmschicht rechts auf der Wand (proportional zur Dicke)
        float maxInsulW = availW * 0.35f;
        // Normalisierung: 1cm = dünn, 30cm = max
        float normalizedThickness = Math.Clamp(thicknessCm / 30f, 0.1f, 1f);
        float insulW = maxInsulW * normalizedThickness * progress;

        SKColor insulColor = insulationType switch
        {
            1 => _xpsColor,
            2 => _mineralWoolColor,
            3 => _woodFiberColor,
            _ => _epsColor
        };
        _insulationFill.Color = SkiaThemeHelper.WithAlpha(insulColor, 200);
        canvas.DrawRect(wallX + wallW, wallY, insulW, wallH, _insulationFill);

        // Dämmstoff-Textur je nach Typ
        DrawInsulationTexture(canvas, wallX + wallW, wallY, insulW, wallH, insulationType);

        // Umriss Gesamtquerschnitt
        _strokePaint.Color = SkiaThemeHelper.TextSecondary;
        canvas.DrawRect(wallX, wallY, wallW + insulW, wallH, _strokePaint);

        // Trennlinie Wand/Dämmung
        canvas.DrawLine(wallX + wallW, wallY, wallX + wallW, wallY + wallH, _strokePaint);

        // Bemaßung Dämmdicke (oben)
        if (insulW > 4f)
        {
            SkiaBlueprintCanvas.DrawDimensionLine(canvas,
                new SKPoint(wallX + wallW, wallY - 4f),
                new SKPoint(wallX + wallW + insulW, wallY - 4f),
                $"{thicknessCm:F0} cm", offset: 14f);
        }

        // Info-Text unten: Stückzahl + Lambda
        int pieces = (int)Math.Ceiling(areaSqm / 0.72);
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{pieces} Platten  |  \u03bb = {lambda:F3}",
            new SKPoint(bounds.MidX, bounds.Bottom - margin + 10f),
            SkiaThemeHelper.TextSecondary, 10f);

        canvas.Restore();
    }

    /// <summary>
    /// Zeichnet Typ-spezifische Texturen in die Dämmschicht
    /// </summary>
    private static void DrawInsulationTexture(SKCanvas canvas, float x, float y, float w, float h, int insulationType)
    {
        if (w < 4f || h < 4f) return;

        // Deterministische Positionen

        switch (insulationType)
        {
            case 0: // EPS: Kleine Kreise (Styropor-Kügelchen)
                _texturePaint.Color = SkiaThemeHelper.WithAlpha(new SKColor(0xDD, 0xDD, 0xDD), 100);
                for (int i = 0; i < 30; i++)
                {
                    float cx = x + 2f + (float)_rng.NextDouble() * (w - 4f);
                    float cy = y + 2f + (float)_rng.NextDouble() * (h - 4f);
                    float r = 1.2f + (float)_rng.NextDouble() * 1.5f;
                    canvas.DrawCircle(cx, cy, r, _texturePaint);
                }
                break;

            case 1: // XPS: Horizontale feine Linien (Extrudierte Struktur)
                _strokePaint.Color = SkiaThemeHelper.WithAlpha(new SKColor(0x81, 0xD4, 0xFA), 60);
                float lineSpacing = h / 12f;
                for (int i = 1; i < 12; i++)
                {
                    float ly = y + i * lineSpacing;
                    canvas.DrawLine(x + 2f, ly, x + w - 2f, ly, _strokePaint);
                }
                _strokePaint.Color = SkiaThemeHelper.TextSecondary; // Zurücksetzen
                break;

            case 2: // Mineralwolle: Wellenlinien (Faserstruktur)
                _strokePaint.Color = SkiaThemeHelper.WithAlpha(new SKColor(0xFF, 0xE0, 0x82), 80);
                for (int i = 0; i < 8; i++)
                {
                    float wy = y + 8f + (float)_rng.NextDouble() * (h - 16f);
                    using var path = new SKPath();
                    path.MoveTo(x + 2f, wy);
                    for (float wx = x + 6f; wx < x + w - 2f; wx += 8f)
                    {
                        float offset = ((int)((wx - x) / 8f) % 2 == 0) ? -2f : 2f;
                        path.LineTo(wx, wy + offset);
                    }
                    canvas.DrawPath(path, _strokePaint);
                }
                _strokePaint.Color = SkiaThemeHelper.TextSecondary;
                break;

            case 3: // Holzfaser: Vertikale kurze Striche (Faserstruktur)
                _strokePaint.Color = SkiaThemeHelper.WithAlpha(new SKColor(0xA1, 0x88, 0x7F), 80);
                for (int i = 0; i < 20; i++)
                {
                    float fx = x + 3f + (float)_rng.NextDouble() * (w - 6f);
                    float fy = y + 3f + (float)_rng.NextDouble() * (h - 10f);
                    float fLen = 3f + (float)_rng.NextDouble() * 5f;
                    canvas.DrawLine(fx, fy, fx, fy + fLen, _strokePaint);
                }
                _strokePaint.Color = SkiaThemeHelper.TextSecondary;
                break;
        }
    }
}
