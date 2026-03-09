namespace RebornSaga.Rendering.Effects;

using SkiaSharp;
using System;

/// <summary>
/// Splittet den Screen in Manga-Panels mit schwarzen Rändern.
/// Anwendung: Dramatic Moment (2-3 Panels), Kampf-Eröffnung, Entscheidungs-Szene.
/// </summary>
public class MangaPanelRenderer
{
    private static readonly SKPaint _borderPaint = new()
    {
        Color = SKColors.Black,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 4f,
        IsAntialias = true
    };

    private static readonly SKPaint _bgPaint = new()
    {
        Color = SKColors.Black,
        IsAntialias = false
    };

    /// <summary>
    /// Rendert den Inhalt als 2 vertikale Panels (oben/unten) mit diagonalem Schnitt.
    /// </summary>
    public static void RenderDualPanel(SKCanvas canvas, SKRect bounds,
        Action<SKCanvas, SKRect> renderTop,
        Action<SKCanvas, SKRect> renderBottom,
        float slant = 0.08f)
    {
        var midY = bounds.MidY;
        var slantPx = bounds.Width * slant;

        // Schwarzer Hintergrund (sichtbar als Panel-Rand)
        canvas.DrawRect(bounds, _bgPaint);

        // Oberes Panel
        canvas.Save();
        using (var topClip = new SKPath())
        {
            topClip.MoveTo(bounds.Left, bounds.Top);
            topClip.LineTo(bounds.Right, bounds.Top);
            topClip.LineTo(bounds.Right, midY - slantPx);
            topClip.LineTo(bounds.Left, midY + slantPx);
            topClip.Close();
            canvas.ClipPath(topClip);

            var topBounds = new SKRect(bounds.Left, bounds.Top, bounds.Right, midY + slantPx);
            renderTop(canvas, topBounds);
        }
        canvas.Restore();

        // Unteres Panel
        canvas.Save();
        using (var bottomClip = new SKPath())
        {
            bottomClip.MoveTo(bounds.Left, midY + slantPx + 4);
            bottomClip.LineTo(bounds.Right, midY - slantPx + 4);
            bottomClip.LineTo(bounds.Right, bounds.Bottom);
            bottomClip.LineTo(bounds.Left, bounds.Bottom);
            bottomClip.Close();
            canvas.ClipPath(bottomClip);

            var bottomBounds = new SKRect(bounds.Left, midY - slantPx + 4, bounds.Right, bounds.Bottom);
            renderBottom(canvas, bottomBounds);
        }
        canvas.Restore();

        // Diagonale Trennlinie
        canvas.DrawLine(bounds.Left, midY + slantPx, bounds.Right, midY - slantPx, _borderPaint);
    }

    /// <summary>
    /// Rendert den Inhalt als 3 vertikale Panels (oben, mitte, unten).
    /// </summary>
    public static void RenderTriplePanel(SKCanvas canvas, SKRect bounds,
        Action<SKCanvas, SKRect> renderTop,
        Action<SKCanvas, SKRect> renderMiddle,
        Action<SKCanvas, SKRect> renderBottom,
        float slant = 0.05f)
    {
        var thirdH = bounds.Height / 3f;
        var slantPx = bounds.Width * slant;
        var gap = 3f;

        canvas.DrawRect(bounds, _bgPaint);

        // Oberes Drittel
        canvas.Save();
        using (var clip = new SKPath())
        {
            clip.MoveTo(bounds.Left, bounds.Top);
            clip.LineTo(bounds.Right, bounds.Top);
            clip.LineTo(bounds.Right, bounds.Top + thirdH - slantPx);
            clip.LineTo(bounds.Left, bounds.Top + thirdH + slantPx);
            clip.Close();
            canvas.ClipPath(clip);
            renderTop(canvas, new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + thirdH + slantPx));
        }
        canvas.Restore();

        // Mittleres Drittel
        canvas.Save();
        using (var clip = new SKPath())
        {
            clip.MoveTo(bounds.Left, bounds.Top + thirdH + slantPx + gap);
            clip.LineTo(bounds.Right, bounds.Top + thirdH - slantPx + gap);
            clip.LineTo(bounds.Right, bounds.Top + thirdH * 2 - slantPx);
            clip.LineTo(bounds.Left, bounds.Top + thirdH * 2 + slantPx);
            clip.Close();
            canvas.ClipPath(clip);
            renderMiddle(canvas, new SKRect(bounds.Left, bounds.Top + thirdH - slantPx + gap,
                bounds.Right, bounds.Top + thirdH * 2 + slantPx));
        }
        canvas.Restore();

        // Unteres Drittel
        canvas.Save();
        using (var clip = new SKPath())
        {
            clip.MoveTo(bounds.Left, bounds.Top + thirdH * 2 + slantPx + gap);
            clip.LineTo(bounds.Right, bounds.Top + thirdH * 2 - slantPx + gap);
            clip.LineTo(bounds.Right, bounds.Bottom);
            clip.LineTo(bounds.Left, bounds.Bottom);
            clip.Close();
            canvas.ClipPath(clip);
            renderBottom(canvas, new SKRect(bounds.Left, bounds.Top + thirdH * 2 - slantPx + gap,
                bounds.Right, bounds.Bottom));
        }
        canvas.Restore();

        // Trennlinien
        canvas.DrawLine(bounds.Left, bounds.Top + thirdH + slantPx,
            bounds.Right, bounds.Top + thirdH - slantPx, _borderPaint);
        canvas.DrawLine(bounds.Left, bounds.Top + thirdH * 2 + slantPx,
            bounds.Right, bounds.Top + thirdH * 2 - slantPx, _borderPaint);
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _borderPaint.Dispose();
        _bgPaint.Dispose();
    }
}
