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

    /// <summary>
    /// Mess-Konfidenz 0..1 (1 = sehr sicher). Bei AR-Punkten die echte ARCore-Confidence
    /// aus Hit-Quality, Multi-Frame-Streuung und Tracking-Stabilitaet (siehe <c>ArPoint.Confidence</c>).
    /// Bei RTK-Stab-Punkten 1.0 (cm-genau). 0 = unbekannt/nicht gesetzt. Wird in der Punkte-Liste
    /// und im PDF-Bericht angezeigt, damit der Nutzer den Wert seiner Messung einschaetzen kann.
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>Zeitpunkt der Messung (UTC)</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Optionales Label ("Ecke Terrasse NW", "Grenzpunkt")</summary>
    public string? Label { get; set; }

    /// <summary>Plan-Kap. 5.6: Relativer Pfad zum JPEG-Foto vom Capture-Zeitpunkt
    /// (relativ zum <c>IAppPaths.PhotosFolder</c>). Wird beim AR-Capture mit dem
    /// aktuellen Kamera-Frame befuellt, sonst null. PDF-Bericht laedt das Foto
    /// neben dem Punkt-Eintrag.</summary>
    public string? PhotoPath { get; set; }

    /// <summary>Plan-Kap. 5.12: Transkript einer Sprach-Annotation (gesprochen vom User
    /// beim Punkt-Setzen). null wenn keine Audio-Erkennung lief.</summary>
    public string? VoiceTranscript { get; set; }

    /// <summary>Plan-Kap. 5.12: Dateiname der Audio-Aufnahme (WAV/MP3), relativ zum
    /// <c>IAppPaths.PhotosFolder</c>. null wenn keine Aufnahme.</summary>
    public string? VoiceAudioPath { get; set; }
}
