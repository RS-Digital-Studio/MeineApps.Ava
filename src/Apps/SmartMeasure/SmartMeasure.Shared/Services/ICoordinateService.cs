namespace SmartMeasure.Shared.Services;

/// <summary>Konvertiert zwischen WGS84 (Lat/Lon) und UTM (metrische x/y Koordinaten)</summary>
public interface ICoordinateService
{
    /// <summary>WGS84 Lat/Lon → UTM Easting/Northing in Metern</summary>
    (double easting, double northing, int zone, char band) ToUtm(double latitude, double longitude);

    /// <summary>UTM → WGS84 Lat/Lon</summary>
    (double latitude, double longitude) FromUtm(double easting, double northing, int zone, char band);

    /// <summary>Konvertiert eine Liste von Lat/Lon-Punkten in lokale metrische Koordinaten
    /// (relativ zum Schwerpunkt, in Metern). Ideal fuer Flaechen-/Abstandsberechnungen.</summary>
    (double[] x, double[] y, double[] z) ToLocalMetric(
        double[] latitudes, double[] longitudes, double[] altitudes);

    /// <summary>Abstand zwischen zwei WGS84-Punkten in Metern (Haversine)</summary>
    double HaversineDistance(double lat1, double lon1, double lat2, double lon2);
}
