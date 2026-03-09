namespace RebornSaga.Rendering.Effects;

using SkiaSharp;
using System;

/// <summary>
/// Horizontale Glitch-Verschiebung + RGB-Split Effekt.
/// Wird als Post-Processing-Layer über den Canvas gelegt.
/// Anwendung: ARIA System-Fehler, Boss-Encounter, Dimension-Wechsel.
/// </summary>
public class GlitchEffect
{
    private static readonly SKPaint _glitchPaint = new() { IsAntialias = false };
    private static readonly Random _random = new();

    private float _intensity;
    private float _duration;
    private float _elapsed;
    private bool _isActive;

    /// <summary>Ob der Effekt gerade aktiv ist.</summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Startet den Glitch-Effekt.
    /// </summary>
    /// <param name="intensity">Stärke 0..1 (0.3 = subtil, 1.0 = massiv).</param>
    /// <param name="duration">Dauer in Sekunden.</param>
    public void Start(float intensity, float duration)
    {
        // Guard: Ungültige Duration → Effekt nicht starten (verhindert Division by Zero in Render)
        if (duration <= 0f)
        {
            _isActive = false;
            return;
        }

        _intensity = Math.Clamp(intensity, 0f, 1f);
        _duration = duration;
        _elapsed = 0;
        _isActive = true;
    }

    /// <summary>
    /// Stoppt den Effekt sofort.
    /// </summary>
    public void Stop() => _isActive = false;

    /// <summary>
    /// Update pro Frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!_isActive) return;
        _elapsed += deltaTime;
        if (_elapsed >= _duration)
            _isActive = false;
    }

    /// <summary>
    /// Rendert den Glitch-Effekt über den Canvas.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        if (!_isActive) return;

        // Abklingende Intensität
        var t = _elapsed / _duration;
        var currentIntensity = _intensity * (1f - t * t); // Quadratisches Abklingen

        var maxShift = bounds.Width * 0.08f * currentIntensity;
        var lineCount = (int)(12 * currentIntensity + 1);

        for (int i = 0; i < lineCount; i++)
        {
            var y = bounds.Top + (float)_random.NextDouble() * bounds.Height;
            var h = 1f + (float)_random.NextDouble() * 8f * currentIntensity;
            var shift = ((float)_random.NextDouble() * 2f - 1f) * maxShift;

            // Roter Kanal verschoben
            _glitchPaint.Color = new SKColor(255, 0, 0, (byte)(50 * currentIntensity));
            canvas.DrawRect(bounds.Left + shift * 1.5f, y, bounds.Width, h, _glitchPaint);

            // Blauer Kanal gegenversetzt
            _glitchPaint.Color = new SKColor(0, 100, 255, (byte)(40 * currentIntensity));
            canvas.DrawRect(bounds.Left - shift, y + 1, bounds.Width, h * 0.7f, _glitchPaint);
        }

        // Weißes Flicker-Band (horizontaler Streifen)
        if (_random.NextDouble() < currentIntensity * 0.4)
        {
            var blockY = bounds.Top + (float)_random.NextDouble() * bounds.Height;
            var blockH = bounds.Height * (0.02f + (float)_random.NextDouble() * 0.05f);

            _glitchPaint.Color = SKColors.White.WithAlpha((byte)(30 * currentIntensity));
            canvas.DrawRect(bounds.Left, blockY, bounds.Width, blockH, _glitchPaint);
        }
    }
}
