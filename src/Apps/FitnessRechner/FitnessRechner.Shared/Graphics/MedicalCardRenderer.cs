using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Statische Utility-Klasse für den holografischen Medical-Card-Hintergrund.
/// Wird von Dashboard-Cards und anderen Views verwendet.
/// Kein State nötig - alle Methoden sind statisch.
/// </summary>
public static class MedicalCardRenderer
{
    // Konstanten für HUD-Bracketing
    private const float BracketArmLength = 10f;
    private const float BracketOffset = 3f;

    // Konstanten für Feature-Akzent
    private const float AccentWidth = 2f;
    private const float AccentHeightRatio = 0.6f;
    private const float AccentCornerRadius = 1f;

    /// <summary>
    /// Zeichnet den holografischen Medical-Card-Hintergrund mit 4 Schichten:
    /// 1. Halbtransparenter Surface-Hintergrund (RoundRect)
    /// 2. Obere Kante mit Cyan-Gradient
    /// 3. HUD-Bracketing (L-förmige Ecken)
    /// 4. Optionaler Feature-Akzent (vertikaler Strich links)
    /// </summary>
    public static void RenderCardBackground(SKCanvas canvas, SKRect bounds,
        SKColor? accentColor = null, float cornerRadius = 12f)
    {
        RenderSurfaceBackground(canvas, bounds, cornerRadius);
        RenderTopEdge(canvas, bounds);
        RenderHudBrackets(canvas, bounds);

        if (accentColor.HasValue)
            RenderFeatureAccent(canvas, bounds, accentColor.Value);
    }

    // =====================================================================
    // Schicht 1: Halbtransparenter Surface-Hintergrund
    // =====================================================================

    /// <summary>
    /// Zeichnet das gefüllte RoundRect mit Surface-Farbe bei 85% Alpha.
    /// </summary>
    private static void RenderSurfaceBackground(SKCanvas canvas, SKRect bounds, float cornerRadius)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = MedicalColors.Surface.WithAlpha(217) // 85% Alpha
        };

        canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, paint);
    }

    // =====================================================================
    // Schicht 2: Obere Kante (horizontaler Cyan-Gradient, 1px)
    // =====================================================================

    /// <summary>
    /// Zeichnet eine subtile Cyan-Linie an der oberen Kante.
    /// Gradient: Links Cyan(100) → Mitte Transparent → Rechts Cyan(100).
    /// </summary>
    private static void RenderTopEdge(SKCanvas canvas, SKRect bounds)
    {
        var cyanEdge = MedicalColors.Cyan.WithAlpha(100);
        var transparent = SKColors.Transparent;

        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.Left, bounds.Top),
            new SKPoint(bounds.Right, bounds.Top),
            new[] { cyanEdge, transparent, cyanEdge },
            new[] { 0f, 0.5f, 1f },
            SKShaderTileMode.Clamp);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Shader = shader
        };

        // Obere Linie von links nach rechts
        float y = bounds.Top;
        canvas.DrawLine(bounds.Left, y, bounds.Right, y, paint);
    }

    // =====================================================================
    // Schicht 3: HUD-Bracketing (4 L-förmige Ecken)
    // =====================================================================

    /// <summary>
    /// Zeichnet in jeder Ecke eine L-förmige Linie (10px Armlänge, 3px Offset).
    /// Erzeugt den typischen Sci-Fi / HUD-Rahmeneffekt.
    /// </summary>
    private static void RenderHudBrackets(SKCanvas canvas, SKRect bounds)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = MedicalColors.Cyan.WithAlpha(77) // 30% Alpha
        };

        float left = bounds.Left + BracketOffset;
        float top = bounds.Top + BracketOffset;
        float right = bounds.Right - BracketOffset;
        float bottom = bounds.Bottom - BracketOffset;

        // Oben-links: → rechts + ↓ unten
        canvas.DrawLine(left, top, left + BracketArmLength, top, paint);
        canvas.DrawLine(left, top, left, top + BracketArmLength, paint);

        // Oben-rechts: ← links + ↓ unten
        canvas.DrawLine(right, top, right - BracketArmLength, top, paint);
        canvas.DrawLine(right, top, right, top + BracketArmLength, paint);

        // Unten-links: → rechts + ↑ oben
        canvas.DrawLine(left, bottom, left + BracketArmLength, bottom, paint);
        canvas.DrawLine(left, bottom, left, bottom - BracketArmLength, paint);

        // Unten-rechts: ← links + ↑ oben
        canvas.DrawLine(right, bottom, right - BracketArmLength, bottom, paint);
        canvas.DrawLine(right, bottom, right, bottom - BracketArmLength, paint);
    }

    // =====================================================================
    // Schicht 4: Feature-Akzent (optionaler vertikaler Strich links)
    // =====================================================================

    /// <summary>
    /// Zeichnet einen vertikalen Akzent-Strich am linken Rand.
    /// 2px breit, 60% der Höhe, vertikal zentriert, 60% Alpha.
    /// </summary>
    private static void RenderFeatureAccent(SKCanvas canvas, SKRect bounds, SKColor accentColor)
    {
        float accentHeight = bounds.Height * AccentHeightRatio;
        float accentTop = bounds.MidY - accentHeight * 0.5f;

        var accentRect = new SKRect(
            bounds.Left,
            accentTop,
            bounds.Left + AccentWidth,
            accentTop + accentHeight);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = accentColor.WithAlpha(153) // 60% Alpha
        };

        canvas.DrawRoundRect(accentRect, AccentCornerRadius, AccentCornerRadius, paint);
    }
}
