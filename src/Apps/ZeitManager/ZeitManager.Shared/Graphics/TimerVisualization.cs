using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace ZeitManager.Graphics;

/// <summary>
/// SkiaSharp-Timer-Ring mit Flüssigkeits-Effekt: Ein sich entleerender Kreis,
/// der die verbleibende Zeit als "Flüssigkeitsstand" innerhalb des Rings darstellt.
/// </summary>
public static class TimerVisualization
{
    private static readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _arcPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _wavePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _timeFont = new() { Size = 28f };
    private static readonly SKFont _nameFont = new() { Size = 11f };
    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

    /// <summary>
    /// Bestimmt die Akzent-Farbe basierend auf dem verbleibenden Fortschritt.
    /// Grün > 30%, Amber 10-30%, Rot < 10%.
    /// </summary>
    private static SKColor GetProgressColor(float fraction)
    {
        if (fraction > 0.3f) return SkiaThemeHelper.Success;
        if (fraction > 0.1f) return SkiaThemeHelper.Warning;
        return SkiaThemeHelper.Error;
    }

    /// <summary>
    /// Rendert einen einzelnen Timer-Ring mit Flüssigkeits-Effekt.
    /// Für die TimerView: Wird im Listenelement eines Timers verwendet.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="progressFraction">Verbleibende Zeit als Fraction (1.0 = voll, 0.0 = leer)</param>
    /// <param name="isRunning">Ob der Timer gerade läuft</param>
    /// <param name="isFinished">Ob der Timer abgelaufen ist</param>
    /// <param name="remainingFormatted">Formatierte verbleibende Zeit</param>
    /// <param name="timerName">Name des Timers (optional)</param>
    /// <param name="animTime">Laufender Animations-Timer</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        float progressFraction, bool isRunning, bool isFinished,
        string remainingFormatted, string? timerName, float animTime)
    {
        float size = Math.Min(bounds.Width, bounds.Height);
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float strokeW = 5f;
        float radius = (size - strokeW * 2 - 12f) / 2f;

        if (radius <= 10) return;

        float progress = Math.Clamp(progressFraction, 0f, 1f);
        var color = isFinished ? SkiaThemeHelper.Success : GetProgressColor(progress);
        var arcRect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        // 1. Track-Ring
        _trackPaint.StrokeWidth = strokeW;
        _trackPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 40);
        canvas.DrawOval(arcRect, _trackPaint);

        // 2. Fortschrittsring (gegen den Uhrzeigersinn, läuft ab)
        float sweepAngle = progress * 360f;
        if (sweepAngle > 0.5f)
        {
            // Glow-Effekt bei laufendem Timer
            if (isRunning)
            {
                float pulse = 0.6f + 0.4f * MathF.Sin(animTime * 3f);
                _glowPaint.StrokeWidth = strokeW + 5f;
                _glowPaint.Color = color.WithAlpha((byte)(60 * pulse));
                _glowPaint.MaskFilter = _glowFilter;

                using var glowPath = new SKPath();
                glowPath.AddArc(arcRect, -90f, sweepAngle);
                canvas.DrawPath(glowPath, _glowPaint);
                _glowPaint.MaskFilter = null;
            }

            // Fortschrittsring
            var endColor = SkiaThemeHelper.AdjustBrightness(color, 1.3f);
            _arcPaint.StrokeWidth = strokeW;
            _arcPaint.Shader = SKShader.CreateSweepGradient(
                new SKPoint(cx, cy),
                new[] { color, endColor },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp, -90f, -90f + sweepAngle);
            _arcPaint.Color = SKColors.White;

            using var arcPath = new SKPath();
            arcPath.AddArc(arcRect, -90f, sweepAngle);
            canvas.DrawPath(arcPath, _arcPaint);
            _arcPaint.Shader = null;
        }

        // 3. Wellen-Füllung im Inneren (Flüssigkeits-Effekt)
        if (progress > 0.01f && !isFinished)
        {
            float innerR = radius - strokeW / 2f - 2f;
            DrawLiquidFill(canvas, cx, cy, innerR, progress, color, animTime, isRunning);
        }

        // 4. Fertig-Häkchen oder Zeitanzeige
        if (isFinished)
        {
            DrawCheckmark(canvas, cx, cy, radius * 0.3f, color);
        }
        else
        {
            // Zeitanzeige zentral
            _textPaint.Color = SkiaThemeHelper.TextPrimary;
            _timeFont.Size = Math.Max(18f, radius * 0.32f);
            canvas.DrawText(remainingFormatted, cx, cy + _timeFont.Size * 0.15f,
                SKTextAlign.Center, _timeFont, _textPaint);
        }

        // 5. Timer-Name (unten im Ring)
        if (!string.IsNullOrEmpty(timerName))
        {
            _textPaint.Color = SkiaThemeHelper.TextMuted;
            _nameFont.Size = Math.Max(9f, radius * 0.11f);
            canvas.DrawText(timerName, cx, cy + radius * 0.55f,
                SKTextAlign.Center, _nameFont, _textPaint);
        }
    }

    /// <summary>
    /// Zeichnet eine Wellen-Füllung innerhalb eines kreisförmigen Bereichs.
    /// Die Höhe repräsentiert den verbleibenden Fortschritt.
    /// </summary>
    private static void DrawLiquidFill(SKCanvas canvas, float cx, float cy, float radius,
        float fraction, SKColor color, float animTime, bool isRunning)
    {
        // Füllhöhe (von unten nach oben)
        float fillTop = cy + radius - (2f * radius * fraction);

        // Clip auf den Kreis
        canvas.Save();
        using var clipPath = new SKPath();
        clipPath.AddCircle(cx, cy, radius);
        canvas.ClipPath(clipPath);

        // Füllung mit Gradient
        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(cx, fillTop),
            new SKPoint(cx, cy + radius),
            new[] { color.WithAlpha(40), color.WithAlpha(20) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawRect(cx - radius, fillTop, radius * 2, cy + radius - fillTop, _fillPaint);
        _fillPaint.Shader = null;

        // Welleneffekt an der Oberfläche (nur bei laufend)
        if (isRunning && fraction > 0.02f && fraction < 0.98f)
        {
            using var wavePath = new SKPath();
            float waveAmplitude = 3f;
            float waveFreq = 0.06f;
            float waveSpeed = animTime * 2.5f;

            wavePath.MoveTo(cx - radius, fillTop);
            for (float x = cx - radius; x <= cx + radius; x += 2f)
            {
                float wave = MathF.Sin((x - cx) * waveFreq + waveSpeed) * waveAmplitude
                           + MathF.Sin((x - cx) * waveFreq * 1.5f + waveSpeed * 0.7f) * waveAmplitude * 0.5f;
                wavePath.LineTo(x, fillTop + wave);
            }
            wavePath.LineTo(cx + radius, cy + radius);
            wavePath.LineTo(cx - radius, cy + radius);
            wavePath.Close();

            _wavePaint.Color = color.WithAlpha(30);
            canvas.DrawPath(wavePath, _wavePaint);
        }

        canvas.Restore();
    }

    /// <summary>
    /// Zeichnet ein Häkchen-Symbol für abgeschlossene Timer.
    /// </summary>
    private static void DrawCheckmark(SKCanvas canvas, float cx, float cy, float size, SKColor color)
    {
        using var checkPath = new SKPath();
        checkPath.MoveTo(cx - size * 0.5f, cy);
        checkPath.LineTo(cx - size * 0.1f, cy + size * 0.4f);
        checkPath.LineTo(cx + size * 0.5f, cy - size * 0.35f);

        _trackPaint.StrokeWidth = 3f;
        _trackPaint.Color = color;
        _trackPaint.StrokeCap = SKStrokeCap.Round;
        canvas.DrawPath(checkPath, _trackPaint);
    }
}
