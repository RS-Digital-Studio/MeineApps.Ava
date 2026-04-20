using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace GardenControl.Shared.Controls;

/// <summary>
/// SkiaSharp-basierter Feuchtigkeits-Gauge.
/// Kreisförmiger Ring mit Farbverlauf (rot→orange→grün→blau),
/// großer Prozentwert in der Mitte, Schwellenwert-Markierung.
/// </summary>
public class MoistureGaugeControl : Control
{
    public static readonly StyledProperty<double> MoisturePercentProperty =
        AvaloniaProperty.Register<MoistureGaugeControl, double>(nameof(MoisturePercent));

    public static readonly StyledProperty<int> ThresholdPercentProperty =
        AvaloniaProperty.Register<MoistureGaugeControl, int>(nameof(ThresholdPercent), 40);

    public static readonly StyledProperty<string> ZoneNameProperty =
        AvaloniaProperty.Register<MoistureGaugeControl, string>(nameof(ZoneName), "");

    public static readonly StyledProperty<bool> IsWateringProperty =
        AvaloniaProperty.Register<MoistureGaugeControl, bool>(nameof(IsWatering));

    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<MoistureGaugeControl, string>(nameof(StatusText), "");

    public double MoisturePercent
    {
        get => GetValue(MoisturePercentProperty);
        set => SetValue(MoisturePercentProperty, value);
    }

    public int ThresholdPercent
    {
        get => GetValue(ThresholdPercentProperty);
        set => SetValue(ThresholdPercentProperty, value);
    }

    public string ZoneName
    {
        get => GetValue(ZoneNameProperty);
        set => SetValue(ZoneNameProperty, value);
    }

    public bool IsWatering
    {
        get => GetValue(IsWateringProperty);
        set => SetValue(IsWateringProperty, value);
    }

    public string StatusText
    {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    static MoistureGaugeControl()
    {
        AffectsRender<MoistureGaugeControl>(
            MoisturePercentProperty, ThresholdPercentProperty,
            IsWateringProperty, ZoneNameProperty, StatusTextProperty);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.Custom(new GaugeDrawOperation(bounds, MoisturePercent, ThresholdPercent,
            ZoneName, StatusText, IsWatering));
    }

    /// <summary>
    /// SkiaSharp-Zeichenoperation für den Gauge.
    /// Typefaces, Paints, Shader und MaskFilter werden statisch gecacht,
    /// um Pro-Frame-Allokationen und native Memory-Leaks zu vermeiden.
    /// </summary>
    private class GaugeDrawOperation : ICustomDrawOperation
    {
        // Gecachte Typefaces - werden einmal erstellt und wiederverwendet
        private static readonly SKTypeface InterBold = SKTypeface.FromFamilyName("Inter",
            SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        private static readonly SKTypeface InterSemiBold = SKTypeface.FromFamilyName("Inter",
            SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

        // Gecachte Paints - wiederverwendet pro Frame (Properties werden mutiert)
        // Kein Dispose noetig - leben bis Prozess-Ende.
        private static readonly SKPaint _strokePaint = new()
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };
        private static readonly SKPaint _gaugePaint = new()
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };
        private static readonly SKPaint _glowPaint = new()
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };
        private static readonly SKPaint _markerPaint = new()
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.5f,
            Color = new SKColor(255, 167, 38),
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };
        private static readonly SKPaint _textPaint = new()
        {
            Color = SKColors.White,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = InterBold
        };
        private static readonly SKPaint _unitPaint = new()
        {
            Color = new SKColor(138, 164, 188),
            IsAntialias = true,
            TextAlign = SKTextAlign.Left,
            Typeface = InterSemiBold
        };
        private static readonly SKPaint _namePaint = new()
        {
            Color = new SKColor(138, 164, 188),
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = InterSemiBold
        };
        private static readonly SKPaint _statusPaint = new()
        {
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = InterSemiBold
        };

        // Gecachter Sweep-Gradient-Shader (Farben und Positionen konstant)
        // Wird neu erstellt wenn Center-Punkt sich aendert (Bounds-Wechsel)
        private static readonly SKColor[] _gradientColors =
        {
            new(239, 83, 80),
            new(255, 167, 38),
            new(102, 187, 106),
            new(66, 165, 245),
            new(66, 165, 245)
        };
        private static readonly float[] _gradientPositions = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        // Gecachter MaskFilter fuer Watering-Glow (Radius 4)
        private static readonly SKMaskFilter _glowBlurFilter =
            SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4);

        private readonly double _moisture;
        private readonly int _threshold;
        private readonly string _zoneName;
        private readonly string _statusText;
        private readonly bool _isWatering;

        public Rect Bounds { get; }

        public GaugeDrawOperation(Rect bounds, double moisture, int threshold,
            string zoneName, string statusText, bool isWatering)
        {
            Bounds = bounds;
            _moisture = Math.Clamp(moisture, 0, 100);
            _threshold = Math.Clamp(threshold, 0, 100);
            _zoneName = zoneName ?? "";
            _statusText = statusText ?? "";
            _isWatering = isWatering;
        }

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
            if (leaseFeature == null) return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            if (canvas == null) return;

            var size = (float)Math.Min(Bounds.Width, Bounds.Height);
            var centerX = (float)Bounds.Width / 2f;
            var centerY = (float)Bounds.Height / 2f;
            var radius = size / 2f - 12f;
            var strokeWidth = size * 0.08f; // 8% der Größe

            // Hintergrund-Ring (dunkel) - gecachter _strokePaint
            _strokePaint.StrokeWidth = strokeWidth;
            _strokePaint.Color = new SKColor(30, 40, 55);

            var arcRect = new SKRect(
                centerX - radius, centerY - radius,
                centerX + radius, centerY + radius);

            // 270° Bogen (von 135° bis 405°, also 270° Sweep)
            const float startAngle = 135f;
            const float totalSweep = 270f;

            canvas.DrawArc(arcRect, startAngle, totalSweep, false, _strokePaint);

            // Farbiger Bogen (Feuchtigkeit) - Shader lokal (Center-Punkt-abhaengig)
            var sweepAngle = (float)(_moisture / 100.0 * totalSweep);
            if (sweepAngle > 0.5f)
            {
                _gaugePaint.StrokeWidth = strokeWidth;
                using (var shader = SKShader.CreateSweepGradient(
                    new SKPoint(centerX, centerY), _gradientColors, _gradientPositions))
                {
                    _gaugePaint.Shader = shader;
                    canvas.DrawArc(arcRect, startAngle, sweepAngle, false, _gaugePaint);
                    _gaugePaint.Shader = null;
                }

                // Glow-Effekt wenn bewässert wird (gecachter MaskFilter)
                if (_isWatering)
                {
                    _glowPaint.StrokeWidth = strokeWidth + 6;
                    _glowPaint.Color = new SKColor(66, 165, 245, 60);
                    _glowPaint.MaskFilter = _glowBlurFilter;
                    canvas.DrawArc(arcRect, startAngle, sweepAngle, false, _glowPaint);
                    _glowPaint.MaskFilter = null;
                }
            }

            // Schwellenwert-Markierung (gelber Strich) - gecachter _markerPaint
            var thresholdAngle = startAngle + (_threshold / 100f * totalSweep);
            var thresholdRad = thresholdAngle * MathF.PI / 180f;
            var markerInner = radius - strokeWidth;
            var markerOuter = radius + strokeWidth;

            canvas.DrawLine(
                centerX + MathF.Cos(thresholdRad) * markerInner,
                centerY + MathF.Sin(thresholdRad) * markerInner,
                centerX + MathF.Cos(thresholdRad) * markerOuter,
                centerY + MathF.Sin(thresholdRad) * markerOuter,
                _markerPaint);

            // Prozentwert in der Mitte - gecachter _textPaint
            var percentText = $"{_moisture:F0}";
            _textPaint.TextSize = size * 0.22f;
            canvas.DrawText(percentText, centerX, centerY + _textPaint.TextSize * 0.1f, _textPaint);

            // %-Zeichen kleiner - gecachter _unitPaint
            _unitPaint.TextSize = size * 0.09f;
            var percentWidth = _textPaint.MeasureText(percentText);
            canvas.DrawText("%", centerX + percentWidth / 2f + 2, centerY - _textPaint.TextSize * 0.15f, _unitPaint);

            // Zone-Name unter dem Wert - gecachter _namePaint
            if (!string.IsNullOrEmpty(_zoneName))
            {
                _namePaint.TextSize = size * 0.08f;
                canvas.DrawText(_zoneName.ToUpper(), centerX, centerY + size * 0.18f, _namePaint);
            }

            // Status-Text am unteren Bogenrand - gecachter _statusPaint
            if (!string.IsNullOrEmpty(_statusText))
            {
                _statusPaint.Color = _isWatering
                    ? new SKColor(66, 165, 245) : new SKColor(102, 187, 106);
                _statusPaint.TextSize = size * 0.07f;
                canvas.DrawText(_statusText, centerX, centerY + size * 0.32f, _statusPaint);
            }
        }

        private static SKColor GetMoistureColor(float percent) => percent switch
        {
            < 25f => new SKColor(239, 83, 80),   // Rot
            < 40f => new SKColor(255, 167, 38),  // Orange
            < 70f => new SKColor(102, 187, 106), // Grün
            _ => new SKColor(66, 165, 245)        // Blau
        };

        public void Dispose() { }
        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;
    }
}
