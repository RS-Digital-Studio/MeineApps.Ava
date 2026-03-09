namespace RebornSaga.Engine.Transitions;

using SkiaSharp;
using System;

/// <summary>
/// Horizontaler Slide-Übergang. Alte Szene schiebt raus, neue Szene schiebt rein.
/// Richtung konfigurierbar (links/rechts).
/// </summary>
public class SlideTransition : TransitionEffect
{
    private readonly bool _slideLeft;

    /// <summary>
    /// Erstellt einen Slide-Übergang.
    /// </summary>
    /// <param name="slideLeft">True: Slide nach links (Standard). False: Slide nach rechts.</param>
    public SlideTransition(bool slideLeft = true) : base(0.4f)
    {
        _slideLeft = slideLeft;
    }

    public override void Render(SKCanvas canvas, SKRect bounds,
        Action<SKCanvas, SKRect> renderOldScene,
        Action<SKCanvas, SKRect> renderNewScene)
    {
        var eased = Ease(Progress);
        var direction = _slideLeft ? -1f : 1f;
        var offset = eased * bounds.Width * direction;

        // Alte Szene rausschieben
        canvas.Save();
        canvas.Translate(offset, 0);
        renderOldScene(canvas, bounds);
        canvas.Restore();

        // Neue Szene reinschieben (von der anderen Seite)
        canvas.Save();
        canvas.Translate(offset + bounds.Width * -direction, 0);
        renderNewScene(canvas, bounds);
        canvas.Restore();
    }
}
