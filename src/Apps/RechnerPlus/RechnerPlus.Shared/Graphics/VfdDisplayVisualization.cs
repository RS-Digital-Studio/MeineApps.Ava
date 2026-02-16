using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace RechnerPlus.Graphics;

/// <summary>
/// SkiaSharp VFD (Vacuum Fluorescent Display) Visualisierung.
/// Zeichnet den Taschenrechner-Wert als leuchtende 7-Segment-Ziffern mit Glow-Effekt.
/// </summary>
public static class VfdDisplayVisualization
{
    private static readonly SKPaint _segmentPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _ghostPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);

    // Segment-Definitionen: 7 Segmente (a-g) pro Ziffer
    //   aaa
    //  f   b
    //   ggg
    //  e   c
    //   ddd
    // Jede Ziffer als bool[7]: {a,b,c,d,e,f,g}
    private static readonly Dictionary<char, bool[]> _segmentMap = new()
    {
        ['0'] = new[] { true, true, true, true, true, true, false },
        ['1'] = new[] { false, true, true, false, false, false, false },
        ['2'] = new[] { true, true, false, true, true, false, true },
        ['3'] = new[] { true, true, true, true, false, false, true },
        ['4'] = new[] { false, true, true, false, false, true, true },
        ['5'] = new[] { true, false, true, true, false, true, true },
        ['6'] = new[] { true, false, true, true, true, true, true },
        ['7'] = new[] { true, true, true, false, false, false, false },
        ['8'] = new[] { true, true, true, true, true, true, true },
        ['9'] = new[] { true, true, true, true, false, true, true },
        ['-'] = new[] { false, false, false, false, false, false, true },
        ['E'] = new[] { true, false, false, true, true, true, true },
        ['r'] = new[] { false, false, false, false, true, false, true },
        [' '] = new[] { false, false, false, false, false, false, false },
    };

    /// <summary>
    /// Rendert den VFD-Display mit leuchtenden 7-Segment-Ziffern.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="displayText">Anzuzeigender Text (Ziffern, Punkt, Minus)</param>
    /// <param name="hasError">Fehlerzustand (rot statt cyan)</param>
    /// <param name="animTime">Animations-Timer für subtiles Flackern</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        string displayText, bool hasError, float animTime)
    {
        if (string.IsNullOrEmpty(displayText)) return;

        // VFD-Farbe: Cyan-Grün bei normal, Rot bei Fehler
        SKColor vfdColor = hasError
            ? SKColor.Parse("#FF4444")
            : SKColor.Parse("#00FFB0");

        // Subtiles Flackern (±3% Helligkeit wie echte VFD-Röhren)
        float flicker = 1f + 0.03f * MathF.Sin(animTime * 44f); // ~7Hz
        byte alpha = (byte)Math.Clamp(255 * flicker, 230, 255);
        vfdColor = vfdColor.WithAlpha(alpha);

        float padding = 12f;
        float availW = bounds.Width - padding * 2;
        float availH = bounds.Height - padding * 2;

        // Maximale Zeichenzahl (Punkte/Kommas zählen halb)
        int charCount = 0;
        int dotCount = 0;
        foreach (char c in displayText)
        {
            if (c == '.' || c == ',') dotCount++;
            else charCount++;
        }

        // Segmentgröße berechnen (passt in verfügbare Breite)
        float digitW = Math.Min(availW / Math.Max(charCount + dotCount * 0.4f, 1), availH * 0.6f);
        float digitH = Math.Min(availH * 0.85f, digitW * 1.8f);
        float segW = digitW * 0.7f;  // Segment-Breite
        float segH = digitH * 0.08f; // Segment-Dicke

        // Startposition (rechtsbündig)
        float totalW = 0;
        foreach (char c in displayText)
        {
            totalW += (c == '.' || c == ',') ? digitW * 0.35f : digitW;
        }

        float x = bounds.Right - padding - Math.Min(totalW, availW);
        float y = bounds.MidY - digitH / 2f;

        // Geister-Segmente (alle Segmente dezent sichtbar)
        _ghostPaint.Color = vfdColor.WithAlpha(12);

        foreach (char c in displayText)
        {
            if (c == '.' || c == ',')
            {
                // Dezimalpunkt
                DrawDot(canvas, x + digitW * 0.15f, y + digitH, segH, vfdColor);
                x += digitW * 0.35f;
                continue;
            }

            // Geister-Segmente (8 alle aktiv) für VFD-Effekt
            DrawDigitGhost(canvas, x, y, segW, digitH, segH);

            // Aktive Segmente
            if (_segmentMap.TryGetValue(c, out var segments))
            {
                DrawDigit(canvas, x, y, segW, digitH, segH, segments, vfdColor);
            }

            x += digitW;
        }
    }

    /// <summary>
    /// Zeichnet eine einzelne 7-Segment-Ziffer.
    /// </summary>
    private static void DrawDigit(SKCanvas canvas, float x, float y,
        float w, float h, float segH, bool[] segments, SKColor color)
    {
        float halfH = h / 2f;
        float margin = segH * 0.3f;

        // Glow-Effekt für aktive Segmente
        _glowPaint.Color = color.WithAlpha(50);
        _glowPaint.MaskFilter = _glowFilter;

        _segmentPaint.Color = color;

        // a: oben horizontal
        if (segments[0])
        {
            DrawHSegment(canvas, x + margin, y, w - margin * 2, segH, color);
        }
        // b: oben rechts vertikal
        if (segments[1])
        {
            DrawVSegment(canvas, x + w - segH, y + margin, segH, halfH - margin * 2, color);
        }
        // c: unten rechts vertikal
        if (segments[2])
        {
            DrawVSegment(canvas, x + w - segH, y + halfH + margin, segH, halfH - margin * 2, color);
        }
        // d: unten horizontal
        if (segments[3])
        {
            DrawHSegment(canvas, x + margin, y + h - segH, w - margin * 2, segH, color);
        }
        // e: unten links vertikal
        if (segments[4])
        {
            DrawVSegment(canvas, x, y + halfH + margin, segH, halfH - margin * 2, color);
        }
        // f: oben links vertikal
        if (segments[5])
        {
            DrawVSegment(canvas, x, y + margin, segH, halfH - margin * 2, color);
        }
        // g: mitte horizontal
        if (segments[6])
        {
            DrawHSegment(canvas, x + margin, y + halfH - segH / 2f, w - margin * 2, segH, color);
        }
    }

    /// <summary>
    /// Zeichnet Geister-Segmente (alle 7 Segmente dezent sichtbar wie bei echtem VFD).
    /// </summary>
    private static void DrawDigitGhost(SKCanvas canvas, float x, float y,
        float w, float h, float segH)
    {
        float halfH = h / 2f;
        float margin = segH * 0.3f;
        var ghostColor = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 10);

        DrawHSegment(canvas, x + margin, y, w - margin * 2, segH, ghostColor);
        DrawVSegment(canvas, x + w - segH, y + margin, segH, halfH - margin * 2, ghostColor);
        DrawVSegment(canvas, x + w - segH, y + halfH + margin, segH, halfH - margin * 2, ghostColor);
        DrawHSegment(canvas, x + margin, y + h - segH, w - margin * 2, segH, ghostColor);
        DrawVSegment(canvas, x, y + halfH + margin, segH, halfH - margin * 2, ghostColor);
        DrawVSegment(canvas, x, y + margin, segH, halfH - margin * 2, ghostColor);
        DrawHSegment(canvas, x + margin, y + halfH - segH / 2f, w - margin * 2, segH, ghostColor);
    }

    /// <summary>
    /// Zeichnet ein horizontales Segment mit Glow.
    /// </summary>
    private static void DrawHSegment(SKCanvas canvas, float x, float y, float w, float h, SKColor color)
    {
        var rect = new SKRect(x, y, x + w, y + h);

        // Glow
        _glowPaint.Color = color.WithAlpha(40);
        _glowPaint.MaskFilter = _glowFilter;
        canvas.DrawRoundRect(rect, h / 2f, h / 2f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Segment
        _segmentPaint.Color = color;
        canvas.DrawRoundRect(rect, h / 2f, h / 2f, _segmentPaint);
    }

    /// <summary>
    /// Zeichnet ein vertikales Segment mit Glow.
    /// </summary>
    private static void DrawVSegment(SKCanvas canvas, float x, float y, float w, float h, SKColor color)
    {
        var rect = new SKRect(x, y, x + w, y + h);

        // Glow
        _glowPaint.Color = color.WithAlpha(40);
        _glowPaint.MaskFilter = _glowFilter;
        canvas.DrawRoundRect(rect, w / 2f, w / 2f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Segment
        _segmentPaint.Color = color;
        canvas.DrawRoundRect(rect, w / 2f, w / 2f, _segmentPaint);
    }

    /// <summary>
    /// Zeichnet einen Dezimalpunkt mit Glow.
    /// </summary>
    private static void DrawDot(SKCanvas canvas, float x, float y, float size, SKColor color)
    {
        // Glow
        _glowPaint.Color = color.WithAlpha(50);
        _glowPaint.MaskFilter = _glowFilter;
        canvas.DrawCircle(x, y - size, size * 1.5f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Punkt
        _segmentPaint.Color = color;
        canvas.DrawCircle(x, y - size, size * 0.7f, _segmentPaint);
    }
}
