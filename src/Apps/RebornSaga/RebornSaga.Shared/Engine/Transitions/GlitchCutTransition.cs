namespace RebornSaga.Engine.Transitions;

using SkiaSharp;
using System;

/// <summary>
/// Glitch-Cut: Horizontale Scan-Lines + RGB-Shift + Flicker.
/// Kurzer, aggressiver Übergang (300ms) für System-Nachrichten und Kämpfe.
/// </summary>
public class GlitchCutTransition : TransitionEffect
{
    private static readonly SKPaint _scanLinePaint = new() { Color = SKColors.Black };
    private static readonly SKPaint _rgbPaint = new() { IsAntialias = true };
    private static readonly Random _random = new();

    public GlitchCutTransition() : base(0.3f) { }

    public override void Render(SKCanvas canvas, SKRect bounds,
        Action<SKCanvas, SKRect> renderOldScene,
        Action<SKCanvas, SKRect> renderNewScene)
    {
        var eased = Ease(Progress);

        if (eased < 0.5f)
        {
            // Alte Szene mit Glitch-Effekt
            renderOldScene(canvas, bounds);
            var intensity = eased * 2f; // 0→1
            DrawGlitch(canvas, bounds, intensity);
        }
        else
        {
            // Neue Szene mit abklingendem Glitch
            renderNewScene(canvas, bounds);
            var intensity = (1f - eased) * 2f; // 1→0
            DrawGlitch(canvas, bounds, intensity);
        }
    }

    private static void DrawGlitch(SKCanvas canvas, SKRect bounds, float intensity)
    {
        var scanLineCount = (int)(8 * intensity + 2);
        var maxShift = bounds.Width * 0.05f * intensity;

        for (int i = 0; i < scanLineCount; i++)
        {
            var y = bounds.Top + (float)_random.NextDouble() * bounds.Height;
            var h = 2f + (float)_random.NextDouble() * 6f * intensity;
            var shift = ((float)_random.NextDouble() * 2f - 1f) * maxShift;

            // RGB-Shift (rote und blaue Linien versetzt)
            _rgbPaint.Color = new SKColor(255, 0, 0, (byte)(60 * intensity));
            canvas.DrawRect(bounds.Left + shift, y, bounds.Width, h, _rgbPaint);

            _rgbPaint.Color = new SKColor(0, 0, 255, (byte)(60 * intensity));
            canvas.DrawRect(bounds.Left - shift, y + 1, bounds.Width, h, _rgbPaint);
        }

        // Horizontale Scan-Lines
        _scanLinePaint.Color = SKColors.Black.WithAlpha((byte)(30 * intensity));
        for (float y = bounds.Top; y < bounds.Bottom; y += 3)
            canvas.DrawRect(bounds.Left, y, bounds.Width, 1, _scanLinePaint);

        // Flicker (zufällig aufblitzen)
        if (_random.NextDouble() < intensity * 0.3)
        {
            _scanLinePaint.Color = SKColors.White.WithAlpha((byte)(40 * intensity));
            canvas.DrawRect(bounds, _scanLinePaint);
        }
    }
}
