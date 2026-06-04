using SkiaSharp;
using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Graphics;

/// <summary>
/// Sonnenbahn-Diagramm (Himmelsdiagramm): die Tagesbahn der Sonne als Kurve in den Achsen
/// Azimut (X, Ost-Sued-West) und Elevation (Y, Hoehe ueber Horizont), plus die aktuelle
/// Sonnenposition und die Horizontlinie. Zeigt auf einen Blick Tageslaenge, Sonnenhoehe und
/// Auf-/Untergangsrichtung. Vollstaendig offline.
/// </summary>
public sealed class SunPathRenderer : IDisposable
{
    private const double AzMin = 30;   // X-Achse: Azimut-Bereich (deckt die DE-Tagesbahn ab)
    private const double AzMax = 330;
    private const double ElMax = 70;   // Y-Achse: Elevation-Maximum

    private static readonly SKColor BgColor = new(20, 24, 43);
    private static readonly SKColor GridColor = new(154, 164, 189, 45);
    private static readonly SKColor HorizonColor = new(255, 112, 67, 160);
    private static readonly SKColor PathColor = new(255, 179, 0);
    private static readonly SKColor SunColor = new(255, 213, 79);
    private static readonly SKColor TextColor = new(154, 164, 189);

    private readonly SKPaint _gridPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = GridColor };
    private readonly SKPaint _horizonPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = HorizonColor };
    private readonly SKPaint _pathPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, Color = PathColor, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKPaint _pathFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _sunGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _sunPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SunColor };
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Color = TextColor };
    private readonly SKFont _font = new(SKTypeface.Default, 10f);

    private float _lastFillTop = -1, _lastFillBottom = -1;

    public IReadOnlyList<SolarPosition> DayArc { get; set; } = [];
    public double CurrentAzimuth { get; set; }
    public double CurrentElevation { get; set; }
    public bool IsDaylight { get; set; }

    public void Render(SKCanvas canvas, SKRect bounds)
    {
        canvas.Clear(BgColor);

        const float padLeft = 28f, padBottom = 16f, padTop = 8f, padRight = 8f;
        var plot = new SKRect(bounds.Left + padLeft, bounds.Top + padTop,
                              bounds.Right - padRight, bounds.Bottom - padBottom);
        if (plot.Width <= 0 || plot.Height <= 0) return;

        float X(double az) => plot.Left + plot.Width * (float)((Math.Clamp(az, AzMin, AzMax) - AzMin) / (AzMax - AzMin));
        float Y(double el) => plot.Bottom - plot.Height * (float)(Math.Clamp(el, 0, ElMax) / ElMax);

        // Y-Gitter (Elevation 0/30/60)
        for (var el = 0; el <= 60; el += 30)
        {
            var y = Y(el);
            canvas.DrawLine(plot.Left, y, plot.Right, y, el == 0 ? _horizonPaint : _gridPaint);
            canvas.DrawText($"{el}°", plot.Left - 4f, y + 3f, SKTextAlign.Right, _font, _textPaint);
        }

        // X-Marker (Ost/Sued/West)
        DrawAzimuthLabel(canvas, X(90), plot.Bottom, "O");
        DrawAzimuthLabel(canvas, X(180), plot.Bottom, "S");
        DrawAzimuthLabel(canvas, X(270), plot.Bottom, "W");

        // Tagesbahn (nur Punkte ueber dem Horizont)
        var daylightPoints = DayArc.Where(p => p.Elevation >= -1).ToList();
        if (daylightPoints.Count >= 2)
        {
            using var path = new SKPath();
            using var fill = new SKPath();
            var first = true;
            foreach (var p in daylightPoints)
            {
                var x = X(p.Azimuth);
                var y = Y(p.Elevation);
                if (first) { path.MoveTo(x, y); fill.MoveTo(x, plot.Bottom); fill.LineTo(x, y); first = false; }
                else { path.LineTo(x, y); fill.LineTo(x, y); }
            }
            fill.LineTo(X(daylightPoints[^1].Azimuth), plot.Bottom);
            fill.Close();

            if (Math.Abs(_lastFillTop - plot.Top) > 0.5f || Math.Abs(_lastFillBottom - plot.Bottom) > 0.5f || _pathFillPaint.Shader == null)
            {
                _pathFillPaint.Shader?.Dispose();
                _pathFillPaint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, plot.Top), new SKPoint(0, plot.Bottom),
                    [PathColor.WithAlpha(90), PathColor.WithAlpha(8)], [0f, 1f], SKShaderTileMode.Clamp);
                _lastFillTop = plot.Top; _lastFillBottom = plot.Bottom;
            }
            canvas.DrawPath(fill, _pathFillPaint);
            canvas.DrawPath(path, _pathPaint);
        }

        // Aktuelle Sonne
        if (IsDaylight && CurrentElevation > 0)
        {
            var sx = X(CurrentAzimuth);
            var sy = Y(CurrentElevation);
            _sunGlowPaint.Shader?.Dispose();
            _sunGlowPaint.Shader = SKShader.CreateRadialGradient(
                new SKPoint(sx, sy), 16f, [SunColor.WithAlpha(150), SKColors.Transparent], [0.2f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawCircle(sx, sy, 16f, _sunGlowPaint);
            canvas.DrawCircle(sx, sy, 6f, _sunPaint);
        }
    }

    private void DrawAzimuthLabel(SKCanvas canvas, float x, float bottom, string label)
        => canvas.DrawText(label, x, bottom + 12f, SKTextAlign.Center, _font, _textPaint);

    public void Dispose()
    {
        _pathFillPaint.Shader?.Dispose();
        _sunGlowPaint.Shader?.Dispose();
        _gridPaint.Dispose();
        _horizonPaint.Dispose();
        _pathPaint.Dispose();
        _pathFillPaint.Dispose();
        _sunGlowPaint.Dispose();
        _sunPaint.Dispose();
        _textPaint.Dispose();
        _font.Dispose();
    }
}
