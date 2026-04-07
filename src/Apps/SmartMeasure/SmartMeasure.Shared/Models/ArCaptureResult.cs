namespace SmartMeasure.Shared.Models;

/// <summary>Ergebnis einer AR-Capture-Session (Punkte + Konturen + Metadaten)</summary>
public class ArCaptureResult
{
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

    /// <summary>Zeitpunkt des Session-Starts (UTC)</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>Hat die Session gueltige GPS-Daten fuer Georeferenzierung?</summary>
    public bool HasGpsReference => GpsLatitude.HasValue && GpsLongitude.HasValue;

    /// <summary>Gesamtanzahl aller Punkte (Einzel + Kontur-Punkte)</summary>
    public int TotalPointCount => Points.Count + Contours.Sum(c => c.Points.Count);
}
