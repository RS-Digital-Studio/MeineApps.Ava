using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace WorkTimePro.Graphics;

/// <summary>
/// Zeichnet einen zentrierten, dezenten "Keine Daten"-Platzhalter, wenn ein Chart
/// keine darstellbaren Werte hat — verhindert, dass leere Karten "kaputt" wirken.
/// </summary>
public static class ChartEmptyState
{
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _font = new() { Size = 13f };

    public static void Draw(SKCanvas canvas, SKRect bounds, string label)
    {
        _textPaint.Color = SkiaThemeHelper.TextMuted;
        canvas.DrawText(label, bounds.MidX, bounds.MidY + 4f, SKTextAlign.Center, _font, _textPaint);
    }
}
