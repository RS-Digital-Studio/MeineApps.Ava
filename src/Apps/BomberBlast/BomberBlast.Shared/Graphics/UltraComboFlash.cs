using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Vollbild-Vignette-Flash fuer ULTRA-Combos (Sprint 1.2 AAA-Audit #7).
/// Trigger: Bei Combo ≥ x10 (genau einmal beim Erreichen, nicht pro Kill).
///
/// Mechanik:
/// - 200ms Animation: Alpha 0→1 in 80ms (Attack), dann 1→0 in 120ms (Decay)
/// - RadialGradient mit transparentem Mittelpunkt + voll-farbigem Rand (Vignette-Bruellen)
/// - Welt-Theme-Farbe (in Schattenwelt violett, in Vulkan orange)
/// - Frame-rate-unabhaengig (deltaTime-basiert)
/// - Kein Allocations pro Frame (SKShader gecacht solange Farbe gleich)
///
/// Inspiration: Vampire Survivors / Risk of Rain — der ikonische "Bildschirm-Bruellen"-Moment.
/// </summary>
public sealed class UltraComboFlash : IDisposable
{
    private const float ATTACK_TIME = 0.08f;   // 80ms hoch
    private const float DECAY_TIME = 0.12f;    // 120ms runter
    private const float TOTAL_DURATION = ATTACK_TIME + DECAY_TIME;

    private float _timer;             // verbleibende Zeit
    private SKColor _color = SKColors.White;
    private SKShader? _cachedShader;
    private SKColor _cachedShaderColor;
    private float _cachedShaderRadius = -1f;
    private float _cachedShaderCenterX = -1f;
    private float _cachedShaderCenterY = -1f;

    private readonly SKPaint _paint = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = false  // Vollbild-Rect — kein Antialias noetig
    };

    private bool _disposed;

    /// <summary>Ob aktuell ein Flash gerendert werden muss.</summary>
    public bool IsActive => _timer > 0;

    /// <summary>
    /// Loest einen Vollbild-Flash aus. Aufruf bei Combo ≥ x10 (idempotent — Re-Trigger
    /// ueberschreibt den laufenden Flash).
    /// </summary>
    /// <param name="color">Vignette-Farbe (typisch Welt-Akzent oder Combo-Farbe).</param>
    public void Trigger(SKColor color)
    {
        _color = color;
        _timer = TOTAL_DURATION;
    }

    /// <summary>Pro Frame: Timer dekrementieren.</summary>
    public void Update(float deltaTime)
    {
        if (_timer > 0)
        {
            _timer -= deltaTime;
            if (_timer < 0) _timer = 0;
        }
    }

    /// <summary>
    /// Rendert den Vignette-Flash ueber den ganzen Bildschirm.
    /// Aufruf nach Spielfeld + HUD, vor Subtitles.
    /// </summary>
    public void Render(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        if (_timer <= 0 || screenWidth <= 0 || screenHeight <= 0) return;

        // Alpha-Hüllkurve: Attack 80ms → Hoch, Decay 120ms → runter
        float elapsed = TOTAL_DURATION - _timer;
        float alpha;
        if (elapsed < ATTACK_TIME)
            alpha = elapsed / ATTACK_TIME;          // 0 → 1
        else
            alpha = 1f - (elapsed - ATTACK_TIME) / DECAY_TIME; // 1 → 0
        alpha = Math.Clamp(alpha, 0f, 1f);

        byte effectiveAlpha = (byte)(alpha * 220);  // Cap bei 220 — kein totales Whiteout

        float cx = screenWidth * 0.5f;
        float cy = screenHeight * 0.5f;
        // Radius bis zur Diagonale — sicher dass die Vignette an die Ecken reicht
        float maxR = MathF.Sqrt(cx * cx + cy * cy);

        // Shader-Cache: nur neu bauen wenn Farbe ODER Geometrie sich aendert
        if (_cachedShader == null || _cachedShaderColor != _color
            || Math.Abs(_cachedShaderRadius - maxR) > 0.5f
            || Math.Abs(_cachedShaderCenterX - cx) > 0.5f
            || Math.Abs(_cachedShaderCenterY - cy) > 0.5f)
        {
            _cachedShader?.Dispose();
            // Vignette: Mitte transparent (40% des Radius), dann linearer Aufbau zur farbigen Ecke
            _cachedShader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy),
                maxR,
                new[]
                {
                    _color.WithAlpha(0),
                    _color.WithAlpha(0),
                    _color.WithAlpha(180),
                    _color
                },
                new[] { 0f, 0.40f, 0.78f, 1f },
                SKShaderTileMode.Clamp);
            _cachedShaderColor = _color;
            _cachedShaderRadius = maxR;
            _cachedShaderCenterX = cx;
            _cachedShaderCenterY = cy;
        }

        _paint.Shader = _cachedShader;
        _paint.Color = SKColors.White.WithAlpha(effectiveAlpha);
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _paint);
        _paint.Shader = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _cachedShader?.Dispose();
        _cachedShader = null;
        _paint.Dispose();
        _disposed = true;
    }
}
