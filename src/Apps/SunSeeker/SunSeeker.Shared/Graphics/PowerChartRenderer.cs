using SkiaSharp;
using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Graphics;

/// <summary>
/// Live-Trend der Solar-Leistung (Watt über Zeit). Gefüllte Fläche + Linie + aktueller Punkt,
/// Y-Gitter mit Watt-Beschriftung. Erwartet eine zeitlich aufsteigend sortierte Sample-Liste.
/// Gecachte Paints; der Flächen-Shader wird nur bei Größen-/Skalen-Änderung neu erzeugt.
/// </summary>
public sealed class PowerChartRenderer : IDisposable
{
    /// <summary>Y-Achsen-Maximum (Watt). Default = Panel-Nennleistung.</summary>
    public double MaxWatts { get; set; } = 400;

    private static readonly SKColor BgColor = new(20, 24, 43);
    private static readonly SKColor GridColor = new(154, 164, 189, 50);
    private static readonly SKColor LineColor = new(255, 179, 0);
    private static readonly SKColor TextColor = new(154, 164, 189);

    private readonly SKPaint _gridPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = GridColor };
    private readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, Color = LineColor, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = LineColor };
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Color = TextColor };
    private readonly SKFont _font = new(SKTypeface.Default, 10f);

    private float _lastFillH = -1;

    public void Render(SKCanvas canvas, SKRect bounds, IReadOnlyList<PowerSample> samples)
    {
        canvas.Clear(BgColor);

        const float padLeft = 36f, padBottom = 16f, padTop = 8f, padRight = 8f;
        var plot = new SKRect(bounds.Left + padLeft, bounds.Top + padTop,
                              bounds.Right - padRight, bounds.Bottom - padBottom);
        if (plot.Width <= 0 || plot.Height <= 0) return;

        // Y-Gitter (0, 25, 50, 75, 100 %)
        for (var i = 0; i <= 4; i++)
        {
            var y = plot.Bottom - plot.Height * i / 4f;
            canvas.DrawLine(plot.Left, y, plot.Right, y, _gridPaint);
            var watts = MaxWatts * i / 4.0;
            canvas.DrawText($"{watts:0}", plot.Left - 4f, y + 3f, SKTextAlign.Right, _font, _textPaint);
        }

        if (samples.Count < 2) return;

        var n = samples.Count;
        float X(int i) => plot.Left + plot.Width * i / (n - 1);
        float Y(double w) => plot.Bottom - plot.Height * (float)Math.Clamp(w / MaxWatts, 0, 1);

        using var line = new SKPath();
        using var fill = new SKPath();
        line.MoveTo(X(0), Y(samples[0].SolarWatts));
        fill.MoveTo(X(0), plot.Bottom);
        fill.LineTo(X(0), Y(samples[0].SolarWatts));
        for (var i = 1; i < n; i++)
        {
            line.LineTo(X(i), Y(samples[i].SolarWatts));
            fill.LineTo(X(i), Y(samples[i].SolarWatts));
        }
        fill.LineTo(X(n - 1), plot.Bottom);
        fill.Close();

        if (Math.Abs(_lastFillH - plot.Height) > 0.5f || _fillPaint.Shader == null)
        {
            _fillPaint.Shader?.Dispose();
            _fillPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, plot.Top), new SKPoint(0, plot.Bottom),
                [LineColor.WithAlpha(110), LineColor.WithAlpha(10)],
                [0f, 1f], SKShaderTileMode.Clamp);
            _lastFillH = plot.Height;
        }

        canvas.DrawPath(fill, _fillPaint);
        canvas.DrawPath(line, _linePaint);
        canvas.DrawCircle(X(n - 1), Y(samples[n - 1].SolarWatts), 4f, _dotPaint);
    }

    public void Dispose()
    {
        _fillPaint.Shader?.Dispose();
        _gridPaint.Dispose();
        _linePaint.Dispose();
        _fillPaint.Dispose();
        _dotPaint.Dispose();
        _textPaint.Dispose();
        _font.Dispose();
    }
}
