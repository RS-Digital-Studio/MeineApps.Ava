using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Leichtgewichtiges Partikel-System für visuelle Effekte.
/// Verwendet Struct-Array Pool (keine Heap-Allokationen pro Partikel).
/// </summary>
public class ParticleSystem : IDisposable
{
    private const int MAX_PARTICLES = 200;
    private const float GRAVITY = 120f;

    private readonly Particle[] _particles = new Particle[MAX_PARTICLES];
    private int _activeCount;
    private readonly Random _random = new();
    private readonly SKPaint _paint = new() { IsAntialias = false };

    /// <summary>Ob aktive Partikel vorhanden sind</summary>
    public bool HasActiveParticles => _activeCount > 0;

    /// <summary>
    /// Partikel emittieren
    /// </summary>
    /// <param name="x">X-Position (Welt-Koordinaten)</param>
    /// <param name="y">Y-Position (Welt-Koordinaten)</param>
    /// <param name="count">Anzahl Partikel</param>
    /// <param name="color">Farbe</param>
    /// <param name="speed">Maximal-Geschwindigkeit</param>
    /// <param name="lifetime">Lebensdauer in Sekunden</param>
    /// <param name="size">Partikelgröße</param>
    public void Emit(float x, float y, int count, SKColor color, float speed = 80f,
        float lifetime = 0.6f, float size = 2.5f)
    {
        for (int i = 0; i < count && _activeCount < MAX_PARTICLES; i++)
        {
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float spd = (float)_random.NextDouble() * speed;

            ref var p = ref _particles[_activeCount];
            p.X = x;
            p.Y = y;
            p.VelocityX = MathF.Cos(angle) * spd;
            p.VelocityY = MathF.Sin(angle) * spd - speed * 0.5f; // Leichter Aufwärts-Bias
            p.Color = color;
            p.Lifetime = lifetime;
            p.MaxLifetime = lifetime;
            p.Size = size * (0.5f + (float)_random.NextDouble());
            p.IsActive = true;

            _activeCount++;
        }
    }

    /// <summary>
    /// Alle Partikel aktualisieren
    /// </summary>
    public void Update(float deltaTime)
    {
        int writeIndex = 0;
        for (int i = 0; i < _activeCount; i++)
        {
            ref var p = ref _particles[i];
            p.Lifetime -= deltaTime;

            if (p.Lifetime <= 0)
                continue;

            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.VelocityY += GRAVITY * deltaTime; // Schwerkraft

            // Kompaktieren: aktive Partikel nach vorne schieben
            if (writeIndex != i)
                _particles[writeIndex] = _particles[i];
            writeIndex++;
        }
        _activeCount = writeIndex;
    }

    /// <summary>
    /// Partikel rendern (mit Viewport-Transformation)
    /// </summary>
    public void Render(SKCanvas canvas, float scale, float offsetX, float offsetY)
    {
        for (int i = 0; i < _activeCount; i++)
        {
            ref var p = ref _particles[i];
            float alpha = Math.Clamp(p.Lifetime / p.MaxLifetime, 0f, 1f);
            float screenX = p.X * scale + offsetX;
            float screenY = p.Y * scale + offsetY;
            float screenSize = p.Size * scale;

            _paint.Color = p.Color.WithAlpha((byte)(alpha * 255));
            canvas.DrawRect(screenX - screenSize / 2, screenY - screenSize / 2,
                screenSize, screenSize, _paint);
        }
    }

    /// <summary>Alle Partikel entfernen</summary>
    public void Clear()
    {
        _activeCount = 0;
    }

    public void Dispose()
    {
        _paint.Dispose();
    }

    /// <summary>
    /// Struct-basierter Partikel (keine Heap-Allokation)
    /// </summary>
    private struct Particle
    {
        public float X, Y;
        public float VelocityX, VelocityY;
        public SKColor Color;
        public float Lifetime;
        public float MaxLifetime;
        public float Size;
        public bool IsActive;
    }
}

/// <summary>
/// Vordefinierte Partikel-Farben für verschiedene Effekte
/// </summary>
public static class ParticleColors
{
    // Block-Zerstörung (braun/orange)
    public static readonly SKColor BlockDestroy = new(139, 90, 43);
    public static readonly SKColor BlockDestroyLight = new(184, 134, 68);

    // Enemy-Kill (rot/orange)
    public static readonly SKColor EnemyDeath = new(255, 80, 40);
    public static readonly SKColor EnemyDeathLight = new(255, 160, 60);

    // PowerUp-Collect (gold)
    public static readonly SKColor PowerUpCollect = new(255, 215, 0);
    public static readonly SKColor PowerUpCollectLight = new(255, 255, 100);

    // Exit-Reveal (grün)
    public static readonly SKColor ExitReveal = new(0, 200, 80);
    public static readonly SKColor ExitRevealLight = new(100, 255, 150);

    // Explosion (orange/rot)
    public static readonly SKColor Explosion = new(255, 140, 20);
    public static readonly SKColor ExplosionLight = new(255, 220, 80);
}
