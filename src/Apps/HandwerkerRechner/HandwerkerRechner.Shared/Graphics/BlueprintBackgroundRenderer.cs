using System;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Animierter Blueprint-Hintergrund fuer den HandwerkerRechner.
/// Rendert 5 Layer: Vertikaler Gradient, Blueprint-Grid, Massband-Markierungen,
/// Floating Tool-Silhouetten, Radiale Vignette.
/// Instance-basiert mit GC-freiem Render-Loop (Struct-Partikel, gecachte Paints, Path.Reset).
/// Wird von einem 5fps DispatcherTimer in der MainView invalidiert.
/// </summary>
public sealed class BlueprintBackgroundRenderer : IDisposable
{
    private bool _disposed;

    // =====================================================================
    // Farben (aus AppPalette + Konzept)
    // =====================================================================

    private static readonly SKColor GradientTop = SKColor.Parse("#223656");
    private static readonly SKColor GradientMid = SKColor.Parse("#182640");
    private static readonly SKColor GradientBot = SKColor.Parse("#141E48");

    // Grid-Linien
    private static readonly SKColor GridColorFine = new(255, 255, 255, 7);   // Weiss Alpha ~3%
    private static readonly SKColor GridColorThick = new(255, 255, 255, 12); // Weiss Alpha ~5%

    // Tool-Silhouetten: Blueprint-Blau mit sehr niedrigem Alpha
    private static readonly SKColor SilhouetteColor = new(59, 130, 246, 12); // #3B82F6 Alpha ~5%

    // Massband-Markierungen
    private static readonly SKColor RulerColor = new(255, 255, 255, 8); // Weiss Alpha ~3%

    // =====================================================================
    // Partikel-System (max 8 Werkzeug-Silhouetten)
    // =====================================================================

    private enum ToolType : byte { Hammer, Wrench, Angle, Screw }

    private struct ToolParticle
    {
        public float X, Y;         // Absolute Position
        public float VelocityX, VelocityY;
        public float Rotation;     // Drehwinkel in Radiant
        public float RotationSpeed;
        public float Size;
        public ToolType Type;
        public bool Active;
    }

    private const int MaxParticles = 8;
    private readonly ToolParticle[] _particles = new ToolParticle[MaxParticles];
    private bool _particlesInitialized;

    // =====================================================================
    // Gecachte Paints (0 GC im Render-Loop)
    // =====================================================================

    private readonly SKPaint _gradientPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    private readonly SKPaint _gridPaintFine = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f, Color = GridColorFine };
    private readonly SKPaint _gridPaintThick = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = GridColorThick };
    private readonly SKPaint _silhouettePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, Color = SilhouetteColor };
    private readonly SKPaint _rulerPaint = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f, Color = RulerColor };
    private readonly SKPaint _vignettePaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };

    // =====================================================================
    // Gecachter Path (fuer Silhouetten wiederverwendet)
    // =====================================================================

    private readonly SKPath _toolPath = new();

    // =====================================================================
    // Shader-Cache (nur bei Groessenaenderung neu erstellt)
    // =====================================================================

    private SKShader? _bgShader;
    private SKShader? _vignetteShader;
    private float _cachedW, _cachedH;

    // =====================================================================
    // Update (vom DispatcherTimer aufgerufen, ~5fps)
    // =====================================================================

    /// <summary>
    /// Aktualisiert Partikel-Positionen und Rotation.
    /// deltaTime in Sekunden (bei 5fps = 0.2f).
    /// </summary>
    public void Update(float deltaTime)
    {
        // Partikel initialisieren (einmalig, wenn Bounds bekannt)
        if (!_particlesInitialized && _cachedW > 1f && _cachedH > 1f)
        {
            InitializeParticles();
            _particlesInitialized = true;
        }

        for (int i = 0; i < MaxParticles; i++)
        {
            if (!_particles[i].Active) continue;
            ref var p = ref _particles[i];

            // Position aktualisieren (sehr langsames Driften)
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.Rotation += p.RotationSpeed * deltaTime;

            // Wraparound: Partikel die den sichtbaren Bereich verlassen
            // werden auf der gegenueberliegenden Seite repositioniert
            if (p.X < -50f) p.X = _cachedW + 40f;
            if (p.X > _cachedW + 50f) p.X = -40f;
            if (p.Y < -50f) p.Y = _cachedH + 40f;
            if (p.Y > _cachedH + 50f) p.Y = -40f;
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
        if (bounds.Width < 1f || bounds.Height < 1f) return;

        RenderGradient(canvas, bounds);
        RenderBlueprintGrid(canvas, bounds, time);
        RenderRulerMarks(canvas, bounds);
        RenderToolSilhouettes(canvas, bounds);
        RenderVignette(canvas, bounds);
    }

    // =====================================================================
    // Layer 1: Vertikaler 3-Farben-Gradient
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
    // Layer 2: Blueprint-Grid (leichtes Driften via sin)
    // =====================================================================

    private void RenderBlueprintGrid(SKCanvas canvas, SKRect bounds, float time)
    {
        float left = bounds.Left;
        float top = bounds.Top;
        float right = bounds.Right;
        float bottom = bounds.Bottom;

        // Leichtes Driften: 1-2px ueber sin(time)
        float driftX = MathF.Sin(time * 0.3f) * 1.5f;
        float driftY = MathF.Cos(time * 0.25f) * 1.0f;

        // Feine Linien alle 40px
        float spacing = 40f;
        float startX = left - spacing + (driftX % spacing);
        float startY = top - spacing + (driftY % spacing);

        // Vertikale feine Linien
        for (float x = startX; x <= right + spacing; x += spacing)
            canvas.DrawLine(x, top, x, bottom, _gridPaintFine);

        // Horizontale feine Linien
        for (float y = startY; y <= bottom + spacing; y += spacing)
            canvas.DrawLine(left, y, right, y, _gridPaintFine);

        // Dickere Sektionslinien alle 200px
        float sectionSpacing = 200f;
        float sectionStartX = left - sectionSpacing + (driftX % sectionSpacing);
        float sectionStartY = top - sectionSpacing + (driftY % sectionSpacing);

        for (float x = sectionStartX; x <= right + sectionSpacing; x += sectionSpacing)
            canvas.DrawLine(x, top, x, bottom, _gridPaintThick);

        for (float y = sectionStartY; y <= bottom + sectionSpacing; y += sectionSpacing)
            canvas.DrawLine(left, y, right, y, _gridPaintThick);
    }

    // =====================================================================
    // Layer 3: Massband-Markierungen am rechten Rand
    // =====================================================================

    private void RenderRulerMarks(SKCanvas canvas, SKRect bounds)
    {
        float right = bounds.Right;
        float top = bounds.Top;
        float bottom = bounds.Bottom;

        // Markierungen alle 10px, laengere alle 50px, noch laengere alle 100px
        for (float y = top; y <= bottom; y += 10f)
        {
            float tickLen;
            float mod100 = y % 100f;
            float mod50 = y % 50f;

            if (MathF.Abs(mod100) < 0.5f)
                tickLen = 12f; // Lange Markierung alle 100px
            else if (MathF.Abs(mod50) < 0.5f)
                tickLen = 8f;  // Mittlere Markierung alle 50px
            else
                tickLen = 4f;  // Kurze Markierung alle 10px

            canvas.DrawLine(right - tickLen, y, right, y, _rulerPaint);
        }
    }

    // =====================================================================
    // Layer 4: Floating Tool-Silhouetten (8 Partikel)
    // =====================================================================

    private void RenderToolSilhouettes(SKCanvas canvas, SKRect bounds)
    {
        for (int i = 0; i < MaxParticles; i++)
        {
            if (!_particles[i].Active) continue;
            ref var p = ref _particles[i];

            // Pruefen ob Partikel im sichtbaren Bereich liegt
            if (p.X < bounds.Left - 40f || p.X > bounds.Right + 40f) continue;
            if (p.Y < bounds.Top - 40f || p.Y > bounds.Bottom + 40f) continue;

            canvas.Save();
            canvas.Translate(p.X, p.Y);
            canvas.RotateRadians(p.Rotation);

            DrawToolSilhouette(canvas, p.Type, p.Size);

            canvas.Restore();
        }
    }

    /// <summary>
    /// Zeichnet eine Werkzeug-Silhouette am Ursprung (0,0).
    /// Verwendet _toolPath.Reset() statt new SKPath().
    /// </summary>
    private void DrawToolSilhouette(SKCanvas canvas, ToolType type, float size)
    {
        float half = size * 0.5f;

        switch (type)
        {
            case ToolType.Hammer:
                // T-Form: Stiel vertikal + Kopf horizontal
                _toolPath.Reset();
                // Stiel (vertikal)
                _toolPath.MoveTo(0f, half);
                _toolPath.LineTo(0f, -half * 0.3f);
                // Kopf (horizontal, oben)
                _toolPath.MoveTo(-half * 0.6f, -half * 0.3f);
                _toolPath.LineTo(half * 0.6f, -half * 0.3f);
                _toolPath.LineTo(half * 0.6f, -half);
                _toolPath.LineTo(-half * 0.6f, -half);
                _toolPath.Close();
                canvas.DrawPath(_toolPath, _silhouettePaint);
                break;

            case ToolType.Wrench:
                // Schmaler Stiel mit Kreis am Ende (Maulschluessel)
                _toolPath.Reset();
                // Stiel
                _toolPath.MoveTo(0f, half);
                _toolPath.LineTo(0f, -half * 0.4f);
                canvas.DrawPath(_toolPath, _silhouettePaint);
                // Oeffnung oben (Kreis-Segment)
                canvas.DrawCircle(0f, -half * 0.65f, half * 0.35f, _silhouettePaint);
                break;

            case ToolType.Angle:
                // L-Form (Winkellineal / Anschlagwinkel)
                _toolPath.Reset();
                // Horizontaler Schenkel
                _toolPath.MoveTo(-half, half * 0.3f);
                _toolPath.LineTo(half * 0.3f, half * 0.3f);
                // Vertikaler Schenkel
                _toolPath.LineTo(half * 0.3f, -half);
                canvas.DrawPath(_toolPath, _silhouettePaint);
                break;

            case ToolType.Screw:
                // Kreis mit Kreuzschlitz
                canvas.DrawCircle(0f, 0f, half * 0.5f, _silhouettePaint);
                // Kreuzschlitz
                _toolPath.Reset();
                float slotLen = half * 0.35f;
                _toolPath.MoveTo(-slotLen, 0f);
                _toolPath.LineTo(slotLen, 0f);
                _toolPath.MoveTo(0f, -slotLen);
                _toolPath.LineTo(0f, slotLen);
                canvas.DrawPath(_toolPath, _silhouettePaint);
                break;
        }
    }

    // =====================================================================
    // Layer 5: Radiale Vignette (dunkle Ecken)
    // =====================================================================

    private void RenderVignette(SKCanvas canvas, SKRect bounds)
    {
        if (_vignetteShader == null) return;

        _vignettePaint.Shader = _vignetteShader;
        canvas.DrawRect(bounds, _vignettePaint);
        _vignettePaint.Shader = null;
    }

    // =====================================================================
    // Shader-Erzeugung (nur bei Groessenaenderung)
    // =====================================================================

    private void RecreateShaders(float w, float h)
    {
        _bgShader?.Dispose();
        _bgShader = SKShader.CreateLinearGradient(
            new SKPoint(w * 0.5f, 0f),
            new SKPoint(w * 0.5f, h),
            new[] { GradientTop, GradientMid, GradientBot },
            new[] { 0f, 0.5f, 1f },
            SKShaderTileMode.Clamp);

        _vignetteShader?.Dispose();
        float radius = MathF.Max(w, h) * 0.8f;
        _vignetteShader = SKShader.CreateRadialGradient(
            new SKPoint(w * 0.5f, h * 0.5f),
            radius,
            new[] { SKColors.Transparent, new SKColor(0, 0, 0, 55) },
            new[] { 0.45f, 1.0f },
            SKShaderTileMode.Clamp);

        _cachedW = w;
        _cachedH = h;
    }

    // =====================================================================
    // Partikel-Initialisierung (einmalig nach Bounds bekannt)
    // =====================================================================

    private void InitializeParticles()
    {
        for (int i = 0; i < MaxParticles; i++)
        {
            ref var p = ref _particles[i];
            p.Active = true;
            p.Type = (ToolType)(i % 4); // Gleichmaessig verteilt
            p.Size = 20f + Random.Shared.NextSingle() * 10f; // 20-30px

            // Ueber die gesamte Flaeche verteilt
            p.X = Random.Shared.NextSingle() * _cachedW;
            p.Y = Random.Shared.NextSingle() * _cachedH;

            // Sehr langsames Driften (2-6 px/s)
            float speed = 2f + Random.Shared.NextSingle() * 4f;
            float angle = Random.Shared.NextSingle() * MathF.Tau;
            p.VelocityX = MathF.Cos(angle) * speed;
            p.VelocityY = MathF.Sin(angle) * speed;

            // Langsame Rotation
            p.Rotation = Random.Shared.NextSingle() * MathF.Tau;
            p.RotationSpeed = (-0.15f + Random.Shared.NextSingle() * 0.3f); // -0.15 bis +0.15 rad/s
        }
    }

    // =====================================================================
    // Dispose
    // =====================================================================

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _gradientPaint.Dispose();
        _gridPaintFine.Dispose();
        _gridPaintThick.Dispose();
        _silhouettePaint.Dispose();
        _rulerPaint.Dispose();
        _vignettePaint.Dispose();

        _toolPath.Dispose();

        _bgShader?.Dispose();
        _vignetteShader?.Dispose();
    }
}
