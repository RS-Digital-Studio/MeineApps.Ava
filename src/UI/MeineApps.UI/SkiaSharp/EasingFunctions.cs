using System;

namespace MeineApps.UI.SkiaSharp;

/// <summary>
/// Mathematische Easing-Funktionen für alle Animationen im Spiel.
/// Alle Funktionen nehmen t (0.0 bis 1.0) und geben den transformierten Wert zurück.
/// </summary>
public static class EasingFunctions
{
    /// <summary>
    /// Standard UI-Animation: Schneller Start, sanftes Ende.
    /// 1 - (1-t)^3
    /// </summary>
    public static float EaseOutCubic(float t)
    {
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }

    /// <summary>
    /// Leichtes Überschießen am Ende (Punch-Effekt).
    /// Gut für Buttons und UI-Elemente die "einschnappen".
    /// </summary>
    public static float EaseOutBack(float t, float overshoot = 1.70158f)
    {
        float t1 = t - 1f;
        return t1 * t1 * ((overshoot + 1f) * t1 + overshoot) + 1f;
    }

    /// <summary>
    /// Federndes Schwingen am Ende (Gummiband-Effekt).
    /// Gut für NumberPop, Stempel-Effekte.
    /// </summary>
    public static float EaseOutElastic(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;

        const float c4 = (2f * MathF.PI) / 3f;
        return MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * c4) + 1f;
    }

    /// <summary>
    /// Aufprall-Hüpfen am Ende.
    /// Gut für Stempel, fallende Objekte.
    /// </summary>
    public static float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1)
            return n1 * t * t;
        if (t < 2f / d1)
        {
            t -= 1.5f / d1;
            return n1 * t * t + 0.75f;
        }
        if (t < 2.5f / d1)
        {
            t -= 2.25f / d1;
            return n1 * t * t + 0.9375f;
        }

        t -= 2.625f / d1;
        return n1 * t * t + 0.984375f;
    }

    /// <summary>
    /// Natürliche gedämpfte Schwingung (Feder-Physik).
    /// damping: 0.3-0.8 (niedrig = mehr Schwingung)
    /// frequency: 3-8 (Schwingungen pro Durchlauf)
    /// </summary>
    public static float Spring(float t, float damping = 0.5f, float frequency = 5f)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;

        float decay = MathF.Exp(-damping * t * 10f);
        return 1f - decay * MathF.Cos(frequency * t * MathF.PI * 2f);
    }

    /// <summary>
    /// Sanfter symmetrischer Übergang (langsam→schnell→langsam).
    /// Gut für Screen-Transitions, Crossfades.
    /// </summary>
    public static float EaseInOutQuint(float t)
    {
        if (t < 0.5f)
            return 16f * t * t * t * t * t;

        float p = 2f * t - 2f;
        return 0.5f * p * p * p * p * p + 1f;
    }

    /// <summary>
    /// Einfache lineare Interpolation zwischen zwei Werten.
    /// </summary>
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>
    /// Clamped Lerp (t wird auf 0-1 begrenzt).
    /// </summary>
    public static float LerpClamped(float a, float b, float t) => Lerp(a, b, Math.Clamp(t, 0f, 1f));

    /// <summary>
    /// Smooth-Step (Hermite-Interpolation): Sanfter als Lerp.
    /// </summary>
    public static float SmoothStep(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Beschleunigende Kurve (langsam→schnell). Quadratisch.
    /// </summary>
    public static float EaseInQuad(float t) => t * t;

    /// <summary>
    /// Verlangsamende Kurve (schnell→langsam). Quadratisch.
    /// </summary>
    public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    /// <summary>
    /// Ping-Pong: 0→1→0 über t von 0 bis 1.
    /// Nützlich für Pulse-Animationen.
    /// </summary>
    public static float PingPong(float t) => 1f - MathF.Abs(2f * Math.Clamp(t, 0f, 1f) - 1f);
}
