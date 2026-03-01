using SkiaSharp;

namespace MeineApps.UI.SkiaSharp.SplashScreen;

/// <summary>
/// Immersiver Splash-Screen-Renderer mit SkiaSharp.
/// Rendert: Gradient-Hintergrund, 24 schwebende Glow-Partikel,
/// pulsierender App-Name, Version, animierter Fortschrittsbalken, Status-Text.
/// Alle SKPaint/SKFont sind gecacht (kein per-frame Allokation).
/// </summary>
public class SplashScreenRenderer : IDisposable
{
    private const int MaxParticles = 24;

    // --- Partikel-Pool ---
    private readonly SplashParticle[] _particles = new SplashParticle[MaxParticles];
    private bool _particlesInitialized;

    // --- Gecachte Paints (kein per-frame Allokation) ---
    private readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _particlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _barBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _barFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _titlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _versionPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _statusPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _percentPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // --- Gecachte Fonts ---
    private readonly SKFont _titleFont = new() { Embolden = true, Size = 32f };
    private readonly SKFont _versionFont = new() { Size = 14f };
    private readonly SKFont _statusFont = new() { Size = 13f };
    private readonly SKFont _percentFont = new() { Size = 13f };

    // --- Gecachte Objekte ---
    private readonly SKPath _barPath = new();
    private SKMaskFilter? _particleGlow;
    private SKMaskFilter? _titleGlow;

    // --- Render-State ---
    private float _time;
    private float _renderedProgress;
    private float _glowPhase;

    // --- Öffentliche Steuerung ---

    /// <summary>Ziel-Fortschritt 0.0-1.0 (wird smooth interpoliert)</summary>
    public float Progress { get; set; }

    /// <summary>Aktueller Status-Text (z.B. "Shader kompilieren...")</summary>
    public string StatusText { get; set; } = "";

    /// <summary>App-Name (z.B. "BomberBlast")</summary>
    public string AppName { get; set; } = "App";

    /// <summary>App-Version (z.B. "v2.0.20")</summary>
    public string AppVersion { get; set; } = "";

    private readonly Random _rng = new();

    /// <summary>
    /// Initialisiert die Partikel mit zufälligen Positionen und Parametern.
    /// </summary>
    private void InitializeParticles(float width, float height)
    {
        if (_particlesInitialized) return;
        _particlesInitialized = true;

        for (var i = 0; i < MaxParticles; i++)
        {
            var x = (float)_rng.NextDouble();
            _particles[i] = new SplashParticle
            {
                X = x,
                BaseX = x,
                Y = (float)_rng.NextDouble(),
                Radius = 2f + (float)_rng.NextDouble() * 4f,
                Alpha = 40f + (float)_rng.NextDouble() * 80f,
                Phase = (float)(_rng.NextDouble() * Math.PI * 2),
                SpeedX = 0f, // Horizontale Bewegung über Sinus
                SpeedY = -0.003f - (float)_rng.NextDouble() * 0.007f, // Langsam aufsteigend
                FloatAmplitude = 5f + (float)_rng.NextDouble() * 15f,
                FloatFrequency = 0.5f + (float)_rng.NextDouble() * 1.5f
            };
        }
    }

    /// <summary>
    /// Aktualisiert Animationen (pro Frame aufrufen, ~60fps).
    /// </summary>
    public void Update(float deltaTime)
    {
        _time += deltaTime;

        // Smooth-Interpolation zum Zielwert (EaseOut)
        var diff = Progress - _renderedProgress;
        if (Math.Abs(diff) > 0.001f)
            _renderedProgress += diff * 0.12f;
        else
            _renderedProgress = Progress;

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
                p.BaseX = (float)_rng.NextDouble();
            }

            // Horizontale Sinus-Oszillation
            p.X = p.BaseX + MathF.Sin(p.Phase) * (p.FloatAmplitude / 1000f);
        }
    }

    /// <summary>
    /// Rendert den kompletten Splash-Screen auf den Canvas.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;
        if (w <= 0 || h <= 0) return;

        InitializeParticles(w, h);

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
            _versionFont.Size = Math.Min(14f, w * 0.035f);
            var versionWidth = _versionFont.MeasureText(AppVersion);
            var versionX = (w - versionWidth) / 2f;
            _versionPaint.Color = SkiaThemeHelper.TextMuted;
            canvas.DrawText(AppVersion, versionX, centerY + _titleFont.Size * 0.35f + 28f, _versionFont, _versionPaint);
        }
    }

    private void RenderProgressBar(SKCanvas canvas, float w, float h)
    {
        var progress = Math.Clamp(_renderedProgress, 0f, 1f);
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

        _statusFont.Size = Math.Min(13f, w * 0.033f);
        var statusWidth = _statusFont.MeasureText(StatusText);
        var statusX = (w - statusWidth) / 2f;

        _statusPaint.Color = SkiaThemeHelper.TextMuted;
        canvas.DrawText(StatusText, statusX, statusY, _statusFont, _statusPaint);
    }

    public void Dispose()
    {
        _bgPaint.Dispose();
        _particlePaint.Dispose();
        _barBgPaint.Dispose();
        _barFillPaint.Dispose();
        _titlePaint.Dispose();
        _versionPaint.Dispose();
        _statusPaint.Dispose();
        _percentPaint.Dispose();
        _glowPaint.Dispose();
        _titleFont.Dispose();
        _versionFont.Dispose();
        _statusFont.Dispose();
        _percentFont.Dispose();
        _barPath.Dispose();
        _particleGlow?.Dispose();
        _titleGlow?.Dispose();
    }
}
