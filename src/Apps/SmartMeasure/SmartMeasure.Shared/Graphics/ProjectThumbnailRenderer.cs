using SkiaSharp;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Graphics;

/// <summary>Mini-Vorschau fuer die Projekt-Liste. Zeigt Messpunkte als Draufsicht
/// mit Verbindungslinien und optionaler Polygon-Fuellung.</summary>
public static class ProjectThumbnailRenderer
{
    // App-Palette
    private static readonly SKColor PrimaryColor = new(255, 107, 0);       // #FF6B00 Orange (Punkte)
    private static readonly SKColor SecondaryColor = new(33, 150, 243);    // #2196F3 Blau (Linien)
    private static readonly SKColor AccentColor = new(76, 175, 80, 60);   // #4CAF50 Gruen halbtransparent (Polygon)
    private static readonly SKColor AccentStroke = new(76, 175, 80, 140); // Polygon-Rand
    private static readonly SKColor BgColor = new(26, 26, 46);            // #1A1A2E
    private static readonly SKColor TextDimmed = new(136, 153, 170);
    private static readonly SKColor TypeBadgeColor = new(255, 255, 255, 30);

    // Gecachte statische Paints (keine Allokation pro Render)
    private static readonly SKPaint FillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = AccentColor };
    private static readonly SKPaint PolygonStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = AccentStroke };
    private static readonly SKPaint LinePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = SecondaryColor };
    private static readonly SKPaint DotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = PrimaryColor };
    private static readonly SKPaint DotStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f, Color = new SKColor(255, 255, 255, 120) };
    private static readonly SKPaint BadgeBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = TypeBadgeColor };
    private static readonly SKPaint BadgeTextPaint = new() { IsAntialias = true, Color = TextDimmed };
    private static readonly SKPaint EmptyTextPaint = new() { Color = TextDimmed, IsAntialias = true };

    // SKFont-Instanzen (SkiaSharp 3.x — statisch, da Renderer stateless)
    private static readonly SKFont BadgeFont = new(SKTypeface.Default, 9f);
    private static readonly SKFont EmptyFont = new(SKTypeface.Default, 12f);

    /// <summary>Thumbnail rendern (statisch, kein State noetig)</summary>
    public static void Render(SKCanvas canvas, SKRect bounds,
        List<SurveyPoint> points, string projectType)
    {
        canvas.Clear(BgColor);

        if (points.Count == 0)
        {
            DrawEmptyState(canvas, bounds);
            return;
        }

        // Lat/Lon → lokale Meter (Schwerpunkt-basiert, inline)
        var (localX, localY) = ToLocalMetric(points);

        // Bounding-Box berechnen
        var minX = localX[0]; var maxX = localX[0];
        var minY = localY[0]; var maxY = localY[0];
        for (int i = 1; i < localX.Length; i++)
        {
            if (localX[i] < minX) minX = localX[i];
            if (localX[i] > maxX) maxX = localX[i];
            if (localY[i] < minY) minY = localY[i];
            if (localY[i] > maxY) maxY = localY[i];
        }

        // 10% Padding auf jeder Seite
        var paddingFrac = 0.1f;
        var drawBounds = new SKRect(
            bounds.Left + bounds.Width * paddingFrac,
            bounds.Top + bounds.Height * paddingFrac,
            bounds.Right - bounds.Width * paddingFrac,
            bounds.Bottom - bounds.Height * paddingFrac);

        // Skalierung: Punkte in drawBounds einpassen
        var rangeX = maxX - minX;
        var rangeY = maxY - minY;
        var range = Math.Max(rangeX, rangeY);
        if (range < 0.001) range = 1; // Einzelpunkt-Fallback

        var scale = Math.Min(drawBounds.Width, drawBounds.Height) / range;
        var centerX = (minX + maxX) / 2.0;
        var centerY = (minY + maxY) / 2.0;
        var offsetX = drawBounds.MidX;
        var offsetY = drawBounds.MidY;

        // Screen-Positionen berechnen
        var screenPts = new SKPoint[localX.Length];
        for (int i = 0; i < localX.Length; i++)
        {
            screenPts[i] = new SKPoint(
                (float)((localX[i] - centerX) * scale) + offsetX,
                (float)((localY[i] - centerY) * -scale) + offsetY); // Y invertieren (Nord=oben)
        }

        // 1. Polygon-Fuellung (>= 3 Punkte)
        if (points.Count >= 3)
            DrawPolygonFill(canvas, screenPts);

        // 2. Verbindungslinien
        DrawConnectionLines(canvas, screenPts);

        // 3. Punkte als Dots
        DrawPointDots(canvas, screenPts);

        // 4. Projekt-Typ Badge (oben rechts)
        if (!string.IsNullOrEmpty(projectType))
            DrawTypeBadge(canvas, bounds, projectType);
    }

    private static void DrawPolygonFill(SKCanvas canvas, SKPoint[] pts)
    {
        using var path = new SKPath();
        path.MoveTo(pts[0]);
        for (int i = 1; i < pts.Length; i++)
            path.LineTo(pts[i]);
        path.Close();

        canvas.DrawPath(path, FillPaint);
        canvas.DrawPath(path, PolygonStrokePaint);
    }

    private static void DrawConnectionLines(SKCanvas canvas, SKPoint[] pts)
    {
        if (pts.Length < 2) return;

        for (int i = 0; i < pts.Length - 1; i++)
            canvas.DrawLine(pts[i], pts[i + 1], LinePaint);
    }

    private static void DrawPointDots(SKCanvas canvas, SKPoint[] pts)
    {
        // Dot-Groesse relativ zur Thumbnail-Groesse (min 2, max 5)
        var dotRadius = Math.Clamp(pts.Length < 10 ? 4f : 3f, 2f, 5f);

        foreach (var pt in pts)
        {
            canvas.DrawCircle(pt, dotRadius, DotPaint);
            canvas.DrawCircle(pt, dotRadius, DotStrokePaint);
        }
    }

    private static void DrawTypeBadge(SKCanvas canvas, SKRect bounds, string projectType)
    {
        var textWidth = BadgeFont.MeasureText(projectType);
        var badgeRect = new SKRect(
            bounds.Right - textWidth - 10f,
            bounds.Top + 2f,
            bounds.Right - 2f,
            bounds.Top + 16f);

        canvas.DrawRoundRect(badgeRect, 3f, 3f, BadgeBgPaint);
        canvas.DrawText(projectType, bounds.Right - 6f, bounds.Top + 13f,
            SKTextAlign.Right, BadgeFont, BadgeTextPaint);
    }

    private static void DrawEmptyState(SKCanvas canvas, SKRect bounds)
    {
        canvas.DrawText("Leer", bounds.MidX, bounds.MidY + 4f,
            SKTextAlign.Center, EmptyFont, EmptyTextPaint);
    }

    /// <summary>Konvertiert Lat/Lon-Punkte in lokale Meter-Koordinaten (Schwerpunkt-basiert).
    /// Gleiche Logik wie CoordinateService.ToLocalMetric(), aber inline ohne DI.</summary>
    private static (double[] x, double[] y) ToLocalMetric(List<SurveyPoint> points)
    {
        var count = points.Count;
        var x = new double[count];
        var y = new double[count];

        // Schwerpunkt als Referenz
        double sumLat = 0, sumLon = 0;
        for (int i = 0; i < count; i++)
        {
            sumLat += points[i].Latitude;
            sumLon += points[i].Longitude;
        }
        var centerLat = sumLat / count;
        var centerLon = sumLon / count;

        const double metersPerDegreeLat = 111320.0;
        var metersPerDegreeLon = 111320.0 * Math.Cos(centerLat * Math.PI / 180.0);

        for (int i = 0; i < count; i++)
        {
            x[i] = (points[i].Longitude - centerLon) * metersPerDegreeLon;
            y[i] = (points[i].Latitude - centerLat) * metersPerDegreeLat;
        }

        return (x, y);
    }
}
