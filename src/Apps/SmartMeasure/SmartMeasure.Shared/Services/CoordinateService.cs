namespace SmartMeasure.Shared.Services;

/// <summary>WGS84 ↔ UTM Konvertierung (Transverse-Mercator-Projektion)</summary>
public class CoordinateService : ICoordinateService
{
    // WGS84 Ellipsoid-Parameter
    private const double SemiMajorAxis = 6378137.0; // a
    private const double Flattening = 1.0 / 298.257223563; // f
    private const double SemiMinorAxis = SemiMajorAxis * (1 - Flattening); // b
    private const double Eccentricity2 = 1 - (SemiMinorAxis * SemiMinorAxis) / (SemiMajorAxis * SemiMajorAxis); // e²
    private const double Eccentricity2Prime = Eccentricity2 / (1 - Eccentricity2); // e'²
    private const double ScaleFactor = 0.9996; // k0
    private const double FalseEasting = 500000.0;
    private const double EarthRadius = 6371000.0;

    /// <summary>Schwelle für Zonen-Abweichungs-Warnung in Grad Longitude</summary>
    private const double ZoneDeviationWarnThresholdDeg = 3.0;

    public int GetUtmZone(double longitude)
    {
        var zone = (int)Math.Floor((longitude + 180.0) / 6.0) + 1;
        if (zone < 1) zone = 1;
        if (zone > 60) zone = 60;
        return zone;
    }

    public (double easting, double northing, int zone, char band) ToUtm(double latitude, double longitude)
    {
        var zone = GetUtmZone(longitude);
        var band = GetUtmBand(latitude);
        var (easting, northing) = ToUtmFixedZone(latitude, longitude, zone);
        // Südhalbkugel: False Northing 10.000.000
        if (latitude < 0)
            northing += 10000000.0;
        return (easting, northing, zone, band);
    }

    public (double easting, double northing) ToUtmFixedZone(double latitude, double longitude, int zone)
    {
        var lonOrigin = (zone - 1) * 6.0 - 180.0 + 3.0; // Zentralmeridian der Ziel-Zone

        var latRad = latitude * Math.PI / 180.0;
        var lonRad = longitude * Math.PI / 180.0;
        var lonOriginRad = lonOrigin * Math.PI / 180.0;

        var sinLat = Math.Sin(latRad);
        var cosLat = Math.Cos(latRad);
        var tanLat = Math.Tan(latRad);

        var n = SemiMajorAxis / Math.Sqrt(1 - Eccentricity2 * sinLat * sinLat);
        var t = tanLat * tanLat;
        var c = Eccentricity2Prime * cosLat * cosLat;
        var a2 = cosLat * (lonRad - lonOriginRad);

        var m = SemiMajorAxis * (
            (1 - Eccentricity2 / 4 - 3 * Eccentricity2 * Eccentricity2 / 64
                - 5 * Eccentricity2 * Eccentricity2 * Eccentricity2 / 256) * latRad
            - (3 * Eccentricity2 / 8 + 3 * Eccentricity2 * Eccentricity2 / 32
                + 45 * Eccentricity2 * Eccentricity2 * Eccentricity2 / 1024) * Math.Sin(2 * latRad)
            + (15 * Eccentricity2 * Eccentricity2 / 256
                + 45 * Eccentricity2 * Eccentricity2 * Eccentricity2 / 1024) * Math.Sin(4 * latRad)
            - (35 * Eccentricity2 * Eccentricity2 * Eccentricity2 / 3072) * Math.Sin(6 * latRad));

        var easting = ScaleFactor * n * (
            a2
            + (1 - t + c) * a2 * a2 * a2 / 6
            + (5 - 18 * t + t * t + 72 * c - 58 * Eccentricity2Prime)
                * a2 * a2 * a2 * a2 * a2 / 120)
            + FalseEasting;

        var northing = ScaleFactor * (
            m + n * tanLat * (
                a2 * a2 / 2
                + (5 - t + 9 * c + 4 * c * c) * a2 * a2 * a2 * a2 / 24
                + (61 - 58 * t + t * t + 600 * c - 330 * Eccentricity2Prime)
                    * a2 * a2 * a2 * a2 * a2 * a2 / 720));

        return (easting, northing);
    }

    public (double latitude, double longitude) FromUtm(double easting, double northing, int zone, char band)
    {
        var isNorthern = band >= 'N';
        if (!isNorthern)
            northing -= 10000000.0;

        var lonOrigin = (zone - 1) * 6.0 - 180.0 + 3.0;
        var x = easting - FalseEasting;
        var y = northing;

        var mu = y / (SemiMajorAxis * ScaleFactor *
            (1 - Eccentricity2 / 4 - 3 * Eccentricity2 * Eccentricity2 / 64
                - 5 * Eccentricity2 * Eccentricity2 * Eccentricity2 / 256));

        var e1 = (1 - Math.Sqrt(1 - Eccentricity2)) / (1 + Math.Sqrt(1 - Eccentricity2));

        var phi1 = mu
            + (3 * e1 / 2 - 27 * e1 * e1 * e1 / 32) * Math.Sin(2 * mu)
            + (21 * e1 * e1 / 16 - 55 * e1 * e1 * e1 * e1 / 32) * Math.Sin(4 * mu)
            + (151 * e1 * e1 * e1 / 96) * Math.Sin(6 * mu);

        var sinPhi1 = Math.Sin(phi1);
        var cosPhi1 = Math.Cos(phi1);
        var tanPhi1 = Math.Tan(phi1);

        var n1 = SemiMajorAxis / Math.Sqrt(1 - Eccentricity2 * sinPhi1 * sinPhi1);
        var r1 = SemiMajorAxis * (1 - Eccentricity2) /
            Math.Pow(1 - Eccentricity2 * sinPhi1 * sinPhi1, 1.5);
        var t1 = tanPhi1 * tanPhi1;
        var c1 = Eccentricity2Prime * cosPhi1 * cosPhi1;
        var d = x / (n1 * ScaleFactor);

        var lat = phi1 - (n1 * tanPhi1 / r1) * (
            d * d / 2
            - (5 + 3 * t1 + 10 * c1 - 4 * c1 * c1 - 9 * Eccentricity2Prime) * d * d * d * d / 24
            + (61 + 90 * t1 + 298 * c1 + 45 * t1 * t1 - 252 * Eccentricity2Prime - 3 * c1 * c1)
                * d * d * d * d * d * d / 720);

        var lon = (d
            - (1 + 2 * t1 + c1) * d * d * d / 6
            + (5 - 2 * c1 + 28 * t1 - 3 * c1 * c1 + 8 * Eccentricity2Prime + 24 * t1 * t1)
                * d * d * d * d * d / 120) / cosPhi1;

        return (lat * 180.0 / Math.PI, lonOrigin + lon * 180.0 / Math.PI);
    }

    public (double[] x, double[] y, double[] z) ToLocalMetric(
        double[] latitudes, double[] longitudes, double[] altitudes)
    {
        if (latitudes.Length == 0)
            return ([], [], []);

        // Schwerpunkt als Referenz
        var centerLat = latitudes.Average();
        var centerLon = longitudes.Average();
        var centerAlt = altitudes.Average();

        // Feste UTM-Zone aus Schwerpunkt — konsistent auch über Zonen-Grenzen
        var refZone = GetUtmZone(centerLon);
        var (refEasting, refNorthing) = ToUtmFixedZone(centerLat, centerLon, refZone);

        // Zonen-Abweichungs-Warnung (Grundstücke bis ca. 30km sind unkritisch)
        var maxLonDeviation = 0.0;
        for (int i = 0; i < longitudes.Length; i++)
        {
            var d = Math.Abs(longitudes[i] - centerLon);
            if (d > maxLonDeviation) maxLonDeviation = d;
        }
        if (maxLonDeviation > ZoneDeviationWarnThresholdDeg)
        {
            System.Diagnostics.Debug.WriteLine(
                $"CoordinateService.ToLocalMetric: Punkte spannen {maxLonDeviation:F2}° Longitude " +
                $"— UTM-Präzision sinkt bei >{ZoneDeviationWarnThresholdDeg}° Distanz vom Zentralmeridian.");
        }

        var x = new double[latitudes.Length];
        var y = new double[latitudes.Length];
        var z = new double[latitudes.Length];

        for (int i = 0; i < latitudes.Length; i++)
        {
            var (e, n) = ToUtmFixedZone(latitudes[i], longitudes[i], refZone);
            x[i] = e - refEasting;
            y[i] = n - refNorthing;
            z[i] = altitudes[i] - centerAlt;
        }

        return (x, y, z);
    }

    public (double x, double y, double z) LatLonToLocal(
        double latitude, double longitude, double altitude,
        double refLatitude, double refLongitude, double refAltitude)
    {
        var refZone = GetUtmZone(refLongitude);
        var (refEasting, refNorthing) = ToUtmFixedZone(refLatitude, refLongitude, refZone);
        var (e, n) = ToUtmFixedZone(latitude, longitude, refZone);
        return (e - refEasting, n - refNorthing, altitude - refAltitude);
    }

    public (double latitude, double longitude, double altitude) LocalToLatLon(
        double x, double y, double z,
        double refLatitude, double refLongitude, double refAltitude)
    {
        var refZone = GetUtmZone(refLongitude);
        var refBand = GetUtmBand(refLatitude);
        var (refEasting, refNorthing) = ToUtmFixedZone(refLatitude, refLongitude, refZone);

        // Delta in absolute UTM-Koords zurückrechnen
        var absE = refEasting + x;
        var absN = refNorthing + y;

        // Südhalbkugel-False-Northing rückgängig-sicher via ToUtm/FromUtm Konvention
        var utmNorthing = refLatitude < 0 ? absN + 10000000.0 : absN;
        var (lat, lon) = FromUtm(absE, utmNorthing, refZone, refBand);
        return (lat, lon, refAltitude + z);
    }

    public double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadius * Math.Asin(Math.Sqrt(a));
    }

    public double GetBearing(double lat1, double lon1, double lat2, double lon2)
    {
        var phi1 = lat1 * Math.PI / 180.0;
        var phi2 = lat2 * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;

        var y = Math.Sin(dLon) * Math.Cos(phi2);
        var x = Math.Cos(phi1) * Math.Sin(phi2)
              - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(dLon);

        var bearing = Math.Atan2(y, x) * 180.0 / Math.PI;
        if (bearing < 0) bearing += 360.0;
        return bearing;
    }

    private static char GetUtmBand(double latitude)
    {
        // UTM Band-Buchstaben (C-X, ohne I und O)
        var bands = "CDEFGHJKLMNPQRSTUVWX";
        var index = (int)Math.Floor((latitude + 80.0) / 8.0);
        if (index < 0) index = 0;
        if (index >= bands.Length) index = bands.Length - 1;
        return bands[index];
    }
}
