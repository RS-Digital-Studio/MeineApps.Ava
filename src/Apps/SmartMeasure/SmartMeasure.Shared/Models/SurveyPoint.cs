using SQLite;

namespace SmartMeasure.Shared.Models;

/// <summary>Ein vermessener Punkt mit RTK-GPS Position und Stab-Neigung</summary>
public class SurveyPoint
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Projekt-Zuordnung</summary>
    public int ProjectId { get; set; }

    /// <summary>Breitengrad (WGS84, Boden-korrigiert)</summary>
    public double Latitude { get; set; }

    /// <summary>Laengengrad (WGS84, Boden-korrigiert)</summary>
    public double Longitude { get; set; }

    /// <summary>Hoehe ueber NN in Metern (hMSL, Geoid-korrigiert, Boden-korrigiert)</summary>
    public double Altitude { get; set; }

    /// <summary>Horizontale Genauigkeit in cm</summary>
    public float HorizontalAccuracy { get; set; }

    /// <summary>Vertikale Genauigkeit in cm</summary>
    public float VerticalAccuracy { get; set; }

    /// <summary>Stab-Neigung beim Messen in Grad (vom Lot)</summary>
    public float TiltAngle { get; set; }

    /// <summary>Kompass-Richtung der Neigung in Grad (true north)</summary>
    public float TiltAzimuth { get; set; }

    /// <summary>RTK Fix-Quality (0=NoFix, 1=GPS, 2=DGPS, 4=RTK-Fix, 5=RTK-Float)</summary>
    public int FixQuality { get; set; }

    /// <summary>Anzahl sichtbare Satelliten</summary>
    public int SatelliteCount { get; set; }

    /// <summary>BNO085 Magnetometer-Accuracy (0-3, >= 2 fuer Horizontal-Korrektur)</summary>
    public int MagAccuracy { get; set; }

    /// <summary>Zeitpunkt der Messung (UTC)</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Optionales Label ("Ecke Terrasse NW", "Grenzpunkt")</summary>
    public string? Label { get; set; }
}
