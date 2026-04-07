using SkiaSharp;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.Graphics;

/// <summary>2D-Draufsicht: Hoehenkarte + Gartenelemente (Wege, Beete, Mauern, Terrassen)</summary>
public class GardenPlanRenderer
{
    public float Zoom { get; set; } = 1.0f;
    public float PanX { get; set; }
    public float PanY { get; set; }
    public bool ShowHeightMap { get; set; } = true;
    public bool ShowGrid { get; set; } = true;

    /// <summary>Letzte berechnete Skalierung (Pixel pro Meter) - fuer inverse Transformation in OnCanvasTapped</summary>
    public double LastScale { get; private set; }

    // Farben fuer Element-Typen
    private static readonly Dictionary<GardenElementType, SKColor> ElementColors = new()
    {
        [GardenElementType.Weg] = new SKColor(120, 144, 156),       // Blaugrau
        [GardenElementType.Beet] = new SKColor(109, 76, 65),        // Braun
        [GardenElementType.Rasen] = new SKColor(102, 187, 106),     // Gruen
        [GardenElementType.Mauer] = new SKColor(239, 83, 80),       // Rot
        [GardenElementType.Zaun] = new SKColor(239, 83, 80, 150),   // Rot halbtransparent
        [GardenElementType.Terrasse] = new SKColor(215, 204, 200)   // Hellbraun
    };

    // Gecachte Paints
    private readonly SKPaint _bgPaint = new() { Color = new SKColor(26, 26, 46) };
    private readonly SKPaint _gridPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f, Color = new SKColor(255, 255, 255, 20) };
    private readonly SKPaint _pointPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(255, 107, 0) };
    private readonly SKPaint _pointStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = new SKColor(255, 255, 255, 180) };
    private readonly SKPaint _labelPaint = new() { IsAntialias = true, Color = new SKColor(255, 107, 0), TextSize = 10f };
    private readonly SKPaint _measurePaint = new() { IsAntialias = true, Color = new SKColor(200, 200, 200), TextSize = 9f, TextAlign = SKTextAlign.Center };
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private readonly SKPaint _drawPreviewStrokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2.5f,
        Color = new SKColor(255, 235, 59), // Gelb
        PathEffect = SKPathEffect.CreateDash([8f, 6f], 0)
    };
    private readonly SKPaint _drawPreviewFillPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        Color = new SKColor(255, 235, 59, 60) // Gelb halbtransparent
    };
    private readonly SKPaint _drawPreviewPointPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        Color = new SKColor(255, 235, 59) // Gelb
    };
    private readonly SKPaint _drawPreviewPointStrokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.5f,
        Color = new SKColor(255, 255, 255, 200)
    };

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
            using var emptyPaint = new SKPaint { Color = new SKColor(136, 153, 170), TextSize = 16f, TextAlign = SKTextAlign.Center, IsAntialias = true };
            canvas.DrawText("Keine Messpunkte vorhanden", bounds.MidX, bounds.MidY, emptyPaint);
            LastScale = 1.0;
            return;
        }

        canvas.Save();

        // Transformationen: Pan + Zoom
        canvas.Translate(bounds.MidX + PanX, bounds.MidY + PanY);

        // Skalierung berechnen
        var rangeX = x.Max() - x.Min();
        var rangeY = y.Max() - y.Min();
        var range = Math.Max(rangeX, rangeY);
        if (range < 0.001) range = 1;
        var scale = Math.Min(bounds.Width, bounds.Height) * 0.4f * Zoom / range;
        LastScale = scale;

        var centerX = (x.Min() + x.Max()) / 2.0;
        var centerY = (y.Min() + y.Max()) / 2.0;

        // Grid zeichnen (1m Raster)
        if (ShowGrid)
        {
            var gridStep = 1.0; // 1m
            if (range > 50) gridStep = 5.0;
            if (range > 100) gridStep = 10.0;

            var gridMin = Math.Floor(x.Min() / gridStep) * gridStep;
            var gridMax = Math.Ceiling(x.Max() / gridStep) * gridStep;
            for (var gx = gridMin; gx <= gridMax; gx += gridStep)
            {
                var sx = (float)((gx - centerX) * scale);
                canvas.DrawLine(sx, (float)((y.Min() - centerY) * scale * -1), sx, (float)((y.Max() - centerY) * scale * -1), _gridPaint);
            }
            gridMin = Math.Floor(y.Min() / gridStep) * gridStep;
            gridMax = Math.Ceiling(y.Max() / gridStep) * gridStep;
            for (var gy = gridMin; gy <= gridMax; gy += gridStep)
            {
                var sy = (float)((gy - centerY) * scale * -1); // Y invertieren
                canvas.DrawLine((float)((x.Min() - centerX) * scale), sy, (float)((x.Max() - centerX) * scale), sy, _gridPaint);
            }
        }

        // Gartenelemente zeichnen
        if (elements != null)
        {
            foreach (var element in elements)
            {
                var pts = _gardenPlanService.ParsePoints(element.PointsJson);
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
            var sy = (float)((y[i] - centerY) * scale * -1); // Y invertieren (Nord=oben)

            canvas.DrawCircle(sx, sy, 5f, _pointPaint);
            canvas.DrawCircle(sx, sy, 5f, _pointStrokePaint);

            // Punkt-Label
            if (labels != null && i < labels.Length && !string.IsNullOrEmpty(labels[i]))
            {
                canvas.DrawText(labels[i], sx + 8, sy - 4, _labelPaint);
            }

            // Abstand zum vorherigen Punkt
            if (i > 0)
            {
                var prevSx = (float)((x[i - 1] - centerX) * scale);
                var prevSy = (float)((y[i - 1] - centerY) * scale * -1);
                var dist = Math.Sqrt((x[i] - x[i - 1]) * (x[i] - x[i - 1]) +
                                     (y[i] - y[i - 1]) * (y[i] - y[i - 1]));
                var midX = (sx + prevSx) / 2;
                var midY = (sy + prevSy) / 2;
                canvas.DrawText($"{dist:F2}m", midX, midY - 4, _measurePaint);
            }
        }

        // Zeichnungs-Vorschau (aktuelle Punkte als gestrichelte Linie)
        if (drawingPreviewPoints != null && drawingPreviewPoints.Count > 0)
        {
            DrawPreview(canvas, drawingPreviewPoints, centerX, centerY, scale, drawingPreviewType);
        }

        canvas.Restore();
    }

    /// <summary>Zeichnungs-Vorschau: gestrichelte Linie/Polygon mit Punkten</summary>
    private void DrawPreview(SKCanvas canvas, IReadOnlyList<(double x, double y)> points,
        double centerX, double centerY, double scale, GardenElementType type)
    {
        // Punkte in Screen-Koordinaten umrechnen
        var screenPoints = new SKPoint[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            screenPoints[i] = new SKPoint(
                (float)((points[i].x - centerX) * scale),
                (float)((points[i].y - centerY) * scale * -1)
            );
        }

        // Polygon-Typen: Gefuellte Vorschau mit geschlossenem Pfad
        bool isPolygon = type is GardenElementType.Beet or GardenElementType.Rasen or GardenElementType.Terrasse;

        if (screenPoints.Length >= 2)
        {
            using var path = new SKPath();
            path.MoveTo(screenPoints[0]);
            for (int i = 1; i < screenPoints.Length; i++)
                path.LineTo(screenPoints[i]);

            if (isPolygon && screenPoints.Length >= 3)
            {
                // Geschlossenes Polygon: Flaeche + Umriss
                path.Close();
                canvas.DrawPath(path, _drawPreviewFillPaint);
            }

            canvas.DrawPath(path, _drawPreviewStrokePaint);
        }

        // Punkte zeichnen
        for (int i = 0; i < screenPoints.Length; i++)
        {
            var radius = i == screenPoints.Length - 1 ? 6f : 4f; // Letzter Punkt groesser
            canvas.DrawCircle(screenPoints[i], radius, _drawPreviewPointPaint);
            canvas.DrawCircle(screenPoints[i], radius, _drawPreviewPointStrokePaint);
        }

        // Abstand-Labels zwischen Vorschau-Punkten
        for (int i = 0; i < screenPoints.Length - 1; i++)
        {
            var dx = points[i + 1].x - points[i].x;
            var dy = points[i + 1].y - points[i].y;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            var midX = (screenPoints[i].X + screenPoints[i + 1].X) / 2;
            var midY = (screenPoints[i].Y + screenPoints[i + 1].Y) / 2;
            canvas.DrawText($"{dist:F2}m", midX, midY - 6, _measurePaint);
        }
    }

    private void DrawFilledPolygon(SKCanvas canvas, List<(double x, double y)> points,
        double centerX, double centerY, double scale, SKColor color)
    {
        using var path = new SKPath();
        for (int i = 0; i < points.Count; i++)
        {
            var sx = (float)((points[i].x - centerX) * scale);
            var sy = (float)((points[i].y - centerY) * scale * -1);
            if (i == 0) path.MoveTo(sx, sy);
            else path.LineTo(sx, sy);
        }
        path.Close();

        _fillPaint.Color = color.WithAlpha(100);
        canvas.DrawPath(path, _fillPaint);

        _strokePaint.Color = color;
        canvas.DrawPath(path, _strokePaint);
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

        _strokePaint.StrokeWidth = 2f; // Zuruecksetzen
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
        _drawPreviewStrokePaint.Dispose();
        _drawPreviewFillPaint.Dispose();
        _drawPreviewPointPaint.Dispose();
        _drawPreviewPointStrokePaint.Dispose();
    }
}
