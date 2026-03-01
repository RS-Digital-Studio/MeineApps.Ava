using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace ZeitManager.Graphics;

/// <summary>
/// SkiaSharp-Timer-Ring mit Flüssigkeits-Effekt, Tropfen-Partikel an der Ablaufkante,
/// großen Countdown-Ziffern in den letzten 5 Sekunden und Ablauf-Burst bei Timer=0.
/// </summary>
public static class TimerVisualization
{
    private static readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _arcPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _wavePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _dropPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _burstPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _countdownPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _timeFont = new() { Size = 28f };
    private static readonly SKFont _nameFont = new() { Size = 11f };
    private static readonly SKFont _countdownFont = new() { Size = 80f };
    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);
    private static readonly SKMaskFilter _countdownGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10f);

    // Tropfen-Partikel System (einfaches Array, kein Heap-Alloc)
    private const int MaxDrops = 8;
    private static readonly float[] _dropX = new float[MaxDrops];
    private static readonly float[] _dropY = new float[MaxDrops];
    private static readonly float[] _dropVy = new float[MaxDrops];
    private static readonly float[] _dropLife = new float[MaxDrops];
    private static readonly float[] _dropSize = new float[MaxDrops];
    private static float _lastDropSpawn;
    private static readonly Random _rng = new();

    // Ablauf-Burst Partikel
    private const int MaxBurstParticles = 20;
    private static readonly float[] _burstX = new float[MaxBurstParticles];
    private static readonly float[] _burstY = new float[MaxBurstParticles];
    private static readonly float[] _burstVx = new float[MaxBurstParticles];
    private static readonly float[] _burstVy = new float[MaxBurstParticles];
    private static readonly float[] _burstLife = new float[MaxBurstParticles];
    private static readonly float[] _burstSize = new float[MaxBurstParticles];
    private static readonly SKColor[] _burstColors = new SKColor[MaxBurstParticles];
    private static bool _burstActive;
    private static float _burstTime;

    /// <summary>
    /// Setzt alle statischen Partikel-Daten zurück (Tropfen, Burst).
    /// Muss beim Start eines neuen Timers aufgerufen werden, da statische Felder
    /// über verschiedene Timer-Lebenszyklen hinweg bestehen bleiben.
    /// </summary>
    public static void Reset()
    {
        // Tropfen-Partikel zurücksetzen
        Array.Clear(_dropX);
        Array.Clear(_dropY);
        Array.Clear(_dropVy);
        Array.Clear(_dropLife);
        Array.Clear(_dropSize);
        _lastDropSpawn = 0f;

        // Burst-Partikel zurücksetzen
        Array.Clear(_burstX);
        Array.Clear(_burstY);
        Array.Clear(_burstVx);
        Array.Clear(_burstVy);
        Array.Clear(_burstLife);
        Array.Clear(_burstSize);
        Array.Clear(_burstColors);
        _burstActive = false;
        _burstTime = 0f;
    }

    /// <summary>
    /// Bestimmt die Akzent-Farbe basierend auf dem verbleibenden Fortschritt.
    /// Grün > 30%, Amber 10-30%, Rot < 10%.
    /// </summary>
    private static SKColor GetProgressColor(float fraction)
    {
        if (fraction > 0.3f) return SkiaThemeHelper.Success;
        if (fraction > 0.1f) return SkiaThemeHelper.Warning;
        return SkiaThemeHelper.Error;
    }

    /// <summary>
    /// Rendert einen einzelnen Timer-Ring mit Flüssigkeits-Effekt, Tropfen und Countdown.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="progressFraction">Verbleibende Zeit als Fraction (1.0 = voll, 0.0 = leer)</param>
    /// <param name="isRunning">Ob der Timer gerade läuft</param>
    /// <param name="isFinished">Ob der Timer abgelaufen ist</param>
    /// <param name="remainingFormatted">Formatierte verbleibende Zeit</param>
    /// <param name="timerName">Name des Timers (optional)</param>
    /// <param name="animTime">Laufender Animations-Timer</param>
    /// <param name="remainingSeconds">Verbleibende Sekunden (für Countdown-Anzeige)</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        float progressFraction, bool isRunning, bool isFinished,
        string remainingFormatted, string? timerName, float animTime,
        double remainingSeconds = -1)
    {
        float size = Math.Min(bounds.Width, bounds.Height);
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float strokeW = 5f;
        float radius = (size - strokeW * 2 - 12f) / 2f;

        if (radius <= 10) return;

        float progress = Math.Clamp(progressFraction, 0f, 1f);
        var color = isFinished ? SkiaThemeHelper.Success : GetProgressColor(progress);
        var arcRect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        // 1. Track-Ring
        _trackPaint.StrokeWidth = strokeW;
        _trackPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 40);
        canvas.DrawOval(arcRect, _trackPaint);

        // 2. Fortschrittsring (gegen den Uhrzeigersinn, läuft ab)
        float sweepAngle = progress * 360f;
        if (sweepAngle > 0.5f)
        {
            // Glow-Effekt bei laufendem Timer
            if (isRunning)
            {
                float pulse = 0.6f + 0.4f * MathF.Sin(animTime * 3f);
                _glowPaint.StrokeWidth = strokeW + 5f;
                _glowPaint.Color = color.WithAlpha((byte)(60 * pulse));
                _glowPaint.MaskFilter = _glowFilter;

                using var glowPath = new SKPath();
                glowPath.AddArc(arcRect, -90f, sweepAngle);
                canvas.DrawPath(glowPath, _glowPaint);
                _glowPaint.MaskFilter = null;
            }

            // Fortschrittsring
            var endColor = SkiaThemeHelper.AdjustBrightness(color, 1.3f);
            _arcPaint.StrokeWidth = strokeW;
            _arcPaint.Shader?.Dispose();
            _arcPaint.Shader = SKShader.CreateSweepGradient(
                new SKPoint(cx, cy),
                new[] { color, endColor },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp, -90f, -90f + sweepAngle);
            _arcPaint.Color = SKColors.White;

            using var arcPath = new SKPath();
            arcPath.AddArc(arcRect, -90f, sweepAngle);
            canvas.DrawPath(arcPath, _arcPaint);
            _arcPaint.Shader?.Dispose();
            _arcPaint.Shader = null;
        }

        // 3. Wellen-Füllung im Inneren (Flüssigkeits-Effekt)
        if (progress > 0.01f && !isFinished)
        {
            float innerR = radius - strokeW / 2f - 2f;
            DrawLiquidFill(canvas, cx, cy, innerR, progress, color, animTime, isRunning);
        }

        // 4. Tropfen-Partikel an der Flüssigkeits-Kante (Zeit "rinnt davon")
        if (isRunning && progress > 0.02f && progress < 0.98f)
        {
            float innerR = radius - strokeW / 2f - 2f;
            float fillTop = cy + innerR - (2f * innerR * progress);
            UpdateAndDrawDrops(canvas, cx, cy, innerR, fillTop, color, animTime);
        }

        // 5. Ablauf-Burst bei Timer=0
        if (isFinished)
        {
            if (!_burstActive)
                TriggerBurst(cx, cy, radius);
            UpdateAndDrawBurst(canvas, animTime);
        }
        else
        {
            _burstActive = false;
        }

        // 6. Countdown-Ziffern (letzte 5 Sekunden) oder Fertig-Häkchen
        if (isFinished)
        {
            DrawCheckmark(canvas, cx, cy, radius * 0.3f, color);
        }
        else if (remainingSeconds >= 0 && remainingSeconds <= 5 && remainingSeconds > 0 && isRunning)
        {
            DrawCountdownDigit(canvas, cx, cy, radius, (int)Math.Ceiling(remainingSeconds), color, animTime);
        }
        else
        {
            // Normale Zeitanzeige
            _textPaint.Color = SkiaThemeHelper.TextPrimary;
            _timeFont.Size = Math.Max(18f, radius * 0.32f);
            canvas.DrawText(remainingFormatted, cx, cy + _timeFont.Size * 0.15f,
                SKTextAlign.Center, _timeFont, _textPaint);
        }

        // 7. Timer-Name (unten im Ring)
        if (!string.IsNullOrEmpty(timerName))
        {
            _textPaint.Color = SkiaThemeHelper.TextMuted;
            _nameFont.Size = Math.Max(9f, radius * 0.11f);
            canvas.DrawText(timerName, cx, cy + radius * 0.55f,
                SKTextAlign.Center, _nameFont, _textPaint);
        }
    }

    /// <summary>
    /// Zeichnet eine große Countdown-Ziffer mit Scale-Bounce (1.5→1.0) in den letzten 5 Sekunden.
    /// </summary>
    private static void DrawCountdownDigit(SKCanvas canvas, float cx, float cy,
        float radius, int secondsLeft, SKColor color, float animTime)
    {
        // Bounce-Effekt: Bei jedem neuen Sekundenwechsel Scale von 1.5 auf 1.0
        float subSecond = animTime % 1f;
        float bounceScale = 1f + 0.4f * MathF.Pow(Math.Max(0, 1f - subSecond * 3f), 2);

        // Opacity-Fade innerhalb jeder Sekunde
        float alpha = Math.Min(1f, 1.3f - subSecond);

        string digit = secondsLeft.ToString();

        // Glow-Schatten
        _countdownPaint.Color = color.WithAlpha((byte)(40 * alpha));
        _countdownPaint.MaskFilter = _countdownGlow;
        _countdownFont.Size = radius * 0.7f * bounceScale;
        canvas.DrawText(digit, cx, cy + _countdownFont.Size * 0.35f,
            SKTextAlign.Center, _countdownFont, _countdownPaint);
        _countdownPaint.MaskFilter = null;

        // Hauptziffer
        _countdownPaint.Color = color.WithAlpha((byte)(220 * alpha));
        canvas.DrawText(digit, cx, cy + _countdownFont.Size * 0.35f,
            SKTextAlign.Center, _countdownFont, _countdownPaint);
    }

    /// <summary>
    /// Aktualisiert und zeichnet Tropfen-Partikel die von der Flüssigkeitsoberfläche fallen.
    /// </summary>
    private static void UpdateAndDrawDrops(SKCanvas canvas, float cx, float cy,
        float innerR, float fillTop, SKColor color, float animTime)
    {
        float dt = 0.033f; // ~30fps

        // Neue Tropfen spawnen (alle 0.3-0.6 Sekunden)
        if (animTime - _lastDropSpawn > 0.3f + (float)_rng.NextDouble() * 0.3f)
        {
            _lastDropSpawn = animTime;

            // Freien Slot finden
            for (int i = 0; i < MaxDrops; i++)
            {
                if (_dropLife[i] <= 0)
                {
                    // Tropfen startet an der Flüssigkeitsoberfläche
                    float halfW = MathF.Sqrt(innerR * innerR - (fillTop - cy) * (fillTop - cy));
                    if (float.IsNaN(halfW)) halfW = innerR * 0.5f;

                    _dropX[i] = cx + ((float)_rng.NextDouble() - 0.5f) * halfW * 1.5f;
                    _dropY[i] = fillTop;
                    _dropVy[i] = 30f + (float)_rng.NextDouble() * 40f; // Nach unten
                    _dropLife[i] = 0.8f + (float)_rng.NextDouble() * 0.4f;
                    _dropSize[i] = 1.5f + (float)_rng.NextDouble() * 2f;
                    break;
                }
            }
        }

        // Tropfen updaten und zeichnen
        for (int i = 0; i < MaxDrops; i++)
        {
            if (_dropLife[i] <= 0) continue;

            _dropLife[i] -= dt;
            _dropY[i] += _dropVy[i] * dt;
            _dropVy[i] += 120f * dt; // Schwerkraft

            // Nur innerhalb des Kreises zeichnen
            float distFromCenter = MathF.Sqrt((_dropX[i] - cx) * (_dropX[i] - cx) + (_dropY[i] - cy) * (_dropY[i] - cy));
            if (distFromCenter > innerR)
            {
                _dropLife[i] = 0;
                continue;
            }

            float dropAlpha = Math.Clamp(_dropLife[i] * 2f, 0f, 1f);
            _dropPaint.Color = color.WithAlpha((byte)(100 * dropAlpha));
            canvas.DrawCircle(_dropX[i], _dropY[i], _dropSize[i], _dropPaint);
        }
    }

    /// <summary>
    /// Löst einen Partikel-Burst aus wenn der Timer abgelaufen ist.
    /// </summary>
    private static void TriggerBurst(float cx, float cy, float radius)
    {
        _burstActive = true;
        _burstTime = 0;

        SKColor[] colors = { SkiaThemeHelper.Success, SkiaThemeHelper.Warning,
            new(0x22, 0xD3, 0xEE), new(0xFF, 0xD7, 0x00), new(0xA7, 0x8B, 0xFA) };

        for (int i = 0; i < MaxBurstParticles; i++)
        {
            float angle = (float)_rng.NextDouble() * MathF.PI * 2f;
            float speed = 80f + (float)_rng.NextDouble() * 120f;

            _burstX[i] = cx;
            _burstY[i] = cy;
            _burstVx[i] = MathF.Cos(angle) * speed;
            _burstVy[i] = MathF.Sin(angle) * speed;
            _burstLife[i] = 0.6f + (float)_rng.NextDouble() * 0.6f;
            _burstSize[i] = 2f + (float)_rng.NextDouble() * 4f;
            _burstColors[i] = colors[_rng.Next(colors.Length)];
        }
    }

    /// <summary>
    /// Aktualisiert und zeichnet die Ablauf-Burst Partikel.
    /// </summary>
    private static void UpdateAndDrawBurst(SKCanvas canvas, float animTime)
    {
        if (!_burstActive) return;

        float dt = 0.033f;
        _burstTime += dt;

        bool anyAlive = false;
        for (int i = 0; i < MaxBurstParticles; i++)
        {
            if (_burstLife[i] <= 0) continue;

            anyAlive = true;
            _burstLife[i] -= dt;
            _burstX[i] += _burstVx[i] * dt;
            _burstY[i] += _burstVy[i] * dt;
            _burstVy[i] += 150f * dt; // Schwerkraft

            float alpha = Math.Clamp(_burstLife[i] * 1.5f, 0f, 1f);
            _burstPaint.Color = _burstColors[i].WithAlpha((byte)(200 * alpha));

            // Rotierende Rechtecke (Confetti-Stil)
            canvas.Save();
            canvas.Translate(_burstX[i], _burstY[i]);
            canvas.RotateDegrees(_burstTime * 300f + i * 45f);
            canvas.DrawRect(-_burstSize[i] / 2, -_burstSize[i] / 3,
                _burstSize[i], _burstSize[i] * 0.66f, _burstPaint);
            canvas.Restore();
        }

        if (!anyAlive)
            _burstActive = false;
    }

    /// <summary>
    /// Zeichnet eine Wellen-Füllung innerhalb eines kreisförmigen Bereichs.
    /// </summary>
    private static void DrawLiquidFill(SKCanvas canvas, float cx, float cy, float radius,
        float fraction, SKColor color, float animTime, bool isRunning)
    {
        // Füllhöhe (von unten nach oben)
        float fillTop = cy + radius - (2f * radius * fraction);

        // Clip auf den Kreis
        canvas.Save();
        using var clipPath = new SKPath();
        clipPath.AddCircle(cx, cy, radius);
        canvas.ClipPath(clipPath);

        // Füllung mit Gradient
        _fillPaint.Shader?.Dispose();
        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(cx, fillTop),
            new SKPoint(cx, cy + radius),
            new[] { color.WithAlpha(40), color.WithAlpha(20) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawRect(cx - radius, fillTop, radius * 2, cy + radius - fillTop, _fillPaint);
        _fillPaint.Shader?.Dispose();
        _fillPaint.Shader = null;

        // Welleneffekt an der Oberfläche (nur bei laufend)
        if (isRunning && fraction > 0.02f && fraction < 0.98f)
        {
            using var wavePath = new SKPath();
            float waveAmplitude = 3f;
            float waveFreq = 0.06f;
            float waveSpeed = animTime * 2.5f;

            wavePath.MoveTo(cx - radius, fillTop);
            for (float x = cx - radius; x <= cx + radius; x += 2f)
            {
                float wave = MathF.Sin((x - cx) * waveFreq + waveSpeed) * waveAmplitude
                           + MathF.Sin((x - cx) * waveFreq * 1.5f + waveSpeed * 0.7f) * waveAmplitude * 0.5f;
                wavePath.LineTo(x, fillTop + wave);
            }
            wavePath.LineTo(cx + radius, cy + radius);
            wavePath.LineTo(cx - radius, cy + radius);
            wavePath.Close();

            _wavePaint.Color = color.WithAlpha(30);
            canvas.DrawPath(wavePath, _wavePaint);
        }

        canvas.Restore();
    }

    /// <summary>
    /// Zeichnet ein Häkchen-Symbol für abgeschlossene Timer.
    /// </summary>
    private static void DrawCheckmark(SKCanvas canvas, float cx, float cy, float size, SKColor color)
    {
        using var checkPath = new SKPath();
        checkPath.MoveTo(cx - size * 0.5f, cy);
        checkPath.LineTo(cx - size * 0.1f, cy + size * 0.4f);
        checkPath.LineTo(cx + size * 0.5f, cy - size * 0.35f);

        _trackPaint.StrokeWidth = 3f;
        _trackPaint.Color = color;
        _trackPaint.StrokeCap = SKStrokeCap.Round;
        canvas.DrawPath(checkPath, _trackPaint);
    }
}
