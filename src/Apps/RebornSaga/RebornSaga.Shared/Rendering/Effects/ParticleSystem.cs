namespace RebornSaga.Rendering.Effects;

using SkiaSharp;
using System;

/// <summary>
/// Partikel-Daten als Struct (kein GC-Druck, alles im Array auf dem Stack/Heap als Werttyp).
/// </summary>
public struct Particle
{
    public float X, Y;
    public float VelocityX, VelocityY;
    public float Life, MaxLife;       // Verbleibende / Maximale Lebenszeit in Sekunden
    public float Size;
    public uint Color;                // SKColor als uint (RRGGBB)
    public byte Shape;                // 0=Kreis, 1=Stern, 2=Quadrat, 3=Linie
    public float Rotation;
    public float RotationSpeed;
    public float Alpha;
    public float GravityY;            // Schwerkraft pro Partikel (aus Config beim Emit gesetzt)
    public float InitialSize;         // Anfangsgröße (für ShrinkOut)
    public bool FadeOut;              // Alpha verringern über Lebenszeit
    public bool ShrinkOut;            // Größe verringern über Lebenszeit
    public bool IsActive;
}

/// <summary>
/// Konfig für Partikel-Emissionen. Wiederverwendbar für verschiedene Effekte.
/// </summary>
public class ParticleConfig
{
    public float MinSpeed { get; set; } = 10f;
    public float MaxSpeed { get; set; } = 50f;
    public float MinLife { get; set; } = 0.5f;
    public float MaxLife { get; set; } = 2f;
    public float MinSize { get; set; } = 2f;
    public float MaxSize { get; set; } = 6f;
    public float Gravity { get; set; } = 0f;
    public float SpreadAngle { get; set; } = 360f;  // Grad
    public float BaseAngle { get; set; } = 0f;       // Grad
    public byte Shape { get; set; } = 0;
    public SKColor Color { get; set; } = SKColors.White;
    public bool FadeOut { get; set; } = true;
    public bool ShrinkOut { get; set; } = false;
    public float RotationSpeed { get; set; } = 0f;
}

/// <summary>
/// Struct-basiertes Partikel-System. Fester Pool, keine Allokationen zur Laufzeit.
/// Inspiriert vom BomberBlast-Pattern, erweitert um Configs und Presets.
/// </summary>
public class ParticleSystem : IDisposable
{
    private readonly Particle[] _particles;
    private readonly int _maxParticles;
    private int _nextIndex;
    private readonly Random _random = new();
    private float _emitAccumulator; // Akkumulator für fraktionale Partikel-Emission

    // Gepoolte Paints (Instanz-Felder, werden in Dispose() aufgeräumt)
    private readonly SKPaint _particlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private readonly SKPath _starPath = new();
    private bool _disposed;

    // --- Vordefinierte Presets ---

    /// <summary>Blaue Funken für magische Effekte (Stern-Form).</summary>
    public static readonly ParticleConfig MagicSparkle = new()
    {
        MinSpeed = 5f, MaxSpeed = 25f, MinLife = 1f, MaxLife = 3f,
        MinSize = 1.5f, MaxSize = 4f, SpreadAngle = 360f,
        Color = new SKColor(0x58, 0xA6, 0xFF), FadeOut = true, Shape = 1
    };

    /// <summary>Goldene Partikel die nach oben steigen (Level-Up).</summary>
    public static readonly ParticleConfig LevelUpGlow = new()
    {
        MinSpeed = 30f, MaxSpeed = 80f, MinLife = 0.8f, MaxLife = 1.5f,
        MinSize = 2f, MaxSize = 5f, SpreadAngle = 60f, BaseAngle = 270f,
        Color = new SKColor(0xF3, 0x9C, 0x12), FadeOut = true, ShrinkOut = true, Shape = 0
    };

    /// <summary>Schnelle blaue Linien für System-Glitch-Effekte.</summary>
    public static readonly ParticleConfig SystemGlitch = new()
    {
        MinSpeed = 50f, MaxSpeed = 150f, MinLife = 0.1f, MaxLife = 0.3f,
        MinSize = 1f, MaxSize = 3f, SpreadAngle = 360f,
        Color = new SKColor(0x4A, 0x90, 0xD9), FadeOut = true, Shape = 3
    };

    /// <summary>Rote Tropfen mit Schwerkraft (Schadens-Effekt).</summary>
    public static readonly ParticleConfig BloodSplatter = new()
    {
        MinSpeed = 20f, MaxSpeed = 60f, MinLife = 0.3f, MaxLife = 0.8f,
        MinSize = 2f, MaxSize = 5f, SpreadAngle = 120f, Gravity = 200f,
        Color = new SKColor(0xE7, 0x4C, 0x3C), FadeOut = true, Shape = 0
    };

    /// <summary>Langsam schwebende blaue Punkte (Ambient-Hintergrund).</summary>
    public static readonly ParticleConfig AmbientFloat = new()
    {
        MinSpeed = 3f, MaxSpeed = 10f, MinLife = 3f, MaxLife = 6f,
        MinSize = 1f, MaxSize = 3f, SpreadAngle = 360f,
        Color = new SKColor(0x4A, 0x90, 0xD9, 0x80), FadeOut = true, Shape = 0
    };

    // --- Element-Presets (Kampf-Effekte) ---

    /// <summary>Orangene Feuer-Partikel die nach oben steigen (Feuer-Element).</summary>
    public static readonly ParticleConfig FireBurst = new()
    {
        MinSpeed = 80f, MaxSpeed = 160f, MinLife = 0.3f, MaxLife = 0.6f,
        MinSize = 3f, MaxSize = 6f, SpreadAngle = 120f, Gravity = -30f,
        Color = new SKColor(0xFF, 0x6B, 0x00), FadeOut = true, ShrinkOut = true, Shape = 0
    };

    /// <summary>Blau-weiße Eissplitter mit Schwerkraft (Eis-Element).</summary>
    public static readonly ParticleConfig IceShard = new()
    {
        MinSpeed = 50f, MaxSpeed = 110f, MinLife = 0.5f, MaxLife = 0.8f,
        MinSize = 2f, MaxSize = 4f, SpreadAngle = 90f, Gravity = 40f,
        Color = new SKColor(0x00, 0xBF, 0xFF), FadeOut = true, ShrinkOut = true, Shape = 2
    };

    /// <summary>Gelbe Blitz-Linien (Blitz-Element).</summary>
    public static readonly ParticleConfig LightningStrike = new()
    {
        MinSpeed = 150f, MaxSpeed = 250f, MinLife = 0.1f, MaxLife = 0.3f,
        MinSize = 2f, MaxSize = 4f, SpreadAngle = 360f,
        Color = new SKColor(0xFF, 0xD7, 0x00), FadeOut = true, Shape = 3
    };

    /// <summary>Grüne schwebende Partikel (Wind-Element).</summary>
    public static readonly ParticleConfig WindGust = new()
    {
        MinSpeed = 100f, MaxSpeed = 200f, MinLife = 0.6f, MaxLife = 1.0f,
        MinSize = 3f, MaxSize = 5f, SpreadAngle = 60f, BaseAngle = 0f, Gravity = -10f,
        Color = new SKColor(0x90, 0xEE, 0x90, 180), FadeOut = true, ShrinkOut = true, Shape = 0
    };

    /// <summary>Goldene aufsteigende Licht-Partikel (Licht-Element).</summary>
    public static readonly ParticleConfig HolyLight = new()
    {
        MinSpeed = 30f, MaxSpeed = 80f, MinLife = 0.8f, MaxLife = 1.2f,
        MinSize = 4f, MaxSize = 8f, SpreadAngle = 60f, BaseAngle = 270f, Gravity = -20f,
        Color = new SKColor(0xFF, 0xD7, 0x00), FadeOut = true, ShrinkOut = true, Shape = 1
    };

    /// <summary>Lila expandierende Void-Partikel (Dunkel-Element).</summary>
    public static readonly ParticleConfig ShadowVoid = new()
    {
        MinSpeed = 20f, MaxSpeed = 60f, MinLife = 0.5f, MaxLife = 0.8f,
        MinSize = 4f, MaxSize = 7f, SpreadAngle = 360f,
        Color = new SKColor(0x4B, 0x00, 0x82), FadeOut = true, Shape = 0
    };

    public ParticleSystem(int maxParticles = 200)
    {
        _maxParticles = maxParticles;
        _particles = new Particle[maxParticles];
    }

    /// <summary>
    /// Emittiert Partikel an einer Position mit gegebener Config.
    /// </summary>
    public void Emit(float x, float y, int count, ParticleConfig config)
    {
        var spreadRad = config.SpreadAngle * MathF.PI / 180f;
        var baseRad = config.BaseAngle * MathF.PI / 180f;

        for (int i = 0; i < count; i++)
        {
            ref var p = ref _particles[_nextIndex % _maxParticles];
            _nextIndex++;

            var angle = baseRad + (float)(_random.NextDouble() - 0.5) * spreadRad;
            var speed = config.MinSpeed + (float)_random.NextDouble() * (config.MaxSpeed - config.MinSpeed);

            p.X = x;
            p.Y = y;
            p.VelocityX = MathF.Cos(angle) * speed;
            p.VelocityY = MathF.Sin(angle) * speed;
            p.MaxLife = config.MinLife + (float)_random.NextDouble() * (config.MaxLife - config.MinLife);
            p.Life = p.MaxLife;
            p.Size = config.MinSize + (float)_random.NextDouble() * (config.MaxSize - config.MinSize);
            p.Color = (uint)config.Color & 0x00FFFFFF; // Alpha entfernen, wird in Render() separat berechnet
            p.Shape = config.Shape;
            p.Alpha = 1f;
            p.Rotation = (float)(_random.NextDouble() * MathF.PI * 2);
            p.RotationSpeed = config.RotationSpeed * ((float)_random.NextDouble() * 2f - 1f);
            p.GravityY = config.Gravity;
            p.InitialSize = p.Size;
            p.FadeOut = config.FadeOut;
            p.ShrinkOut = config.ShrinkOut;
            p.IsActive = true;
        }
    }

    /// <summary>
    /// Kontinuierliche Emission (für Ambient-Partikel). Jeden Frame aufrufen.
    /// Rate = Partikel pro Sekunde. AreaWidth/Height für zufällige Streuung.
    /// </summary>
    public void EmitContinuous(float x, float y, float rate, float deltaTime, ParticleConfig config,
        float areaWidth = 0, float areaHeight = 0)
    {
        // Akkumulator-basierte Emission: sammelt fraktionale Partikel über Frames hinweg
        _emitAccumulator += rate * deltaTime;
        int count = (int)_emitAccumulator;
        _emitAccumulator -= count;

        for (int i = 0; i < count; i++)
        {
            var px = x + (areaWidth > 0 ? (float)(_random.NextDouble() - 0.5) * areaWidth : 0);
            var py = y + (areaHeight > 0 ? (float)(_random.NextDouble() - 0.5) * areaHeight : 0);
            Emit(px, py, 1, config);
        }
    }

    /// <summary>
    /// Alle aktiven Partikel aktualisieren (Position, Schwerkraft, Lebenszeit).
    /// </summary>
    public void Update(float deltaTime)
    {
        for (int i = 0; i < _maxParticles; i++)
        {
            ref var p = ref _particles[i];
            if (!p.IsActive) continue;

            p.Life -= deltaTime;
            if (p.Life <= 0)
            {
                p.IsActive = false;
                continue;
            }

            // Bewegung
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.Rotation += p.RotationSpeed * deltaTime;

            // Schwerkraft pro Partikel (aus Config beim Emit gesetzt)
            p.VelocityY += p.GravityY * deltaTime;

            // Fade und Shrink basierend auf Lebenszeit und Config
            var lifeRatio = p.Life / p.MaxLife;
            p.Alpha = p.FadeOut ? lifeRatio : 1f;
            if (p.ShrinkOut)
                p.Size = p.InitialSize * lifeRatio;
        }
    }

    /// <summary>
    /// Alle aktiven Partikel auf das Canvas zeichnen.
    /// </summary>
    public void Render(SKCanvas canvas)
    {
        for (int i = 0; i < _maxParticles; i++)
        {
            ref var p = ref _particles[i];
            if (!p.IsActive) continue;

            // Farbe aus uint rekonstruieren + Alpha anwenden
            var color = new SKColor(
                (byte)(p.Color >> 16 & 0xFF),
                (byte)(p.Color >> 8 & 0xFF),
                (byte)(p.Color & 0xFF),
                (byte)(p.Alpha * 255));

            switch (p.Shape)
            {
                case 0: // Kreis
                    _particlePaint.Color = color;
                    canvas.DrawCircle(p.X, p.Y, p.Size, _particlePaint);
                    break;

                case 1: // Stern (4-zackig)
                    canvas.Save();
                    canvas.Translate(p.X, p.Y);
                    canvas.RotateRadians(p.Rotation);
                    DrawStar(canvas, p.Size, color);
                    canvas.Restore();
                    break;

                case 2: // Quadrat (rotiert)
                    canvas.Save();
                    canvas.Translate(p.X, p.Y);
                    canvas.RotateRadians(p.Rotation);
                    _particlePaint.Color = color;
                    canvas.DrawRect(-p.Size, -p.Size, p.Size * 2, p.Size * 2, _particlePaint);
                    canvas.Restore();
                    break;

                case 3: // Linie (Glitch-Effekt)
                    _linePaint.Color = color;
                    canvas.DrawLine(p.X, p.Y, p.X + p.Size * 3, p.Y, _linePaint);
                    break;
            }
        }
    }

    /// <summary>
    /// Alle Partikel deaktivieren (z.B. bei Szenen-Wechsel).
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _maxParticles; i++)
            _particles[i].IsActive = false;
    }

    /// <summary>
    /// Gibt an, ob mindestens ein Partikel aktiv ist.
    /// </summary>
    public bool HasActiveParticles
    {
        get
        {
            for (int i = 0; i < _maxParticles; i++)
                if (_particles[i].IsActive) return true;
            return false;
        }
    }

    /// <summary>
    /// Gibt alle nativen Ressourcen frei (SKPaint, SKPath).
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _particlePaint.Dispose();
        _linePaint.Dispose();
        _starPath.Dispose();
    }

    /// <summary>
    /// Zeichnet einen 4-zackigen Stern am Ursprung (0,0). SKPath wird wiederverwendet.
    /// </summary>
    private void DrawStar(SKCanvas canvas, float size, SKColor color)
    {
        _particlePaint.Color = color;
        _starPath.Rewind();

        // 4-zackiger Stern
        _starPath.MoveTo(0, -size);
        _starPath.LineTo(size * 0.3f, -size * 0.3f);
        _starPath.LineTo(size, 0);
        _starPath.LineTo(size * 0.3f, size * 0.3f);
        _starPath.LineTo(0, size);
        _starPath.LineTo(-size * 0.3f, size * 0.3f);
        _starPath.LineTo(-size, 0);
        _starPath.LineTo(-size * 0.3f, -size * 0.3f);
        _starPath.Close();

        canvas.DrawPath(_starPath, _particlePaint);
    }
}
