using System.Text.Json;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

/// <summary>Unit-Tests fuer ArTransferService. Fokus: Rotation, UTM-Projektion,
/// Geoid-Korrektur, GroundPlaneY-Anwendung, Geospatial-Fallback.
///
/// Realer CoordinateService + Egm96GeoidService werden verwendet — die sind
/// pure Funktionen ohne externe Abhaengigkeiten und passen ideal fuer Unit-Tests.
/// ProjectService und MeasurementService werden gemockt.</summary>
public class ArTransferServiceTests
{
    private const double Munich_Lat = 48.7758;
    private const double Munich_Lon = 9.1829;
    private const double Munich_AltEllipsoid = 568.0; // ~520 NN + 48 Geoid-Differenz

    private static (ArTransferService svc, IProjectService projects, IMeasurementService meas) MakeService()
    {
        var projects = Substitute.For<IProjectService>();
        projects.GetProjectAsync(Arg.Any<int>()).Returns(new SurveyProject { Id = 1 });
        var meas = Substitute.For<IMeasurementService>();
        var coord = new CoordinateService();
        var geoid = new Egm96GeoidService();
        return (new ArTransferService(projects, coord, meas, geoid), projects, meas);
    }

    private static ArCaptureResult MakeResult(
        float heading = 0f,
        float? groundPlaneY = null,
        bool withGeospatial = false)
    {
        return new ArCaptureResult
        {
            GpsLatitude = Munich_Lat,
            GpsLongitude = Munich_Lon,
            GpsAltitude = Munich_AltEllipsoid,
            GpsAccuracy = 3.5f,
            MagneticHeading = heading,
            GroundPlaneY = groundPlaneY,
            GeospatialActive = withGeospatial,
            StartedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public void ConvertToSurveyPoints_OhneGpsReferenz_GibtLeereListe()
    {
        var (svc, _, _) = MakeService();
        var result = new ArCaptureResult();

        var points = svc.ConvertToSurveyPoints(result, projectId: 1);

        points.Should().BeEmpty();
    }

    [Fact]
    public void ConvertToSurveyPoints_HeadingNord_ArZminus1_ZeigtNachNorden()
    {
        // Heading 0° = Blick nach Norden. ARCore -Z = vorne = Norden.
        // Erwartet: 1m Richtung Norden in WGS84 → Lat erhöht sich.
        var (svc, _, _) = MakeService();
        var result = MakeResult(heading: 0f);
        result.Points.Add(new ArPoint { X = 0, Y = 0, Z = -1 });

        var points = svc.ConvertToSurveyPoints(result, 1);

        points.Should().HaveCount(1);
        // 1m Norden ≈ +9e-6° Latitude in DE
        (points[0].Latitude - Munich_Lat).Should().BeApproximately(8.99e-6, 1e-7);
        // UTM-Reprojektion hat einen winzigen Ost-Drift im Sub-Mikro-Grad-Bereich
        // (1m Norden bei 9° E ist nicht exakt parallel zum Meridian). Toleranz 1e-6°.
        (points[0].Longitude - Munich_Lon).Should().BeApproximately(0, 1e-6);
    }

    [Fact]
    public void ConvertToSurveyPoints_HeadingOst_ArZminus1_ZeigtNachOsten()
    {
        // Heading 90° = Blick nach Osten. ARCore -Z = vorne = Osten.
        var (svc, _, _) = MakeService();
        var result = MakeResult(heading: 90f);
        result.Points.Add(new ArPoint { X = 0, Y = 0, Z = -1 });

        var points = svc.ConvertToSurveyPoints(result, 1);

        // 1m Osten in DE bei 48.78° N ≈ +1.36e-5° Longitude (lon-Grad ist kleiner als lat)
        (points[0].Latitude - Munich_Lat).Should().BeApproximately(0, 1e-7);
        (points[0].Longitude - Munich_Lon).Should().BeApproximately(1.36e-5, 1e-6);
    }

    [Fact]
    public void ConvertToSurveyPoints_GroundPlaneY_WirdAbgezogen()
    {
        // ARCore Y-Ursprung in 1.5m Augenhoehe → GroundPlaneY = -1.5.
        // Punkt bei Y=-1.45 ist 5cm ueber Boden — finale Hoehe sollte gpsAlt + 0.05 sein
        // (vor Geoid-Korrektur).
        var (svc, _, _) = MakeService();
        var result = MakeResult(groundPlaneY: -1.5f);
        result.Points.Add(new ArPoint { X = 0, Y = -1.45f, Z = 0 });

        var points = svc.ConvertToSurveyPoints(result, 1);

        // Geoid-Korrektur in Munich ~48m → Ellipsoid 568 + 0.05 - 48 = 520.05 NN
        points[0].Altitude.Should().BeApproximately(520.05, 0.6);
    }

    [Fact]
    public void ConvertToSurveyPoints_OhneGroundPlaneY_NutztYDirekt()
    {
        var (svc, _, _) = MakeService();
        var result = MakeResult(groundPlaneY: null);
        result.Points.Add(new ArPoint { X = 0, Y = 0.5f, Z = 0 });

        var points = svc.ConvertToSurveyPoints(result, 1);

        // Ohne GroundPlaneY-Offset: Hoehe = gpsAlt + 0.5 (Ellipsoid) → Geoid-korrigiert
        points[0].Altitude.Should().BeApproximately(520.5, 0.6);
    }

    [Fact]
    public void ConvertToSurveyPoints_MitGeospatialKoords_BevorzugtVPS()
    {
        // Wenn ArPoint.GeoLatitude/GeoLongitude gesetzt sind, soll der Service
        // diese DIREKT uebernehmen und nicht aus X/Z+Heading rechnen.
        var (svc, _, _) = MakeService();
        var result = MakeResult(heading: 0f);
        const double vpsLat = 48.8;
        const double vpsLon = 9.2;
        result.Points.Add(new ArPoint
        {
            X = 100, // sollte ignoriert werden weil VPS vorhanden
            Z = 100,
            Y = 0,
            GeoLatitude = vpsLat,
            GeoLongitude = vpsLon,
            GeoAltitude = 600,
            GeoHorizontalAccuracy = 2.0f,
        });

        var points = svc.ConvertToSurveyPoints(result, 1);

        points[0].Latitude.Should().Be(vpsLat);
        points[0].Longitude.Should().Be(vpsLon);
        // Geo-Accuracy in cm: 2m * 100
        points[0].HorizontalAccuracy.Should().Be(200f);
    }

    [Fact]
    public void ConvertToSurveyPoints_NutztUtmStatt111320Approximation()
    {
        // Bei 100m Distanz auf 48° N sollte UTM gegenueber 111320-Approximation
        // einen messbar besseren Wert liefern. Genauer: ueber 1000m muss der
        // UTM-Wert konsistent mit Haversine sein.
        var (svc, _, _) = MakeService();
        var result = MakeResult(heading: 0f);
        result.Points.Add(new ArPoint { X = 0, Y = 0, Z = -1000 }); // 1km Norden

        var points = svc.ConvertToSurveyPoints(result, 1);

        // Haversine-Distanz zwischen Ausgangspunkt und Ergebnis sollte ~1km sein.
        var coord = new CoordinateService();
        var distance = coord.HaversineDistance(Munich_Lat, Munich_Lon,
            points[0].Latitude, points[0].Longitude);

        // UTM ↔ Haversine haben modell-bedingt einen Restfehler (~30cm/km auf 48° N).
        // Wichtig: deutlich besser als 111320-Naeherung (die wuerde >5m abweichen) und
        // konsistent reproduzierbar.
        distance.Should().BeApproximately(1000.0, 0.5);
    }

    [Fact]
    public void ConvertToGardenElements_LeereKontur_WirdIgnoriert()
    {
        var (svc, _, _) = MakeService();
        var result = MakeResult();
        result.Contours.Add(new ArContour
        {
            ContourType = ArContourType.Beet,
            Points = [new ArPoint()], // nur 1 Punkt = keine Linie
        });

        var elements = svc.ConvertToGardenElements(result, 1);

        elements.Should().BeEmpty();
    }

    [Fact]
    public void ConvertToGardenElements_GeschlossenePolygon_RechnetFlaeche()
    {
        // 10×10m-Quadrat → Flaeche = 100 m²
        var (svc, _, _) = MakeService();
        var result = MakeResult(heading: 0f);
        result.Contours.Add(new ArContour
        {
            ContourType = ArContourType.Beet,
            IsClosed = true,
            Points = [
                new ArPoint { X = 0, Y = 0, Z = 0 },
                new ArPoint { X = 10, Y = 0, Z = 0 },
                new ArPoint { X = 10, Y = 0, Z = -10 },
                new ArPoint { X = 0, Y = 0, Z = -10 },
            ],
        });

        var elements = svc.ConvertToGardenElements(result, 1);

        elements.Should().HaveCount(1);
        elements[0].AreaSquareMeters.Should().BeApproximately(100.0, 0.01);
        // Polylinie ohne Schluss-Edge: 3 Kanten je 10m = 30m
        // Mit Schluss-Edge (IsClosed=true): 4 Kanten = 40m
        elements[0].LengthMeters.Should().BeApproximately(40.0, 0.01);
    }

    [Fact]
    public void ConvertToGardenElements_OffeneLinie_LaengeOhneSchlussEdge()
    {
        // 3 Punkte auf einer Linie: 0-10-20 → Laenge = 20m
        var (svc, _, _) = MakeService();
        var result = MakeResult(heading: 0f);
        result.Contours.Add(new ArContour
        {
            ContourType = ArContourType.Weg,
            IsClosed = false,
            Points = [
                new ArPoint { X = 0 },
                new ArPoint { X = 10 },
                new ArPoint { X = 20 },
            ],
        });

        var elements = svc.ConvertToGardenElements(result, 1);

        elements[0].AreaSquareMeters.Should().Be(0); // offene Kontur, keine Flaeche
        elements[0].LengthMeters.Should().BeApproximately(20.0, 0.01);
    }

    [Fact]
    public void ConvertToGardenElements_PointsJson_IstV2Format()
    {
        var (svc, _, _) = MakeService();
        var result = MakeResult();
        result.Contours.Add(new ArContour
        {
            ContourType = ArContourType.Beet,
            IsClosed = true,
            Points = [
                new ArPoint { X = 0, Z = 0 },
                new ArPoint { X = 1, Z = 0 },
                new ArPoint { X = 1, Z = -1 },
            ],
        });

        var elements = svc.ConvertToGardenElements(result, 1);

        elements[0].PointsJson.Should().Contain("\"v\":2");
        var parsed = JsonDocument.Parse(elements[0].PointsJson);
        parsed.RootElement.GetProperty("v").GetInt32().Should().Be(2);
        parsed.RootElement.GetProperty("points").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public void ConvertToSurveyPoints_VerticalAccuracyIst18xHorizontale()
    {
        // Plan Kap. 4.1 — VerticalAccuracy konservativ schlechter als horizontale
        var (svc, _, _) = MakeService();
        var result = MakeResult();
        result.Points.Add(new ArPoint());

        var points = svc.ConvertToSurveyPoints(result, 1);

        points[0].VerticalAccuracy.Should().BeApproximately(
            points[0].HorizontalAccuracy * 1.8f, 0.01f);
    }

    [Fact]
    public void ConvertToSurveyPoints_AndroidLocationSource_BehaeltMin50cmAccuracy()
    {
        // Bei Android-LocationManager-Quelle bleibt das 50cm-Minimum als Ceiling für
        // optimistisches GPS.
        var (svc, _, _) = MakeService();
        var result = MakeResult();
        result.GpsAccuracy = 0.001f; // unrealistisch genau, Android meldet sowas manchmal
        result.GpsSource = ArGpsSource.AndroidLocation;
        result.Points.Add(new ArPoint());

        var points = svc.ConvertToSurveyPoints(result, 1);

        points[0].HorizontalAccuracy.Should().BeGreaterThanOrEqualTo(50f);
    }

    [Fact]
    public async Task TransferToProjectAsync_OhneGpsReferenz_NutztFallbackUrsprung()
    {
        // Frueher warf das ohne GPS-Referenz — seit dem AR-First-Umbau wird stattdessen ein
        // Fallback-Ursprung gesetzt (Schwerpunkt vorhandener Projektpunkte, sonst DE-Default),
        // damit die translation-invariante Messung (Distanzen/Flaechen) NICHT verloren geht.
        // Nur die absolute Karten-Lage ist dann ein Platzhalter.
        var (svc, projects, meas) = MakeService();
        var result = new ArCaptureResult(); // kein GPS
        result.Points.Add(new ArPoint { X = 0, Y = 0, Z = -1 });

        var count = await svc.TransferToProjectAsync(result, 1);

        count.Should().Be(1);                          // Punkt wurde uebertragen, kein Wurf
        result.HasGpsReference.Should().BeTrue();      // Fallback-Ursprung wurde gesetzt
        result.GpsLatitude.Should().NotBeNull();
        result.GpsLongitude.Should().NotBeNull();
        await projects.Received().AddPointAsync(1, Arg.Any<SurveyPoint>());
        meas.Received().AddPoint(Arg.Any<SurveyPoint>());
    }

    [Fact]
    public async Task TransferToProjectAsync_PunktDbFehler_FuegtNichtZuMeasurement()
    {
        // Wenn DB-Insert kracht, soll der Punkt NICHT in MeasurementService landen
        // (sonst inkonsistent: User sieht Punkt in Liste, aber DB hat ihn nicht).
        var (svc, projects, meas) = MakeService();
        projects.AddPointAsync(Arg.Any<int>(), Arg.Any<SurveyPoint>())
            .Returns(Task.FromException(new Exception("DB voll")));

        var result = MakeResult();
        result.Points.Add(new ArPoint());

        var count = await svc.TransferToProjectAsync(result, 1);

        count.Should().Be(0);
        meas.DidNotReceive().AddPoint(Arg.Any<SurveyPoint>());
    }
}
