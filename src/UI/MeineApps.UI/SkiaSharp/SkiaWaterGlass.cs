using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Labs.Controls;
using SkiaSharp;

namespace MeineApps.UI.SkiaSharp;

/// <summary>
/// SkiaSharp-basiertes animiertes Wasserglas mit Wellen-Animation, Tropfen-Partikeln und Glas-Glanz.
/// Genutzt in: FitnessRechner (Wasser-Tracker).
/// </summary>
public class SkiaWaterGlass : Control
{
    // === StyledProperties ===

    public static readonly StyledProperty<double> FillPercentProperty =
        AvaloniaProperty.Register<SkiaWaterGlass, double>(nameof(FillPercent), 0.0);

    public static readonly StyledProperty<bool> WaveEnabledProperty =
        AvaloniaProperty.Register<SkiaWaterGlass, bool>(nameof(WaveEnabled), true);

    public static readonly StyledProperty<Color> WaterColorProperty =
        AvaloniaProperty.Register<SkiaWaterGlass, Color>(nameof(WaterColor), Color.FromRgb(0x22, 0xD3, 0xEE));

    public static readonly StyledProperty<bool> ShowDropsProperty =
        AvaloniaProperty.Register<SkiaWaterGlass, bool>(nameof(ShowDrops), false);

    // === Properties ===

    /// <summary>Füllstand (0.0 = leer, 1.0 = voll).</summary>
    public double FillPercent { get => GetValue(FillPercentProperty); set => SetValue(FillPercentProperty, value); }

    /// <summary>Wellen-Animation aktivieren.</summary>
    public bool WaveEnabled { get => GetValue(WaveEnabledProperty); set => SetValue(WaveEnabledProperty, value); }

    /// <summary>Farbe des Wassers.</summary>
    public Color WaterColor { get => GetValue(WaterColorProperty); set => SetValue(WaterColorProperty, value); }

    /// <summary>Tropfen-Partikel anzeigen (kurzzeitig nach Hinzufügen).</summary>
    public bool ShowDrops { get => GetValue(ShowDropsProperty); set => SetValue(ShowDropsProperty, value); }

    // === Interner State ===

    private readonly SKCanvasView _canvasView;
    private DispatcherTimer? _animationTimer;
    private float _time;
    private readonly SkiaParticleManager _dropParticles = new(12);
    private float _prevFillPercent;
    private float _dropCooldown;

    // Gecachte Paints
    private static readonly SKPaint _glassPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f };
    private static readonly SKPaint _waterPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _shinePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _bubblePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    public SkiaWaterGlass()
    {
        _canvasView = new SKCanvasView();
        _canvasView.PaintSurface += OnPaintSurface;
        LogicalChildren.Add(_canvasView);
        VisualChildren.Add(_canvasView);
    }

    static SkiaWaterGlass()
    {
        FillPercentProperty.Changed.AddClassHandler<SkiaWaterGlass>((g, e) =>
        {
            g._prevFillPercent = (float)(e.OldValue ?? 0.0);
            g.InvalidateCanvas();
            g.UpdateAnimationTimer();
        });
        WaveEnabledProperty.Changed.AddClassHandler<SkiaWaterGlass>((g, _) => g.UpdateAnimationTimer());
        ShowDropsProperty.Changed.AddClassHandler<SkiaWaterGlass>((g, _) =>
        {
            if (g.ShowDrops)
                g._dropCooldown = 0.8f; // Tropfen für 0.8 Sekunden
            g.UpdateAnimationTimer();
        });
        WaterColorProperty.Changed.AddClassHandler<SkiaWaterGlass>((g, _) => g.InvalidateCanvas());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _canvasView.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _canvasView.Measure(availableSize);
        return availableSize;
    }

    private void InvalidateCanvas() => _canvasView.InvalidateSurface();

    private void UpdateAnimationTimer()
    {
        bool needsAnimation = WaveEnabled || ShowDrops || _dropCooldown > 0;

        if (needsAnimation && _animationTimer == null)
        {
            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) }; // 30fps
            _animationTimer.Tick += (_, _) =>
            {
                _time += 0.033f;
                _dropCooldown -= 0.033f;

                // Tropfen spawnen während Cooldown
                if (_dropCooldown > 0 && _time % 0.1f < 0.035f)
                {
                    var rng = new Random();
                    var waterSk = SkiaThemeHelper.ToSKColor(WaterColor);
                    _dropParticles.Add(SkiaParticlePresets.CreateWaterDrop(rng, 0, 0, waterSk));
                }

                if (_dropCooldown <= 0 && !WaveEnabled && !ShowDrops)
                {
                    _animationTimer?.Stop();
                    _animationTimer = null;
                }

                _dropParticles.Update(0.033f);
                InvalidateCanvas();
            };
            _animationTimer.Start();
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        float w = bounds.Width;
        float h = bounds.Height;

        // Glas-Dimensionen (leicht trapezförmig)
        float glassPadding = 20f;
        float glassTopWidth = w * 0.5f;
        float glassBottomWidth = w * 0.38f;
        float glassHeight = h - glassPadding * 2;
        float glassLeft = (w - glassTopWidth) / 2f;
        float glassRight = glassLeft + glassTopWidth;
        float glassTop = glassPadding;
        float glassBottom = glassPadding + glassHeight;

        float bottomLeft = (w - glassBottomWidth) / 2f;
        float bottomRight = bottomLeft + glassBottomWidth;

        float fill = (float)Math.Clamp(FillPercent, 0.0, 1.0);
        var waterColor = SkiaThemeHelper.ToSKColor(WaterColor);

        // 1. Wasser-Füllung (mit Wellen)
        if (fill > 0.001f)
        {
            float waterTop = glassBottom - fill * glassHeight;

            // Interpolierte Breite auf Wasserhöhe (Trapez)
            float t = (waterTop - glassTop) / glassHeight;
            float waterLeftEdge = Lerp(glassLeft, bottomLeft, t);
            float waterRightEdge = Lerp(glassRight, bottomRight, t);

            using var waterPath = new SKPath();

            if (WaveEnabled && fill > 0.02f)
            {
                // Wellen-Oberkante
                waterPath.MoveTo(waterLeftEdge, waterTop);
                int steps = 20;
                for (int i = 0; i <= steps; i++)
                {
                    float px = waterLeftEdge + (waterRightEdge - waterLeftEdge) * i / steps;
                    float waveAmplitude = 2.5f + fill * 2f;
                    float py = waterTop + MathF.Sin(_time * 3f + i * 0.6f) * waveAmplitude
                                        + MathF.Sin(_time * 2f + i * 0.3f) * waveAmplitude * 0.5f;
                    waterPath.LineTo(px, py);
                }
            }
            else
            {
                waterPath.MoveTo(waterLeftEdge, waterTop);
                waterPath.LineTo(waterRightEdge, waterTop);
            }

            // Boden und linke Seite schließen
            waterPath.LineTo(bottomRight, glassBottom);
            waterPath.LineTo(bottomLeft, glassBottom);
            waterPath.Close();

            // Wasser-Gradient (hell oben, dunkel unten)
            var waterLight = waterColor.WithAlpha(200);
            var waterDark = SkiaThemeHelper.AdjustBrightness(waterColor, 0.6f).WithAlpha(220);
            _waterPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(w / 2f, waterTop),
                new SKPoint(w / 2f, glassBottom),
                new[] { waterLight, waterDark },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp);

            // Clip auf Glas-Form
            canvas.Save();
            using var glassClipPath = CreateGlassPath(glassLeft, glassTop, glassRight, glassBottom,
                bottomLeft, bottomRight);
            canvas.ClipPath(glassClipPath);
            canvas.DrawPath(waterPath, _waterPaint);
            _waterPaint.Shader = null;

            // Blasen
            DrawBubbles(canvas, waterLeftEdge, waterRightEdge, waterTop, glassBottom, waterColor);

            canvas.Restore();
        }

        // 2. Glas-Umriss
        _glassPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextSecondary, 120);
        using var glassOutline = CreateGlassPath(glassLeft, glassTop, glassRight, glassBottom,
            bottomLeft, bottomRight);
        canvas.DrawPath(glassOutline, _glassPaint);

        // 3. Glas-Glanz (weißer Gradient-Streifen links)
        float shineX = glassLeft + 6f;
        float shineW = 4f;
        _shinePaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(shineX, glassTop + 10f),
            new SKPoint(shineX + shineW, glassTop + 10f),
            new[] { SKColors.White.WithAlpha(0), SKColors.White.WithAlpha(50), SKColors.White.WithAlpha(0) },
            new[] { 0f, 0.5f, 1f },
            SKShaderTileMode.Clamp);
        canvas.DrawRect(shineX, glassTop + 10f, shineW, glassHeight * 0.6f, _shinePaint);
        _shinePaint.Shader = null;

        // 4. Tropfen-Partikel (über dem Glas)
        if (_dropParticles.HasActiveParticles)
        {
            // Partikel-Positionen auf Glas-Öffnung zentrieren
            canvas.Save();
            canvas.Translate(w / 2f, glassTop - 10f);
            _dropParticles.Draw(canvas, withGlow: true);
            canvas.Restore();
        }

        // 5. Prozent-Text unten
        if (fill > 0)
        {
            string pctText = $"{(int)(fill * 100)}%";
            _fillPaint.Color = SkiaThemeHelper.TextPrimary;
            var font = new SKFont { Size = Math.Max(12f, glassHeight * 0.12f) };
            using var blob = SKTextBlob.Create(pctText, font);
            if (blob != null)
            {
                canvas.DrawText(pctText, w / 2f - blob.Bounds.Width / 2f,
                    glassBottom + font.Size + 4f, font, _fillPaint);
            }
        }
    }

    private static SKPath CreateGlassPath(float topLeft, float top, float topRight, float bottom,
        float bottomLeft, float bottomRight)
    {
        var path = new SKPath();
        float cornerR = 8f;

        path.MoveTo(topLeft, top);
        path.LineTo(topRight, top);
        // Rechte Seite nach unten (leicht nach innen)
        path.LineTo(bottomRight, bottom - cornerR);
        path.ArcTo(new SKPoint(bottomRight, bottom), new SKPoint(bottomRight - cornerR, bottom), cornerR);
        // Boden
        path.LineTo(bottomLeft + cornerR, bottom);
        path.ArcTo(new SKPoint(bottomLeft, bottom), new SKPoint(bottomLeft, bottom - cornerR), cornerR);
        // Linke Seite nach oben
        path.LineTo(topLeft, top);
        path.Close();

        return path;
    }

    private void DrawBubbles(SKCanvas canvas, float left, float right, float waterTop, float bottom, SKColor waterColor)
    {
        if (!WaveEnabled) return;

        _bubblePaint.Color = SKColors.White.WithAlpha(40);

        // 3-5 statische Blasen-Positionen (deterministisch, schweben leicht)
        for (int i = 0; i < 4; i++)
        {
            float bx = left + (right - left) * (0.2f + i * 0.2f);
            float baseY = waterTop + (bottom - waterTop) * (0.3f + i * 0.15f);
            float by = baseY + MathF.Sin(_time * 1.5f + i * 1.2f) * 3f;
            float br = 2f + i * 0.5f;

            canvas.DrawCircle(bx, by, br, _bubblePaint);
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animationTimer?.Stop();
        _animationTimer = null;
    }
}
