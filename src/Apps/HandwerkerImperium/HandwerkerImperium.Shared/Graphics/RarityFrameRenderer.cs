using System;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Seltenheitsstufe für Rahmen-Rendering.
/// </summary>
public enum Rarity
{
    Common,     // Grau, einfach
    Uncommon,   // Grün, subtiler Puls
    Rare,       // Blau, Glow-Aura
    Epic,       // Lila, Sparkle-Partikel an Ecken
    Legendary   // Rainbow animiert, Shimmer
}

/// <summary>
/// Statischer SkiaSharp-Renderer für visuell animierte Rahmen je nach Seltenheit.
/// Wiederverwendbar für Worker, Equipment, Rewards, Feature-Cards etc.
/// Alle SKPaint-Objekte gecacht als static readonly, keine Allokationen im Draw-Pfad.
/// </summary>
public static class RarityFrameRenderer
{
    // ═══════════════════════════════════════════════════════════════════════
    // Farben je Seltenheit
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly SKColor ColorCommon   = new(0x94, 0xA3, 0xB8); // Slate/Grau
    private static readonly SKColor ColorUncommon = new(0x22, 0xC5, 0x5E); // Grün
    private static readonly SKColor ColorRare     = new(0x3B, 0x82, 0xF6); // Blau
    private static readonly SKColor ColorEpic     = new(0xA8, 0x55, 0xF7); // Lila

    // ═══════════════════════════════════════════════════════════════════════
    // Gecachte Paints (EINMALIG erstellt, Felder werden pro Draw gesetzt)
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly SKPaint _framePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke
    };

    private static readonly SKPaint _glowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint _sparklePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    // MaskFilter für Glow-Unschärfe – einmalig erstellt, unveränderlich
    private static readonly SKMaskFilter _blurFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

    // ═══════════════════════════════════════════════════════════════════════
    // Öffentliche Haupt-Methode
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet den zur Seltenheit passenden Rahmen in die gegebenen Bounds.
    /// </summary>
    /// <param name="canvas">Ziel-Canvas</param>
    /// <param name="bounds">Bereich für den Rahmen (vom Aufrufer vorgegeben)</param>
    /// <param name="rarity">Seltenheitsstufe</param>
    /// <param name="time">Animationszeit in Sekunden (monoton steigend)</param>
    /// <param name="cornerRadius">Ecken-Radius in dp</param>
    public static void DrawRarityFrame(SKCanvas canvas, SKRect bounds, Rarity rarity, float time, float cornerRadius = 8f)
    {
        switch (rarity)
        {
            case Rarity.Common:
                DrawCommonFrame(canvas, bounds, cornerRadius);
                break;
            case Rarity.Uncommon:
                DrawUncommonFrame(canvas, bounds, time, cornerRadius);
                break;
            case Rarity.Rare:
                DrawRareFrame(canvas, bounds, time, cornerRadius);
                break;
            case Rarity.Epic:
                DrawEpicFrame(canvas, bounds, time, cornerRadius);
                break;
            case Rarity.Legendary:
                DrawLegendaryFrame(canvas, bounds, time, cornerRadius);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Common – einfacher grauer Rand
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawCommonFrame(SKCanvas canvas, SKRect bounds, float cornerRadius)
    {
        _framePaint.Color = ColorCommon;
        _framePaint.StrokeWidth = 1f;
        _framePaint.MaskFilter = null;
        canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, _framePaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Uncommon – grüner Rand mit subtilем Alpha-Puls
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawUncommonFrame(SKCanvas canvas, SKRect bounds, float time, float cornerRadius)
    {
        // Alpha oszilliert zwischen 180 und 255 mit 3-Sekunden-Zyklus
        float pulse = (MathF.Sin(time * (MathF.PI * 2f / 3f)) + 1f) * 0.5f; // 0..1
        byte alpha  = (byte)(180 + pulse * 75f);                             // 180..255

        _framePaint.Color = ColorUncommon.WithAlpha(alpha);
        _framePaint.StrokeWidth = 2f;
        _framePaint.MaskFilter = null;
        canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, _framePaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Rare – blauer Rand + weiche Glow-Aura
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawRareFrame(SKCanvas canvas, SKRect bounds, float time, float cornerRadius)
    {
        // Glow-Aura: weich und pulsierend (Alpha 40–80)
        float pulse    = (MathF.Sin(time * MathF.PI) + 1f) * 0.5f; // 0..1, 2s Zyklus
        byte glowAlpha = (byte)(40 + pulse * 40f);

        _glowPaint.Color = ColorRare.WithAlpha(glowAlpha);
        _glowPaint.MaskFilter = _blurFilter;

        // Aura etwas größer als der eigentliche Rahmen
        var auraBounds = new SKRect(
            bounds.Left   - 3f,
            bounds.Top    - 3f,
            bounds.Right  + 3f,
            bounds.Bottom + 3f);
        canvas.DrawRoundRect(auraBounds, cornerRadius + 3f, cornerRadius + 3f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Scharfer innerer Rand
        _framePaint.Color = ColorRare;
        _framePaint.StrokeWidth = 2f;
        _framePaint.MaskFilter = null;
        canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, _framePaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Epic – lila Rand + lila Glow + Sparkles an den 4 Ecken
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawEpicFrame(SKCanvas canvas, SKRect bounds, float time, float cornerRadius)
    {
        // Lila Glow-Aura (wie Rare, aber lila)
        float pulse    = (MathF.Sin(time * MathF.PI) + 1f) * 0.5f;
        byte glowAlpha = (byte)(40 + pulse * 40f);

        _glowPaint.Color = ColorEpic.WithAlpha(glowAlpha);
        _glowPaint.MaskFilter = _blurFilter;

        var auraBounds = new SKRect(
            bounds.Left   - 3f,
            bounds.Top    - 3f,
            bounds.Right  + 3f,
            bounds.Bottom + 3f);
        canvas.DrawRoundRect(auraBounds, cornerRadius + 3f, cornerRadius + 3f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Scharfer lila Rand
        _framePaint.Color = ColorEpic;
        _framePaint.StrokeWidth = 2f;
        _framePaint.MaskFilter = null;
        canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, _framePaint);

        // Sparkles an 4 Ecken mit je versetztem Timing-Offset (0 / 0.25 / 0.5 / 0.75 * Zyklus)
        const float CycleSeconds = 1.6f;
        float sparkleBaseAlpha = 180f + pulse * 75f; // 180..255
        float sparkleSize = 4f + pulse * 2f;         // 4..6 dp

        float OffsetAlpha(float offset)
        {
            float t = (MathF.Sin((time + offset * CycleSeconds) * (MathF.PI * 2f / CycleSeconds)) + 1f) * 0.5f;
            return 180f + t * 75f;
        }

        float OffsetRotation(float offset) => (time / CycleSeconds + offset) * 360f % 360f;

        DrawSparkle(canvas, bounds.Left,  bounds.Top,    sparkleSize, OffsetRotation(0f),    ColorEpic, OffsetAlpha(0f));
        DrawSparkle(canvas, bounds.Right, bounds.Top,    sparkleSize, OffsetRotation(0.25f), ColorEpic, OffsetAlpha(0.25f));
        DrawSparkle(canvas, bounds.Right, bounds.Bottom, sparkleSize, OffsetRotation(0.5f),  ColorEpic, OffsetAlpha(0.5f));
        DrawSparkle(canvas, bounds.Left,  bounds.Bottom, sparkleSize, OffsetRotation(0.75f), ColorEpic, OffsetAlpha(0.75f));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Legendary – Rainbow-Hue-Shift + Shimmer + rotierende Sparkle-Sterne
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawLegendaryFrame(SKCanvas canvas, SKRect bounds, float time, float cornerRadius)
    {
        // Hue wandert alle 2 Sekunden einmal um 360°
        float baseHue = (time / 2f % 1f) * 360f;

        // Hauptrahmen: Gradient entlang des Rahmens via 4 Segmente (oben / rechts / unten / links)
        // Jedes Segment bekommt eine leicht versetzte Hue-Startfarbe für den Regenbogen-Eindruck
        DrawRainbowSegment(canvas, bounds.Left,  bounds.Top,    bounds.Right,  bounds.Top,    cornerRadius, baseHue,         baseHue + 90f);
        DrawRainbowSegment(canvas, bounds.Right, bounds.Top,    bounds.Right,  bounds.Bottom, cornerRadius, baseHue + 90f,   baseHue + 180f);
        DrawRainbowSegment(canvas, bounds.Right, bounds.Bottom, bounds.Left,   bounds.Bottom, cornerRadius, baseHue + 180f,  baseHue + 270f);
        DrawRainbowSegment(canvas, bounds.Left,  bounds.Bottom, bounds.Left,   bounds.Top,    cornerRadius, baseHue + 270f,  baseHue + 360f);

        // Shimmer: Ein heller Lichtblitz wandert entlang des Rahmens
        DrawShimmer(canvas, bounds, time, cornerRadius);

        // 6 kleine Sparkle-Sterne gleichmäßig um den Rahmen verteilt
        float perimeter = 2f * (bounds.Width + bounds.Height);
        for (int i = 0; i < 6; i++)
        {
            float fraction = i / 6f + (time / 3f % 1f); // gleichmäßig verteilt + wandernd
            fraction %= 1f;

            GetPointOnRect(bounds, fraction, out float px, out float py);

            float sparkleHue = (baseHue + i * 60f) % 360f;
            var sparkleColor = SKColor.FromHsv(sparkleHue, 70f, 100f);
            float rotation   = (time * 90f + i * 60f) % 360f;

            DrawSparkle(canvas, px, py, 3.5f, rotation, sparkleColor, 200f);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Hilfs-Methoden: Rainbow-Segment, Shimmer, Punkt auf Rahmen
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein einzelnes Segment des Legendary-Rainbow-Rahmens als Gradient-Linie.
    /// </summary>
    private static void DrawRainbowSegment(SKCanvas canvas,
        float x0, float y0, float x1, float y1,
        float cornerRadius,
        float hueStart, float hueEnd)
    {
        var colorStart = SKColor.FromHsv(hueStart % 360f, 80f, 100f);
        var colorEnd   = SKColor.FromHsv(hueEnd   % 360f, 80f, 100f);

        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(x0, y0),
            new SKPoint(x1, y1),
            new[] { colorStart, colorEnd },
            null,
            SKShaderTileMode.Clamp);

        _framePaint.Shader      = shader;
        _framePaint.StrokeWidth = 3f;
        _framePaint.MaskFilter  = null;
        canvas.DrawLine(x0, y0, x1, y1, _framePaint);
        _framePaint.Shader = null;
    }

    /// <summary>
    /// Zeichnet einen hellen Shimmer-Punkt, der entlang des Rahmens gleitet (3s Umlauf).
    /// </summary>
    private static void DrawShimmer(SKCanvas canvas, SKRect bounds, float time, float cornerRadius)
    {
        float fraction = (time / 3f) % 1f;
        GetPointOnRect(bounds, fraction, out float px, out float py);

        _glowPaint.Color      = new SKColor(255, 255, 255, 200);
        _glowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f);
        canvas.DrawCircle(px, py, 5f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Kleiner heller Kern
        _glowPaint.Color = SKColors.White;
        canvas.DrawCircle(px, py, 2f, _glowPaint);
    }

    /// <summary>
    /// Berechnet einen Punkt auf dem Umfang des Rechtecks (fraction 0..1).
    /// 0=oben-links, 0.25=oben-rechts, 0.5=unten-rechts, 0.75=unten-links.
    /// </summary>
    private static void GetPointOnRect(SKRect r, float fraction, out float x, out float y)
    {
        float perimeter = 2f * (r.Width + r.Height);
        float dist      = fraction * perimeter;

        if (dist < r.Width) // Obere Kante: links→rechts
        {
            x = r.Left + dist;
            y = r.Top;
        }
        else if (dist < r.Width + r.Height) // Rechte Kante: oben→unten
        {
            x = r.Right;
            y = r.Top + (dist - r.Width);
        }
        else if (dist < 2f * r.Width + r.Height) // Untere Kante: rechts→links
        {
            x = r.Right - (dist - r.Width - r.Height);
            y = r.Bottom;
        }
        else // Linke Kante: unten→oben
        {
            x = r.Left;
            y = r.Bottom - (dist - 2f * r.Width - r.Height);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Sparkle – 4-Punkt-Stern
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet einen 4-Punkt-Stern (8 Punkte: 4 Spitzen + 4 Einbuchtungen) an (cx, cy).
    /// </summary>
    /// <param name="canvas">Ziel-Canvas</param>
    /// <param name="cx">Mittelpunkt X</param>
    /// <param name="cy">Mittelpunkt Y</param>
    /// <param name="size">Radius der äußeren Spitzen in dp</param>
    /// <param name="rotation">Rotation in Grad</param>
    /// <param name="color">Füllfarbe</param>
    /// <param name="alpha">Alpha-Wert (0–255f)</param>
    private static void DrawSparkle(SKCanvas canvas, float cx, float cy, float size, float rotation, SKColor color, float alpha)
    {
        _sparklePaint.Color = color.WithAlpha((byte)Math.Clamp(alpha, 0f, 255f));

        float outerR = size;
        float innerR = size * 0.35f; // Einbuchtungs-Radius

        using var path = new SKPath();

        for (int i = 0; i < 8; i++)
        {
            // Winkel: 0° = oben, alle 45° ein Punkt (abwechselnd Spitze/Einbuchtung)
            float angle  = (i * 45f - 90f) * MathF.PI / 180f;
            float radius = (i % 2 == 0) ? outerR : innerR;
            float px     = MathF.Cos(angle) * radius;
            float py     = MathF.Sin(angle) * radius;

            if (i == 0)
                path.MoveTo(px, py);
            else
                path.LineTo(px, py);
        }

        path.Close();

        // Rotation + Translation via Canvas-State
        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees(rotation);
        canvas.DrawPath(path, _sparklePaint);
        canvas.Restore();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Öffentliche Hilfs-Methoden
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt die Hauptfarbe für die gegebene Seltenheit zurück.
    /// Legendary: HSV-animiert basierend auf <paramref name="time"/>.
    /// </summary>
    public static SKColor GetRarityColor(Rarity rarity, float time = 0f)
    {
        return rarity switch
        {
            Rarity.Common    => ColorCommon,
            Rarity.Uncommon  => ColorUncommon,
            Rarity.Rare      => ColorRare,
            Rarity.Epic      => ColorEpic,
            Rarity.Legendary => SKColor.FromHsv((time / 2f % 1f) * 360f, 80f, 100f),
            _                => ColorCommon
        };
    }

    /// <summary>
    /// Gibt eine subtile Hintergrundfarbe (Alpha 15–25) für die Seltenheit zurück.
    /// Geeignet für Card-Hintergründe oder Highlight-Flächen.
    /// </summary>
    public static SKColor GetRarityBackgroundColor(Rarity rarity)
    {
        return rarity switch
        {
            Rarity.Common    => ColorCommon.WithAlpha(15),
            Rarity.Uncommon  => ColorUncommon.WithAlpha(20),
            Rarity.Rare      => ColorRare.WithAlpha(22),
            Rarity.Epic      => ColorEpic.WithAlpha(25),
            Rarity.Legendary => new SKColor(0xFF, 0xD7, 0x00, 20), // Goldton, Alpha 20
            _                => ColorCommon.WithAlpha(15)
        };
    }
}
