using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WorkTimePro.Controls;

/// <summary>
/// Kreisförmiger Fortschrittsring (0-100%) für die Tagesansicht.
/// Zeichnet einen Track-Kreis und darüber einen farbigen Fortschritts-Arc.
/// </summary>
public class CircularProgressControl : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<CircularProgressControl, double>(nameof(Progress));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<CircularProgressControl, IBrush?>(nameof(TrackBrush),
            new SolidColorBrush(Color.Parse("#333333")));

    public static readonly StyledProperty<IBrush?> ProgressBrushProperty =
        AvaloniaProperty.Register<CircularProgressControl, IBrush?>(nameof(ProgressBrush),
            new SolidColorBrush(Color.Parse("#4CAF50")));

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<CircularProgressControl, double>(nameof(StrokeWidth), 6.0);

    /// <summary>Fortschritt 0-100</summary>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>Farbe des Hintergrund-Kreises</summary>
    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    /// <summary>Farbe des Fortschritts-Arcs</summary>
    public IBrush? ProgressBrush
    {
        get => GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    /// <summary>Strichbreite des Rings</summary>
    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    static CircularProgressControl()
    {
        AffectsRender<CircularProgressControl>(ProgressProperty, TrackBrushProperty,
            ProgressBrushProperty, StrokeWidthProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 1 || h < 1) return;

        var center = new Point(w / 2, h / 2);
        var radius = Math.Min(w, h) / 2 - StrokeWidth / 2;
        if (radius < 1) return;

        // Track-Kreis (Hintergrund)
        if (TrackBrush != null)
        {
            var trackPen = new Pen(TrackBrush, StrokeWidth);
            context.DrawEllipse(null, trackPen, center, radius, radius);
        }

        // Fortschritts-Arc
        var progress = Math.Clamp(Progress, 0, 100);
        if (progress <= 0 || ProgressBrush == null) return;

        var sweepAngle = progress / 100.0 * 360.0;

        // Start bei 12 Uhr (-90°), Uhrzeigersinn
        var startAngle = -90.0;
        var startRad = startAngle * Math.PI / 180;
        var endRad = (startAngle + sweepAngle) * Math.PI / 180;

        var startPoint = new Point(
            center.X + radius * Math.Cos(startRad),
            center.Y + radius * Math.Sin(startRad));

        var endPoint = new Point(
            center.X + radius * Math.Cos(endRad),
            center.Y + radius * Math.Sin(endRad));

        var isLargeArc = sweepAngle > 180;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(startPoint, false);
            ctx.ArcTo(endPoint, new Size(radius, radius), 0,
                isLargeArc, SweepDirection.Clockwise);
        }

        var progressPen = new Pen(ProgressBrush, StrokeWidth, lineCap: PenLineCap.Round);
        context.DrawGeometry(null, progressPen, geometry);
    }
}
