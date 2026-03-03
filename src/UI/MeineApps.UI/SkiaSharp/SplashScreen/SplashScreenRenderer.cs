using SkiaSharp;

namespace MeineApps.UI.SkiaSharp.SplashScreen;

/// <summary>
/// Standard-Splash-Screen-Renderer (generisch, Theme-basiert).
/// Rendert: Gradient-Hintergrund, 24 schwebende Glow-Partikel,
/// pulsierender App-Name, Version, animierter Fortschrittsbalken, Status-Text.
/// Wird als Fallback verwendet wenn keine app-spezifische Implementierung gesetzt wird.
/// Alle SKPaint/SKFont sind gecacht (kein per-frame Allokation).
/// </summary>
public class SplashScreenRenderer : SplashRendererBase
{
    private const int MaxParticles = 24;

    // --- Partikel-Pool ---
    private readonly SplashParticle[] _particles = new SplashParticle[MaxParticles];

    // --- Gecachte Paints (kein per-frame Allokation) ---
    private readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _particlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _barBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _barFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _titlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _percentPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // --- Gecachte Fonts ---
    private readonly SKFont _titleFont = new() { Embolden = true, Size = 32f };
    private readonly SKFont _percentFont = new() { Size = 13f };

    // --- Gecachte Objekte ---
    private readonly SKPath _barPath = new();
    private SKMaskFilter? _particleGlow;
    private SKMaskFilter? _titleGlow;

    // --- Render-State ---
    private float _glowPhase;

    /// <summary>
    /// Initialisiert die Partikel mit zufälligen Positionen und Parametern.
    /// </summary>
    private void InitializeParticles()
    {
        if (IsInitialized) return;
        IsInitialized = true;

        for (var i = 0; i < MaxParticles; i++)
        {
            var x = (float)Rng.NextDouble();
            _particles[i] = new SplashParticle
            {
                X = x,
                BaseX = x,
                Y = (float)Rng.NextDouble(),
                Radius = 2f + (float)Rng.NextDouble() * 4f,
                Alpha = 40f + (float)Rng.NextDouble() * 80f,
                Phase = (float)(Rng.NextDouble() * Math.PI * 2),
                SpeedX = 0f,
                SpeedY = -0.003f - (float)Rng.NextDouble() * 0.007f,
                FloatAmplitude = 5f + (float)Rng.NextDouble() * 15f,
                FloatFrequency = 0.5f + (float)Rng.NextDouble() * 1.5f
            };
        }
    }

    /// <summary>
    /// Aktualisiert Partikel-Animationen und Glow-Phase.
    /// </summary>
    protected override void OnUpdate(float deltaTime)
    {
        // Glow-Puls-Phase (~1.5 Hz)
        _glowPhase += deltaTime * MathF.PI * 3f;
        if (_glowPhase > MathF.PI * 2f) _glowPhase -= MathF.PI * 2f;

        // Partikel bewegen
        for (var i = 0; i < MaxParticles; i++)
        {
            ref var p = ref _particles[i];
            p.Phase += deltaTime * p.FloatFrequency * MathF.PI * 2f;
            p.Y += p.SpeedY * deltaTime;

            // Wrap-around: Partikel die oben rausgehen unten wieder einsetzen
            if (p.Y < -0.05f)
            {
                p.Y = 1.05f;
                p.BaseX = (float)Rng.NextDouble();
            }

            // Horizontale Sinus-Oszillation
            p.X = p.BaseX + MathF.Sin(p.Phase) * (p.FloatAmplitude / 1000f);
        }
    }

    /// <summary>
    /// Rendert den kompletten Standard-Splash-Screen auf den Canvas.
    /// </summary>
    protected override void OnRender(SKCanvas canvas, SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;

        InitializeParticles();

        // Lazy-Init für MaskFilter
        _particleGlow ??= SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f);
        _titleGlow ??= SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

        RenderBackground(canvas, bounds);
        RenderParticles(canvas, w, h);
        RenderAppName(canvas, w, h);
        RenderProgressBar(canvas, w, h);
        RenderStatusText(canvas, w, h);
    }

    private void RenderBackground(SKCanvas canvas, SKRect bounds)
    {
        // Vertikaler Gradient: Background → Surface (dunkleres Ende)
        var bgTop = SkiaThemeHelper.Background;
        var bgBottom = SkiaThemeHelper.AdjustBrightness(SkiaThemeHelper.Surface, 0.6f);

        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.MidX, bounds.Top),
            new SKPoint(bounds.MidX, bounds.Bottom),
            new[] { bgTop, bgBottom },
            new[] { 0f, 1f },
            SKShaderTileMode.Clamp);

        _bgPaint.Shader = shader;
        canvas.DrawRect(bounds, _bgPaint);
        _bgPaint.Shader = null;
    }

    private void RenderParticles(SKCanvas canvas, float w, float h)
    {
        for (var i = 0; i < MaxParticles; i++)
        {
            ref var p = ref _particles[i];
            var px = p.X * w;
            var py = p.Y * h;

            // Pulsierendes Alpha
            var alpha = (byte)Math.Clamp(p.Alpha + 20f * MathF.Sin(p.Phase * 0.7f), 20, 140);

            _particlePaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Primary, alpha);
            _particlePaint.MaskFilter = _particleGlow;
            canvas.DrawCircle(px, py, p.Radius, _particlePaint);
        }
        _particlePaint.MaskFilter = null;
    }

    private void RenderAppName(SKCanvas canvas, float w, float h)
    {
        var centerY = h * 0.40f;

        // Pulsierender Glow hinter dem Text
        var glowAlpha = (byte)(30 + 25 * MathF.Sin(_glowPhase));
        _glowPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Primary, glowAlpha);
        _glowPaint.MaskFilter = _titleGlow;

        // Titel messen und zentrieren
        _titleFont.Size = Math.Min(36f, w * 0.08f);
        var titleWidth = _titleFont.MeasureText(AppName);
        var titleX = (w - titleWidth) / 2f;

        // Glow-Kreis hinter dem Titel
        canvas.DrawCircle(w / 2f, centerY, titleWidth * 0.7f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // App-Name zeichnen
        _titlePaint.Color = SkiaThemeHelper.TextPrimary;
        canvas.DrawText(AppName, titleX, centerY + _titleFont.Size * 0.35f, _titleFont, _titlePaint);

        // Version unter dem Namen
        if (!string.IsNullOrEmpty(AppVersion))
        {
            VersionFont.Size = Math.Min(14f, w * 0.035f);
            var versionWidth = VersionFont.MeasureText(AppVersion);
            var versionX = (w - versionWidth) / 2f;
            VersionPaint.Color = SkiaThemeHelper.TextMuted;
            canvas.DrawText(AppVersion, versionX, centerY + _titleFont.Size * 0.35f + 28f, VersionFont, VersionPaint);
        }
    }

    private void RenderProgressBar(SKCanvas canvas, float w, float h)
    {
        var progress = Math.Clamp(RenderedProgress, 0f, 1f);
        var barY = h * 0.62f;
        var barWidth = Math.Min(280f, w * 0.65f);
        var barHeight = 8f;
        var barRadius = 4f;
        var barLeft = (w - barWidth) / 2f;

        // Hintergrund-Track
        _barBgPaint.Color = SkiaThemeHelper.Surface;
        _barPath.Reset();
        _barPath.AddRoundRect(new SKRoundRect(new SKRect(barLeft, barY, barLeft + barWidth, barY + barHeight), barRadius));
        canvas.DrawPath(_barPath, _barBgPaint);

        // Fortschritts-Fill
        if (progress > 0.005f)
        {
            var fillWidth = barWidth * progress;

            using var fillShader = SKShader.CreateLinearGradient(
                new SKPoint(barLeft, barY),
                new SKPoint(barLeft + barWidth, barY),
                new[] { SkiaThemeHelper.Primary, SkiaThemeHelper.Accent },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp);

            _barFillPaint.Shader = fillShader;
            _barPath.Reset();
            _barPath.AddRoundRect(new SKRoundRect(new SKRect(barLeft, barY, barLeft + fillWidth, barY + barHeight), barRadius));
            canvas.DrawPath(_barPath, _barFillPaint);
            _barFillPaint.Shader = null;

            // Glow am Ende des Fortschrittsbalkens
            var glowX = barLeft + fillWidth;
            var glowAlpha = (byte)(60 + 30 * MathF.Sin(_glowPhase * 1.5f));
            _particlePaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Primary, glowAlpha);
            _particlePaint.MaskFilter = _particleGlow;
            canvas.DrawCircle(glowX, barY + barHeight / 2f, 6f, _particlePaint);
            _particlePaint.MaskFilter = null;
        }

        // Prozent-Text rechts
        var percentText = $"{(int)(progress * 100)}%";
        _percentFont.Size = Math.Min(13f, w * 0.033f);
        _percentPaint.Color = SkiaThemeHelper.TextSecondary;
        canvas.DrawText(percentText, barLeft + barWidth + 10f, barY + barHeight / 2f + _percentFont.Size * 0.35f, _percentFont, _percentPaint);
    }

    private void RenderStatusText(SKCanvas canvas, float w, float h)
    {
        if (string.IsNullOrEmpty(StatusText)) return;

        var barY = h * 0.62f;
        var statusY = barY + 30f;

        StatusFont.Size = Math.Min(13f, w * 0.033f);
        var statusWidth = StatusFont.MeasureText(StatusText);
        var statusX = (w - statusWidth) / 2f;

        StatusPaint.Color = SkiaThemeHelper.TextMuted;
        canvas.DrawText(StatusText, statusX, statusY, StatusFont, StatusPaint);
    }

    protected override void OnDispose()
    {
        _bgPaint.Dispose();
        _particlePaint.Dispose();
        _barBgPaint.Dispose();
        _barFillPaint.Dispose();
        _titlePaint.Dispose();
        _glowPaint.Dispose();
        _percentPaint.Dispose();
        _titleFont.Dispose();
        _percentFont.Dispose();
        _barPath.Dispose();
        _particleGlow?.Dispose();
        _titleGlow?.Dispose();
    }
}
