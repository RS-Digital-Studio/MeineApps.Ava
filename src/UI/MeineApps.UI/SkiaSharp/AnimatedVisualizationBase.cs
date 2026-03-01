using System;

namespace MeineApps.UI.SkiaSharp;

/// <summary>
/// Basis für SkiaSharp-Renderer die eine Einschwing-Animation beim ersten Render unterstützen.
/// Verwendung: Statische Instanz pro Visualization, StartAnimation() bei Datenwechsel aufrufen,
/// AnimationProgress in Render() verwenden.
/// </summary>
public class AnimatedVisualizationBase
{
    private float _animationProgress;
    private bool _isAnimating;
    private DateTime _animationStart;

    /// <summary>Animationsdauer in Millisekunden (Default: 600ms).</summary>
    public float AnimationDurationMs { get; set; } = 600f;

    /// <summary>Easing-Funktion (Default: EaseOutCubic).</summary>
    public Func<float, float> EasingFunction { get; set; } = EasingFunctions.EaseOutCubic;

    /// <summary>Aktueller Animationsfortschritt (0.0-1.0, bereits mit Easing).</summary>
    public float AnimationProgress => _animationProgress;

    /// <summary>True wenn Animation noch läuft.</summary>
    public bool IsAnimating => _isAnimating;

    /// <summary>Startet die Animation von 0. Mehrfach-Aufruf startet neu.</summary>
    public void StartAnimation()
    {
        _animationStart = DateTime.UtcNow;
        _isAnimating = true;
        _animationProgress = 0f;
    }

    /// <summary>
    /// Aktualisiert den Animationsfortschritt. Vor jedem Render() aufrufen.
    /// Gibt true zurück solange die Animation läuft (für InvalidateSurface-Loop).
    /// </summary>
    public bool UpdateAnimation()
    {
        if (!_isAnimating)
        {
            _animationProgress = 1f;
            return false;
        }

        var elapsed = (float)(DateTime.UtcNow - _animationStart).TotalMilliseconds;
        var rawProgress = Math.Clamp(elapsed / AnimationDurationMs, 0f, 1f);

        _animationProgress = EasingFunction(rawProgress);

        if (rawProgress >= 1f)
        {
            _isAnimating = false;
            _animationProgress = 1f;
            return false;
        }

        return true;
    }

    /// <summary>Setzt Animation sofort auf Ende (kein Einschwingen).</summary>
    public void SkipAnimation()
    {
        _isAnimating = false;
        _animationProgress = 1f;
    }
}
