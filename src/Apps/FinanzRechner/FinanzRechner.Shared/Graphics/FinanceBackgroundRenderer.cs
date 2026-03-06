using System;
using SkiaSharp;

namespace FinanzRechner.Graphics;

/// <summary>
/// Animierter Hintergrund fuer den FinanzRechner: "Financial Data Stream".
/// Rendert 5 Layer: Smaragd-Gradient, Chart-Linien, Mini-Balken-Partikel, Sparkle-Punkte, Vignette.
/// Instance-basiert mit GC-freiem Render-Loop (Struct-Pools, gecachte Paints).
/// Wird von einem ~5fps DispatcherTimer in der MainView invalidiert.
/// </summary>
public sealed class FinanceBackgroundRenderer : IDisposable
{
    private bool _disposed;

    // =====================================================================
    // Farb-Konstanten (Smaragd-Palette)
    // =====================================================================

    // Gradient-Farben (Smaragd-Palette passend zu AppPalette)
    private static readonly SKColor GradientTop = new(0x22, 0x3A, 0x32);    // #223A32
    private static readonly SKColor GradientMid = new(0x18, 0x2A, 0x24);    // #182A24
    private static readonly SKColor GradientBot = new(0x0E, 0x24, 0x18);    // #0E2418

    // Akzent-Farbe (Smaragd)
    private static readonly SKColor Emerald = new(0x10, 0xB9, 0x81);        // #10B981

    // Vignette-Basis (dunkelster Gradient)
    private static readonly SKColor VignetteDark = new(0x0A, 0x18, 0x12);   // Dunkler als GradientBot

    // =====================================================================
    // Partikel-Typen und -Pool
    // =====================================================================

    private enum ParticleType : byte { MiniBar, Sparkle }

    private struct FinanceParticle
    {
        public float X, Y;
        public float VelocityX, VelocityY;
        public float Alpha, MaxAlpha;
        public float Width, Height;       // Fuer Mini-Balken (Width=Breite, Height=Hoehe)
        public float Life, MaxLife, Phase;
        public ParticleType Type;
        public bool Active;
    }

    private const int MaxMiniBarParticles = 8;
    private const int MaxSparkleParticles = 5;
    private const int MaxParticles = MaxMiniBarParticles + MaxSparkleParticles; // 13 gesamt (unter 15 Limit)
    private readonly FinanceParticle[] _particles = new FinanceParticle[MaxParticles];

    // =====================================================================
    // Gecachte Paints (0 GC im Render-Loop)
    // =====================================================================

    private readonly SKPaint _gradientPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    private readonly SKPaint _chartLinePaint = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
    private readonly SKPaint _particlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _vignettePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // =====================================================================
    // Shader-Cache mit Bounds-Check
    // =====================================================================

    private SKShader? _bgShader;
    private SKShader? _vignetteShader;
    private float _lastW, _lastH;

    // =====================================================================
    // Chart-Linien Konfiguration
    // =====================================================================

    private const int ChartLineCount = 6;
    private static readonly SKPathEffect DashEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, 0f);

    // =====================================================================
    // Update (vom DispatcherTimer aufgerufen)
    // =====================================================================

    /// <summary>
    /// Aktualisiert Partikel-Positionen und -Zustaende. Pro Frame aufrufen.
    /// </summary>
    public void Update(float deltaTime)
    {
        int activeBars = 0;
        int activeSparkles = 0;

        for (int i = 0; i < _particles.Length; i++)
        {
            if (!_particles[i].Active)
                continue;

            ref var p = ref _particles[i];
            p.Life += deltaTime;

            if (p.Life >= p.MaxLife)
            {
                p.Active = false;
                continue;
            }

            // Typ-spezifisches Update
            if (p.Type == ParticleType.MiniBar)
            {
                activeBars++;
                // Langsam aufsteigen mit leichtem X-Wobble
                p.Y += p.VelocityY * deltaTime;
                p.X += MathF.Sin(p.Phase + p.Life * 0.8f) * 0.3f;
            }
            else
            {
                activeSparkles++;
                // Sparkles: Alpha pulsiert (0 -> MaxAlpha -> 0 ueber die Lebensdauer)
                // Position bleibt statisch
            }
        }

        // Neue Partikel spawnen
        if (activeBars < MaxMiniBarParticles && _lastW > 0f)
            TrySpawnMiniBar();

        if (activeSparkles < MaxSparkleParticles && _lastW > 0f)
            TrySpawnSparkle();
    }

    // =====================================================================
    // Render (5 Layer)
    // =====================================================================

    /// <summary>
    /// Zeichnet alle 5 Layer. bounds = canvas.LocalClipBounds.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, float time)
    {
        RenderBackground(canvas, bounds);
        RenderChartLines(canvas, bounds, time);
        RenderParticles(canvas, bounds);
        RenderVignette(canvas, bounds);
    }

    // =====================================================================
    // Layer 1: Vertikaler 3-Farben Gradient (dunkel-smaragd)
    // =====================================================================

    private void RenderBackground(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width;
        float h = bounds.Height;

        // Shader nur neu erstellen wenn sich die Groesse geaendert hat
        if (_bgShader == null || MathF.Abs(w - _lastW) > 1f || MathF.Abs(h - _lastH) > 1f)
        {
            _bgShader?.Dispose();

            _bgShader = SKShader.CreateLinearGradient(
                new SKPoint(bounds.MidX, bounds.Top),
                new SKPoint(bounds.MidX, bounds.Bottom),
                new[] { GradientTop, GradientMid, GradientBot },
                new[] { 0f, 0.5f, 1f },
                SKShaderTileMode.Clamp);

            _lastW = w;
            _lastH = h;

            // Vignette-Shader auch invalidieren
            _vignetteShader?.Dispose();
            _vignetteShader = null;
        }

        _gradientPaint.Shader = _bgShader;
        canvas.DrawRect(bounds, _gradientPaint);
        _gradientPaint.Shader = null;
    }

    // =====================================================================
    // Layer 2: Dezente horizontale Chart-Linien (wie Kursdiagramm-Raster)
    // =====================================================================

    private void RenderChartLines(SKCanvas canvas, SKRect bounds, float time)
    {
        float top = bounds.Top;
        float bottom = bounds.Bottom;
        float left = bounds.Left;
        float right = bounds.Right;
        float totalHeight = bottom - top;

        _chartLinePaint.PathEffect = DashEffect;

        for (int i = 0; i < ChartLineCount; i++)
        {
            // Gleichmaessig verteilt (15% bis 85% der Hoehe)
            float yRatio = 0.15f + i * (0.70f / (ChartLineCount - 1));
            float y = top + totalHeight * yRatio;

            // Dezentes Alpha-Pulsieren pro Linie (unterschiedliche Phase)
            float pulse = 0.5f + 0.5f * MathF.Sin(time * 0.3f + i * 1.1f);
            byte alpha = (byte)(6 + pulse * 4); // Alpha 6-10

            _chartLinePaint.Color = SKColors.White.WithAlpha(alpha);
            canvas.DrawLine(left, y, right, y, _chartLinePaint);
        }

        _chartLinePaint.PathEffect = null;
    }

    // =====================================================================
    // Layer 3+4: Mini-Balken + Sparkle-Punkte (Partikel)
    // =====================================================================

    private void RenderParticles(SKCanvas canvas, SKRect bounds)
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            if (!_particles[i].Active) continue;

            ref var p = ref _particles[i];
            float lifeRatio = p.Life / p.MaxLife;

            if (p.Type == ParticleType.MiniBar)
            {
                RenderMiniBar(canvas, ref p, lifeRatio);
            }
            else
            {
                RenderSparkle(canvas, ref p, lifeRatio);
            }
        }
    }

    /// <summary>
    /// Rendert einen einzelnen Mini-Balken (Kurs-Balken-Effekt).
    /// </summary>
    private void RenderMiniBar(SKCanvas canvas, ref FinanceParticle p, float lifeRatio)
    {
        // Fade: In (0-15%) -> Voll (15-70%) -> Out (70-100%)
        float alpha;
        if (lifeRatio < 0.15f)
            alpha = lifeRatio / 0.15f;
        else if (lifeRatio < 0.70f)
            alpha = 1f;
        else
            alpha = 1f - (lifeRatio - 0.70f) / 0.30f;

        float finalAlpha = p.MaxAlpha * alpha;
        if (finalAlpha < 0.01f) return;

        byte alphaB = (byte)(finalAlpha * 255f);
        _particlePaint.Color = Emerald.WithAlpha(alphaB);

        // Vertikales Rechteck zeichnen (Mini-Balken)
        canvas.DrawRect(p.X - p.Width * 0.5f, p.Y - p.Height, p.Width, p.Height, _particlePaint);
    }

    /// <summary>
    /// Rendert einen Sparkle-Punkt (kurz aufblitzender Datenpunkt).
    /// </summary>
    private void RenderSparkle(SKCanvas canvas, ref FinanceParticle p, float lifeRatio)
    {
        // Glockenfoermiger Alpha-Verlauf: 0 -> MaxAlpha -> 0
        // Sinus-basiert fuer weichen Uebergang
        float alpha = MathF.Sin(lifeRatio * MathF.PI);
        float finalAlpha = p.MaxAlpha * alpha;
        if (finalAlpha < 0.01f) return;

        byte alphaB = (byte)(finalAlpha * 255f);
        _particlePaint.Color = Emerald.WithAlpha(alphaB);

        // Kleiner leuchtender Punkt
        canvas.DrawCircle(p.X, p.Y, p.Width, _particlePaint);
    }

    // =====================================================================
    // Layer 5: Radiale Vignette
    // =====================================================================

    private void RenderVignette(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width;
        float h = bounds.Height;

        // Shader nur neu erstellen wenn sich die Groesse geaendert hat
        if (_vignetteShader == null)
        {
            float radius = MathF.Max(w, h) * 0.75f;

            _vignetteShader = SKShader.CreateRadialGradient(
                new SKPoint(bounds.MidX, bounds.MidY),
                radius,
                new[] { SKColors.Transparent, VignetteDark.WithAlpha(140) },
                new[] { 0.4f, 1.0f },
                SKShaderTileMode.Clamp);
        }

        _vignettePaint.Shader = _vignetteShader;
        canvas.DrawRect(bounds, _vignettePaint);
        _vignettePaint.Shader = null;
    }

    // =====================================================================
    // Partikel-Emission
    // =====================================================================

    /// <summary>
    /// Versucht einen Mini-Balken zu spawnen (~0.15 Wahrscheinlichkeit pro Aufruf).
    /// </summary>
    private void TrySpawnMiniBar()
    {
        // Spawn-Wahrscheinlichkeit (~5fps, ca. 0.75 Partikel/s)
        if (Random.Shared.NextSingle() > 0.15f) return;

        int slot = FindFreeSlot(0, MaxMiniBarParticles);
        if (slot < 0) return;

        ref var p = ref _particles[slot];
        p.Active = true;
        p.Type = ParticleType.MiniBar;
        p.Life = 0f;
        p.MaxLife = 6f + Random.Shared.NextSingle() * 4f; // 6-10 Sekunden (langsam)
        p.Phase = Random.Shared.NextSingle() * MathF.Tau;

        // Breite 3-5px, Hoehe 8-20px
        p.Width = 3f + Random.Shared.NextSingle() * 2f;
        p.Height = 8f + Random.Shared.NextSingle() * 12f;

        // Spawn im unteren 70% des Screens
        p.X = _lastW * (0.05f + Random.Shared.NextSingle() * 0.90f);
        p.Y = _lastH * (0.30f + Random.Shared.NextSingle() * 0.70f);

        // Langsam nach oben steigend
        p.VelocityX = 0f; // X-Wobble kommt aus dem Sinus im Update
        p.VelocityY = -(6f + Random.Shared.NextSingle() * 8f); // -6 bis -14 px/s

        // Alpha 15-22 (dezent)
        p.MaxAlpha = 0.06f + Random.Shared.NextSingle() * 0.03f; // 0.06-0.09 (~15-22 von 255)
    }

    /// <summary>
    /// Versucht einen Sparkle-Punkt zu spawnen (~0.10 Wahrscheinlichkeit pro Aufruf).
    /// </summary>
    private void TrySpawnSparkle()
    {
        // Seltener als Mini-Balken
        if (Random.Shared.NextSingle() > 0.10f) return;

        int slot = FindFreeSlot(MaxMiniBarParticles, MaxParticles);
        if (slot < 0) return;

        ref var p = ref _particles[slot];
        p.Active = true;
        p.Type = ParticleType.Sparkle;
        p.Life = 0f;
        p.MaxLife = 2f + Random.Shared.NextSingle() * 1f; // 2-3 Sekunden

        // Zufaellige Position im gesamten Screen
        p.X = _lastW * Random.Shared.NextSingle();
        p.Y = _lastH * (0.10f + Random.Shared.NextSingle() * 0.80f);

        // Punkt-Groesse 1.5-3px
        p.Width = 1.5f + Random.Shared.NextSingle() * 1.5f;

        // Alpha 15-22 (dezent)
        p.MaxAlpha = 0.06f + Random.Shared.NextSingle() * 0.03f;

        // Sparkles bewegen sich nicht
        p.VelocityX = 0f;
        p.VelocityY = 0f;
    }

    /// <summary>
    /// Sucht einen freien Partikel-Slot im angegebenen Bereich.
    /// </summary>
    private int FindFreeSlot(int from, int to)
    {
        for (int i = from; i < to; i++)
        {
            if (!_particles[i].Active)
                return i;
        }
        return -1;
    }

    // =====================================================================
    // Dispose
    // =====================================================================

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _gradientPaint.Dispose();
        _chartLinePaint.Dispose();
        _particlePaint.Dispose();
        _vignettePaint.Dispose();

        _bgShader?.Dispose();
        _vignetteShader?.Dispose();

        // Statisches DashEffect NICHT disposen (static readonly, lebt bis Prozessende)
    }
}
