using SQLite;

namespace SmartMeasure.Shared.Models;

/// <summary>Ein Gartenelement (Weg, Beet, Mauer, Terrasse) auf dem Grundstueck</summary>
public class GardenElement
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Projekt-Zuordnung</summary>
    public int ProjectId { get; set; }

    /// <summary>Typ: Weg, Beet, Rasen, Mauer, Zaun, Terrasse</summary>
    public GardenElementType ElementType { get; set; }

    /// <summary>Polygon/Linie als JSON-String (List von (x,y) in UTM-Meter)</summary>
    public string PointsJson { get; set; } = "[]";

    /// <summary>Breite in Metern (fuer Wege)</summary>
    public float Width { get; set; }

    /// <summary>Hoehe in Metern (fuer Mauern)</summary>
    public float Height { get; set; }

    /// <summary>Zielhoehe ueber NN (fuer Terrassen - Aufschuettung/Abtrag)</summary>
    public double TargetAltitude { get; set; }

    /// <summary>Material (Pflaster, Kies, Naturstein, Beton, Holz, etc.)</summary>
    public string Material { get; set; } = string.Empty;

    /// <summary>Untertyp (Gemuesebeet, Blumenbeet, Rasen, Wildblumen)</summary>
    public string SubType { get; set; } = string.Empty;

    /// <summary>Schichtdicke in cm (fuer Materialliste)</summary>
    public float LayerThicknessCm { get; set; } = 20;

    /// <summary>Berechnete Flaeche in m²</summary>
    public double AreaSquareMeters { get; set; }

    /// <summary>Berechnete Laenge in m (fuer Wege, Mauern)</summary>
    public double LengthMeters { get; set; }

    /// <summary>Berechnetes Volumen in m³ (fuer Terrassen: Aufschuettung/Abtrag)</summary>
    public double VolumeMeters { get; set; }

    /// <summary>Optionale Notiz</summary>
    public string? Notes { get; set; }

    /// <summary>Reihenfolge fuer Undo/Redo</summary>
    public int SortOrder { get; set; }
}

/// <summary>Typen von Gartenelementen</summary>
public enum GardenElementType
{
    Weg,
    Beet,
    Rasen,
    Mauer,
    Zaun,
    Terrasse
}
