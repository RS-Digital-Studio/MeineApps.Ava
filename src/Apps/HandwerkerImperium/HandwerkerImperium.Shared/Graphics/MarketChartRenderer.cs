using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// V7 (): SkiaSharp-Renderer fuer den 24h-Preisverlauf eines Materials.
///
/// Zeichnet eine geglaettete Bezier-Linie ueber 24 Stunden mit Min/Max-Annotation, der aktuellen
/// Stunde als pulsierender Punkt, einem grid und Gradient-Fuellung unter der Kurve.
///
/// IDisposable mit gecachten SKPaint/SKFont — GC-Druck bei 15-24fps Render-Loop.
/// </summary>
public sealed class MarketChartRenderer : IDisposable
{
    private bool _disposed;
    private float _time; // Akkumuliert deltaTime fuer Pulse-Animation

    // Layout-Konstanten
    private const float Padding = 24f;
    private const float AxisHeight = 18f;

    // Farben
    private static readonly SKColor LineColor = new(0xF9, 0x73, 0x16);          // Craft-Orange (Markt-Akzent)
    private static readonly SKColor LineFillTop = new(0xF9, 0x73, 0x16, 110);   // Bezier-Fill oben (halbtransparent)
    private static readonly SKColor LineFillBottom = new(0xF9, 0x73, 0x16, 0);  // Bezier-Fill unten (transparent)
    private static readonly SKColor GridColor = new(0xFF, 0xFF, 0xFF, 30);
    private static readonly SKColor TextColor = new(0xFF, 0xFF, 0xFF, 200);
    private static readonly SKColor MinMaxColor = new(0xFF, 0xFF, 0xFF, 220);
    private static readonly SKColor NowColor = new(0xFF, 0xD7, 0x00);            // Craft-Gold

    // Gecachte Paints (geteilt, IDisposable)
    private readonly SKPaint _linePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2.5f,
        Color = LineColor,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round
    };
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _gridPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f,
        Color = GridColor,
        PathEffect = SKPathEffect.CreateDash([3f, 4f], 0f)
    };
    private readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _glowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f)
    };
    private readonly SKFont _axisFont = new() { Size = 10f, Edging = SKFontEdging.Antialias };
    private readonly SKFont _valueFont = new() { Size = 11f, Edging = SKFontEdging.Antialias, Embolden = true };
    private readonly SKPath _bezierPath = new();
    private readonly SKPath _fillPath = new();
    // wiederverwendbarer Bezier-Punkte-Buffer — frueher SKPoint[24] pro
    // Frame allokiert (360 Heap-Allocs/s bei 15fps).
    private readonly SKPoint[] _pointsBuffer = new SKPoint[24];

    /// <summary>
    /// Rendert den 24-Punkte-Preisverlauf in <paramref name="bounds"/>.
    /// </summary>
    /// <param name="series">24-Werte-Array (Index 0 = 00:00 UTC, 23 = 23:00 UTC).</param>
    /// <param name="currentHour">Aktuelle UTC-Stunde 0-23 (Now-Indikator).</param>
    /// <param name="deltaTime">Sekunden seit letztem Frame (Pulse-Animation).</param>
    public void Render(SKCanvas canvas, SKRect bounds, decimal[]? series, int currentHour, float deltaTime)
    {
        if (series is null || series.Length != 24) return;
        _time += deltaTime;

        // Plot-Bereich (mit Padding fuer Achsen-Labels)
        var plot = new SKRect(
            bounds.Left + Padding,
            bounds.Top + Padding * 0.5f,
            bounds.Right - Padding * 0.5f,
            bounds.Bottom - AxisHeight - 4f);
        if (plot.Width <= 0 || plot.Height <= 0) return;

        // Min/Max der Serie
        decimal min = series[0];
        decimal max = series[0];
        for (int i = 1; i < 24; i++)
        {
            if (series[i] < min) min = series[i];
            if (series[i] > max) max = series[i];
        }
        decimal span = max - min;
        if (span <= 0) span = 1m; // Flat-Line-Fallback

        // Grid (3 horizontale Linien)
        for (int g = 0; g <= 3; g++)
        {
            float gy = plot.Top + plot.Height * g / 3f;
            canvas.DrawLine(plot.Left, gy, plot.Right, gy, _gridPaint);
        }

        // X-Achse: Stunden-Labels alle 6h (0/6/12/18/24)
        _axisFont.MeasureText("00", out var measureBounds);
        _ = measureBounds;
        _fillPaint.Color = TextColor;
        for (int hr = 0; hr <= 24; hr += 6)
        {
            float xPos = plot.Left + plot.Width * hr / 23f;
            string label = hr == 24 ? "24" : hr.ToString("00");
            float tw = _axisFont.MeasureText(label);
            canvas.DrawText(label, xPos - tw * 0.5f, plot.Bottom + AxisHeight, _axisFont, _fillPaint);
        }

        // Bezier-Punkte berechnen — P-H02: in den wiederverwendbaren Instanz-Buffer schreiben.
        var points = _pointsBuffer;
        for (int i = 0; i < 24; i++)
        {
            float x = plot.Left + plot.Width * i / 23f;
            float t = (float)((double)(series[i] - min) / (double)span);
            float y = plot.Bottom - t * plot.Height;
            points[i] = new SKPoint(x, y);
        }

        // Bezier-Linie zeichnen (geglaettete Catmull-Rom-aehnliche Kurven via QuadTo).
        // Wir nehmen den Midpoint-Ansatz: zwischen zwei Punkten ist der Control-Point der Mittelpunkt
        // des vorherigen Punktes → erzeugt eine glatte Kurve ohne Overshoot.
        _bezierPath.Reset();
        _fillPath.Reset();
        _bezierPath.MoveTo(points[0]);
        _fillPath.MoveTo(points[0].X, plot.Bottom);
        _fillPath.LineTo(points[0]);
        for (int i = 1; i < points.Length; i++)
        {
            var mid = new SKPoint(
                (points[i - 1].X + points[i].X) * 0.5f,
                (points[i - 1].Y + points[i].Y) * 0.5f);
            _bezierPath.QuadTo(points[i - 1], mid);
            _fillPath.QuadTo(points[i - 1], mid);
        }
        _bezierPath.LineTo(points[^1]);
        _fillPath.LineTo(points[^1]);
        _fillPath.LineTo(points[^1].X, plot.Bottom);
        _fillPath.Close();

        // Gradient-Fuellung
        using (var shader = SKShader.CreateLinearGradient(
            new SKPoint(plot.Left, plot.Top),
            new SKPoint(plot.Left, plot.Bottom),
            [LineFillTop, LineFillBottom],
            SKShaderTileMode.Clamp))
        {
            _fillPaint.Shader = shader;
            _fillPaint.Color = SKColors.White;
            canvas.DrawPath(_fillPath, _fillPaint);
            _fillPaint.Shader = null;
        }

        // Bezier-Linie
        canvas.DrawPath(_bezierPath, _linePaint);

        // Min/Max-Annotation: weisse Punkte + Beschriftung
        int minIdx = 0;
        int maxIdx = 0;
        for (int i = 1; i < 24; i++)
        {
            if (series[i] < series[minIdx]) minIdx = i;
            if (series[i] > series[maxIdx]) maxIdx = i;
        }
        DrawValueDot(canvas, points[minIdx], $"Min {FormatPrice(series[minIdx])}", MinMaxColor);
        DrawValueDot(canvas, points[maxIdx], $"Max {FormatPrice(series[maxIdx])}", MinMaxColor);

        // "Jetzt"-Indikator (pulsierender Punkt + Beschriftung) auf currentHour
        int clampedHour = Math.Clamp(currentHour, 0, 23);
        float pulse = 0.6f + 0.4f * (float)Math.Sin(_time * 4f);
        var nowPos = points[clampedHour];

        // Glow um den aktuellen Punkt
        _glowPaint.Color = NowColor.WithAlpha((byte)(80 * pulse));
        canvas.DrawCircle(nowPos, 8f + pulse * 3f, _glowPaint);

        // Kern-Punkt
        _dotPaint.Color = NowColor;
        canvas.DrawCircle(nowPos, 4f, _dotPaint);
        _dotPaint.Color = new SKColor(0x33, 0x1A, 0x00);
        canvas.DrawCircle(nowPos, 2f, _dotPaint);

        // Vertikal-Linie zur Stunden-Achse
        using (var dashEffect = SKPathEffect.CreateDash([4f, 4f], _time * 8f))
        {
            _gridPaint.PathEffect = dashEffect;
            _gridPaint.Color = NowColor.WithAlpha(140);
            canvas.DrawLine(nowPos.X, nowPos.Y + 6f, nowPos.X, plot.Bottom, _gridPaint);
            _gridPaint.Color = GridColor;
        }
        // Static-Dash zuruecksetzen
        _gridPaint.PathEffect = SKPathEffect.CreateDash([3f, 4f], 0f);
    }

    private void DrawValueDot(SKCanvas canvas, SKPoint p, string label, SKColor color)
    {
        _dotPaint.Color = color;
        canvas.DrawCircle(p, 3.5f, _dotPaint);
        _fillPaint.Color = color;
        float tw = _valueFont.MeasureText(label);
        // Label oberhalb (mit ein bisschen Versatz, damit nicht alle uebereinander stehen)
        canvas.DrawText(label, p.X - tw * 0.5f, p.Y - 8f, _valueFont, _fillPaint);
    }

    private static string FormatPrice(decimal price)
    {
        if (price >= 1_000_000m) return $"{price / 1_000_000m:0.#}M";
        if (price >= 1_000m) return $"{price / 1_000m:0.#}K";
        return ((int)price).ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _linePaint.Dispose();
        _fillPaint.Dispose();
        _gridPaint.Dispose();
        _dotPaint.Dispose();
        _glowPaint.Dispose();
        _axisFont.Dispose();
        _valueFont.Dispose();
        _bezierPath.Dispose();
        _fillPath.Dispose();
    }
}
