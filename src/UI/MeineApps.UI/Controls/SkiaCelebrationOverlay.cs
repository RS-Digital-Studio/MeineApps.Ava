using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace MeineApps.UI.Controls;

/// <summary>
/// Premium-Confetti-Overlay mit SkiaSharp-Rendering.
/// Visuell deutlich hochwertiger als die Border-basierte CelebrationOverlay:
/// - Glow-Effekte auf Partikeln
/// - Stern- und Kreisformen neben Rechtecken
/// - Leuchtende Trails
/// - Blitz-Flash zu Beginn
/// - Mehr Partikel (80 statt 40) dank effizientem SkiaSharp-Rendering
/// </summary>
public class SkiaCelebrationOverlay : SKCanvasView
{
    private const int MaxParticles = 80;
    private readonly CelebrationParticle[] _particles = new CelebrationParticle[MaxParticles];
    private int _activeCount;

    private DispatcherTimer? _timer;
    private float _elapsed;
    private bool _isAnimating;
    private float _flashAlpha; // Initialer Blitz-Effekt
    private DateTime _lastTickTime; // Echte deltaTime statt hartkodierter 0.016f

    private readonly Random _rng = new();

    // Gecachter SKPath pro Instanz (nicht static wegen Thread-Safety bei mehreren Overlays)
    private readonly SKPath _starPath = new();

    // Konfetti-Farben (erweiterte Palette, immutable - static ist hier sicher)
    private static readonly SKColor[] ConfettiColors =
    [
        new(0xFF, 0xD7, 0x00), // Gold
        new(0xEF, 0x44, 0x44), // Rot
        new(0x22, 0xC5, 0x5E), // Grün
        new(0x3B, 0x82, 0xF6), // Blau
        new(0xA7, 0x8B, 0xFA), // Violett
        new(0x22, 0xD3, 0xEE), // Cyan
        new(0xEC, 0x48, 0x99), // Pink
        new(0xF5, 0x9E, 0x0B), // Amber
    ];

    // Gecachte Paints pro Instanz (nicht static wegen Thread-Safety / konkurrierender Color-Setzung)
    private readonly SKPaint _fillPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private readonly SKPaint _glowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f)
    };

    private readonly SKPaint _flashPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        Color = SKColors.White
    };

    // Animationsdauer
    private const float AnimationDuration = 2.0f;
    private const float FlashDuration = 0.15f;

    public SkiaCelebrationOverlay()
    {
        IsHitTestVisible = false;
        IsVisible = false;
    }

    /// <summary>
    /// Startet die Celebration-Animation. Kann mehrfach aufgerufen werden.
    /// </summary>
    public void ShowConfetti()
    {
        if (_isAnimating)
            _timer?.Stop();

        var bounds = this.LogicalSize;
        float width = bounds.Width > 0 ? bounds.Width : 400;
        float height = bounds.Height > 0 ? bounds.Height : 800;

        _activeCount = MaxParticles;
        _elapsed = 0;
        _flashAlpha = 0.4f; // Initialer Blitz

        for (int i = 0; i < MaxParticles; i++)
        {
            var color = ConfettiColors[_rng.Next(ConfettiColors.Length)];
            var shape = (ParticleShape)_rng.Next(3); // Rechteck, Kreis, Stern
            float angle = (float)(_rng.NextDouble() * Math.PI * 2);
            float speed = 100f + (float)_rng.NextDouble() * 200f;

            _particles[i] = new CelebrationParticle
            {
                X = (float)_rng.NextDouble() * width,
                Y = -20f - (float)_rng.NextDouble() * 80f,
                VelocityX = MathF.Cos(angle) * speed * 0.5f,
                VelocityY = 60f + (float)_rng.NextDouble() * 180f,
                Size = 4f + (float)_rng.NextDouble() * 5f,
                Rotation = (float)_rng.NextDouble() * 360f,
                RotationSpeed = -360f + (float)_rng.NextDouble() * 720f,
                Color = color,
                Shape = shape,
                PhaseOffset = (float)(_rng.NextDouble() * Math.PI * 2),
                HasGlow = _rng.NextDouble() > 0.6, // 40% der Partikel haben Glow
                Active = true
            };
        }

        StartAnimation();
    }

    /// <summary>
    /// Startet einen subtileren Sparkle-Effekt (weniger Partikel, nur Kreise/Sterne).
    /// </summary>
    public void ShowSparkle()
    {
        if (_isAnimating)
            _timer?.Stop();

        var bounds = this.LogicalSize;
        float width = bounds.Width > 0 ? bounds.Width : 400;
        float height = bounds.Height > 0 ? bounds.Height : 800;
        float centerX = width / 2f;
        float centerY = height / 3f;

        _activeCount = 30;
        _elapsed = 0;
        _flashAlpha = 0.2f;

        for (int i = 0; i < MaxParticles; i++)
        {
            if (i >= _activeCount)
            {
                _particles[i].Active = false;
                continue;
            }

            var color = ConfettiColors[_rng.Next(ConfettiColors.Length)];
            float angle = (float)(_rng.NextDouble() * Math.PI * 2);
            float speed = 60f + (float)_rng.NextDouble() * 120f;

            _particles[i] = new CelebrationParticle
            {
                X = centerX + (float)(_rng.NextDouble() - 0.5) * 100f,
                Y = centerY + (float)(_rng.NextDouble() - 0.5) * 60f,
                VelocityX = MathF.Cos(angle) * speed,
                VelocityY = MathF.Sin(angle) * speed - 40f,
                Size = 2f + (float)_rng.NextDouble() * 4f,
                Rotation = 0,
                RotationSpeed = 0,
                Color = color.WithAlpha(200),
                Shape = _rng.NextDouble() > 0.5 ? ParticleShape.Star : ParticleShape.Circle,
                PhaseOffset = 0,
                HasGlow = true,
                Active = true
            };
        }

        StartAnimation();
    }

    private void StartAnimation()
    {
        _isAnimating = true;
        IsVisible = true;
        _lastTickTime = DateTime.UtcNow;

        _timer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        // Echte deltaTime messen statt hartkodierter 0.016f
        var now = DateTime.UtcNow;
        float dt = Math.Min(0.05f, (float)(now - _lastTickTime).TotalSeconds); // Cap bei 50ms
        _lastTickTime = now;

        _elapsed += dt;

        if (_elapsed >= AnimationDuration)
        {
            StopAnimation();
            return;
        }

        // Flash abklingen lassen
        if (_flashAlpha > 0)
            _flashAlpha = Math.Max(0, _flashAlpha - dt / FlashDuration * 0.4f);

        var bounds = this.LogicalSize;
        float height = bounds.Height > 0 ? bounds.Height : 800;
        bool anyActive = false;

        for (int i = 0; i < MaxParticles; i++)
        {
            ref var p = ref _particles[i];
            if (!p.Active) continue;

            // Schwerkraft
            p.VelocityY += 150f * dt;

            // Sin-Schwankung für natürliche Bewegung
            float sway = MathF.Sin(_elapsed * 3f + p.PhaseOffset) * 25f * dt;

            p.X += p.VelocityX * dt + sway;
            p.Y += p.VelocityY * dt;
            p.Rotation += p.RotationSpeed * dt;

            // Luftwiderstand
            p.VelocityX *= 0.998f;

            if (p.Y > height + 30)
            {
                p.Active = false;
                continue;
            }

            anyActive = true;
        }

        if (!anyActive)
        {
            StopAnimation();
            return;
        }

        InvalidateSurface();
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        if (!_isAnimating) return;

        // Fade-Out in den letzten 30%
        float fadeStart = AnimationDuration * 0.7f;
        float globalAlpha = _elapsed > fadeStart
            ? 1f - (_elapsed - fadeStart) / (AnimationDuration - fadeStart)
            : 1f;
        globalAlpha = Math.Clamp(globalAlpha, 0f, 1f);

        // Initialer Blitz-Flash
        if (_flashAlpha > 0)
        {
            _flashPaint.Color = SKColors.White.WithAlpha((byte)(_flashAlpha * 255));
            canvas.DrawRect(bounds, _flashPaint);
        }

        // Partikel zeichnen
        for (int i = 0; i < MaxParticles; i++)
        {
            ref var p = ref _particles[i];
            if (!p.Active) continue;

            byte alpha = (byte)(globalAlpha * 255);
            if (alpha == 0) continue;

            canvas.Save();
            canvas.Translate(p.X, p.Y);
            canvas.RotateDegrees(p.Rotation);

            // Glow-Hintergrund (optional)
            if (p.HasGlow)
            {
                _glowPaint.Color = p.Color.WithAlpha((byte)(alpha * 0.3f));
                canvas.DrawCircle(0, 0, p.Size * 2.5f, _glowPaint);
            }

            _fillPaint.Color = p.Color.WithAlpha(alpha);

            switch (p.Shape)
            {
                case ParticleShape.Rectangle:
                    canvas.DrawRect(-p.Size, -p.Size * 0.6f, p.Size * 2, p.Size * 1.2f, _fillPaint);
                    break;

                case ParticleShape.Circle:
                    canvas.DrawCircle(0, 0, p.Size, _fillPaint);
                    break;

                case ParticleShape.Star:
                    DrawStar(canvas, p.Size, _fillPaint);
                    break;
            }

            canvas.Restore();
        }
    }

    /// <summary>
    /// Zeichnet einen 5-zackigen Stern. Nutzt gecachten SKPath (vermeidet native Allokationen pro Frame).
    /// </summary>
    private void DrawStar(SKCanvas canvas, float size, SKPaint paint)
    {
        _starPath.Rewind();
        const int points = 5;
        float outerRadius = size;
        float innerRadius = size * 0.4f;

        for (int i = 0; i < points * 2; i++)
        {
            float radius = i % 2 == 0 ? outerRadius : innerRadius;
            float angle = (float)(i * Math.PI / points - Math.PI / 2);
            float x = MathF.Cos(angle) * radius;
            float y = MathF.Sin(angle) * radius;

            if (i == 0)
                _starPath.MoveTo(x, y);
            else
                _starPath.LineTo(x, y);
        }

        _starPath.Close();
        canvas.DrawPath(_starPath, paint);
    }

    private void StopAnimation()
    {
        _timer?.Stop();
        _isAnimating = false;
        IsVisible = false;

        for (int i = 0; i < MaxParticles; i++)
            _particles[i].Active = false;
    }

    /// <summary>
    /// Aktuelle Canvas-Größe (in logischen Pixeln).
    /// </summary>
    private SKSize LogicalSize => new((float)Bounds.Width, (float)Bounds.Height);

    private enum ParticleShape
    {
        Rectangle,
        Circle,
        Star
    }

    private struct CelebrationParticle
    {
        public float X, Y;
        public float VelocityX, VelocityY;
        public float Size;
        public float Rotation, RotationSpeed;
        public SKColor Color;
        public ParticleShape Shape;
        public float PhaseOffset;
        public bool HasGlow;
        public bool Active;
    }
}
