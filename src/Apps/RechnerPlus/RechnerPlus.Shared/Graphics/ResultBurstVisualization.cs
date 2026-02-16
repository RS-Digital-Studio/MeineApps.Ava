using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace RechnerPlus.Graphics;

/// <summary>
/// SkiaSharp Ergebnis-Burst-Effekt für den "="-Button.
/// Zeigt einen expandierenden Lichtring + Partikel wenn ein Ergebnis berechnet wird.
/// </summary>
public static class ResultBurstVisualization
{
    private static readonly SKPaint _ringPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

    /// <summary>
    /// Rendert den Ergebnis-Burst-Effekt.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="progress">Animation-Fortschritt (0.0 = Start, 1.0 = Ende)</param>
    /// <param name="burstColor">Farbe des Bursts (Primary-Farbe)</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        float progress, SKColor burstColor)
    {
        if (progress <= 0 || progress >= 1) return;

        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float maxRadius = Math.Min(bounds.Width, bounds.Height) / 2f;

        // Easing: Schneller Start, langsames Ausklingen
        float eased = 1f - MathF.Pow(1f - progress, 3f);
        float fadeOut = MathF.Pow(1f - progress, 1.5f);

        // 1. Expandierender Ring
        float ringRadius = eased * maxRadius * 0.8f;
        float ringAlpha = fadeOut * 200f;

        _ringPaint.StrokeWidth = Math.Max(1f, (1f - eased) * 4f);
        _ringPaint.Color = burstColor.WithAlpha((byte)ringAlpha);
        _ringPaint.MaskFilter = _glowFilter;
        canvas.DrawCircle(cx, cy, ringRadius, _ringPaint);
        _ringPaint.MaskFilter = null;

        // 2. Innerer Glow
        float innerAlpha = fadeOut * 100f;
        _dotPaint.Color = burstColor.WithAlpha((byte)innerAlpha);
        _dotPaint.MaskFilter = _glowFilter;
        float innerRadius = eased * maxRadius * 0.3f;
        canvas.DrawCircle(cx, cy, innerRadius, _dotPaint);
        _dotPaint.MaskFilter = null;

        // 3. Partikel-Strahlen (8 Stück, gleichmäßig verteilt)
        int particleCount = 8;
        for (int i = 0; i < particleCount; i++)
        {
            float angle = (i / (float)particleCount) * MathF.PI * 2f;
            float dist = eased * maxRadius * 0.6f;
            float px = cx + MathF.Cos(angle) * dist;
            float py = cy + MathF.Sin(angle) * dist;
            float pSize = Math.Max(1f, (1f - eased) * 3f);
            byte pAlpha = (byte)(fadeOut * 180f);

            _dotPaint.Color = burstColor.WithAlpha(pAlpha);
            canvas.DrawCircle(px, py, pSize, _dotPaint);
        }
    }
}
