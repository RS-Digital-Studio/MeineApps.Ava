using SkiaSharp;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.Graphics;

/// <summary>
/// 2D-Draufsicht: Gartenelemente (Wege, Beete, Mauern, Terrassen) + Messpunkte.
///
/// Optimierungen:
/// - Min/Max in einem Pass statt 4x LINQ
/// - Preview-Path + Screen-Point-Array gecacht (während aktiver Zeichnung)
/// - SKFont-API (SkiaSharp 3.x)
/// </summary>
public sealed class GardenPlanRenderer : IDisposable
{
    public float Zoom { get; set; } = 1.0f;
    public float PanX { get; set; }
    public float PanY { get; set; }
    public bool ShowHeightMap { get; set; } = true;
    public bool ShowGrid { get; set; } = true;

    public double LastScale { get; private set; }
    public double LastCenterX { get; private set; }
    public double LastCenterY { get; private set; }

    private static readonly Dictionary<GardenElementType, SKColor> ElementColors = new()
    {
        [GardenElementType.Weg] = new SKColor(120, 144, 156),
        [GardenElementType.Beet] = new SKColor(109, 76, 65),
        [GardenElementType.Rasen] = new SKColor(102, 187, 106),
        [GardenElementType.Mauer] = new SKColor(239, 83, 80),
        [GardenElementType.Zaun] = new SKColor(239, 83, 80, 150),
        [GardenElementType.Terrasse] = new SKColor(215, 204, 200),
    };

    // Gecachte Paints
    private readonly SKPaint _bgPaint = new() { Color = new SKColor(26, 26, 46) };
    private readonly SKPaint _gridPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f, Color = new SKColor(255, 255, 255, 20) };
    private readonly SKPaint _pointPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(255, 107, 0) };
    private readonly SKPaint _pointStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = new SKColor(255, 255, 255, 180) };
    private readonly SKPaint _labelPaint = new() { IsAntialias = true, Color = new SKColor(255, 107, 0) };
    private readonly SKPaint _measurePaint = new() { IsAntialias = true, Color = new SKColor(200, 200, 200) };
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private readonly SKPaint _emptyTextPaint = new() { Color = new SKColor(136, 153, 170), IsAntialias = true };

    private readonly SKPaint _drawPreviewStrokePaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f,
        Color = new SKColor(255, 235, 59),
        PathEffect = SKPathEffect.CreateDash([8f, 6f], 0)
    };
    private readonly SKPaint _drawPreviewFillPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill,
        Color = new SKColor(255, 235, 59, 60)
    };
    private readonly SKPaint _drawPreviewPointPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Fill,
        Color = new SKColor(255, 235, 59)
    };
    private readonly SKPaint _drawPreviewPointStrokePaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f,
        Color = new SKColor(255, 255, 255, 200)
    };

    // SKFont-Instanzen
    private readonly SKFont _labelFont = new(SKTypeface.Default, 10f);
    private readonly SKFont _measureFont = new(SKTypeface.Default, 9f);
    private readonly SKFont _emptyFont = new(SKTypeface.Default, 16f);

    // Wiederverwendbare Pfade/Arrays für Zeichnung und Vorschau
    private readonly SKPath _elementPath = new();
    private readonly SKPath _previewPath = new();
    private SKPoint[] _previewPoints = Array.Empty<SKPoint>();

    private readonly IGardenPlanService _gardenPlanService;

    public GardenPlanRenderer(IGardenPlanService gardenPlanService)
    {
        _gardenPlanService = gardenPlanService;
    }

    public void Render(SKCanvas canvas, SKRect bounds,
        double[] x, double[] y, double[] z, string[]? labels,
        List<GardenElement>? elements, TerrainMesh? mesh,
        IReadOnlyList<(double x, double y)>? drawingPreviewPoints = null,
        GardenElementType drawingPreviewType = GardenElementType.Weg)
    {
        canvas.Clear(_bgPaint.Color);

        if (x.Length == 0)
        {
            canvas.DrawText("Keine Messpunkte vorhanden",
                bounds.MidX, bounds.MidY,
                SKTextAlign.Center, _emptyFont, _emptyTextPaint);
            LastScale = 1.0;
            return;
        }

        canvas.Save();
        canvas.Translate(bounds.MidX + PanX, bounds.MidY + PanY);

        // Min/Max in einem Pass berechnen (spart 4x O(n) LINQ-Aufrufe)
        double minX = x[0], maxX = x[0], minY = y[0], maxY = y[0];
        for (int i = 1; i < x.Length; i++)
        {
            if (x[i] < minX) minX = x[i];
            if (x[i] > maxX) maxX = x[i];
            if (y[i] < minY) minY = y[i];
            if (y[i] > maxY) maxY = y[i];
        }

        var rangeX = maxX - minX;
        var rangeY = maxY - minY;
        var range = Math.Max(rangeX, rangeY);
        if (range < 0.001) range = 1;
        var scale = Math.Min(bounds.Width, bounds.Height) * 0.4f * Zoom / range;
        LastScale = scale;

        var centerX = (minX + maxX) / 2.0;
        var centerY = (minY + maxY) / 2.0;
        LastCenterX = centerX;
        LastCenterY = centerY;

        if (ShowGrid)
            DrawGrid(canvas, scale, centerX, centerY, minX, maxX, minY, maxY, range);

        if (elements != null)
        {
            foreach (var element in elements)
            {
                // LocalPoints wird vom ViewModel auf die aktuelle Referenz gemappt.
                // Fallback auf ParsePoints (v1-Legacy) wenn der Cache noch nicht gefüllt ist.
                var pts = element.LocalPoints ?? _gardenPlanService.ParsePoints(element.PointsJson);
                if (pts.Count < 2) continue;

                var color = ElementColors.GetValueOrDefault(element.ElementType, new SKColor(128, 128, 128));

                switch (element.ElementType)
                {
                    case GardenElementType.Weg:
                        DrawPolyline(canvas, pts, centerX, centerY, scale, color, element.Width * (float)scale);
                        break;
                    case GardenElementType.Beet:
                    case GardenElementType.Rasen:
                    case GardenElementType.Terrasse:
                        DrawFilledPolygon(canvas, pts, centerX, centerY, scale, color);
                        break;
                    case GardenElementType.Mauer:
                    case GardenElementType.Zaun:
                        DrawPolyline(canvas, pts, centerX, centerY, scale, color, 3f);
                        break;
                }
            }
        }

        // Messpunkte zeichnen
        for (int i = 0; i < x.Length; i++)
        {
            var sx = (float)((x[i] - centerX) * scale);
            var sy = (float)((y[i] - centerY) * scale * -1);

            canvas.DrawCircle(sx, sy, 5f, _pointPaint);
            canvas.DrawCircle(sx, sy, 5f, _pointStrokePaint);

            if (labels != null && i < labels.Length && !string.IsNullOrEmpty(labels[i]))
                canvas.DrawText(labels[i], sx + 8, sy - 4, SKTextAlign.Left, _labelFont, _labelPaint);

            if (i > 0)
            {
                var prevSx = (float)((x[i - 1] - centerX) * scale);
                var prevSy = (float)((y[i - 1] - centerY) * scale * -1);
                var dist = Math.Sqrt((x[i] - x[i - 1]) * (x[i] - x[i - 1]) +
                                     (y[i] - y[i - 1]) * (y[i] - y[i - 1]));
                var midX = (sx + prevSx) / 2;
                var midY = (sy + prevSy) / 2;
                canvas.DrawText($"{dist:F2}m", midX, midY - 4,
                    SKTextAlign.Center, _measureFont, _measurePaint);
            }
        }

        if (drawingPreviewPoints != null && drawingPreviewPoints.Count > 0)
            DrawPreview(canvas, drawingPreviewPoints, centerX, centerY, scale, drawingPreviewType);

        canvas.Restore();
    }

    private void DrawGrid(SKCanvas canvas, double scale, double centerX, double centerY,
        double minX, double maxX, double minY, double maxY, double range)
    {
        var gridStep = range > 100 ? 10.0 : range > 50 ? 5.0 : 1.0;

        var gridMinX = Math.Floor(minX / gridStep) * gridStep;
        var gridMaxX = Math.Ceiling(maxX / gridStep) * gridStep;
        for (var gx = gridMinX; gx <= gridMaxX; gx += gridStep)
        {
            var sx = (float)((gx - centerX) * scale);
            canvas.DrawLine(sx, (float)((minY - centerY) * scale * -1),
                            sx, (float)((maxY - centerY) * scale * -1), _gridPaint);
        }

        var gridMinY = Math.Floor(minY / gridStep) * gridStep;
        var gridMaxY = Math.Ceiling(maxY / gridStep) * gridStep;
        for (var gy = gridMinY; gy <= gridMaxY; gy += gridStep)
        {
            var sy = (float)((gy - centerY) * scale * -1);
            canvas.DrawLine((float)((minX - centerX) * scale), sy,
                            (float)((maxX - centerX) * scale), sy, _gridPaint);
        }
    }

    private void DrawPreview(SKCanvas canvas, IReadOnlyList<(double x, double y)> points,
        double centerX, double centerY, double scale, GardenElementType type)
    {
        if (_previewPoints.Length < points.Count)
            _previewPoints = new SKPoint[points.Count];

        for (int i = 0; i < points.Count; i++)
        {
            _previewPoints[i] = new SKPoint(
                (float)((points[i].x - centerX) * scale),
                (float)((points[i].y - centerY) * scale * -1));
        }

        var isPolygon = type is GardenElementType.Beet or GardenElementType.Rasen or GardenElementType.Terrasse;

        if (points.Count >= 2)
        {
            _previewPath.Rewind();
            _previewPath.MoveTo(_previewPoints[0]);
            for (int i = 1; i < points.Count; i++)
                _previewPath.LineTo(_previewPoints[i]);

            if (isPolygon && points.Count >= 3)
            {
                _previewPath.Close();
                canvas.DrawPath(_previewPath, _drawPreviewFillPaint);
            }

            canvas.DrawPath(_previewPath, _drawPreviewStrokePaint);
        }

        for (int i = 0; i < points.Count; i++)
        {
            var radius = i == points.Count - 1 ? 6f : 4f;
            canvas.DrawCircle(_previewPoints[i], radius, _drawPreviewPointPaint);
            canvas.DrawCircle(_previewPoints[i], radius, _drawPreviewPointStrokePaint);
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            var dx = points[i + 1].x - points[i].x;
            var dy = points[i + 1].y - points[i].y;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            var midX = (_previewPoints[i].X + _previewPoints[i + 1].X) / 2;
            var midY = (_previewPoints[i].Y + _previewPoints[i + 1].Y) / 2;
            canvas.DrawText($"{dist:F2}m", midX, midY - 6,
                SKTextAlign.Center, _measureFont, _measurePaint);
        }
    }

    private void DrawFilledPolygon(SKCanvas canvas, List<(double x, double y)> points,
        double centerX, double centerY, double scale, SKColor color)
    {
        _elementPath.Rewind();
        for (int i = 0; i < points.Count; i++)
        {
            var sx = (float)((points[i].x - centerX) * scale);
            var sy = (float)((points[i].y - centerY) * scale * -1);
            if (i == 0) _elementPath.MoveTo(sx, sy);
            else _elementPath.LineTo(sx, sy);
        }
        _elementPath.Close();

        _fillPaint.Color = color.WithAlpha(100);
        canvas.DrawPath(_elementPath, _fillPaint);

        _strokePaint.Color = color;
        _strokePaint.StrokeWidth = 2f;
        canvas.DrawPath(_elementPath, _strokePaint);
    }

    private void DrawPolyline(SKCanvas canvas, List<(double x, double y)> points,
        double centerX, double centerY, double scale, SKColor color, float width)
    {
        _strokePaint.Color = color;
        _strokePaint.StrokeWidth = Math.Max(2f, width);

        for (int i = 0; i < points.Count - 1; i++)
        {
            var sx1 = (float)((points[i].x - centerX) * scale);
            var sy1 = (float)((points[i].y - centerY) * scale * -1);
            var sx2 = (float)((points[i + 1].x - centerX) * scale);
            var sy2 = (float)((points[i + 1].y - centerY) * scale * -1);
            canvas.DrawLine(sx1, sy1, sx2, sy2, _strokePaint);
        }

        _strokePaint.StrokeWidth = 2f;
    }

    public void Dispose()
    {
        _bgPaint.Dispose();
        _gridPaint.Dispose();
        _pointPaint.Dispose();
        _pointStrokePaint.Dispose();
        _labelPaint.Dispose();
        _measurePaint.Dispose();
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _emptyTextPaint.Dispose();
        _drawPreviewStrokePaint.PathEffect?.Dispose();
        _drawPreviewStrokePaint.Dispose();
        _drawPreviewFillPaint.Dispose();
        _drawPreviewPointPaint.Dispose();
        _drawPreviewPointStrokePaint.Dispose();
        _labelFont.Dispose();
        _measureFont.Dispose();
        _emptyFont.Dispose();
        _elementPath.Dispose();
        _previewPath.Dispose();
    }
}
