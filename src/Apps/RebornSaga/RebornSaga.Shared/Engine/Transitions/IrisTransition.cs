namespace RebornSaga.Engine.Transitions;

using SkiaSharp;
using System;

/// <summary>
/// Iris-Übergang: Kreis öffnet/schließt sich.
/// Erste Hälfte: Kreis schließt sich über alter Szene.
/// Zweite Hälfte: Kreis öffnet sich über neuer Szene. (700ms)
/// Klassischer Retro-RPG-Effekt.
/// </summary>
public class IrisTransition : TransitionEffect
{
    private static readonly SKPaint _maskPaint = new() { Color = SKColors.Black };

    public IrisTransition() : base(0.7f) { }

    public override void Render(SKCanvas canvas, SKRect bounds,
        Action<SKCanvas, SKRect> renderOldScene,
        Action<SKCanvas, SKRect> renderNewScene)
    {
        var eased = Ease(Progress);
        var cx = bounds.MidX;
        var cy = bounds.MidY;

        // Maximaler Radius = Diagonale des Bildschirms
        var maxRadius = MathF.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height) / 2f;

        if (eased < 0.5f)
        {
            // Kreis schließt sich (Radius maxRadius→0)
            var radius = maxRadius * (1f - eased * 2f);
            renderOldScene(canvas, bounds);

            // Schwarze Maske außerhalb des Kreises
            canvas.Save();
            using var clipPath = new SKPath();
            clipPath.AddCircle(cx, cy, Math.Max(1f, radius));
            canvas.ClipPath(clipPath, SKClipOperation.Difference);
            canvas.DrawRect(bounds, _maskPaint);
            canvas.Restore();
        }
        else
        {
            // Kreis öffnet sich (Radius 0→maxRadius)
            var radius = maxRadius * (eased - 0.5f) * 2f;
            renderNewScene(canvas, bounds);

            // Schwarze Maske außerhalb des Kreises
            canvas.Save();
            using var clipPath = new SKPath();
            clipPath.AddCircle(cx, cy, Math.Max(1f, radius));
            canvas.ClipPath(clipPath, SKClipOperation.Difference);
            canvas.DrawRect(bounds, _maskPaint);
            canvas.Restore();
        }
    }
}
