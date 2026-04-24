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

    /// <summary>LEGACY v1: Lokale Meter-Punkte aus JSON parsen. Liefert leere Liste für v2-Format.
    /// Für neue Verwendung: GetLocalPoints() oder ParsePointsWgs84().</summary>
    List<(double x, double y)> ParsePoints(string json);

    /// <summary>LEGACY v1: Lokale Meter-Punkte als JSON-Array serialisieren.
    /// Für neue Verwendung: SerializePointsWgs84().</summary>
    string SerializePoints(List<(double x, double y)> points);

    /// <summary>v2: Absolute WGS84 Lat/Lon aus JSON parsen. Liefert null wenn JSON im v1-Format
    /// (Array-Root) ist.</summary>
    List<(double latitude, double longitude)>? ParsePointsWgs84(string json);

    /// <summary>v2: Absolute WGS84 Lat/Lon als JSON serialisieren
    /// (Format: {"v":2,"points":[[lat,lon],...]}).</summary>
    string SerializePointsWgs84(List<(double latitude, double longitude)> points);

    /// <summary>Gartenelement in lokale Meter-Koordinaten relativ zu (refLat, refLon) konvertieren.
    /// Erkennt v1 (Direktübernahme, ignoriert Ref) und v2 (UTM-Projektion via CoordinateService).</summary>
    List<(double x, double y)> GetLocalPoints(
        GardenElement element, double refLatitude, double refLongitude,
        ICoordinateService coordinateService);

    /// <summary>Lokale Meter-Punkte in WGS84 Lat/Lon konvertieren (für Speicherung als v2).</summary>
    List<(double latitude, double longitude)> LocalToWgs84(
        IReadOnlyList<(double x, double y)> localPoints,
        double refLatitude, double refLongitude,
        ICoordinateService coordinateService);
}
