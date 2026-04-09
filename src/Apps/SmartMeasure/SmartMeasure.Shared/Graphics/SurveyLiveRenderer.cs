using SkiaSharp;

namespace SmartMeasure.Shared.Graphics;

/// <summary>Live-Kompass mit Genauigkeits-Ring fuer die Vermessungs-Ansicht.
/// Aeusserer Ring: Kompass (N/E/S/W + 30-Grad-Schritte, Nordpfeil rot).
/// Innerer Ring: Genauigkeits-Ring (Radius proportional zu HorizontalAccuracy).
/// Zentrum: Fadenkreuz + Accuracy-Zahl. Satelliten, Fix-Glow, Neigungsindikator.</summary>
public class SurveyLiveRenderer : IDisposable
{
    // --- Oeffentliche Properties (pro Frame gesetzt) ---

    /// <summary>Kompass-Heading in Grad (0=Nord, 90=Ost)</summary>
    public float CompassHeading { get; set; }

    /// <summary>Horizontale Genauigkeit in cm</summary>
    public float HorizontalAccuracy { get; set; }

    /// <summary>Vertikale Genauigkeit in cm</summary>
    public float VerticalAccuracy { get; set; }

    /// <summary>Anzahl sichtbare Satelliten</summary>
    public int SatelliteCount { get; set; }

    /// <summary>Fix-Quality (0=NoFix, 1=GPS, 2=DGPS, 4=RTK-Fix, 5=RTK-Float)</summary>
    public int FixQuality { get; set; }

    /// <summary>Stab-Neigung in Grad vom Lot</summary>
    public float TiltAngle { get; set; }

    // --- App-Palette Farben ---

    private static readonly SKColor PrimaryColor = new(255, 107, 0);       // #FF6B00 Orange
    private static readonly SKColor AccentColor = new(76, 175, 80);        // #4CAF50 Gruen
    private static readonly SKColor SecondaryColor = new(33, 150, 243);    // #2196F3 Blau
    private static readonly SKColor BgColor = new(26, 26, 46);             // #1A1A2E
    private static readonly SKColor NorthColor = new(239, 83, 80);         // Rot
    private static readonly SKColor TextWhite = new(230, 230, 230);
    private static readonly SKColor TextDimmed = new(136, 153, 170);

    // Fix-Status Farben
    private static readonly SKColor FixGreen = new(76, 175, 80, 60);      // RTK Fix
    private static readonly SKColor FixYellow = new(255, 235, 59, 50);    // Float
    private static readonly SKColor FixRed = new(239, 83, 80, 50);        // NoFix

    // Accuracy-Ring Farben
    private static readonly SKColor AccuracyGreen = new(76, 175, 80, 120);
    private static readonly SKColor AccuracyYellow = new(255, 235, 59, 120);
    private static readonly SKColor AccuracyRed = new(239, 83, 80, 120);

    // --- Gecachte Paints (keine Allokation pro Frame) ---

    private readonly SKPaint _bgPaint = new() { Color = BgColor };

    // Kompass-Ring
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
    private readonly SKPaint _compassTextPaint = new()
    {
        IsAntialias = true, Color = TextWhite, TextSize = 14f,
        TextAlign = SKTextAlign.Center, FakeBoldText = true
    };
    private readonly SKPaint _degreeTextPaint = new()
    {
        IsAntialias = true, Color = TextDimmed, TextSize = 10f,
        TextAlign = SKTextAlign.Center
    };
    private readonly SKPaint _northTextPaint = new()
    {
        IsAntialias = true, Color = NorthColor, TextSize = 16f,
        TextAlign = SKTextAlign.Center, FakeBoldText = true
    };

    // Nordpfeil
    private readonly SKPaint _northArrowPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill, Color = NorthColor
    };

    // Genauigkeits-Ring
    private readonly SKPaint _accuracyRingPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill
    };
    private readonly SKPaint _accuracyRingStrokePaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f
    };

    // Fadenkreuz
    private readonly SKPaint _crosshairPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f, Color = new SKColor(255, 255, 255, 120)
    };
    private readonly SKPaint _centerDotPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill, Color = PrimaryColor
    };

    // Accuracy-Text
    private readonly SKPaint _accuracyTextPaint = new()
    {
        IsAntialias = true, Color = TextWhite, TextSize = 16f,
        TextAlign = SKTextAlign.Center, FakeBoldText = true
    };
    private readonly SKPaint _accuracyUnitPaint = new()
    {
        IsAntialias = true, Color = TextDimmed, TextSize = 10f,
        TextAlign = SKTextAlign.Center
    };

    // Satelliten-Punkte
    private readonly SKPaint _satDotPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill, Color = SecondaryColor
    };
    private readonly SKPaint _satDotDimPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill,
        Color = new SKColor(33, 150, 243, 60)
    };

    // Fix-Glow (Shader wird pro Frame mit aktuellen Parametern erstellt)
    private readonly SKPaint _glowPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill
    };
    private int _lastGlowFixQuality = -1;
    private float _lastGlowCx, _lastGlowCy, _lastGlowRadius;

    // Neigungsindikator
    private readonly SKPaint _tiltBgPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f, Color = new SKColor(200, 200, 200, 40)
    };
    private readonly SKPaint _tiltDotPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill, Color = PrimaryColor
    };
    private readonly SKPaint _tiltTextPaint = new()
    {
        IsAntialias = true, Color = TextDimmed, TextSize = 9f,
        TextAlign = SKTextAlign.Center
    };

    public void Render(SKCanvas canvas, SKRect bounds)
    {
        canvas.Clear(_bgPaint.Color);

        // Quadratischen Bereich im Zentrum bestimmen
        var size = Math.Min(bounds.Width, bounds.Height);
        var cx = bounds.MidX;
        var cy = bounds.MidY;
        var outerRadius = size * 0.42f;
        var innerRadius = outerRadius * 0.55f;

        // 1. Fix-Status Hintergrund-Glow
        DrawFixGlow(canvas, cx, cy, outerRadius);

        // 2. Aeusserer Kompass-Ring (rotiert mit Heading)
        DrawCompassRing(canvas, cx, cy, outerRadius);

        // 3. Satelliten-Punkte (zwischen aeusserem und innerem Ring)
        DrawSatellites(canvas, cx, cy, outerRadius * 0.78f);

        // 4. Genauigkeits-Ring (innerer Kreis)
        DrawAccuracyRing(canvas, cx, cy, innerRadius);

        // 5. Fadenkreuz im Zentrum
        DrawCrosshair(canvas, cx, cy, innerRadius * 0.4f);

        // 6. Accuracy-Text im Zentrum
        DrawAccuracyText(canvas, cx, cy);

        // 7. Neigungsindikator (unten rechts)
        DrawTiltIndicator(canvas, bounds);
    }

    private void DrawFixGlow(SKCanvas canvas, float cx, float cy, float radius)
    {
        var glowRadius = radius * 1.1f;

        // Shader nur bei geaenderten Parametern neu erstellen
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

        // Aeusserer Kreis
        canvas.DrawCircle(cx, cy, radius, _compassRingPaint);

        // Tick-Marks und Beschriftungen alle 30 Grad
        string[] cardinals = ["N", "E", "S", "W"];
        for (int deg = 0; deg < 360; deg += 10)
        {
            var rad = deg * MathF.PI / 180f;
            var sin = MathF.Sin(rad);
            var cos = MathF.Cos(rad);

            if (deg % 30 == 0)
            {
                // Grosse Tick-Marks
                var innerTick = radius - 12f;
                var outerTick = radius - 2f;
                canvas.DrawLine(
                    cx + sin * innerTick, cy - cos * innerTick,
                    cx + sin * outerTick, cy - cos * outerTick,
                    _tickPaint);

                // Kardinalrichtungen oder Gradzahl
                var textRadius = radius - 22f;
                var tx = cx + sin * textRadius;
                var ty = cy - cos * textRadius;

                if (deg % 90 == 0)
                {
                    var cardinal = cardinals[deg / 90];
                    var paint = deg == 0 ? _northTextPaint : _compassTextPaint;
                    canvas.DrawText(cardinal, tx, ty + 5f, paint);
                }
                else
                {
                    canvas.DrawText($"{deg}", tx, ty + 4f, _degreeTextPaint);
                }
            }
            else
            {
                // Kleine Tick-Marks (alle 10 Grad)
                var innerTick = radius - 6f;
                var outerTick = radius - 2f;
                canvas.DrawLine(
                    cx + sin * innerTick, cy - cos * innerTick,
                    cx + sin * outerTick, cy - cos * outerTick,
                    _tickMinorPaint);
            }
        }

        // Nordpfeil (dreieckig, am aeusseren Rand)
        using var northPath = new SKPath();
        var arrowTip = radius + 8f;
        var arrowBase = radius - 2f;
        var arrowWidth = 6f;
        northPath.MoveTo(cx, cy - arrowTip);
        northPath.LineTo(cx - arrowWidth, cy - arrowBase);
        northPath.LineTo(cx + arrowWidth, cy - arrowBase);
        northPath.Close();
        canvas.DrawPath(northPath, _northArrowPaint);

        canvas.Restore();
    }

    private void DrawSatellites(SKCanvas canvas, float cx, float cy, float radius)
    {
        // Maximal 24 Satelliten-Slots darstellen
        const int maxSlots = 24;
        var activeCount = Math.Min(SatelliteCount, maxSlots);

        for (int i = 0; i < maxSlots; i++)
        {
            var angle = i * 360f / maxSlots * MathF.PI / 180f;
            var sx = cx + MathF.Sin(angle) * radius;
            var sy = cy - MathF.Cos(angle) * radius;
            var dotRadius = 3f;

            if (i < activeCount)
            {
                canvas.DrawCircle(sx, sy, dotRadius, _satDotPaint);
            }
            else
            {
                canvas.DrawCircle(sx, sy, dotRadius * 0.7f, _satDotDimPaint);
            }
        }
    }

    private void DrawAccuracyRing(SKCanvas canvas, float cx, float cy, float maxRadius)
    {
        // Radius proportional zur Accuracy (kleiner = besser)
        // 0cm → Punkt, 3cm → 30% Radius, 10cm → 60%, >30cm → 100%
        var accuracyNorm = Math.Clamp(HorizontalAccuracy / 30f, 0.05f, 1f);
        var ringRadius = maxRadius * accuracyNorm;

        // Farbe nach Genauigkeit
        SKColor fillColor, strokeColor;
        if (HorizontalAccuracy < 3f)
        {
            fillColor = AccuracyGreen;
            strokeColor = AccentColor;
        }
        else if (HorizontalAccuracy < 10f)
        {
            fillColor = AccuracyYellow;
            strokeColor = new SKColor(255, 235, 59);
        }
        else
        {
            fillColor = AccuracyRed;
            strokeColor = new SKColor(239, 83, 80);
        }

        _accuracyRingPaint.Color = fillColor;
        _accuracyRingStrokePaint.Color = strokeColor;

        canvas.DrawCircle(cx, cy, ringRadius, _accuracyRingPaint);
        canvas.DrawCircle(cx, cy, ringRadius, _accuracyRingStrokePaint);
    }

    private void DrawCrosshair(SKCanvas canvas, float cx, float cy, float length)
    {
        // Horizontale Linie
        canvas.DrawLine(cx - length, cy, cx - 4f, cy, _crosshairPaint);
        canvas.DrawLine(cx + 4f, cy, cx + length, cy, _crosshairPaint);

        // Vertikale Linie
        canvas.DrawLine(cx, cy - length, cx, cy - 4f, _crosshairPaint);
        canvas.DrawLine(cx, cy + 4f, cx, cy + length, _crosshairPaint);

        // Zentrums-Punkt
        canvas.DrawCircle(cx, cy, 3f, _centerDotPaint);
    }

    private void DrawAccuracyText(SKCanvas canvas, float cx, float cy)
    {
        // Accuracy-Wert
        var accText = HorizontalAccuracy < 100f
            ? $"\u00b1{HorizontalAccuracy:F1}cm"
            : $"\u00b1{HorizontalAccuracy / 100f:F2}m";

        canvas.DrawText(accText, cx, cy + 28f, _accuracyTextPaint);

        // Vertikal darunter
        var vertText = $"V: \u00b1{VerticalAccuracy:F1}cm";
        canvas.DrawText(vertText, cx, cy + 42f, _accuracyUnitPaint);

        // Satelliten-Count + Fix-Status
        var fixLabel = FixQuality switch
        {
            4 => "RTK Fix",
            5 => "RTK Float",
            2 => "DGPS",
            1 => "GPS",
            _ => "No Fix"
        };
        canvas.DrawText($"{SatelliteCount} Sat  {fixLabel}", cx, cy + 56f, _accuracyUnitPaint);
    }

    private void DrawTiltIndicator(SKCanvas canvas, SKRect bounds)
    {
        // Kleiner Kreis unten rechts: zeigt Neigung des Stabs
        var indicatorRadius = 24f;
        var cx = bounds.Right - 40f;
        var cy = bounds.Bottom - 40f;

        // Hintergrund-Kreis
        canvas.DrawCircle(cx, cy, indicatorRadius, _tiltBgPaint);

        // Innerer Punkt (verschoben nach Neigung, max bis Rand)
        var maxOffset = indicatorRadius - 4f;
        var tiltNorm = Math.Clamp(TiltAngle / 15f, 0f, 1f); // 15 Grad = Rand
        var offset = maxOffset * tiltNorm;

        // Farbe: gruen bei <2, gelb <5, rot >5
        _tiltDotPaint.Color = TiltAngle < 2f ? AccentColor
            : TiltAngle < 5f ? new SKColor(255, 235, 59)
            : NorthColor;

        // Punkt nach unten verschieben (vereinfacht, zeigt Betrag)
        canvas.DrawCircle(cx, cy + offset, 4f, _tiltDotPaint);

        // Beschriftung
        canvas.DrawText($"{TiltAngle:F1}\u00b0", cx, cy - indicatorRadius - 4f, _tiltTextPaint);
    }

    /// <summary>Alle gecachten Paint-Objekte freigeben</summary>
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
    }
}
