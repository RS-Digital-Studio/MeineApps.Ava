namespace RebornSaga.Rendering.Backgrounds.Layers;

using SkiaSharp;
using System;

/// <summary>
/// Leichtgewichtiger Partikel-Renderer für atmosphärische Effekte.
/// Kein State, kein Array — deterministische Animation basierend auf time + Index.
/// </summary>
public static class SceneParticleRenderer
{
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    public static void Render(SKCanvas canvas, SKRect bounds, ParticleDef[] particles, float time)
    {
        foreach (var p in particles)
        {
            switch (p.Style)
            {
                case ParticleStyle.Firefly: DrawFireflies(canvas, bounds, p, time); break;
                case ParticleStyle.Spark: DrawSparks(canvas, bounds, p, time); break;
                case ParticleStyle.Dust: DrawDust(canvas, bounds, p, time); break;
                case ParticleStyle.Leaf: DrawLeaves(canvas, bounds, p, time); break;
                case ParticleStyle.Snowflake: DrawSnowflakes(canvas, bounds, p, time); break;
                case ParticleStyle.MagicOrb: DrawMagicOrbs(canvas, bounds, p, time); break;
                case ParticleStyle.Ember: DrawEmbers(canvas, bounds, p, time); break;
                case ParticleStyle.Star: DrawStars(canvas, bounds, p, time); break;
                case ParticleStyle.ScanLine: DrawScanLines(canvas, bounds, p, time); break;
                case ParticleStyle.GlitchLine: DrawGlitchLines(canvas, bounds, p, time); break;
                case ParticleStyle.Smoke: DrawSmoke(canvas, bounds, p, time); break;
                case ParticleStyle.RingOrbit: DrawRingOrbit(canvas, bounds, p, time); break;
            }
        }
    }

    /// <summary>Kleine leuchtende Punkte, langsame Sinus-Drift, pulsierender Alpha.</summary>
    private static void DrawFireflies(SKCanvas canvas, SKRect bounds, ParticleDef p, float time)
    {
        for (int i = 0; i < p.Count; i++)
        {
            var phase = i * 2.39f; // Goldener Winkel für Verteilung
            var x = bounds.Left + bounds.Width * ((MathF.Sin(time * 0.3f + phase) + 1f) * 0.5f);
            var y = bounds.Top + bounds.Height * (0.2f + 0.6f * ((MathF.Cos(time * 0.2f + phase * 1.7f) + 1f) * 0.5f));
            var alpha = (byte)(p.Alpha * (0.3f + MathF.Sin(time * 2f + phase) * 0.7f));

            _fillPaint.Color = p.Color.WithAlpha(alpha);
            canvas.DrawCircle(x, y, 2f, _fillPaint);
            // Glow
            _fillPaint.Color = p.Color.WithAlpha((byte)(alpha * 0.3f));
            canvas.DrawCircle(x, y, 5f, _fillPaint);
        }
    }

    /// <summary>Aufwärts, schnell, verblasst.</summary>
    private static void DrawSparks(SKCanvas canvas, SKRect bounds, ParticleDef p, float time)
    {
        for (int i = 0; i < p.Count; i++)
        {
            var phase = i * 1.83f;
            var cycle = (time * 1.5f + phase) % 3f;
            var t = cycle / 3f; // 0-1

            var x = bounds.MidX + MathF.Sin(phase * 3f) * bounds.Width * 0.15f +
                    MathF.Sin(time * 4f + phase) * 5f;
            var y = bounds.Bottom - bounds.Height * 0.2f - t * bounds.Height * 0.5f;
            var alpha = (byte)(p.Alpha * (1f - t));
            var size = 1.5f * (1f - t * 0.5f);

            _fillPaint.Color = p.Color.WithAlpha(alpha);
            canvas.DrawCircle(x, y, size, _fillPaint);
        }
    }

    /// <summary>Sehr langsam schwebend, kaum sichtbar.</summary>
    private static void DrawDust(SKCanvas canvas, SKRect bounds, ParticleDef p, float time)
    {
        for (int i = 0; i < p.Count; i++)
        {
            var phase = i * 3.14f;
            var x = bounds.Left + bounds.Width * ((i + 0.5f) / p.Count) +
                    MathF.Sin(time * 0.15f + phase) * 15f;
            var y = bounds.Top + bounds.Height * (0.15f + 0.7f *
                    ((MathF.Sin(time * 0.1f + phase * 0.7f) + 1f) * 0.5f));
            var alpha = (byte)(p.Alpha * (0.4f + MathF.Sin(time * 0.5f + phase) * 0.3f));

            _fillPaint.Color = p.Color.WithAlpha(alpha);
            canvas.DrawCircle(x, y, 1.5f, _fillPaint);
        }
    }

    /// <summary>Fällt diagonal, leichtes Pendeln.</summary>
    private static void DrawLeaves(SKCanvas canvas, SKRect bounds, ParticleDef p, float time)
    {
        for (int i = 0; i < p.Count; i++)
        {
            var phase = i * 2.17f;
            var cycle = (time * 0.4f + phase) % 4f;
            var t = cycle / 4f;

            var x = bounds.Left + bounds.Width * ((i + 0.3f) / p.Count) +
                    MathF.Sin(time * 1.5f + phase) * 12f;
            var y = bounds.Top + t * bounds.Height;
            var rotation = MathF.Sin(time * 2f + phase) * 30f;

            canvas.Save();
            canvas.Translate(x, y);
            canvas.RotateDegrees(rotation);
            _fillPaint.Color = p.Color.WithAlpha(p.Alpha);
            canvas.DrawOval(0, 0, 4f, 2f, _fillPaint);
            canvas.Restore();
        }
    }

    /// <summary>Fällt langsam, seitlicher Drift.</summary>
    private static void DrawSnowflakes(SKCanvas canvas, SKRect bounds, ParticleDef p, float time)
    {
        for (int i = 0; i < p.Count; i++)
        {
            var phase = i * 1.61f;
            var cycle = (time * 0.25f + phase) % 5f;
            var t = cycle / 5f;

            var x = bounds.Left + bounds.Width * ((i + 0.5f) / p.Count) +
                    MathF.Sin(time * 0.8f + phase) * 20f;
            var y = bounds.Top + t * bounds.Height;

            _fillPaint.Color = p.Color.WithAlpha(p.Alpha);
            canvas.DrawCircle(x, y, 2f, _fillPaint);
        }
    }

    /// <summary>Kreisförmige Orbit-Bewegung, leuchtend.</summary>
    private static void DrawMagicOrbs(SKCanvas canvas, SKRect bounds, ParticleDef p, float time)
    {
        for (int i = 0; i < p.Count; i++)
        {
            var phase = i * MathF.PI * 2f / p.Count;
            var orbitR = bounds.Width * (0.1f + i * 0.03f);
            var speed = 0.5f + i * 0.1f;

            var x = bounds.MidX + MathF.Cos(time * speed + phase) * orbitR;
            var y = bounds.MidY * 0.7f + MathF.Sin(time * speed + phase) * orbitR * 0.5f;
            var pulse = 0.6f + MathF.Sin(time * 2f + phase) * 0.4f;

            // Glow
            _fillPaint.Color = p.Color.WithAlpha((byte)(p.Alpha * 0.3f * pulse));
            canvas.DrawCircle(x, y, 8f, _fillPaint);
            // Kern
            _fillPaint.Color = p.Color.WithAlpha((byte)(p.Alpha * pulse));
            canvas.DrawCircle(x, y, 3f, _fillPaint);
        }
    }

    /// <summary>Wie Sparks aber langsamer, größer.</summary>
    private static void DrawEmbers(SKCanvas canvas, SKRect bounds, ParticleDef p, float time)
    {
        for (int i = 0; i < p.Count; i++)
        {
            var phase = i * 2.47f;
            var cycle = (time * 0.6f + phase) % 4f;
            var t = cycle / 4f;

            var x = bounds.MidX + MathF.Sin(phase * 2f) * bounds.Width * 0.2f +
                    MathF.Sin(time * 1.5f + phase) * 8f;
            var y = bounds.Bottom - bounds.Height * 0.15f - t * bounds.Height * 0.6f;
            var alpha = (byte)(p.Alpha * (1f - t * 0.8f));

            _fillPaint.Color = p.Color.WithAlpha(alpha);
            canvas.DrawCircle(x, y, 2.5f * (1f - t * 0.3f), _fillPaint);
        }
    }

    /// <summary>Statisch, Twinkle-Alpha.</summary>
    private static void DrawStars(SKCanvas canvas, SKRect bounds, ParticleDef p, float time)
    {
        for (int i = 0; i < p.Count; i++)
        {
            var phase = i * 7.13f;
            // Feste Position (deterministisch aus Index)
            var x = bounds.Left + bounds.Width * ((MathF.Sin(phase) + 1f) * 0.5f);
            var y = bounds.Top + bounds.Height * 0.4f * ((MathF.Cos(phase * 1.3f) + 1f) * 0.5f);
            var twinkle = 0.3f + MathF.Sin(time * 1.5f + phase) * 0.7f;

            _fillPaint.Color = p.Color.WithAlpha((byte)(p.Alpha * twinkle));
            canvas.DrawCircle(x, y, 1.5f, _fillPaint);
        }
    }

    /// <summary>Horizontale Scan-Linien (SystemVoid).</summary>
    private static void DrawScanLines(SKCanvas canvas, SKRect bounds, ParticleDef p, float time)
    {
        _strokePaint.Color = p.Color.WithAlpha(p.Alpha);
        _strokePaint.StrokeWidth = 0.5f;

        // Statische Linien
        for (float y = bounds.Top; y < bounds.Bottom; y += 4f)
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, _strokePaint);

        // Wandernde helle Linie
        var scanY = bounds.Top + ((time * 100f) % bounds.Height);
        _strokePaint.Color = p.Color.WithAlpha((byte)(p.Alpha * 2));
        _strokePaint.StrokeWidth = 2f;
        canvas.DrawLine(bounds.Left, scanY, bounds.Right, scanY, _strokePaint);
    }

    /// <summary>Zufällige horizontale Segmente (Dreamworld).</summary>
    private static void DrawGlitchLines(SKCanvas canvas, SKRect bounds, ParticleDef p, float time)
    {
        _strokePaint.StrokeWidth = 1f;

        for (int i = 0; i < p.Count; i++)
        {
            var ly = bounds.Top + bounds.Height * ((MathF.Sin(time * 0.5f + i * 0.8f) + 1f) / 2f);
            var lw = bounds.Width * (0.1f + MathF.Sin(time * 3f + i) * 0.05f);
            var lx = bounds.MidX - lw / 2f + MathF.Sin(time * 2f + i * 1.5f) * 20f;
            var alpha = (byte)(p.Alpha * (0.5f + MathF.Sin(time * 4f + i) * 0.5f));

            _strokePaint.Color = p.Color.WithAlpha(alpha);
            canvas.DrawLine(lx, ly, lx + lw, ly, _strokePaint);
        }
    }

    /// <summary>Langsam aufsteigend, große Kreise, niedrig-Alpha.</summary>
    private static void DrawSmoke(SKCanvas canvas, SKRect bounds, ParticleDef p, float time)
    {
        for (int i = 0; i < p.Count; i++)
        {
            var phase = i * 2.91f;
            var cycle = (time * 0.15f + phase) % 6f;
            var t = cycle / 6f;

            var x = bounds.Left + bounds.Width * ((i + 0.5f) / p.Count) +
                    MathF.Sin(time * 0.3f + phase) * 15f;
            var y = bounds.Bottom - bounds.Height * 0.1f - t * bounds.Height * 0.5f;
            var size = 15f + t * 25f;
            var alpha = (byte)(p.Alpha * (1f - t) * 0.5f);

            _fillPaint.Color = p.Color.WithAlpha(alpha);
            canvas.DrawCircle(x, y, size, _fillPaint);
        }
    }

    /// <summary>Kreise die langsam um Zentrum rotieren (Title).</summary>
    private static void DrawRingOrbit(SKCanvas canvas, SKRect bounds, ParticleDef p, float time)
    {
        _strokePaint.StrokeWidth = 0.5f;

        for (int i = 0; i < p.Count; i++)
        {
            var radius = bounds.Width * (0.15f + i * 0.08f);
            var alpha = (byte)(p.Alpha * (0.5f + i * 0.15f));
            _strokePaint.Color = p.Color.WithAlpha(alpha);

            canvas.Save();
            canvas.Translate(bounds.MidX, bounds.MidY * 0.6f);
            canvas.RotateRadians(time * 0.2f * (i % 2 == 0 ? 1f : -1f));
            canvas.DrawCircle(0, 0, radius, _strokePaint);
            canvas.Restore();
        }
    }

    public static void Cleanup()
    {
        _fillPaint.Dispose();
        _strokePaint.Dispose();
    }
}
