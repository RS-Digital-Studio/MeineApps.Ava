using System;
using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Rendert holografische Quick-Action Buttons im VitalOS Medical-Design.
/// Verwendet für +kg, +250ml, +kcal Dashboard-Buttons.
/// Static Class - kein Render-Loop (20fps), daher using var für Paints OK.
/// </summary>
public static class QuickActionButtonRenderer
{
    // Konstanten
    private const float CornerRadius = 12f;
    private const float BorderWidth = 1.5f;
    private const float IconCircleDiameter = 28f;
    private const float IconCirclePadding = 8f;
    private const float LabelFontSize = 14f;
    private const float PulsPeriod = 3f; // Puls-Zyklus in Sekunden

    /// <summary>
    /// Zeichnet einen holografischen Quick-Action Button.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich (von canvas.LocalClipBounds)</param>
    /// <param name="label">Button-Text (z.B. "+kg", "+250 ml", "+kcal")</param>
    /// <param name="iconSymbol">Icon-Typ: "weight", "water" oder "fire"</param>
    /// <param name="featureColor">Feature-Farbe aus MedicalColors</param>
    /// <param name="time">Aktuelle Render-Zeit für Puls-Animation</param>
    /// <param name="isPressed">Ob der Button gerade gedrückt wird</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        string label, string iconSymbol, SKColor featureColor, float time, bool isPressed)
    {
        // Press-Effekt: Scale 0.95 vom Zentrum
        if (isPressed)
        {
            canvas.Save();
            canvas.Scale(0.95f, 0.95f, bounds.MidX, bounds.MidY);
        }

        // Innere Bounds mit kleinem Inset (damit der Rand nicht abgeschnitten wird)
        var innerBounds = new SKRect(
            bounds.Left + BorderWidth,
            bounds.Top + BorderWidth,
            bounds.Right - BorderWidth,
            bounds.Bottom - BorderWidth);

        // 1. Hintergrund: Feature-Farbe bei 25% Alpha, RoundRect
        RenderBackground(canvas, innerBounds, featureColor, isPressed);

        // 2. Rand: Feature-Farbe mit pulsierender Opacity
        RenderPulsingBorder(canvas, innerBounds, featureColor, time);

        // 3. Icon-Bereich: Kreis links mit Icon
        float iconCenterX = innerBounds.Left + IconCirclePadding + IconCircleDiameter / 2f;
        float iconCenterY = innerBounds.MidY;
        RenderIconCircle(canvas, iconCenterX, iconCenterY, featureColor, iconSymbol);

        // 4. Label: Weiß, 14pt, Bold, rechts neben Icon
        float labelX = iconCenterX + IconCircleDiameter / 2f + 8f;
        RenderLabel(canvas, labelX, innerBounds.MidY, label);

        if (isPressed)
            canvas.Restore();
    }

    // =====================================================================
    // Schicht 1: Halbtransparenter Hintergrund
    // =====================================================================

    private static void RenderBackground(SKCanvas canvas, SKRect bounds, SKColor featureColor, bool isPressed)
    {
        // Bei Pressed-State etwas heller (35% statt 25%)
        byte alpha = isPressed ? (byte)89 : (byte)64;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = featureColor.WithAlpha(alpha)
        };

        canvas.DrawRoundRect(bounds, CornerRadius, CornerRadius, paint);
    }

    // =====================================================================
    // Schicht 2: Pulsierender Rand
    // =====================================================================

    private static void RenderPulsingBorder(SKCanvas canvas, SKRect bounds, SKColor featureColor, float time)
    {
        // Puls-Formel: Opacity zwischen 40% und 80% (3 Sekunden Zyklus)
        float pulseFactor = 0.4f + 0.4f * (0.5f + 0.5f * MathF.Sin(time * MathF.PI * 2f / PulsPeriod));
        byte borderAlpha = (byte)(pulseFactor * 255f);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = BorderWidth,
            Color = featureColor.WithAlpha(borderAlpha)
        };

        canvas.DrawRoundRect(bounds, CornerRadius, CornerRadius, paint);
    }

    // =====================================================================
    // Schicht 3: Icon-Kreis mit Symbol
    // =====================================================================

    private static void RenderIconCircle(SKCanvas canvas, float cx, float cy, SKColor featureColor, string iconSymbol)
    {
        float radius = IconCircleDiameter / 2f;

        // Kreis-Hintergrund: Feature-Farbe bei 40% Alpha
        using var circlePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = featureColor.WithAlpha(102)
        };
        canvas.DrawCircle(cx, cy, radius, circlePaint);

        // Icon innerhalb des Kreises zeichnen
        float iconSize = radius * 0.7f;
        switch (iconSymbol)
        {
            case "weight":
                DrawScaleIcon(canvas, cx, cy, iconSize);
                break;
            case "water":
                DrawDropIcon(canvas, cx, cy, iconSize);
                break;
            case "fire":
                DrawFlameIcon(canvas, cx, cy, iconSize);
                break;
        }
    }

    // =====================================================================
    // Schicht 4: Label-Text
    // =====================================================================

    private static void RenderLabel(SKCanvas canvas, float x, float cy, string label)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = MedicalColors.TextPrimary,
            TextSize = LabelFontSize,
            FakeBoldText = true,
            TextAlign = SKTextAlign.Left
        };

        // Vertikale Zentrierung: Textmetrik verwenden
        var metrics = paint.FontMetrics;
        float textY = cy - (metrics.Ascent + metrics.Descent) / 2f;

        canvas.DrawText(label, x, textY, paint);
    }

    // =====================================================================
    // Icon-Zeichenmethoden
    // =====================================================================

    /// <summary>
    /// Waagen-Icon: Horizontaler Balken + vertikale Stütze + Dreieck-Basis.
    /// </summary>
    private static void DrawScaleIcon(SKCanvas canvas, float x, float y, float size)
    {
        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = SKColors.White
        };

        float s = size;

        // Waage: Balken oben
        canvas.DrawLine(x - s, y - s * 0.3f, x + s, y - s * 0.3f, strokePaint);
        // Stütze vertikal
        canvas.DrawLine(x, y - s * 0.3f, x, y + s * 0.4f, strokePaint);

        // Dreieck-Basis
        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White
        };

        using var path = new SKPath();
        path.MoveTo(x - s * 0.5f, y + s * 0.6f);
        path.LineTo(x + s * 0.5f, y + s * 0.6f);
        path.LineTo(x, y + s * 0.3f);
        path.Close();
        canvas.DrawPath(path, fillPaint);
    }

    /// <summary>
    /// Wasser-Tropfen-Icon: Bezier-Tropfenform.
    /// </summary>
    private static void DrawDropIcon(SKCanvas canvas, float x, float y, float size)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White
        };

        float s = size;

        using var path = new SKPath();
        path.MoveTo(x, y - s);  // Spitze oben
        path.CubicTo(
            x - s * 0.8f, y + s * 0.1f,
            x - s * 0.6f, y + s * 0.8f,
            x, y + s);
        path.CubicTo(
            x + s * 0.6f, y + s * 0.8f,
            x + s * 0.8f, y + s * 0.1f,
            x, y - s);
        path.Close();

        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Flammen-Icon: Bezier-Flammenform.
    /// </summary>
    private static void DrawFlameIcon(SKCanvas canvas, float x, float y, float size)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White
        };

        float s = size;

        using var path = new SKPath();
        path.MoveTo(x, y - s);                             // Flammenspitze oben
        path.CubicTo(x + s * 0.6f, y - s * 0.2f,          // Rechte Seite nach außen
                      x + s * 0.7f, y + s * 0.5f,
                      x, y + s);                            // Unten Mitte
        path.CubicTo(x - s * 0.7f, y + s * 0.5f,          // Linke Seite nach außen
                      x - s * 0.6f, y - s * 0.2f,
                      x, y - s);                            // Zurück zur Spitze
        path.Close();

        canvas.DrawPath(path, paint);
    }
}
