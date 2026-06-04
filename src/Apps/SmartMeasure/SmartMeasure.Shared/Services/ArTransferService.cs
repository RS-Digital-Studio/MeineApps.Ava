using System.Text.Json;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Sensor-Fusion: Konvertiert AR-Koordinaten (lokal, relativ) nach WGS84 und uebertraegt ins Projekt</summary>
public class ArTransferService : IArTransferService
{
    private readonly IProjectService _projectService;
    private readonly ICoordinateService _coordinateService;
    private readonly IMeasurementService _measurementService;
    private readonly IGeoidService _geoidService;

    /// <summary>FixQuality-Wert fuer AR-erfasste Punkte</summary>
    private const int ArFixQuality = 10;

    /// <summary>Minimale geschaetzte AR-Genauigkeit in cm</summary>
    private const float MinArAccuracyCm = 50f;

    /// <summary>Faktor GPS-Accuracy (m) → AR-Genauigkeit (cm). AR addiert eigene Ungenauigkeit auf GPS-Basis</summary>
    private const float GpsToArAccuracyFactor = 100f;

    /// <summary>
    /// VerticalAccuracy-Faktor relativ zur horizontalen Genauigkeit.
    /// GPS-Höhenmessungen sind klassisch ~1.5–2× ungenauer als Lagewerte (PDOP-/VDOP-Verhältnis).
    /// AR-Höhe addiert eine eigene Komponente (Ground-Plane-Detection ±5cm, Multi-Frame-Y-Drift ±2cm).
    /// </summary>
    private const float VerticalToHorizontalAccFactor = 1.8f;

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
        IMeasurementService measurementService,
        IGeoidService geoidService)
    {
        _projectService = projectService;
        _coordinateService = coordinateService;
        _measurementService = measurementService;
        _geoidService = geoidService;
    }

    public async Task<int> TransferToProjectAsync(ArCaptureResult result, int projectId)
    {
        // Ohne GPS-Referenz (z.B. Indoor, kein Fix) wuerde eine Georeferenzierung scheitern.
        // Die Messung selbst (Distanzen, Flaechen, relative Form) ist aber translation-invariant
        // und darf NICHT verloren gehen — frueher warf das hier eine Exception und der gesamte
        // AR-Transfer ging verloren. Stattdessen setzen wir einen Fallback-Ursprung: den
        // Schwerpunkt bereits vorhandener Projektpunkte, sonst einen neutralen Default. Masse
        // bleiben exakt, nur die absolute Karten-Lage ist dann ein Platzhalter.
        if (!result.HasGpsReference)
        {
            var existing = await _projectService.GetProjectAsync(projectId);
            if (existing != null && existing.Points.Count > 0)
            {
                result.GpsLatitude = existing.Points.Average(p => p.Latitude);
                result.GpsLongitude = existing.Points.Average(p => p.Longitude);
                result.GpsAltitude ??= existing.Points.Average(p => p.Altitude);
            }
            else
            {
                result.GpsLatitude ??= 48.7758;
                result.GpsLongitude ??= 9.1829;
                // Ellipsoid-Höhe (~568 m): nach der Geoid-Korrektur (~−48 m in DE) ergibt das
                // eine plausible NN-Höhe von ~520 m. Vorher stand hier 520 (eine NN-Größe), die
                // fälschlich als Ellipsoid einging → ~48 m Offset im Platzhalter.
                result.GpsAltitude ??= 568.0;
            }
            System.Diagnostics.Debug.WriteLine(
                "AR-Transfer: Keine GPS-Referenz — Fallback-Ursprung verwendet (Masse korrekt, Karten-Lage ungenau).");
        }

        var transferredCount = 0;

        // 1. Einzelpunkte konvertieren und speichern. Pro-Punkt-Try-Catch: DB-Insert kann
        //    failen, In-Memory-Add aber gelingen → erst DB, dann erst _measurementService,
        //    damit beide Welten konsistent sind.
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

        // Plan 3.1: Bei RTK-Quelle ist die GPS-Accuracy bereits cm-genau — wir respektieren
        // sie statt das 50cm-Minimum (MinArAccuracyCm) anzuwenden. Das ist der ganze Punkt
        // der RTK-Fusion: nicht die AR-Schätzung als Fehler-Ceiling, sondern den echten
        // RTK-Fehler durchreichen.
        var isRtk = result.GpsSource == ArGpsSource.RtkRover;

        // ARCore-Session-Ursprung liegt typisch in Augen-/Brusthöhe (1.0-1.7m über Boden).
        // Wenn der ArCaptureService eine horizontale Ground-Plane gefunden hat, ist deren
        // Y-Wert die Boden-Höhe relativ zum Session-Ursprung — wir ziehen diesen Offset
        // ab, damit ArPoint.Y absolute Höhe über Boden wird.
        var groundOffset = result.GroundPlaneY ?? 0f;

        // Fallback-Accuracy wenn Geospatial nicht aktiv (cm, aus GPS * Faktor).
        // Bei RTK: kein 50cm-Minimum, kein 100x-Faktor — die RTK-Accuracy ist bereits cm-genau.
        // Wir addieren nur einen kleinen ARCore-Drift-Term (5cm) auf den GPS-Wert.
        var fallbackAccuracyCm = isRtk
            ? Math.Max(gpsAccuracy * 100f + 5f, 2f) // RTK in m → cm + 5cm AR-Drift, min 2cm
            : Math.Max(gpsAccuracy * GpsToArAccuracyFactor, MinArAccuracyCm);

        // Heading-Rotation nur als Fallback — Geospatial-Koords pro Punkt werden bevorzugt
        var headingRad = headingDeg * Math.PI / 180.0;
        var sinH = Math.Sin(headingRad);
        var cosH = Math.Cos(headingRad);

        var points = new List<SurveyPoint>(result.Points.Count);

        foreach (var arPoint in result.Points)
        {
            double finalLat, finalLon, finalAltEllipsoid;
            float finalAccuracyCm;

            // Y-Wert relativ zum Boden (falls Ground-Plane erkannt wurde)
            var yRel = arPoint.Y - groundOffset;

            // Priorität 1: Geospatial-Koords pro Punkt (±1-3m via VPS, höchste Präzision)
            if (arPoint.GeoLatitude.HasValue && arPoint.GeoLongitude.HasValue)
            {
                finalLat = arPoint.GeoLatitude.Value;
                finalLon = arPoint.GeoLongitude.Value;
                finalAltEllipsoid = arPoint.GeoAltitude ?? (gpsAlt + yRel);
                // Geo-Accuracy in Metern → cm umrechnen
                finalAccuracyCm = arPoint.GeoHorizontalAccuracy.HasValue
                    ? arPoint.GeoHorizontalAccuracy.Value * 100f
                    : fallbackAccuracyCm;
            }
            else
            {
                // Priorität 2: Rotation aus AR-Lokal-Koords + Heading (Fallback ~±50cm).
                // UTM-basierte Umkehrung via CoordinateService — präziser als
                // 111320-Approximation (~8cm/100m Fehler auf 50° Breite).
                var (newLat, newLon) = RotateAndProject(
                    arPoint.X, arPoint.Z, gpsLat, gpsLon, sinH, cosH, _coordinateService);
                finalLat = newLat;
                finalLon = newLon;
                finalAltEllipsoid = gpsAlt + yRel;
                finalAccuracyCm = fallbackAccuracyCm;
            }

            // Android Location.Altitude / ARCore-Geospatial-Altitude sind WGS84-Ellipsoid.
            // Korrektur nach NN analog BLE-Pfad (in DE ~48m Differenz).
            var finalAlt = _geoidService.EllipsoidToGeoid(finalLat, finalLon, finalAltEllipsoid);

            // VerticalAccuracy ist nach Plan-Kap. 4.1 konservativ schlechter als die
            // horizontale (GPS-Höhe hat höheres VDOP, AR-Höhe kommt zusätzlich aus
            // Ground-Plane-Schätzung). 1.8× ist ein vorsichtiger Mittelwert.
            var verticalAccCm = finalAccuracyCm * VerticalToHorizontalAccFactor;

            // TiltAngle/MagAccuracy werden pro AR-Punkt aus der Camera-Pose und dem Magnetometer
            // beim Capture-Zeitpunkt übernommen — gibt späterer Tilt-Korrektur / Quality-Berechnung
            // belastbare Daten. TiltAzimuth bleibt 0, weil AR kein "Stab-Azimuth" hat.
            points.Add(new SurveyPoint
            {
                ProjectId = projectId,
                Latitude = finalLat,
                Longitude = finalLon,
                Altitude = finalAlt,
                HorizontalAccuracy = finalAccuracyCm,
                VerticalAccuracy = verticalAccCm,
                TiltAngle = arPoint.CameraPitchDeg,
                TiltAzimuth = 0f,
                FixQuality = ArFixQuality,
                SatelliteCount = 0,
                MagAccuracy = arPoint.MagAccuracyAtCapture,
                // Echte Mess-Konfidenz: Die RTK-Position ist nur der GPS-Anker des Session-
                // Ursprungs — die einzelnen Punkt-Lagen (X/Z lokal) stammen weiterhin aus
                // ARCore-HitTests. Der RTK-Fix wirkt daher als CEILING (Fix=1.0, Float=0.7)
                // und wird mit der echten ARCore-Punktgüte (Hit-Quality + Streuung + Tracking)
                // MULTIPLIZIERT, statt sie zu ersetzen. Sonst bekäme ein Punkt mit 8 cm
                // Streuung und Instant-Placement fälschlich Confidence 1.0.
                Confidence = isRtk
                    ? arPoint.Confidence * (result.RtkFixQuality == 4 ? 1f : 0.7f)
                    : arPoint.Confidence,
                Timestamp = arPoint.Timestamp,
                Label = arPoint.Label,
                // Plan-Kap. 5.6: Foto-Annotation pro Punkt durchreichen
                PhotoPath = arPoint.PhotoPath,
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
                    // Fallback auf Heading-basierte Rotation aus AR-Lokal-Koords via UTM
                    var (lat, lon) = RotateAndProject(
                        arPoint.X, arPoint.Z, gpsLat, gpsLon, sinH, cosH, _coordinateService);
                    wgsPoints.Add((lat, lon));
                }
            }

            // v2-Format: absolute WGS84 Lat/Lon direkt persistieren (drift-resistent)
            // Format: {"v":2,"points":[[lat,lon],...]}
            var pointArrays = wgsPoints.Select(p => new[] { p.lat, p.lon }).ToArray();
            var envelope = new { v = 2, points = pointArrays };
            var pointsJson = JsonSerializer.Serialize(envelope);

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

            // Flaeche/Laenge: Via Kontur-Schwerpunkt als temporäre lokale Referenz berechnen.
            // Area/Length sind translation-invariant — Referenz-Wahl egal, solange konsistent.
            var contourRefLat = wgsPoints.Average(p => p.lat);
            var contourRefLon = wgsPoints.Average(p => p.lon);
            var localPoints = new List<(double x, double y)>(wgsPoints.Count);
            foreach (var (lat, lon) in wgsPoints)
            {
                var (lx, ly, _) = _coordinateService.LatLonToLocal(lat, lon, 0,
                    contourRefLat, contourRefLon, 0);
                localPoints.Add((lx, ly));
            }

            if (contour.IsClosed && contour.Points.Count >= 3)
                element.AreaSquareMeters = CalculatePolygonArea(localPoints);
            element.LengthMeters = CalculatePolylineLength(localPoints, contour.IsClosed);

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
    ///
    /// Meter→WGS84-Umkehrung läuft über <see cref="ICoordinateService.LocalToLatLon"/> (UTM-basiert) —
    /// spart ~8 cm pro 100 m gegenüber der naiven 111320-m/Grad-Approximation.
    /// </remarks>
    private static (double latitude, double longitude) RotateAndProject(
        float arX, float arZ,
        double gpsLat, double gpsLon,
        double sinH, double cosH,
        ICoordinateService coordinateService)
    {
        var eastOffset = arX * cosH - arZ * sinH;
        var nordOffset = -arX * sinH - arZ * cosH;

        var (newLat, newLon, _) = coordinateService.LocalToLatLon(
            eastOffset, nordOffset, 0, gpsLat, gpsLon, 0);

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

    /// <summary>Laenge einer Polylinie in Metern (UTM-Meter-Koordinaten).
    /// Bei <paramref name="closed"/>=true wird die Schluss-Kante (letzter→erster Punkt) addiert.</summary>
    private static double CalculatePolylineLength(List<(double x, double y)> points, bool closed)
    {
        if (points.Count < 2) return 0;

        double length = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            var dx = points[i + 1].x - points[i].x;
            var dy = points[i + 1].y - points[i].y;
            length += Math.Sqrt(dx * dx + dy * dy);
        }
        if (closed && points.Count >= 3)
        {
            var dx = points[0].x - points[^1].x;
            var dy = points[0].y - points[^1].y;
            length += Math.Sqrt(dx * dx + dy * dy);
        }
        return length;
    }
}
