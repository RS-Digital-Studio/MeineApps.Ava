using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// 3 Sub: Dachdreieck + Winkel, Dachfläche mit Ziegelraster, Solar-Panel-Layout mit Kompass.
/// </summary>
public static class RoofSolarVisualization
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

    private static readonly SKPaint _roofFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _roofStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _tileFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _panelFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _panelStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private static readonly SKPaint _compassPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _layerPaint = new() { IsAntialias = false };

    /// <summary>
    /// subType: 0=Dachneigung, 1=Dachziegel, 2=Solar
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds, int subType,
        float run, float rise, float pitchDeg, float pitchPercent,
        float roofArea, float tilesPerSqm, int tilesNeeded,
        float solarArea, float kwPeak, float annualYieldKwh, int orientationIdx, float tiltDeg,
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
            case 0: RenderPitch(canvas, bounds, run, rise, pitchDeg, pitchPercent); break;
            case 1: RenderTiles(canvas, bounds, roofArea, tilesPerSqm, tilesNeeded); break;
            case 2: RenderSolar(canvas, bounds, solarArea, kwPeak, annualYieldKwh, orientationIdx, tiltDeg); break;
        }

        canvas.Restore();
    }

    private static void RenderPitch(SKCanvas canvas, SKRect bounds,
        float run, float rise, float pitchDeg, float pitchPercent)
    {
        if (run <= 0 || rise <= 0) return;

        var fit = SkiaBlueprintCanvas.FitToCanvas(bounds, run, rise, 45f);
        float scale = fit.Scale;
        float ox = fit.OffsetX;
        float oy = fit.OffsetY;

        float rw = run * scale;
        float rh = rise * scale;
        float baseY = oy + rh;

        // Dach-Dreieck
        using var roofPath = new SKPath();
        roofPath.MoveTo(ox, baseY);
        roofPath.LineTo(ox + rw, baseY);
        roofPath.LineTo(ox, baseY - rh);
        roofPath.Close();

        _roofFill.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Warning, 40);
        canvas.DrawPath(roofPath, _roofFill);

        _roofStroke.Color = SkiaThemeHelper.TextPrimary;
        canvas.DrawPath(roofPath, _roofStroke);

        // Winkel-Arc
        float arcR = Math.Min(rw, rh) * 0.3f;
        SkiaBlueprintCanvas.DrawAngleArc(canvas,
            new SKPoint(ox + rw, baseY), arcR,
            180f, -pitchDeg,
            $"{pitchDeg:F1}°", SkiaThemeHelper.Accent);

        // Maßlinien
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox, baseY + 4f), new SKPoint(ox + rw, baseY + 4f),
            $"Run: {run:F2} m", offset: 14f);

        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox - 4f, baseY), new SKPoint(ox - 4f, baseY - rh),
            $"Rise: {rise:F2} m", offset: -14f);

        // Neigung in Prozent
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{pitchPercent:F1}%",
            new SKPoint(ox + rw / 3f, baseY - rh / 3f - 10f),
            SkiaThemeHelper.Warning, 11f);
    }

    private static void RenderTiles(SKCanvas canvas, SKRect bounds,
        float roofArea, float tilesPerSqm, int tilesNeeded)
    {
        if (roofArea <= 0) return;

        // Dachfläche als Parallelogramm (perspektivisch)
        float sideLen = MathF.Sqrt(roofArea);
        var fit = SkiaBlueprintCanvas.FitToCanvas(bounds, sideLen * 1.3f, sideLen, 40f);
        float scale = fit.Scale;
        float ox = fit.OffsetX;
        float oy = fit.OffsetY;

        float w = sideLen * scale;
        float h = sideLen * scale;
        float skewX = w * 0.2f;

        // Parallelogramm (leicht perspektivisch)
        using var roofPath = new SKPath();
        roofPath.MoveTo(ox + skewX, oy);
        roofPath.LineTo(ox + w + skewX, oy);
        roofPath.LineTo(ox + w, oy + h);
        roofPath.LineTo(ox, oy + h);
        roofPath.Close();

        _roofFill.Color = new SKColor(0xC4, 0x6B, 0x37, 60); // Ziegel-braun
        canvas.DrawPath(roofPath, _roofFill);

        // Ziegelraster
        canvas.Save();
        canvas.ClipPath(roofPath);

        float tileH = h / Math.Max(1f, MathF.Sqrt(tilesPerSqm) * sideLen / 4f);
        tileH = Math.Clamp(tileH, 6f, 30f);
        float tileW = tileH * 2f;

        _tileFill.Color = new SKColor(0xC4, 0x6B, 0x37, 30);
        _panelStroke.Color = new SKColor(0xC4, 0x6B, 0x37, 80);

        int rowCount = (int)(h / tileH) + 1;
        for (int row = 0; row < rowCount; row++)
        {
            float y = oy + row * tileH;
            float rowOffset = (row % 2) * tileW / 2f;
            float skew = skewX * (1f - row * tileH / h);

            int colCount = (int)(w / tileW) + 2;
            for (int col = -1; col < colCount; col++)
            {
                float x = ox + col * tileW + rowOffset + skew;
                canvas.DrawRect(x, y, tileW, tileH, _panelStroke);
            }
        }

        canvas.Restore();

        _roofStroke.Color = SkiaThemeHelper.TextPrimary;
        canvas.DrawPath(roofPath, _roofStroke);

        // Info
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{roofArea:F1} m² = {tilesNeeded} Ziegel",
            new SKPoint(bounds.MidX, oy + h + 14f),
            SkiaThemeHelper.TextSecondary, 10f);
    }

    private static void RenderSolar(SKCanvas canvas, SKRect bounds,
        float solarArea, float kwPeak, float annualYieldKwh, int orientationIdx, float tiltDeg)
    {
        if (solarArea <= 0) return;

        // Dachfläche mit Solar-Panels
        float sideLen = MathF.Sqrt(solarArea);
        float panelW = 1.7f; // Standard-Panelgröße in m
        float panelH = 1.0f;

        int cols = Math.Max(1, (int)(sideLen / panelW));
        int rows = Math.Max(1, (int)(sideLen / panelH));

        float totalW = cols * panelW;
        float totalH = rows * panelH;

        var fit = SkiaBlueprintCanvas.FitToCanvas(bounds, totalW + 4f, totalH + 4f, 50f);
        float scale = fit.Scale;
        float ox = fit.OffsetX + 2f * scale;
        float oy = fit.OffsetY + 2f * scale;

        // Dach-Hintergrund
        float dw = totalW * scale;
        float dh = totalH * scale;
        _roofFill.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Card, 120);
        canvas.DrawRect(ox - 4f, oy - 4f, dw + 8f, dh + 8f, _roofFill);

        // Solar-Panels
        float pw = panelW * scale;
        float ph = panelH * scale;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                float x = ox + col * pw;
                float y = oy + row * ph;

                // Panel-Füllung (dunkelblau)
                _panelFill.Color = new SKColor(0x1E, 0x3A, 0x5F, 200);
                canvas.DrawRect(x + 1f, y + 1f, pw - 2f, ph - 2f, _panelFill);

                // Gitter-Linien (Zellen)
                _panelStroke.Color = new SKColor(0x30, 0x60, 0x90, 150);
                int cellRows = 6;
                int cellCols = 10;
                for (int cr = 1; cr < cellRows; cr++)
                    canvas.DrawLine(x + 1f, y + 1f + cr * (ph - 2f) / cellRows,
                        x + pw - 1f, y + 1f + cr * (ph - 2f) / cellRows, _panelStroke);
                for (int cc = 1; cc < cellCols; cc++)
                    canvas.DrawLine(x + 1f + cc * (pw - 2f) / cellCols, y + 1f,
                        x + 1f + cc * (pw - 2f) / cellCols, y + ph - 1f, _panelStroke);

                // Panel-Rahmen
                _panelStroke.Color = new SKColor(0xA0, 0xA0, 0xA0);
                canvas.DrawRect(x + 1f, y + 1f, pw - 2f, ph - 2f, _panelStroke);
            }
        }

        // Kompass-Richtung (kleiner Kreis rechts oben)
        float compassR = 18f;
        float compassX = bounds.Right - 30f;
        float compassY = bounds.Top + 30f;

        _compassPaint.Color = SkiaThemeHelper.TextMuted;
        canvas.DrawCircle(compassX, compassY, compassR, _compassPaint);

        // Richtungspfeil
        string[] dirs = { "N", "NO", "O", "SO", "S", "SW", "W", "NW" };
        float dirAngle = orientationIdx * 45f - 90f; // 0=N, Uhrzeiger
        float arrowEndX = compassX + MathF.Cos(dirAngle * MathF.PI / 180f) * (compassR - 3f);
        float arrowEndY = compassY + MathF.Sin(dirAngle * MathF.PI / 180f) * (compassR - 3f);

        _compassPaint.Color = SkiaThemeHelper.Warning;
        _compassPaint.StrokeWidth = 2.5f;
        canvas.DrawLine(compassX, compassY, arrowEndX, arrowEndY, _compassPaint);
        _compassPaint.StrokeWidth = 1.5f;

        // N-Markierung
        SkiaBlueprintCanvas.DrawMeasurementText(canvas, "N",
            new SKPoint(compassX, compassY - compassR - 5f),
            SkiaThemeHelper.TextMuted, 8f);

        string dirLabel = orientationIdx < dirs.Length ? dirs[orientationIdx] : "?";
        SkiaBlueprintCanvas.DrawMeasurementText(canvas, dirLabel,
            new SKPoint(compassX, compassY + compassR + 8f),
            SkiaThemeHelper.Warning, 8f);

        // Info-Text
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{kwPeak:F1} kWp | {annualYieldKwh:F0} kWh/a",
            new SKPoint(ox + dw / 2f, oy + dh + 14f),
            SkiaThemeHelper.Accent, 10f);
    }
}
