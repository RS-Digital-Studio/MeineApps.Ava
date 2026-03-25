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
    /// Typefaces werden statisch gecacht um native Memory Leaks zu vermeiden.
    /// </summary>
    private class GaugeDrawOperation : ICustomDrawOperation
    {
        // Gecachte Typefaces - werden einmal erstellt und wiederverwendet
        private static readonly SKTypeface InterBold = SKTypeface.FromFamilyName("Inter",
            SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        private static readonly SKTypeface InterSemiBold = SKTypeface.FromFamilyName("Inter",
            SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

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

            // Hintergrund-Ring (dunkel)
            using var bgPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeWidth,
                Color = new SKColor(30, 40, 55),
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };

            var arcRect = new SKRect(
                centerX - radius, centerY - radius,
                centerX + radius, centerY + radius);

            // 270° Bogen (von 135° bis 405°, also 270° Sweep)
            const float startAngle = 135f;
            const float totalSweep = 270f;

            canvas.DrawArc(arcRect, startAngle, totalSweep, false, bgPaint);

            // Farbiger Bogen (Feuchtigkeit)
            var sweepAngle = (float)(_moisture / 100.0 * totalSweep);
            if (sweepAngle > 0.5f)
            {
                var gaugeColor = GetMoistureColor((float)_moisture);

                using var gaugePaint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = strokeWidth,
                    IsAntialias = true,
                    StrokeCap = SKStrokeCap.Round
                };

                // Gradient entlang des Bogens
                using var shader = SKShader.CreateSweepGradient(
                    new SKPoint(centerX, centerY),
                    new[] {
                        new SKColor(239, 83, 80),   // Rot (trocken)
                        new SKColor(255, 167, 38),  // Orange
                        new SKColor(102, 187, 106), // Grün (optimal)
                        new SKColor(66, 165, 245),  // Blau (nass)
                        new SKColor(66, 165, 245)
                    },
                    new[] { 0f, 0.25f, 0.5f, 0.75f, 1f });

                gaugePaint.Shader = shader;

                canvas.DrawArc(arcRect, startAngle, sweepAngle, false, gaugePaint);

                // Glow-Effekt wenn bewässert wird
                if (_isWatering)
                {
                    using var blurFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4);
                    using var glowPaint = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = strokeWidth + 6,
                        Color = new SKColor(66, 165, 245, 60),
                        IsAntialias = true,
                        StrokeCap = SKStrokeCap.Round,
                        MaskFilter = blurFilter
                    };
                    canvas.DrawArc(arcRect, startAngle, sweepAngle, false, glowPaint);
                }
            }

            // Schwellenwert-Markierung (gelber Strich)
            var thresholdAngle = startAngle + (_threshold / 100f * totalSweep);
            var thresholdRad = thresholdAngle * MathF.PI / 180f;
            var markerInner = radius - strokeWidth;
            var markerOuter = radius + strokeWidth;

            using var markerPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2.5f,
                Color = new SKColor(255, 167, 38), // Orange
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };

            canvas.DrawLine(
                centerX + MathF.Cos(thresholdRad) * markerInner,
                centerY + MathF.Sin(thresholdRad) * markerInner,
                centerX + MathF.Cos(thresholdRad) * markerOuter,
                centerY + MathF.Sin(thresholdRad) * markerOuter,
                markerPaint);

            // Prozentwert in der Mitte
            var percentText = $"{_moisture:F0}";
            using var textPaint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                TextSize = size * 0.22f,
                Typeface = InterBold
            };
            canvas.DrawText(percentText, centerX, centerY + textPaint.TextSize * 0.1f, textPaint);

            // %-Zeichen kleiner
            using var unitPaint = new SKPaint
            {
                Color = new SKColor(138, 164, 188),
                IsAntialias = true,
                TextAlign = SKTextAlign.Left,
                TextSize = size * 0.09f,
                Typeface = InterSemiBold
            };
            var percentWidth = textPaint.MeasureText(percentText);
            canvas.DrawText("%", centerX + percentWidth / 2f + 2, centerY - textPaint.TextSize * 0.15f, unitPaint);

            // Zone-Name unter dem Wert
            if (!string.IsNullOrEmpty(_zoneName))
            {
                using var namePaint = new SKPaint
                {
                    Color = new SKColor(138, 164, 188),
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center,
                    TextSize = size * 0.08f,
                    Typeface = InterSemiBold
                };
                canvas.DrawText(_zoneName.ToUpper(), centerX, centerY + size * 0.18f, namePaint);
            }

            // Status-Text am unteren Bogenrand
            if (!string.IsNullOrEmpty(_statusText))
            {
                var statusColor = _isWatering
                    ? new SKColor(66, 165, 245) : new SKColor(102, 187, 106);
                using var statusPaint = new SKPaint
                {
                    Color = statusColor,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center,
                    TextSize = size * 0.07f,
                    Typeface = InterSemiBold
                };
                canvas.DrawText(_statusText, centerX, centerY + size * 0.32f, statusPaint);
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
