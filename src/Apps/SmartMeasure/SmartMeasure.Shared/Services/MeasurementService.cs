using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Verwaltet Messpunkte und berechnet geometrische Werte</summary>
public class MeasurementService : IMeasurementService
{
    private const double EarthRadius = 6371000.0; // Meter

    public List<SurveyPoint> CurrentPoints { get; } = [];
    public event Action<SurveyPoint>? PointAdded;

    public void AddPoint(SurveyPoint point)
    {
        CurrentPoints.Add(point);
        PointAdded?.Invoke(point);
    }

    public void ClearPoints()
    {
        CurrentPoints.Clear();
    }

    public double CalculateDistance2D(SurveyPoint a, SurveyPoint b)
    {
        // Haversine-Formel fuer kurze Distanzen (< 10km)
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

    public double CalculateArea(List<SurveyPoint> points)
    {
        if (points.Count < 3) return 0;

        // Shoelace-Formel auf UTM-Koordinaten (lokale metrische Projektion)
        var (xs, ys) = ToLocalMetric(points);

        double area = 0;
        for (int i = 0; i < xs.Length; i++)
        {
            int j = (i + 1) % xs.Length;
            area += xs[i] * ys[j];
            area -= xs[j] * ys[i];
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

    /// <summary>Konvertiert Lat/Lon in lokale metrische Koordinaten (Meter, relativ zum Schwerpunkt)</summary>
    private static (double[] x, double[] y) ToLocalMetric(List<SurveyPoint> points)
    {
        // Schwerpunkt als Referenz
        var centerLat = points.Average(p => p.Latitude);
        var centerLon = points.Average(p => p.Longitude);

        var metersPerDegreeLat = 111320.0;
        var metersPerDegreeLon = 111320.0 * Math.Cos(ToRad(centerLat));

        var xs = points.Select(p => (p.Longitude - centerLon) * metersPerDegreeLon).ToArray();
        var ys = points.Select(p => (p.Latitude - centerLat) * metersPerDegreeLat).ToArray();

        return (xs, ys);
    }

    private static double ToRad(double degrees) => degrees * Math.PI / 180.0;
}
