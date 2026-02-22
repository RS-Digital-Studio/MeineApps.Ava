using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using SkiaSharp;

namespace BomberBlast.Views;

public partial class MainMenuView : UserControl
{
    // Einfaches Partikel-Array (Struct für GC-freie Animation)
    private struct MenuParticle
    {
        public float X, Y, Vx, Vy, Size;
        public byte R, G, B, Alpha;
        public float Phase; // Für sin-basiertes Schimmern
    }

    private const int PARTICLE_COUNT = 40;
    private readonly MenuParticle[] _particles = new MenuParticle[PARTICLE_COUNT];
    private readonly Random _rng = new();
    private readonly DispatcherTimer _animTimer;
    private SKCanvasView? _bgCanvas;
    private readonly SKPaint _particlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private float _canvasWidth, _canvasHeight;

    public MainMenuView()
    {
        InitializeComponent();

        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) }; // ~30fps
        _animTimer.Tick += (_, _) => _bgCanvas?.InvalidateSurface();

        AttachedToVisualTree += (_, _) =>
        {
            _bgCanvas = this.FindControl<SKCanvasView>("BgCanvas");
            if (_bgCanvas != null)
            {
                _bgCanvas.PaintSurface += OnPaintSurface;
            }
            InitParticles();
            _animTimer.Start();
        };

        DetachedFromVisualTree += (_, _) =>
        {
            _animTimer.Stop();
            if (_bgCanvas != null)
                _bgCanvas.PaintSurface -= OnPaintSurface;
        };
    }

    private void InitParticles()
    {
        // Farben: Verschiedene warme/coole Töne
        ReadOnlySpan<(byte r, byte g, byte b)> colors =
        [
            (255, 100, 50),   // Orange
            (80, 200, 255),   // Cyan
            (255, 215, 0),    // Gold
            (150, 80, 255),   // Lila
            (80, 255, 120),   // Grün
            (255, 80, 80),    // Rot
        ];

        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            var color = colors[_rng.Next(colors.Length)];
            _particles[i] = new MenuParticle
            {
                X = _rng.NextSingle() * 800,
                Y = _rng.NextSingle() * 600,
                Vx = (_rng.NextSingle() - 0.5f) * 0.3f, // Leichte seitliche Drift
                Vy = -(_rng.NextSingle() * 0.5f + 0.2f),
                Size = _rng.NextSingle() * 3f + 1.5f,
                R = color.r, G = color.g, B = color.b,
                Alpha = (byte)(_rng.Next(60, 140)),
                Phase = _rng.NextSingle() * MathF.PI * 2f
            };
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        _canvasWidth = bounds.Width;
        _canvasHeight = bounds.Height;

        canvas.Clear(SKColors.Transparent); // Vorheriges Frame löschen (transparent = Hintergrund scheint durch)

        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            ref var p = ref _particles[i];

            // Bewegen
            p.Y += p.Vy;
            p.X += p.Vx;
            p.Phase += 0.03f;

            // Wrap Y: Oben raus → unten rein
            if (p.Y < -10)
            {
                p.Y = _canvasHeight + 10;
                p.X = _rng.NextSingle() * _canvasWidth;
            }
            // Wrap X
            if (p.X < -10) p.X = _canvasWidth + 10;
            else if (p.X > _canvasWidth + 10) p.X = -10;

            // Schimmerndes Alpha
            byte alpha = (byte)Math.Clamp(p.Alpha + (int)(MathF.Sin(p.Phase) * 30f), 30, 200);

            // Äußerer Glow (bei helleren Partikeln)
            if (alpha > 80)
            {
                _particlePaint.Color = new SKColor(p.R, p.G, p.B, (byte)(alpha / 4));
                canvas.DrawCircle(p.X, p.Y, p.Size * 2.5f, _particlePaint);
            }

            // Kern-Partikel
            _particlePaint.Color = new SKColor(p.R, p.G, p.B, alpha);
            canvas.DrawCircle(p.X, p.Y, p.Size, _particlePaint);
        }
    }
}
