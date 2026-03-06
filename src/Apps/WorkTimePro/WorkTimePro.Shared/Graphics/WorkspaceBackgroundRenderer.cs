using System;
using SkiaSharp;

namespace WorkTimePro.Graphics;

/// <summary>
/// Animierter "Professional Dashboard / Calendar Grid"-Hintergrund fuer WorkTimePro.
/// Rendert 5 Layer: Vertikaler 3-Farben Gradient, Dot-Matrix-Grid, Calendar-Block-Partikel,
/// gestrichelte Stundenteilungs-Linien, radiale Vignette.
/// Instance-basiert mit GC-freiem Render-Loop (Struct-Pool, gecachte Paints, Shader-Caching).
/// Wird von einem ~5fps DispatcherTimer (200ms) in der MainView invalidiert.
/// </summary>
public sealed class WorkspaceBackgroundRenderer : IDisposable
{
    private bool _disposed;

    // =====================================================================
    // Farben (aus der Aufgabe, passend zur AppPalette)
    // =====================================================================

    private static readonly SKColor GradientTop = SKColor.Parse("#2C3440");
    private static readonly SKColor GradientMid = SKColor.Parse("#202630");
    private static readonly SKColor GradientBot = SKColor.Parse("#1C2028");
    private static readonly SKColor DotColor = new(255, 255, 255, 18);        // Weiss Alpha ~7
    private static readonly SKColor BlockColor = new(79, 139, 249, 15);       // Primary Blau Alpha ~6%
    private static readonly SKColor LineColor = new(255, 255, 255, 15);       // Weiss Alpha ~6

    // =====================================================================
    // Calendar-Block-Partikel (max 10)
    // =====================================================================

    private const int MaxBlocks = 10;

    private struct CalendarBlock
    {
        public float X, Y;
        public float VelocityX, VelocityY;
        public float Width, Height;
        public float Life, MaxLife;
        public float Alpha;
        public bool Active;
    }

    private readonly CalendarBlock[] _blocks = new CalendarBlock[MaxBlocks];

    // =====================================================================
    // Gecachte Paints (0 GC im Render-Loop)
    // =====================================================================

    private readonly SKPaint _gradientPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    private readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _blockPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _linePaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 0.5f,
        PathEffect = SKPathEffect.CreateDash(new[] { 6f, 8f }, 0f)
    };
    private readonly SKPaint _vignettePaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };

    // =====================================================================
    // Shader-Cache mit Bounds-Check
    // =====================================================================

    private SKShader? _bgShader;
    private SKShader? _vignetteShader;
    private float _cachedW, _cachedH;

    // =====================================================================
    // Spawn-Timer fuer Calendar-Blocks
    // =====================================================================

    private float _spawnTimer;
    private const float SpawnInterval = 1.5f; // Alle 1.5 Sekunden versuchen, einen Block zu spawnen

    // =====================================================================
    // Update (vom DispatcherTimer aufgerufen)
    // =====================================================================

    /// <summary>
    /// Aktualisiert Calendar-Block-Partikel. deltaTime in Sekunden (bei 5fps = 0.2f).
    /// </summary>
    public void Update(float deltaTime)
    {
        // Spawn-Timer
        _spawnTimer += deltaTime;
        if (_spawnTimer >= SpawnInterval)
        {
            _spawnTimer -= SpawnInterval;
            TrySpawnBlock();
        }

        // Bestehende Blocks aktualisieren
        for (int i = 0; i < MaxBlocks; i++)
        {
            if (!_blocks[i].Active) continue;

            ref var b = ref _blocks[i];
            b.Life += deltaTime;

            if (b.Life >= b.MaxLife)
            {
                b.Active = false;
                continue;
            }

            // Langsam driften
            b.X += b.VelocityX * deltaTime;
            b.Y += b.VelocityY * deltaTime;
        }
    }

    // =====================================================================
    // Render (5 Layer)
    // =====================================================================

    /// <summary>
    /// Zeichnet alle 5 Layer. bounds = canvas.LocalClipBounds.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, float time)
    {
        if (bounds.Width < 1 || bounds.Height < 1) return;

        RenderGradient(canvas, bounds);
        RenderDotMatrix(canvas, bounds, time);
        RenderCalendarBlocks(canvas, bounds);
        RenderHourLines(canvas, bounds);
        RenderVignette(canvas, bounds);
    }

    // =====================================================================
    // Layer 1: Vertikaler 3-Farben Gradient
    // =====================================================================

    private void RenderGradient(SKCanvas canvas, SKRect bounds)
    {
        float w = bounds.Width;
        float h = bounds.Height;

        // Shader nur bei Groessenaenderung neu erstellen
        if (_bgShader == null || MathF.Abs(w - _cachedW) > 1f || MathF.Abs(h - _cachedH) > 1f)
        {
            RecreateShaders(w, h);
        }

        _gradientPaint.Shader = _bgShader;
        canvas.DrawRect(bounds, _gradientPaint);
        _gradientPaint.Shader = null;
    }

    // =====================================================================
    // Layer 2: Dot-Matrix-Grid (wie Kalender-Rasterpapier)
    // =====================================================================

    private void RenderDotMatrix(SKCanvas canvas, SKRect bounds, float time)
    {
        const float spacing = 24f;
        const float dotRadius = 0.8f;

        _dotPaint.Color = DotColor;

        // Langsames vertikales Driften (1.5px/s) - wie ein sich scrollender Kalender
        float drift = (time * 1.5f) % spacing;

        for (float y = -spacing + drift; y < bounds.Height + spacing; y += spacing)
        {
            for (float x = 0; x < bounds.Width; x += spacing)
            {
                canvas.DrawCircle(x, y, dotRadius, _dotPaint);
            }
        }
    }

    // =====================================================================
    // Layer 3: Calendar-Block-Partikel (abgerundete Rechtecke)
    // =====================================================================

    private void RenderCalendarBlocks(SKCanvas canvas, SKRect bounds)
    {
        for (int i = 0; i < MaxBlocks; i++)
        {
            if (!_blocks[i].Active) continue;

            ref var b = ref _blocks[i];
            float lifeRatio = b.Life / b.MaxLife;

            // Fade: In (0-15%) -> Voll (15-75%) -> Out (75-100%)
            float alpha;
            if (lifeRatio < 0.15f)
                alpha = lifeRatio / 0.15f;
            else if (lifeRatio < 0.75f)
                alpha = 1f;
            else
                alpha = 1f - (lifeRatio - 0.75f) / 0.25f;

            float finalAlpha = b.Alpha * alpha;
            if (finalAlpha < 0.01f) continue;

            byte alphaB = (byte)(finalAlpha * 255f);
            _blockPaint.Color = BlockColor.WithAlpha(alphaB);

            // Abgerundetes Rechteck zeichnen (CornerRadius 2)
            float left = b.X;
            float top = b.Y;
            canvas.DrawRoundRect(left, top, b.Width, b.Height, 2f, 2f, _blockPaint);
        }
    }

    // =====================================================================
    // Layer 4: Feine horizontale Trennlinien (Stundeneinteilung)
    // =====================================================================

    private void RenderHourLines(SKCanvas canvas, SKRect bounds)
    {
        const float lineSpacing = 120f;

        _linePaint.Color = LineColor;

        for (float y = lineSpacing; y < bounds.Height; y += lineSpacing)
        {
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, _linePaint);
        }
    }

    // =====================================================================
    // Layer 5: Radiale Vignette (dunkle Ecken fuer Tiefe)
    // =====================================================================

    private void RenderVignette(SKCanvas canvas, SKRect bounds)
    {
        if (_vignetteShader == null) return;

        _vignettePaint.Shader = _vignetteShader;
        canvas.DrawRect(bounds, _vignettePaint);
        _vignettePaint.Shader = null;
    }

    // =====================================================================
    // Shader-Erstellung (nur bei Groessenaenderung)
    // =====================================================================

    private void RecreateShaders(float w, float h)
    {
        // Hintergrund-Gradient
        _bgShader?.Dispose();
        _bgShader = SKShader.CreateLinearGradient(
            new SKPoint(w / 2f, 0f),
            new SKPoint(w / 2f, h),
            new[] { GradientTop, GradientMid, GradientBot },
            new[] { 0f, 0.5f, 1f },
            SKShaderTileMode.Clamp);

        // Vignette (radial, transparent in der Mitte, dunkel am Rand)
        _vignetteShader?.Dispose();
        float radius = MathF.Max(w, h) * 0.75f;
        _vignetteShader = SKShader.CreateRadialGradient(
            new SKPoint(w / 2f, h / 2f),
            radius,
            new[] { SKColors.Transparent, new SKColor(0, 0, 0, 50) },
            new[] { 0.4f, 1.0f },
            SKShaderTileMode.Clamp);

        _cachedW = w;
        _cachedH = h;
    }

    // =====================================================================
    // Calendar-Block-Spawn
    // =====================================================================

    private void TrySpawnBlock()
    {
        // Freien Slot suchen
        int freeSlot = -1;
        for (int i = 0; i < MaxBlocks; i++)
        {
            if (!_blocks[i].Active)
            {
                freeSlot = i;
                break;
            }
        }

        if (freeSlot < 0) return; // Pool voll
        if (_cachedW < 1f || _cachedH < 1f) return; // Bounds noch nicht bekannt

        ref var b = ref _blocks[freeSlot];
        b.Active = true;
        b.Life = 0f;
        b.MaxLife = 8f + Random.Shared.NextSingle() * 6f; // 8-14 Sekunden Lebensdauer

        // Groesse: kleine abgerundete Rechtecke (wie Kalendereintraege)
        b.Width = 12f + Random.Shared.NextSingle() * 12f;  // 12-24px breit
        b.Height = 6f + Random.Shared.NextSingle() * 6f;   // 6-12px hoch

        // Alpha: sehr transparent (10-18 von 255)
        b.Alpha = (10f + Random.Shared.NextSingle() * 8f) / 255f;

        // Position: Breit verteilt ueber die gesamte Flaeche
        b.X = Random.Shared.NextSingle() * _cachedW;
        b.Y = Random.Shared.NextSingle() * _cachedH;

        // Driftrichtung: langsam in verschiedene Richtungen (nicht nur vertikal)
        float angle = Random.Shared.NextSingle() * MathF.Tau;
        float speed = 3f + Random.Shared.NextSingle() * 5f; // 3-8 px/s
        b.VelocityX = MathF.Cos(angle) * speed;
        b.VelocityY = MathF.Sin(angle) * speed;
    }

    // =====================================================================
    // Dispose
    // =====================================================================

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _gradientPaint.Dispose();
        _dotPaint.Dispose();
        _blockPaint.Dispose();

        _linePaint.PathEffect?.Dispose();
        _linePaint.Dispose();

        _vignettePaint.Dispose();

        _bgShader?.Dispose();
        _vignetteShader?.Dispose();
    }
}
