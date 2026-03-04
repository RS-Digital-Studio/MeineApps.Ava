using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FinanzRechner.Graphics;

/// <summary>
/// Subtile animierte Header-Hintergründe für die 6 Finanz-Rechner.
/// Jeder Rechner-Typ hat eine individuelle Animation die zum Thema passt.
/// Zeichnet ÜBER den bestehenden farbigen Header mit Transparenz (20-40% max).
/// </summary>
public static class CalculatorHeaderRenderer
{
    /// <summary>
    /// Die 6 Finanz-Rechner-Typen.
    /// </summary>
    public enum CalculatorType
    {
        CompoundInterest,
        SavingsPlan,
        Loan,
        Amortization,
        Yield,
        Inflation
    }

    // --- Farb-Konstanten pro Typ ---
    private static readonly SKColor GreenLight = new(0x22, 0xC5, 0x5E, 90);  // ~35%
    private static readonly SKColor GreenDark = new(0x16, 0xA3, 0x4A, 50);   // ~20%
    private static readonly SKColor GreenGlow = new(0x22, 0xC5, 0x5E, 40);   // ~16%

    private static readonly SKColor BlueLight = new(0x3B, 0x82, 0xF6, 90);
    private static readonly SKColor BlueDark = new(0x25, 0x63, 0xEB, 50);
    private static readonly SKColor BlueGlow = new(0x3B, 0x82, 0xF6, 40);

    private static readonly SKColor OrangeLight = new(0xF5, 0x9E, 0x0B, 90);
    private static readonly SKColor OrangeDark = new(0xD9, 0x77, 0x06, 50);
    private static readonly SKColor OrangeGlow = new(0xF5, 0x9E, 0x0B, 40);

    private static readonly SKColor RedLight = new(0xEF, 0x44, 0x44, 90);
    private static readonly SKColor RedDark = new(0xDC, 0x26, 0x26, 50);
    private static readonly SKColor RedGlow = new(0xEF, 0x44, 0x44, 40);

    private static readonly SKColor PurpleLight = new(0x8B, 0x5C, 0xF6, 90);
    private static readonly SKColor PurpleDark = new(0x7C, 0x3A, 0xED, 50);
    private static readonly SKColor PurpleGlow = new(0x8B, 0x5C, 0xF6, 40);

    private static readonly SKColor TealLight = new(0x14, 0xB8, 0xA6, 90);
    private static readonly SKColor TealDark = new(0x0D, 0x94, 0x88, 50);
    private static readonly SKColor TealGlow = new(0x14, 0xB8, 0xA6, 40);

    private static readonly SKColor White20 = new(255, 255, 255, 50);  // ~20%
    private static readonly SKColor White10 = new(255, 255, 255, 25);  // ~10%

    // --- Gecachte SKPaints ---
    private static readonly SKPaint LinePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round
    };

    private static readonly SKPaint FillPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint GlowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint DotPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    // --- Gecachte Blur-Filter ---
    private static readonly SKMaskFilter BlurSmall = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);
    private static readonly SKMaskFilter BlurMedium = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8f);

    // --- Gecachte Paths (Rewind statt new) ---
    private static readonly SKPath CurvePath = new();
    private static readonly SKPath FillPath = new();
    private static readonly SKPath StepPath = new();
    private static readonly SKPath BarPath = new();

    // --- Animations-Konstanten ---
    private const float LoopDuration = 3.0f; // 3s Loop

    /// <summary>
    /// Rendert die zum Typ passende Header-Animation.
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds, float time, CalculatorType type)
    {
        var w = bounds.Width;
        var h = bounds.Height;
        if (w < 1 || h < 1) return;

        switch (type)
        {
            case CalculatorType.CompoundInterest:
                RenderCompoundInterest(canvas, bounds, w, h, time);
                break;
            case CalculatorType.SavingsPlan:
                RenderSavingsPlan(canvas, bounds, w, h, time);
                break;
            case CalculatorType.Loan:
                RenderLoan(canvas, bounds, w, h, time);
                break;
            case CalculatorType.Amortization:
                RenderAmortization(canvas, bounds, w, h, time);
                break;
            case CalculatorType.Yield:
                RenderYield(canvas, bounds, w, h, time);
                break;
            case CalculatorType.Inflation:
                RenderInflation(canvas, bounds, w, h, time);
                break;
        }
    }

    /// <summary>
    /// Zinseszins: Exponentialkurve die sich aufbaut, grüne Töne.
    /// Die Kurve wächst von links nach rechts exponentiell.
    /// </summary>
    private static void RenderCompoundInterest(SKCanvas canvas, SKRect bounds, float w, float h, float time)
    {
        var loopT = (time % LoopDuration) / LoopDuration;
        var eased = EasingFunctions.EaseOutCubic(Math.Min(loopT * 1.5f, 1f));

        var marginX = w * 0.08f;
        var marginTop = h * 0.2f;
        var marginBottom = h * 0.15f;
        var chartW = w - 2 * marginX;
        var chartH = h - marginTop - marginBottom;

        // Horizontale Rasterlinien
        LinePaint.StrokeWidth = 0.5f;
        LinePaint.Color = White10;
        for (var i = 0; i < 4; i++)
        {
            var y = marginTop + chartH * (i / 3f);
            canvas.DrawLine(marginX, y, marginX + chartW, y, LinePaint);
        }

        // Exponentialkurve zeichnen
        const int segments = 40;
        var visibleSegments = (int)(segments * eased);

        CurvePath.Rewind();
        FillPath.Rewind();

        var baseY = marginTop + chartH;
        FillPath.MoveTo(marginX, baseY);

        for (var i = 0; i <= visibleSegments; i++)
        {
            var t = (float)i / segments;
            var x = marginX + t * chartW;
            // Exponentialkurve: y = 1 - e^(2.5*t) normalisiert
            var expVal = (MathF.Exp(2.5f * t) - 1f) / (MathF.Exp(2.5f) - 1f);
            var y = baseY - expVal * chartH;

            if (i == 0)
                CurvePath.MoveTo(x, y);
            else
                CurvePath.LineTo(x, y);

            FillPath.LineTo(x, y);
        }

        // Fläche unter der Kurve
        if (visibleSegments > 0)
        {
            var lastX = marginX + ((float)visibleSegments / segments) * chartW;
            FillPath.LineTo(lastX, baseY);
            FillPath.Close();

            using var fillShader = SKShader.CreateLinearGradient(
                new SKPoint(0, marginTop),
                new SKPoint(0, baseY),
                new[] { GreenLight, GreenDark },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp);
            FillPaint.Shader = fillShader;
            canvas.DrawPath(FillPath, FillPaint);
            FillPaint.Shader = null;
        }

        // Kurven-Linie
        LinePaint.StrokeWidth = 2.5f;
        LinePaint.Color = GreenLight;
        canvas.DrawPath(CurvePath, LinePaint);

        // Glow-Punkt am Ende der Kurve
        if (visibleSegments > 0)
        {
            var endT = (float)visibleSegments / segments;
            var endX = marginX + endT * chartW;
            var endExpVal = (MathF.Exp(2.5f * endT) - 1f) / (MathF.Exp(2.5f) - 1f);
            var endY = baseY - endExpVal * chartH;

            // Glow
            GlowPaint.Color = GreenGlow;
            GlowPaint.MaskFilter = BlurMedium;
            canvas.DrawCircle(endX, endY, 8f, GlowPaint);
            GlowPaint.MaskFilter = null;

            // Punkt
            DotPaint.Color = GreenLight;
            canvas.DrawCircle(endX, endY, 3.5f, DotPaint);
        }

        // Schwebende Punkte für Datenpunkte-Feeling
        DrawFloatingDots(canvas, w, h, time, GreenGlow, 5, 0.3f);
    }

    /// <summary>
    /// Sparplan: Aufsteigende Stufen von links nach rechts, blaue Töne.
    /// Treppen-Effekt der sich stufenweise aufbaut.
    /// </summary>
    private static void RenderSavingsPlan(SKCanvas canvas, SKRect bounds, float w, float h, float time)
    {
        var loopT = (time % LoopDuration) / LoopDuration;
        var eased = EasingFunctions.EaseOutCubic(Math.Min(loopT * 1.4f, 1f));

        var marginX = w * 0.06f;
        var marginTop = h * 0.15f;
        var marginBottom = h * 0.12f;
        var chartH = h - marginTop - marginBottom;
        var chartW = w - 2 * marginX;

        const int steps = 8;
        var stepW = chartW / steps;
        var baseY = marginTop + chartH;

        StepPath.Rewind();

        var visibleSteps = (int)(steps * eased + 0.5f);
        if (visibleSteps < 1) visibleSteps = 1;

        for (var i = 0; i < visibleSteps; i++)
        {
            var stepHeight = chartH * ((i + 1f) / steps) * 0.85f;
            var x = marginX + i * stepW;
            var y = baseY - stepHeight;
            var stepEase = EasingFunctions.EaseOutCubic(Math.Min((eased * steps - i) * 2f, 1f));
            if (stepEase < 0) stepEase = 0;

            var actualHeight = stepHeight * stepEase;
            var actualY = baseY - actualHeight;

            // Stufen-Rechteck mit Gradient
            var stepRect = new SKRect(x + 2, actualY, x + stepW - 2, baseY);
            using var stepShader = SKShader.CreateLinearGradient(
                new SKPoint(0, actualY),
                new SKPoint(0, baseY),
                new[] { BlueLight, BlueDark },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp);
            FillPaint.Shader = stepShader;
            canvas.DrawRoundRect(stepRect, 3, 3, FillPaint);
            FillPaint.Shader = null;

            // Obere Kante der Stufe (heller)
            LinePaint.StrokeWidth = 1.5f;
            LinePaint.Color = White20;
            canvas.DrawLine(x + 2, actualY, x + stepW - 2, actualY, LinePaint);
        }

        // Aufsteigende Trendlinie über den Stufen
        LinePaint.StrokeWidth = 1.5f;
        LinePaint.Color = BlueGlow;
        var startY = baseY - chartH * (1f / steps) * 0.85f * 0.5f;
        var endTrendY = baseY - chartH * 0.85f * 0.6f;
        var trendEndX = marginX + visibleSteps * stepW;

        // Sinus-modulierte Trendlinie
        CurvePath.Rewind();
        for (var i = 0; i <= 30; i++)
        {
            var t = (float)i / 30;
            var x = marginX + t * (trendEndX - marginX);
            var baseLineY = startY + (endTrendY - startY) * t;
            var sineOffset = MathF.Sin(t * MathF.PI * 3 + time * 0.8f) * h * 0.02f;
            var y = baseLineY + sineOffset;

            if (i == 0)
                CurvePath.MoveTo(x, y);
            else
                CurvePath.LineTo(x, y);
        }
        canvas.DrawPath(CurvePath, LinePaint);

        DrawFloatingDots(canvas, w, h, time, BlueGlow, 4, 0.5f);
    }

    /// <summary>
    /// Kredit: Abnehmende Kurve mit sinkenden Balken, orange Töne.
    /// Zeigt die abnehmende Restschuld über die Laufzeit.
    /// </summary>
    private static void RenderLoan(SKCanvas canvas, SKRect bounds, float w, float h, float time)
    {
        var loopT = (time % LoopDuration) / LoopDuration;
        var eased = EasingFunctions.EaseOutCubic(Math.Min(loopT * 1.5f, 1f));

        var marginX = w * 0.08f;
        var marginTop = h * 0.18f;
        var marginBottom = h * 0.12f;
        var chartW = w - 2 * marginX;
        var chartH = h - marginTop - marginBottom;
        var baseY = marginTop + chartH;

        // Horizontale Rasterlinien
        LinePaint.StrokeWidth = 0.5f;
        LinePaint.Color = White10;
        for (var i = 0; i < 4; i++)
        {
            var y = marginTop + chartH * (i / 3f);
            canvas.DrawLine(marginX, y, marginX + chartW, y, LinePaint);
        }

        // Absteigende Balken (Restschuld sinkt)
        const int bars = 7;
        var barW = chartW / bars;
        var visibleBars = (int)(bars * eased + 0.5f);

        for (var i = 0; i < visibleBars; i++)
        {
            // Restschuld sinkt: 100% → ~15%
            var remaining = 1f - (i / (float)(bars - 1)) * 0.85f;
            var barHeight = chartH * remaining * 0.9f;
            var barEase = EasingFunctions.EaseOutCubic(Math.Min((eased * bars - i) * 2f, 1f));
            if (barEase < 0) barEase = 0;

            var actualH = barHeight * barEase;
            var x = marginX + i * barW;
            var y = baseY - actualH;

            var barRect = new SKRect(x + 3, y, x + barW - 3, baseY);
            using var barShader = SKShader.CreateLinearGradient(
                new SKPoint(0, y),
                new SKPoint(0, baseY),
                new[] { OrangeLight, OrangeDark },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp);
            FillPaint.Shader = barShader;
            canvas.DrawRoundRect(barRect, 3, 3, FillPaint);
            FillPaint.Shader = null;
        }

        // Absteigende Trendlinie
        CurvePath.Rewind();
        const int curveSegs = 30;
        var curveEndX = marginX + Math.Min(visibleBars, bars) * barW;

        for (var i = 0; i <= curveSegs; i++)
        {
            var t = (float)i / curveSegs;
            var x = marginX + t * (curveEndX - marginX);
            // Absteigende Kurve
            var decay = 1f - t * 0.85f;
            var y = marginTop + chartH * (1f - decay * 0.9f);
            y += MathF.Sin(t * MathF.PI * 2 + time * 0.6f) * h * 0.015f;

            if (i == 0)
                CurvePath.MoveTo(x, y);
            else
                CurvePath.LineTo(x, y);
        }

        LinePaint.StrokeWidth = 2f;
        LinePaint.Color = OrangeLight;
        canvas.DrawPath(CurvePath, LinePaint);

        DrawFloatingDots(canvas, w, h, time, OrangeGlow, 4, 0.7f);
    }

    /// <summary>
    /// Tilgungsplan: Gestapelte Balken (Tilgung + Zinsen) die sich aufbauen, rote Töne.
    /// </summary>
    private static void RenderAmortization(SKCanvas canvas, SKRect bounds, float w, float h, float time)
    {
        var loopT = (time % LoopDuration) / LoopDuration;
        var eased = EasingFunctions.EaseOutCubic(Math.Min(loopT * 1.3f, 1f));

        var marginX = w * 0.06f;
        var marginTop = h * 0.15f;
        var marginBottom = h * 0.1f;
        var chartW = w - 2 * marginX;
        var chartH = h - marginTop - marginBottom;
        var baseY = marginTop + chartH;

        const int bars = 6;
        var barW = chartW / bars;

        for (var i = 0; i < bars; i++)
        {
            var barEase = EasingFunctions.EaseOutCubic(Math.Min((eased * bars - i) * 1.5f, 1f));
            if (barEase <= 0) continue;

            var x = marginX + i * barW;
            var totalHeight = chartH * 0.85f;

            // Tilgungsanteil steigt, Zinsanteil sinkt über die Laufzeit
            var principalRatio = 0.3f + (i / (float)(bars - 1)) * 0.5f;
            var interestRatio = 1f - principalRatio;

            var principalH = totalHeight * principalRatio * barEase;
            var interestH = totalHeight * interestRatio * barEase;

            // Zinsanteil (oben) - helleres Rot
            var interestY = baseY - principalH - interestH;
            var interestRect = new SKRect(x + 3, interestY, x + barW - 3, baseY - principalH);
            FillPaint.Color = new SKColor(0xEF, 0x44, 0x44, 70); // ~27%
            canvas.DrawRoundRect(interestRect, 2, 2, FillPaint);

            // Tilgungsanteil (unten) - kräftigeres Rot
            var principalRect = new SKRect(x + 3, baseY - principalH, x + barW - 3, baseY);
            FillPaint.Color = RedLight;
            canvas.DrawRoundRect(principalRect, 2, 2, FillPaint);

            // Trennlinie zwischen den Anteilen
            LinePaint.StrokeWidth = 1f;
            LinePaint.Color = White20;
            canvas.DrawLine(x + 3, baseY - principalH, x + barW - 3, baseY - principalH, LinePaint);
        }

        // Kumulierte Tilgungs-Linie (ansteigende Diagonale)
        LinePaint.StrokeWidth = 2f;
        LinePaint.Color = RedGlow;
        var lineEndX = marginX + Math.Min((int)(bars * eased + 0.5f), bars) * barW;
        canvas.DrawLine(marginX, baseY, lineEndX, marginTop + chartH * 0.2f, LinePaint);

        DrawFloatingDots(canvas, w, h, time, RedGlow, 3, 0.9f);
    }

    /// <summary>
    /// Rendite: Aufsteigende Trendlinie mit Glow-Trail, lila Töne.
    /// Zeigt einen steigenden Rendite-Trend mit glühender Spur.
    /// </summary>
    private static void RenderYield(SKCanvas canvas, SKRect bounds, float w, float h, float time)
    {
        var loopT = (time % LoopDuration) / LoopDuration;
        var eased = EasingFunctions.EaseOutCubic(Math.Min(loopT * 1.4f, 1f));

        var marginX = w * 0.08f;
        var marginTop = h * 0.2f;
        var marginBottom = h * 0.12f;
        var chartW = w - 2 * marginX;
        var chartH = h - marginTop - marginBottom;
        var baseY = marginTop + chartH;

        // Rasterlinien
        LinePaint.StrokeWidth = 0.5f;
        LinePaint.Color = White10;
        for (var i = 0; i < 5; i++)
        {
            var y = marginTop + chartH * (i / 4f);
            canvas.DrawLine(marginX, y, marginX + chartW, y, LinePaint);
        }

        // Aufsteigende Trendlinie mit Wellenform
        const int segments = 50;
        var visibleSegs = (int)(segments * eased);

        CurvePath.Rewind();
        FillPath.Rewind();
        FillPath.MoveTo(marginX, baseY);

        float lastX = marginX, lastY = baseY;

        for (var i = 0; i <= visibleSegs; i++)
        {
            var t = (float)i / segments;
            var x = marginX + t * chartW;

            // Aufsteigend mit leichter Wellung
            var trend = t * 0.75f; // 75% des Chart-Bereichs
            var wave = MathF.Sin(t * MathF.PI * 4 + time * 0.5f) * 0.04f;
            var smallWave = MathF.Sin(t * MathF.PI * 8 + time * 0.3f) * 0.015f;
            var y = baseY - (trend + wave + smallWave) * chartH;

            if (i == 0)
                CurvePath.MoveTo(x, y);
            else
                CurvePath.LineTo(x, y);

            FillPath.LineTo(x, y);
            lastX = x;
            lastY = y;
        }

        // Fläche unter der Kurve
        if (visibleSegs > 0)
        {
            FillPath.LineTo(lastX, baseY);
            FillPath.Close();

            using var gradShader = SKShader.CreateLinearGradient(
                new SKPoint(0, marginTop),
                new SKPoint(0, baseY),
                new[] { PurpleLight, new SKColor(0x8B, 0x5C, 0xF6, 15) },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp);
            FillPaint.Shader = gradShader;
            canvas.DrawPath(FillPath, FillPaint);
            FillPaint.Shader = null;
        }

        // Glow-Trail (breitere unscharfe Linie)
        LinePaint.StrokeWidth = 6f;
        LinePaint.Color = PurpleGlow;
        LinePaint.MaskFilter = BlurSmall;
        canvas.DrawPath(CurvePath, LinePaint);
        LinePaint.MaskFilter = null;

        // Scharfe Trendlinie
        LinePaint.StrokeWidth = 2.5f;
        LinePaint.Color = PurpleLight;
        canvas.DrawPath(CurvePath, LinePaint);

        // Glow-Punkt am Ende
        if (visibleSegs > 0)
        {
            GlowPaint.Color = PurpleGlow;
            GlowPaint.MaskFilter = BlurMedium;
            canvas.DrawCircle(lastX, lastY, 10f, GlowPaint);
            GlowPaint.MaskFilter = null;

            DotPaint.Color = PurpleLight;
            canvas.DrawCircle(lastX, lastY, 4f, DotPaint);
        }

        DrawFloatingDots(canvas, w, h, time, PurpleGlow, 5, 0.2f);
    }

    /// <summary>
    /// Inflation: Schrumpfende Münze / Kaufkraft-Verlust, teal Töne.
    /// Konzentrische Kreise die schrumpfen (Kaufkraft nimmt ab).
    /// </summary>
    private static void RenderInflation(SKCanvas canvas, SKRect bounds, float w, float h, float time)
    {
        var loopT = (time % LoopDuration) / LoopDuration;
        var eased = EasingFunctions.EaseOutCubic(Math.Min(loopT * 1.3f, 1f));

        var cx = w * 0.5f;
        var cy = h * 0.5f;
        var maxRadius = Math.Min(w, h) * 0.35f;

        // Konzentrische schrumpfende Kreise (Kaufkraft-Verlust)
        const int rings = 5;
        for (var i = rings - 1; i >= 0; i--)
        {
            var ringT = (float)(i + 1) / rings;
            var radius = maxRadius * ringT;

            // Schrumpf-Animation: Kreise werden kleiner über die Zeit
            var shrinkFactor = 1f - eased * 0.3f * (1f - ringT);
            var animRadius = radius * shrinkFactor;

            // Pulsieren
            var pulse = 1f + MathF.Sin(time * 1.5f + i * 0.8f) * 0.03f;
            animRadius *= pulse;

            var alpha = (byte)(30 + (rings - i) * 12); // Innere Ringe kräftiger
            FillPaint.Color = new SKColor(0x14, 0xB8, 0xA6, (byte)(alpha * 0.7f));
            canvas.DrawCircle(cx, cy, animRadius, FillPaint);

            // Ring-Rand
            LinePaint.StrokeWidth = 1f;
            LinePaint.Color = new SKColor(0x14, 0xB8, 0xA6, (byte)(alpha * 0.5f));
            canvas.DrawCircle(cx, cy, animRadius, LinePaint);
        }

        // Absteigende Trendlinie (Kaufkraft sinkt)
        CurvePath.Rewind();
        var lineStartX = w * 0.15f;
        var lineEndX = w * 0.85f;
        var lineStartY = h * 0.25f;
        var lineEndY = h * 0.7f;
        var visibleEnd = lineStartX + (lineEndX - lineStartX) * eased;

        const int lineSegs = 30;
        for (var i = 0; i <= lineSegs; i++)
        {
            var t = (float)i / lineSegs;
            var x = lineStartX + t * (visibleEnd - lineStartX);
            if (x > visibleEnd) break;

            var y = lineStartY + (lineEndY - lineStartY) * t;
            y += MathF.Sin(t * MathF.PI * 3 + time * 0.7f) * h * 0.02f;

            if (i == 0)
                CurvePath.MoveTo(x, y);
            else
                CurvePath.LineTo(x, y);
        }

        // Glow-Trail
        LinePaint.StrokeWidth = 4f;
        LinePaint.Color = TealGlow;
        LinePaint.MaskFilter = BlurSmall;
        canvas.DrawPath(CurvePath, LinePaint);
        LinePaint.MaskFilter = null;

        // Scharfe Linie
        LinePaint.StrokeWidth = 2f;
        LinePaint.Color = TealLight;
        canvas.DrawPath(CurvePath, LinePaint);

        // Pfeil-Indikator am Ende (↓ Kaufkraft sinkt)
        if (eased > 0.3f)
        {
            var arrowX = lineStartX + (visibleEnd - lineStartX) * 0.95f;
            var arrowBaseT = 0.95f;
            var arrowY = lineStartY + (lineEndY - lineStartY) * arrowBaseT;

            LinePaint.StrokeWidth = 2f;
            LinePaint.Color = TealLight;
            canvas.DrawLine(arrowX, arrowY, arrowX - 5, arrowY - 6, LinePaint);
            canvas.DrawLine(arrowX, arrowY, arrowX + 5, arrowY - 6, LinePaint);
        }

        DrawFloatingDots(canvas, w, h, time, TealGlow, 4, 0.4f);
    }

    /// <summary>
    /// Zeichnet subtile schwebende Glow-Punkte für zusätzliche Lebendigkeit.
    /// </summary>
    private static void DrawFloatingDots(SKCanvas canvas, float w, float h, float time,
        SKColor color, int count, float phaseOffset)
    {
        for (var i = 0; i < count; i++)
        {
            var phase = phaseOffset + i * 1.3f;
            var x = w * (0.15f + 0.7f * ((MathF.Sin(phase * 2.1f + time * 0.3f) + 1f) * 0.5f));
            var y = h * (0.1f + 0.8f * ((MathF.Cos(phase * 1.7f + time * 0.25f) + 1f) * 0.5f));
            var pulse = 0.5f + 0.5f * MathF.Sin(time * 0.8f + phase * 3f);
            var radius = 2f + pulse * 2f;

            GlowPaint.Color = color.WithAlpha((byte)(color.Alpha * pulse * 0.7f));
            GlowPaint.MaskFilter = BlurSmall;
            canvas.DrawCircle(x, y, radius * 2.5f, GlowPaint);
            GlowPaint.MaskFilter = null;

            DotPaint.Color = color.WithAlpha((byte)(color.Alpha * pulse));
            canvas.DrawCircle(x, y, radius * 0.7f, DotPaint);
        }
    }
}
