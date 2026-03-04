using System;
using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Animierter medizinischer Hintergrund fuer den FitnessRechner.
/// Rendert 5 Layer: Navy-Gradient, EKG-Grid, EKG-Trace, Vital-Partikel, Vignette.
/// Instance-basiert mit GC-freiem Render-Loop (Struct-Pools, gecachte Paints, Path.Reset).
/// Wird von einem 20fps DispatcherTimer in der MainView invalidiert.
/// </summary>
public sealed class MedicalBackgroundRenderer : IDisposable
{
    private bool _disposed;

    // =====================================================================
    // Partikel-Typen und -Pool
    // =====================================================================

    private enum VitalParticleType : byte { Cross, Heart, WaterDrop, Scale, Pulse }

    private struct VitalParticle
    {
        public float X, Y, VelocityX, VelocityY;
        public float Alpha, Size, Life, MaxLife, Phase;
        public VitalParticleType Type;
        public bool Active;
    }

    private const int MaxParticles = 60;
    private readonly VitalParticle[] _particles = new VitalParticle[MaxParticles];

    // =====================================================================
    // Beat-State
    // =====================================================================

    private float _beatTimer;
    private float _beatGlow;

    // =====================================================================
    // Gecachte Paints (0 GC im Render-Loop)
    // =====================================================================

    private readonly SKPaint _gradientPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    private readonly SKPaint _gridPaintFine = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
    private readonly SKPaint _gridPaintThick = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private readonly SKPaint _ekgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKPaint _ekgGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _particlePaint = new() { IsAntialias = true };
    private readonly SKPaint _vignettePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // =====================================================================
    // Gecachte Paths
    // =====================================================================

    private readonly SKPath _ekgPath = new();
    private readonly SKPath _particlePath = new();

    // =====================================================================
    // Gecachte MaskFilter
    // =====================================================================

    private readonly SKMaskFilter _ekgGlowMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f);
    private readonly SKMaskFilter _particleGlowMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);

    // =====================================================================
    // Shader-Cache mit Bounds-Check
    // =====================================================================

    private SKShader? _bgShader;
    private SKShader? _vignetteShader;
    private float _lastW, _lastH;

    // =====================================================================
    // Vorberechnete Grid-Farben (vermeidet Allokation pro Frame)
    // =====================================================================

    private readonly SKColor _gridColorFine = MedicalColors.Grid.WithAlpha(20);
    private readonly SKColor _gridColorThick = MedicalColors.Grid.WithAlpha(30);

    // =====================================================================
    // Update (vom DispatcherTimer aufgerufen)
    // =====================================================================

    /// <summary>
    /// Aktualisiert Beat-Timer, Glow und Partikel. Pro Frame aufrufen.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Beat-Timer
        _beatTimer += deltaTime;
        if (_beatTimer >= MedicalColors.BeatPeriod)
        {
            _beatTimer -= MedicalColors.BeatPeriod;
            _beatGlow = 1f;
            EmitBeatParticles(2 + Random.Shared.Next(2)); // 2-3 pro Beat
        }

        // Beat-Glow abklingen
        if (_beatGlow > 0f)
            _beatGlow = MathF.Max(0f, _beatGlow - deltaTime * 4f);

        // Partikel aktualisieren
        for (int i = 0; i < _particles.Length; i++)
        {
            if (!_particles[i].Active) continue;
            ref var p = ref _particles[i];
            p.Life += deltaTime;
            if (p.Life >= p.MaxLife) { p.Active = false; continue; }
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
        }
    }

    // =====================================================================
    // Render (5 Layer)
    // =====================================================================

    /// <summary>
    /// Zeichnet alle 5 Layer. bounds = canvas.LocalClipBounds.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, float time)
    {
        RenderBackground(canvas, bounds);
        RenderGrid(canvas, bounds);
        RenderEkg(canvas, bounds);
        RenderParticles(canvas, bounds);
        RenderVignette(canvas, bounds);
    }

    // =====================================================================
    // Layer 1: Deep Navy Gradient (radial, Mitte hell, Ecken dunkel)
    // =====================================================================

    private void RenderBackground(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width;
        float h = bounds.Height;

        // Shader nur neu erstellen wenn sich die Groesse geaendert hat
        if (_bgShader == null || MathF.Abs(w - _lastW) > 1f || MathF.Abs(h - _lastH) > 1f)
        {
            _bgShader?.Dispose();

            float radius = MathF.Sqrt(w * w + h * h) * 0.5f;

            _bgShader = SKShader.CreateRadialGradient(
                new SKPoint(bounds.MidX, bounds.MidY),
                radius,
                new[] { MedicalColors.NavyDeep, MedicalColors.NavyDarkest },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp);

            // Bounds merken (wird auch fuer Vignette verwendet)
            _lastW = w;
            _lastH = h;

            // Vignette-Shader auch invalidieren
            _vignetteShader?.Dispose();
            _vignetteShader = null;
        }

        _gradientPaint.Shader = _bgShader;
        canvas.DrawRect(bounds, _gradientPaint);
        _gradientPaint.Shader = null;
    }

    // =====================================================================
    // Layer 2: Medical Grid (statisch, wie EKG-Papier)
    // =====================================================================

    private void RenderGrid(SKCanvas canvas, SKRect bounds)
    {
        float left = bounds.Left;
        float top = bounds.Top;
        float right = bounds.Right;
        float bottom = bounds.Bottom;

        // Feine Linien alle 40px
        _gridPaintFine.Color = _gridColorFine;
        for (float x = left; x <= right; x += 40f)
            canvas.DrawLine(x, top, x, bottom, _gridPaintFine);
        for (float y = top; y <= bottom; y += 40f)
            canvas.DrawLine(left, y, right, y, _gridPaintFine);

        // Dickere Linien alle 200px
        _gridPaintThick.Color = _gridColorThick;
        for (float x = left; x <= right; x += 200f)
            canvas.DrawLine(x, top, x, bottom, _gridPaintThick);
        for (float y = top; y <= bottom; y += 200f)
            canvas.DrawLine(left, y, right, y, _gridPaintThick);
    }

    // =====================================================================
    // Layer 3: EKG-Trace (animiert, Sweep von links nach rechts)
    // =====================================================================

    private void RenderEkg(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width;
        float h = bounds.Height;

        float ekgLeft = bounds.Left + w * 0.10f;
        float ekgWidth = w * 0.80f; // 10% bis 90%
        float ekgY = bounds.Top + h * 0.42f;
        float ekgAmplitude = h * 0.12f;

        // Sweep-Position basierend auf Beat-Timer
        float sweepNorm = _beatTimer / MedicalColors.BeatPeriod; // 0-1

        // EKG-Pfad aufbauen (3 Wiederholungen = 72 Punkte)
        _ekgPath.Reset();

        int waveLen = MedicalColors.EkgWave.Length;
        int totalPoints = waveLen * 3;
        bool firstPoint = true;

        for (int seg = 0; seg < 3; seg++)
        {
            for (int j = 0; j < waveLen; j++)
            {
                int totalIndex = seg * waveLen + j;
                float xNorm = (float)totalIndex / totalPoints;

                float px = ekgLeft + xNorm * ekgWidth;
                float py = ekgY - MedicalColors.EkgWave[j] * ekgAmplitude;

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

        // Sweep-X berechnen
        float sweepX = ekgLeft + sweepNorm * ekgWidth;

        // Trail-Effekt: Nur links vom Sweep-Punkt sichtbar mit LinearGradient-Fade
        canvas.Save();
        canvas.ClipRect(new SKRect(ekgLeft, ekgY - ekgAmplitude * 1.5f, sweepX, ekgY + ekgAmplitude * 1.5f));

        // Trail-Shader: Links transparent, am Sweep-Punkt voll Cyan
        float trailStart = MathF.Max(ekgLeft, sweepX - ekgWidth * 0.7f);
        using var trailShader = SKShader.CreateLinearGradient(
            new SKPoint(trailStart, ekgY),
            new SKPoint(sweepX, ekgY),
            new[] { MedicalColors.Cyan.WithAlpha(0), MedicalColors.Cyan },
            null,
            SKShaderTileMode.Clamp);

        _ekgPaint.Shader = trailShader;
        canvas.DrawPath(_ekgPath, _ekgPaint);
        _ekgPaint.Shader = null;

        canvas.Restore();

        // Glow-Punkt am Sweep-Ende
        // Y-Position des Sweep-Punktes aus der Welle berechnen
        int sweepWaveIndex = (int)(sweepNorm * waveLen) % waveLen;
        float sweepPy = ekgY - MedicalColors.EkgWave[sweepWaveIndex] * ekgAmplitude;

        // Verstaerkter Glow bei QRS-Spike
        float glowSize = 5f + _beatGlow * 15f;
        byte glowAlpha = (byte)(60 + _beatGlow * 180);

        _ekgGlowPaint.Color = MedicalColors.Cyan.WithAlpha(glowAlpha);
        _ekgGlowPaint.MaskFilter = _ekgGlowMask;
        canvas.DrawCircle(sweepX, sweepPy, glowSize, _ekgGlowPaint);
        _ekgGlowPaint.MaskFilter = null;

        // Fester Kern-Punkt
        _ekgGlowPaint.Color = MedicalColors.Cyan;
        canvas.DrawCircle(sweepX, sweepPy, 3f, _ekgGlowPaint);
    }

    // =====================================================================
    // Layer 4: Vital-Partikel (animiert)
    // =====================================================================

    private void RenderParticles(SKCanvas canvas, SKRect bounds)
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            if (!_particles[i].Active) continue;

            ref var p = ref _particles[i];
            float lifeRatio = p.Life / p.MaxLife;

            // Fade: In (0-20%) -> Voll (20-70%) -> Out (70-100%)
            float alpha;
            if (lifeRatio < 0.2f)
                alpha = lifeRatio / 0.2f;
            else if (lifeRatio < 0.7f)
                alpha = 1f;
            else
                alpha = 1f - (lifeRatio - 0.7f) / 0.3f;

            float finalAlpha = p.Alpha * alpha;
            if (finalAlpha < 0.01f) continue;

            byte alphaB = (byte)(finalAlpha * 255f);
            _particlePaint.Color = MedicalColors.Cyan.WithAlpha(alphaB);

            switch (p.Type)
            {
                case VitalParticleType.Cross:
                    DrawCross(canvas, p.X, p.Y, p.Size);
                    break;
                case VitalParticleType.Heart:
                    DrawHeart(canvas, p.X, p.Y, p.Size);
                    break;
                case VitalParticleType.WaterDrop:
                    DrawWaterDrop(canvas, p.X, p.Y, p.Size);
                    break;
                case VitalParticleType.Scale:
                    DrawScale(canvas, p.X, p.Y, p.Size);
                    break;
                case VitalParticleType.Pulse:
                    DrawPulse(canvas, p.X, p.Y, p.Size, alphaB);
                    break;
            }
        }
    }

    // --- Partikel-Formen ---

    /// <summary>
    /// Kreuz-Form (+ Symbol, medizinisch).
    /// </summary>
    private void DrawCross(SKCanvas canvas, float x, float y, float size)
    {
        float half = size * 0.5f;
        float thick = size * 0.2f;

        // Vertikale Linie
        _particlePaint.Style = SKPaintStyle.Stroke;
        _particlePaint.StrokeWidth = thick;
        canvas.DrawLine(x, y - half, x, y + half, _particlePaint);
        // Horizontale Linie
        canvas.DrawLine(x - half, y, x + half, y, _particlePaint);
        _particlePaint.Style = SKPaintStyle.Fill;
    }

    /// <summary>
    /// Kleines Herz (2x Bezier oben, Punkt unten).
    /// </summary>
    private void DrawHeart(SKCanvas canvas, float x, float y, float size)
    {
        float s = size * 0.5f;

        _particlePath.Reset();
        _particlePath.MoveTo(x, y + s * 0.7f); // Untere Spitze
        // Linke Haelfte
        _particlePath.CubicTo(
            x - s * 1.2f, y - s * 0.2f,
            x - s * 0.6f, y - s * 1.0f,
            x, y - s * 0.4f);
        // Rechte Haelfte
        _particlePath.CubicTo(
            x + s * 0.6f, y - s * 1.0f,
            x + s * 1.2f, y - s * 0.2f,
            x, y + s * 0.7f);
        _particlePath.Close();

        canvas.DrawPath(_particlePath, _particlePaint);
    }

    /// <summary>
    /// Tropfen-Form (Arc oben + Spitze unten).
    /// </summary>
    private void DrawWaterDrop(SKCanvas canvas, float x, float y, float size)
    {
        float s = size * 0.5f;

        _particlePath.Reset();
        _particlePath.MoveTo(x, y - s); // Spitze oben
        // Linke Seite nach unten
        _particlePath.CubicTo(
            x - s * 0.8f, y,
            x - s * 0.6f, y + s * 0.8f,
            x, y + s);
        // Rechte Seite nach oben
        _particlePath.CubicTo(
            x + s * 0.6f, y + s * 0.8f,
            x + s * 0.8f, y,
            x, y - s);
        _particlePath.Close();

        canvas.DrawPath(_particlePath, _particlePaint);
    }

    /// <summary>
    /// Waagen-Silhouette (Dreieck + Balken).
    /// </summary>
    private void DrawScale(SKCanvas canvas, float x, float y, float size)
    {
        float s = size * 0.5f;

        // Balken oben
        _particlePaint.Style = SKPaintStyle.Stroke;
        _particlePaint.StrokeWidth = size * 0.15f;
        canvas.DrawLine(x - s, y - s * 0.3f, x + s, y - s * 0.3f, _particlePaint);

        // Stuetze (vertikale Linie Mitte)
        canvas.DrawLine(x, y - s * 0.3f, x, y + s * 0.4f, _particlePaint);

        // Basis-Dreieck
        _particlePaint.Style = SKPaintStyle.Fill;
        _particlePath.Reset();
        _particlePath.MoveTo(x - s * 0.4f, y + s * 0.6f);
        _particlePath.LineTo(x + s * 0.4f, y + s * 0.6f);
        _particlePath.LineTo(x, y + s * 0.3f);
        _particlePath.Close();
        canvas.DrawPath(_particlePath, _particlePaint);
    }

    /// <summary>
    /// Puls-Partikel: Kreis mit Blur-Glow.
    /// </summary>
    private void DrawPulse(SKCanvas canvas, float x, float y, float size, byte alpha)
    {
        // Glow
        _particlePaint.MaskFilter = _particleGlowMask;
        _particlePaint.Color = MedicalColors.Cyan.WithAlpha((byte)(alpha / 2));
        canvas.DrawCircle(x, y, size, _particlePaint);
        _particlePaint.MaskFilter = null;

        // Kern
        _particlePaint.Color = MedicalColors.Cyan.WithAlpha(alpha);
        canvas.DrawCircle(x, y, size * 0.5f, _particlePaint);
    }

    // =====================================================================
    // Layer 5: Corner-Vignette
    // =====================================================================

    private void RenderVignette(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width;
        float h = bounds.Height;

        // Shader nur neu erstellen wenn sich die Groesse geaendert hat
        if (_vignetteShader == null)
        {
            float radius = MathF.Max(w, h) * 0.75f;

            _vignetteShader = SKShader.CreateRadialGradient(
                new SKPoint(bounds.MidX, bounds.MidY),
                radius,
                new[] { SKColors.Transparent, MedicalColors.NavyDarkest.WithAlpha(153) },
                new[] { 0.4f, 1.0f },
                SKShaderTileMode.Clamp);
        }

        _vignettePaint.Shader = _vignetteShader;
        canvas.DrawRect(bounds, _vignettePaint);
        _vignettePaint.Shader = null;
    }

    // =====================================================================
    // Partikel-Emission
    // =====================================================================

    /// <summary>
    /// Erzeugt 2-3 Vital-Partikel bei jedem Herzschlag.
    /// </summary>
    private void EmitBeatParticles(int count)
    {
        for (int c = 0; c < count; c++)
        {
            // Freien Slot suchen
            int freeSlot = -1;
            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].Active)
                {
                    freeSlot = i;
                    break;
                }
            }

            if (freeSlot < 0) return; // Pool voll

            ref var p = ref _particles[freeSlot];
            p.Active = true;
            p.Life = 0f;
            p.MaxLife = 3f + Random.Shared.NextSingle() * 2f; // 3-5 Sekunden
            p.Size = 4f + Random.Shared.NextSingle() * 4f; // 4-8px
            p.Alpha = 0.15f + Random.Shared.NextSingle() * 0.10f; // 0.15-0.25
            p.Phase = Random.Shared.NextSingle() * MathF.Tau;
            p.Type = (VitalParticleType)Random.Shared.Next(5);

            // Position: Breit verteilt, untere 80% der Hoehe
            // X/Y werden als absolute Pixel gesetzt, basierend auf den letzten bekannten Bounds
            p.X = _lastW * Random.Shared.NextSingle();
            p.Y = _lastH * 0.2f + _lastH * 0.8f * Random.Shared.NextSingle();

            // Langsam aufsteigend mit leichter X-Drift
            p.VelocityX = -5f + Random.Shared.NextSingle() * 10f;
            p.VelocityY = -(15f + Random.Shared.NextSingle() * 10f); // -15 bis -25 px/s
        }
    }

    // =====================================================================
    // Dispose
    // =====================================================================

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _gradientPaint.Dispose();
        _gridPaintFine.Dispose();
        _gridPaintThick.Dispose();
        _ekgPaint.Dispose();
        _ekgGlowPaint.Dispose();
        _particlePaint.Dispose();
        _vignettePaint.Dispose();

        _ekgPath.Dispose();
        _particlePath.Dispose();

        _ekgGlowMask.Dispose();
        _particleGlowMask.Dispose();

        _bgShader?.Dispose();
        _vignetteShader?.Dispose();
    }
}
