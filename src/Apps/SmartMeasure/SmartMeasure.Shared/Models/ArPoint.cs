namespace SmartMeasure.Shared.Models;

/// <summary>Ein 3D-Punkt aus der AR-Kamera-Erfassung (lokale Meter-Koordinaten)</summary>
public class ArPoint
{
    /// <summary>Position X in Metern (lokal, relativ zum AR-Session-Start, rechts positiv)</summary>
    public float X { get; set; }

    /// <summary>Position Y in Metern (lokal, relativ zum AR-Session-Start, oben positiv)</summary>
    public float Y { get; set; }

    /// <summary>Position Z in Metern (lokal, relativ zum AR-Session-Start, nach hinten positiv)</summary>
    public float Z { get; set; }

    /// <summary>ARCore Anchor-ID (fuer stabile Positionierung in der Welt)</summary>
    public string? AnchorId { get; set; }

    /// <summary>
    /// Konfidenz (0.0 - 1.0). Setzt sich zusammen aus: Hit-Quality (Plane/Point/Instant),
    /// Anchor-Tracking-Quality, Depth-Agreement, Multi-Frame-Variance, Stability-Score.
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>Standardabweichung der Multi-Frame-Averaging-Samples in Metern (kleiner = besser).</summary>
    public float PositionStdDev { get; set; }

    /// <summary>Anzahl Samples die zum Punkt-Averaging beigetragen haben (typisch 5-10).</summary>
    public int SampleCount { get; set; }

    /// <summary>
    /// Hit-Qualität beim Setzen: 3=Plane, 2=Feature-Point, 1=Instant Placement, 0=kein Hit.
    /// Gibt Aufschluss ob Punkt drift-stabil ist (3) oder nur geschätzt (1).
    /// </summary>
    public int HitQuality { get; set; }

    /// <summary>Optionales Label ("Ecke Terrasse", "Beetkante")</summary>
    public string? Label { get; set; }

    /// <summary>
    /// Absolute WGS84-Latitude direkt aus ARCore Geospatial-API (nur wenn Earth-Tracking aktiv war).
    /// Präziser als manuelle Konvertierung aus X/Z+Heading, weil VPS-gestützt (±1-3m).
    /// </summary>
    public double? GeoLatitude { get; set; }

    /// <summary>Absolute WGS84-Longitude aus ARCore Geospatial-API.</summary>
    public double? GeoLongitude { get; set; }

    /// <summary>Absolute Höhe über WGS84-Ellipsoid in Metern aus ARCore Geospatial-API.</summary>
    public double? GeoAltitude { get; set; }

    /// <summary>Horizontale Genauigkeit der Geo-Position in Metern (beim Capture-Zeitpunkt).</summary>
    public float? GeoHorizontalAccuracy { get; set; }

    /// <summary>
    /// Kamera-Pitch in Grad zum Capture-Zeitpunkt (0 = horizontal, +90 = nach oben, -90 = nach unten).
    /// Wird aus ARCore-Camera-Pose extrahiert und ist nicht der RTK-Stab-Tilt — hier zählt die
    /// Phone-Orientierung, weil sie die Mess-Genauigkeit beeinflusst (steiler Pitch → größerer Depth-Fehler).
    /// </summary>
    public float CameraPitchDeg { get; set; }

    /// <summary>
    /// Android-Magnetometer-Accuracy beim Capture (0=unreliable, 1=low, 2=medium, 3=high).
    /// Wandert nach SurveyPoint.MagAccuracy — relevant für Heading-Vertrauen bei späterer Tilt-Korrektur.
    /// </summary>
    public int MagAccuracyAtCapture { get; set; }

    /// <summary>
    /// Anteil der Sampling-Frames in denen ARCore Tracking hatte (0..1).
    /// 1.0 = perfektes Tracking durchgängig, ~0.5 = die Hälfte verlor Tracking, &lt;0.5 = verworfen.
    /// Wird in FinalizeSampling pro Punkt berechnet — separat vom Session-Median, damit
    /// einzelne wackelige Messungen sichtbar werden.
    /// </summary>
    public float SampleTrackingContinuity { get; set; } = 1f;

    /// <summary>Zeitpunkt der Erfassung (UTC)</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Euklidischer Abstand zu einem anderen Punkt in Metern</summary>
    public float DistanceTo(ArPoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>2D-Abstand (nur X/Z, ohne Hoehe) in Metern</summary>
    public float Distance2DTo(ArPoint other)
    {
        var dx = X - other.X;
        var dz = Z - other.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
