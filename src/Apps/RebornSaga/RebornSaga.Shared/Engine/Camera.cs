namespace RebornSaga.Engine;

using SkiaSharp;
using System;

/// <summary>
/// Viewport-Kamera mit Pan, Zoom und Screen-Shake-Effekt.
/// Wird von Szenen verwendet um den sichtbaren Bereich zu steuern.
/// </summary>
public class Camera
{
    /// <summary>
    /// X-Position der Kamera (Weltkoordinaten).
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// Y-Position der Kamera (Weltkoordinaten).
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    /// Zoom-Faktor (1.0 = Normalansicht, >1 = herangezoomt).
    /// </summary>
    public float Zoom { get; set; } = 1f;

    // Screen-Shake-State
    private float _shakeIntensity;
    private float _shakeDuration;
    private float _shakeTimer;
    private readonly Random _random = new();

    /// <summary>
    /// Aktueller horizontaler Shake-Offset (für manuelles Anwenden).
    /// </summary>
    public float ShakeOffsetX { get; private set; }

    /// <summary>
    /// Aktueller vertikaler Shake-Offset (für manuelles Anwenden).
    /// </summary>
    public float ShakeOffsetY { get; private set; }

    /// <summary>
    /// Startet einen Screen-Shake-Effekt (z.B. bei Treffern im Kampf).
    /// </summary>
    /// <param name="intensity">Stärke des Shake in Pixel.</param>
    /// <param name="duration">Dauer in Sekunden.</param>
    public void Shake(float intensity, float duration)
    {
        _shakeIntensity = intensity;
        _shakeDuration = duration;
        _shakeTimer = duration;
    }

    /// <summary>
    /// Kamera pro Frame aktualisieren (Shake-Timer herunterzählen).
    /// </summary>
    public void Update(float deltaTime)
    {
        if (_shakeTimer > 0)
        {
            // Guard: Division durch Null verhindern falls _shakeDuration ungültig
            if (_shakeDuration <= 0)
            {
                _shakeTimer = 0;
                ShakeOffsetX = 0;
                ShakeOffsetY = 0;
                return;
            }

            _shakeTimer -= deltaTime;
            // Intensität nimmt linear ab (Ausfaden)
            var factor = _shakeTimer / _shakeDuration;
            ShakeOffsetX = (float)(_random.NextDouble() * 2 - 1) * _shakeIntensity * factor;
            ShakeOffsetY = (float)(_random.NextDouble() * 2 - 1) * _shakeIntensity * factor;
        }
        else
        {
            ShakeOffsetX = 0;
            ShakeOffsetY = 0;
        }
    }

    /// <summary>
    /// Wendet die Kamera-Transformation auf den Canvas an.
    /// Zentriert die Kamera-Position im sichtbaren Bereich, wendet Zoom und Shake an.
    /// Muss mit canvas.Save()/Restore() umschlossen werden.
    /// </summary>
    public void ApplyTransform(SKCanvas canvas, SKRect bounds)
    {
        canvas.Translate(bounds.MidX + ShakeOffsetX, bounds.MidY + ShakeOffsetY);
        canvas.Scale(Zoom);
        canvas.Translate(-X, -Y);
    }
}
