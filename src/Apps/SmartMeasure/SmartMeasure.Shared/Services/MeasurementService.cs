using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>
/// Verwaltet Messpunkte und berechnet geometrische Werte.
///
/// Präzision:
/// - Distanzen: Haversine-Formel (metergenau für &lt;10km)
/// - Flächen: UTM-Projektion via ICoordinateService + Convex-Hull-Shoelace
///   (ungeordnete Messpunkte liefern sonst falsche Flächen)
///
/// Thread-Safety: Alle Zugriffe auf CurrentPoints müssen vom UI-Thread erfolgen.
/// </summary>
public class MeasurementService : IMeasurementService
{
    private const double EarthRadius = 6371000.0;

    private readonly ICoordinateService _coordinateService;

    /// <summary>Aktuelle Messpunkte. NUR vom UI-Thread lesen/schreiben.</summary>
    public List<SurveyPoint> CurrentPoints { get; } = [];

    public event Action<SurveyPoint>? PointAdded;
    public event Action? PointsReset;

    public MeasurementService(ICoordinateService coordinateService)
    {
        _coordinateService = coordinateService;
    }

    public void AddPoint(SurveyPoint point)
    {
        CurrentPoints.Add(point);
        PointAdded?.Invoke(point);
    }

    public void ReplacePoints(IEnumerable<SurveyPoint> points)
    {
        CurrentPoints.Clear();
        CurrentPoints.AddRange(points);
        // Einmal am Ende — Listener rechnen ihre Mesh/Charts nicht pro Punkt neu
        PointsReset?.Invoke();
    }

    public void ClearPoints()
    {
        CurrentPoints.Clear();
        PointsReset?.Invoke();
    }

    public double CalculateDistance2D(SurveyPoint a, SurveyPoint b)
    {
        // Haversine-Formel für kurze Distanzen (< 10km)
        var dLat = ToRad(b.Latitude - a.Latitude);
        var dLon = ToRad(b.Longitude - a.Longitude);
        var lat1 = ToRad(a.Latitude);
        var lat2 = ToRad(b.Latitude);

        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return 2 * EarthRadius * Math.Asin(Math.Sqrt(h));
    }

    public double CalculateDistance3D(SurveyPoint a, SurveyPoint b)
    {
        var d2d = CalculateDistance2D(a, b);
        var dAlt = b.Altitude - a.Altitude;
        return Math.Sqrt(d2d * d2d + dAlt * dAlt);
    }

    /// <summary>
    /// Berechnet die Polygonfläche in m². Nutzt UTM-Projektion für hohe Präzision
    /// und bildet die konvexe Hülle, da Messpunkte in beliebiger
    /// Reihenfolge vorliegen können und Shoelace auf ungeordneten Punkten
    /// komplett falsche Ergebnisse liefern würde.
    /// </summary>
    public double CalculateArea(List<SurveyPoint> points)
    {
        if (points.Count < 3) return 0;

        // UTM statt naive 111320-Approximation — auf 100m würde die Approximation
        // ~8cm Fehler einführen.
        var lats = points.Select(p => p.Latitude).ToArray();
        var lons = points.Select(p => p.Longitude).ToArray();
        var alts = new double[points.Count]; // Höhe irrelevant für 2D-Fläche

        var (xs, ys, _) = _coordinateService.ToLocalMetric(lats, lons, alts);

        // Convex-Hull bilden (Andrew's Monotone Chain), damit Shoelace sinnvoll ist
        var hull = ConvexHull(xs, ys);
        if (hull.Count < 3) return 0;

        double area = 0;
        for (int i = 0; i < hull.Count; i++)
        {
            int j = (i + 1) % hull.Count;
            area += hull[i].x * hull[j].y;
            area -= hull[j].x * hull[i].y;
        }

        return Math.Abs(area) / 2.0;
    }

    public double CalculatePerimeter(List<SurveyPoint> points)
    {
        if (points.Count < 2) return 0;

        double perimeter = 0;
        for (int i = 0; i < points.Count; i++)
        {
            int j = (i + 1) % points.Count;
            perimeter += CalculateDistance2D(points[i], points[j]);
        }

        return perimeter;
    }

    /// <summary>
    /// Andrew's Monotone Chain O(n log n). Returns CCW-Reihenfolge.
    /// </summary>
    private static List<(double x, double y)> ConvexHull(double[] xs, double[] ys)
    {
        var n = xs.Length;
        var pts = new (double x, double y)[n];
        for (int i = 0; i < n; i++) pts[i] = (xs[i], ys[i]);

        Array.Sort(pts, (a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        // Duplikate entfernen
        var unique = new List<(double x, double y)>(n);
        for (int i = 0; i < n; i++)
        {
            if (i > 0 && Math.Abs(pts[i].x - pts[i - 1].x) < 1e-12
                      && Math.Abs(pts[i].y - pts[i - 1].y) < 1e-12) continue;
            unique.Add(pts[i]);
        }
        if (unique.Count < 3) return unique;

        var m = unique.Count;
        var hull = new List<(double x, double y)>(2 * m);

        for (int i = 0; i < m; i++)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], unique[i]) <= 0)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(unique[i]);
        }

        var lowerCount = hull.Count + 1;
        for (int i = m - 2; i >= 0; i--)
        {
            while (hull.Count >= lowerCount && Cross(hull[^2], hull[^1], unique[i]) <= 0)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(unique[i]);
        }

        hull.RemoveAt(hull.Count - 1);
        return hull;
    }

    private static double Cross((double x, double y) o, (double x, double y) a, (double x, double y) b)
        => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

    private static double ToRad(double degrees) => degrees * Math.PI / 180.0;
}
