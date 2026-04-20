using System.Text.Json;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Gartenelemente + Materialliste</summary>
public class GardenPlanService : IGardenPlanService
{
    // Standard-Schichtdicken pro Material (cm)
    private static readonly Dictionary<string, float> DefaultThicknesses = new()
    {
        ["Pflaster"] = 13f, // 5cm Bettung + 8cm Stein
        ["Kies"] = 20f,
        ["Naturstein"] = 15f,
        ["Beton"] = 15f,
        ["Holz"] = 3f,      // Terrassendielen
        ["Erde"] = 30f,
        ["Rasen"] = 5f       // Rollrasen/Aussaat
    };

    public List<MaterialEstimate> CalculateMaterials(List<GardenElement> elements)
    {
        var estimates = new List<MaterialEstimate>();

        foreach (var element in elements)
        {
            var points = ParsePoints(element.PointsJson);
            if (points.Count < 2) continue;

            switch (element.ElementType)
            {
                case GardenElementType.Weg:
                    var wegLength = CalculatePolylineLength(points);
                    var wegArea = wegLength * element.Width;
                    var wegThickness = element.LayerThicknessCm > 0
                        ? element.LayerThicknessCm
                        : GetDefaultThickness(element.Material);
                    estimates.Add(new MaterialEstimate
                    {
                        Material = $"{element.Material} ({element.SubType})",
                        Quantity = wegArea,
                        Unit = "m²",
                        ElementName = $"Weg {element.Material}"
                    });
                    estimates.Add(new MaterialEstimate
                    {
                        Material = $"Unterbau {element.Material}",
                        Quantity = wegArea * wegThickness / 100.0,
                        Unit = "m³",
                        ElementName = $"Weg {element.Material}"
                    });
                    break;

                case GardenElementType.Beet:
                case GardenElementType.Rasen:
                    var beetArea = CalculatePolygonArea(points);
                    var beetMaterial = element.ElementType == GardenElementType.Rasen ? "Rasen" : "Erde";
                    estimates.Add(new MaterialEstimate
                    {
                        Material = $"{beetMaterial} ({element.SubType})",
                        Quantity = beetArea,
                        Unit = "m²",
                        ElementName = $"{element.ElementType} {element.SubType}"
                    });
                    if (element.ElementType == GardenElementType.Beet)
                    {
                        var erdeDicke = element.LayerThicknessCm > 0
                            ? element.LayerThicknessCm : 30f;
                        estimates.Add(new MaterialEstimate
                        {
                            Material = "Muttererde",
                            Quantity = beetArea * erdeDicke / 100.0,
                            Unit = "m³",
                            ElementName = $"Beet {element.SubType}"
                        });
                    }
                    break;

                case GardenElementType.Mauer:
                case GardenElementType.Zaun:
                    var mauerLength = CalculatePolylineLength(points);
                    estimates.Add(new MaterialEstimate
                    {
                        Material = $"{element.Material} {element.ElementType}",
                        Quantity = mauerLength,
                        Unit = "lfm",
                        ElementName = $"{element.ElementType} {element.Material}"
                    });
                    if (element.Height > 0)
                    {
                        estimates.Add(new MaterialEstimate
                        {
                            Material = $"{element.Material} Fläche",
                            Quantity = mauerLength * element.Height,
                            Unit = "m²",
                            ElementName = $"{element.ElementType} {element.Material}"
                        });
                    }
                    break;

                case GardenElementType.Terrasse:
                    var terrasseArea = CalculatePolygonArea(points);
                    var terraDicke = element.LayerThicknessCm > 0
                        ? element.LayerThicknessCm : GetDefaultThickness(element.Material);
                    estimates.Add(new MaterialEstimate
                    {
                        Material = $"{element.Material} Terrasse",
                        Quantity = terrasseArea,
                        Unit = "m²",
                        ElementName = $"Terrasse {element.Material}"
                    });
                    estimates.Add(new MaterialEstimate
                    {
                        Material = $"Unterbau Terrasse",
                        Quantity = terrasseArea * terraDicke / 100.0,
                        Unit = "m³",
                        ElementName = $"Terrasse {element.Material}"
                    });
                    if (Math.Abs(element.VolumeMeters) > 0.001)
                    {
                        estimates.Add(new MaterialEstimate
                        {
                            Material = element.VolumeMeters > 0 ? "Aufschüttung" : "Abtrag",
                            Quantity = Math.Abs(element.VolumeMeters),
                            Unit = "m³",
                            ElementName = $"Terrasse Erdarbeiten"
                        });
                    }
                    break;
            }
        }

        return estimates;
    }

    /// <summary>
    /// Shoelace-Fläche. Erwartet metrische Koordinaten (UTM-Meter).
    /// Liefert 0 wenn die Werte wie Lat/Lon aussehen (|x|&lt;180 und |y|&lt;90) —
    /// das passiert sonst mit astronomisch falschen Ergebnissen (Grad² ≠ m²).
    /// </summary>
    public double CalculatePolygonArea(List<(double x, double y)> points)
    {
        if (points.Count < 3) return 0;

        // Plausibilitäts-Check: Sind das UTM-Meter oder WGS84-Grade?
        // UTM-Easting liegt üblich bei 500000, UTM-Northing bei Millionen.
        // Bei Werten < 1 für beide Koordinaten in Range [-180, 180] ist es wahrscheinlich
        // Grad-Input — dann Shoelace-Ergebnis ist nicht in m², sondern Grad² × 12 Mrd m².
        var suspicious = points.All(p =>
            Math.Abs(p.x) <= 180.0 && Math.Abs(p.y) <= 90.0 &&
            !(Math.Abs(p.x) > 1.0 || Math.Abs(p.y) > 1.0));

        if (suspicious)
        {
            System.Diagnostics.Debug.WriteLine(
                "GardenPlanService.CalculatePolygonArea: Punkte sehen nach Lat/Lon aus — " +
                "Caller muss vorher in UTM-Meter konvertieren. 0 zurückgegeben.");
            return 0;
        }

        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            int j = (i + 1) % points.Count;
            area += points[i].x * points[j].y;
            area -= points[j].x * points[i].y;
        }
        return Math.Abs(area) / 2.0;
    }

    public double CalculatePolylineLength(List<(double x, double y)> points)
    {
        double length = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            var dx = points[i + 1].x - points[i].x;
            var dy = points[i + 1].y - points[i].y;
            length += Math.Sqrt(dx * dx + dy * dy);
        }
        return length;
    }

    public List<(double x, double y)> ParsePoints(string json)
    {
        try
        {
            var arrays = JsonSerializer.Deserialize<double[][]>(json);
            if (arrays == null) return [];
            return arrays.Where(a => a.Length >= 2).Select(a => (a[0], a[1])).ToList();
        }
        catch
        {
            return [];
        }
    }

    public string SerializePoints(List<(double x, double y)> points)
    {
        var arrays = points.Select(p => new[] { p.x, p.y }).ToArray();
        return JsonSerializer.Serialize(arrays);
    }

    private static float GetDefaultThickness(string material)
    {
        return DefaultThicknesses.GetValueOrDefault(material, 15f);
    }
}
