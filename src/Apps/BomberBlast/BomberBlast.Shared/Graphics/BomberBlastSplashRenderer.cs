using SkiaSharp;
using MeineApps.UI.SkiaSharp.SplashScreen;

namespace BomberBlast.Graphics;

/// <summary>
/// Splash-Screen-Renderer "Die Bombe" für BomberBlast.
/// Zeigt eine Cartoon-Bombe mit brennender Lunte (wird kürzer mit Progress),
/// Feuer-Partikel, Rauch-Wisps und einen Explosions-Flash bei ~95% Progress.
/// Feuriger Rot→Gelb Fortschrittsbalken.
/// </summary>
public sealed class BomberBlastSplashRenderer : SplashRendererBase
{
    // --- Partikel-Konfiguration ---
    private const int MaxFireParticles = 20;
    private const int MaxSmokeParticles = 8;
    private const int MaxFuseSparkles = 4;

    // --- Partikel-Pools (Struct-Arrays, kein GC-Druck) ---
    private readonly FireParticle[] _fireParticles = new FireParticle[MaxFireParticles];
    private readonly SmokeParticle[] _smokeParticles = new SmokeParticle[MaxSmokeParticles];
    private int _activeFireParticles;

    // --- Explosions-Flash ---
    private float _flashAlpha;
    private float _flashRadius;
    private bool _flashTriggered;

    // --- Gecachte Paints (kein per-frame Allokation) ---
    private readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _titlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _titleGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _bombBodyPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _bombStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private readonly SKPaint _bombHighlightPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _fusePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3f };
    private readonly SKPaint _fuseGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _fireParticlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _smokeParticlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _flashPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _fuseSparkPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // --- Gecachter Path für Lunte ---
    private readonly SKPath _fusePath = new();

    // --- Gecachte Fonts ---
    private readonly SKFont _titleFont = new() { Embolden = true, Size = 32f };

    // --- Gecachte MaskFilter ---
    private SKMaskFilter? _titleGlowFilter;
    private SKMaskFilter? _fuseGlowFilter;

    // --- Gecachte Hintergrund-Shader (vermeidet per-frame Allokation) ---
    private SKShader? _bgShader;
    private SKShader? _vignetteShader;
    private float _cachedW, _cachedH;
    private static readonly SKColor[] BgGradientColors = { BgTop, BgBottom };
    private static readonly SKColor[] VignetteGradientColors = { new(0xFF, 0x8C, 0x00, 20), SKColors.Transparent };

    // --- Farb-Konstanten ---
    private static readonly SKColor BgTop = new(0x1A, 0x08, 0x08);
    private static readonly SKColor BgBottom = new(0x0D, 0x04, 0x04);
    private static readonly SKColor BombBody = new(0x1A, 0x1A, 0x1A);
    private static readonly SKColor BombStroke = new(0x33, 0x33, 0x33);
    private static readonly SKColor FuseColor = new(0x8B, 0x69, 0x14);
    private static readonly SKColor FireYellow = new(0xFB, 0xBF, 0x24);
    private static readonly SKColor FireOrange = new(0xF9, 0x73, 0x16);
    private static readonly SKColor FireRed = new(0xDC, 0x26, 0x26);

    // --- Structs ---
    private struct FireParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Radius;
    }

    private struct SmokeParticle
    {
        public float X, Y, Alpha, Speed, Radius, Phase;
    }

    private void InitializeSmoke(float w, float h)
    {
        if (IsInitialized) return;
        IsInitialized = true;

        var cx = w / 2f;
        var bombCy = h * 0.43f;

        for (var i = 0; i < MaxSmokeParticles; i++)
        {
            _smokeParticles[i] = new SmokeParticle
            {
                X = cx + ((float)Rng.NextDouble() - 0.5f) * 60f,
                Y = bombCy + ((float)Rng.NextDouble() - 0.5f) * 40f,
                Alpha = 15f + (float)Rng.NextDouble() * 25f,
                Speed = 8f + (float)Rng.NextDouble() * 12f,
                Radius = 4f + (float)Rng.NextDouble() * 4f,
                Phase = (float)(Rng.NextDouble() * Math.PI * 2)
            };
        }
    }

    // ═══════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════

    protected override void OnUpdate(float deltaTime)
    {
        // Explosions-Flash
        if (!_flashTriggered && RenderedProgress >= 0.95f)
        {
            _flashTriggered = true;
            _flashAlpha = 255f;
            _flashRadius = 10f;
        }
        if (_flashAlpha > 0f)
        {
            _flashAlpha -= deltaTime * 800f;
            _flashRadius += deltaTime * 400f;
            if (_flashAlpha < 0f) _flashAlpha = 0f;
        }

        // Feuer-Partikel erzeugen (kontinuierlich nahe Lunte/Bombe)
        SpawnFireParticles(deltaTime);

        // Feuer-Partikel Update
        for (var i = 0; i < _activeFireParticles; i++)
        {
            ref var p = ref _fireParticles[i];
            p.Life -= deltaTime;
            if (p.Life <= 0f)
            {
                // Kompaktierung
                if (i < _activeFireParticles - 1)
                    _fireParticles[i] = _fireParticles[_activeFireParticles - 1];
                _activeFireParticles--;
                i--;
                continue;
            }
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            // Leichte Schwerkraft (fallen nach unten)
            p.VelocityY += 30f * deltaTime;
        }

        // Rauch-Partikel Update
        for (var i = 0; i < MaxSmokeParticles; i++)
        {
            ref var s = ref _smokeParticles[i];
            s.Phase += deltaTime * 0.8f;
            s.Y -= s.Speed * deltaTime;
            s.Alpha -= 5f * deltaTime;

            if (s.Alpha <= 0f || s.Y < 0f)
            {
                // Zurücksetzen
                s.Y = 0f; // Wird im Render auf echte Position gesetzt
                s.Alpha = 15f + (float)Rng.NextDouble() * 25f;
                s.Speed = 8f + (float)Rng.NextDouble() * 12f;
                s.Radius = 4f + (float)Rng.NextDouble() * 4f;
            }
        }
    }

    // Spawn-Timer-Feld (vermeidet Spawn-Burst bei niedrigem deltaTime)
    private float _fireSpawnAccumulator;

    private void SpawnFireParticles(float deltaTime)
    {
        _fireSpawnAccumulator += deltaTime;
        float spawnInterval = 0.05f; // ~20 Partikel/Sekunde

        while (_fireSpawnAccumulator >= spawnInterval && _activeFireParticles < MaxFireParticles)
        {
            _fireSpawnAccumulator -= spawnInterval;

            var angle = (float)(Rng.NextDouble() * Math.PI * 2);
            var speed = 20f + (float)Rng.NextDouble() * 60f;

            _fireParticles[_activeFireParticles++] = new FireParticle
            {
                X = 0f, // Relativ zur Luntenspitze, wird im Render versetzt
                Y = 0f,
                VelocityX = MathF.Cos(angle) * speed * 0.5f,
                VelocityY = -MathF.Abs(MathF.Sin(angle)) * speed - 15f, // Primär nach oben
                Life = 0.4f + (float)Rng.NextDouble() * 0.5f,
                MaxLife = 0.9f,
                Radius = 1.5f + (float)Rng.NextDouble() * 1.5f
            };
        }
    }

    // ═══════════════════════════════════════════════
    // RENDER
    // ═══════════════════════════════════════════════

    protected override void OnRender(SKCanvas canvas, SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;

        InitializeSmoke(w, h);

        _titleGlowFilter ??= SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);
        _fuseGlowFilter ??= SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f);

        RenderBackground(canvas, bounds, w, h);
        RenderTitle(canvas, w, h);
        RenderSmoke(canvas, w, h);
        RenderBomb(canvas, w, h);
        RenderFuse(canvas, w, h);
        RenderFireParticles(canvas, w, h);

        // Fortschrittsbalken bei y ~ 72%
        var barWidth = Math.Min(260f, w * 0.6f);
        DrawProgressBar(canvas, w, h * 0.72f, barWidth, 8f, 4f,
            FireRed, FireYellow, BgTop);

        // Status-Text bei y ~ 77%
        DrawStatusText(canvas, w, h * 0.77f);

        // Version bei y ~ 92%
        DrawVersion(canvas, w, h * 0.92f);

        // Explosions-Flash (über allem)
        RenderFlash(canvas, w, h);
    }

    // ═══════════════════════════════════════════════
    // HINTERGRUND
    // ═══════════════════════════════════════════════

    private void RenderBackground(SKCanvas canvas, SKRect bounds, float w, float h)
    {
        // Shader bei Größenänderung neu erstellen (sonst gecacht)
        if (_bgShader == null || w != _cachedW || h != _cachedH)
        {
            _cachedW = w;
            _cachedH = h;
            _bgShader?.Dispose();
            _vignetteShader?.Dispose();

            _bgShader = SKShader.CreateLinearGradient(
                new SKPoint(w / 2f, 0f),
                new SKPoint(w / 2f, h),
                BgGradientColors,
                null, SKShaderTileMode.Clamp);

            float minDim = Math.Min(w, h);
            _vignetteShader = SKShader.CreateRadialGradient(
                new SKPoint(w / 2f, h * 0.43f),
                minDim * 0.6f,
                VignetteGradientColors,
                null, SKShaderTileMode.Clamp);
        }

        // Dunkles Feuer-Rot Gradient
        _bgPaint.Shader = _bgShader;
        canvas.DrawRect(bounds, _bgPaint);
        _bgPaint.Shader = null;

        // Explosions-Vignette
        _bgPaint.Shader = _vignetteShader;
        canvas.DrawRect(bounds, _bgPaint);
        _bgPaint.Shader = null;
    }

    // ═══════════════════════════════════════════════
    // TITEL
    // ═══════════════════════════════════════════════

    private void RenderTitle(SKCanvas canvas, float w, float h)
    {
        var titleY = h * 0.12f;

        _titleFont.Size = Math.Min(32f, w * 0.075f);

        // Roter Glow hinter dem Titel
        _titleGlowPaint.Color = new SKColor(0xDC, 0x26, 0x26, 40);
        _titleGlowPaint.MaskFilter = _titleGlowFilter;
        DrawCenteredText(canvas, AppName, titleY, _titleFont, _titleGlowPaint, w);
        _titleGlowPaint.MaskFilter = null;

        // Weißer Titel
        _titlePaint.Color = SKColors.White;
        DrawCenteredText(canvas, AppName, titleY, _titleFont, _titlePaint, w);
    }

    // ═══════════════════════════════════════════════
    // BOMBE (Cartoon-Stil)
    // ═══════════════════════════════════════════════

    private void RenderBomb(SKCanvas canvas, float w, float h)
    {
        var cx = w / 2f;
        var cy = h * 0.43f;
        float bombRadius = 40f;

        // Bomben-Körper (schwarzer Kreis)
        _bombBodyPaint.Color = BombBody;
        canvas.DrawCircle(cx, cy, bombRadius, _bombBodyPaint);

        // Dunkler Rand
        _bombStrokePaint.Color = BombStroke;
        canvas.DrawCircle(cx, cy, bombRadius, _bombStrokePaint);

        // Glanzpunkt oben-links (weißer Kreis)
        _bombHighlightPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 80);
        canvas.DrawCircle(cx - bombRadius * 0.3f, cy - bombRadius * 0.3f, 8f, _bombHighlightPaint);
    }

    // ═══════════════════════════════════════════════
    // LUNTE (wird kürzer mit Progress)
    // ═══════════════════════════════════════════════

    private void RenderFuse(SKCanvas canvas, float w, float h)
    {
        var cx = w / 2f;
        var cy = h * 0.43f;
        float bombRadius = 40f;

        // Lunte startet oben-rechts am Bomben-Rand (ca. 45 Grad)
        float fuseStartAngle = -MathF.PI / 4f; // -45 Grad (oben-rechts)
        float fuseStartX = cx + MathF.Cos(fuseStartAngle) * bombRadius;
        float fuseStartY = cy + MathF.Sin(fuseStartAngle) * bombRadius;

        // Gesamtlänge der Lunte = maxLänge * (1 - RenderedProgress)
        float maxFuseLength = 50f;
        float currentLength = maxFuseLength * (1f - Math.Clamp(RenderedProgress, 0f, 1f));

        if (currentLength <= 1f) return; // Lunte komplett verbrannt

        // Endpunkt der Lunte (nach oben-rechts, leicht geschwungen)
        float fuseEndX = fuseStartX + currentLength * 0.7f;
        float fuseEndY = fuseStartY - currentLength * 0.7f;

        // Kontrollpunkt für Bezier-Kurve (leichte Schwingung)
        float ctrlX = fuseStartX + currentLength * 0.5f;
        float ctrlY = fuseStartY - currentLength * 0.2f + MathF.Sin(Time * 3f) * 3f;

        // Lunte zeichnen
        _fusePaint.Color = FuseColor;
        _fusePath.Reset();
        _fusePath.MoveTo(fuseStartX, fuseStartY);
        _fusePath.QuadTo(ctrlX, ctrlY, fuseEndX, fuseEndY);
        canvas.DrawPath(_fusePath, _fusePaint);

        // Glühender Punkt am brennenden Ende
        float glowPulse = 0.7f + 0.3f * MathF.Sin(Time * 12f);
        float glowRadius = 4f * glowPulse;

        // Gelb-oranges Leuchten
        _fuseGlowPaint.Color = new SKColor(0xFF, 0xCC, 0x00, (byte)(200 * glowPulse));
        _fuseGlowPaint.MaskFilter = _fuseGlowFilter;
        canvas.DrawCircle(fuseEndX, fuseEndY, glowRadius * 2f, _fuseGlowPaint);
        _fuseGlowPaint.MaskFilter = null;

        // Heller Kern
        _fuseGlowPaint.Color = new SKColor(0xFF, 0xFF, 0x88, 255);
        canvas.DrawCircle(fuseEndX, fuseEndY, glowRadius * 0.7f, _fuseGlowPaint);

        // Mini-Funken um die Luntenspitze
        RenderFuseSparkles(canvas, fuseEndX, fuseEndY);
    }

    private void RenderFuseSparkles(SKCanvas canvas, float tipX, float tipY)
    {
        for (int i = 0; i < MaxFuseSparkles; i++)
        {
            float phase = (Time * 4f + i * 1.57f) % 1f; // 1.57 = PI/2
            if (phase > 0.6f) continue;

            float angle = (i * 90f + Time * 200f) * MathF.PI / 180f;
            float dist = 4f + phase * 10f;
            float sx = tipX + MathF.Cos(angle) * dist;
            float sy = tipY + MathF.Sin(angle) * dist;

            byte alpha = (byte)((1f - phase / 0.6f) * 220);
            float size = (1f - phase / 0.6f) * 1.5f;

            _fuseSparkPaint.Color = FireYellow.WithAlpha(alpha);
            canvas.DrawCircle(sx, sy, size, _fuseSparkPaint);
        }
    }

    // ═══════════════════════════════════════════════
    // FEUER-PARTIKEL
    // ═══════════════════════════════════════════════

    private void RenderFireParticles(SKCanvas canvas, float w, float h)
    {
        if (_activeFireParticles == 0) return;

        // Feuer-Partikel-Ursprung: Luntenspitze
        var cx = w / 2f;
        var cy = h * 0.43f;
        float bombRadius = 40f;
        float fuseStartAngle = -MathF.PI / 4f;
        float fuseStartX = cx + MathF.Cos(fuseStartAngle) * bombRadius;
        float fuseStartY = cy + MathF.Sin(fuseStartAngle) * bombRadius;
        float maxFuseLength = 50f;
        float currentLength = maxFuseLength * (1f - Math.Clamp(RenderedProgress, 0f, 1f));
        float originX = fuseStartX + currentLength * 0.7f;
        float originY = fuseStartY - currentLength * 0.7f;

        for (var i = 0; i < _activeFireParticles; i++)
        {
            ref var p = ref _fireParticles[i];
            float px = originX + p.X;
            float py = originY + p.Y;

            float lifeRatio = p.Life / p.MaxLife;

            // Farb-Interpolation: Gelb→Orange→Rot basierend auf Lebensdauer
            SKColor color;
            if (lifeRatio > 0.6f)
            {
                // Gelb→Orange
                float t = (lifeRatio - 0.6f) / 0.4f;
                color = InterpolateColor(FireOrange, FireYellow, t);
            }
            else
            {
                // Orange→Rot
                float t = lifeRatio / 0.6f;
                color = InterpolateColor(FireRed, FireOrange, t);
            }

            byte alpha = (byte)(255f * Math.Min(lifeRatio * 2f, 1f)); // Schnelles Fade-In
            _fireParticlePaint.Color = color.WithAlpha(alpha);
            canvas.DrawCircle(px, py, p.Radius * lifeRatio + 0.5f, _fireParticlePaint);
        }
    }

    // ═══════════════════════════════════════════════
    // RAUCH-WISPS
    // ═══════════════════════════════════════════════

    private void RenderSmoke(SKCanvas canvas, float w, float h)
    {
        var cx = w / 2f;
        var bombCy = h * 0.43f;

        for (var i = 0; i < MaxSmokeParticles; i++)
        {
            ref var s = ref _smokeParticles[i];

            // Zurücksetzen bei Y=0
            if (s.Y <= 0f)
            {
                s.X = cx + ((float)Rng.NextDouble() - 0.5f) * 60f;
                s.Y = bombCy - 10f + ((float)Rng.NextDouble() - 0.5f) * 20f;
            }

            if (s.Alpha <= 0f) continue;

            // Sinus-Drift
            float driftX = MathF.Sin(s.Phase) * 12f;

            // Grau mit niedrigem Alpha
            byte gray = (byte)(0x66 + (s.Alpha / 40f) * 0x22);
            _smokeParticlePaint.Color = new SKColor(gray, gray, gray, (byte)Math.Clamp(s.Alpha, 0, 40));
            canvas.DrawCircle(s.X + driftX, s.Y, s.Radius, _smokeParticlePaint);
        }
    }

    // ═══════════════════════════════════════════════
    // EXPLOSIONS-FLASH
    // ═══════════════════════════════════════════════

    private void RenderFlash(SKCanvas canvas, float w, float h)
    {
        if (_flashAlpha <= 0f) return;

        var cx = w / 2f;
        var cy = h * 0.43f; // Bomben-Zentrum

        byte alpha = (byte)Math.Clamp(_flashAlpha, 0, 255);

        // Expandierender weißer Kreis
        _flashPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, alpha);
        canvas.DrawCircle(cx, cy, _flashRadius, _flashPaint);

        // Gesamter Canvas-Flash (halbe Alpha-Intensität)
        byte canvasAlpha = (byte)Math.Clamp(_flashAlpha * 0.4f, 0, 200);
        _flashPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, canvasAlpha);
        canvas.DrawRect(0f, 0f, w, h, _flashPaint);
    }

    // ═══════════════════════════════════════════════
    // HILFSFUNKTIONEN
    // ═══════════════════════════════════════════════

    /// <summary>Lineare Farbinterpolation zwischen zwei SKColors.</summary>
    private static SKColor InterpolateColor(SKColor from, SKColor to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new SKColor(
            (byte)(from.Red + (to.Red - from.Red) * t),
            (byte)(from.Green + (to.Green - from.Green) * t),
            (byte)(from.Blue + (to.Blue - from.Blue) * t));
    }

    // ═══════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════

    protected override void OnDispose()
    {
        _bgPaint.Dispose();
        _titlePaint.Dispose();
        _titleGlowPaint.Dispose();
        _bombBodyPaint.Dispose();
        _bombStrokePaint.Dispose();
        _bombHighlightPaint.Dispose();
        _fusePaint.Dispose();
        _fuseGlowPaint.Dispose();
        _fireParticlePaint.Dispose();
        _smokeParticlePaint.Dispose();
        _flashPaint.Dispose();
        _fuseSparkPaint.Dispose();
        _fusePath.Dispose();
        _titleFont.Dispose();
        _titleGlowFilter?.Dispose();
        _fuseGlowFilter?.Dispose();
        _bgShader?.Dispose();
        _vignetteShader?.Dispose();
    }
}
