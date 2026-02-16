using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Labs.Controls;
using SkiaSharp;

namespace MeineApps.UI.SkiaSharp;

/// <summary>
/// Definiert eine farbige Zone im Gauge (z.B. "Normal" = Grün von 18.5 bis 25).
/// </summary>
public class GaugeZone
{
    public Color Color { get; set; } = Colors.Gray;
    public double Start { get; set; }
    public double End { get; set; }
    public string Label { get; set; } = "";
}

/// <summary>
/// SkiaSharp-basierter Halbkreis-Tachometer mit farbigen Zonen und animiertem Zeiger.
/// Genutzt in: FitnessRechner (BMI), HandwerkerRechner (Treppen/Dach-Winkel),
/// FinanzRechner (Budget-Gauges).
/// </summary>
public class SkiaGauge : Control
{
    // === StyledProperties ===

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<SkiaGauge, double>(nameof(Value), 0.0);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<SkiaGauge, double>(nameof(Minimum), 0.0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<SkiaGauge, double>(nameof(Maximum), 100.0);

    public static readonly StyledProperty<bool> NeedleAnimatedProperty =
        AvaloniaProperty.Register<SkiaGauge, bool>(nameof(NeedleAnimated), true);

    public static readonly StyledProperty<bool> ShowValueTextProperty =
        AvaloniaProperty.Register<SkiaGauge, bool>(nameof(ShowValueText), true);

    public static readonly StyledProperty<string> ValueFormatProperty =
        AvaloniaProperty.Register<SkiaGauge, string>(nameof(ValueFormat), "F1");

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<SkiaGauge, string>(nameof(Unit), "");

    // === Properties ===

    /// <summary>Aktueller Wert (zwischen Minimum und Maximum).</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Minimal-Wert der Skala.</summary>
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <summary>Maximal-Wert der Skala.</summary>
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>Zeiger-Animation aktivieren.</summary>
    public bool NeedleAnimated { get => GetValue(NeedleAnimatedProperty); set => SetValue(NeedleAnimatedProperty, value); }

    /// <summary>Wert als Text unter dem Gauge anzeigen.</summary>
    public bool ShowValueText { get => GetValue(ShowValueTextProperty); set => SetValue(ShowValueTextProperty, value); }

    /// <summary>Format-String für den Wert (z.B. "F1" für eine Nachkommastelle).</summary>
    public string ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    /// <summary>Einheit die hinter dem Wert angezeigt wird (z.B. "°", "kg/m²").</summary>
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <summary>Liste der farbigen Zonen.</summary>
    public List<GaugeZone> Zones { get; set; } = new();

    // === Interner State ===

    private readonly SKCanvasView _canvasView;
    private DispatcherTimer? _needleTimer;
    private float _displayedValue; // Animierter Wert
    private float _targetValue;    // Ziel-Wert

    // Gecachte Paints
    private static readonly SKPaint _zonePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Butt };
    private static readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _needlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _centerPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f) };
    private static readonly SKFont _valueFont = new() { Size = 18f };
    private static readonly SKFont _unitFont = new() { Size = 11f };
    private static readonly SKFont _labelFont = new() { Size = 8f };

    public SkiaGauge()
    {
        _canvasView = new SKCanvasView();
        _canvasView.PaintSurface += OnPaintSurface;
        LogicalChildren.Add(_canvasView);
        VisualChildren.Add(_canvasView);
    }

    static SkiaGauge()
    {
        ValueProperty.Changed.AddClassHandler<SkiaGauge>((g, _) => g.OnValueChanged());
        MinimumProperty.Changed.AddClassHandler<SkiaGauge>((g, _) => g.InvalidateCanvas());
        MaximumProperty.Changed.AddClassHandler<SkiaGauge>((g, _) => g.InvalidateCanvas());
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

    private void OnValueChanged()
    {
        _targetValue = (float)Value;

        if (NeedleAnimated)
        {
            // Animations-Timer starten falls nicht aktiv
            if (_needleTimer == null)
            {
                _needleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // 60fps für smooth
                _needleTimer.Tick += (_, _) =>
                {
                    float diff = _targetValue - _displayedValue;
                    if (MathF.Abs(diff) < 0.01f)
                    {
                        _displayedValue = _targetValue;
                        _needleTimer?.Stop();
                        _needleTimer = null;
                    }
                    else
                    {
                        // Ease-Out: Schnell starten, langsam enden
                        _displayedValue += diff * 0.12f;
                    }
                    InvalidateCanvas();
                };
            }
            _needleTimer.Start();
        }
        else
        {
            _displayedValue = _targetValue;
            InvalidateCanvas();
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        float w = bounds.Width;
        float h = bounds.Height;
        // Halbkreis: Breite bestimmt, Höhe = halbe Breite + Platz für Text
        float gaugeW = w - 20f;
        float gaugeH = gaugeW / 2f;
        float cx = bounds.MidX;
        float cy = bounds.Top + 10f + gaugeH; // Mittelpunkt am unteren Rand des Halbkreises
        float radius = gaugeW / 2f - 10f;
        float strokeW = Math.Max(8f, radius * 0.12f);

        if (radius <= 10) return;

        float min = (float)Minimum;
        float max = (float)Maximum;
        float range = max - min;
        if (range <= 0) range = 1;

        var arcRect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        // 1. Track (grauer Halbkreis)
        _trackPaint.StrokeWidth = strokeW;
        _trackPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 60);

        using (var trackPath = new SKPath())
        {
            trackPath.AddArc(arcRect, 180f, 180f);
            canvas.DrawPath(trackPath, _trackPaint);
        }

        // 2. Farbige Zonen
        _zonePaint.StrokeWidth = strokeW;
        foreach (var zone in Zones)
        {
            float zoneStart = Math.Clamp((float)(zone.Start - min) / range, 0f, 1f);
            float zoneEnd = Math.Clamp((float)(zone.End - min) / range, 0f, 1f);
            float startAngle = 180f + zoneStart * 180f;
            float sweepAngle = (zoneEnd - zoneStart) * 180f;

            if (sweepAngle <= 0) continue;

            _zonePaint.Color = SkiaThemeHelper.ToSKColor(zone.Color);

            using var zonePath = new SKPath();
            zonePath.AddArc(arcRect, startAngle, sweepAngle);
            canvas.DrawPath(zonePath, _zonePaint);
        }

        // 3. Zeiger
        float normalizedValue = Math.Clamp((_displayedValue - min) / range, 0f, 1f);
        float needleAngle = 180f + normalizedValue * 180f;
        float needleAngleRad = needleAngle * MathF.PI / 180f;

        float needleLength = radius - strokeW - 4f;
        float needleTipX = cx + MathF.Cos(needleAngleRad) * needleLength;
        float needleTipY = cy + MathF.Sin(needleAngleRad) * needleLength;

        // Zeiger-Glow
        var needleColor = GetColorForValue(normalizedValue);
        _glowPaint.Color = needleColor.WithAlpha(60);
        canvas.DrawCircle(needleTipX, needleTipY, 6f, _glowPaint);

        // Zeiger-Linie
        _needlePaint.Color = needleColor;
        float perpX = -MathF.Sin(needleAngleRad) * 3f;
        float perpY = MathF.Cos(needleAngleRad) * 3f;

        using var needlePath = new SKPath();
        needlePath.MoveTo(cx + perpX, cy + perpY);
        needlePath.LineTo(cx - perpX, cy - perpY);
        needlePath.LineTo(needleTipX, needleTipY);
        needlePath.Close();
        canvas.DrawPath(needlePath, _needlePaint);

        // Zentraler Kreis
        _centerPaint.Color = SkiaThemeHelper.Surface;
        canvas.DrawCircle(cx, cy, 5f, _centerPaint);
        _centerPaint.Color = needleColor;
        canvas.DrawCircle(cx, cy, 3f, _centerPaint);

        // 4. Wert-Text
        if (ShowValueText)
        {
            string valueText = _displayedValue.ToString(ValueFormat ?? "F1");
            string unitText = Unit ?? "";

            _textPaint.Color = SkiaThemeHelper.TextPrimary;
            _valueFont.Size = Math.Max(14f, radius * 0.22f);

            using var valueBlob = SKTextBlob.Create(valueText, _valueFont);
            if (valueBlob != null)
            {
                float textX = cx - valueBlob.Bounds.Width / 2f;
                float textY = cy + radius * 0.25f;
                canvas.DrawText(valueText, textX, textY, _valueFont, _textPaint);

                // Einheit
                if (!string.IsNullOrEmpty(unitText))
                {
                    _textPaint.Color = SkiaThemeHelper.TextMuted;
                    _unitFont.Size = Math.Max(9f, radius * 0.12f);
                    using var unitBlob = SKTextBlob.Create(unitText, _unitFont);
                    if (unitBlob != null)
                    {
                        canvas.DrawText(unitText, cx - unitBlob.Bounds.Width / 2f,
                            textY + _valueFont.Size * 0.9f, _unitFont, _textPaint);
                    }
                }
            }
        }

        // 5. Zonen-Labels (optional, am äußeren Rand)
        DrawZoneLabels(canvas, cx, cy, radius, strokeW, min, range);
    }

    private void DrawZoneLabels(SKCanvas canvas, float cx, float cy, float radius, float strokeW,
        float min, float range)
    {
        _labelFont.Size = Math.Max(7f, radius * 0.08f);
        _textPaint.Color = SkiaThemeHelper.TextMuted;

        foreach (var zone in Zones)
        {
            if (string.IsNullOrEmpty(zone.Label)) continue;

            float zoneMid = (float)((zone.Start + zone.End) / 2.0 - min) / range;
            float angle = (180f + zoneMid * 180f) * MathF.PI / 180f;
            float labelR = radius + strokeW / 2f + 10f;

            float lx = cx + MathF.Cos(angle) * labelR;
            float ly = cy + MathF.Sin(angle) * labelR;

            using var labelBlob = SKTextBlob.Create(zone.Label, _labelFont);
            if (labelBlob != null)
                canvas.DrawText(zone.Label, lx - labelBlob.Bounds.Width / 2f, ly, _labelFont, _textPaint);
        }
    }

    /// <summary>
    /// Bestimmt die Farbe des Zeigers basierend auf dem normalisierten Wert (0-1).
    /// Interpoliert zwischen den Zonen-Farben.
    /// </summary>
    private SKColor GetColorForValue(float normalized)
    {
        if (Zones.Count == 0)
            return SkiaThemeHelper.Primary;

        float min = (float)Minimum;
        float range = (float)(Maximum - Minimum);
        if (range <= 0) range = 1;
        float actualValue = min + normalized * range;

        foreach (var zone in Zones)
        {
            if (actualValue >= zone.Start && actualValue <= zone.End)
                return SkiaThemeHelper.ToSKColor(zone.Color);
        }

        return SkiaThemeHelper.Primary;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _needleTimer?.Stop();
        _needleTimer = null;
    }
}
