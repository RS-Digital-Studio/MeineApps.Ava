using System;
using SkiaSharp;

namespace ZeitManager.Graphics;

/// <summary>
/// Animierter "Warm Clockwork"-Hintergrund fuer den ZeitManager.
/// Rendert 5 Layer: Warmer 3-Farben-Gradient, konzentrische Uhrenringe,
/// driftende Gluehwuermchen-Partikel, Tick-Markierungen, radiale Vignette.
/// Instance-basiert mit GC-freiem Render-Loop (Struct-Pool, gecachte Paints).
/// Wird von einem ~5fps DispatcherTimer in der MainView invalidiert.
/// </summary>
public sealed class ClockworkBackgroundRenderer : IDisposable
{
    private bool _disposed;

    // =====================================================================
    // Farb-Konstanten
    // =====================================================================

    private static readonly SKColor GradientTop = SKColor.Parse("#382C22");    // Warmes Schokobraun
    private static readonly SKColor GradientMid = SKColor.Parse("#2A2018");    // Tiefes Dunkelbraun
    private static readonly SKColor GradientBot = SKColor.Parse("#301A10");    // Warmes Espresso-Dunkel
    private static readonly SKColor AmberAccent = SKColor.Parse("#F7A833");    // Primary Amber

    // =====================================================================
    // Partikel-Pool (Gluehwuermchen)
    // =====================================================================

    private struct DotParticle
    {
        public float X, Y;
        public float VelocityX, VelocityY;
        public float Radius;
        public float Life, MaxLife;
        public float Phase;
        public bool Active;
    }

    private const int MaxParticles = 12;
    private readonly DotParticle[] _particles = new DotParticle[MaxParticles];

    // =====================================================================
    // Gecachte Paints (0 GC im Render-Loop)
    // =====================================================================

    private readonly SKPaint _gradientPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    private readonly SKPaint _ringPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private readonly SKPaint _tickPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _particlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _vignettePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // =====================================================================
    // Gecachte MaskFilter (Glow fuer Partikel)
    // =====================================================================

    private readonly SKMaskFilter _particleGlowMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);

    // =====================================================================
    // Shader-Cache (nur bei Bounds-Aenderung neu erstellt)
    // =====================================================================

    private SKShader? _bgShader;
    private SKShader? _vignetteShader;
    private float _lastW, _lastH;

    // =====================================================================
    // Update (vom DispatcherTimer aufgerufen)
    // =====================================================================

    /// <summary>
    /// Aktualisiert Partikel-Positionen und spawnt neue. Pro Frame aufrufen.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Bestehende Partikel bewegen
        for (int i = 0; i < MaxParticles; i++)
        {
            if (!_particles[i].Active) continue;

            ref var p = ref _particles[i];
            p.Life += deltaTime;

            if (p.Life >= p.MaxLife)
            {
                p.Active = false;
                continue;
            }

            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
        }

        // Neue Partikel spawnen (bei ~5fps ca. alle 1-2 Sekunden ein neuer)
        SpawnParticleIfNeeded();
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
        RenderConcentricRings(canvas, bounds, time);
        RenderTickMarks(canvas, bounds);
        RenderParticles(canvas, bounds);
        RenderVignette(canvas, bounds);
    }

    // =====================================================================
    // Layer 1: Vertikaler 3-Farben Gradient (warm-dunkel)
    // =====================================================================

    private void RenderBackground(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width;
        float h = bounds.Height;

        // Shader nur neu erstellen wenn sich die Groesse geaendert hat
        if (_bgShader == null || MathF.Abs(w - _lastW) > 1f || MathF.Abs(h - _lastH) > 1f)
        {
            _bgShader?.Dispose();

            _bgShader = SKShader.CreateLinearGradient(
                new SKPoint(bounds.MidX, bounds.Top),
                new SKPoint(bounds.MidX, bounds.Bottom),
                new[] { GradientTop, GradientMid, GradientBot },
                new[] { 0f, 0.5f, 1f },
                SKShaderTileMode.Clamp);

            // Bounds merken
            _lastW = w;
            _lastH = h;

            // Vignette-Shader invalidieren
            _vignetteShader?.Dispose();
            _vignetteShader = null;
        }

        _gradientPaint.Shader = _bgShader;
        canvas.DrawRect(bounds, _gradientPaint);
        _gradientPaint.Shader = null;
    }

    // =====================================================================
    // Layer 2: Konzentrische Kreisringe (Uhrzifferblatt-Echo)
    // =====================================================================

    private void RenderConcentricRings(SKCanvas canvas, SKRect bounds, float time)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY;

        // Basis-Radius: 30% der kleineren Dimension
        float baseRadius = MathF.Min(bounds.Width, bounds.Height) * 0.30f;

        // 4 Ringe mit leichtem Pulsieren ueber time (Radius +/- 2px via sin)
        for (int i = 0; i < 4; i++)
        {
            float radiusFactor = 0.6f + i * 0.25f; // 0.6, 0.85, 1.1, 1.35
            float pulseDelta = 2f * MathF.Sin(time * 0.8f + i * 0.7f);
            float radius = baseRadius * radiusFactor + pulseDelta;

            // Alpha 8-10, Amber-Farbe
            byte alpha = (byte)(8 + (i % 2) * 2); // 8 oder 10
            _ringPaint.Color = AmberAccent.WithAlpha(alpha);
            _ringPaint.StrokeWidth = 1f;

            canvas.DrawCircle(cx, cy, radius, _ringPaint);
        }
    }

    // =====================================================================
    // Layer 3: Driftende Dot-Partikel (Gluehwuermchen)
    // =====================================================================

    private void RenderParticles(SKCanvas canvas, SKRect bounds)
    {
        for (int i = 0; i < MaxParticles; i++)
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

            // Basis-Alpha 15-20, moduliert durch Lebenszyklus
            float baseAlpha = 15f + (p.Phase / MathF.Tau) * 5f; // 15-20
            byte finalAlpha = (byte)(baseAlpha * alpha);
            if (finalAlpha < 2) continue;

            // Glow-Kreis (groesserer Radius, niedrigeres Alpha)
            _particlePaint.MaskFilter = _particleGlowMask;
            _particlePaint.Color = AmberAccent.WithAlpha((byte)(finalAlpha / 2));
            canvas.DrawCircle(p.X, p.Y, p.Radius * 2f, _particlePaint);
            _particlePaint.MaskFilter = null;

            // Kern-Punkt (kleinerer Radius, volle Farbe)
            _particlePaint.Color = AmberAccent.WithAlpha(finalAlpha);
            canvas.DrawCircle(p.X, p.Y, p.Radius, _particlePaint);
        }
    }

    // =====================================================================
    // Layer 4: Tick-Markierungen (Minutenstriche)
    // =====================================================================

    private void RenderTickMarks(SKCanvas canvas, SKRect bounds)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY;

        // Ring-Radius: 38% der kleineren Dimension
        float ringRadius = MathF.Min(bounds.Width, bounds.Height) * 0.38f;

        // 60 Tick-Markierungen wie eine Uhr, aber sehr dezent
        for (int i = 0; i < 60; i++)
        {
            float angle = i * 6f * MathF.PI / 180f - MathF.PI / 2f;

            bool isMajor = i % 5 == 0; // Stunden-Markierung (12 Stueck)
            float innerRadius = isMajor ? ringRadius * 0.92f : ringRadius * 0.96f;
            float outerRadius = ringRadius;

            float x1 = cx + MathF.Cos(angle) * innerRadius;
            float y1 = cy + MathF.Sin(angle) * innerRadius;
            float x2 = cx + MathF.Cos(angle) * outerRadius;
            float y2 = cy + MathF.Sin(angle) * outerRadius;

            // Alpha 6-10 fuer Haupt-Ticks, 4-6 fuer Neben-Ticks
            byte alpha = isMajor ? (byte)10 : (byte)5;
            _tickPaint.Color = AmberAccent.WithAlpha(alpha);
            _tickPaint.StrokeWidth = isMajor ? 1.5f : 0.8f;

            canvas.DrawLine(x1, y1, x2, y2, _tickPaint);
        }
    }

    // =====================================================================
    // Layer 5: Radiale Vignette (dunkle Ecken)
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
                new[] { SKColors.Transparent, new SKColor(0, 0, 0, 140) },
                new[] { 0.4f, 1.0f },
                SKShaderTileMode.Clamp);
        }

        _vignettePaint.Shader = _vignetteShader;
        canvas.DrawRect(bounds, _vignettePaint);
        _vignettePaint.Shader = null;
    }

    // =====================================================================
    // Partikel-Spawn
    // =====================================================================

    /// <summary>
    /// Spawnt einen neuen Partikel wenn ein freier Slot vorhanden und
    /// die Spawn-Wahrscheinlichkeit erfuellt ist.
    /// </summary>
    private void SpawnParticleIfNeeded()
    {
        // Freien Slot suchen und aktive zaehlen
        int freeSlot = -1;
        int activeCount = 0;
        for (int i = 0; i < MaxParticles; i++)
        {
            if (!_particles[i].Active)
            {
                if (freeSlot < 0) freeSlot = i;
            }
            else
            {
                activeCount++;
            }
        }

        if (freeSlot < 0) return;

        // Maximal 8 aktive Partikel gleichzeitig
        if (activeCount >= 8) return;

        // Spawn-Wahrscheinlichkeit: ~30% pro Frame bei ~5fps = ca. 1.5 neue/s
        if (Random.Shared.NextSingle() > 0.30f) return;

        // Bounds-Abhaengig (nutze zuletzt bekannte Groesse)
        if (_lastW < 10f || _lastH < 10f) return;

        ref var p = ref _particles[freeSlot];
        p.Active = true;
        p.Life = 0f;
        p.MaxLife = 4f + Random.Shared.NextSingle() * 4f; // 4-8 Sekunden
        p.Radius = 2f + Random.Shared.NextSingle() * 2f;  // 2-4px
        p.Phase = Random.Shared.NextSingle() * MathF.Tau;

        // Startposition: Breit verteilt ueber den gesamten Screen
        p.X = _lastW * Random.Shared.NextSingle();
        p.Y = _lastH * Random.Shared.NextSingle();

        // Langsame Drift in zufaellige Richtung (wie schwebende Gluehwuermchen)
        float driftAngle = Random.Shared.NextSingle() * MathF.Tau;
        float driftSpeed = 3f + Random.Shared.NextSingle() * 5f; // 3-8 px/s
        p.VelocityX = MathF.Cos(driftAngle) * driftSpeed;
        p.VelocityY = MathF.Sin(driftAngle) * driftSpeed;
    }

    // =====================================================================
    // Dispose
    // =====================================================================

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _gradientPaint.Dispose();
        _ringPaint.Dispose();
        _tickPaint.Dispose();
        _particlePaint.Dispose();
        _vignettePaint.Dispose();

        _particleGlowMask.Dispose();

        _bgShader?.Dispose();
        _vignetteShader?.Dispose();
    }
}
