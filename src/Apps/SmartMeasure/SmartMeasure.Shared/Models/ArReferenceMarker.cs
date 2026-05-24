using SQLite;

namespace SmartMeasure.Shared.Models;

/// <summary>Plan-Kap. 5.7: Ein vorab eingemessener Referenz-Marker (z.B. ArUco, 10x10 cm
/// gedruckt auf wasserfeste Folie). Sobald die AR-Session den Marker via Augmented-Images-
/// API erkennt, kann sie sich instantan im Vermessungs-Koordinatensystem ausrichten —
/// unabhaengig vom RTK-Stab und ohne Geospatial-API-Coverage. Per Projekt mehrere
/// Marker moeglich.</summary>
public class ArReferenceMarker
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Projekt-Zuordnung (analog SurveyPoint).</summary>
    public int ProjectId { get; set; }

    /// <summary>Anzeige-Name z.B. "Marker NW-Ecke". Wird im AR-Overlay angezeigt sobald
    /// der Marker im Sichtfeld ist.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Eindeutige ARCore-AugmentedImageDatabase-ID. Muss zur Asset-Datei passen.</summary>
    public string ImageAssetName { get; set; } = string.Empty;

    /// <summary>Physische Breite des Markers in Metern (typisch 0.10 fuer A4-1/10).</summary>
    public float WidthMeters { get; set; } = 0.10f;

    /// <summary>Mit RTK-Stab eingemessene Position des Marker-Zentrums (WGS84).</summary>
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }

    /// <summary>Genauigkeit beim Einmessen in cm — bei RTK-Fix typisch 2cm.</summary>
    public float AccuracyCm { get; set; }

    /// <summary>Zeitpunkt der initialen Einmessung (UTC).</summary>
    public DateTime CalibratedAt { get; set; }
}
