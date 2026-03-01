using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace RechnerPlus.Graphics;

/// <summary>
/// Mini-Funktionsgraph für den Taschenrechner.
/// Zeigt eine mathematische Funktion als smooth SKPath mit Glow-Effekt.
/// Erscheint bei Funktions-Eingaben (sin, cos, tan, sqrt, log, x²).
/// Aktueller Eingabewert wird als leuchtender Punkt auf der Kurve markiert.
/// </summary>
public static class FunctionGraphVisualization
{
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _gridPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _axisPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _curvePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _labelFont = new() { Size = 9f };
    private static readonly SKFont _valueFont = new() { Size = 10f };

    // Statische Filter/Effekte (einmal erstellt, nicht pro Frame)
    private static readonly SKMaskFilter _curveGlowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);
    private static readonly SKMaskFilter _dotGlowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f);
    private static readonly SKPathEffect _dashEffect = SKPathEffect.CreateDash(new[] { 3f, 3f }, 0);

    // Wiederverwendbare Arrays (nur bei Groessenänderung neu alloziert)
    private static float[] _xValues = Array.Empty<float>();
    private static float[] _yValues = Array.Empty<float>();

    // Vorgefertigte Funktions-Bereiche
    private static readonly Dictionary<string, (float minX, float maxX)> _functionRanges = new()
    {
        ["sin"] = (-MathF.PI * 2, MathF.PI * 2),
        ["cos"] = (-MathF.PI * 2, MathF.PI * 2),
        ["tan"] = (-MathF.PI * 1.5f, MathF.PI * 1.5f),
        ["sqrt"] = (0f, 10f),
        ["log"] = (0.01f, 100f),
        ["ln"] = (0.01f, 10f),
        ["x²"] = (-5f, 5f),
        ["x³"] = (-3f, 3f),
        ["1/x"] = (-5f, 5f),
        ["abs"] = (-5f, 5f),
    };

    /// <summary>
    /// Gibt den Standard-X-Bereich für eine Funktion zurück.
    /// </summary>
    public static (float minX, float maxX) GetRange(string functionName)
    {
        return _functionRanges.TryGetValue(functionName, out var range) ? range : (-10f, 10f);
    }

    /// <summary>
    /// Rendert den Funktionsgraph.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="function">Mathematische Funktion f(x)</param>
    /// <param name="functionName">Name der Funktion (für Range + Label)</param>
    /// <param name="currentX">Aktueller X-Wert (leuchtender Punkt)</param>
    /// <param name="animTime">Animation für Glow-Pulsierung</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        Func<float, float> function, string functionName = "",
        float? currentX = null, float animTime = 0f)
    {
        float padding = 24f;
        float plotLeft = bounds.Left + padding + 16f;  // Platz für Y-Labels
        float plotRight = bounds.Right - padding;
        float plotTop = bounds.Top + padding;
        float plotBottom = bounds.Bottom - padding - 12f; // Platz für X-Labels
        float plotW = plotRight - plotLeft;
        float plotH = plotBottom - plotTop;

        if (plotW < 20 || plotH < 20) return;

        // Hintergrund (leicht transparent)
        _bgPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Surface, 180);
        canvas.DrawRoundRect(bounds, 8f, 8f, _bgPaint);

        // Rand
        _gridPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 40);
        _gridPaint.StrokeWidth = 1f;
        canvas.DrawRoundRect(bounds, 8f, 8f, _gridPaint);

        // Bereich ermitteln
        var (minX, maxX) = GetRange(functionName);

        // Y-Bereich durch Abtastung ermitteln
        int sampleCount = (int)plotW;
        sampleCount = Math.Clamp(sampleCount, 50, 300);

        // Statische Arrays wiederverwenden, nur bei Größenänderung neu allozieren
        if (_xValues.Length != sampleCount)
        {
            _xValues = new float[sampleCount];
            _yValues = new float[sampleCount];
        }
        var xValues = _xValues;
        var yValues = _yValues;

        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)(sampleCount - 1);
            float x = minX + t * (maxX - minX);
            xValues[i] = x;

            float y;
            try
            {
                y = function(x);
            }
            catch
            {
                y = float.NaN;
            }

            // Asymptoten begrenzen (z.B. tan, 1/x)
            if (float.IsNaN(y) || float.IsInfinity(y) || MathF.Abs(y) > 1000f)
            {
                yValues[i] = float.NaN;
                continue;
            }

            yValues[i] = y;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        // Sicherheitscheck
        if (minY >= maxY)
        {
            minY = -1f;
            maxY = 1f;
        }

        // Y-Bereich leicht erweitern für Padding
        float yPadding = (maxY - minY) * 0.1f;
        minY -= yPadding;
        maxY += yPadding;

        // xStep und yStep einmal berechnen – DrawGrid und DrawLabels teilen dieselben Werte
        float xStep = CalculateStep(maxX - minX);
        float yStep = CalculateStep(maxY - minY);

        // Clip auf Plot-Bereich
        canvas.Save();
        canvas.ClipRect(new SKRect(plotLeft - 1, plotTop - 1, plotRight + 1, plotBottom + 1));

        // Grid-Linien zeichnen (vorberechnete Steps übergeben)
        DrawGrid(canvas, plotLeft, plotTop, plotW, plotH, minX, maxX, minY, maxY, xStep, yStep);

        // Achsen zeichnen
        DrawAxes(canvas, plotLeft, plotTop, plotW, plotH, minX, maxX, minY, maxY);

        // Funktionskurve zeichnen
        DrawCurve(canvas, plotLeft, plotTop, plotW, plotH, xValues, yValues, minY, maxY, animTime);

        canvas.Restore();

        // Achsen-Labels (außerhalb des Clips, vorberechnete Steps übergeben)
        DrawLabels(canvas, plotLeft, plotTop, plotW, plotH, plotBottom, minX, maxX, minY, maxY, functionName, xStep, yStep);

        // Aktuellen Punkt markieren
        if (currentX.HasValue)
        {
            DrawCurrentPoint(canvas, plotLeft, plotTop, plotW, plotH,
                minX, maxX, minY, maxY, function, currentX.Value, animTime);
        }
    }

    /// <summary>
    /// Zeichnet dezente Grid-Linien. xStep/yStep werden vom Aufrufer übergeben (einmalige Berechnung).
    /// </summary>
    private static void DrawGrid(SKCanvas canvas, float left, float top,
        float w, float h, float minX, float maxX, float minY, float maxY,
        float xStep, float yStep)
    {
        _gridPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 20);
        _gridPaint.StrokeWidth = 0.5f;

        // Vertikale Grid-Linien (X-Achse)
        float xStart = MathF.Ceiling(minX / xStep) * xStep;
        for (float gx = xStart; gx <= maxX; gx += xStep)
        {
            float px = left + (gx - minX) / (maxX - minX) * w;
            canvas.DrawLine(px, top, px, top + h, _gridPaint);
        }

        // Horizontale Grid-Linien (Y-Achse)
        float yStart = MathF.Ceiling(minY / yStep) * yStep;
        for (float gy = yStart; gy <= maxY; gy += yStep)
        {
            float py = top + h - (gy - minY) / (maxY - minY) * h;
            canvas.DrawLine(left, py, left + w, py, _gridPaint);
        }
    }

    /// <summary>
    /// Zeichnet die X- und Y-Achse (falls im sichtbaren Bereich).
    /// </summary>
    private static void DrawAxes(SKCanvas canvas, float left, float top,
        float w, float h, float minX, float maxX, float minY, float maxY)
    {
        _axisPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 80);

        // X-Achse (y=0)
        if (minY <= 0 && maxY >= 0)
        {
            float axisY = top + h - (0 - minY) / (maxY - minY) * h;
            canvas.DrawLine(left, axisY, left + w, axisY, _axisPaint);
        }

        // Y-Achse (x=0)
        if (minX <= 0 && maxX >= 0)
        {
            float axisX = left + (0 - minX) / (maxX - minX) * w;
            canvas.DrawLine(axisX, top, axisX, top + h, _axisPaint);
        }
    }

    /// <summary>
    /// Zeichnet die Funktionskurve mit Glow und Gradient-Füllung.
    /// </summary>
    private static void DrawCurve(SKCanvas canvas, float left, float top,
        float w, float h, float[] xValues, float[] yValues,
        float minY, float maxY, float animTime)
    {
        var curveColor = SkiaThemeHelper.Primary;

        // Gradient-Füllung unter der Kurve erstellen
        using var fillPath = new SKPath();
        using var curvePath = new SKPath();

        bool inPath = false;
        bool fillStarted = false;
        float lastPx = 0, lastPy = 0;

        // Y=0 Linie für Füllung
        float zeroY = top + h - (0 - minY) / (maxY - minY) * h;
        zeroY = Math.Clamp(zeroY, top, top + h);

        for (int i = 0; i < xValues.Length; i++)
        {
            if (float.IsNaN(yValues[i]))
            {
                // Lücke (Asymptote) - Pfad unterbrechen
                if (fillStarted)
                {
                    fillPath.LineTo(lastPx, zeroY);
                    fillPath.Close();
                    fillStarted = false;
                }
                inPath = false;
                continue;
            }

            float t = i / (float)(xValues.Length - 1);
            float px = left + t * w;
            float py = top + h - (yValues[i] - minY) / (maxY - minY) * h;

            if (!inPath)
            {
                curvePath.MoveTo(px, py);
                fillPath.MoveTo(px, zeroY);
                fillPath.LineTo(px, py);
                inPath = true;
                fillStarted = true;
            }
            else
            {
                curvePath.LineTo(px, py);
                fillPath.LineTo(px, py);
            }

            lastPx = px;
            lastPy = py;
        }

        if (fillStarted)
        {
            fillPath.LineTo(lastPx, zeroY);
            fillPath.Close();
        }

        // Gradient-Füllung (transparenter Verlauf unter der Kurve)
        // Alten Shader disposen bevor neuer zugewiesen wird (Native Memory Leak)
        _fillPaint.Shader?.Dispose();
        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(left, top),
            new SKPoint(left, top + h),
            new[] { curveColor.WithAlpha(25), curveColor.WithAlpha(5) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawPath(fillPath, _fillPaint);
        _fillPaint.Shader?.Dispose();
        _fillPaint.Shader = null;

        // Glow unter der Kurve (breiterer, transparenter Strich)
        float pulse = 0.7f + 0.3f * MathF.Sin(animTime * 2f);
        _glowPaint.Color = curveColor.WithAlpha((byte)(pulse * 30));
        _glowPaint.StrokeWidth = 6f;
        _glowPaint.MaskFilter = _curveGlowFilter; // Statisches Filter-Objekt, kein Leak
        canvas.DrawPath(curvePath, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Kurve selbst
        _curvePaint.Color = curveColor;
        canvas.DrawPath(curvePath, _curvePaint);
    }

    /// <summary>
    /// Zeichnet Achsen-Beschriftungen. xStep/yStep werden vom Aufrufer übergeben (einmalige Berechnung).
    /// </summary>
    private static void DrawLabels(SKCanvas canvas, float left, float top,
        float w, float h, float plotBottom, float minX, float maxX, float minY, float maxY,
        string functionName, float xStep, float yStep)
    {
        _textPaint.Color = SkiaThemeHelper.TextMuted;
        _labelFont.Size = 8f;

        // X-Achsen-Labels (unten)
        float xStart = MathF.Ceiling(minX / xStep) * xStep;
        for (float gx = xStart; gx <= maxX; gx += xStep)
        {
            float px = left + (gx - minX) / (maxX - minX) * w;
            string label = FormatAxisValue(gx);
            canvas.DrawText(label, px, plotBottom + 10f, SKTextAlign.Center, _labelFont, _textPaint);
        }

        // Y-Achsen-Labels (links)
        float yStart = MathF.Ceiling(minY / yStep) * yStep;
        for (float gy = yStart; gy <= maxY; gy += yStep)
        {
            float py = top + h - (gy - minY) / (maxY - minY) * h;
            string label = FormatAxisValue(gy);
            canvas.DrawText(label, left - 4f, py + 3f, SKTextAlign.Right, _labelFont, _textPaint);
        }

        // Funktionsname (oben links)
        if (!string.IsNullOrEmpty(functionName))
        {
            _textPaint.Color = SkiaThemeHelper.Primary;
            _valueFont.Size = 11f;
            string displayName = functionName switch
            {
                "x²" => "f(x) = x²",
                "x³" => "f(x) = x³",
                "sqrt" => "f(x) = √x",
                "1/x" => "f(x) = 1/x",
                "abs" => "f(x) = |x|",
                _ => $"f(x) = {functionName}(x)"
            };
            canvas.DrawText(displayName, left + 4f, top - 6f, SKTextAlign.Left, _valueFont, _textPaint);
        }
    }

    /// <summary>
    /// Zeichnet den aktuellen Punkt auf der Kurve (leuchtender Dot + Wertanzeige).
    /// </summary>
    private static void DrawCurrentPoint(SKCanvas canvas, float left, float top,
        float w, float h, float minX, float maxX, float minY, float maxY,
        Func<float, float> function, float currentX, float animTime)
    {
        float currentY;
        try
        {
            currentY = function(currentX);
        }
        catch
        {
            return;
        }

        if (float.IsNaN(currentY) || float.IsInfinity(currentY) || MathF.Abs(currentY) > 1000f)
            return;

        // Auf Plot-Koordinaten umrechnen
        float px = left + (currentX - minX) / (maxX - minX) * w;
        float py = top + h - (currentY - minY) / (maxY - minY) * h;

        // Prüfen ob im sichtbaren Bereich
        if (px < left || px > left + w || py < top || py > top + h) return;

        // Pulsierender Glow
        float pulse = 0.6f + 0.4f * MathF.Sin(animTime * 4f);
        _dotPaint.Color = SkiaThemeHelper.Accent.WithAlpha((byte)(pulse * 50));
        _dotPaint.MaskFilter = _dotGlowFilter; // Statisches Filter-Objekt, kein Leak
        canvas.DrawCircle(px, py, 8f, _dotPaint);
        _dotPaint.MaskFilter = null;

        // Äußerer Ring
        _dotPaint.Color = SkiaThemeHelper.Accent.WithAlpha(180);
        canvas.DrawCircle(px, py, 5f, _dotPaint);

        // Innerer Punkt (weiß)
        _dotPaint.Color = SKColors.White;
        canvas.DrawCircle(px, py, 3f, _dotPaint);

        // Gestrichelte vertikale Linie zum X-Achsen-Bereich
        float zeroY = top + h - (0 - minY) / (maxY - minY) * h;
        zeroY = Math.Clamp(zeroY, top, top + h);
        _gridPaint.Color = SkiaThemeHelper.Accent.WithAlpha(40);
        _gridPaint.StrokeWidth = 1f;
        _gridPaint.PathEffect = _dashEffect; // Statisches Effekt-Objekt, kein Leak
        canvas.DrawLine(px, py, px, zeroY, _gridPaint);
        _gridPaint.PathEffect = null;

        // Wertanzeige (Tooltip über dem Punkt)
        string valueText = $"({FormatValue(currentX)}, {FormatValue(currentY)})";
        _valueFont.Size = 9f;
        float textWidth = _valueFont.MeasureText(valueText, out _);

        // Tooltip-Position (über dem Punkt, oder darunter wenn zu nah am oberen Rand)
        float tooltipX = px;
        float tooltipY = py - 16f;
        if (tooltipY - 10f < top) tooltipY = py + 18f;

        // Tooltip-Hintergrund
        var tooltipRect = new SKRect(
            tooltipX - textWidth / 2f - 4f, tooltipY - 10f,
            tooltipX + textWidth / 2f + 4f, tooltipY + 4f);
        _bgPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Surface, 220);
        canvas.DrawRoundRect(tooltipRect, 4f, 4f, _bgPaint);

        // Tooltip-Rand
        _gridPaint.Color = SkiaThemeHelper.Accent.WithAlpha(60);
        _gridPaint.StrokeWidth = 0.5f;
        canvas.DrawRoundRect(tooltipRect, 4f, 4f, _gridPaint);

        // Tooltip-Text
        _textPaint.Color = SkiaThemeHelper.Accent;
        canvas.DrawText(valueText, tooltipX, tooltipY, SKTextAlign.Center, _valueFont, _textPaint);
    }

    /// <summary>
    /// Berechnet eine sinnvolle Schrittweite für Grid-Linien.
    /// </summary>
    private static float CalculateStep(float range)
    {
        if (range <= 0) return 1f;

        float rawStep = range / 5f; // ~5 Grid-Linien
        float magnitude = MathF.Pow(10f, MathF.Floor(MathF.Log10(rawStep)));

        float normalized = rawStep / magnitude;
        float step;
        if (normalized < 1.5f) step = magnitude;
        else if (normalized < 3.5f) step = 2f * magnitude;
        else if (normalized < 7.5f) step = 5f * magnitude;
        else step = 10f * magnitude;

        return step;
    }

    /// <summary>
    /// Formatiert einen Achsen-Wert kompakt.
    /// </summary>
    private static string FormatAxisValue(float value)
    {
        if (MathF.Abs(value) < 0.001f) return "0";
        if (MathF.Abs(value) >= 100f) return $"{value:F0}";
        if (MathF.Abs(value) >= 10f) return $"{value:F0}";
        if (MathF.Abs(value) >= 1f) return $"{value:F1}";
        return $"{value:F2}";
    }

    /// <summary>
    /// Formatiert einen Wert für die Tooltip-Anzeige.
    /// </summary>
    private static string FormatValue(float value)
    {
        if (MathF.Abs(value) < 0.01f) return $"{value:F3}";
        if (MathF.Abs(value) < 1f) return $"{value:F2}";
        if (MathF.Abs(value) < 100f) return $"{value:F1}";
        return $"{value:F0}";
    }
}
