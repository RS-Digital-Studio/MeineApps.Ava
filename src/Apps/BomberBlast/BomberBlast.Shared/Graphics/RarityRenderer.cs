using BomberBlast.Models;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Statische Draw-Methoden für Raritäts-Rahmen, Glow und Shimmer-Effekte.
/// Verwendet gepoolte SKPaint-Objekte für GC-Optimierung.
/// </summary>
public static class RarityRenderer
{
    // Gepoolte Paint-Objekte
    private static readonly SKPaint BorderPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke
    };

    private static readonly SKPaint GlowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke
    };

    private static readonly SKPaint ShimmerPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint FillPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    /// <summary>
    /// Statische Felder vorinitialisieren (SKPaint).
    /// Wird im SplashOverlay-Preloader aufgerufen um Jank beim ersten Render zu vermeiden.
    /// </summary>
    public static void Preload()
    {
        // Statische readonly-Felder werden durch diesen Methodenaufruf
        // vom CLR-Klassen-Initializer angelegt
    }

    /// <summary>
    /// Zeichnet einen Raritäts-Rahmen um ein Rechteck.
    /// Common: schlichter weißer Rahmen, Rare: blauer Glow, Epic: violetter Puls, Legendary: goldener Shimmer.
    /// </summary>
    public static void DrawRarityBorder(SKCanvas canvas, SKRect rect, Rarity rarity, float time)
    {
        var color = rarity.GetColor();
        float borderWidth = rarity.GetBorderWidth();
        float glowRadius = rarity.GetGlowRadius();

        // Glow-Effekt (ab Rare aufwärts)
        if (glowRadius > 0)
        {
            DrawRarityGlow(canvas, rect, rarity, time);
        }

        // Haupt-Rahmen
        BorderPaint.Color = color;
        BorderPaint.StrokeWidth = borderWidth;
        BorderPaint.MaskFilter = null;
        canvas.DrawRoundRect(rect, 4f, 4f, BorderPaint);
    }

    /// <summary>
    /// Zeichnet einen leuchtenden Glow-Effekt um ein Rechteck.
    /// Pulsiert sanft für Epic und Legendary.
    /// </summary>
    public static void DrawRarityGlow(SKCanvas canvas, SKRect rect, Rarity rarity, float time)
    {
        float glowRadius = rarity.GetGlowRadius();
        if (glowRadius <= 0) return;

        var glowColor = rarity.GetGlowColor();

        // Puls-Effekt für Epic und Legendary
        float pulseFactor = 1f;
        if (rarity >= Rarity.Epic)
        {
            float pulseSpeed = rarity == Rarity.Legendary ? 3f : 2f;
            pulseFactor = 0.7f + 0.3f * MathF.Sin(time * pulseSpeed);
        }

        // Äußerer Glow
        byte glowAlpha = (byte)(60 * pulseFactor);
        GlowPaint.Color = glowColor.WithAlpha(glowAlpha);
        GlowPaint.StrokeWidth = glowRadius * 2;
        GlowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, glowRadius);

        var glowRect = new SKRect(
            rect.Left - glowRadius,
            rect.Top - glowRadius,
            rect.Right + glowRadius,
            rect.Bottom + glowRadius);

        canvas.DrawRoundRect(glowRect, 6f, 6f, GlowPaint);
        GlowPaint.MaskFilter = null;
    }

    /// <summary>
    /// Zeichnet einen wandernden Shimmer-Effekt auf dem Rahmen (nur Legendary).
    /// Ein heller Lichtpunkt gleitet entlang der Kante.
    /// </summary>
    public static void DrawRarityShimmer(SKCanvas canvas, SKRect rect, Rarity rarity, float time)
    {
        if (rarity != Rarity.Legendary) return;

        // Shimmer-Position entlang des Umfangs (0-1 normalisiert)
        float shimmerProgress = (time * 0.5f) % 1f;
        float perimeter = 2 * (rect.Width + rect.Height);
        float pos = shimmerProgress * perimeter;

        // Position auf dem Rechteck berechnen
        float sx, sy;
        if (pos < rect.Width)
        {
            // Obere Kante (links→rechts)
            sx = rect.Left + pos;
            sy = rect.Top;
        }
        else if (pos < rect.Width + rect.Height)
        {
            // Rechte Kante (oben→unten)
            sx = rect.Right;
            sy = rect.Top + (pos - rect.Width);
        }
        else if (pos < 2 * rect.Width + rect.Height)
        {
            // Untere Kante (rechts→links)
            sx = rect.Right - (pos - rect.Width - rect.Height);
            sy = rect.Bottom;
        }
        else
        {
            // Linke Kante (unten→oben)
            sx = rect.Left;
            sy = rect.Bottom - (pos - 2 * rect.Width - rect.Height);
        }

        // Shimmer-Punkt zeichnen
        ShimmerPaint.Color = SKColors.White.WithAlpha(180);
        ShimmerPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);
        canvas.DrawCircle(sx, sy, 3f, ShimmerPaint);

        // Innerer heller Kern
        ShimmerPaint.Color = SKColors.White.WithAlpha(255);
        ShimmerPaint.MaskFilter = null;
        canvas.DrawCircle(sx, sy, 1.5f, ShimmerPaint);
    }

    /// <summary>
    /// Zeichnet einen farbigen Hintergrund-Streifen mit Raritäts-Farbe (für Karten, Items).
    /// Dezenter Gradient von links nach rechts.
    /// </summary>
    public static void DrawRarityBackground(SKCanvas canvas, SKRect rect, Rarity rarity)
    {
        var color = rarity.GetColor();
        byte bgAlpha = rarity switch
        {
            Rarity.Common => 15,
            Rarity.Rare => 25,
            Rarity.Epic => 30,
            Rarity.Legendary => 35,
            _ => 15
        };

        FillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(rect.Left, rect.MidY),
            new SKPoint(rect.Right, rect.MidY),
            new[] { color.WithAlpha(bgAlpha), color.WithAlpha(0) },
            SKShaderTileMode.Clamp);

        canvas.DrawRoundRect(rect, 4f, 4f, FillPaint);
        FillPaint.Shader = null;
    }

    /// <summary>
    /// Zeichnet ein kleines Raritäts-Badge (farbiger Punkt mit Kürzel).
    /// Für kompakte Darstellung in Listen und HUD.
    /// </summary>
    public static void DrawRarityBadge(SKCanvas canvas, float x, float y, Rarity rarity, float size = 12f)
    {
        var color = rarity.GetColor();

        // Farbiger Punkt
        FillPaint.Color = color;
        FillPaint.Shader = null;
        canvas.DrawCircle(x, y, size / 2f, FillPaint);

        // Buchstabe (C/R/E/L)
        string letter = rarity switch
        {
            Rarity.Common => "C",
            Rarity.Rare => "R",
            Rarity.Epic => "E",
            Rarity.Legendary => "L",
            _ => "?"
        };

        using var font = new SKFont { Size = size * 0.65f };
        BorderPaint.Color = rarity == Rarity.Common ? SKColors.Black : SKColors.White;
        BorderPaint.Style = SKPaintStyle.Fill;
        BorderPaint.StrokeWidth = 0;

        var textBounds = new SKRect();
        font.MeasureText(letter, out textBounds);
        canvas.DrawText(letter, x - textBounds.MidX, y - textBounds.MidY, font, BorderPaint);

        // Paint zurücksetzen
        BorderPaint.Style = SKPaintStyle.Stroke;
    }

    /// <summary>
    /// Komplett-Rendering: Rahmen + Glow + Shimmer (kombiniert alle Effekte).
    /// </summary>
    public static void DrawComplete(SKCanvas canvas, SKRect rect, Rarity rarity, float time)
    {
        DrawRarityBackground(canvas, rect, rarity);
        DrawRarityBorder(canvas, rect, rarity, time);
        DrawRarityShimmer(canvas, rect, rarity, time);
    }
}
