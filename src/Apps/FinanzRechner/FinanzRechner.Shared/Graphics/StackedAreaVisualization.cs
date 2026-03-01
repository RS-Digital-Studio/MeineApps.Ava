using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FinanzRechner.Graphics;

/// <summary>
/// SkiaSharp-Renderer für gestapelte Flächendiagramme (Stacked Area Chart).
/// Ersetzt LiveCharts StackedAreaSeries in CompoundInterest-, SavingsPlan- und InflationView.
/// </summary>
public static class StackedAreaVisualization
{
    private static readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeWidth = 2f };
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _gridPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _labelFont = new() { Size = 10f };
    private static readonly SKFont _axisFont = new() { Size = 8f };

    /// <summary>
    /// Statische Felder vorinitialisieren (SKPaint, SKFont).
    /// Wird im SplashOverlay-Preloader aufgerufen um Jank beim ersten Render zu vermeiden.
    /// </summary>
    public static void WarmUp()
    {
        // Statische readonly-Felder werden durch diesen Methodenaufruf
        // vom CLR-Klassen-Initializer angelegt
    }

    /// <summary>
    /// Rendert ein gestapeltes Flächendiagramm mit 2 Flächen.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="xLabels">X-Achsen-Labels (z.B. Jahreszahlen)</param>
    /// <param name="area1">Untere Fläche (Basis)</param>
    /// <param name="area2">Obere Fläche (gestapelt auf area1)</param>
    /// <param name="color1">Farbe der unteren Fläche</param>
    /// <param name="color2">Farbe der oberen Fläche</param>
    /// <param name="legend1">Legendentext Fläche 1</param>
    /// <param name="legend2">Legendentext Fläche 2</param>
    // Hinweis: Nur vom UI-Thread aufrufen (statische Paints nicht thread-safe)
    public static void Render(SKCanvas canvas, SKRect bounds,
        string[] xLabels, float[] area1, float[] area2,
        SKColor color1, SKColor color2,
        string legend1, string legend2)
    {
        int count = xLabels.Length;
        if (count < 2 || area1.Length != count || area2.Length != count) return;

        float padding = 8f;
        float leftMargin = 40f;
        float bottomMargin = 22f;
        float topMargin = 10f;

        float chartLeft = bounds.Left + leftMargin;
        float chartRight = bounds.Right - padding;
        float chartTop = bounds.Top + topMargin;
        float chartBottom = bounds.Bottom - bottomMargin;
        float chartW = chartRight - chartLeft;
        float chartH = chartBottom - chartTop;

        if (chartW <= 20 || chartH <= 20) return;

        // Gestapelte Werte berechnen (area2 liegt auf area1)
        float maxVal = 100f;
        var stacked = new float[count];
        for (int i = 0; i < count; i++)
        {
            stacked[i] = area1[i] + area2[i];
            maxVal = Math.Max(maxVal, stacked[i]);
        }
        maxVal *= 1.1f;

        float xStep = chartW / (count - 1);

        // Grid-Linien + Y-Achse
        _gridPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 20);
        _textPaint.Color = SkiaThemeHelper.TextMuted;

        float gridStep = ChartHelper.CalculateGridStep(maxVal);
        for (float v = 0; v <= maxVal; v += gridStep)
        {
            float y = chartBottom - (v / maxVal) * chartH;
            canvas.DrawLine(chartLeft, y, chartRight, y, _gridPaint);
            string label = ChartHelper.FormatYLabel(v);
            canvas.DrawText(label, chartLeft - 4f, y + 3f, SKTextAlign.Right, _axisFont, _textPaint);
        }

        // X-Labels (nicht alle anzeigen wenn zu viele)
        int labelStep = count > 20 ? 5 : count > 10 ? 2 : 1;
        for (int i = 0; i < count; i += labelStep)
        {
            float x = chartLeft + xStep * i;
            _textPaint.Color = SkiaThemeHelper.TextMuted;
            canvas.DrawText(xLabels[i], x, chartBottom + 14f,
                SKTextAlign.Center, _labelFont, _textPaint);
        }
        // Immer letzten Label anzeigen
        if ((count - 1) % labelStep != 0)
        {
            float x = chartLeft + xStep * (count - 1);
            _textPaint.Color = SkiaThemeHelper.TextMuted;
            canvas.DrawText(xLabels[count - 1], x, chartBottom + 14f,
                SKTextAlign.Center, _labelFont, _textPaint);
        }

        // Punkte berechnen: Basis (area1) und Stacked (area1 + area2)
        var basePoints = new SKPoint[count];
        var stackedPoints = new SKPoint[count];
        for (int i = 0; i < count; i++)
        {
            float x = chartLeft + xStep * i;
            float yBase = chartBottom - (area1[i] / maxVal) * chartH;
            float yStacked = chartBottom - (stacked[i] / maxVal) * chartH;
            basePoints[i] = new SKPoint(x, yBase);
            stackedPoints[i] = new SKPoint(x, yStacked);
        }

        // Obere Fläche zeichnen (area2: zwischen basePoints und stackedPoints)
        DrawStackedArea(canvas, chartBottom, stackedPoints, basePoints, color2);

        // Untere Fläche zeichnen (area1: zwischen Baseline und basePoints)
        DrawBaseArea(canvas, chartBottom, basePoints, color1);

        // Linien zeichnen
        using var basePath = CreatePath(basePoints);
        using var stackedPath = CreatePath(stackedPoints);

        _linePaint.Color = color1;
        _linePaint.StrokeWidth = 2f;
        canvas.DrawPath(basePath, _linePaint);

        _linePaint.Color = color2;
        canvas.DrawPath(stackedPath, _linePaint);
    }

    /// <summary>
    /// Zeichnet die untere (Basis-) Fläche mit Gradient.
    /// </summary>
    private static void DrawBaseArea(SKCanvas canvas, float chartBottom,
        SKPoint[] points, SKColor color)
    {
        if (points.Length < 2) return;

        using var path = new SKPath();
        path.MoveTo(points[0].X, chartBottom);
        for (int i = 0; i < points.Length; i++)
            path.LineTo(points[i]);
        path.LineTo(points[^1].X, chartBottom);
        path.Close();

        float minY = points.Min(p => p.Y);
        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, minY),
            new SKPoint(0, chartBottom),
            new[] { SkiaThemeHelper.WithAlpha(color, 136), SkiaThemeHelper.WithAlpha(color, 20) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawPath(path, _fillPaint);
        _fillPaint.Shader = null;
    }

    /// <summary>
    /// Zeichnet die obere (gestapelte) Fläche zwischen zwei Punkt-Arrays.
    /// </summary>
    private static void DrawStackedArea(SKCanvas canvas, float chartBottom,
        SKPoint[] topPoints, SKPoint[] bottomPoints, SKColor color)
    {
        if (topPoints.Length < 2 || bottomPoints.Length < 2) return;

        using var path = new SKPath();
        // Obere Linie vorwärts
        path.MoveTo(topPoints[0]);
        for (int i = 1; i < topPoints.Length; i++)
            path.LineTo(topPoints[i]);
        // Untere Linie rückwärts
        for (int i = bottomPoints.Length - 1; i >= 0; i--)
            path.LineTo(bottomPoints[i]);
        path.Close();

        float minY = topPoints.Min(p => p.Y);
        float maxY = bottomPoints.Max(p => p.Y);
        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, minY),
            new SKPoint(0, maxY),
            new[] { SkiaThemeHelper.WithAlpha(color, 136), SkiaThemeHelper.WithAlpha(color, 20) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawPath(path, _fillPaint);
        _fillPaint.Shader = null;
    }

    /// <summary>
    /// Erstellt einen einfachen Linien-Pfad (keine Splines - Finanzdaten sollten direkt verbunden sein).
    /// </summary>
    private static SKPath CreatePath(SKPoint[] points)
    {
        var path = new SKPath();
        if (points.Length < 2) return path;

        path.MoveTo(points[0]);
        for (int i = 1; i < points.Length; i++)
            path.LineTo(points[i]);

        return path;
    }

}
