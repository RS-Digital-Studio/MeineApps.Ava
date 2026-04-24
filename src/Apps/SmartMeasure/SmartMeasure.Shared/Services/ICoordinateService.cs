namespace SmartMeasure.Shared.Services;

/// <summary>Konvertiert zwischen WGS84 (Lat/Lon) und UTM (metrische x/y Koordinaten).
/// Präzision: UTM-Transverse-Mercator bis ~30km vom Zentralmeridian auf Millimeter genau,
/// darüber hinaus wachsender Projektions-Fehler. Für Grundstücke bis ~2km hochpräzise.</summary>
public interface ICoordinateService
{
    /// <summary>WGS84 Lat/Lon → UTM Easting/Northing in Metern. Zone wird aus Longitude bestimmt.</summary>
    (double easting, double northing, int zone, char band) ToUtm(double latitude, double longitude);

    /// <summary>WGS84 Lat/Lon → UTM in erzwungener Zone. Wichtig für Grundstücke auf Zonen-Grenze
    /// (z.B. DE Longitude 12°): alle Punkte werden konsistent in eine Referenz-Zone projiziert,
    /// statt bei einigen Punkten in Zone N und bei anderen in N+1 zu landen.</summary>
    (double easting, double northing) ToUtmFixedZone(double latitude, double longitude, int zone);

    /// <summary>UTM → WGS84 Lat/Lon</summary>
    (double latitude, double longitude) FromUtm(double easting, double northing, int zone, char band);

    /// <summary>UTM-Zonen-Nummer (1–60) für eine Longitude.</summary>
    int GetUtmZone(double longitude);

    /// <summary>Konvertiert eine Liste von Lat/Lon-Punkten in lokale metrische Koordinaten
    /// (relativ zum Schwerpunkt, in Metern via UTM). Ideal für Flächen- und Abstands-
    /// berechnungen. Alle Punkte werden in der Zone des Schwerpunkts projiziert
    /// (konsistente Referenz, auch wenn einzelne Punkte über eine UTM-Zonen-Grenze liegen).</summary>
    (double[] x, double[] y, double[] z) ToLocalMetric(
        double[] latitudes, double[] longitudes, double[] altitudes);

    /// <summary>Einzelner Punkt: WGS84 → lokale Meter relativ zu (refLat, refLon, refAlt).
    /// Nutzt feste UTM-Zone der Referenz — konsistent auch über Zonengrenzen.</summary>
    (double x, double y, double z) LatLonToLocal(
        double latitude, double longitude, double altitude,
        double refLatitude, double refLongitude, double refAltitude);

    /// <summary>Einzelner Punkt: lokale Meter → WGS84 (inverse von LatLonToLocal).</summary>
    (double latitude, double longitude, double altitude) LocalToLatLon(
        double x, double y, double z,
        double refLatitude, double refLongitude, double refAltitude);

    /// <summary>Abstand zwischen zwei WGS84-Punkten in Metern (Haversine)</summary>
    double HaversineDistance(double lat1, double lon1, double lat2, double lon2);
}
