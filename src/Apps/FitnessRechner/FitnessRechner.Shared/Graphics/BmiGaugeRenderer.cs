using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// BMI-Halbkreis-Gauge mit 4 Zonen (Untergewicht/Normal/Übergewicht/Adipositas).
/// Medical-Ästhetik: Grid-Hintergrund, holographischer Nadel-Glow, Scan-Line-Sweep.
/// Thread-safe: Verwendet lokale Paint-Objekte statt statischer Felder.
/// </summary>
public static class BmiGaugeRenderer
{
    // BMI-Zonen: Blau (Untergewicht), Grün (Normal), Gelb (Übergewicht), Rot (Adipositas)
    private static readonly SKColor _zoneUnderweight = new(0x3B, 0x82, 0xF6); // Blau
    private static readonly SKColor _zoneNormal = new(0x22, 0xC5, 0x5E); // Grün
    private static readonly SKColor _zoneOverweight = new(0xF5, 0x9E, 0x0B); // Gelb/Amber
    private static readonly SKColor _zoneObese = new(0xEF, 0x44, 0x44); // Rot

    // Medical Grid
    private const float GridSpacing = 40f;
    private const byte GridAlpha = 20; // ~8% von 255

    // Scan-Line
    private const float ScanLineHeight = 20f;
    private const float ScanLineCycleSeconds = 5f;
    private const byte ScanLineAlpha = 38; // ~15% von 255

    // Nadel-Glow
    private const float NeedleGlowBlur = 4f;

    /// <summary>
    /// Rendert BMI-Gauge als Halbkreis mit Zeiger.
    /// </summary>
    /// <param name="bmiValue">BMI-Wert (10-45 Range)</param>
    /// <param name="hasResult">Ergebnis vorhanden</param>
    /// <param name="time">Animationszeit in Sekunden (0f = statischer Snapshot)</param>
    public static void Render(SKCanvas canvas, SKRect bounds, float bmiValue, bool hasResult, float time = 0f)
    {
        if (!hasResult || bmiValue <= 0) return;

        float w = bounds.Width;
        float h = bounds.Height;
        float cx = bounds.MidX;

        // --- Medical Grid im Hintergrund ---
        RenderMedicalGrid(canvas, bounds);

        // Gauge nimmt unteren 2/3-Bereich ein
        float strokeW = Math.Max(12f, w * 0.04f);
        float radius = Math.Min(w * 0.4f, h * 0.55f);
        float cy = bounds.Top + h * 0.55f;

        var arcRect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        // Startwinkel 180° (links), Sweep 180° (Halbkreis)
        float startAngle = 180f;
        float totalSweep = 180f;

        // 4 BMI-Zonen zeichnen (10-18.5, 18.5-25, 25-30, 30-45)
        float minBmi = 10f;
        float maxBmi = 45f;
        float range = maxBmi - minBmi;

        var zones = new[]
        {
            (Start: 10f, End: 18.5f, Color: _zoneUnderweight),
            (Start: 18.5f, End: 25f, Color: _zoneNormal),
            (Start: 25f, End: 30f, Color: _zoneOverweight),
            (Start: 30f, End: 45f, Color: _zoneObese),
        };

        // Zonen-Bögen zeichnen
        using var zoneFill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Butt,
            StrokeWidth = strokeW
        };

        foreach (var zone in zones)
        {
            float zoneStart = (zone.Start - minBmi) / range * totalSweep + startAngle;
            float zoneSweep = (zone.End - zone.Start) / range * totalSweep;

            zoneFill.Color = zone.Color;
            using var path = new SKPath();
            path.AddArc(arcRect, zoneStart, zoneSweep);
            canvas.DrawPath(path, zoneFill);
        }

        // Track-Rahmen (dünner Rand über den Zonen) + Zeiger-Linie teilen sich ein Paint
        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeWidth = strokeW + 2f,
            Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 30)
        };
        using var trackPath = new SKPath();
        trackPath.AddArc(arcRect, startAngle, totalSweep);
        canvas.DrawPath(trackPath, strokePaint);

        // Zeiger
        float clampedBmi = Math.Clamp(bmiValue, minBmi, maxBmi);
        float needleAngle = (clampedBmi - minBmi) / range * totalSweep + startAngle;
        float needleAngleRad = needleAngle * MathF.PI / 180f;

        float needleLen = radius - strokeW * 0.5f - 4f;
        float needleX = cx + MathF.Cos(needleAngleRad) * needleLen;
        float needleY = cy + MathF.Sin(needleAngleRad) * needleLen;

        // --- Holographischer Glow auf der Nadel ---
        using var needleGlowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeWidth = 4f,
            Color = MedicalColors.Cyan.WithAlpha(100),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, NeedleGlowBlur)
        };
        using var needleGlowPath = new SKPath();
        needleGlowPath.MoveTo(cx, cy);
        needleGlowPath.LineTo(needleX, needleY);
        canvas.DrawPath(needleGlowPath, needleGlowPaint);

        // Zeiger-Linie (strokePaint wiederverwenden mit neuen Werten)
        strokePaint.StrokeWidth = 2.5f;
        strokePaint.Color = SkiaThemeHelper.TextPrimary;
        using var needlePath = new SKPath();
        needlePath.MoveTo(cx, cy);
        needlePath.LineTo(needleX, needleY);
        canvas.DrawPath(needlePath, strokePaint);

        // Füllung für Zeiger-Basis, Zeiger-Spitze
        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SkiaThemeHelper.TextPrimary
        };

        // Zentraler Kreis (Zeiger-Basis)
        canvas.DrawCircle(cx, cy, 5f, fillPaint);

        // Zeiger-Spitze (leuchtender Punkt)
        SKColor dotColor = bmiValue switch
        {
            < 18.5f => _zoneUnderweight,
            < 25f => _zoneNormal,
            < 30f => _zoneOverweight,
            _ => _zoneObese,
        };
        fillPaint.Color = dotColor;
        canvas.DrawCircle(needleX, needleY, 6f, fillPaint);

        // Text-Paint für BMI-Wert und Zonen-Labels
        using var textPaint = new SKPaint { IsAntialias = true };

        // BMI-Wert Text unter dem Gauge
        textPaint.Color = SkiaThemeHelper.TextPrimary;
        textPaint.TextSize = Math.Max(18f, radius * 0.22f);
        textPaint.TextAlign = SKTextAlign.Center;
        textPaint.FakeBoldText = true;
        canvas.DrawText($"{bmiValue:F1}", cx, cy + radius * 0.35f, textPaint);

        // Zonen-Labels (klein, unter dem Bogen) - MedicalColors.TextMuted
        textPaint.TextSize = Math.Max(8f, radius * 0.1f);
        textPaint.FakeBoldText = false;
        textPaint.Color = MedicalColors.TextMuted;

        float labelRadius = radius + strokeW * 0.5f + 10f;
        var labels = new[] { ("18.5", 18.5f), ("25", 25f), ("30", 30f) };
        foreach (var (text, val) in labels)
        {
            float angle = (val - minBmi) / range * totalSweep + startAngle;
            float rad = angle * MathF.PI / 180f;
            float lx = cx + MathF.Cos(rad) * labelRadius;
            float ly = cy + MathF.Sin(rad) * labelRadius;
            canvas.DrawText(text, lx, ly, textPaint);
        }

        // --- Scan-Line-Sweep (von oben nach unten, 5s Zyklus) ---
        RenderScanLine(canvas, bounds, time);
    }

    /// <summary>
    /// Medical Grid: Feine Cyan-Linien im Hintergrund (8% Opacity).
    /// </summary>
    private static void RenderMedicalGrid(SKCanvas canvas, SKRect bounds)
    {
        using var gridPaint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f,
            Color = MedicalColors.Grid.WithAlpha(GridAlpha)
        };

        // Vertikale Linien
        for (float x = bounds.Left + GridSpacing; x < bounds.Right; x += GridSpacing)
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, gridPaint);

        // Horizontale Linien
        for (float y = bounds.Top + GridSpacing; y < bounds.Bottom; y += GridSpacing)
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, gridPaint);
    }

    /// <summary>
    /// Horizontaler Scan-Line-Sweep über den Gauge (5s Zyklus).
    /// Bei time=0f wird die Linie am oberen Rand gezeichnet (statischer Snapshot).
    /// </summary>
    private static void RenderScanLine(SKCanvas canvas, SKRect bounds, float time)
    {
        // Position berechnen: 0→1 über die Höhe, 5s Zyklus
        float progress = (time / ScanLineCycleSeconds) % 1f;
        float lineY = bounds.Top + progress * bounds.Height;

        using var scanShader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.Left, lineY - ScanLineHeight * 0.5f),
            new SKPoint(bounds.Left, lineY + ScanLineHeight * 0.5f),
            new[]
            {
                MedicalColors.Cyan.WithAlpha(0),
                MedicalColors.Cyan.WithAlpha(ScanLineAlpha),
                MedicalColors.Cyan.WithAlpha(0)
            },
            new[] { 0f, 0.5f, 1f },
            SKShaderTileMode.Clamp);

        using var scanPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = scanShader
        };

        canvas.DrawRect(bounds.Left, lineY - ScanLineHeight * 0.5f,
            bounds.Width, ScanLineHeight, scanPaint);
    }
}
