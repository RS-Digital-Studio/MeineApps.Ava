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

    /// <summary>
    /// Polygon/Linie persistiert als JSON. Zwei Formate:
    /// - v2 (neu, aktuell): {"v":2,"points":[[lat,lon],...]} — absolute WGS84 Lat/Lon.
    ///   Robust gegen Schwerpunkt-Drift wenn sich Messpunkte ändern.
    /// - v1 (legacy): [[x,y],...] — lokale UTM-Meter relativ zum damaligen Schwerpunkt.
    ///   Beim Laden wird versucht, sie als v1 mit aktuellem Schwerpunkt zu interpretieren
    ///   (Drift-Risiko). Nicht mehr für neue Elemente verwendet.
    /// </summary>
    public string PointsJson { get; set; } = "[]";

    /// <summary>Transient: Lokale Meter-Koordinaten relativ zum aktuellen Projekt-Schwerpunkt.
    /// Wird vom GardenPlanViewModel bei Projekt-Load oder Messpunkt-Änderung neu berechnet.
    /// Renderer und Export nutzen diese Cache statt PointsJson zu parsen.</summary>
    [Ignore]
    public List<(double x, double y)>? LocalPoints { get; set; }

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
    Terrasse,
    Grenze,
    Gebaeude,
    Wasser,
    Kante
}
