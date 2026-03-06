using SkiaSharp;

namespace RechnerPlus.Graphics;

/// <summary>
/// Animierter Hintergrund: Digital Circuit Board mit Dot-Grid und Math-Partikeln.
/// Alle Paints/Fonts sind gecacht (0 GC-Allokation pro Frame).
/// Render-Rate: ~5fps (200ms Timer), daher bewusst einfach gehalten.
/// </summary>
public sealed class CalculatorBackgroundRenderer : IDisposable
{
    private const int MaxParticles = 15;
    private static readonly string[] MathSymbols = ["÷", "×", "+", "−", "=", "π", "√", "∑", "%"];

    private bool _disposed;
    private readonly Particle[] _particles = new Particle[MaxParticles];

    // Gecachte Paints (kein new pro Frame)
    private readonly SKPaint _gradientPaint = new() { IsAntialias = false };
    private readonly SKPaint _dotPaint = new() { IsAntialias = true };
    private readonly SKPaint _particlePaint = new() { IsAntialias = true };
    private readonly SKPaint _vignettePaint = new() { IsAntialias = false };

    // Gecachter Font fuer Partikel-Symbole (Size wird pro Partikel angepasst)
    private readonly SKFont _symbolFont = new(SKTypeface.Default, 14f);

    // Shader-Cache (nur bei Groessenaenderung neu erstellt)
    private SKShader? _bgShader;
    private SKShader? _vignetteShader;
    private float _cachedW, _cachedH;

    // Farben aus AppPalette (GradientStart/Mid/End + Primary Indigo)
    private static readonly SKColor GradientTop = SKColor.Parse("#302A56");
    private static readonly SKColor GradientMid = SKColor.Parse("#221E40");
    private static readonly SKColor GradientBot = SKColor.Parse("#2C1850");
    private static readonly SKColor DotColor = new(124, 127, 247, 10);   // Indigo, Alpha ~4%
    private static readonly SKColor ParticleColor = new(124, 127, 247, 20); // Indigo, Alpha ~8%

    private struct Particle
    {
        public float X, Y, Speed, Phase;
        public int SymbolIndex;
        public bool Active;
        public float Life, MaxLife;
        public float Size;
    }

    /// <summary>
    /// Aktualisiert Partikel-Positionen. deltaTime in Sekunden (bei 5fps = 0.2f).
    /// </summary>
    public void Update(float deltaTime)
    {
        for (int i = 0; i < MaxParticles; i++)
        {
            ref var p = ref _particles[i];
            if (!p.Active)
            {
                // 3% Chance pro Frame, neuen Partikel zu spawnen
                if (Random.Shared.NextDouble() < 0.03)
                {
                    p.Active = true;
                    p.X = Random.Shared.NextSingle();          // 0..1 (normalisiert)
                    p.Y = 1.1f;                                 // startet unterhalb des sichtbaren Bereichs
                    p.Speed = 0.008f + Random.Shared.NextSingle() * 0.012f;
                    p.Phase = Random.Shared.NextSingle() * MathF.Tau;
                    p.SymbolIndex = Random.Shared.Next(MathSymbols.Length);
                    p.MaxLife = 8f + Random.Shared.NextSingle() * 12f;
                    p.Life = 0f;
                    p.Size = 10f + Random.Shared.NextSingle() * 6f;
                }
                continue;
            }

            // Nach oben driften + leichtes horizontales Schwingen (Sin-Welle)
            p.Y -= p.Speed * deltaTime * 10f;
            p.X += MathF.Sin(p.Phase + p.Life * 0.3f) * 0.0003f;
            p.Life += deltaTime;

            // Deaktivieren wenn ausserhalb oder Lebenszeit abgelaufen
            if (p.Y < -0.1f || p.Life > p.MaxLife)
                p.Active = false;
        }
    }

    /// <summary>
    /// Rendert alle 4 Layer auf den Canvas.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, float time)
    {
        if (bounds.Width < 1 || bounds.Height < 1) return;

        RenderGradient(canvas, bounds);
        RenderDotGrid(canvas, bounds, time);
        RenderParticles(canvas, bounds);
        RenderVignette(canvas, bounds);
    }

    /// <summary>
    /// Layer 1: Vertikaler 3-Farben Gradient (GradientStart -> Mid -> End).
    /// </summary>
    private void RenderGradient(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width, h = bounds.Height;

        // Shader nur bei Groessenaenderung neu erstellen
        if (MathF.Abs(w - _cachedW) > 1f || MathF.Abs(h - _cachedH) > 1f)
        {
            RecreateShaders(w, h);
        }

        _gradientPaint.Shader = _bgShader;
        canvas.DrawRect(bounds, _gradientPaint);
    }

    /// <summary>
    /// Layer 2: Subtiles Dot-Grid (Leiterplatten-Punkte) mit langsamem vertikalem Drift.
    /// </summary>
    private void RenderDotGrid(SKCanvas canvas, SKRect bounds, float time)
    {
        const float spacing = 32f;
        const float dotRadius = 1.2f;

        _dotPaint.Color = DotColor;

        // Langsames vertikales Driften (2px/s)
        float drift = (time * 2f) % spacing;

        for (float y = -spacing + drift; y < bounds.Height + spacing; y += spacing)
        {
            for (float x = 0; x < bounds.Width; x += spacing)
            {
                canvas.DrawCircle(x, y, dotRadius, _dotPaint);
            }
        }
    }

    /// <summary>
    /// Layer 3: Floating Math-Symbole als transparente Partikel, langsam nach oben driftend.
    /// </summary>
    private void RenderParticles(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width, h = bounds.Height;

        for (int i = 0; i < MaxParticles; i++)
        {
            ref var p = ref _particles[i];
            if (!p.Active) continue;

            // Fade-in (erste Sekunde) und Fade-out (letzte 2 Sekunden)
            float alpha;
            if (p.Life < 1f)
                alpha = p.Life;
            else if (p.Life > p.MaxLife - 2f)
                alpha = (p.MaxLife - p.Life) / 2f;
            else
                alpha = 1f;

            _particlePaint.Color = ParticleColor.WithAlpha((byte)(alpha * 22));

            // Font-Groesse pro Partikel anpassen (kein new, nur Size-Setter)
            _symbolFont.Size = p.Size;

            float tx = p.X * w;
            float ty = p.Y * h;

            canvas.DrawText(MathSymbols[p.SymbolIndex], tx, ty, _symbolFont, _particlePaint);
        }
    }

    /// <summary>
    /// Layer 4: Radiale Vignette (dunkle Ecken fuer mehr Tiefe).
    /// </summary>
    private void RenderVignette(SKCanvas canvas, SKRect bounds)
    {
        _vignettePaint.Shader = _vignetteShader;
        canvas.DrawRect(bounds, _vignettePaint);
    }

    /// <summary>
    /// Erstellt Gradient- und Vignette-Shader neu (nur bei Groessenaenderung).
    /// </summary>
    private void RecreateShaders(float w, float h)
    {
        _bgShader?.Dispose();
        _bgShader = SKShader.CreateLinearGradient(
            new SKPoint(w / 2, 0),
            new SKPoint(w / 2, h),
            [GradientTop, GradientMid, GradientBot],
            [0f, 0.5f, 1f],
            SKShaderTileMode.Clamp);

        _vignetteShader?.Dispose();
        float radius = MathF.Max(w, h) * 0.8f;
        _vignetteShader = SKShader.CreateRadialGradient(
            new SKPoint(w / 2, h / 2),
            radius,
            [SKColors.Transparent, new SKColor(0, 0, 0, 60)],
            SKShaderTileMode.Clamp);

        _cachedW = w;
        _cachedH = h;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _bgShader?.Dispose();
        _vignetteShader?.Dispose();
        _gradientPaint.Dispose();
        _dotPaint.Dispose();
        _particlePaint.Dispose();
        _vignettePaint.Dispose();
        _symbolFont.Dispose();
    }
}
