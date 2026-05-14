using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Vollbild-Vignette-Flash fuer Game-Feel-Momente (.2 +.3 /#18).
///
/// Verwendet als:
/// - <b>UltraCombo-Flash</b>: Bei Combo ≥ x10, Welt-Akzent-Farbe, 200ms (Default-Trigger).
/// - <b>Damage-Flash</b>: Bei Player-Hit, rote Vignette, kuerzer (siehe TriggerWithDuration).
///
/// Mechanik:
/// - Default 200ms: Alpha 0→1 in 80ms (Attack), dann 1→0 in 120ms (Decay)
/// - RadialGradient mit transparentem Mittelpunkt + voll-farbigem Rand (Vignette-Bruellen)
/// - Frame-rate-unabhaengig (deltaTime-basiert)
/// - Kein Allocations pro Frame (SKShader gecacht solange Farbe gleich)
///
/// Inspiration: Vampire Survivors / Risk of Rain — der ikonische "Bildschirm-Bruellen"-Moment.
/// </summary>
public sealed class UltraComboFlash : IDisposable
{
    /// <summary>Default-Attack-Time (80ms).</summary>
    public const float DEFAULT_ATTACK_TIME = 0.08f;
    /// <summary>Default-Decay-Time (120ms).</summary>
    public const float DEFAULT_DECAY_TIME = 0.12f;

    private float _attackTime = DEFAULT_ATTACK_TIME;
    private float _decayTime = DEFAULT_DECAY_TIME;
    private float TotalDuration => _attackTime + _decayTime;

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
    /// Loest einen Vollbild-Flash mit Default-Dauer (200ms) aus.
    /// Aufruf z.B. bei Combo ≥ x10 (idempotent — Re-Trigger ueberschreibt laufenden Flash).
    /// </summary>
    /// <param name="color">Vignette-Farbe (typisch Welt-Akzent oder Combo-Farbe).</param>
    public void Trigger(SKColor color) => TriggerWithDuration(color, DEFAULT_ATTACK_TIME, DEFAULT_DECAY_TIME);

    /// <summary>
    /// Loest einen Vollbild-Flash mit individuellen Attack/Decay-Zeiten aus.
    /// Beispiel: Damage-Flash mit (color: Rot, attack: 0.05f, decay: 0.25f) = 50ms snap + 250ms fade.
    /// </summary>
    public void TriggerWithDuration(SKColor color, float attackSeconds, float decaySeconds)
    {
        _color = color;
        _attackTime = Math.Max(0.001f, attackSeconds);
        _decayTime = Math.Max(0.001f, decaySeconds);
        _timer = TotalDuration;
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

        // Alpha-Hüllkurve: Attack → Hoch, Decay → runter
        float elapsed = TotalDuration - _timer;
        float alpha;
        if (elapsed < _attackTime)
            alpha = elapsed / _attackTime;          // 0 → 1
        else
            alpha = 1f - (elapsed - _attackTime) / _decayTime; // 1 → 0
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
