namespace RebornSaga.Engine.Transitions;

using SkiaSharp;
using System;

/// <summary>
/// Fade zu Schwarz und wieder auf. Standard-Übergang für Szenen-Wechsel.
/// Erste Hälfte: Alte Szene blendet aus. Zweite Hälfte: Neue Szene blendet ein.
/// </summary>
public class FadeTransition : TransitionEffect
{
    private static readonly SKPaint _fadePaint = new() { Color = SKColors.Black };

    public FadeTransition() : base(0.5f) { }

    public override void Render(SKCanvas canvas, SKRect bounds,
        Action<SKCanvas, SKRect> renderOldScene,
        Action<SKCanvas, SKRect> renderNewScene)
    {
        var eased = Ease(Progress);

        if (eased < 0.5f)
        {
            // Erste Hälfte: Alte Szene ausblenden (Alpha 0→255)
            renderOldScene(canvas, bounds);
            _fadePaint.Color = SKColors.Black.WithAlpha((byte)(eased * 2f * 255));
            canvas.DrawRect(bounds, _fadePaint);
        }
        else
        {
            // Zweite Hälfte: Neue Szene einblenden (Alpha 255→0)
            renderNewScene(canvas, bounds);
            _fadePaint.Color = SKColors.Black.WithAlpha((byte)((1f - (eased - 0.5f) * 2f) * 255));
            canvas.DrawRect(bounds, _fadePaint);
        }
    }
}
