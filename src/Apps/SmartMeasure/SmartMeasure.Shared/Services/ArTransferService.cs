using System.Text.Json;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Sensor-Fusion: Konvertiert AR-Koordinaten (lokal, relativ) nach WGS84 und uebertraegt ins Projekt</summary>
public class ArTransferService : IArTransferService
{
    private readonly IProjectService _projectService;
    private readonly ICoordinateService _coordinateService;
    private readonly IMeasurementService _measurementService;

    /// <summary>Meter pro Breitengrad (WGS84 Naeherung)</summary>
    private const double MetersPerDegreeLat = 111320.0;

    /// <summary>FixQuality-Wert fuer AR-erfasste Punkte</summary>
    private const int ArFixQuality = 10;

    /// <summary>Minimale geschaetzte AR-Genauigkeit in cm</summary>
    private const float MinArAccuracyCm = 50f;

    /// <summary>Faktor GPS-Accuracy (m) → AR-Genauigkeit (cm). AR addiert eigene Ungenauigkeit auf GPS-Basis</summary>
    private const float GpsToArAccuracyFactor = 100f;

    /// <summary>Mapping: ArContourType → GardenElementType (vollstaendig fuer alle 9 Typen)</summary>
    private static readonly Dictionary<ArContourType, GardenElementType> ContourTypeMapping = new()
    {
        [ArContourType.Weg] = GardenElementType.Weg,
        [ArContourType.Beet] = GardenElementType.Beet,
        [ArContourType.Mauer] = GardenElementType.Mauer,
        [ArContourType.Zaun] = GardenElementType.Zaun,
        [ArContourType.Terrasse] = GardenElementType.Terrasse,
        [ArContourType.Grenze] = GardenElementType.Grenze,
        [ArContourType.Gebaeude] = GardenElementType.Gebaeude,
        [ArContourType.Wasser] = GardenElementType.Wasser,
        [ArContourType.Kante] = GardenElementType.Kante,
    };

    public ArTransferService(
        IProjectService projectService,
        ICoordinateService coordinateService,
        IMeasurementService measurementService)
    {
        _projectService = projectService;
        _coordinateService = coordinateService;
        _measurementService = measurementService;
    }

    public async Task<int> TransferToProjectAsync(ArCaptureResult result, int projectId)
    {
        if (!result.HasGpsReference)
            throw new InvalidOperationException("AR-Ergebnis hat keine GPS-Referenz fuer Georeferenzierung");

        var transferredCount = 0;

        // 1. Einzelpunkte konvertieren und speichern (mit per-Punkt Fehlerbehandlung)
        var surveyPoints = ConvertToSurveyPoints(result, projectId);
        foreach (var point in surveyPoints)
        {
            try
            {
                await _projectService.AddPointAsync(projectId, point);
                _measurementService.AddPoint(point);
                transferredCount++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AR-Transfer: Punkt fehlgeschlagen: {ex.Message}");
            }
        }

        // 2. Konturen als GardenElements konvertieren und speichern
        var gardenElements = ConvertToGardenElements(result, projectId);
        foreach (var element in gardenElements)
        {
            try
            {
                await _projectService.AddGardenElementAsync(projectId, element);
                transferredCount++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AR-Transfer: Kontur fehlgeschlagen: {ex.Message}");
            }
        }

        // 3. Projekt-Metadaten aktualisieren
        var project = await _projectService.GetProjectAsync(projectId);
        if (project != null)
        {
            project.PointCount = project.Points.Count;
            await _projectService.UpdateProjectAsync(project);
        }

        return transferredCount;
    }

    public List<SurveyPoint> ConvertToSurveyPoints(ArCaptureResult result, int projectId)
    {
        if (!result.HasGpsReference)
            return [];

        var gpsLat = result.GpsLatitude!.Value;
        var gpsLon = result.GpsLongitude!.Value;
        var gpsAlt = result.GpsAltitude ?? 0.0;
        var headingDeg = result.MagneticHeading ?? 0f;
        var gpsAccuracy = result.GpsAccuracy ?? 5f;

        // Fallback-Accuracy wenn Geospatial nicht aktiv (cm, aus GPS * Faktor)
        var fallbackAccuracyCm = Math.Max(gpsAccuracy * GpsToArAccuracyFactor, MinArAccuracyCm);

        // Heading-Rotation nur als Fallback — Geospatial-Koords pro Punkt werden bevorzugt
        var headingRad = headingDeg * Math.PI / 180.0;
        var sinH = Math.Sin(headingRad);
        var cosH = Math.Cos(headingRad);
        var metersPerDegreeLon = MetersPerDegreeLat * Math.Cos(gpsLat * Math.PI / 180.0);

        var points = new List<SurveyPoint>(result.Points.Count);

        foreach (var arPoint in result.Points)
        {
            double finalLat, finalLon, finalAlt;
            float finalAccuracyCm;

            // Priorität 1: Geospatial-Koords pro Punkt (±1-3m via VPS, höchste Präzision)
            if (arPoint.GeoLatitude.HasValue && arPoint.GeoLongitude.HasValue)
            {
                finalLat = arPoint.GeoLatitude.Value;
                finalLon = arPoint.GeoLongitude.Value;
                finalAlt = arPoint.GeoAltitude ?? (gpsAlt + arPoint.Y);
                // Geo-Accuracy in Metern → cm umrechnen
                finalAccuracyCm = arPoint.GeoHorizontalAccuracy.HasValue
                    ? arPoint.GeoHorizontalAccuracy.Value * 100f
                    : fallbackAccuracyCm;
            }
            else
            {
                // Priorität 2: Rotation aus AR-Lokal-Koords + Heading (Fallback ~±50cm)
                var (newLat, newLon) = RotateAndProject(
                    arPoint.X, arPoint.Z, gpsLat, gpsLon, sinH, cosH, metersPerDegreeLon);
                finalLat = newLat;
                finalLon = newLon;
                finalAlt = gpsAlt + arPoint.Y;
                finalAccuracyCm = fallbackAccuracyCm;
            }

            points.Add(new SurveyPoint
            {
                ProjectId = projectId,
                Latitude = finalLat,
                Longitude = finalLon,
                Altitude = finalAlt,
                HorizontalAccuracy = finalAccuracyCm,
                VerticalAccuracy = finalAccuracyCm,
                TiltAngle = 0f,
                TiltAzimuth = 0f,
                FixQuality = ArFixQuality,
                SatelliteCount = 0,
                MagAccuracy = 0,
                Timestamp = arPoint.Timestamp,
                Label = arPoint.Label
            });
        }

        return points;
    }

    public List<GardenElement> ConvertToGardenElements(ArCaptureResult result, int projectId)
    {
        if (!result.HasGpsReference)
            return [];

        var gpsLat = result.GpsLatitude!.Value;
        var gpsLon = result.GpsLongitude!.Value;
        var headingDeg = result.MagneticHeading ?? 0f;

        var headingRad = headingDeg * Math.PI / 180.0;
        var sinH = Math.Sin(headingRad);
        var cosH = Math.Cos(headingRad);
        var metersPerDegreeLon = MetersPerDegreeLat * Math.Cos(gpsLat * Math.PI / 180.0);

        var elements = new List<GardenElement>(result.Contours.Count);

        for (var i = 0; i < result.Contours.Count; i++)
        {
            var contour = result.Contours[i];
            if (contour.Points.Count < 2) continue;

            // Kontur-Punkte nach WGS84 umrechnen — Geospatial-Koords pro Punkt bevorzugen
            var wgsPoints = new List<(double lat, double lon)>(contour.Points.Count);
            foreach (var arPoint in contour.Points)
            {
                if (arPoint.GeoLatitude.HasValue && arPoint.GeoLongitude.HasValue)
                {
                    // VPS-Koords direkt nutzen (höchste Präzision)
                    wgsPoints.Add((arPoint.GeoLatitude.Value, arPoint.GeoLongitude.Value));
                }
                else
                {
                    // Fallback auf Heading-basierte Rotation aus AR-Lokal-Koords
                    var (lat, lon) = RotateAndProject(
                        arPoint.X, arPoint.Z, gpsLat, gpsLon, sinH, cosH, metersPerDegreeLon);
                    wgsPoints.Add((lat, lon));
                }
            }

            // WGS84 → lokale UTM-Meter fuer PointsJson
            var lats = wgsPoints.Select(p => p.lat).ToArray();
            var lons = wgsPoints.Select(p => p.lon).ToArray();
            var alts = new double[wgsPoints.Count]; // Hoehe 0 fuer 2D-Gartenelemente
            var (xs, ys, _) = _coordinateService.ToLocalMetric(lats, lons, alts);

            // Punkte als JSON serialisieren (Format: [[x,y], [x,y], ...])
            var pointArrays = new double[xs.Length][];
            for (var j = 0; j < xs.Length; j++)
                pointArrays[j] = [xs[j], ys[j]];
            var pointsJson = JsonSerializer.Serialize(pointArrays);

            // ArContourType → GardenElementType (unbekannte Typen → Beet als Fallback)
            var elementType = ContourTypeMapping.GetValueOrDefault(contour.ContourType, GardenElementType.Beet);

            var element = new GardenElement
            {
                ProjectId = projectId,
                ElementType = elementType,
                PointsJson = pointsJson,
                Notes = contour.Label ?? $"AR-Kontur {i + 1}",
                SortOrder = i,
            };

            // Flaeche/Laenge berechnen
            var utmPoints = pointArrays.Select(a => (a[0], a[1])).ToList();
            if (contour.IsClosed && contour.Points.Count >= 3)
                element.AreaSquareMeters = CalculatePolygonArea(utmPoints);
            element.LengthMeters = CalculatePolylineLength(utmPoints);

            elements.Add(element);
        }

        return elements;
    }

    /// <summary>
    /// AR-Punkt (X/Z lokal) nach Heading rotieren und in WGS84 projizieren.
    /// </summary>
    /// <remarks>
    /// ARCore-Koordinatensystem: +X = rechts, +Y = oben, +Z = nach HINTEN (vom Gerät weg).
    /// Entsprechend zeigt -Z in Blickrichtung der Kamera.
    ///
    /// MagneticHeading: Grad von Norden im Uhrzeigersinn (0° = Norden, 90° = Osten).
    ///
    /// Transformation Local → World:
    /// Lokaler "rechts"-Vektor +X zeigt bei Heading h im World-Frame nach (sin(h-90°), cos(h-90°))
    ///   = (cos(h), -sin(h)) im (east, north)-System.
    /// Lokaler "vorne"-Vektor -Z zeigt im World-Frame nach (sin(h), cos(h)).
    ///
    /// Daraus folgt:
    ///   east  = arX * cos(h) + (-arZ) * sin(h) = arX * cos(h) - arZ * sin(h)
    ///   north = arX * (-sin(h)) + (-arZ) * cos(h) = -arX * sin(h) - arZ * cos(h)
    ///
    /// Verifikations-Tests:
    /// - Heading 0 (Nord), arZ=-1 (vorne): east=0, north=+1 ✓
    /// - Heading 90 (Ost), arZ=-1 (vorne): east=+1, north=0 ✓
    /// - Heading 90 (Ost), arX=-1 (links): east=0, north=+1 ✓
    /// - Heading 180 (Süd), arZ=-1 (vorne): east=0, north=-1 ✓
    /// - Heading 270 (West), arZ=-1 (vorne): east=-1, north=0 ✓
    /// </remarks>
    private static (double latitude, double longitude) RotateAndProject(
        float arX, float arZ,
        double gpsLat, double gpsLon,
        double sinH, double cosH,
        double metersPerDegreeLon)
    {
        var eastOffset = arX * cosH - arZ * sinH;
        var nordOffset = -arX * sinH - arZ * cosH;

        // Meter → WGS84 Grad-Offset
        var newLat = gpsLat + nordOffset / MetersPerDegreeLat;
        var newLon = gpsLon + eastOffset / metersPerDegreeLon;

        return (newLat, newLon);
    }

    /// <summary>Shoelace-Flaeche eines Polygons in m² (UTM-Meter-Koordinaten)</summary>
    private static double CalculatePolygonArea(List<(double x, double y)> points)
    {
        if (points.Count < 3) return 0;

        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            int j = (i + 1) % points.Count;
            area += points[i].x * points[j].y;
            area -= points[j].x * points[i].y;
        }
        return Math.Abs(area) / 2.0;
    }

    /// <summary>Laenge einer Polylinie in Metern (UTM-Meter-Koordinaten)</summary>
    private static double CalculatePolylineLength(List<(double x, double y)> points)
    {
        double length = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            var dx = points[i + 1].x - points[i].x;
            var dy = points[i + 1].y - points[i].y;
            length += Math.Sqrt(dx * dx + dy * dy);
        }
        return length;
    }
}
