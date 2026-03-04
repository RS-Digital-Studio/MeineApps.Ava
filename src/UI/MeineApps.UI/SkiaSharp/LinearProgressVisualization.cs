using MeineApps.UI.SkiaSharp.Shaders;
using SkiaSharp;

namespace MeineApps.UI.SkiaSharp;

/// <summary>
/// SkiaSharp linearer Fortschrittsbalken mit Gradient, Glow und optionalem Prozent-Text.
/// Ersetzt Avalonia ProgressBar für visuell ansprechendere Darstellung.
/// Shared-Renderer, wiederverwendbar in allen Apps.
/// </summary>
public static class LinearProgressVisualization
{
    private static readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _font = new() { Size = 10f };

    /// <summary>
    /// Rendert einen linearen Fortschrittsbalken.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="progress">Fortschritt 0.0-1.0 (kann > 1.0 sein für Überschreitung)</param>
    /// <param name="startColor">Gradient Start-Farbe</param>
    /// <param name="endColor">Gradient End-Farbe</param>
    /// <param name="showText">Prozentwert anzeigen</param>
    /// <param name="glowEnabled">Glow-Effekt am Ende</param>
    /// <param name="animationTime">Animationszeit für Shimmer-Effekt (0 = kein Shimmer)</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        float progress, SKColor startColor, SKColor endColor,
        bool showText = true, bool glowEnabled = true, float animationTime = 0f)
    {
        float barH = Math.Min(bounds.Height - 4f, 14f);
        float barTop = bounds.MidY - barH / 2f;
        float barLeft = bounds.Left + 4f;
        float barRight = showText ? bounds.Right - 40f : bounds.Right - 4f;
        float barW = barRight - barLeft;
        float cornerR = barH / 2f;

        if (barW <= 10) return;

        // Track
        _trackPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 30);
        canvas.DrawRoundRect(new SKRect(barLeft, barTop, barRight, barTop + barH),
            cornerR, cornerR, _trackPaint);

        // Füllbalken
        float clampedProgress = Math.Clamp(progress, 0f, 1f);
        float fillW = clampedProgress * barW;
        if (fillW > 0)
        {
            float fillRight = barLeft + fillW;

            _fillPaint.Shader?.Dispose();
            _fillPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(barLeft, barTop),
                new SKPoint(fillRight, barTop),
                new[] { startColor, endColor },
                null, SKShaderTileMode.Clamp);

            var fillRect = new SKRect(barLeft, barTop, fillRight, barTop + barH);
            canvas.DrawRoundRect(fillRect, cornerR, cornerR, _fillPaint);
            _fillPaint.Shader = null;

            // Shimmer-Overlay auf dem Füllbalken (animiert)
            if (animationTime > 0f && fillW > 20f)
            {
                canvas.Save();
                canvas.ClipRoundRect(new SKRoundRect(fillRect, cornerR));
                SkiaShimmerEffect.DrawShimmerOverlay(canvas, fillRect, animationTime,
                    shimmerColor: SKColors.White.WithAlpha(50),
                    stripWidth: 0.15f, speed: 0.5f);
                canvas.Restore();
            }

            // Glow am Ende
            if (glowEnabled && fillW > 5f)
            {
                _glowPaint.Color = SkiaThemeHelper.WithAlpha(endColor, 60);
                _glowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);
                canvas.DrawCircle(fillRight - 2f, barTop + barH / 2f, barH / 2f + 2f, _glowPaint);
                _glowPaint.MaskFilter = null;
            }
        }

        // Überschreitung markieren (> 100%) mit Gold-Shimmer
        if (progress > 1f)
        {
            var overRect = new SKRect(barLeft, barTop, barRight, barTop + barH);
            if (animationTime > 0f)
            {
                // Gold-Shimmer bei Überschreitung
                canvas.Save();
                canvas.ClipRoundRect(new SKRoundRect(overRect, cornerR));
                SkiaShimmerEffect.DrawGoldShimmer(canvas, overRect, animationTime);
                canvas.Restore();
            }
            else
            {
                // Statischer Shimmer-Streifen als Fallback
                _fillPaint.Color = SkiaThemeHelper.WithAlpha(SKColors.White, 30);
                canvas.DrawRoundRect(new SKRect(barLeft, barTop, barRight, barTop + barH / 3f),
                    cornerR, cornerR / 3f, _fillPaint);
            }
        }

        // Prozent-Text
        if (showText)
        {
            int percent = (int)(progress * 100);
            _textPaint.Color = progress >= 1f ? SkiaThemeHelper.Success : SkiaThemeHelper.TextPrimary;
            _font.Size = 11f;
            canvas.DrawText($"{percent}%", barRight + 6f, bounds.MidY + 4f,
                SKTextAlign.Left, _font, _textPaint);
        }
    }
}
