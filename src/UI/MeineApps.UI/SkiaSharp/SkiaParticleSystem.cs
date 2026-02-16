using SkiaSharp;

namespace MeineApps.UI.SkiaSharp;

/// <summary>
/// Struct-basiertes Partikelsystem für SkiaSharp-Visualisierungen.
/// Keine Heap-Allokationen - optimal für 60fps-Rendering.
/// </summary>
public struct SkiaParticle
{
    public float X, Y;
    public float VelocityX, VelocityY;
    public float Life, MaxLife;
    public float Size;
    public SKColor Color;
    public float Rotation, RotationSpeed;
    public float Gravity;

    /// <summary>
    /// Aktueller Alpha-Wert basierend auf verbleibender Lebenszeit (1.0 → 0.0).
    /// </summary>
    public readonly float Alpha => MaxLife > 0 ? Math.Clamp(Life / MaxLife, 0f, 1f) : 0f;

    /// <summary>
    /// True wenn der Partikel noch lebt.
    /// </summary>
    public readonly bool IsAlive => Life > 0;
}

/// <summary>
/// Verwaltung und Rendering einer Partikel-Liste.
/// Maximal-Limit verhindert Memory-Probleme.
/// </summary>
public class SkiaParticleManager
{
    private readonly List<SkiaParticle> _particles = new();
    private readonly int _maxParticles;
    private readonly Random _random = new();

    // Gecachte Paints (werden wiederverwendet)
    private static readonly SKPaint _fillPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint _glowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f)
    };

    public SkiaParticleManager(int maxParticles = 50)
    {
        _maxParticles = maxParticles;
    }

    /// <summary>
    /// Gibt zurück ob aktive Partikel vorhanden sind.
    /// </summary>
    public bool HasActiveParticles => _particles.Count > 0;

    /// <summary>
    /// Aktuelle Anzahl lebender Partikel.
    /// </summary>
    public int Count => _particles.Count;

    /// <summary>
    /// Fügt einen Partikel hinzu (älteste werden entfernt wenn Limit erreicht).
    /// </summary>
    public void Add(SkiaParticle particle)
    {
        if (_particles.Count >= _maxParticles)
            _particles.RemoveAt(0);
        _particles.Add(particle);
    }

    /// <summary>
    /// Fügt mehrere Partikel auf einmal hinzu.
    /// </summary>
    public void AddBurst(int count, Func<Random, SkiaParticle> factory)
    {
        for (int i = 0; i < count; i++)
        {
            if (_particles.Count >= _maxParticles)
                _particles.RemoveAt(0);
            _particles.Add(factory(_random));
        }
    }

    /// <summary>
    /// Aktualisiert alle Partikel (Position, Lebenszeit, Schwerkraft).
    /// </summary>
    public void Update(float deltaTime)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.VelocityY += p.Gravity * deltaTime;
            p.Rotation += p.RotationSpeed * deltaTime;
            p.Life -= deltaTime;

            if (p.Life <= 0)
            {
                _particles.RemoveAt(i);
                continue;
            }

            _particles[i] = p;
        }
    }

    /// <summary>
    /// Zeichnet alle aktiven Partikel als Kreise mit Opacity-Fade.
    /// </summary>
    public void Draw(SKCanvas canvas, bool withGlow = false)
    {
        foreach (var p in _particles)
        {
            byte alpha = (byte)(p.Alpha * 255);
            if (alpha == 0) continue;

            var color = p.Color.WithAlpha(alpha);

            // Optional: Glow-Hintergrund
            if (withGlow)
            {
                _glowPaint.Color = color.WithAlpha((byte)(alpha * 0.4f));
                canvas.DrawCircle(p.X, p.Y, p.Size * 2f, _glowPaint);
            }

            _fillPaint.Color = color;
            canvas.DrawCircle(p.X, p.Y, p.Size, _fillPaint);
        }
    }

    /// <summary>
    /// Zeichnet Partikel als Rechtecke (für Konfetti-Effekt).
    /// </summary>
    public void DrawAsConfetti(SKCanvas canvas)
    {
        foreach (var p in _particles)
        {
            byte alpha = (byte)(p.Alpha * 255);
            if (alpha == 0) continue;

            _fillPaint.Color = p.Color.WithAlpha(alpha);

            canvas.Save();
            canvas.Translate(p.X, p.Y);
            canvas.RotateDegrees(p.Rotation);
            canvas.DrawRect(-p.Size, -p.Size * 0.6f, p.Size * 2, p.Size * 1.2f, _fillPaint);
            canvas.Restore();
        }
    }

    /// <summary>
    /// Entfernt alle Partikel.
    /// </summary>
    public void Clear() => _particles.Clear();
}

/// <summary>
/// Vorgefertigte Partikel-Presets für häufige Effekte.
/// </summary>
public static class SkiaParticlePresets
{
    /// <summary>
    /// Konfetti-Partikel für Celebrations (verschiedene Farben, Schwerkraft, Rotation).
    /// </summary>
    public static SkiaParticle CreateConfetti(Random rng, float x, float y)
    {
        var colors = new[]
        {
            new SKColor(0xEF, 0x44, 0x44), // Rot
            new SKColor(0xF5, 0x9E, 0x0B), // Amber
            new SKColor(0x22, 0xC5, 0x5E), // Grün
            new SKColor(0x3B, 0x82, 0xF6), // Blau
            new SKColor(0xA7, 0x8B, 0xFA), // Violett
            new SKColor(0x22, 0xD3, 0xEE), // Cyan
            new SKColor(0xEC, 0x48, 0x99), // Pink
            new SKColor(0xFF, 0xD7, 0x00), // Gold
        };

        float angle = (float)(rng.NextDouble() * Math.PI * 2);
        float speed = 80f + (float)rng.NextDouble() * 160f;

        return new SkiaParticle
        {
            X = x,
            Y = y,
            VelocityX = MathF.Cos(angle) * speed,
            VelocityY = MathF.Sin(angle) * speed - 100f, // Nach oben starten
            Life = 1.2f + (float)rng.NextDouble() * 0.8f,
            MaxLife = 2f,
            Size = 3f + (float)rng.NextDouble() * 3f,
            Color = colors[rng.Next(colors.Length)],
            Rotation = (float)rng.NextDouble() * 360f,
            RotationSpeed = -180f + (float)rng.NextDouble() * 360f,
            Gravity = 200f
        };
    }

    /// <summary>
    /// Funkeln-Partikel (kleine leuchtende Punkte, schweben nach oben).
    /// </summary>
    public static SkiaParticle CreateSparkle(Random rng, float x, float y, SKColor baseColor)
    {
        return new SkiaParticle
        {
            X = x + (float)(rng.NextDouble() - 0.5) * 20f,
            Y = y + (float)(rng.NextDouble() - 0.5) * 20f,
            VelocityX = (float)(rng.NextDouble() - 0.5) * 30f,
            VelocityY = -20f - (float)rng.NextDouble() * 40f,
            Life = 0.5f + (float)rng.NextDouble() * 0.5f,
            MaxLife = 1f,
            Size = 1.5f + (float)rng.NextDouble() * 2f,
            Color = baseColor,
            Gravity = -10f // Leicht nach oben
        };
    }

    /// <summary>
    /// Wassertropfen-Partikel (fallen nach unten, klein und blau).
    /// </summary>
    public static SkiaParticle CreateWaterDrop(Random rng, float x, float y, SKColor? waterColor = null)
    {
        var color = waterColor ?? new SKColor(0x22, 0xD3, 0xEE); // Cyan
        return new SkiaParticle
        {
            X = x + (float)(rng.NextDouble() - 0.5) * 8f,
            Y = y,
            VelocityX = (float)(rng.NextDouble() - 0.5) * 15f,
            VelocityY = 20f + (float)rng.NextDouble() * 40f,
            Life = 0.8f + (float)rng.NextDouble() * 0.4f,
            MaxLife = 1.2f,
            Size = 1.5f + (float)rng.NextDouble() * 1.5f,
            Color = color,
            Gravity = 150f
        };
    }

    /// <summary>
    /// Glow-Partikel (langsam, groß, transparent - für Aura-Effekte).
    /// </summary>
    public static SkiaParticle CreateGlow(Random rng, float x, float y, SKColor glowColor)
    {
        float angle = (float)(rng.NextDouble() * Math.PI * 2);
        float speed = 5f + (float)rng.NextDouble() * 15f;

        return new SkiaParticle
        {
            X = x,
            Y = y,
            VelocityX = MathF.Cos(angle) * speed,
            VelocityY = MathF.Sin(angle) * speed,
            Life = 0.6f + (float)rng.NextDouble() * 0.6f,
            MaxLife = 1.2f,
            Size = 4f + (float)rng.NextDouble() * 6f,
            Color = glowColor.WithAlpha(180),
            Gravity = 0f
        };
    }

    /// <summary>
    /// Münzen-Partikel (Gold, fallen oder steigen, Glanz-Effekt).
    /// </summary>
    public static SkiaParticle CreateCoin(Random rng, float x, float y, bool rising = false)
    {
        return new SkiaParticle
        {
            X = x + (float)(rng.NextDouble() - 0.5) * 40f,
            Y = y,
            VelocityX = (float)(rng.NextDouble() - 0.5) * 50f,
            VelocityY = rising ? (-80f - (float)rng.NextDouble() * 60f) : (30f + (float)rng.NextDouble() * 40f),
            Life = 1.0f + (float)rng.NextDouble() * 0.5f,
            MaxLife = 1.5f,
            Size = 4f + (float)rng.NextDouble() * 3f,
            Color = new SKColor(0xFF, 0xD7, 0x00), // Gold
            Rotation = (float)rng.NextDouble() * 360f,
            RotationSpeed = 90f + (float)rng.NextDouble() * 180f,
            Gravity = rising ? 50f : 120f
        };
    }

    /// <summary>
    /// Feuerwerk-Burst (viele Partikel radial von einem Punkt).
    /// </summary>
    public static SkiaParticle CreateFirework(Random rng, float x, float y, SKColor color)
    {
        float angle = (float)(rng.NextDouble() * Math.PI * 2);
        float speed = 100f + (float)rng.NextDouble() * 200f;

        return new SkiaParticle
        {
            X = x,
            Y = y,
            VelocityX = MathF.Cos(angle) * speed,
            VelocityY = MathF.Sin(angle) * speed,
            Life = 0.4f + (float)rng.NextDouble() * 0.6f,
            MaxLife = 1f,
            Size = 2f + (float)rng.NextDouble() * 2f,
            Color = color,
            Gravity = 80f
        };
    }
}
