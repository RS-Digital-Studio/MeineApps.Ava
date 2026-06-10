namespace SmartMeasure.Shared.Models;

/// <summary>Herkunft der GPS-Referenz einer AR-Capture-Session.</summary>
public enum ArGpsSource
{
    /// <summary>Keine GPS-Referenz erfasst.</summary>
    None = 0,
    /// <summary>Android-LocationManager (Handy-GPS, typisch ±3–8m).</summary>
    AndroidLocation = 1,
}

/// <summary>Ergebnis einer AR-Capture-Session (Punkte + Konturen + Metadaten)</summary>
public class ArCaptureResult
{
    /// <summary>Quelle der GPS-Referenz — relevant für Accuracy-Berechnung in ArTransferService.</summary>
    public ArGpsSource GpsSource { get; set; } = ArGpsSource.None;

    /// <summary>Alle gesetzten Einzelpunkte</summary>
    public List<ArPoint> Points { get; set; } = [];

    /// <summary>Alle gezeichneten Konturen</summary>
    public List<ArContour> Contours { get; set; } = [];

    /// <summary>GPS-Ankerposition zum Zeitpunkt des Session-Starts (fuer Georeferenzierung)</summary>
    public double? GpsLatitude { get; set; }

    /// <summary>GPS-Ankerposition zum Zeitpunkt des Session-Starts</summary>
    public double? GpsLongitude { get; set; }

    /// <summary>GPS-Hoehe zum Zeitpunkt des Session-Starts</summary>
    public double? GpsAltitude { get; set; }

    /// <summary>GPS-Genauigkeit in Metern zum Zeitpunkt des Session-Starts</summary>
    public float? GpsAccuracy { get; set; }

    /// <summary>Kompass-Heading (Nordrichtung) zum Session-Start in Grad (0-360)</summary>
    public float? MagneticHeading { get; set; }

    /// <summary>Barometrische Hoehe zum Session-Start in Metern</summary>
    public float? BarometricAltitude { get; set; }

    /// <summary>Dauer der AR-Session</summary>
    public TimeSpan SessionDuration { get; set; }

    /// <summary>
    /// Y-Wert der Ground-Plane in ARCore-Welt (lokale Höhe des Bodens).
    /// Wenn gesetzt → alle ArPoint.Y-Werte werden nachträglich relativ zum Boden interpretiert.
    /// Hilft bei absoluter Höhen-Messung unabhängig von der Startposition der Kamera.
    /// </summary>
    public float? GroundPlaneY { get; set; }

    /// <summary>Tracking-Quality-Score der Session (0-100).</summary>
    public int TrackingQualityScore { get; set; } = 100;

    /// <summary>
    /// Anzahl Frames im Tracking-State über die gesamte Session-Dauer.
    /// Wenn &lt; 80% der Total-Frames → Session war unsicher, Präzision reduziert.
    /// </summary>
    public float TrackingContinuityRatio { get; set; } = 1f;

    /// <summary>
    /// War Geospatial-Tracking (VPS) während der Session aktiv? Wenn true sind
    /// die Lat/Lon/Alt-Werte aus ARCore-Earth-API (±1-3m) statt Handy-GPS (±5m).
    /// </summary>
    public bool GeospatialActive { get; set; }

    /// <summary>
    /// Horizontale Genauigkeit des Geospatial-Trackings in Metern (Median über Session).
    /// Typisch 1-3m in urbanen Gebieten, 5-20m auf dem Land.
    /// </summary>
    public float? GeospatialHorizontalAccuracy { get; set; }

    /// <summary>
    /// Heading-Genauigkeit des Geospatial-Trackings in Grad (Median über Session).
    /// Typisch 5-10° mit VPS — Magnetometer-Alternative wäre 15-30° in Metallumgebung.
    /// </summary>
    public float? GeospatialHeadingAccuracy { get; set; }

    /// <summary>Zeitpunkt des Session-Starts (UTC)</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>Hat die Session gueltige GPS-Daten fuer Georeferenzierung?</summary>
    public bool HasGpsReference => GpsLatitude.HasValue && GpsLongitude.HasValue;

    /// <summary>Gesamtanzahl aller Punkte (Einzel + Kontur-Punkte)</summary>
    public int TotalPointCount => Points.Count + Contours.Sum(c => c.Points.Count);
}
