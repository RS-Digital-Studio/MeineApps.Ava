using SkiaSharp;
using HandwerkerImperium.Models;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert das Kriegs-Protokoll als scrollbare Liste mit Farbkodierung.
/// Einträge faden ein, neue Einträge haben Glow-Effekt.
/// Alle Paints gecacht, 0 GC pro Frame.
/// </summary>
public sealed class GuildWarLogRenderer : IDisposable
{
    private bool _disposed;
    private float _time;

    private const int MaxVisibleEntries = 10;
    private const float EntryHeight = 24;

    // Gecachte Paints
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKFont _textFont = new() { Edging = SKFontEdging.Antialias };
    private readonly SKFont _pointsFont = new() { Embolden = true, Edging = SKFontEdging.Antialias };

    private static readonly SKColor ScoreColor = new(0xEA, 0x58, 0x0C);
    private static readonly SKColor TextColor = new(0xAA, 0xAA, 0xAA);

    // Gecachte Punkte-Strings (vermeidet $"+{x}" pro Eintrag pro Frame)
    private readonly Dictionary<long, string> _pointsStringCache = new();

    /// <summary>
    /// Rendert die War-Log-Liste.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, IReadOnlyList<GuildWarLogEntry>? entries, float deltaTime)
    {
        if (entries == null || entries.Count == 0) return;
        _time += deltaTime;

        float x = bounds.Left;
        float w = bounds.Width;
        float y = bounds.Top;

        _textFont.Size = 11;
        _pointsFont.Size = 11;

        int count = Math.Min(entries.Count, MaxVisibleEntries);
        for (int i = 0; i < count; i++)
        {
            var entry = entries[i];
            float ey = y + i * EntryHeight;

            // Zebrastreifen-Hintergrund
            if (i % 2 == 0)
            {
                _fillPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 5);
                canvas.DrawRoundRect(x, ey, w, EntryHeight - 2, 3, 3, _fillPaint);
            }

            // Glow für neueste Einträge (erste 2)
            if (i < 2)
            {
                float glow = MathF.Max(0, 1f - _time * 0.2f);
                _fillPaint.Color = ScoreColor.WithAlpha((byte)(15 * glow));
                canvas.DrawRoundRect(x, ey, w, EntryHeight - 2, 3, 3, _fillPaint);
            }

            // Nachricht
            _fillPaint.Color = TextColor;
            canvas.DrawText(entry.Message ?? "", x + 4, ey + 15, SKTextAlign.Left, _textFont, _fillPaint);

            // Punkte (gecachter String pro Wert)
            if (!_pointsStringCache.TryGetValue(entry.Points, out var pointsText))
            {
                pointsText = $"+{entry.Points}";
                _pointsStringCache[entry.Points] = pointsText;
            }
            _fillPaint.Color = ScoreColor;
            canvas.DrawText(pointsText, x + w - 4, ey + 15, SKTextAlign.Right, _pointsFont, _fillPaint);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fillPaint.Dispose();
        _textFont.Dispose();
        _pointsFont.Dispose();
    }
}
