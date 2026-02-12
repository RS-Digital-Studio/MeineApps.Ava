using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace MeineApps.UI.Controls;

/// <summary>
/// Kreisförmiger Fortschrittsanzeiger (Ring).
/// Value 0.0 bis 1.0, zeichnet einen Fortschrittsring von 12-Uhr-Position im Uhrzeigersinn.
/// Verwendung: Timer-Countdown, Stoppuhr-Visualisierung.
/// </summary>
public class CircularProgress : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<CircularProgress, double>(nameof(Value), 0.0);

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<CircularProgress, double>(nameof(StrokeWidth), 8.0);

    public static readonly StyledProperty<IBrush?> StrokeBrushProperty =
        AvaloniaProperty.Register<CircularProgress, IBrush?>(nameof(StrokeBrush));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<CircularProgress, IBrush?>(nameof(TrackBrush));

    /// <summary>Fortschritt von 0.0 (leer) bis 1.0 (voll).</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Breite des Rings in Pixel.</summary>
    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    /// <summary>Farbe des Fortschritts-Rings.</summary>
    public IBrush? StrokeBrush
    {
        get => GetValue(StrokeBrushProperty);
        set => SetValue(StrokeBrushProperty, value);
    }

    /// <summary>Farbe des Hintergrund-Tracks.</summary>
    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    static CircularProgress()
    {
        AffectsRender<CircularProgress>(ValueProperty, StrokeWidthProperty, StrokeBrushProperty, TrackBrushProperty);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;

        var strokeWidth = StrokeWidth;
        var radius = (size - strokeWidth) / 2;
        if (radius <= 0) return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);

        // Hintergrund-Ring (Track)
        var trackBrush = TrackBrush;
        if (trackBrush != null)
        {
            var trackPen = new Pen(trackBrush, strokeWidth, lineCap: PenLineCap.Round);
            context.DrawEllipse(null, trackPen, center, radius, radius);
        }

        // Fortschritts-Ring
        var value = Math.Clamp(Value, 0.0, 1.0);
        if (value <= 0 || StrokeBrush == null) return;

        var progressPen = new Pen(StrokeBrush, strokeWidth, lineCap: PenLineCap.Round);

        if (value >= 1.0)
        {
            // Vollständiger Kreis
            context.DrawEllipse(null, progressPen, center, radius, radius);
            return;
        }

        // Bogen von 12-Uhr-Position im Uhrzeigersinn
        var startAngle = -Math.PI / 2;
        var sweepAngle = 2 * Math.PI * value;

        var startX = center.X + radius * Math.Cos(startAngle);
        var startY = center.Y + radius * Math.Sin(startAngle);
        var endX = center.X + radius * Math.Cos(startAngle + sweepAngle);
        var endY = center.Y + radius * Math.Sin(startAngle + sweepAngle);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(startX, startY), false);
            ctx.ArcTo(
                new Point(endX, endY),
                new Size(radius, radius),
                0,
                value > 0.5,
                SweepDirection.Clockwise);
        }

        context.DrawGeometry(null, progressPen, geometry);
    }
}
