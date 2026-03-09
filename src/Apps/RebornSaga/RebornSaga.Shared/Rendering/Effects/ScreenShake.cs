namespace RebornSaga.Rendering.Effects;

using SkiaSharp;
using System;

/// <summary>
/// Screen-Shake Effekt: Canvas-Translation mit abklingender Amplitude.
/// Anwendung: Kritischer Treffer, Explosion, Boss-Stampfen.
/// </summary>
public class ScreenShake
{
    private static readonly Random _random = new();

    private float _intensity;
    private float _duration;
    private float _elapsed;
    private bool _isActive;

    /// <summary>Aktuelle X-Verschiebung (wird vom Renderer angewandt).</summary>
    public float OffsetX { get; private set; }

    /// <summary>Aktuelle Y-Verschiebung.</summary>
    public float OffsetY { get; private set; }

    /// <summary>Ob der Shake aktiv ist.</summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Startet einen Screen-Shake.
    /// </summary>
    /// <param name="intensity">Pixel-Amplitude (3 = leicht, 5 = mittel, 10 = stark).</param>
    /// <param name="duration">Dauer in Sekunden.</param>
    public void Start(float intensity, float duration)
    {
        // Guard: Ungültige Duration → Effekt nicht starten (verhindert Division by Zero in Update)
        if (duration <= 0f)
        {
            _isActive = false;
            OffsetX = 0;
            OffsetY = 0;
            return;
        }

        _intensity = intensity;
        _duration = duration;
        _elapsed = 0;
        _isActive = true;
    }

    /// <summary>
    /// Update pro Frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!_isActive)
        {
            OffsetX = 0;
            OffsetY = 0;
            return;
        }

        _elapsed += deltaTime;
        if (_elapsed >= _duration)
        {
            _isActive = false;
            OffsetX = 0;
            OffsetY = 0;
            return;
        }

        // Abklingende Intensität
        var t = _elapsed / _duration;
        var currentIntensity = _intensity * (1f - t);

        // Zufällige Verschiebung
        OffsetX = ((float)_random.NextDouble() * 2f - 1f) * currentIntensity;
        OffsetY = ((float)_random.NextDouble() * 2f - 1f) * currentIntensity;
    }

    /// <summary>
    /// Wendet den Shake auf den Canvas an (Translate). Muss am Frame-Anfang aufgerufen werden.
    /// Gibt true zurück wenn aktiv.
    /// </summary>
    public bool Apply(SKCanvas canvas)
    {
        if (!_isActive) return false;
        canvas.Translate(OffsetX, OffsetY);
        return true;
    }

    /// <summary>Leichter Shake (Treffer, kleine Explosion).</summary>
    public void Light(float duration = 0.15f) => Start(3f, duration);

    /// <summary>Mittlerer Shake (kritischer Treffer).</summary>
    public void Medium(float duration = 0.25f) => Start(5f, duration);

    /// <summary>Starker Shake (Boss-Attacke, Ultimate).</summary>
    public void Heavy(float duration = 0.4f) => Start(10f, duration);
}
