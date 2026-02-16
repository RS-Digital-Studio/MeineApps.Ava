using SkiaSharp;

namespace MeineApps.UI.SkiaSharp;

/// <summary>
/// Statische Helper-Klasse für technische Zeichnungen (Blaupausen-Stil).
/// Bietet Maßlinien, Raster, Winkel-Arcs, Schraffuren und Auto-Skalierung.
/// Wird von allen HandwerkerRechner-Visualisierungen genutzt.
/// </summary>
public static class SkiaBlueprintCanvas
{
    // Gecachte Paints - werden NIE pro Frame erstellt
    private static readonly SKPaint _gridPaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 0.5f
    };

    private static readonly SKPaint _dimLinePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f
    };

    private static readonly SKPaint _dimArrowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint _textPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint _dashedPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f
    };

    private static readonly SKPaint _crosshatchPaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 0.8f
    };

    private static readonly SKPaint _anglePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.5f
    };

    private static readonly SKFont _defaultFont = new()
    {
        Size = 11f
    };

    /// <summary>
    /// Ergebnis der Auto-Skalierung: Scale-Faktor und Offset zum Zentrieren.
    /// </summary>
    public readonly record struct FitResult(float Scale, float OffsetX, float OffsetY);

    /// <summary>
    /// Berechnet Scale + Offset damit contentW×contentH zentriert in bounds passt.
    /// Padding wird ringsum abgezogen (Standard 30px für Maßlinien).
    /// </summary>
    public static FitResult FitToCanvas(SKRect bounds, float contentWidth, float contentHeight, float padding = 30f)
    {
        if (contentWidth <= 0 || contentHeight <= 0)
            return new FitResult(1f, bounds.MidX, bounds.MidY);

        float availW = bounds.Width - padding * 2;
        float availH = bounds.Height - padding * 2;

        if (availW <= 0 || availH <= 0)
            return new FitResult(1f, bounds.MidX, bounds.MidY);

        float scale = Math.Min(availW / contentWidth, availH / contentHeight);
        float offsetX = bounds.Left + padding + (availW - contentWidth * scale) / 2f;
        float offsetY = bounds.Top + padding + (availH - contentHeight * scale) / 2f;

        return new FitResult(scale, offsetX, offsetY);
    }

    /// <summary>
    /// Zeichnet ein Hintergrund-Raster (Blueprint-Stil).
    /// </summary>
    public static void DrawGrid(SKCanvas canvas, SKRect bounds, float spacing = 20f, SKColor? color = null)
    {
        var gridColor = color ?? SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 40);
        _gridPaint.Color = gridColor;

        // Vertikale Linien
        for (float x = bounds.Left; x <= bounds.Right; x += spacing)
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, _gridPaint);

        // Horizontale Linien
        for (float y = bounds.Top; y <= bounds.Bottom; y += spacing)
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, _gridPaint);
    }

    /// <summary>
    /// Zeichnet eine Maßlinie mit Pfeilen an beiden Enden und Text in der Mitte.
    /// </summary>
    public static void DrawDimensionLine(SKCanvas canvas, SKPoint start, SKPoint end,
        string text, SKColor? color = null, float fontSize = 10f, float offset = 0f)
    {
        var dimColor = color ?? SkiaThemeHelper.TextSecondary;
        _dimLinePaint.Color = dimColor;
        _dimArrowPaint.Color = dimColor;

        // Offset senkrecht zur Linie
        float dx = end.X - start.X;
        float dy = end.Y - start.Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        if (length < 1f) return;

        float nx = -dy / length * offset;
        float ny = dx / length * offset;

        var p1 = new SKPoint(start.X + nx, start.Y + ny);
        var p2 = new SKPoint(end.X + nx, end.Y + ny);

        // Hauptlinie
        canvas.DrawLine(p1, p2, _dimLinePaint);

        // Pfeile (kleine Dreiecke an den Enden)
        float arrowSize = 4f;
        float dirX = dx / length;
        float dirY = dy / length;
        float perpX = -dirY;
        float perpY = dirX;

        // Pfeil am Start
        using var arrowPath = new SKPath();
        arrowPath.MoveTo(p1);
        arrowPath.LineTo(p1.X + dirX * arrowSize + perpX * arrowSize * 0.5f,
                          p1.Y + dirY * arrowSize + perpY * arrowSize * 0.5f);
        arrowPath.LineTo(p1.X + dirX * arrowSize - perpX * arrowSize * 0.5f,
                          p1.Y + dirY * arrowSize - perpY * arrowSize * 0.5f);
        arrowPath.Close();
        canvas.DrawPath(arrowPath, _dimArrowPaint);

        // Pfeil am Ende
        using var arrowPath2 = new SKPath();
        arrowPath2.MoveTo(p2);
        arrowPath2.LineTo(p2.X - dirX * arrowSize + perpX * arrowSize * 0.5f,
                           p2.Y - dirY * arrowSize + perpY * arrowSize * 0.5f);
        arrowPath2.LineTo(p2.X - dirX * arrowSize - perpX * arrowSize * 0.5f,
                           p2.Y - dirY * arrowSize - perpY * arrowSize * 0.5f);
        arrowPath2.Close();
        canvas.DrawPath(arrowPath2, _dimArrowPaint);

        // Verlängerungslinien (senkrecht an Start und Ende)
        if (MathF.Abs(offset) > 1f)
        {
            float extLen = MathF.Abs(offset) + 4f;
            float extDir = offset > 0 ? 1f : -1f;
            canvas.DrawLine(start.X, start.Y,
                           start.X + (-dy / length) * extLen * extDir,
                           start.Y + (dx / length) * extLen * extDir, _dimLinePaint);
            canvas.DrawLine(end.X, end.Y,
                           end.X + (-dy / length) * extLen * extDir,
                           end.Y + (dx / length) * extLen * extDir, _dimLinePaint);
        }

        // Text in der Mitte
        DrawMeasurementText(canvas, text,
            new SKPoint((p1.X + p2.X) / 2f, (p1.Y + p2.Y) / 2f - 4f),
            dimColor, fontSize);
    }

    /// <summary>
    /// Zeichnet einen Winkel-Bogen mit Beschriftung.
    /// </summary>
    public static void DrawAngleArc(SKCanvas canvas, SKPoint center, float radius,
        float startAngleDeg, float sweepAngleDeg, string label, SKColor? color = null)
    {
        var arcColor = color ?? SkiaThemeHelper.Accent;
        _anglePaint.Color = arcColor;

        // Bogen zeichnen
        var rect = new SKRect(center.X - radius, center.Y - radius,
                              center.X + radius, center.Y + radius);

        using var path = new SKPath();
        path.AddArc(rect, startAngleDeg, sweepAngleDeg);
        canvas.DrawPath(path, _anglePaint);

        // Label in der Mitte des Bogens
        float midAngleRad = (startAngleDeg + sweepAngleDeg / 2f) * MathF.PI / 180f;
        float labelRadius = radius + 12f;
        var labelPos = new SKPoint(
            center.X + MathF.Cos(midAngleRad) * labelRadius,
            center.Y + MathF.Sin(midAngleRad) * labelRadius);

        DrawMeasurementText(canvas, label, labelPos, arcColor, 10f);
    }

    /// <summary>
    /// Zeichnet eine gestrichelte Linie.
    /// </summary>
    public static void DrawDashedLine(SKCanvas canvas, SKPoint start, SKPoint end,
        SKColor? color = null, float dashLength = 6f, float gapLength = 4f)
    {
        var dashColor = color ?? SkiaThemeHelper.TextMuted;
        _dashedPaint.Color = dashColor;
        _dashedPaint.PathEffect = SKPathEffect.CreateDash(new[] { dashLength, gapLength }, 0);

        canvas.DrawLine(start, end, _dashedPaint);

        _dashedPaint.PathEffect = null; // Zurücksetzen für nächsten Aufruf
    }

    /// <summary>
    /// Zeichnet Text an einer Position (zentriert).
    /// </summary>
    public static void DrawMeasurementText(SKCanvas canvas, string text, SKPoint pos,
        SKColor? color = null, float fontSize = 11f, SKTextAlign align = SKTextAlign.Center)
    {
        var textColor = color ?? SkiaThemeHelper.TextPrimary;
        _textPaint.Color = textColor;
        _defaultFont.Size = fontSize;

        // Hintergrund für bessere Lesbarkeit
        float textWidth;
        using (var textBlob = SKTextBlob.Create(text, _defaultFont))
        {
            if (textBlob == null) return;
            textWidth = textBlob.Bounds.Width;
        }

        float bgPadding = 3f;
        var bgColor = SkiaThemeHelper.IsDarkTheme
            ? SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Background, 200)
            : SkiaThemeHelper.WithAlpha(SKColors.White, 200);

        _textPaint.Color = bgColor;
        float bgX = align switch
        {
            SKTextAlign.Left => pos.X - bgPadding,
            SKTextAlign.Right => pos.X - textWidth - bgPadding,
            _ => pos.X - textWidth / 2f - bgPadding
        };
        canvas.DrawRect(bgX, pos.Y - fontSize + 1f, textWidth + bgPadding * 2, fontSize + bgPadding, _textPaint);

        // Text zeichnen
        _textPaint.Color = textColor;
        float textX = align switch
        {
            SKTextAlign.Left => pos.X,
            SKTextAlign.Right => pos.X - textWidth,
            _ => pos.X - textWidth / 2f
        };
        canvas.DrawText(text, textX, pos.Y, _defaultFont, _textPaint);
    }

    /// <summary>
    /// Zeichnet eine Kreuzschraffur (für Verschnitt/Abfall-Bereiche).
    /// </summary>
    public static void DrawCrosshatch(SKCanvas canvas, SKRect area, SKColor? color = null, float spacing = 8f)
    {
        var hatchColor = color ?? SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Error, 120);
        _crosshatchPaint.Color = hatchColor;

        canvas.Save();
        canvas.ClipRect(area);

        // Diagonale Linien (45°)
        float maxDim = Math.Max(area.Width, area.Height) * 2;
        for (float d = -maxDim; d < maxDim; d += spacing)
        {
            canvas.DrawLine(area.Left + d, area.Top, area.Left + d + maxDim, area.Top + maxDim, _crosshatchPaint);
        }

        canvas.Restore();
    }

    /// <summary>
    /// Zeichnet eine Schraffur in eine Richtung (für Füllmuster).
    /// </summary>
    public static void DrawHatch(SKCanvas canvas, SKRect area, float angleDeg, SKColor? color = null, float spacing = 6f)
    {
        var hatchColor = color ?? SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 80);
        _crosshatchPaint.Color = hatchColor;

        canvas.Save();
        canvas.ClipRect(area);
        canvas.Translate(area.MidX, area.MidY);
        canvas.RotateDegrees(angleDeg);

        float maxDim = MathF.Sqrt(area.Width * area.Width + area.Height * area.Height);
        for (float y = -maxDim; y < maxDim; y += spacing)
        {
            canvas.DrawLine(-maxDim, y, maxDim, y, _crosshatchPaint);
        }

        canvas.Restore();
    }
}
