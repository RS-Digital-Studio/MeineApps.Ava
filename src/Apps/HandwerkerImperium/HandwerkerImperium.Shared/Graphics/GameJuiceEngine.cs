using System;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Zentrale Effekt-Engine für alle visuellen Game-Juice-Effekte.
/// Singleton via DI, rendert Partikel, ScreenShake, Overlays etc.
/// Struct-basierter Pool für GC-freie Performance.
/// </summary>
public class GameJuiceEngine : IDisposable
{
    private bool _disposed;

    // Effekt-Pool (struct-basiert, kein GC)
    private const int MaxEffects = 200;
    private readonly JuiceEffect[] _effects = new JuiceEffect[MaxEffects];
    private int _effectCount;
    private readonly object _lock = new();

    // ScreenShake-State
    private float _shakeIntensity;
    private float _shakeDuration;
    private float _shakeTimer;
    private float _shakeOffsetX;
    private float _shakeOffsetY;

    // Flash-Overlay
    private SKColor _flashColor;
    private float _flashDuration;
    private float _flashTimer;

    // Vignette
    private float _vignetteIntensity;
    private float _vignetteTarget;
    private const float VignetteLerpSpeed = 4f;

    // Gecachte Paints
    private readonly SKPaint _particlePaint = new() { IsAntialias = true };
    private readonly SKPaint _glowPaint = new() { IsAntialias = true };
    private readonly SKPaint _textPaint = new() { IsAntialias = true };
    private readonly SKPaint _overlayPaint = new() { IsAntialias = true };
    private readonly SKPaint _vignettePaint = new() { IsAntialias = true };
    private readonly SKPaint _ringPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    // Gecachte Font- und Path-Objekte (vermeidet Allokationen pro Frame)
    private readonly SKFont _font = new() { Edging = SKFontEdging.Antialias };
    private readonly SKPath _sparklePath = new();

    // Pseudo-Random für deterministische Effekte
    private uint _rngState = 42;

    /// <summary>
    /// Aktueller ScreenShake-Offset X (Canvas vor dem Rendern verschieben).
    /// </summary>
    public float ShakeOffsetX => _shakeOffsetX;

    /// <summary>
    /// Aktueller ScreenShake-Offset Y.
    /// </summary>
    public float ShakeOffsetY => _shakeOffsetY;

    /// <summary>
    /// Bildschirm-Schütteln auslösen (z.B. bei teurem Kauf, Explosion).
    /// </summary>
    /// <param name="intensity">Stärke in dp (2-8 empfohlen).</param>
    /// <param name="duration">Dauer in Sekunden (0.2-0.5 empfohlen).</param>
    public void ScreenShake(float intensity = 4f, float duration = 0.3f)
    {
        lock (_lock)
        {
            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeTimer = duration;
        }
    }

    /// <summary>
    /// Expandierender Ring bei Tap (z.B. Upgrade-Button).
    /// </summary>
    public void RadialBurst(float x, float y, SKColor color, float maxRadius = 60f, float duration = 0.4f)
    {
        lock (_lock)
        {
            AddEffect(new JuiceEffect
            {
                Type = EffectType.RadialBurst,
                X = x, Y = y,
                Color = color,
                MaxRadius = maxRadius,
                Duration = duration,
                Timer = duration
            });
        }
    }

    /// <summary>
    /// Münzen fliegen von Quelle zum Ziel-HUD.
    /// </summary>
    public void CoinsFlyToWallet(float fromX, float fromY, float toX, float toY, int count = 10)
    {
        lock (_lock)
        {
            count = Math.Clamp(count, 4, 16);
            for (int i = 0; i < count; i++)
            {
                float delay = i * 0.04f; // 40ms Stagger
                float ctrlX = EasingFunctions.Lerp(fromX, toX, 0.5f) + NextRandom(-40f, 40f);
                float ctrlY = Math.Min(fromY, toY) - 30f - NextRandom(0f, 50f);

                AddEffect(new JuiceEffect
                {
                    Type = EffectType.CoinFly,
                    X = fromX, Y = fromY,
                    TargetX = toX, TargetY = toY,
                    ControlX = ctrlX, ControlY = ctrlY,
                    Duration = 0.5f + delay,
                    Timer = 0.5f + delay,
                    Delay = delay,
                    Size = 8f
                });
            }
        }
    }

    /// <summary>
    /// Glitzer-Effekt auf einer Fläche.
    /// </summary>
    public void SparkleEffect(SKRect rect, SKColor color, int count = 8, float duration = 0.6f)
    {
        lock (_lock)
        {
            for (int i = 0; i < Math.Min(count, 20); i++)
            {
                AddEffect(new JuiceEffect
                {
                    Type = EffectType.Sparkle,
                    X = rect.Left + NextRandom(0f, rect.Width),
                    Y = rect.Top + NextRandom(0f, rect.Height),
                    Color = color,
                    Duration = duration,
                    Timer = duration,
                    Size = NextRandom(2f, 5f),
                    Phase = NextRandom(0f, MathF.PI * 2f)
                });
            }
        }
    }

    /// <summary>
    /// Zahl springt raus mit Physik (z.B. "+500 Euro").
    /// </summary>
    public void NumberPop(float x, float y, string text, SKColor color, float fontSize = 18f)
    {
        lock (_lock)
        {
            AddEffect(new JuiceEffect
            {
                Type = EffectType.NumberPop,
                X = x, Y = y,
                Color = color,
                Duration = 1.0f,
                Timer = 1.0f,
                Size = fontSize,
                VelocityY = -120f, // Nach oben
                Text = text
            });
        }
    }

    /// <summary>
    /// Kurzer Fullscreen-Flash (z.B. bei Level-Up).
    /// </summary>
    public void FlashOverlay(SKColor color, float duration = 0.15f)
    {
        lock (_lock)
        {
            _flashColor = color;
            _flashDuration = duration;
            _flashTimer = duration;
        }
    }

    /// <summary>
    /// Vignette-Intensität setzen (0 = aus, 0.3-0.5 = subtil, 0.8 = stark).
    /// Wird sanft interpoliert.
    /// </summary>
    public void SetVignette(float intensity)
    {
        lock (_lock)
        {
            _vignetteTarget = Math.Clamp(intensity, 0f, 1f);
        }
    }

    /// <summary>
    /// Expandierende Schockwelle (z.B. bei Prestige).
    /// </summary>
    public void ShockwaveRing(float x, float y, SKColor color, float maxRadius = 100f, float duration = 0.5f)
    {
        lock (_lock)
        {
            AddEffect(new JuiceEffect
            {
                Type = EffectType.Shockwave,
                X = x, Y = y,
                Color = color,
                MaxRadius = maxRadius,
                Duration = duration,
                Timer = duration
            });
        }
    }

    /// <summary>
    /// Konfetti-Burst (z.B. bei Achievement).
    /// </summary>
    public void ConfettiBurst(float x, float y, int count = 30, float spread = 100f)
    {
        lock (_lock)
        {
            for (int i = 0; i < Math.Min(count, 50); i++)
            {
                float angle = NextRandom(0f, MathF.PI * 2f);
                float speed = NextRandom(80f, 200f);
                var color = ConfettiColors[((int)NextRandom(0, ConfettiColors.Length - 0.01f))];

                AddEffect(new JuiceEffect
                {
                    Type = EffectType.Confetti,
                    X = x, Y = y,
                    VelocityX = MathF.Cos(angle) * speed,
                    VelocityY = MathF.Sin(angle) * speed - 100f, // Bias nach oben
                    Color = color,
                    Duration = 1.5f,
                    Timer = 1.5f,
                    Size = NextRandom(4f, 8f),
                    Phase = NextRandom(0f, MathF.PI) // Rotations-Phase
                });
            }
        }
    }

    /// <summary>
    /// Alle aktiven Effekte updaten. Pro Frame aufrufen.
    /// </summary>
    public void Update(float deltaTime)
    {
        lock (_lock)
        {
            // ScreenShake
            if (_shakeTimer > 0)
            {
                _shakeTimer -= deltaTime;
                float decay = _shakeTimer / _shakeDuration;
                _shakeOffsetX = MathF.Sin(_shakeTimer * 50f) * _shakeIntensity * decay;
                _shakeOffsetY = MathF.Cos(_shakeTimer * 37f) * _shakeIntensity * decay * 0.7f;
            }
            else
            {
                _shakeOffsetX = 0f;
                _shakeOffsetY = 0f;
            }

            // Flash
            if (_flashTimer > 0)
                _flashTimer -= deltaTime;

            // Vignette (sanft interpolieren)
            _vignetteIntensity += (_vignetteTarget - _vignetteIntensity) * Math.Min(1f, VignetteLerpSpeed * deltaTime);

            // Effekte updaten (rückwärts iterieren für Swap-Remove)
            for (int i = _effectCount - 1; i >= 0; i--)
            {
                ref var e = ref _effects[i];
                e.Timer -= deltaTime;

                if (e.Timer <= 0)
                {
                    // Effekt entfernen (Swap mit letztem)
                    _effects[i] = _effects[_effectCount - 1];
                    _effectCount--;
                    continue;
                }

                // Physik für Confetti
                if (e.Type == EffectType.Confetti)
                {
                    e.X += e.VelocityX * deltaTime;
                    e.Y += e.VelocityY * deltaTime;
                    e.VelocityY += 300f * deltaTime; // Schwerkraft
                    e.VelocityX *= 0.98f; // Luftwiderstand
                    e.Phase += deltaTime * 8f; // Rotation
                }

                // Physik für NumberPop
                if (e.Type == EffectType.NumberPop)
                {
                    e.Y += e.VelocityY * deltaTime;
                    e.VelocityY += 80f * deltaTime; // Leichte Schwerkraft
                }
            }
        }
    }

    /// <summary>
    /// Alle aktiven Effekte auf das Canvas zeichnen. Nach dem Content rendern.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        lock (_lock)
        {
            for (int i = 0; i < _effectCount; i++)
            {
                ref var e = ref _effects[i];
                float progress = 1f - (e.Timer / e.Duration);

                // Delay-Phase: Noch nicht rendern
                if (e.Delay > 0 && e.Timer > e.Duration - e.Delay)
                    continue;

                switch (e.Type)
                {
                    case EffectType.RadialBurst:
                        RenderRadialBurst(canvas, ref e, progress);
                        break;
                    case EffectType.CoinFly:
                        RenderCoinFly(canvas, ref e, progress);
                        break;
                    case EffectType.Sparkle:
                        RenderSparkle(canvas, ref e, progress);
                        break;
                    case EffectType.NumberPop:
                        RenderNumberPop(canvas, ref e, progress);
                        break;
                    case EffectType.Shockwave:
                        RenderShockwave(canvas, ref e, progress);
                        break;
                    case EffectType.Confetti:
                        RenderConfetti(canvas, ref e, progress);
                        break;
                }
            }

            // Flash-Overlay (über allem)
            if (_flashTimer > 0)
            {
                float alpha = (_flashTimer / _flashDuration) * 0.6f;
                _overlayPaint.Color = _flashColor.WithAlpha((byte)(alpha * 255));
                canvas.DrawRect(bounds, _overlayPaint);
            }

            // Vignette-Overlay (über allem)
            if (_vignetteIntensity > 0.01f)
            {
                RenderVignette(canvas, bounds);
            }
        }
    }

    /// <summary>
    /// Gibt true zurück wenn gerade Effekte aktiv sind.
    /// </summary>
    public bool HasActiveEffects
    {
        get
        {
            lock (_lock)
            {
                return _effectCount > 0 || _shakeTimer > 0 || _flashTimer > 0;
            }
        }
    }

    /// <summary>
    /// Alle Effekte sofort beenden.
    /// </summary>
    public void ClearAll()
    {
        lock (_lock)
        {
            _effectCount = 0;
            _shakeTimer = 0;
            _flashTimer = 0;
            _shakeOffsetX = 0;
            _shakeOffsetY = 0;
        }
    }

    // --- Private Render-Methoden ---

    private void RenderRadialBurst(SKCanvas canvas, ref JuiceEffect e, float progress)
    {
        float easedProgress = EasingFunctions.EaseOutCubic(progress);
        float radius = e.MaxRadius * easedProgress;
        float alpha = (1f - progress) * 0.8f;

        _ringPaint.Color = e.Color.WithAlpha((byte)(alpha * 255));
        _ringPaint.StrokeWidth = Math.Max(1f, 4f * (1f - progress));

        canvas.DrawCircle(e.X, e.Y, radius, _ringPaint);

        // Innerer gefüllter Kreis (schnell verschwindend)
        if (progress < 0.3f)
        {
            float innerAlpha = (1f - progress / 0.3f) * 0.3f;
            _particlePaint.Color = e.Color.WithAlpha((byte)(innerAlpha * 255));
            canvas.DrawCircle(e.X, e.Y, radius * 0.4f, _particlePaint);
        }
    }

    private void RenderCoinFly(SKCanvas canvas, ref JuiceEffect e, float progress)
    {
        float adjustedProgress = Math.Clamp((progress * e.Duration - e.Delay) / (e.Duration - e.Delay), 0f, 1f);
        float easedT = EasingFunctions.EaseInOutQuint(adjustedProgress);

        // Quadratische Bezier-Kurve
        float oneMinusT = 1f - easedT;
        float cx = oneMinusT * oneMinusT * e.X + 2f * oneMinusT * easedT * e.ControlX + easedT * easedT * e.TargetX;
        float cy = oneMinusT * oneMinusT * e.Y + 2f * oneMinusT * easedT * e.ControlY + easedT * easedT * e.TargetY;

        // Skalierung (schrumpft beim Ziel)
        float scale = EasingFunctions.Lerp(1f, 0.5f, easedT);
        float coinSize = e.Size * scale;

        // Goldene Münze zeichnen
        _particlePaint.Color = new SKColor(0xFF, 0xD7, 0x00); // Gold
        canvas.DrawCircle(cx, cy, coinSize, _particlePaint);

        // Euro-Prägung
        _particlePaint.Color = new SKColor(0xDA, 0xA5, 0x20); // DarkerGold
        float euroSize = coinSize * 0.5f;
        _font.Size = euroSize * 2f;
        _font.Embolden = false;
        _textPaint.Color = new SKColor(0xDA, 0xA5, 0x20);
        canvas.DrawText("E", cx - euroSize * 0.4f, cy + euroSize * 0.4f, SKTextAlign.Left, _font, _textPaint);

        // Glanz-Highlight
        _particlePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 80);
        canvas.DrawCircle(cx - coinSize * 0.25f, cy - coinSize * 0.25f, coinSize * 0.3f, _particlePaint);
    }

    private void RenderSparkle(SKCanvas canvas, ref JuiceEffect e, float progress)
    {
        float alpha = EasingFunctions.PingPong(progress);
        float sparklePhase = e.Phase + progress * MathF.PI * 4f;
        float size = e.Size * (0.5f + 0.5f * MathF.Sin(sparklePhase));

        _particlePaint.Color = e.Color.WithAlpha((byte)(alpha * 200));

        // 4-zackiger Stern (gecachter Path, Reset pro Aufruf)
        _sparklePath.Reset();
        _sparklePath.MoveTo(e.X, e.Y - size);
        _sparklePath.LineTo(e.X + size * 0.3f, e.Y);
        _sparklePath.LineTo(e.X + size, e.Y);
        _sparklePath.LineTo(e.X + size * 0.3f, e.Y);
        _sparklePath.LineTo(e.X, e.Y + size);
        _sparklePath.LineTo(e.X - size * 0.3f, e.Y);
        _sparklePath.LineTo(e.X - size, e.Y);
        _sparklePath.LineTo(e.X - size * 0.3f, e.Y);
        _sparklePath.Close();

        canvas.DrawPath(_sparklePath, _particlePaint);
    }

    private void RenderNumberPop(SKCanvas canvas, ref JuiceEffect e, float progress)
    {
        // Scale: EaseOutBack für "Herausspringen"
        float scaleT = Math.Min(progress * 4f, 1f); // Schnelle Scale-Animation (250ms)
        float scale = EasingFunctions.EaseOutBack(scaleT);

        // Fade-Out im letzten Drittel
        float alpha = progress < 0.6f ? 1f : 1f - (progress - 0.6f) / 0.4f;

        float fontSize = e.Size * scale;
        _font.Size = fontSize;
        _font.Embolden = true;

        // Schatten
        _textPaint.Color = new SKColor(0, 0, 0, (byte)(alpha * 120));
        canvas.DrawText(e.Text ?? "", e.X + 1.5f, e.Y + 1.5f, SKTextAlign.Center, _font, _textPaint);

        // Text
        _textPaint.Color = e.Color.WithAlpha((byte)(alpha * 255));
        canvas.DrawText(e.Text ?? "", e.X, e.Y, SKTextAlign.Center, _font, _textPaint);
    }

    private void RenderShockwave(SKCanvas canvas, ref JuiceEffect e, float progress)
    {
        float easedProgress = EasingFunctions.EaseOutCubic(progress);
        float radius = e.MaxRadius * easedProgress;
        float alpha = (1f - progress) * 0.5f;

        _ringPaint.Color = e.Color.WithAlpha((byte)(alpha * 255));
        _ringPaint.StrokeWidth = Math.Max(1f, 6f * (1f - progress));

        canvas.DrawCircle(e.X, e.Y, radius, _ringPaint);

        // Zweiter dünnerer Ring (leicht versetzt)
        if (progress > 0.1f)
        {
            float innerProgress = (progress - 0.1f) / 0.9f;
            float innerEased = EasingFunctions.EaseOutCubic(innerProgress);
            float innerRadius = e.MaxRadius * 0.7f * innerEased;
            float innerAlpha = (1f - innerProgress) * 0.3f;

            _ringPaint.Color = e.Color.WithAlpha((byte)(innerAlpha * 255));
            _ringPaint.StrokeWidth = Math.Max(1f, 3f * (1f - innerProgress));
            canvas.DrawCircle(e.X, e.Y, innerRadius, _ringPaint);
        }
    }

    private void RenderConfetti(SKCanvas canvas, ref JuiceEffect e, float progress)
    {
        float alpha = progress < 0.7f ? 1f : 1f - (progress - 0.7f) / 0.3f;
        float rotation = e.Phase;

        canvas.Save();
        canvas.Translate(e.X, e.Y);
        canvas.RotateDegrees(rotation * 57.3f); // Radians zu Grad

        _particlePaint.Color = e.Color.WithAlpha((byte)(alpha * 255));

        // Rechteck-Konfetti (rotierend)
        float w = e.Size;
        float h = e.Size * 0.6f * MathF.Abs(MathF.Sin(rotation * 0.5f));
        canvas.DrawRect(-w / 2, -h / 2, w, Math.Max(1f, h), _particlePaint);

        canvas.Restore();
    }

    private void RenderVignette(SKCanvas canvas, SKRect bounds)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float radius = Math.Max(bounds.Width, bounds.Height) * 0.7f;

        byte maxAlpha = (byte)(_vignetteIntensity * 180);

        using var shader = SKShader.CreateRadialGradient(
            new SKPoint(cx, cy), radius,
            new[] { SKColors.Transparent, new SKColor(0, 0, 0, maxAlpha) },
            new[] { 0.4f, 1.0f },
            SKShaderTileMode.Clamp);

        _vignettePaint.Shader = shader;
        canvas.DrawRect(bounds, _vignettePaint);
        _vignettePaint.Shader = null;
    }

    // --- Hilfsmethoden ---

    private void AddEffect(JuiceEffect effect)
    {
        if (_effectCount >= MaxEffects)
        {
            // Ältesten Effekt überschreiben
            _effects[0] = effect;
            return;
        }
        _effects[_effectCount++] = effect;
    }

    private float NextRandom(float min, float max)
    {
        _rngState = _rngState * 1664525 + 1013904223;
        float t = (_rngState >> 16) / 65535f;
        return min + (max - min) * t;
    }

    // Konfetti-Farben
    private static readonly SKColor[] ConfettiColors =
    {
        new(0xFF, 0xD7, 0x00), // Gold
        new(0xFF, 0x45, 0x00), // Rot-Orange
        new(0x00, 0xBF, 0xFF), // Blau
        new(0x32, 0xCD, 0x32), // Grün
        new(0xFF, 0x69, 0xB4), // Pink
        new(0xFF, 0xA5, 0x00), // Orange
        new(0x87, 0xCE, 0xFA), // Hellblau
        new(0xFF, 0xFF, 0x00), // Gelb
    };

    // --- Effekt-Typen ---

    private enum EffectType : byte
    {
        RadialBurst,
        CoinFly,
        Sparkle,
        NumberPop,
        Shockwave,
        Confetti
    }

    /// <summary>
    /// Struct-basierter Effekt für GC-freie Performance.
    /// </summary>
    private struct JuiceEffect
    {
        public EffectType Type;
        public float X, Y;
        public float TargetX, TargetY;
        public float ControlX, ControlY;
        public float VelocityX, VelocityY;
        public SKColor Color;
        public float Duration;
        public float Timer;
        public float MaxRadius;
        public float Size;
        public float Phase;
        public float Delay;
        public string? Text;
    }

    /// <summary>
    /// Gibt native SkiaSharp-Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _particlePaint?.Dispose();
        _glowPaint?.Dispose();
        _textPaint?.Dispose();
        _overlayPaint?.Dispose();
        _vignettePaint?.Dispose();
        _ringPaint?.Dispose();
        _font?.Dispose();
        _sparklePath?.Dispose();
    }
}
