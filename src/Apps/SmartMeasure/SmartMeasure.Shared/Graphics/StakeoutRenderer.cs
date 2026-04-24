using SkiaSharp;

namespace SmartMeasure.Shared.Graphics;

/// <summary>
/// Kompass-Pfeil der zum Ziel-Punkt zeigt. Farbcodiert nach Distanz:
/// - Grün bei &lt; 10cm (Ziel erreicht)
/// - Gelb bei 10cm - 1m
/// - Orange bei 1m - 5m
/// - Rot bei &gt; 5m
///
/// Eingabe: HeadingDeg = Bewegungs- oder Magnet-Heading (0° = Nord, 90° = Ost),
/// BearingDeg = Richtung zum Ziel (gleiche Konvention), DistanceMeters, AltitudeDelta.
/// Der Pfeil zeigt relativ: (bearing - heading) → wenn heading=90° und bearing=90°,
/// zeigt Pfeil nach oben (Ziel ist geradeaus).
/// </summary>
public sealed class StakeoutRenderer : IDisposable
{
    public double HeadingDeg { get; set; }
    public double BearingDeg { get; set; }
    public double DistanceMeters { get; set; } = double.PositiveInfinity;
    public double AltitudeDeltaMeters { get; set; }
    public bool HasTarget { get; set; }
    public string TargetLabel { get; set; } = string.Empty;

    private readonly SKPaint _bgPaint = new() { Color = new SKColor(22, 33, 62) };
    private readonly SKPaint _ringPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f,
        Color = new SKColor(255, 255, 255, 30)
    };
    private readonly SKPaint _arrowFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _arrowStrokePaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3f,
        Color = new SKColor(255, 255, 255, 180)
    };
    private readonly SKPaint _centerDotPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(255, 255, 255, 100)
    };
    private readonly SKPaint _textPaint = new()
    {
        IsAntialias = true, Color = new SKColor(255, 255, 255, 220)
    };
    private readonly SKPaint _textSmallPaint = new()
    {
        IsAntialias = true, Color = new SKColor(180, 180, 180, 220)
    };
    private readonly SKPaint _targetReachedBgPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(76, 175, 80, 50)
    };

    private readonly SKFont _distanceFont = new(SKTypeface.Default, 34f) { Edging = SKFontEdging.SubpixelAntialias };
    private readonly SKFont _labelFont = new(SKTypeface.Default, 14f);
    private readonly SKFont _infoFont = new(SKTypeface.Default, 11f);

    public void Render(SKCanvas canvas, SKRect bounds)
    {
        canvas.Clear(_bgPaint.Color);
        var cx = bounds.MidX;
        var cy = bounds.MidY;
        var radius = Math.Min(bounds.Width, bounds.Height) * 0.38f;

        // Hintergrund-Glow bei erreichtem Ziel
        if (HasTarget && DistanceMeters < 0.1)
            canvas.DrawCircle(cx, cy, radius * 1.1f, _targetReachedBgPaint);

        // Äußere Kreise (Distanz-Ringe: 1m, 5m, 10m)
        canvas.DrawCircle(cx, cy, radius, _ringPaint);
        canvas.DrawCircle(cx, cy, radius * 0.66f, _ringPaint);
        canvas.DrawCircle(cx, cy, radius * 0.33f, _ringPaint);

        if (!HasTarget)
        {
            canvas.DrawText("Kein Ziel ausgewählt", cx, cy, SKTextAlign.Center, _labelFont, _textPaint);
            return;
        }

        // Pfeil-Farbe nach Distanz
        var arrowColor = DistanceMeters switch
        {
            < 0.1 => new SKColor(76, 175, 80),       // Grün
            < 1.0 => new SKColor(255, 235, 59),      // Gelb
            < 5.0 => new SKColor(255, 152, 0),       // Orange
            _ => new SKColor(239, 83, 80)            // Rot
        };
        _arrowFillPaint.Color = arrowColor;

        // Pfeil-Rotation relativ: 0° = Nord im Display, bearing-heading = wo das Ziel liegt
        var relativeAngle = (BearingDeg - HeadingDeg + 360.0) % 360.0;
        var rad = relativeAngle * Math.PI / 180.0;

        // Pfeil-Länge basiert auf Distanz (nah = klein, fern = lang bis max radius*0.8)
        var arrowLen = (float)Math.Min(radius * 0.8, radius * 0.3 + Math.Min(DistanceMeters, 20) * radius * 0.025);

        // Pfeil zeichnen: Startpunkt in Center, Spitze in relativer Richtung
        var tipX = cx + (float)Math.Sin(rad) * arrowLen;
        var tipY = cy - (float)Math.Cos(rad) * arrowLen; // Y negativ → nach oben

        using var arrowPath = new SKPath();
        // Pfeil als Dreieck mit Schaft
        var headWidth = arrowLen * 0.3f;
        var shaftWidth = arrowLen * 0.15f;
        var headLen = arrowLen * 0.4f;

        // Lokales Koordinatensystem um Center rotieren
        var cosR = (float)Math.Cos(rad);
        var sinR = (float)Math.Sin(rad);

        // Pfeil ohne Rotation: zeigt von (0,0) nach (0, -arrowLen)
        var localPoints = new[]
        {
            new SKPoint(0, -arrowLen),                     // Spitze
            new SKPoint(-headWidth / 2, -arrowLen + headLen),
            new SKPoint(-shaftWidth / 2, -arrowLen + headLen),
            new SKPoint(-shaftWidth / 2, 0),
            new SKPoint(shaftWidth / 2, 0),
            new SKPoint(shaftWidth / 2, -arrowLen + headLen),
            new SKPoint(headWidth / 2, -arrowLen + headLen),
        };

        // Rotation + Translation (Rot-Matrix: x'=x*cos-y*sin, y'=x*sin+y*cos)
        // Pfeil zeigt nach Norden (0°), rotiere um relativeAngle im Uhrzeigersinn (= Winkel-Konvention)
        arrowPath.MoveTo(
            cx + localPoints[0].X * cosR - localPoints[0].Y * sinR,
            cy + localPoints[0].X * sinR + localPoints[0].Y * cosR);
        for (var i = 1; i < localPoints.Length; i++)
        {
            var p = localPoints[i];
            arrowPath.LineTo(cx + p.X * cosR - p.Y * sinR, cy + p.X * sinR + p.Y * cosR);
        }
        arrowPath.Close();

        canvas.DrawPath(arrowPath, _arrowFillPaint);
        canvas.DrawPath(arrowPath, _arrowStrokePaint);

        // Center-Dot
        canvas.DrawCircle(cx, cy, 4f, _centerDotPaint);

        // Distanz-Text unter dem Pfeil
        string distText;
        if (DistanceMeters < 1.0)
            distText = $"{(DistanceMeters * 100):F1} cm";
        else if (DistanceMeters < 10.0)
            distText = $"{DistanceMeters:F2} m";
        else
            distText = $"{DistanceMeters:F1} m";
        canvas.DrawText(distText, cx, bounds.Bottom - 72, SKTextAlign.Center, _distanceFont, _textPaint);

        // Ziel-Label
        canvas.DrawText(TargetLabel, cx, bounds.Bottom - 44, SKTextAlign.Center, _labelFont, _textPaint);

        // Höhen-Delta wenn signifikant
        if (Math.Abs(AltitudeDeltaMeters) > 0.05)
        {
            var altText = AltitudeDeltaMeters > 0
                ? $"Ziel liegt {AltitudeDeltaMeters * 100:F0} cm höher"
                : $"Ziel liegt {-AltitudeDeltaMeters * 100:F0} cm tiefer";
            canvas.DrawText(altText, cx, bounds.Bottom - 24, SKTextAlign.Center, _infoFont, _textSmallPaint);
        }
    }

    public void Dispose()
    {
        _bgPaint.Dispose();
        _ringPaint.Dispose();
        _arrowFillPaint.Dispose();
        _arrowStrokePaint.Dispose();
        _centerDotPaint.Dispose();
        _textPaint.Dispose();
        _textSmallPaint.Dispose();
        _targetReachedBgPaint.Dispose();
        _distanceFont.Dispose();
        _labelFont.Dispose();
        _infoFont.Dispose();
    }
}
