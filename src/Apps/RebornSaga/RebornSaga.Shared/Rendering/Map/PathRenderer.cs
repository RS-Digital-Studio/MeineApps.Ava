namespace RebornSaga.Rendering.Map;

using SkiaSharp;
using System;

/// <summary>
/// Zeichnet animierte Pfade zwischen Map-Knoten.
/// Leuchtende pulsierende Linien mit Partikel-Effekt auf aktiven Pfaden.
/// </summary>
public static class PathRenderer
{
    // Farben
    private static readonly SKColor ActiveColor = new(0x4A, 0x90, 0xD9);    // Blau - freigeschalteter Pfad
    private static readonly SKColor InactiveColor = new(0x30, 0x36, 0x3D);  // Grau - gesperrter Pfad
    private static readonly SKColor CompletedColor = new(0xF3, 0x9C, 0x12); // Gold - erledigter Pfad

    // Gepoolte Paints
    private static readonly SKPaint _pathPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeWidth = 3f
    };
    private static readonly SKPaint _glowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeWidth = 6f
    };
    private static readonly SKPaint _dotPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    // Gecachte MaskFilter
    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);

    /// <summary>
    /// Zeichnet einen Pfad zwischen zwei Knoten-Positionen.
    /// </summary>
    /// <param name="canvas">Canvas zum Zeichnen.</param>
    /// <param name="fromX">Start-X.</param>
    /// <param name="fromY">Start-Y.</param>
    /// <param name="toX">Ziel-X.</param>
    /// <param name="toY">Ziel-Y.</param>
    /// <param name="isActive">Pfad freigeschaltet?</param>
    /// <param name="isCompleted">Beide Endknoten erledigt?</param>
    /// <param name="animTime">Animations-Zeit in Sekunden (für Puls-Effekt).</param>
    public static void Draw(SKCanvas canvas, float fromX, float fromY, float toX, float toY,
        bool isActive, bool isCompleted, float animTime)
    {
        if (!isActive && !isCompleted)
        {
            // Gestrichelte inaktive Linie
            DrawDashedLine(canvas, fromX, fromY, toX, toY);
            return;
        }

        var color = isCompleted ? CompletedColor : ActiveColor;

        // Glow-Effekt auf aktiven Pfaden
        var glowAlpha = (byte)Math.Min(255, 30 + (int)(20 * MathF.Sin(animTime * 2f)));
        _glowPaint.Color = color.WithAlpha(glowAlpha);
        _glowPaint.MaskFilter = _glowFilter;
        canvas.DrawLine(fromX, fromY, toX, toY, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Hauptlinie
        _pathPaint.Color = color.WithAlpha(180);
        canvas.DrawLine(fromX, fromY, toX, toY, _pathPaint);

        // Wandernder Leuchtpunkt auf aktiven (nicht erledigten) Pfaden
        if (isActive && !isCompleted)
            DrawTravelingDot(canvas, fromX, fromY, toX, toY, color, animTime);
    }

    /// <summary>
    /// Zeichnet eine gestrichelte Linie für inaktive Pfade.
    /// </summary>
    private static void DrawDashedLine(SKCanvas canvas, float fromX, float fromY, float toX, float toY)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        if (length < 1f) return;

        var dashLength = 6f;
        var gapLength = 4f;
        var segmentLength = dashLength + gapLength;
        var segments = (int)(length / segmentLength);

        var nx = dx / length;
        var ny = dy / length;

        _pathPaint.Color = InactiveColor.WithAlpha(80);

        for (int i = 0; i < segments; i++)
        {
            var startT = i * segmentLength;
            var endT = startT + dashLength;
            if (endT > length) endT = length;

            canvas.DrawLine(
                fromX + nx * startT, fromY + ny * startT,
                fromX + nx * endT, fromY + ny * endT,
                _pathPaint);
        }
    }

    /// <summary>
    /// Zeichnet einen leuchtenden Punkt der entlang des Pfades wandert.
    /// </summary>
    private static void DrawTravelingDot(SKCanvas canvas, float fromX, float fromY,
        float toX, float toY, SKColor color, float animTime)
    {
        // T-Wert (0-1) basierend auf Animation, Ping-Pong
        var t = (animTime * 0.5f) % 1f;
        var pingPong = t < 0.5f ? t * 2f : 2f - t * 2f;

        var dotX = fromX + (toX - fromX) * pingPong;
        var dotY = fromY + (toY - fromY) * pingPong;

        // Leuchtender Punkt
        _dotPaint.Color = color.WithAlpha(200);
        canvas.DrawCircle(dotX, dotY, 4f, _dotPaint);

        // Halo
        _dotPaint.Color = color.WithAlpha(60);
        canvas.DrawCircle(dotX, dotY, 8f, _dotPaint);
    }

    /// <summary>
    /// Gibt alle statischen nativen Ressourcen frei.
    /// </summary>
    public static void Cleanup()
    {
        _pathPaint.Dispose();
        _glowPaint.Dispose();
        _dotPaint.Dispose();
        // _glowFilter ist static readonly — NICHT disposen
    }
}
