using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace WorkTimePro.Graphics;

/// <summary>
/// SkiaSharp 24h-Timeline für die TodayView.
/// Zeigt Arbeitsblöcke und Pausen als farbige Segmente auf einem Tagesverlauf.
/// </summary>
public static class DayTimelineVisualization
{
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _blockPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _nowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _tickPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
    private static readonly SKFont _hourFont = new() { Size = 9f };
    private static readonly SKFont _labelFont = new() { Size = 10f };

    /// <summary>
    /// Ein Zeitblock im Tagesverlauf.
    /// </summary>
    public readonly struct TimeBlock
    {
        public readonly float StartHour;  // 0-24 (z.B. 8.5 = 08:30)
        public readonly float EndHour;
        public readonly bool IsPause;     // true = Pause, false = Arbeit

        public TimeBlock(float startHour, float endHour, bool isPause)
        {
            StartHour = startHour;
            EndHour = endHour;
            IsPause = isPause;
        }
    }

    /// <summary>
    /// Rendert die 24h-Tages-Timeline.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="blocks">Arbeits-/Pausen-Blöcke</param>
    /// <param name="currentHour">Aktuelle Stunde (z.B. 14.75 = 14:45), -1 wenn nicht anzeigen</param>
    /// <param name="startHour">Beginn der sichtbaren Achse (Standard: 6)</param>
    /// <param name="endHour">Ende der sichtbaren Achse (Standard: 20)</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        TimeBlock[] blocks, float currentHour = -1,
        int startHour = 6, int endHour = 20)
    {
        float padding = 12f;
        float labelH = 16f;
        float barH = 24f;
        float barTop = bounds.Top + padding;
        float barBottom = barTop + barH;
        float barLeft = bounds.Left + padding + 20f; // Platz für Labels links
        float barRight = bounds.Right - padding;
        float barW = barRight - barLeft;

        if (barW <= 20) return;

        float hourRange = endHour - startHour;
        if (hourRange <= 0) hourRange = 14;

        // Hintergrund-Balken (Track)
        _bgPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 25);
        canvas.DrawRoundRect(new SKRect(barLeft, barTop, barRight, barBottom), 4f, 4f, _bgPaint);

        // Stunden-Ticks und Labels
        for (int h = startHour; h <= endHour; h++)
        {
            float x = barLeft + ((h - startHour) / hourRange) * barW;
            bool isMajor = h % 3 == 0;

            _tickPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, (byte)(isMajor ? 80 : 30));
            canvas.DrawLine(x, barTop, x, barBottom, _tickPaint);

            if (isMajor)
            {
                _textPaint.Color = SkiaThemeHelper.TextMuted;
                _hourFont.Size = 9f;
                canvas.DrawText($"{h}", x, barBottom + labelH, SKTextAlign.Center, _hourFont, _textPaint);
            }
        }

        // Zeitblöcke zeichnen
        foreach (var block in blocks)
        {
            float bStart = Math.Clamp(block.StartHour, startHour, endHour);
            float bEnd = Math.Clamp(block.EndHour, startHour, endHour);
            if (bEnd <= bStart) continue;

            float x1 = barLeft + ((bStart - startHour) / hourRange) * barW;
            float x2 = barLeft + ((bEnd - startHour) / hourRange) * barW;

            var blockRect = new SKRect(x1, barTop + 1, x2, barBottom - 1);

            if (block.IsPause)
            {
                // Pause: Orange schraffiert
                _blockPaint.Color = SkiaThemeHelper.Warning.WithAlpha(60);
                canvas.DrawRect(blockRect, _blockPaint);

                // Schraffur
                _tickPaint.Color = SkiaThemeHelper.Warning.WithAlpha(80);
                _tickPaint.StrokeWidth = 0.8f;
                canvas.Save();
                canvas.ClipRect(blockRect);
                for (float sx = x1 - barH; sx < x2 + barH; sx += 6f)
                {
                    canvas.DrawLine(sx, barBottom - 1, sx + barH, barTop + 1, _tickPaint);
                }
                canvas.Restore();
                _tickPaint.StrokeWidth = 0.5f;
            }
            else
            {
                // Arbeit: Grün mit Gradient
                using (var shader = SKShader.CreateLinearGradient(
                    new SKPoint(x1, barTop),
                    new SKPoint(x1, barBottom),
                    new[] { SkiaThemeHelper.Success.WithAlpha(200), SkiaThemeHelper.Success.WithAlpha(140) },
                    null, SKShaderTileMode.Clamp))
                {
                    _blockPaint.Shader = shader;
                    canvas.DrawRoundRect(blockRect, 2f, 2f, _blockPaint);
                    _blockPaint.Shader = null;
                }
            }
        }

        // Aktuelle-Zeit-Markierung (rote Linie)
        if (currentHour >= startHour && currentHour <= endHour)
        {
            float nowX = barLeft + ((currentHour - startHour) / hourRange) * barW;
            _nowPaint.Color = SkiaThemeHelper.Error;
            canvas.DrawLine(nowX, barTop - 3f, nowX, barBottom + 3f, _nowPaint);

            // Kleiner Punkt oben
            _blockPaint.Color = SkiaThemeHelper.Error;
            canvas.DrawCircle(nowX, barTop - 3f, 3f, _blockPaint);
        }
    }
}
