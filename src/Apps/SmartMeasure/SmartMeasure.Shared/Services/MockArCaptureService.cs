using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Desktop-Mock fuer AR-Capture (simuliert ein kleines Grundstueck).
/// Erzeugt deterministische Daten (Seed=42) und kann beide Pfade des
/// <see cref="ArTransferService"/> bedienen: Heading-Fallback und VPS-Geospatial
/// pro Punkt — abhaengig von <see cref="SimulateGeospatial"/>.</summary>
public class MockArCaptureService : IArCaptureService
{
    /// <summary>Simulierter Ground-Plane-Y-Offset: ARCore-Session startet typisch
    /// in ~1.5 m Augenhöhe. Echte Welt-Höhe = ArPoint.Y - GroundPlaneY.</summary>
    public float SimulatedGroundPlaneY { get; set; } = -1.5f;

    /// <summary>Wenn true: pro ArPoint werden auch GeoLatitude/GeoLongitude/GeoAltitude/Acc
    /// gesetzt (simuliert ARCore Geospatial-API / VPS aktiv).</summary>
    public bool SimulateGeospatial { get; set; }

    /// <summary>Wenn true: ein Punkt bekommt absichtlich eine schlechte HitQuality + hohe
    /// StdDev, damit die Confidence-Pipeline auch unter realistischen Bedingungen läuft.</summary>
    public bool SimulateNoisyPoint { get; set; }

    /// <summary>Simulierte Tracking-Quality (0..100). Default 95.</summary>
    public int TrackingQualityScore { get; set; } = 95;

    /// <summary>Anteil der Frames im Tracking-State (0..1). Default 0.97.</summary>
    public float TrackingContinuityRatio { get; set; } = 0.97f;

    /// <summary>Status der letzten Mock-Capture-Operation. Mock simuliert weder Abbruch noch
    /// Fehler — Wert ist <see cref="ArCaptureCompletionStatus.Success"/> nach jedem
    /// <see cref="CaptureAsync"/>-Lauf.</summary>
    public ArCaptureCompletionStatus LastCompletionStatus { get; private set; }

    /// <summary>Mock liefert nie einen Fehler. Plan Kap. 4.3.</summary>
    public string? LastError => null;

    /// <summary>Mock zeigt keinen AR-Pfeil — Stakeout-Targets werden ignoriert.
    /// Plan Kap. 5.9.</summary>
    public void SetStakeoutTargets(IReadOnlyList<StakeoutTarget>? targets) { /* no-op */ }

    /// <summary>Mock hat keine Earth-Anchors — Site-Points werden ignoriert. Plan Kap. 5.2.</summary>
    public void SetSitePoints(IReadOnlyList<SurveyPoint>? points) { /* no-op */ }

    /// <summary>Mock hat keine AR-Session — Vorlade-Punkte werden ignoriert.</summary>
    public void SetPreloadPoints(IReadOnlyList<SurveyPoint>? points) { /* no-op */ }

    /// <summary>Mock hat keine ARCore-Augmented-Images — Marker werden ignoriert. Plan Kap. 5.7.</summary>
    public void SetReferenceMarkers(IReadOnlyList<ArReferenceMarker>? markers) { /* no-op */ }

    public Task<bool> IsAvailableAsync() => Task.FromResult(true);

    public async Task<ArCaptureResult?> CaptureAsync()
    {
        // Simuliere kurze Verzoegerung wie eine echte AR-Session
        await Task.Delay(500);
        LastCompletionStatus = ArCaptureCompletionStatus.Success;

        var random = new Random(42);
        const double gpsLat = 48.7758;
        const double gpsLon = 9.1829;
        const double gpsAltEllipsoid = 568.0;       // ~520 NN + ~48 Geoid-Differenz in DE
        const float magneticHeading = 15f;          // leicht nach Osten verdreht

        var result = new ArCaptureResult
        {
            StartedAt = DateTime.UtcNow,
            SessionDuration = TimeSpan.FromMinutes(5),
            // Mock simuliert Handy-GPS (kein RTK-Stab) — die Standard-AR-ohne-Hardware-Quelle.
            GpsSource = ArGpsSource.AndroidLocation,
            GpsLatitude = gpsLat,
            GpsLongitude = gpsLon,
            GpsAltitude = gpsAltEllipsoid,
            GpsAccuracy = 3.5f,
            MagneticHeading = magneticHeading,
            BarometricAltitude = 520.5f,
            GroundPlaneY = SimulatedGroundPlaneY,
            TrackingQualityScore = TrackingQualityScore,
            TrackingContinuityRatio = TrackingContinuityRatio,
            GeospatialActive = SimulateGeospatial,
            GeospatialHorizontalAccuracy = SimulateGeospatial ? 1.8f : null,
            GeospatialHeadingAccuracy = SimulateGeospatial ? 6.5f : null,
        };

        // Eckpunkte des Grundstuecks (12x8m)
        var corners = new[]
        {
            (x: 0f, z: 0f),
            (x: 12f, z: 0f),
            (x: 12f, z: 8f),
            (x: 0f, z: 8f),
        };

        // Y-Werte sind relativ zum Session-Ursprung (Augenhöhe).
        // Bodennähe = SimulatedGroundPlaneY + kleine Variation.
        foreach (var (x, z) in corners)
        {
            var yLocal = SimulatedGroundPlaneY + (float)(random.NextDouble() * 0.3 - 0.05);
            var p = new ArPoint
            {
                X = x,
                Y = yLocal,
                Z = z,
                Confidence = 0.85f + (float)(random.NextDouble() * 0.15),
                HitQuality = 3,
                SampleCount = 10,
                PositionStdDev = 0.008f + (float)(random.NextDouble() * 0.005),
                Label = $"Ecke ({x:F0}/{z:F0})",
            };
            ApplyGeospatial(p, gpsLat, gpsLon, gpsAltEllipsoid, magneticHeading);
            result.Points.Add(p);
        }

        // Zusaetzliche Zwischenpunkte
        for (var i = 0; i < 6; i++)
        {
            var x = 1f + (float)(random.NextDouble() * 10);
            var z = 1f + (float)(random.NextDouble() * 6);
            var p = new ArPoint
            {
                X = x,
                Y = SimulatedGroundPlaneY + (float)(random.NextDouble() * 0.4 - 0.1),
                Z = z,
                Confidence = 0.7f + (float)(random.NextDouble() * 0.3),
                HitQuality = 2,
                SampleCount = 8,
                PositionStdDev = 0.012f,
            };
            ApplyGeospatial(p, gpsLat, gpsLon, gpsAltEllipsoid, magneticHeading);
            result.Points.Add(p);
        }

        if (SimulateNoisyPoint)
        {
            var noisy = new ArPoint
            {
                X = 6f,
                Y = SimulatedGroundPlaneY + 0.05f,
                Z = 4f,
                Confidence = 0.4f,
                HitQuality = 1,
                SampleCount = 4,
                PositionStdDev = 0.045f,
                Label = "Noisy",
            };
            ApplyGeospatial(noisy, gpsLat, gpsLon, gpsAltEllipsoid, magneticHeading);
            result.Points.Add(noisy);
        }

        // Beispiel-Kontur: Weg (Linie)
        var weg = new ArContour
        {
            ContourType = ArContourType.Weg,
            Label = "Hauptweg",
            IsClosed = false,
        };
        foreach (var (x, z) in new[] { (0f, 4f), (6f, 4f), (12f, 4f) })
        {
            var p = new ArPoint
            {
                X = x,
                Y = SimulatedGroundPlaneY + (float)(random.NextDouble() * 0.1),
                Z = z,
                Confidence = 0.9f,
                HitQuality = 3,
                SampleCount = 10,
            };
            ApplyGeospatial(p, gpsLat, gpsLon, gpsAltEllipsoid, magneticHeading);
            weg.Points.Add(p);
        }
        result.Contours.Add(weg);

        // Beispiel-Kontur: Beet (geschlossenes Polygon)
        var beet = new ArContour
        {
            ContourType = ArContourType.Beet,
            Label = "Gemuesebeet",
            IsClosed = true,
        };
        foreach (var (x, z) in new[] { (2f, 1f), (5f, 1f), (5f, 3f), (2f, 3f) })
        {
            var p = new ArPoint
            {
                X = x,
                Y = SimulatedGroundPlaneY + (float)(random.NextDouble() * 0.07),
                Z = z,
                Confidence = 0.85f,
                HitQuality = 3,
                SampleCount = 10,
            };
            ApplyGeospatial(p, gpsLat, gpsLon, gpsAltEllipsoid, magneticHeading);
            beet.Points.Add(p);
        }
        result.Contours.Add(beet);

        return result;
    }

    /// <summary>Setzt Geospatial-Felder auf einem ArPoint passend zu lokalen X/Z + Heading,
    /// damit der VPS-Pfad in ArTransferService getestet werden kann. Verwendet dieselbe
    /// Rotations-Formel wie ArTransferService (East/North aus arX/arZ + Heading) und eine
    /// kleine Naehrung für Lat/Lon-Offset.</summary>
    private void ApplyGeospatial(ArPoint p, double gpsLat, double gpsLon,
        double gpsAltEllipsoid, float headingDeg)
    {
        if (!SimulateGeospatial) return;

        var headingRad = headingDeg * Math.PI / 180.0;
        var sinH = Math.Sin(headingRad);
        var cosH = Math.Cos(headingRad);

        // Gleiche Formel wie ArTransferService.RotateAndProject
        var east = p.X * cosH - p.Z * sinH;
        var north = -p.X * sinH - p.Z * cosH;

        // Lokaler Meter→Grad-Konversion (Mock-Vereinfachung; echte Hardware nutzt VPS direkt)
        const double metersPerDegreeLat = 111320.0;
        var metersPerDegreeLon = metersPerDegreeLat * Math.Cos(gpsLat * Math.PI / 180.0);

        p.GeoLatitude = gpsLat + north / metersPerDegreeLat;
        p.GeoLongitude = gpsLon + east / metersPerDegreeLon;
        p.GeoAltitude = gpsAltEllipsoid + (p.Y - SimulatedGroundPlaneY);
        p.GeoHorizontalAccuracy = 1.5f;
    }
}
