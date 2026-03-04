using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Leichtgewichtiges Partikel-System für visuelle Effekte.
/// Verwendet Struct-Array Pool (keine Heap-Allokationen pro Partikel).
/// Unterstützt verschiedene Formen (Rechteck, Kreis, Funke, Glut) und Glow-Effekte.
/// </summary>
public sealed class ParticleSystem : IDisposable
{
    private const int MAX_PARTICLES = 300;
    private const float GRAVITY = 120f;

    private readonly Particle[] _particles = new Particle[MAX_PARTICLES];
    private int _activeCount;
    private readonly Random _random = new();
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private readonly SKPaint _glowPaint = new() { IsAntialias = true };
    private readonly SKMaskFilter _particleGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4);

    /// <summary>Ob aktive Partikel vorhanden sind</summary>
    public bool HasActiveParticles => _activeCount > 0;

    /// <summary>Wenn false, werden Emit-Aufrufe ignoriert (ReducedEffects)</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Partikel emittieren (Basis-Methode, Standard-Rechtecke)
    /// </summary>
    public void Emit(float x, float y, int count, SKColor color, float speed = 80f,
        float lifetime = 0.6f, float size = 2.5f)
    {
        if (!Enabled) return;
        EmitShaped(x, y, count, color, ParticleShape.Rectangle, speed, lifetime, size);
    }

    /// <summary>
    /// Partikel mit bestimmter Form emittieren
    /// </summary>
    public void EmitShaped(float x, float y, int count, SKColor color,
        ParticleShape shape, float speed = 80f, float lifetime = 0.6f, float size = 2.5f,
        bool hasGlow = false, float airResistance = 0f)
    {
        if (!Enabled) return;
        for (int i = 0; i < count && _activeCount < MAX_PARTICLES; i++)
        {
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float spd = (float)_random.NextDouble() * speed;

            ref var p = ref _particles[_activeCount];
            p.X = x;
            p.Y = y;
            p.VelocityX = MathF.Cos(angle) * spd;
            p.VelocityY = MathF.Sin(angle) * spd - speed * 0.5f; // Aufwärts-Bias
            p.Color = color;
            p.Lifetime = lifetime * (0.7f + (float)_random.NextDouble() * 0.6f); // Lebensdauer variiert
            p.MaxLifetime = p.Lifetime;
            p.Size = size * (0.5f + (float)_random.NextDouble());
            p.Shape = shape;
            p.HasGlow = hasGlow;
            p.Rotation = (float)(_random.NextDouble() * 360f);
            p.RotationSpeed = ((float)_random.NextDouble() - 0.5f) * 400f; // -200→+200 °/s
            p.AirResistance = airResistance;

            _activeCount++;
        }
    }

    /// <summary>
    /// Explosions-Funken emittieren: Schnelle, leuchtende Streifen die nach außen fliegen
    /// </summary>
    public void EmitExplosionSparks(float x, float y, int count, SKColor color, float speed = 150f)
    {
        EmitShaped(x, y, count, color, ParticleShape.Spark,
            speed: speed, lifetime: 0.4f, size: 3f, hasGlow: true, airResistance: 0.5f);
    }

    /// <summary>
    /// Glut-Partikel emittieren: Langsam schwebende, glühende Punkte
    /// </summary>
    public void EmitEmbers(float x, float y, int count, SKColor color)
    {
        EmitShaped(x, y, count, color, ParticleShape.Ember,
            speed: 40f, lifetime: 0.8f, size: 2f, hasGlow: true, airResistance: 2f);
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

            // Luftwiderstand bremst Geschwindigkeit
            if (p.AirResistance > 0)
            {
                float drag = 1f - p.AirResistance * deltaTime;
                drag = Math.Max(0.5f, drag);
                p.VelocityX *= drag;
                p.VelocityY *= drag;
            }

            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;

            // Schwerkraft (Glut steigt auf statt zu fallen)
            if (p.Shape == ParticleShape.Ember)
                p.VelocityY -= GRAVITY * 0.3f * deltaTime; // Leichter Aufstieg
            else
                p.VelocityY += GRAVITY * deltaTime;

            p.Rotation += p.RotationSpeed * deltaTime;

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

            switch (p.Shape)
            {
                case ParticleShape.Circle:
                    RenderCircle(canvas, ref p, screenX, screenY, screenSize, alpha);
                    break;

                case ParticleShape.Spark:
                    RenderSpark(canvas, ref p, screenX, screenY, screenSize, alpha, scale);
                    break;

                case ParticleShape.Ember:
                    RenderEmber(canvas, ref p, screenX, screenY, screenSize, alpha);
                    break;

                default: // Rectangle
                    _paint.Color = p.Color.WithAlpha((byte)(alpha * 255));
                    canvas.DrawRect(screenX - screenSize / 2, screenY - screenSize / 2,
                        screenSize, screenSize, _paint);
                    break;
            }
        }
    }

    /// <summary>Kreis-Partikel mit optionalem Glow</summary>
    private void RenderCircle(SKCanvas canvas, ref Particle p,
        float x, float y, float size, float alpha)
    {
        // Glow-Halo (größer, transparenter)
        if (p.HasGlow)
        {
            _glowPaint.Color = p.Color.WithAlpha((byte)(alpha * 80));
            _glowPaint.MaskFilter = _particleGlow;
            canvas.DrawCircle(x, y, size * 1.5f, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        _paint.Color = p.Color.WithAlpha((byte)(alpha * 255));
        canvas.DrawCircle(x, y, size / 2f, _paint);
    }

    /// <summary>Funken-Partikel: Elongierter Streifen in Flugrichtung</summary>
    private void RenderSpark(SKCanvas canvas, ref Particle p,
        float x, float y, float size, float alpha, float scale)
    {
        float speed = MathF.Sqrt(p.VelocityX * p.VelocityX + p.VelocityY * p.VelocityY);
        if (speed < 1f) speed = 1f;

        // Richtungsvektor normalisiert
        float dirX = p.VelocityX / speed;
        float dirY = p.VelocityY / speed;

        // Funke ist länglich in Flugrichtung (Länge proportional zur Geschwindigkeit)
        float length = Math.Min(size * 3f, speed * scale * 0.03f + size);
        float halfW = size * 0.3f; // Schmal

        // Glow-Effekt
        if (p.HasGlow)
        {
            _glowPaint.Color = p.Color.WithAlpha((byte)(alpha * 100));
            _glowPaint.MaskFilter = _particleGlow;
            canvas.DrawCircle(x, y, size, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Funke als Linie zeichnen
        _paint.Color = p.Color.WithAlpha((byte)(alpha * 255));
        _paint.Style = SKPaintStyle.Stroke;
        _paint.StrokeWidth = halfW * 2f;
        _paint.StrokeCap = SKStrokeCap.Round;
        canvas.DrawLine(
            x - dirX * length / 2f, y - dirY * length / 2f,
            x + dirX * length / 2f, y + dirY * length / 2f,
            _paint);
        _paint.Style = SKPaintStyle.Fill;

        // Heller Kopf
        _paint.Color = SKColors.White.WithAlpha((byte)(alpha * 200));
        canvas.DrawCircle(x + dirX * length / 2f, y + dirY * length / 2f, halfW, _paint);
    }

    /// <summary>Glut-Partikel: Kleiner glühender Punkt mit Pulsation</summary>
    private void RenderEmber(SKCanvas canvas, ref Particle p,
        float x, float y, float size, float alpha)
    {
        // Pulsation basierend auf Rotation (als Timer missbraucht)
        float pulse = MathF.Sin(p.Rotation * 0.1f) * 0.3f + 0.7f;
        float glowSize = size * (1.5f + pulse * 0.5f);

        // Großer weicher Glow
        _glowPaint.Color = p.Color.WithAlpha((byte)(alpha * 60 * pulse));
        _glowPaint.MaskFilter = _particleGlow;
        canvas.DrawCircle(x, y, glowSize, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Heller Kern
        var coreColor = new SKColor(
            (byte)Math.Min(255, p.Color.Red + 60),
            (byte)Math.Min(255, p.Color.Green + 40),
            (byte)Math.Min(255, p.Color.Blue + 20));
        _paint.Color = coreColor.WithAlpha((byte)(alpha * 255 * pulse));
        canvas.DrawCircle(x, y, size * 0.4f, _paint);
    }

    /// <summary>Alle Partikel entfernen</summary>
    public void Clear()
    {
        _activeCount = 0;
    }

    public void Dispose()
    {
        _paint.Dispose();
        _glowPaint.Dispose();
        _particleGlow.Dispose();
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
        public ParticleShape Shape;
        public bool HasGlow;
        public float Rotation;
        public float RotationSpeed;
        public float AirResistance;
    }
}

/// <summary>
/// Form der Partikel
/// </summary>
public enum ParticleShape
{
    /// <summary>Standard-Rechteck (Legacy)</summary>
    Rectangle,
    /// <summary>Runder Partikel mit optionalem Glow</summary>
    Circle,
    /// <summary>Elongierter Funke in Flugrichtung</summary>
    Spark,
    /// <summary>Glühende Glut die langsam aufsteigt</summary>
    Ember
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

    // Explosions-Funken (hell orange-weiß)
    public static readonly SKColor ExplosionSpark = new(255, 200, 100);

    // Glut (dunkel orange-rot)
    public static readonly SKColor ExplosionEmber = new(255, 100, 30);
    public static readonly SKColor ExplosionEmberBright = new(255, 180, 60);
}
