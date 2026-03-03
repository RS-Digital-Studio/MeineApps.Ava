using SkiaSharp;
using MeineApps.UI.SkiaSharp.SplashScreen;

namespace FitnessRechner.Graphics;

/// <summary>
/// Splash-Renderer "Der Herzschlag" für den FitnessRechner.
/// Zeigt eine EKG-Herzschlag-Linie, pulsierende Ringe und Herzschlag-Partikel.
/// </summary>
public sealed class FitnessRechnerSplashRenderer : SplashRendererBase
{
    // --- EKG-Wellenform (24 normalisierte Y-Offsets von der Baseline) ---
    private static readonly float[] EkgWave =
    {
        0f, 0f, 0.05f, 0.08f, 0.05f, 0f,                          // P-Welle
        0f, -0.08f, 0.45f, -0.15f, 0f, 0f,                        // QRS-Komplex
        0f, 0f, 0.03f, 0.06f, 0.08f, 0.06f, 0.03f, 0f,            // T-Welle
        0f, 0f, 0f, 0f                                              // Baseline
    };

    // --- Partikel ---
    private const int HeartbeatParticleCount = 20;
    private struct HeartbeatParticle { public float X, Y, Alpha, VelocityX, VelocityY, Life, MaxLife; }
    private readonly HeartbeatParticle[] _particles = new HeartbeatParticle[HeartbeatParticleCount];
    private int _nextParticle; // Ring-Buffer-Index

    // --- Herzschlag-Timing ---
    private const float BeatsPerSecond = 1.2f; // 72 BPM
    private const float BeatPeriod = 1f / BeatsPerSecond;
    private float _beatTimer;
    private float _beatGlow; // 0-1, Glow bei QRS-Spike

    // --- Ripple-Ringe (3 konzentrische, expandierend) ---
    private const int RippleCount = 3;
    private readonly float[] _ripplePhases = new float[RippleCount];

    // --- Pulsierender Hintergrund-Kreis ---
    private float _bgCircleScale = 1f;

    // --- Farben ---
    private static readonly SKColor ColorBgTop = new(0x0A, 0x1A, 0x1F);
    private static readonly SKColor ColorBgBottom = new(0x05, 0x10, 0x15);
    private static readonly SKColor ColorCyan = new(0x06, 0xB6, 0xD4);
    private static readonly SKColor ColorGreen = new(0x22, 0xC5, 0x5E);
    private static readonly SKColor ColorProgressBg = new(0x0A, 0x1A, 0x1F);

    // --- Gecachte Paints ---
    private readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _ripplePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private readonly SKPaint _bgCirclePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _ekgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKPaint _ekgGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _namePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.White };
    private readonly SKPaint _particlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // --- Gecachte Fonts ---
    private readonly SKFont _nameFont = new() { Size = 28f };

    // --- Gecachte Pfade ---
    private readonly SKPath _ekgPath = new();

    // --- Gecachte MaskFilter ---
    private readonly SKMaskFilter _ekgGlowMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10f);

    public FitnessRechnerSplashRenderer()
    {
        // Ripple-Phasen versetzt starten (jeweils ~0.33 Sekunden Abstand)
        for (var i = 0; i < RippleCount; i++)
            _ripplePhases[i] = i * (BeatPeriod / RippleCount);

        // Partikel initial tot (Life=0)
        for (var i = 0; i < HeartbeatParticleCount; i++)
            _particles[i].Life = 0f;
    }

    protected override void OnUpdate(float deltaTime)
    {
        // --- Herzschlag-Timer ---
        _beatTimer += deltaTime;
        if (_beatTimer >= BeatPeriod)
        {
            _beatTimer -= BeatPeriod;
            _beatGlow = 1f;

            // Partikel-Burst bei jedem Beat: 4-5 Partikel emittieren
            EmitBeatParticles(4);
        }

        // Beat-Glow abklingen (schneller Decay)
        if (_beatGlow > 0f)
            _beatGlow = MathF.Max(0f, _beatGlow - deltaTime * 4f);

        // Hintergrund-Kreis pulsiert im Herzrhythmus
        var beatNorm = _beatTimer / BeatPeriod; // 0-1 innerhalb eines Beats
        _bgCircleScale = 1f + 0.05f * MathF.Exp(-beatNorm * 6f); // Schneller Impuls, langsames Decay

        // --- Ripple-Ringe aktualisieren ---
        for (var i = 0; i < RippleCount; i++)
        {
            _ripplePhases[i] += deltaTime;
            if (_ripplePhases[i] >= BeatPeriod)
                _ripplePhases[i] -= BeatPeriod;
        }

        // --- Partikel aktualisieren ---
        for (var i = 0; i < HeartbeatParticleCount; i++)
        {
            ref var p = ref _particles[i];
            if (p.Life <= 0f) continue;

            p.Life -= deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;

            // Alpha fadet mit abnehmender Lebensdauer
            var lifeRatio = Math.Clamp(p.Life / p.MaxLife, 0f, 1f);
            p.Alpha = lifeRatio * 180f;

            // Geschwindigkeit verlangsamen (Drag)
            p.VelocityX *= 1f - deltaTime * 1.5f;
            p.VelocityY *= 1f - deltaTime * 1.5f;
        }
    }

    protected override void OnRender(SKCanvas canvas, SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;
        var cx = w / 2f;

        // --- Hintergrund: Dunkel-Cyan Gradient ---
        using (var bgShader = SKShader.CreateLinearGradient(
                   new SKPoint(cx, 0), new SKPoint(cx, h),
                   new[] { ColorBgTop, ColorBgBottom }, null, SKShaderTileMode.Clamp))
        {
            _bgPaint.Shader = bgShader;
            canvas.DrawRect(bounds, _bgPaint);
            _bgPaint.Shader = null;
        }

        var ekgCenterY = h * 0.42f;

        // --- Pulsierende Ripple-Ringe ---
        DrawRipples(canvas, cx, ekgCenterY, w, h);

        // --- Pulsierender Hintergrund-Kreis ---
        var bgCircleRadius = MathF.Min(w, h) * 0.22f * _bgCircleScale;
        _bgCirclePaint.Color = new SKColor(0x06, 0xB6, 0xD4, 0x0F);
        canvas.DrawCircle(cx, ekgCenterY, bgCircleRadius, _bgCirclePaint);

        // --- App-Name ---
        var nameY = h * 0.12f;
        _nameFont.Size = MathF.Min(28f, w * 0.065f);
        _namePaint.Color = SKColors.White;
        DrawCenteredText(canvas, AppName, nameY, _nameFont, _namePaint, w);

        // --- EKG-Linie ---
        DrawEkg(canvas, w, h, cx, ekgCenterY);

        // --- Herzschlag-Partikel ---
        DrawParticles(canvas, w, h);

        // --- Fortschrittsbalken ---
        var barWidth = w * 0.55f;
        var barY = h * 0.72f;
        DrawProgressBar(canvas, w, barY, barWidth, 8f, 4f, ColorCyan, ColorGreen, ColorProgressBg);

        // --- Status-Text und Version ---
        DrawStatusText(canvas, w, h * 0.77f);
        DrawVersion(canvas, w, h * 0.92f);
    }

    /// <summary>
    /// Zeichnet 3 pulsierende konzentrische Ripple-Ringe.
    /// </summary>
    private void DrawRipples(SKCanvas canvas, float cx, float cy, float w, float h)
    {
        var maxRadius = MathF.Min(w, h) * 0.45f;

        for (var i = 0; i < RippleCount; i++)
        {
            var phase = _ripplePhases[i] / BeatPeriod; // 0-1
            var radius = maxRadius * phase;
            var alpha = (byte)(40 * (1f - phase)); // Fadet mit Expansion

            if (alpha < 2) continue;

            _ripplePaint.Color = new SKColor(0x06, 0xB6, 0xD4, alpha);
            _ripplePaint.StrokeWidth = MathF.Max(1f, 2f * (1f - phase));
            canvas.DrawCircle(cx, cy, radius, _ripplePaint);
        }
    }

    /// <summary>
    /// Zeichnet die EKG-Herzschlag-Linie als horizontalen Sweep mit Trail.
    /// </summary>
    private void DrawEkg(SKCanvas canvas, float w, float h, float cx, float ekgY)
    {
        var ekgWidth = w * 0.80f;
        var ekgLeft = w * 0.10f;
        var ekgAmplitude = h * 0.12f;

        // Sweep-Position basierend auf Beat-Timer (periodisch von links nach rechts)
        var sweepNorm = _beatTimer / BeatPeriod; // 0-1 pro Beat-Zyklus

        _ekgPath.Reset();

        var waveLen = EkgWave.Length;
        var firstPoint = true;

        // Die gesamte EKG-Welle über die Breite zeichnen, mit Alpha-Trail
        for (var seg = 0; seg < 3; seg++) // 3 Wiederholungen der Welle über die Breite
        {
            for (var j = 0; j < waveLen; j++)
            {
                var totalIndex = seg * waveLen + j;
                var totalPoints = waveLen * 3;
                var xNorm = (float)totalIndex / totalPoints;

                var px = ekgLeft + xNorm * ekgWidth;
                var py = ekgY - EkgWave[j] * ekgAmplitude;

                if (firstPoint)
                {
                    _ekgPath.MoveTo(px, py);
                    firstPoint = false;
                }
                else
                {
                    _ekgPath.LineTo(px, py);
                }
            }
        }

        // Trail-Effekt: Sweep-Punkt wandert, links davon sichtbar mit Fade
        // Verwende einen Shader-Gradient für den Alpha-Fade
        var sweepX = ekgLeft + sweepNorm * ekgWidth;

        canvas.Save();
        // Clip auf den sichtbaren Bereich (links vom Sweep-Punkt)
        canvas.ClipRect(new SKRect(ekgLeft, ekgY - ekgAmplitude * 1.5f, sweepX, ekgY + ekgAmplitude * 1.5f));

        // Trail mit Alpha-Gradient (links=transparent, rechts=voll)
        using (var trailShader = SKShader.CreateLinearGradient(
                   new SKPoint(MathF.Max(ekgLeft, sweepX - ekgWidth * 0.7f), ekgY),
                   new SKPoint(sweepX, ekgY),
                   new[] { new SKColor(0x06, 0xB6, 0xD4, 0x00), ColorCyan },
                   null, SKShaderTileMode.Clamp))
        {
            _ekgPaint.Shader = trailShader;
            canvas.DrawPath(_ekgPath, _ekgPaint);
            _ekgPaint.Shader = null;
        }

        canvas.Restore();

        // Glow-Punkt am Sweep-Ende
        var sweepWaveIndex = (int)(sweepNorm * EkgWave.Length) % EkgWave.Length;
        var sweepPy = ekgY - EkgWave[sweepWaveIndex] * ekgAmplitude;

        // Verstärkter Glow bei QRS-Spike (Beat-Moment)
        var glowSize = 5f + _beatGlow * 15f;
        var glowAlpha = (byte)(60 + _beatGlow * 180);

        _ekgGlowPaint.Color = new SKColor(0x06, 0xB6, 0xD4, glowAlpha);
        _ekgGlowPaint.MaskFilter = _ekgGlowMask;
        canvas.DrawCircle(sweepX, sweepPy, glowSize, _ekgGlowPaint);
        _ekgGlowPaint.MaskFilter = null;

        // Fester Kern-Punkt
        _ekgGlowPaint.Color = ColorCyan;
        canvas.DrawCircle(sweepX, sweepPy, 3f, _ekgGlowPaint);
    }

    /// <summary>
    /// Emittiert Partikel bei einem Herzschlag.
    /// Partikel driften radial nach außen vom EKG-Zentrum.
    /// </summary>
    private void EmitBeatParticles(int count)
    {
        for (var i = 0; i < count; i++)
        {
            ref var p = ref _particles[_nextParticle];
            _nextParticle = (_nextParticle + 1) % HeartbeatParticleCount;

            // Starte in der Mitte (normalisierte Koordinaten)
            p.X = 0.5f;
            p.Y = 0.42f;

            // Radiale Geschwindigkeit nach außen
            var angle = Rng.NextSingle() * MathF.Tau;
            var speed = 0.05f + Rng.NextSingle() * 0.08f;
            p.VelocityX = MathF.Cos(angle) * speed;
            p.VelocityY = MathF.Sin(angle) * speed;

            p.MaxLife = 0.8f + Rng.NextSingle() * 0.6f;
            p.Life = p.MaxLife;
            p.Alpha = 180f;
        }
    }

    /// <summary>
    /// Zeichnet die Herzschlag-Partikel.
    /// </summary>
    private void DrawParticles(SKCanvas canvas, float w, float h)
    {
        for (var i = 0; i < HeartbeatParticleCount; i++)
        {
            ref var p = ref _particles[i];
            if (p.Life <= 0f) continue;

            var px = p.X * w;
            var py = p.Y * h;
            var alpha = (byte)Math.Clamp(p.Alpha, 0, 255);
            var lifeRatio = Math.Clamp(p.Life / p.MaxLife, 0f, 1f);
            var radius = 2f + 3f * (1f - lifeRatio); // Expandiert beim Sterben

            // Farbe: Cyan→Grün basierend auf Lebensdauer
            var r = (byte)(0x06 + (0x22 - 0x06) * (1f - lifeRatio));
            var g = (byte)(0xB6 + (0xC5 - 0xB6) * (1f - lifeRatio));
            var b = (byte)(0xD4 + (0x5E - 0xD4) * (1f - lifeRatio));

            _particlePaint.Color = new SKColor(r, g, b, alpha);
            canvas.DrawCircle(px, py, radius, _particlePaint);
        }
    }

    protected override void OnDispose()
    {
        _bgPaint.Dispose();
        _ripplePaint.Dispose();
        _bgCirclePaint.Dispose();
        _ekgPaint.Dispose();
        _ekgGlowPaint.Dispose();
        _namePaint.Dispose();
        _particlePaint.Dispose();

        _nameFont.Dispose();

        _ekgPath.Dispose();

        _ekgGlowMask.Dispose();
    }
}
