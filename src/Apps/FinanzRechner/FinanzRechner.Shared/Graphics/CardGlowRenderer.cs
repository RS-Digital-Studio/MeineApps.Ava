using MeineApps.UI.SkiaSharp.Shaders;
using SkiaSharp;

namespace FinanzRechner.Graphics;

/// <summary>
/// SkiaSharp-Renderer für status-basierte Edge-Glow-Effekte auf Cards.
/// Nutzt SkiaGlowEffect GPU-Shader für performante, animierte Leuchtränder.
/// </summary>
public static class CardGlowRenderer
{
    // Budget-Status-Farben
    private static readonly SKColor BudgetGreenColor = SKColor.Parse("#22C55E");
    private static readonly SKColor BudgetYellowColor = SKColor.Parse("#F59E0B");
    private static readonly SKColor BudgetRedColor = SKColor.Parse("#EF4444");

    // Bilanz-Farben
    private static readonly SKColor BalancePositiveColor = SKColor.Parse("#22C55E");
    private static readonly SKColor BalanceNegativeColor = SKColor.Parse("#EF4444");

    // Flash-Farbe
    private static readonly SKColor FlashGoldColor = SKColor.Parse("#FFD700");

    /// <summary>
    /// Zeichnet einen status-abhängigen Edge-Glow basierend auf der Budget-Auslastung.
    /// Grün (unter 80%), Gelb (80-100%), Rot (über 100%) mit unterschiedlicher Puls-Geschwindigkeit.
    /// </summary>
    public static void RenderBudgetGlow(SKCanvas canvas, SKRect bounds, float time, float budgetPercent)
    {
        if (budgetPercent < 0.8f)
        {
            // Unter Budget: Grüner Glow, langsames Pulsing
            SkiaGlowEffect.DrawEdgeGlow(canvas, bounds, time,
                BudgetGreenColor.WithAlpha(120),
                glowRadius: 0.06f,
                pulseSpeed: 1.2f,
                pulseMin: 0.01f,
                pulseMax: 0.05f);
        }
        else if (budgetPercent <= 1.0f)
        {
            // Nahe am Budget: Gelber Glow, mittleres Pulsing
            SkiaGlowEffect.DrawEdgeGlow(canvas, bounds, time,
                BudgetYellowColor.WithAlpha(150),
                glowRadius: 0.07f,
                pulseSpeed: 2.0f,
                pulseMin: 0.02f,
                pulseMax: 0.07f);
        }
        else
        {
            // Budget überschritten: Roter Glow, schnelles Pulsing
            SkiaGlowEffect.DrawEdgeGlow(canvas, bounds, time,
                BudgetRedColor.WithAlpha(180),
                glowRadius: 0.08f,
                pulseSpeed: 3.0f,
                pulseMin: 0.02f,
                pulseMax: 0.10f);
        }
    }

    /// <summary>
    /// Zeichnet einen subtilen Edge-Glow für Bilanz-Anzeigen.
    /// Grün bei positivem Saldo, Rot bei negativem.
    /// </summary>
    public static void RenderBalanceGlow(SKCanvas canvas, SKRect bounds, float time, bool isPositive)
    {
        var color = isPositive
            ? BalancePositiveColor.WithAlpha(100)
            : BalanceNegativeColor.WithAlpha(100);

        // Dezenter, langsam pulsierender Glow mit kleinem Radius
        SkiaGlowEffect.DrawEdgeGlow(canvas, bounds, time, color,
            glowRadius: 0.04f,
            pulseSpeed: 1.0f,
            pulseMin: 0.01f,
            pulseMax: 0.04f);
    }

    /// <summary>
    /// Zeichnet einen kurzen Gold-Flash-Effekt nach einer Berechnung.
    /// flashProgress läuft von 0.0 (Start) bis 1.0 (Ende) über ca. 0.6 Sekunden.
    /// </summary>
    public static void RenderCalculationFlash(SKCanvas canvas, SKRect bounds, float time, float flashProgress)
    {
        if (flashProgress <= 0f || flashProgress >= 1f)
            return;

        // Intensität basierend auf Flash-Phase berechnen
        float intensity;
        if (flashProgress < 0.3f)
        {
            // Aufbau-Phase: 0→1 (quadratisch für sanften Einstieg)
            float t = flashProgress / 0.3f;
            intensity = t * t;
        }
        else if (flashProgress < 0.5f)
        {
            // Maximale Intensität
            intensity = 1.0f;
        }
        else
        {
            // Fade-Out: 1→0 (invers-quadratisch für sanftes Ausklingen)
            float t = (flashProgress - 0.5f) / 0.5f;
            intensity = 1.0f - (t * t);
        }

        // Dynamischer Glow-Radius basierend auf Intensität
        float dynamicRadius = 0.03f + (intensity * 0.09f);
        byte alpha = (byte)(200 * intensity);

        SkiaGlowEffect.DrawEdgeGlow(canvas, bounds, time,
            FlashGoldColor.WithAlpha(alpha),
            glowRadius: dynamicRadius,
            pulseSpeed: 0f,
            pulseMin: dynamicRadius,
            pulseMax: dynamicRadius);
    }
}
