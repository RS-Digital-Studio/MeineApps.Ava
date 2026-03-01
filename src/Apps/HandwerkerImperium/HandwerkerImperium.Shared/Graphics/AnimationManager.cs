using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Manages UI particle effects (coins, confetti).
/// Max 100 particles to prevent memory issues.
/// </summary>
public class AnimationManager
{
    private const int MaxParticles = 100;

    private readonly Particle[] _particles = new Particle[MaxParticles];
    private int _particleCount;
    private readonly Random _random = new();
    private readonly object _lock = new();

    /// <summary>
    /// Whether there are active particles to render.
    /// </summary>
    public bool HasActiveParticles
    {
        get
        {
            lock (_lock) return _particleCount > 0;
        }
    }

    /// <summary>
    /// Adds a coin particle that flies upward and fades out.
    /// </summary>
    /// <param name="x">Start X position.</param>
    /// <param name="y">Start Y position.</param>
    public void AddCoinParticle(float x, float y)
    {
        lock (_lock)
        {
            if (_particleCount >= MaxParticles) return;

            // Leichte horizontale Streuung
            float vx = (_random.NextSingle() - 0.5f) * 30f;
            float vy = -60f - _random.NextSingle() * 40f; // Nach oben

            _particles[_particleCount++] = new Particle
            {
                X = x,
                Y = y,
                VelocityX = vx,
                VelocityY = vy,
                Color = new SKColor(0xFF, 0xD7, 0x00), // Gold
                Alpha = 1.0f,
                Lifetime = 1.2f,
                RemainingLife = 1.2f,
                Size = 4f + _random.NextSingle() * 2f,
                Type = ParticleType.Coin
            };
        }
    }

    /// <summary>
    /// Fügt einen Arbeits-Partikel mit konfigurierbarer Farbe hinzu (kleiner als Münzen).
    /// </summary>
    public void AddWorkParticle(float x, float y, SKColor color)
    {
        lock (_lock)
        {
            if (_particleCount >= MaxParticles) return;

            float vx = (_random.NextSingle() - 0.5f) * 40f;
            float vy = -30f - _random.NextSingle() * 30f;

            _particles[_particleCount++] = new Particle
            {
                X = x,
                Y = y,
                VelocityX = vx,
                VelocityY = vy,
                Color = color,
                Alpha = 1.0f,
                Lifetime = 0.8f,
                RemainingLife = 0.8f,
                Size = 2f + _random.NextSingle() * 2f,
                Type = ParticleType.Coin // Verhaelt sich wie Coin (leichte Gravity)
            };
        }
    }

    /// <summary>
    /// Adds a burst of colored confetti particles.
    /// </summary>
    /// <param name="cx">Center X position of the burst.</param>
    /// <param name="cy">Center Y position of the burst.</param>
    public void AddLevelUpConfetti(float cx, float cy)
    {
        lock (_lock)
        {
            int count = Math.Min(20, MaxParticles - _particleCount);

            for (int i = 0; i < count; i++)
            {
                // Zufaellige Richtung (radial vom Zentrum)
                float angle = _random.NextSingle() * MathF.Tau;
                float speed = 80f + _random.NextSingle() * 120f;
                float vx = MathF.Cos(angle) * speed;
                float vy = MathF.Sin(angle) * speed;

                // Zufaellige Festfarbe
                var color = ConfettiColors[_random.Next(ConfettiColors.Length)];

                _particles[_particleCount++] = new Particle
                {
                    X = cx + (_random.NextSingle() - 0.5f) * 10f,
                    Y = cy + (_random.NextSingle() - 0.5f) * 10f,
                    VelocityX = vx,
                    VelocityY = vy,
                    Color = color,
                    Alpha = 1.0f,
                    Lifetime = 1.5f,
                    RemainingLife = 1.5f,
                    Size = 3f + _random.NextSingle() * 3f,
                    Type = ParticleType.Confetti
                };
            }
        }
    }

    /// <summary>
    /// Updates all active particles. Call once per frame.
    /// </summary>
    /// <param name="deltaSeconds">Time since last update in seconds.</param>
    public void Update(double deltaSeconds)
    {
        float dt = (float)deltaSeconds;

        lock (_lock)
        {
            int aliveCount = 0;
            for (int i = 0; i < _particleCount; i++)
            {
                var p = _particles[i];

                // Position aktualisieren
                p.X += p.VelocityX * dt;
                p.Y += p.VelocityY * dt;

                // Schwerkraft fuer Konfetti
                if (p.Type == ParticleType.Confetti)
                {
                    p.VelocityY += 120f * dt; // Gravity
                    p.VelocityX *= 0.98f;     // Air resistance
                }

                // Coin: leichte Verlangsamung
                if (p.Type == ParticleType.Coin)
                {
                    p.VelocityY += 40f * dt;  // Leichte Gravity
                }

                // Lebenszeit reduzieren
                p.RemainingLife -= dt;

                // Alpha basierend auf verbleibender Lebenszeit
                if (p.Lifetime > 0)
                {
                    p.Alpha = Math.Clamp(p.RemainingLife / p.Lifetime, 0f, 1f);
                }

                // Nur lebende Partikel behalten
                if (p.RemainingLife > 0 && p.Alpha > 0)
                {
                    _particles[aliveCount++] = p;
                }
            }
            _particleCount = aliveCount;
        }
    }

    /// <summary>
    /// Renders all active particles onto the canvas.
    /// </summary>
    /// <param name="canvas">SkiaSharp canvas to draw on.</param>
    public void Render(SKCanvas canvas)
    {
        lock (_lock)
        {
            if (_particleCount == 0) return;

            using var paint = new SKPaint { IsAntialias = false };

            for (int i = 0; i < _particleCount; i++)
            {
                var p = _particles[i];
                byte alpha = (byte)(p.Alpha * 255);
                paint.Color = p.Color.WithAlpha(alpha);

                switch (p.Type)
                {
                    case ParticleType.Coin:
                        // Muenze: Kreis mit hellerem Innenpunkt
                        canvas.DrawCircle(p.X, p.Y, p.Size, paint);
                        paint.Color = new SKColor(0xFF, 0xF0, 0x70, alpha);
                        canvas.DrawCircle(p.X, p.Y, p.Size * 0.5f, paint);
                        break;

                    case ParticleType.Confetti:
                        // Konfetti: Kleines Rechteck
                        canvas.DrawRect(
                            p.X - p.Size / 2,
                            p.Y - p.Size / 2,
                            p.Size,
                            p.Size * 0.6f,
                            paint);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Removes all active particles.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _particleCount = 0;
        }
    }

    // Konfetti-Farbpalette
    private static readonly SKColor[] ConfettiColors =
    [
        new SKColor(0xF4, 0x43, 0x36), // Red
        new SKColor(0x4C, 0xAF, 0x50), // Green
        new SKColor(0x21, 0x96, 0xF3), // Blue
        new SKColor(0xFF, 0xC1, 0x07), // Yellow
        new SKColor(0x9C, 0x27, 0xB0), // Purple
        new SKColor(0xFF, 0x57, 0x22), // Deep orange
        new SKColor(0x00, 0xBC, 0xD4), // Cyan
        new SKColor(0xE9, 0x1E, 0x63)  // Pink
    ];

    private enum ParticleType
    {
        Coin,
        Confetti
    }

    private struct Particle
    {
        public float X;
        public float Y;
        public float VelocityX;
        public float VelocityY;
        public SKColor Color;
        public float Alpha;
        public float Lifetime;
        public float RemainingLife;
        public float Size;
        public ParticleType Type;
    }
}
