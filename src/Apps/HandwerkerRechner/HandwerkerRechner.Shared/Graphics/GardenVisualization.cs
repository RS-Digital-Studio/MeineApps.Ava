using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// 3 Sub: Pflastermuster+Fugen, Erdschichten-Profil, Teichfolie-Draufsicht.
/// </summary>
public static class GardenVisualization
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

    private static readonly SKPaint _stoneFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _stoneStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private static readonly SKPaint _soilFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _layerPaint = new() { IsAntialias = false };
    private static readonly Random _rng = new(42);

    // Erdfarben
    private static readonly SKColor _topsoilColor = new(0x5C, 0x3D, 0x2E); // Mutterboden
    private static readonly SKColor _sandColor = new(0xD4, 0xBE, 0x8C);     // Sand
    private static readonly SKColor _gravelColor = new(0x9C, 0x8C, 0x7C);   // Kies
    private static readonly SKColor _waterColor = new(0x22, 0x7F, 0xBB);     // Wasser

    /// <summary>
    /// subType: 0=Pflaster, 1=Erde, 2=Teichfolie
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds, int subType,
        float pavingArea, float stoneLengthCm, float stoneWidthCm, float jointWidthMm, int stonesNeeded,
        float soilArea, float soilDepthCm, int bagsNeeded,
        float pondLength, float pondWidth, float pondDepth, float overlap, float linerArea,
        bool hasResult)
    {
        if (!hasResult) return;

        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        SkiaBlueprintCanvas.DrawGrid(canvas, bounds, 20f);

        // Global Alpha Fade-In
        _layerPaint.Color = SKColors.White.WithAlpha((byte)(255 * progress));
        canvas.SaveLayer(_layerPaint);

        switch (subType)
        {
            case 0: RenderPaving(canvas, bounds, pavingArea, stoneLengthCm, stoneWidthCm, jointWidthMm, stonesNeeded); break;
            case 1: RenderSoil(canvas, bounds, soilArea, soilDepthCm, bagsNeeded); break;
            case 2: RenderPondLiner(canvas, bounds, pondLength, pondWidth, pondDepth, overlap, linerArea); break;
        }

        canvas.Restore();
    }

    /// <summary>
    /// Pflastersteine: Raster mit Fugenlinien.
    /// </summary>
    private static void RenderPaving(SKCanvas canvas, SKRect bounds,
        float pavingArea, float stoneLenCm, float stoneWidCm, float jointMm, int stonesNeeded)
    {
        if (pavingArea <= 0 || stoneLenCm <= 0 || stoneWidCm <= 0) return;

        float sideLen = MathF.Sqrt(pavingArea);
        var fit = SkiaBlueprintCanvas.FitToCanvas(bounds, sideLen, sideLen, 35f);
        float scale = fit.Scale;
        float ox = fit.OffsetX;
        float oy = fit.OffsetY;

        float areaW = sideLen * scale;
        float areaH = sideLen * scale;

        float sw = stoneLenCm / 100f * scale;
        float sh = stoneWidCm / 100f * scale;
        float jointW = jointMm / 1000f * scale;
        jointW = Math.Max(jointW, 0.5f);

        if (sw > 2f && sh > 2f)
        {
            int cols = (int)Math.Ceiling(areaW / (sw + jointW));
            int rows = (int)Math.Ceiling(areaH / (sh + jointW));

            canvas.Save();
            canvas.ClipRect(new SKRect(ox, oy, ox + areaW, oy + areaH));

            // Fugenhintergrund
            _soilFill.Color = SkiaThemeHelper.WithAlpha(_sandColor, 80);
            canvas.DrawRect(ox, oy, areaW, areaH, _soilFill);

            for (int row = 0; row < rows; row++)
            {
                float rowOffset = (row % 2) * (sw + jointW) / 2f; // Halbversatz
                for (int col = -1; col < cols + 1; col++)
                {
                    float x = ox + col * (sw + jointW) + rowOffset;
                    float y = oy + row * (sh + jointW);

                    // Leichte Farbvariation
                    byte r = (byte)(0x90 + _rng.Next(40));
                    byte g = (byte)(0x85 + _rng.Next(30));
                    byte b = (byte)(0x78 + _rng.Next(20));
                    _stoneFill.Color = new SKColor(r, g, b);

                    float drawW = Math.Min(sw, ox + areaW - x);
                    float drawH = Math.Min(sh, oy + areaH - y);
                    if (drawW > 0 && drawH > 0)
                    {
                        canvas.DrawRect(x, y, drawW, drawH, _stoneFill);
                    }
                }
            }

            canvas.Restore();
        }

        // Umriss
        _borderPaint.Color = SkiaThemeHelper.TextPrimary;
        canvas.DrawRect(ox, oy, areaW, areaH, _borderPaint);

        // Maßlinien
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox, oy + areaH + 4f), new SKPoint(ox + areaW, oy + areaH + 4f),
            $"{sideLen:F1} m", offset: 12f);

        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{stonesNeeded} Steine",
            new SKPoint(ox + areaW / 2f, oy - 8f),
            SkiaThemeHelper.Accent, 10f);
    }

    private static readonly SKFont _layerFont = new() { Size = 9f };
    private static readonly SKPaint _layerText = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKColor _grassColor = new(0x22, 0xC5, 0x5E); // Gras-Grün

    /// <summary>
    /// Erdschichten-Profil: Seitenansicht mit 3 Schichten (Mutterboden, Sand, Kies/Untergrund).
    /// Schicht-Labels rechts, Grasnarbe oben, Wurzel-Andeutungen.
    /// </summary>
    private static void RenderSoil(SKCanvas canvas, SKRect bounds,
        float soilArea, float soilDepthCm, int bagsNeeded)
    {
        if (soilArea <= 0 || soilDepthCm <= 0) return;

        float sideLen = MathF.Sqrt(soilArea);
        // 3 Schichten: Mutterboden (gewünscht), Sand-Schicht (30% Tiefe), Kies/Untergrund (Rest)
        float sandDepthCm = soilDepthCm * 0.3f;
        float gravelDepthCm = 15f; // Festwert für Untergrund
        float totalDepthCm = soilDepthCm + sandDepthCm + gravelDepthCm;

        var fit = SkiaBlueprintCanvas.FitToCanvas(bounds, sideLen, totalDepthCm / 100f, 45f);
        float scale = fit.Scale;
        float ox = fit.OffsetX;
        float oy = fit.OffsetY;

        float profileW = sideLen * scale;
        float soilH = soilDepthCm / 100f * scale;
        float sandH = sandDepthCm / 100f * scale;
        float gravelH = gravelDepthCm / 100f * scale;
        float totalH = soilH + sandH + gravelH;

        // === Grasnarbe (wellige grüne Linie oben) ===
        _borderPaint.Color = _grassColor;
        _borderPaint.StrokeWidth = 3f;
        using var grassPath = new SKPath();
        grassPath.MoveTo(ox, oy);
        int segments = (int)(profileW / 8f);
        for (int i = 1; i <= segments; i++)
        {
            float px = ox + i * (profileW / segments);
            float py = oy + MathF.Sin(i * 0.8f) * 2f; // Leichte Welle
            grassPath.LineTo(px, py);
        }
        canvas.DrawPath(grassPath, _borderPaint);
        _borderPaint.StrokeWidth = 2f;

        // Gras-Halme (kleine vertikale Striche)
        _stoneFill.Color = SkiaThemeHelper.WithAlpha(_grassColor, 150);
        for (int i = 0; i < 20; i++)
        {
            float gx = ox + (float)_rng.NextDouble() * profileW;
            float gh = 3f + (float)_rng.NextDouble() * 4f;
            canvas.DrawLine(gx, oy, gx, oy - gh, _borderPaint);
        }

        // === Schicht 1: Mutterboden (braun, mit Textur) ===
        _soilFill.Color = SkiaThemeHelper.WithAlpha(_topsoilColor, 200);
        canvas.DrawRect(ox, oy, profileW, soilH, _soilFill);

        // Wurzel-Andeutungen
        _stoneFill.Color = SkiaThemeHelper.WithAlpha(new SKColor(0x8B, 0x5E, 0x3C), 100);
        for (int i = 0; i < 5; i++)
        {
            float rx = ox + 15f + (float)_rng.NextDouble() * (profileW - 30f);
            float ry = oy + 4f;
            float rLen = 8f + (float)_rng.NextDouble() * (soilH * 0.6f);
            using var rootPath = new SKPath();
            rootPath.MoveTo(rx, ry);
            rootPath.QuadTo(rx + (float)(_rng.NextDouble() - 0.5) * 10f, ry + rLen * 0.5f,
                rx + (float)(_rng.NextDouble() - 0.5) * 8f, ry + rLen);
            _borderPaint.Color = SkiaThemeHelper.WithAlpha(new SKColor(0x8B, 0x5E, 0x3C), 60);
            _borderPaint.StrokeWidth = 1f;
            canvas.DrawPath(rootPath, _borderPaint);
            _borderPaint.StrokeWidth = 2f;
        }

        // Erd-Partikel (Punkte)
        _stoneFill.Color = SkiaThemeHelper.WithAlpha(_topsoilColor, 100);
        for (int i = 0; i < 30; i++)
        {
            float px = ox + (float)_rng.NextDouble() * profileW;
            float py = oy + (float)_rng.NextDouble() * soilH;
            canvas.DrawCircle(px, py, 1f + (float)_rng.NextDouble() * 1.5f, _stoneFill);
        }

        // === Schicht 2: Sand (gelb/beige) ===
        float sandY = oy + soilH;
        _soilFill.Color = SkiaThemeHelper.WithAlpha(_sandColor, 180);
        canvas.DrawRect(ox, sandY, profileW, sandH, _soilFill);

        // Feine Punkte als Sand-Textur
        _stoneFill.Color = SkiaThemeHelper.WithAlpha(_sandColor, 100);
        for (int i = 0; i < 40; i++)
        {
            float px = ox + (float)_rng.NextDouble() * profileW;
            float py = sandY + (float)_rng.NextDouble() * sandH;
            canvas.DrawCircle(px, py, 0.5f + (float)_rng.NextDouble() * 0.8f, _stoneFill);
        }

        // === Schicht 3: Kies/Untergrund (grau, Schraffur) ===
        float gravelY = sandY + sandH;
        _soilFill.Color = SkiaThemeHelper.WithAlpha(_gravelColor, 140);
        canvas.DrawRect(ox, gravelY, profileW, gravelH, _soilFill);

        SkiaBlueprintCanvas.DrawHatch(canvas,
            new SKRect(ox, gravelY, ox + profileW, gravelY + gravelH),
            45f, SkiaThemeHelper.WithAlpha(_gravelColor, 60), 6f);

        // Kleine Steine im Kies
        _stoneFill.Color = SkiaThemeHelper.WithAlpha(_gravelColor, 80);
        for (int i = 0; i < 15; i++)
        {
            float sx = ox + (float)_rng.NextDouble() * profileW;
            float sy = gravelY + (float)_rng.NextDouble() * gravelH;
            float sr = 1.5f + (float)_rng.NextDouble() * 2f;
            canvas.DrawOval(sx, sy, sr, sr * 0.7f, _stoneFill);
        }

        // === Trennlinien zwischen Schichten (gestrichelt) ===
        SkiaBlueprintCanvas.DrawDashedLine(canvas,
            new SKPoint(ox, sandY), new SKPoint(ox + profileW, sandY),
            SkiaThemeHelper.Warning, 4f, 3f);

        SkiaBlueprintCanvas.DrawDashedLine(canvas,
            new SKPoint(ox, gravelY), new SKPoint(ox + profileW, gravelY),
            SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 120), 4f, 3f);

        // === Umriss ===
        _borderPaint.Color = SkiaThemeHelper.TextPrimary;
        canvas.DrawRect(ox, oy, profileW, totalH, _borderPaint);

        // === Schicht-Labels (rechts außen) ===
        float labelX = ox + profileW + 8f;
        _layerText.Color = _topsoilColor;
        _layerFont.Size = 8f;

        canvas.DrawText("Mutterboden", labelX, oy + soilH / 2f + 3f,
            SKTextAlign.Left, _layerFont, _layerText);

        _layerText.Color = _sandColor;
        canvas.DrawText("Sand", labelX, sandY + sandH / 2f + 3f,
            SKTextAlign.Left, _layerFont, _layerText);

        _layerText.Color = _gravelColor;
        canvas.DrawText("Kies", labelX, gravelY + gravelH / 2f + 3f,
            SKTextAlign.Left, _layerFont, _layerText);

        // === Maßlinien ===
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox - 4f, oy), new SKPoint(ox - 4f, oy + soilH),
            $"{soilDepthCm:F0} cm", offset: -14f);

        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox, oy + totalH + 4f), new SKPoint(ox + profileW, oy + totalH + 4f),
            $"{sideLen:F1} m", offset: 12f);

        // === Säcke-Info ===
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{bagsNeeded} Säcke",
            new SKPoint(ox + profileW / 2f, oy + soilH / 2f),
            SkiaThemeHelper.TextPrimary, 11f);
    }

    /// <summary>
    /// Teich-Draufsicht mit Folie und Überlappung.
    /// </summary>
    private static void RenderPondLiner(SKCanvas canvas, SKRect bounds,
        float pondLength, float pondWidth, float pondDepth, float overlap, float linerArea)
    {
        if (pondLength <= 0 || pondWidth <= 0) return;

        // Folie ist größer als Teich (Tiefe + Überlappung auf jeder Seite)
        float linerW = pondLength + 2 * pondDepth + 2 * overlap;
        float linerH = pondWidth + 2 * pondDepth + 2 * overlap;

        var fit = SkiaBlueprintCanvas.FitToCanvas(bounds, linerW, linerH, 35f);
        float scale = fit.Scale;
        float ox = fit.OffsetX;
        float oy = fit.OffsetY;

        float flw = linerW * scale;
        float flh = linerH * scale;

        // Folie (gestrichelter Umriss)
        SkiaBlueprintCanvas.DrawDashedLine(canvas,
            new SKPoint(ox, oy), new SKPoint(ox + flw, oy),
            SkiaThemeHelper.TextMuted, 6f, 4f);
        SkiaBlueprintCanvas.DrawDashedLine(canvas,
            new SKPoint(ox + flw, oy), new SKPoint(ox + flw, oy + flh),
            SkiaThemeHelper.TextMuted, 6f, 4f);
        SkiaBlueprintCanvas.DrawDashedLine(canvas,
            new SKPoint(ox + flw, oy + flh), new SKPoint(ox, oy + flh),
            SkiaThemeHelper.TextMuted, 6f, 4f);
        SkiaBlueprintCanvas.DrawDashedLine(canvas,
            new SKPoint(ox, oy + flh), new SKPoint(ox, oy),
            SkiaThemeHelper.TextMuted, 6f, 4f);

        // Teich (zentriert in der Folie)
        float pondOffX = (pondDepth + overlap) * scale;
        float pondOffY = (pondDepth + overlap) * scale;
        float pw = pondLength * scale;
        float ph = pondWidth * scale;

        // Wasser-Füllung
        _soilFill.Color = SkiaThemeHelper.WithAlpha(_waterColor, 80);

        // Teich als abgerundetes Rechteck
        var pondRect = new SKRoundRect(new SKRect(ox + pondOffX, oy + pondOffY,
            ox + pondOffX + pw, oy + pondOffY + ph), 8f);
        canvas.DrawRoundRect(pondRect, _soilFill);

        // Wasser-Glanz
        _soilFill.Color = SkiaThemeHelper.WithAlpha(_waterColor, 30);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(
            ox + pondOffX + pw * 0.1f, oy + pondOffY + ph * 0.1f,
            ox + pondOffX + pw * 0.5f, oy + pondOffY + ph * 0.4f), 6f), _soilFill);

        // Teich-Umriss
        _borderPaint.Color = SkiaThemeHelper.Accent;
        canvas.DrawRoundRect(pondRect, _borderPaint);

        // Maßlinien
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox + pondOffX, oy + pondOffY + ph + 4f),
            new SKPoint(ox + pondOffX + pw, oy + pondOffY + ph + 4f),
            $"{pondLength:F1} m", offset: 10f);

        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox + pondOffX + pw + 4f, oy + pondOffY),
            new SKPoint(ox + pondOffX + pw + 4f, oy + pondOffY + ph),
            $"{pondWidth:F1} m", offset: 10f);

        // Folie-Maße
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox, oy - 4f), new SKPoint(ox + flw, oy - 4f),
            $"Folie: {linerW:F2} m", offset: -10f);

        // Tiefe + Folienfläche
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"T={pondDepth:F1}m | {linerArea:F1} m²",
            new SKPoint(ox + flw / 2f, oy + flh + 14f),
            SkiaThemeHelper.TextSecondary, 9f);
    }
}
