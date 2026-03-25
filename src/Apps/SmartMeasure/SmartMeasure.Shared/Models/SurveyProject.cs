using SQLite;

namespace SmartMeasure.Shared.Models;

/// <summary>Ein Vermessungsprojekt mit Messpunkten und Gartenelementen</summary>
public class SurveyProject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Projektname</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Typ: Grundstueck, Garten, Raum, Einzelmessung</summary>
    public string ProjectType { get; set; } = "Grundstueck";

    /// <summary>Optionale Notizen</summary>
    public string? Notes { get; set; }

    /// <summary>Erstelldatum (UTC)</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Letztes Aenderungsdatum (UTC)</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Berechnete Gesamtflaeche in m² (Shoelace)</summary>
    public double AreaSquareMeters { get; set; }

    /// <summary>Berechneter Umfang in m</summary>
    public double PerimeterMeters { get; set; }

    /// <summary>Anzahl Messpunkte</summary>
    public int PointCount { get; set; }

    // Nicht in DB - werden separat geladen
    [Ignore]
    public List<SurveyPoint> Points { get; set; } = [];

    [Ignore]
    public List<GardenElement> GardenElements { get; set; } = [];
}
