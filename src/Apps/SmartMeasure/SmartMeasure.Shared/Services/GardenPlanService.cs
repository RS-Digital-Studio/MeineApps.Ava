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
            // Längen/Flächen bei Finish-Draw bereits gespeichert.
            // Falls 0 (z.B. altes Element ohne gespeicherte Metriken), aus LocalPoints nachrechnen.
            var points = element.LocalPoints ?? ParsePoints(element.PointsJson);
            if (points.Count < 2 && element.AreaSquareMeters <= 0 && element.LengthMeters <= 0) continue;

            switch (element.ElementType)
            {
                case GardenElementType.Weg:
                    var wegLength = element.LengthMeters > 0
                        ? element.LengthMeters
                        : CalculatePolylineLength(points);
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
                    var beetArea = element.AreaSquareMeters > 0
                        ? element.AreaSquareMeters
                        : CalculatePolygonArea(points);
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
                    var mauerLength = element.LengthMeters > 0
                        ? element.LengthMeters
                        : CalculatePolylineLength(points);
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
                    var terrasseArea = element.AreaSquareMeters > 0
                        ? element.AreaSquareMeters
                        : CalculatePolygonArea(points);
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
    /// Shoelace-Fläche in m². Erwartet LOKALE metrische Koordinaten relativ zu einem
    /// Referenzpunkt (z.B. `CoordinateService.LatLonToLocal`-Output oder Canvas-Tap-Koords).
    ///
    /// WICHTIG: Niemals direkt mit WGS84 Lat/Lon-Werten aufrufen — das Ergebnis wäre Grad² ·
    /// ~12 Mrd m² pro Einheit und damit komplett falsch. v2-PointsJson wird via
    /// `GetLocalPoints(..., coordService)` konvertiert bevor diese Methode ruft.
    /// </summary>
    public double CalculatePolygonArea(List<(double x, double y)> points)
    {
        if (points.Count < 3) return 0;

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
        if (string.IsNullOrWhiteSpace(json)) return [];

        // v2 erkennt am Object-Root: {"v":2,...} — dann kein v1-Array. Leer zurückgeben.
        var trimmed = json.TrimStart();
        if (trimmed.Length > 0 && trimmed[0] == '{') return [];

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

    public List<(double latitude, double longitude)>? ParsePointsWgs84(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        var trimmed = json.TrimStart();
        // v2 muss Object-Root haben
        if (trimmed.Length == 0 || trimmed[0] != '{') return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("v", out var version)) return null;
            if (version.ValueKind != JsonValueKind.Number || version.GetInt32() != 2) return null;

            if (!root.TryGetProperty("points", out var pointsArray)) return null;
            if (pointsArray.ValueKind != JsonValueKind.Array) return null;

            var result = new List<(double lat, double lon)>(pointsArray.GetArrayLength());
            foreach (var pair in pointsArray.EnumerateArray())
            {
                if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() < 2) continue;
                var lat = pair[0].GetDouble();
                var lon = pair[1].GetDouble();
                result.Add((lat, lon));
            }
            return result;
        }
        catch
        {
            return null;
        }
    }

    public string SerializePointsWgs84(List<(double latitude, double longitude)> points)
    {
        // Format: {"v":2,"points":[[lat,lon],...]}
        var arrays = points.Select(p => new[] { p.latitude, p.longitude }).ToArray();
        var envelope = new { v = 2, points = arrays };
        return JsonSerializer.Serialize(envelope);
    }

    public List<(double x, double y)> GetLocalPoints(
        GardenElement element, double refLatitude, double refLongitude,
        ICoordinateService coordinateService)
    {
        // v2 bevorzugen: absolute Lat/Lon → via UTM zu lokalen Metern projizieren
        var wgs = ParsePointsWgs84(element.PointsJson);
        if (wgs != null)
        {
            var result = new List<(double x, double y)>(wgs.Count);
            foreach (var (lat, lon) in wgs)
            {
                var (x, y, _) = coordinateService.LatLonToLocal(lat, lon, 0,
                    refLatitude, refLongitude, 0);
                result.Add((x, y));
            }
            return result;
        }

        // Legacy v1: Punkte sind bereits lokale Meter (relativ zum damaligen Schwerpunkt).
        // Annahme: aktueller Schwerpunkt ≈ damaliger Schwerpunkt. Drift möglich — User sollte
        // das Element neu zeichnen. Aber pragmatisch: besser rendern als nichts zeigen.
        return ParsePoints(element.PointsJson);
    }

    public List<(double latitude, double longitude)> LocalToWgs84(
        IReadOnlyList<(double x, double y)> localPoints,
        double refLatitude, double refLongitude,
        ICoordinateService coordinateService)
    {
        var result = new List<(double lat, double lon)>(localPoints.Count);
        foreach (var (x, y) in localPoints)
        {
            var (lat, lon, _) = coordinateService.LocalToLatLon(x, y, 0,
                refLatitude, refLongitude, 0);
            result.Add((lat, lon));
        }
        return result;
    }

    private static float GetDefaultThickness(string material)
    {
        return DefaultThicknesses.GetValueOrDefault(material, 15f);
    }
}
