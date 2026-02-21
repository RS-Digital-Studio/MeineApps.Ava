using System;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Feuerwerk-Partikel-System für Celebrations.
/// Struct-basierter Pool (max 400 Partikel), keine GC-Allokationen im Render-Loop.
/// Raketen steigen auf, explodieren in farbige Burst-Muster.
/// </summary>
public class FireworksRenderer
{
    // --- Konstanten ---
    private const int MaxParticles = 400;
    private const float Gravity = 120f;         // dp/s²
    private const float RocketSpeed = -350f;    // dp/s (nach oben)
    private const float BurstSpeed = 180f;      // dp/s (radial)
    private const float TrailAlpha = 0.4f;

    // --- Partikel-Pool (struct, GC-frei) ---
    private readonly FireworkParticle[] _particles = new FireworkParticle[MaxParticles];
    private int _particleCount;

    // --- Gecachte Paints ---
    private static readonly SKPaint _particlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _glowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f)
    };
    private static readonly SKPaint _trailPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };

    // --- Pseudo-Random ---
    private uint _rngState = 137;

    // --- Feuerwerk-Farben (warm, kräftig) ---
    private static readonly SKColor[] BurstColors =
    [
        new(0xFF, 0xD7, 0x00), // Gold
        new(0xFF, 0x45, 0x00), // Orange-Rot
        new(0x00, 0xFF, 0x7F), // Spring-Grün
        new(0x00, 0xBF, 0xFF), // Deep-Sky-Blau
        new(0xFF, 0x69, 0xB4), // Hot-Pink
        new(0xBF, 0x40, 0xFF), // Lila
        new(0xFF, 0xFF, 0xFF), // Weiß
        new(0xFF, 0xA5, 0x00), // Orange
    ];

    /// <summary>
    /// Ob aktuell Partikel aktiv sind.
    /// </summary>
    public bool IsActive => _particleCount > 0;

    /// <summary>
    /// Startet eine Feuerwerksrakete von unten nach oben.
    /// Bei Explosion: 20-30 Burst-Partikel in zufälliger Farbe.
    /// </summary>
    /// <param name="x">X-Position (0..boundsWidth)</param>
    /// <param name="boundsHeight">Höhe des Canvas (Rakete startet von unten)</param>
    /// <param name="burstY">Explosion-Höhe (von oben gemessen, 0.2-0.5 der Höhe empfohlen)</param>
    public void LaunchRocket(float x, float boundsHeight, float burstY = 0f)
    {
        if (burstY <= 0f) burstY = boundsHeight * (0.2f + NextRandom() * 0.25f);

        if (_particleCount >= MaxParticles) return;

        _particles[_particleCount++] = new FireworkParticle
        {
            Type = ParticleType.Rocket,
            X = x,
            Y = boundsHeight + 10f,
            VX = NextRandom(-15f, 15f),
            VY = RocketSpeed,
            Life = 1f,
            MaxLife = 1f,
            Color = new SKColor(0xFF, 0xCC, 0x44), // Heller Raketenstreifen
            Size = 3f,
            BurstY = burstY,
            BoundsHeight = boundsHeight
        };
    }

    /// <summary>
    /// Startet mehrere Raketen gleichzeitig (für große Celebrations).
    /// </summary>
    public void LaunchVolley(SKRect bounds, int count = 5)
    {
        for (int i = 0; i < count; i++)
        {
            float x = bounds.Left + bounds.Width * (0.15f + NextRandom() * 0.7f);
            float burstY = bounds.Height * (0.15f + NextRandom() * 0.3f);

            // Zeitversetzter Start via negative Life (Delay-Simulation)
            float delay = i * 0.25f;

            if (_particleCount >= MaxParticles) break;
            _particles[_particleCount++] = new FireworkParticle
            {
                Type = ParticleType.Rocket,
                X = x,
                Y = bounds.Bottom + 10f,
                VX = NextRandom(-15f, 15f),
                VY = RocketSpeed,
                Life = 1f + delay,
                MaxLife = 1f + delay,
                Color = new SKColor(0xFF, 0xCC, 0x44),
                Size = 3f,
                BurstY = burstY,
                BoundsHeight = bounds.Height
            };
        }
    }

    /// <summary>
    /// Update aller Partikel. Aufrufen mit deltaTime (typisch 0.05 bei 20fps).
    /// </summary>
    public void Update(float deltaTime)
    {
        int writeIdx = 0;

        for (int i = 0; i < _particleCount; i++)
        {
            ref var p = ref _particles[i];
            p.Life -= deltaTime;

            if (p.Life <= 0f) continue;

            // Delay-Phase: Noch nicht sichtbar
            if (p.Life > p.MaxLife - 0.001f && p.Type == ParticleType.Rocket && p.MaxLife > 1.1f)
            {
                // Warte noch (Delay)
                _particles[writeIdx++] = p;
                continue;
            }

            switch (p.Type)
            {
                case ParticleType.Rocket:
                    p.X += p.VX * deltaTime;
                    p.Y += p.VY * deltaTime;

                    // Explosion wenn Zielhöhe erreicht
                    if (p.Y <= p.BurstY)
                    {
                        SpawnBurst(p.X, p.Y);
                        continue; // Rakete entfernen
                    }
                    break;

                case ParticleType.Burst:
                    p.X += p.VX * deltaTime;
                    p.Y += p.VY * deltaTime;
                    p.VY += Gravity * deltaTime; // Schwerkraft
                    p.VX *= 0.98f; // Luftwiderstand
                    break;

                case ParticleType.Sparkle:
                    p.X += p.VX * deltaTime;
                    p.Y += p.VY * deltaTime;
                    p.VY += Gravity * 0.3f * deltaTime;
                    break;
            }

            _particles[writeIdx++] = p;
        }

        _particleCount = writeIdx;
    }

    /// <summary>
    /// Rendert alle aktiven Partikel auf den Canvas.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        for (int i = 0; i < _particleCount; i++)
        {
            ref readonly var p = ref _particles[i];

            // Delay-Phase überspringen
            float realLife = p.MaxLife > 1.1f ? p.Life - (p.MaxLife - 1f) : p.Life;
            if (realLife > 1f || realLife <= 0f) continue;

            float alpha = Math.Clamp(realLife / p.MaxLife, 0f, 1f);
            // Schnelleres Ausblenden am Ende
            if (realLife < 0.3f) alpha = realLife / 0.3f;

            switch (p.Type)
            {
                case ParticleType.Rocket:
                    DrawRocket(canvas, p, alpha);
                    break;
                case ParticleType.Burst:
                    DrawBurstParticle(canvas, p, alpha);
                    break;
                case ParticleType.Sparkle:
                    DrawSparkleParticle(canvas, p, alpha);
                    break;
            }
        }
    }

    /// <summary>
    /// Alle Partikel entfernen.
    /// </summary>
    public void Clear() => _particleCount = 0;

    // ═══════════════════════════════════════════════════════════════════
    // PRIVATE: Burst-Spawn
    // ═══════════════════════════════════════════════════════════════════

    private void SpawnBurst(float x, float y)
    {
        var burstColor = BurstColors[(int)(NextRandom() * BurstColors.Length) % BurstColors.Length];
        int burstCount = 20 + (int)(NextRandom() * 12); // 20-32 Partikel

        for (int i = 0; i < burstCount; i++)
        {
            if (_particleCount >= MaxParticles) break;

            float angle = MathF.PI * 2f * i / burstCount + NextRandom(-0.15f, 0.15f);
            float speed = BurstSpeed * (0.6f + NextRandom() * 0.5f);
            float life = 0.8f + NextRandom() * 0.6f;

            // Farbvariation: Hauptfarbe + leichte Abweichung
            var color = VaryColor(burstColor);

            _particles[_particleCount++] = new FireworkParticle
            {
                Type = ParticleType.Burst,
                X = x,
                Y = y,
                VX = MathF.Cos(angle) * speed,
                VY = MathF.Sin(angle) * speed,
                Life = life,
                MaxLife = life,
                Color = color,
                Size = 2f + NextRandom() * 2f
            };
        }

        // Einige Sparkle-Partikel für Glitzer-Effekt
        int sparkleCount = 6 + (int)(NextRandom() * 6);
        for (int i = 0; i < sparkleCount; i++)
        {
            if (_particleCount >= MaxParticles) break;

            float angle = NextRandom() * MathF.PI * 2f;
            float speed = BurstSpeed * 0.3f * (0.5f + NextRandom() * 0.5f);

            _particles[_particleCount++] = new FireworkParticle
            {
                Type = ParticleType.Sparkle,
                X = x,
                Y = y,
                VX = MathF.Cos(angle) * speed,
                VY = MathF.Sin(angle) * speed,
                Life = 1.2f + NextRandom() * 0.8f,
                MaxLife = 2f,
                Color = SKColors.White,
                Size = 1f + NextRandom() * 1.5f
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // PRIVATE: Draw-Methoden
    // ═══════════════════════════════════════════════════════════════════

    private static void DrawRocket(SKCanvas canvas, in FireworkParticle p, float alpha)
    {
        // Glow
        _glowPaint.Color = p.Color.WithAlpha((byte)(100 * alpha));
        canvas.DrawCircle(p.X, p.Y, 5f, _glowPaint);

        // Kern
        _particlePaint.Color = p.Color.WithAlpha((byte)(255 * alpha));
        canvas.DrawCircle(p.X, p.Y, p.Size, _particlePaint);

        // Trail (kurze Linie nach unten)
        _trailPaint.Color = p.Color.WithAlpha((byte)(80 * alpha));
        canvas.DrawLine(p.X, p.Y, p.X - p.VX * 0.02f, p.Y - p.VY * 0.02f, _trailPaint);
    }

    private static void DrawBurstParticle(SKCanvas canvas, in FireworkParticle p, float alpha)
    {
        float size = p.Size * (0.5f + alpha * 0.5f); // Schrumpft beim Verblassen

        // Glow
        _glowPaint.Color = p.Color.WithAlpha((byte)(60 * alpha));
        canvas.DrawCircle(p.X, p.Y, size + 2f, _glowPaint);

        // Kern
        _particlePaint.Color = p.Color.WithAlpha((byte)(220 * alpha));
        canvas.DrawCircle(p.X, p.Y, size, _particlePaint);
    }

    private static void DrawSparkleParticle(SKCanvas canvas, in FireworkParticle p, float alpha)
    {
        _particlePaint.Color = p.Color.WithAlpha((byte)(180 * alpha));
        canvas.DrawCircle(p.X, p.Y, p.Size * alpha, _particlePaint);
    }

    // ═══════════════════════════════════════════════════════════════════
    // PRIVATE: Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    private SKColor VaryColor(SKColor baseColor)
    {
        int r = Math.Clamp(baseColor.Red + (int)(NextRandom(-20f, 20f)), 0, 255);
        int g = Math.Clamp(baseColor.Green + (int)(NextRandom(-20f, 20f)), 0, 255);
        int b = Math.Clamp(baseColor.Blue + (int)(NextRandom(-20f, 20f)), 0, 255);
        return new SKColor((byte)r, (byte)g, (byte)b);
    }

    private float NextRandom()
    {
        _rngState = _rngState * 1103515245 + 12345;
        return (_rngState >> 16 & 0x7FFF) / (float)0x7FFF;
    }

    private float NextRandom(float min, float max) => min + NextRandom() * (max - min);

    // ═══════════════════════════════════════════════════════════════════
    // Partikel-Struct (GC-frei)
    // ═══════════════════════════════════════════════════════════════════

    private enum ParticleType : byte { Rocket, Burst, Sparkle }

    private struct FireworkParticle
    {
        public ParticleType Type;
        public float X, Y;
        public float VX, VY;
        public float Life, MaxLife;
        public SKColor Color;
        public float Size;
        public float BurstY;        // Nur Rocket: Zielhöhe für Explosion
        public float BoundsHeight;  // Nur Rocket: Canvas-Höhe
    }
}
