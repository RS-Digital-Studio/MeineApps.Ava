using SkiaSharp;
using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Graphics;

/// <summary>
/// Live-Ausricht-Kompass. Norden ist oben fix. Drei Marker auf dem Ring: die SONNE (golden,
/// Position aus Azimut/Elevation), die SOLL-Ausrichtung (gruen) und die aktuelle PANEL-Richtung
/// (kraeftiger Pfeil, Farbe = Ausricht-Qualitaet). Im Zentrum ein Neigungs-Bogen (Ist gegen Soll)
/// und die Live-Werte. Gecachte Paints/Fonts (keine Allokation pro Frame), Shader-Caching fuer
/// den Qualitaets-Glow.
/// </summary>
public sealed class SunCompassRenderer : IDisposable
{
    // Pro Frame gesetzt
    public double PanelAzimuth { get; set; }
    public double TargetAzimuth { get; set; }
    public double SunAzimuth { get; set; }
    public double SunElevation { get; set; }
    public double PanelTilt { get; set; }
    public double TargetTilt { get; set; }
    public bool AzimuthReliable { get; set; } = true;
    public bool IsDaylight { get; set; } = true;
    public AlignmentQuality Quality { get; set; } = AlignmentQuality.Poor;

    private static readonly SKColor BgColor = new(20, 24, 43);
    private static readonly SKColor RingColor = new(154, 164, 189, 90);
    private static readonly SKColor TickColor = new(154, 164, 189, 170);
    private static readonly SKColor TickMinorColor = new(154, 164, 189, 60);
    private static readonly SKColor TextColor = new(240, 237, 230);
    private static readonly SKColor TextDimColor = new(154, 164, 189);
    private static readonly SKColor NorthColor = new(239, 83, 80);
    private static readonly SKColor SunColor = new(255, 213, 79);
    private static readonly SKColor SunDimColor = new(120, 124, 140);
    private static readonly SKColor TargetColor = new(67, 160, 71);
    private static readonly SKColor TiltTrackColor = new(154, 164, 189, 70);

    private static SKColor QualityColor(AlignmentQuality q) => q switch
    {
        AlignmentQuality.Excellent => new SKColor(67, 160, 71),
        AlignmentQuality.Good => new SKColor(156, 204, 101),
        AlignmentQuality.Fair => new SKColor(255, 193, 7),
        _ => new SKColor(239, 83, 80),
    };

    private readonly SKPaint _bgPaint = new() { Color = BgColor };
    private readonly SKPaint _ringPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = RingColor };
    private readonly SKPaint _tickPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f, Color = TickColor };
    private readonly SKPaint _tickMinorPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f, Color = TickMinorColor };
    private readonly SKPaint _cardinalPaint = new() { IsAntialias = true, Color = TextColor };
    private readonly SKPaint _northPaint = new() { IsAntialias = true, Color = NorthColor };
    private readonly SKPaint _sunPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SunColor };
    private readonly SKPaint _sunGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _targetPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = TargetColor };
    private readonly SKPaint _targetStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = TargetColor };
    private readonly SKPaint _panelArrowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _tiltTrackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 6f, Color = TiltTrackColor, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _tiltTargetPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 6f, Color = TargetColor, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _tiltValuePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 6f, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _centerTextPaint = new() { IsAntialias = true, Color = TextColor };
    private readonly SKPaint _centerDimPaint = new() { IsAntialias = true, Color = TextDimColor };

    private readonly SKFont _cardinalFont = new(SKTypeface.Default, 16f) { Embolden = true };
    private readonly SKFont _degreeFont = new(SKTypeface.Default, 10f);
    private readonly SKFont _centerBigFont = new(SKTypeface.Default, 30f) { Embolden = true };
    private readonly SKFont _centerSmallFont = new(SKTypeface.Default, 12f);

    private SKColor _lastGlowColor = SKColors.Transparent;
    private float _lastGlowCx, _lastGlowCy, _lastGlowRadius;

    public void Render(SKCanvas canvas, SKRect bounds)
    {
        canvas.Clear(BgColor);

        var size = Math.Min(bounds.Width, bounds.Height);
        var cx = bounds.MidX;
        var cy = bounds.MidY;
        var outerRadius = size * 0.40f;

        DrawQualityGlow(canvas, cx, cy, outerRadius);
        DrawCompassRing(canvas, cx, cy, outerRadius);
        DrawSunMarker(canvas, cx, cy, outerRadius);
        DrawTargetMarker(canvas, cx, cy, outerRadius);
        DrawPanelArrow(canvas, cx, cy, outerRadius * 0.78f);
        DrawTiltArc(canvas, cx, cy, outerRadius * 0.46f);
        DrawCenterText(canvas, cx, cy);
    }

    private void DrawQualityGlow(SKCanvas canvas, float cx, float cy, float radius)
    {
        var glowRadius = radius * 1.18f;
        var baseColor = AzimuthReliable && IsDaylight ? QualityColor(Quality) : new SKColor(120, 124, 140);
        var glowColor = baseColor.WithAlpha(70);

        if (_lastGlowColor != glowColor || Math.Abs(_lastGlowCx - cx) > 0.5f
            || Math.Abs(_lastGlowCy - cy) > 0.5f || Math.Abs(_lastGlowRadius - glowRadius) > 0.5f)
        {
            _glowPaint.Shader?.Dispose();
            _glowPaint.Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy), glowRadius,
                [glowColor, SKColors.Transparent], [0.55f, 1.0f], SKShaderTileMode.Clamp);
            _lastGlowColor = glowColor;
            _lastGlowCx = cx; _lastGlowCy = cy; _lastGlowRadius = glowRadius;
        }

        canvas.DrawCircle(cx, cy, glowRadius, _glowPaint);
    }

    private void DrawCompassRing(SKCanvas canvas, float cx, float cy, float radius)
    {
        canvas.DrawCircle(cx, cy, radius, _ringPaint);

        ReadOnlySpan<string> cardinals = ["N", "O", "S", "W"];
        for (var deg = 0; deg < 360; deg += 10)
        {
            var (sin, cos) = SinCos(deg);
            if (deg % 30 == 0)
            {
                canvas.DrawLine(cx + sin * (radius - 12f), cy - cos * (radius - 12f),
                                cx + sin * (radius - 2f), cy - cos * (radius - 2f), _tickPaint);

                var tr = radius - 24f;
                var tx = cx + sin * tr;
                var ty = cy - cos * tr;
                if (deg % 90 == 0)
                {
                    var paint = deg == 0 ? _northPaint : _cardinalPaint;
                    canvas.DrawText(cardinals[deg / 90], tx, ty + 6f, SKTextAlign.Center, _cardinalFont, paint);
                }
                else
                {
                    canvas.DrawText($"{deg}", tx, ty + 4f, SKTextAlign.Center, _degreeFont, _centerDimPaint);
                }
            }
            else
            {
                canvas.DrawLine(cx + sin * (radius - 6f), cy - cos * (radius - 6f),
                                cx + sin * (radius - 2f), cy - cos * (radius - 2f), _tickMinorPaint);
            }
        }
    }

    private void DrawSunMarker(SKCanvas canvas, float cx, float cy, float radius)
    {
        var (sin, cos) = SinCos(SunAzimuth);
        var sx = cx + sin * radius;
        var sy = cy - cos * radius;

        if (IsDaylight)
        {
            // Glow proportional zur Elevation (hoeher = greller)
            var elevNorm = (float)Math.Clamp(SunElevation / 60.0, 0.1, 1.0);
            _sunGlowPaint.Shader?.Dispose();
            _sunGlowPaint.Shader = SKShader.CreateRadialGradient(
                new SKPoint(sx, sy), 22f,
                [SunColor.WithAlpha((byte)(160 * elevNorm)), SKColors.Transparent],
                [0.2f, 1.0f], SKShaderTileMode.Clamp);
            canvas.DrawCircle(sx, sy, 22f, _sunGlowPaint);
            canvas.DrawCircle(sx, sy, 9f, _sunPaint);
        }
        else
        {
            _sunPaint.Color = SunDimColor;
            canvas.DrawCircle(sx, sy, 6f, _sunPaint);
            _sunPaint.Color = SunColor;
        }
    }

    private void DrawTargetMarker(SKCanvas canvas, float cx, float cy, float radius)
    {
        var (sin, cos) = SinCos(TargetAzimuth);
        // Dreieck am Ring, Spitze nach innen
        var bx = cx + sin * (radius + 6f);
        var by = cy - cos * (radius + 6f);
        var tipX = cx + sin * (radius - 14f);
        var tipY = cy - cos * (radius - 14f);
        var perpSin = sin; var perpCos = cos;
        // senkrecht zur Radialrichtung
        var sideX = cos * 8f;
        var sideY = sin * 8f;

        using var path = new SKPath();
        path.MoveTo(tipX, tipY);
        path.LineTo(bx + sideX, by + sideY);
        path.LineTo(bx - sideX, by - sideY);
        path.Close();
        canvas.DrawPath(path, _targetPaint);
        _ = (perpSin, perpCos);
    }

    private void DrawPanelArrow(SKCanvas canvas, float cx, float cy, float length)
    {
        var color = AzimuthReliable ? QualityColor(Quality) : new SKColor(120, 124, 140);
        _panelArrowPaint.Color = color;

        var (sin, cos) = SinCos(PanelAzimuth);
        var tipX = cx + sin * length;
        var tipY = cy - cos * length;
        // Pfeil-Basis breit am Zentrum
        var baseHalfX = cos * 11f;
        var baseHalfY = sin * 11f;

        using var path = new SKPath();
        path.MoveTo(tipX, tipY);
        path.LineTo(cx + baseHalfX, cy + baseHalfY);
        path.LineTo(cx - baseHalfX, cy - baseHalfY);
        path.Close();
        canvas.DrawPath(path, _panelArrowPaint);
    }

    private void DrawTiltArc(SKCanvas canvas, float cx, float cy, float radius)
    {
        // Halbkreis-Track unten (0..90 Grad Neigung), Soll-Tick + Ist-Wert.
        var rect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        // Track: 90 Grad-Bogen rechts unten (von 0 unten bis 90 rechts)
        using var track = new SKPath();
        track.AddArc(rect, 90, -90); // von unten (90) nach rechts (0)
        canvas.DrawPath(track, _tiltTrackPaint);

        // Soll-Markierung
        var targetFrac = (float)Math.Clamp(TargetTilt / 90.0, 0, 1);
        DrawTiltTick(canvas, cx, cy, radius, targetFrac, TargetColor, 10f);

        // Ist-Wert (Bogen von 0 bis PanelTilt), qualitaetsgefaerbt
        var panelFrac = (float)Math.Clamp(PanelTilt / 90.0, 0, 1);
        _tiltValuePaint.Color = (AzimuthReliable ? QualityColor(Quality) : new SKColor(120, 124, 140)).WithAlpha(220);
        using var valueArc = new SKPath();
        valueArc.AddArc(rect, 90, -90 * panelFrac);
        canvas.DrawPath(valueArc, _tiltValuePaint);
    }

    private void DrawTiltTick(SKCanvas canvas, float cx, float cy, float radius, float frac, SKColor color, float len)
    {
        var angleDeg = 90 - 90 * frac; // 0 unten -> 90 rechts
        var rad = angleDeg * Math.PI / 180.0;
        var dx = (float)Math.Cos(rad);
        var dy = (float)Math.Sin(rad);
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3f, Color = color };
        canvas.DrawLine(
            cx + dx * (radius - len), cy + dy * (radius - len),
            cx + dx * (radius + len), cy + dy * (radius + len), paint);
    }

    private void DrawCenterText(SKCanvas canvas, float cx, float cy)
    {
        if (!AzimuthReliable)
        {
            canvas.DrawText("Panel neigen", cx, cy - 4f, SKTextAlign.Center, _centerSmallFont, _centerDimPaint);
            canvas.DrawText("zum Ausrichten", cx, cy + 12f, SKTextAlign.Center, _centerSmallFont, _centerDimPaint);
            return;
        }

        canvas.DrawText($"{PanelAzimuth:0}°", cx, cy + 2f, SKTextAlign.Center, _centerBigFont, _centerTextPaint);
        canvas.DrawText($"Soll {TargetAzimuth:0}°", cx, cy + 22f, SKTextAlign.Center, _centerSmallFont, _centerDimPaint);
    }

    private static (float sin, float cos) SinCos(double azimuthDeg)
    {
        var rad = azimuthDeg * Math.PI / 180.0;
        return ((float)Math.Sin(rad), (float)Math.Cos(rad));
    }

    public void Dispose()
    {
        _glowPaint.Shader?.Dispose();
        _sunGlowPaint.Shader?.Dispose();
        _bgPaint.Dispose();
        _ringPaint.Dispose();
        _tickPaint.Dispose();
        _tickMinorPaint.Dispose();
        _cardinalPaint.Dispose();
        _northPaint.Dispose();
        _sunPaint.Dispose();
        _sunGlowPaint.Dispose();
        _targetPaint.Dispose();
        _targetStrokePaint.Dispose();
        _panelArrowPaint.Dispose();
        _glowPaint.Dispose();
        _tiltTrackPaint.Dispose();
        _tiltTargetPaint.Dispose();
        _tiltValuePaint.Dispose();
        _centerTextPaint.Dispose();
        _centerDimPaint.Dispose();
        _cardinalFont.Dispose();
        _degreeFont.Dispose();
        _centerBigFont.Dispose();
        _centerSmallFont.Dispose();
    }
}
