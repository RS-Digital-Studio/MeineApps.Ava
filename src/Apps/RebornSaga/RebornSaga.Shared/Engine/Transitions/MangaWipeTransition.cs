namespace RebornSaga.Engine.Transitions;

using SkiaSharp;
using System;

/// <summary>
/// Diagonaler Manga-Panel-Wipe. Die neue Szene "schneidet" sich diagonal
/// über die alte Szene wie ein Manga-Panel-Rahmen. (600ms)
/// </summary>
public class MangaWipeTransition : TransitionEffect
{
    private static readonly SKPaint _borderPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 4f,
        Color = SKColors.White
    };

    public MangaWipeTransition() : base(0.6f) { }

    public override void Render(SKCanvas canvas, SKRect bounds,
        Action<SKCanvas, SKRect> renderOldScene,
        Action<SKCanvas, SKRect> renderNewScene)
    {
        var eased = Ease(Progress);

        // Diagonale von oben-rechts nach unten-links bewegt sich nach rechts
        var w = bounds.Width;
        var h = bounds.Height;
        var slant = w * 0.15f; // Schräge des Schnitts

        // Position der Diagonale (von links nach rechts)
        var cutX = -slant + (w + slant * 2) * eased;

        // Alte Szene rendern
        renderOldScene(canvas, bounds);

        // Neue Szene nur rechts der Diagonale (mit Clip)
        canvas.Save();
        using var clipPath = new SKPath();
        clipPath.MoveTo(cutX + slant, bounds.Top);
        clipPath.LineTo(bounds.Right + slant, bounds.Top);
        clipPath.LineTo(bounds.Right + slant, bounds.Bottom);
        clipPath.LineTo(cutX - slant, bounds.Bottom);
        clipPath.Close();

        canvas.ClipPath(clipPath);
        renderNewScene(canvas, bounds);
        canvas.Restore();

        // Diagonale Panel-Linie zeichnen
        if (eased > 0.02f && eased < 0.98f)
        {
            canvas.DrawLine(cutX + slant, bounds.Top, cutX - slant, bounds.Bottom, _borderPaint);
        }
    }
}
