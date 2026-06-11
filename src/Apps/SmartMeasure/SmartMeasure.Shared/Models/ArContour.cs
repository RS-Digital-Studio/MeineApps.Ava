namespace SmartMeasure.Shared.Models;

/// <summary>Eine Konturlinie aus der AR-Erfassung (verbundene ArPoints)</summary>
public class ArContour
{
    /// <summary>Punkte der Kontur (in Reihenfolge)</summary>
    public List<ArPoint> Points { get; set; } = [];

    /// <summary>Typ der Kontur</summary>
    public ArContourType ContourType { get; set; } = ArContourType.Grenze;

    /// <summary>Optionales Label</summary>
    public string? Label { get; set; }

    /// <summary>Ist die Kontur geschlossen (letzter Punkt verbindet mit erstem)?</summary>
    public bool IsClosed { get; set; }

    /// <summary>Gesamtlaenge der Kontur in Metern als 3D-SCHRAEGDISTANZ (inkl. Hoehenanteil).
    /// Fuer Plan-Masse <see cref="CalculateHorizontalLength"/> verwenden — App-weit ist die
    /// Horizontaldistanz die kanonische Laenge (Segment-Pillen, persistiertes
    /// GardenElement.LengthMeters, Live-Footer).</summary>
    public float CalculateLength()
    {
        if (Points.Count < 2) return 0;

        var length = 0f;
        for (var i = 1; i < Points.Count; i++)
            length += Points[i].DistanceTo(Points[i - 1]);

        if (IsClosed && Points.Count > 2)
            length += Points[^1].DistanceTo(Points[0]);

        return length;
    }

    /// <summary>Gesamtlaenge der Kontur in Metern als HORIZONTALDISTANZ (Grundriss, X/Z) —
    /// die kanonische Laenge fuer Gartenplan-Masse. Am Hang weicht sie von der
    /// 3D-Schraegdistanz ab (20 % Steigung ≈ 2 %).</summary>
    public float CalculateHorizontalLength()
    {
        if (Points.Count < 2) return 0;

        var length = 0f;
        for (var i = 1; i < Points.Count; i++)
            length += Points[i].Distance2DTo(Points[i - 1]);

        if (IsClosed && Points.Count > 2)
            length += Points[^1].Distance2DTo(Points[0]);

        return length;
    }

    /// <summary>Flaeche der Kontur in m² (nur bei geschlossenen Konturen, Shoelace auf X/Z-Ebene)</summary>
    public float CalculateArea()
    {
        if (!IsClosed || Points.Count < 3) return 0;

        var area = 0f;
        for (var i = 0; i < Points.Count; i++)
        {
            var j = (i + 1) % Points.Count;
            area += Points[i].X * Points[j].Z;
            area -= Points[j].X * Points[i].Z;
        }

        return MathF.Abs(area) / 2f;
    }
}

/// <summary>Typen von AR-Konturen</summary>
public enum ArContourType
{
    Grenze,
    Weg,
    Beet,
    Mauer,
    Zaun,
    Terrasse,
    Gebaeude,
    Wasser,
    Kante
}
