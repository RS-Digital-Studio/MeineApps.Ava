using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Labs.Controls;
using SkiaSharp;

namespace MeineApps.UI.SkiaSharp;

/// <summary>
/// SkiaSharp-basierter Gradient-Fortschrittsring mit Glow, Tick-Marks und Partikel-Effekten.
/// Ersetzt/erweitert den bestehenden CircularProgress für Premium-Visualisierungen.
/// Genutzt in: ZeitManager (3×), WorkTimePro (1×), FitnessRechner Dashboard (3×).
/// </summary>
public class SkiaGradientRing : Control
{
    // === StyledProperties ===

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<SkiaGradientRing, double>(nameof(Value), 0.0);

    public static readonly StyledProperty<Color> StartColorProperty =
        AvaloniaProperty.Register<SkiaGradientRing, Color>(nameof(StartColor), Colors.LimeGreen);

    public static readonly StyledProperty<Color> EndColorProperty =
        AvaloniaProperty.Register<SkiaGradientRing, Color>(nameof(EndColor), Colors.Cyan);

    public static readonly StyledProperty<Color> TrackColorProperty =
        AvaloniaProperty.Register<SkiaGradientRing, Color>(nameof(TrackColor), Color.FromArgb(40, 255, 255, 255));

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<SkiaGradientRing, double>(nameof(StrokeWidth), 8.0);

    public static readonly StyledProperty<bool> GlowEnabledProperty =
        AvaloniaProperty.Register<SkiaGradientRing, bool>(nameof(GlowEnabled), true);

    public static readonly StyledProperty<bool> ShowTickMarksProperty =
        AvaloniaProperty.Register<SkiaGradientRing, bool>(nameof(ShowTickMarks), false);

    public static readonly StyledProperty<int> TickMarkCountProperty =
        AvaloniaProperty.Register<SkiaGradientRing, int>(nameof(TickMarkCount), 60);

    public static readonly StyledProperty<bool> ParticlesEnabledProperty =
        AvaloniaProperty.Register<SkiaGradientRing, bool>(nameof(ParticlesEnabled), false);

    public static readonly StyledProperty<bool> IsPulsingProperty =
        AvaloniaProperty.Register<SkiaGradientRing, bool>(nameof(IsPulsing), false);

    // === Properties ===

    /// <summary>Fortschritt (0.0 - 1.0).</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Start-Farbe des Gradienten (bei 0%).</summary>
    public Color StartColor { get => GetValue(StartColorProperty); set => SetValue(StartColorProperty, value); }

    /// <summary>End-Farbe des Gradienten (bei 100%).</summary>
    public Color EndColor { get => GetValue(EndColorProperty); set => SetValue(EndColorProperty, value); }

    /// <summary>Farbe des Hintergrund-Tracks.</summary>
    public Color TrackColor { get => GetValue(TrackColorProperty); set => SetValue(TrackColorProperty, value); }

    /// <summary>Strichstärke des Rings.</summary>
    public double StrokeWidth { get => GetValue(StrokeWidthProperty); set => SetValue(StrokeWidthProperty, value); }

    /// <summary>Glow-Effekt aktivieren (SKBlurMaskFilter).</summary>
    public bool GlowEnabled { get => GetValue(GlowEnabledProperty); set => SetValue(GlowEnabledProperty, value); }

    /// <summary>Uhren-Markierungen anzeigen.</summary>
    public bool ShowTickMarks { get => GetValue(ShowTickMarksProperty); set => SetValue(ShowTickMarksProperty, value); }

    /// <summary>Anzahl der Tick-Marks (Standard 60).</summary>
    public int TickMarkCount { get => GetValue(TickMarkCountProperty); set => SetValue(TickMarkCountProperty, value); }

    /// <summary>Partikel am Endpunkt des Fortschritts.</summary>
    public bool ParticlesEnabled { get => GetValue(ParticlesEnabledProperty); set => SetValue(ParticlesEnabledProperty, value); }

    /// <summary>Puls-Animation (Opacity-Schwankung).</summary>
    public bool IsPulsing { get => GetValue(IsPulsingProperty); set => SetValue(IsPulsingProperty, value); }

    // === Interner State ===

    private readonly SKCanvasView _canvasView;
    private DispatcherTimer? _animationTimer;
    private float _time;
    private readonly SkiaParticleManager _particles = new(15);

    // Gecachte Paint-Objekte
    private static readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _arcPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _tickPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

    public SkiaGradientRing()
    {
        _canvasView = new SKCanvasView();
        _canvasView.PaintSurface += OnPaintSurface;
        LogicalChildren.Add(_canvasView);
        VisualChildren.Add(_canvasView);
    }

    static SkiaGradientRing()
    {
        // Bei Property-Änderung neu zeichnen
        ValueProperty.Changed.AddClassHandler<SkiaGradientRing>((r, _) => r.InvalidateCanvas());
        StartColorProperty.Changed.AddClassHandler<SkiaGradientRing>((r, _) => r.InvalidateCanvas());
        EndColorProperty.Changed.AddClassHandler<SkiaGradientRing>((r, _) => r.InvalidateCanvas());
        TrackColorProperty.Changed.AddClassHandler<SkiaGradientRing>((r, _) => r.InvalidateCanvas());
        StrokeWidthProperty.Changed.AddClassHandler<SkiaGradientRing>((r, _) => r.InvalidateCanvas());
        IsPulsingProperty.Changed.AddClassHandler<SkiaGradientRing>((r, _) => r.UpdateAnimationTimer());
        ParticlesEnabledProperty.Changed.AddClassHandler<SkiaGradientRing>((r, _) => r.UpdateAnimationTimer());
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

    private void InvalidateCanvas()
    {
        _canvasView.InvalidateSurface();
    }

    private void UpdateAnimationTimer()
    {
        bool needsAnimation = IsPulsing || ParticlesEnabled;

        if (needsAnimation && _animationTimer == null)
        {
            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) }; // 30fps
            _animationTimer.Tick += (_, _) =>
            {
                _time += 0.033f;
                InvalidateCanvas();
            };
            _animationTimer.Start();
        }
        else if (!needsAnimation && _animationTimer != null)
        {
            _animationTimer.Stop();
            _animationTimer = null;
            _particles.Clear();
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        float size = Math.Min(bounds.Width, bounds.Height);
        float strokeW = (float)StrokeWidth;
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float radius = (size - strokeW * 2 - (GlowEnabled ? 8f : 0f)) / 2f;

        if (radius <= 0) return;

        float value = (float)Math.Clamp(Value, 0.0, 1.0);
        float sweepAngle = value * 360f;
        float startAngle = -90f; // 12-Uhr-Position

        var startColor = SkiaThemeHelper.ToSKColor(StartColor);
        var endColor = SkiaThemeHelper.ToSKColor(EndColor);
        var trackColor = SkiaThemeHelper.ToSKColor(TrackColor);

        // Puls-Opacity
        float pulseAlpha = 1f;
        if (IsPulsing)
            pulseAlpha = 0.7f + 0.3f * MathF.Sin(_time * 4f);

        var arcRect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        // 1. Track-Ring (voller Kreis)
        _trackPaint.StrokeWidth = strokeW;
        _trackPaint.Color = trackColor;
        canvas.DrawOval(arcRect, _trackPaint);

        // 2. TickMarks (vor dem Arc, damit sie darunter liegen)
        if (ShowTickMarks)
            DrawTickMarks(canvas, cx, cy, radius, strokeW);

        if (value <= 0.001f) return;

        // 3. Glow-Layer (größer, blurrig)
        if (GlowEnabled)
        {
            var glowColor = SkiaThemeHelper.Lerp(startColor, endColor, value);
            _glowPaint.StrokeWidth = strokeW + 4f;
            _glowPaint.Color = glowColor.WithAlpha((byte)(100 * pulseAlpha));
            _glowPaint.MaskFilter = _glowFilter;

            using var glowPath = new SKPath();
            glowPath.AddArc(arcRect, startAngle, sweepAngle);
            canvas.DrawPath(glowPath, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // 4. Gradient-Arc
        _arcPaint.StrokeWidth = strokeW;

        // SweepGradient für den Bogen
        var gradientColors = new[] { startColor, endColor };
        var gradientPositions = new[] { 0f, 1f };
        _arcPaint.Shader = SKShader.CreateSweepGradient(
            new SKPoint(cx, cy), gradientColors, gradientPositions,
            SKShaderTileMode.Clamp, startAngle, startAngle + sweepAngle);
        _arcPaint.Color = SKColors.White.WithAlpha((byte)(255 * pulseAlpha)); // Shader bestimmt Farbe

        using var arcPath = new SKPath();
        arcPath.AddArc(arcRect, startAngle, sweepAngle);
        canvas.DrawPath(arcPath, _arcPaint);
        _arcPaint.Shader = null;

        // 5. Leuchtender Endpunkt
        float endAngleRad = (startAngle + sweepAngle) * MathF.PI / 180f;
        float endX = cx + MathF.Cos(endAngleRad) * radius;
        float endY = cy + MathF.Sin(endAngleRad) * radius;

        var endColor2 = SkiaThemeHelper.Lerp(startColor, endColor, value);
        _arcPaint.Shader = null;
        _arcPaint.Color = endColor2.WithAlpha((byte)(255 * pulseAlpha));
        _arcPaint.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(endX, endY, strokeW * 0.6f, _arcPaint);
        _arcPaint.Style = SKPaintStyle.Stroke;

        // 6. Partikel am Endpunkt
        if (ParticlesEnabled)
        {
            // Gelegentlich neue Partikel spawnen
            if (_time % 0.15f < 0.035f)
                _particles.Add(SkiaParticlePresets.CreateGlow(new Random(), endX, endY, endColor2));

            _particles.Update(0.033f);
            _particles.Draw(canvas, withGlow: true);
        }
    }

    private void DrawTickMarks(SKCanvas canvas, float cx, float cy, float radius, float strokeW)
    {
        int count = Math.Max(4, TickMarkCount);
        var tickColor = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 60);
        _tickPaint.Color = tickColor;

        float innerRadius = radius - strokeW / 2f - 2f;
        float outerRadius = radius + strokeW / 2f + 2f;

        for (int i = 0; i < count; i++)
        {
            float angleRad = (i * 360f / count - 90f) * MathF.PI / 180f;
            bool isMajor = (count == 60 && i % 5 == 0) || (count != 60 && i % (count / 12) == 0);

            _tickPaint.StrokeWidth = isMajor ? 1.5f : 0.5f;
            float tickInner = isMajor ? innerRadius - 4f : innerRadius - 1f;

            canvas.DrawLine(
                cx + MathF.Cos(angleRad) * tickInner,
                cy + MathF.Sin(angleRad) * tickInner,
                cx + MathF.Cos(angleRad) * outerRadius,
                cy + MathF.Sin(angleRad) * outerRadius,
                _tickPaint);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animationTimer?.Stop();
        _animationTimer = null;
    }
}
