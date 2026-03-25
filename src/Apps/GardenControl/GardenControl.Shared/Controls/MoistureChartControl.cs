using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace GardenControl.Shared.Controls;

/// <summary>
/// SkiaSharp-basierter Feuchtigkeitsverlauf-Chart.
/// Liniengraph mit Farbfläche darunter, Schwellenwert-Linie,
/// und Bewässerungsereignisse als blaue Balken.
/// </summary>
public class MoistureChartControl : Control
{
    public static readonly StyledProperty<List<ChartDataPoint>> DataPointsProperty =
        AvaloniaProperty.Register<MoistureChartControl, List<ChartDataPoint>>(nameof(DataPoints));

    public static readonly StyledProperty<int> ThresholdPercentProperty =
        AvaloniaProperty.Register<MoistureChartControl, int>(nameof(ThresholdPercent), 40);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MoistureChartControl, string>(nameof(Title), "");

    public List<ChartDataPoint> DataPoints
    {
        get => GetValue(DataPointsProperty);
        set => SetValue(DataPointsProperty, value);
    }

    public int ThresholdPercent
    {
        get => GetValue(ThresholdPercentProperty);
        set => SetValue(ThresholdPercentProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    static MoistureChartControl()
    {
        AffectsRender<MoistureChartControl>(DataPointsProperty, ThresholdPercentProperty, TitleProperty);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.Custom(new ChartDrawOperation(bounds, DataPoints, ThresholdPercent, Title));
    }

    private class ChartDrawOperation : ICustomDrawOperation
    {
        // Gecachte Typefaces
        private static readonly SKTypeface InterSemiBold = SKTypeface.FromFamilyName("Inter",
            SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

        private readonly List<ChartDataPoint>? _points;
        private readonly int _threshold;
        private readonly string _title;

        public Rect Bounds { get; }

        public ChartDrawOperation(Rect bounds, List<ChartDataPoint>? points, int threshold, string title)
        {
            Bounds = bounds;
            _points = points;
            _threshold = threshold;
            _title = title ?? "";
        }

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
            if (leaseFeature == null) return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            if (canvas == null) return;

            var width = (float)Bounds.Width;
            var height = (float)Bounds.Height;
            var paddingLeft = 40f;
            var paddingRight = 12f;
            var paddingTop = 28f;
            var paddingBottom = 24f;
            var chartWidth = width - paddingLeft - paddingRight;
            var chartHeight = height - paddingTop - paddingBottom;

            // Hintergrund
            using var bgPaint = new SKPaint { Color = new SKColor(28, 46, 66, 180) };
            canvas.DrawRoundRect(0, 0, width, height, 12, 12, bgPaint);

            // Titel
            if (!string.IsNullOrEmpty(_title))
            {
                using var titlePaint = new SKPaint
                {
                    Color = new SKColor(240, 244, 248),
                    TextSize = 13f,
                    IsAntialias = true,
                    Typeface = InterSemiBold
                };
                canvas.DrawText(_title, paddingLeft, 18f, titlePaint);
            }

            // Grid-Linien (horizontal, alle 25%)
            using var gridPaint = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 15),
                StrokeWidth = 1f
            };
            using var labelPaint = new SKPaint
            {
                Color = new SKColor(90, 122, 150),
                TextSize = 10f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Right
            };

            for (var i = 0; i <= 4; i++)
            {
                var y = paddingTop + chartHeight * (1 - i / 4f);
                canvas.DrawLine(paddingLeft, y, width - paddingRight, y, gridPaint);
                canvas.DrawText($"{i * 25}%", paddingLeft - 4, y + 4, labelPaint);
            }

            // Schwellenwert-Linie (gestrichelt, orange)
            var thresholdY = paddingTop + chartHeight * (1 - _threshold / 100f);
            using var thresholdPaint = new SKPaint
            {
                Color = new SKColor(255, 167, 38, 150),
                StrokeWidth = 1.5f,
                IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, 0)
            };
            canvas.DrawLine(paddingLeft, thresholdY, width - paddingRight, thresholdY, thresholdPaint);

            // Schwellenwert-Label
            using var thresholdLabel = new SKPaint
            {
                Color = new SKColor(255, 167, 38),
                TextSize = 9f,
                IsAntialias = true
            };
            canvas.DrawText($"Schwelle {_threshold}%", width - paddingRight - 70, thresholdY - 4, thresholdLabel);

            if (_points == null || _points.Count < 2) return;

            // Datenpunkte normalisieren
            var minTime = _points.Min(p => p.TimestampUtc).Ticks;
            var maxTime = _points.Max(p => p.TimestampUtc).Ticks;
            var timeRange = Math.Max(maxTime - minTime, 1);

            // Feuchtigkeits-Linie + Fläche
            using var path = new SKPath();
            using var areaPath = new SKPath();

            var firstX = paddingLeft + (float)((_points[0].TimestampUtc.Ticks - minTime) / (double)timeRange) * chartWidth;
            var firstY = paddingTop + chartHeight * (1 - (float)_points[0].MoisturePercent / 100f);

            path.MoveTo(firstX, firstY);
            areaPath.MoveTo(firstX, paddingTop + chartHeight); // Unten starten
            areaPath.LineTo(firstX, firstY);

            for (var i = 1; i < _points.Count; i++)
            {
                var x = paddingLeft + (float)((_points[i].TimestampUtc.Ticks - minTime) / (double)timeRange) * chartWidth;
                var y = paddingTop + chartHeight * (1 - (float)_points[i].MoisturePercent / 100f);
                path.LineTo(x, y);
                areaPath.LineTo(x, y);
            }

            // Fläche schließen
            var lastX = paddingLeft + (float)((_points[^1].TimestampUtc.Ticks - minTime) / (double)timeRange) * chartWidth;
            areaPath.LineTo(lastX, paddingTop + chartHeight);
            areaPath.Close();

            // Fläche unter Kurve (Gradient)
            using var areaPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            using var areaShader = SKShader.CreateLinearGradient(
                new SKPoint(0, paddingTop),
                new SKPoint(0, paddingTop + chartHeight),
                new[] { new SKColor(102, 187, 106, 60), new SKColor(102, 187, 106, 5) },
                null, SKShaderTileMode.Clamp);
            areaPaint.Shader = areaShader;
            canvas.DrawPath(areaPath, areaPaint);

            // Bewässerungsereignisse als blaue Balken
            foreach (var point in _points.Where(p => p.WasWatering))
            {
                var x = paddingLeft + (float)((point.TimestampUtc.Ticks - minTime) / (double)timeRange) * chartWidth;
                using var waterPaint = new SKPaint
                {
                    Color = new SKColor(66, 165, 245, 40),
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(x - 1.5f, paddingTop, 3f, chartHeight, waterPaint);
            }

            // Linie zeichnen
            using var linePaint = new SKPaint
            {
                Color = new SKColor(102, 187, 106),
                StrokeWidth = 2.5f,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };
            canvas.DrawPath(path, linePaint);

            // Aktueller Wert: Punkt am Ende
            var currentY = paddingTop + chartHeight * (1 - (float)_points[^1].MoisturePercent / 100f);
            using var dotPaint = new SKPaint
            {
                Color = new SKColor(102, 187, 106),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawCircle(lastX, currentY, 4f, dotPaint);
            dotPaint.Color = new SKColor(102, 187, 106, 60);
            canvas.DrawCircle(lastX, currentY, 8f, dotPaint);

            // Zeit-Achse (ein paar Labels)
            using var timePaint = new SKPaint
            {
                Color = new SKColor(90, 122, 150),
                TextSize = 9f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };

            var timeLabels = Math.Min(6, _points.Count);
            for (var i = 0; i < timeLabels; i++)
            {
                var idx = i * (_points.Count - 1) / Math.Max(timeLabels - 1, 1);
                var x = paddingLeft + (float)((_points[idx].TimestampUtc.Ticks - minTime) / (double)timeRange) * chartWidth;
                var timeStr = _points[idx].TimestampUtc.ToLocalTime().ToString("HH:mm");
                canvas.DrawText(timeStr, x, height - 6f, timePaint);
            }
        }

        public void Dispose() { }
        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;
    }
}

/// <summary>
/// Datenpunkt für den Feuchtigkeitsverlauf-Chart
/// </summary>
public class ChartDataPoint
{
    public DateTime TimestampUtc { get; set; }
    public double MoisturePercent { get; set; }
    public bool WasWatering { get; set; }
}
