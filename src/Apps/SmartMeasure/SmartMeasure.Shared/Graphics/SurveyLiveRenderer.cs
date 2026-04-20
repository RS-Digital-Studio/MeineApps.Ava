using SkiaSharp;

namespace SmartMeasure.Shared.Graphics;

/// <summary>
/// Live-Kompass mit Genauigkeits-Ring für die Vermessungs-Ansicht.
/// Äußerer Ring: Kompass (N/E/S/W + 30°-Schritte, Nordpfeil rot).
/// Innerer Ring: Genauigkeits-Ring (Radius proportional zu HorizontalAccuracy).
/// Zentrum: Fadenkreuz + Accuracy-Zahl. Satelliten, Fix-Glow, Neigungsindikator.
///
/// Optimierungen:
/// - Nordpfeil-Pfad einmalig gecacht (vorher pro Frame neu gebaut)
/// - SKFont-API (SkiaSharp 3.x)
/// - Shader-Caching für Fix-Glow (Shader nur bei Parameter-Änderung neu erstellen)
/// </summary>
public sealed class SurveyLiveRenderer : IDisposable
{
    // --- Öffentliche Properties (pro Frame gesetzt) ---

    public float CompassHeading { get; set; }
    public float HorizontalAccuracy { get; set; }
    public float VerticalAccuracy { get; set; }
    public int SatelliteCount { get; set; }
    public int FixQuality { get; set; }
    public float TiltAngle { get; set; }

    // --- App-Palette Farben ---

    private static readonly SKColor PrimaryColor = new(255, 107, 0);
    private static readonly SKColor AccentColor = new(76, 175, 80);
    private static readonly SKColor SecondaryColor = new(33, 150, 243);
    private static readonly SKColor BgColor = new(26, 26, 46);
    private static readonly SKColor NorthColor = new(239, 83, 80);
    private static readonly SKColor TextWhite = new(230, 230, 230);
    private static readonly SKColor TextDimmed = new(136, 153, 170);

    private static readonly SKColor FixGreen = new(76, 175, 80, 60);
    private static readonly SKColor FixYellow = new(255, 235, 59, 50);
    private static readonly SKColor FixRed = new(239, 83, 80, 50);

    private static readonly SKColor AccuracyGreen = new(76, 175, 80, 120);
    private static readonly SKColor AccuracyYellow = new(255, 235, 59, 120);
    private static readonly SKColor AccuracyRed = new(239, 83, 80, 120);

    private static readonly SKColor AccuracyStrokeYellow = new(255, 235, 59);
    private static readonly SKColor AccuracyStrokeRed = new(239, 83, 80);
    private static readonly SKColor TiltStrokeYellow = new(255, 235, 59);

    // --- Gecachte Paints (keine Allokation pro Frame) ---

    private readonly SKPaint _bgPaint = new() { Color = BgColor };

    private readonly SKPaint _compassRingPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f, Color = new SKColor(200, 200, 200, 80)
    };
    private readonly SKPaint _tickPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.5f, Color = new SKColor(200, 200, 200, 160)
    };
    private readonly SKPaint _tickMinorPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke,
        StrokeWidth = 0.8f, Color = new SKColor(200, 200, 200, 60)
    };
    private readonly SKPaint _compassTextPaint = new() { IsAntialias = true, Color = TextWhite };
    private readonly SKPaint _degreeTextPaint = new() { IsAntialias = true, Color = TextDimmed };
    private readonly SKPaint _northTextPaint = new() { IsAntialias = true, Color = NorthColor };

    private readonly SKPaint _northArrowPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill, Color = NorthColor
    };

    private readonly SKPaint _accuracyRingPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _accuracyRingStrokePaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f
    };

    private readonly SKPaint _crosshairPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f, Color = new SKColor(255, 255, 255, 120)
    };
    private readonly SKPaint _centerDotPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill, Color = PrimaryColor
    };

    private readonly SKPaint _accuracyTextPaint = new() { IsAntialias = true, Color = TextWhite };
    private readonly SKPaint _accuracyUnitPaint = new() { IsAntialias = true, Color = TextDimmed };

    private readonly SKPaint _satDotPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill, Color = SecondaryColor
    };
    private readonly SKPaint _satDotDimPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill,
        Color = new SKColor(33, 150, 243, 60)
    };

    private readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private int _lastGlowFixQuality = -1;
    private float _lastGlowCx, _lastGlowCy, _lastGlowRadius;

    private readonly SKPaint _tiltBgPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f, Color = new SKColor(200, 200, 200, 40)
    };
    private readonly SKPaint _tiltDotPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill, Color = PrimaryColor
    };
    private readonly SKPaint _tiltTextPaint = new() { IsAntialias = true, Color = TextDimmed };

    // SKFont-Instanzen (SkiaSharp 3.x)
    private readonly SKFont _compassFont = new(SKTypeface.Default, 14f) { Embolden = true };
    private readonly SKFont _degreeFont = new(SKTypeface.Default, 10f);
    private readonly SKFont _northFont = new(SKTypeface.Default, 16f) { Embolden = true };
    private readonly SKFont _accuracyFont = new(SKTypeface.Default, 16f) { Embolden = true };
    private readonly SKFont _accuracyUnitFont = new(SKTypeface.Default, 10f);
    private readonly SKFont _tiltFont = new(SKTypeface.Default, 9f);

    // Gecachter Nordpfeil-Pfad (lokale Koordinaten, wird per RotateDegrees positioniert)
    private readonly SKPath _northArrowPath;
    private float _northArrowRadius = -1f;

    public SurveyLiveRenderer()
    {
        _northArrowPath = new SKPath();
    }

    public void Render(SKCanvas canvas, SKRect bounds)
    {
        canvas.Clear(_bgPaint.Color);

        var size = Math.Min(bounds.Width, bounds.Height);
        var cx = bounds.MidX;
        var cy = bounds.MidY;
        var outerRadius = size * 0.42f;
        var innerRadius = outerRadius * 0.55f;

        // Nordpfeil-Pfad nur bei Radius-Änderung neu bauen
        if (Math.Abs(_northArrowRadius - outerRadius) > 0.5f)
        {
            RebuildNorthArrow(outerRadius);
            _northArrowRadius = outerRadius;
        }

        DrawFixGlow(canvas, cx, cy, outerRadius);
        DrawCompassRing(canvas, cx, cy, outerRadius);
        DrawSatellites(canvas, cx, cy, outerRadius * 0.78f);
        DrawAccuracyRing(canvas, cx, cy, innerRadius);
        DrawCrosshair(canvas, cx, cy, innerRadius * 0.4f);
        DrawAccuracyText(canvas, cx, cy);
        DrawTiltIndicator(canvas, bounds);
    }

    private void RebuildNorthArrow(float radius)
    {
        _northArrowPath.Rewind();
        var tip = radius + 8f;
        var baseY = radius - 2f;
        var width = 6f;
        // Um (0, 0) herum — DrawCompassRing translated und rotiert den Canvas
        _northArrowPath.MoveTo(0, -tip);
        _northArrowPath.LineTo(-width, -baseY);
        _northArrowPath.LineTo(width, -baseY);
        _northArrowPath.Close();
    }

    private void DrawFixGlow(SKCanvas canvas, float cx, float cy, float radius)
    {
        var glowRadius = radius * 1.1f;

        if (_lastGlowFixQuality != FixQuality ||
            Math.Abs(_lastGlowCx - cx) > 0.5f ||
            Math.Abs(_lastGlowCy - cy) > 0.5f ||
            Math.Abs(_lastGlowRadius - glowRadius) > 0.5f)
        {
            var glowColor = FixQuality >= 4 ? FixGreen
                : FixQuality >= 2 ? FixYellow
                : FixRed;

            _glowPaint.Shader?.Dispose();
            _glowPaint.Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy), glowRadius,
                [glowColor, SKColors.Transparent],
                [0.3f, 1.0f],
                SKShaderTileMode.Clamp);

            _lastGlowFixQuality = FixQuality;
            _lastGlowCx = cx;
            _lastGlowCy = cy;
            _lastGlowRadius = glowRadius;
        }

        canvas.DrawCircle(cx, cy, glowRadius, _glowPaint);
    }

    private void DrawCompassRing(SKCanvas canvas, float cx, float cy, float radius)
    {
        canvas.Save();
        canvas.RotateDegrees(-CompassHeading, cx, cy);

        canvas.DrawCircle(cx, cy, radius, _compassRingPaint);

        ReadOnlySpan<string> cardinals = ["N", "E", "S", "W"];
        for (int deg = 0; deg < 360; deg += 10)
        {
            var rad = deg * MathF.PI / 180f;
            var sin = MathF.Sin(rad);
            var cos = MathF.Cos(rad);

            if (deg % 30 == 0)
            {
                var innerTick = radius - 12f;
                var outerTick = radius - 2f;
                canvas.DrawLine(
                    cx + sin * innerTick, cy - cos * innerTick,
                    cx + sin * outerTick, cy - cos * outerTick,
                    _tickPaint);

                var textRadius = radius - 22f;
                var tx = cx + sin * textRadius;
                var ty = cy - cos * textRadius;

                if (deg % 90 == 0)
                {
                    var cardinal = cardinals[deg / 90];
                    var paint = deg == 0 ? _northTextPaint : _compassTextPaint;
                    var font = deg == 0 ? _northFont : _compassFont;
                    canvas.DrawText(cardinal, tx, ty + 5f, SKTextAlign.Center, font, paint);
                }
                else
                {
                    canvas.DrawText($"{deg}", tx, ty + 4f, SKTextAlign.Center, _degreeFont, _degreeTextPaint);
                }
            }
            else
            {
                var innerTick = radius - 6f;
                var outerTick = radius - 2f;
                canvas.DrawLine(
                    cx + sin * innerTick, cy - cos * innerTick,
                    cx + sin * outerTick, cy - cos * outerTick,
                    _tickMinorPaint);
            }
        }

        // Nordpfeil zeichnen — Canvas ist bereits rotiert, Path ist um (0, 0) herum
        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.DrawPath(_northArrowPath, _northArrowPaint);
        canvas.Restore();

        canvas.Restore();
    }

    private void DrawSatellites(SKCanvas canvas, float cx, float cy, float radius)
    {
        const int maxSlots = 24;
        var activeCount = Math.Min(SatelliteCount, maxSlots);

        for (int i = 0; i < maxSlots; i++)
        {
            var angle = i * 360f / maxSlots * MathF.PI / 180f;
            var sx = cx + MathF.Sin(angle) * radius;
            var sy = cy - MathF.Cos(angle) * radius;
            var dotRadius = 3f;

            if (i < activeCount)
                canvas.DrawCircle(sx, sy, dotRadius, _satDotPaint);
            else
                canvas.DrawCircle(sx, sy, dotRadius * 0.7f, _satDotDimPaint);
        }
    }

    private void DrawAccuracyRing(SKCanvas canvas, float cx, float cy, float maxRadius)
    {
        var accuracyNorm = Math.Clamp(HorizontalAccuracy / 30f, 0.05f, 1f);
        var ringRadius = maxRadius * accuracyNorm;

        SKColor fillColor, strokeColor;
        if (HorizontalAccuracy < 3f)
        {
            fillColor = AccuracyGreen;
            strokeColor = AccentColor;
        }
        else if (HorizontalAccuracy < 10f)
        {
            fillColor = AccuracyYellow;
            strokeColor = AccuracyStrokeYellow;
        }
        else
        {
            fillColor = AccuracyRed;
            strokeColor = AccuracyStrokeRed;
        }

        _accuracyRingPaint.Color = fillColor;
        _accuracyRingStrokePaint.Color = strokeColor;

        canvas.DrawCircle(cx, cy, ringRadius, _accuracyRingPaint);
        canvas.DrawCircle(cx, cy, ringRadius, _accuracyRingStrokePaint);
    }

    private void DrawCrosshair(SKCanvas canvas, float cx, float cy, float length)
    {
        canvas.DrawLine(cx - length, cy, cx - 4f, cy, _crosshairPaint);
        canvas.DrawLine(cx + 4f, cy, cx + length, cy, _crosshairPaint);
        canvas.DrawLine(cx, cy - length, cx, cy - 4f, _crosshairPaint);
        canvas.DrawLine(cx, cy + 4f, cx, cy + length, _crosshairPaint);
        canvas.DrawCircle(cx, cy, 3f, _centerDotPaint);
    }

    private void DrawAccuracyText(SKCanvas canvas, float cx, float cy)
    {
        var accText = HorizontalAccuracy < 100f
            ? $"\u00b1{HorizontalAccuracy:F1}cm"
            : $"\u00b1{HorizontalAccuracy / 100f:F2}m";

        canvas.DrawText(accText, cx, cy + 28f, SKTextAlign.Center, _accuracyFont, _accuracyTextPaint);

        var vertText = $"V: \u00b1{VerticalAccuracy:F1}cm";
        canvas.DrawText(vertText, cx, cy + 42f, SKTextAlign.Center, _accuracyUnitFont, _accuracyUnitPaint);

        var fixLabel = FixQuality switch
        {
            4 => "RTK Fix",
            5 => "RTK Float",
            2 => "DGPS",
            1 => "GPS",
            _ => "No Fix"
        };
        canvas.DrawText($"{SatelliteCount} Sat  {fixLabel}", cx, cy + 56f,
            SKTextAlign.Center, _accuracyUnitFont, _accuracyUnitPaint);
    }

    private void DrawTiltIndicator(SKCanvas canvas, SKRect bounds)
    {
        var indicatorRadius = 24f;
        var cx = bounds.Right - 40f;
        var cy = bounds.Bottom - 40f;

        canvas.DrawCircle(cx, cy, indicatorRadius, _tiltBgPaint);

        var maxOffset = indicatorRadius - 4f;
        var tiltNorm = Math.Clamp(TiltAngle / 15f, 0f, 1f);
        var offset = maxOffset * tiltNorm;

        _tiltDotPaint.Color = TiltAngle < 2f ? AccentColor
            : TiltAngle < 5f ? TiltStrokeYellow
            : NorthColor;

        canvas.DrawCircle(cx, cy + offset, 4f, _tiltDotPaint);

        canvas.DrawText($"{TiltAngle:F1}\u00b0", cx, cy - indicatorRadius - 4f,
            SKTextAlign.Center, _tiltFont, _tiltTextPaint);
    }

    public void Dispose()
    {
        _glowPaint.Shader?.Dispose();
        _bgPaint.Dispose();
        _compassRingPaint.Dispose();
        _tickPaint.Dispose();
        _tickMinorPaint.Dispose();
        _compassTextPaint.Dispose();
        _degreeTextPaint.Dispose();
        _northTextPaint.Dispose();
        _northArrowPaint.Dispose();
        _accuracyRingPaint.Dispose();
        _accuracyRingStrokePaint.Dispose();
        _crosshairPaint.Dispose();
        _centerDotPaint.Dispose();
        _accuracyTextPaint.Dispose();
        _accuracyUnitPaint.Dispose();
        _satDotPaint.Dispose();
        _satDotDimPaint.Dispose();
        _glowPaint.Dispose();
        _tiltBgPaint.Dispose();
        _tiltDotPaint.Dispose();
        _tiltTextPaint.Dispose();
        _compassFont.Dispose();
        _degreeFont.Dispose();
        _northFont.Dispose();
        _accuracyFont.Dispose();
        _accuracyUnitFont.Dispose();
        _tiltFont.Dispose();
        _northArrowPath.Dispose();
    }
}
