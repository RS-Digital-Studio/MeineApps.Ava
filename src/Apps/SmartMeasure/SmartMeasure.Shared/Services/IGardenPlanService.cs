using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Gartenelemente verwalten + Materialliste berechnen</summary>
public interface IGardenPlanService
{
    /// <summary>Materialliste fuer alle Gartenelemente eines Projekts berechnen</summary>
    List<MaterialEstimate> CalculateMaterials(List<GardenElement> elements);

    /// <summary>Flaeche eines Polygons berechnen (m²)</summary>
    double CalculatePolygonArea(List<(double x, double y)> points);

    /// <summary>Laenge einer Polylinie berechnen (m)</summary>
    double CalculatePolylineLength(List<(double x, double y)> points);

    /// <summary>Polygon-Punkte aus JSON deserialisieren</summary>
    List<(double x, double y)> ParsePoints(string json);

    /// <summary>Polygon-Punkte als JSON serialisieren</summary>
    string SerializePoints(List<(double x, double y)> points);
}
