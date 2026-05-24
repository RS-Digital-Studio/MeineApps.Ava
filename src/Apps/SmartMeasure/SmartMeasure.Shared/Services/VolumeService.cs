using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.4 (MVP): Polygon-Area via Shoelace + V = A * h. Mesh-basierte
/// Aushub-Berechnung (Marching Cubes auf Depth-Image-Akkumulation) folgt mit Plan-Kap. 5.5
/// — diese Klasse liefert die einfache Prismen-Variante die fuer rechteckige Gruben
/// oder gleichmaessige Aufschuettungen ausreichend genau ist.</summary>
public sealed class VolumeService : IVolumeService
{
    private readonly IGardenPlanService _gardenPlan;
    private readonly ICoordinateService _coordinateService;

    /// <summary>Material-Dichte-Tabelle in kg/m^3 (Erd-/Bau-typisch).</summary>
    private static readonly (string name, double density)[] MaterialCatalog =
    [
        ("Mutterboden (locker)", 1300),
        ("Mutterboden (verdichtet)", 1600),
        ("Sand (trocken)", 1500),
        ("Sand (feucht)", 1900),
        ("Kies", 1800),
        ("Schotter", 1500),
        ("Beton (Frischbeton)", 2400),
        ("Wasser", 1000),
    ];

    public VolumeService(IGardenPlanService gardenPlan, ICoordinateService coordinateService)
    {
        _gardenPlan = gardenPlan;
        _coordinateService = coordinateService;
    }

    public VolumeEstimate EstimatePrism(GardenElement closedContour, double depthMeters)
    {
        var area = ComputeBaseArea(closedContour);
        var volume = area * Math.Abs(depthMeters);
        // Surface = Boden + Top + 4 Seiten (vereinfacht: Umfang * Tiefe)
        var perimeter = ComputePerimeter(closedContour);
        var surface = 2 * area + perimeter * Math.Abs(depthMeters);
        return new VolumeEstimate(volume, surface, area, EstimateMaterials(volume));
    }

    public VolumeEstimate EstimateLayered(IReadOnlyList<(GardenElement contour, double layerHeightMeters)> layers)
    {
        if (layers.Count == 0)
            return new VolumeEstimate(0, 0, 0, []);

        double volume = 0;
        double surface = 0;
        double baseArea = 0;

        for (var i = 0; i < layers.Count; i++)
        {
            var (contour, h) = layers[i];
            var a = ComputeBaseArea(contour);
            if (i == 0) baseArea = a;
            volume += a * Math.Abs(h);
            surface += a + ComputePerimeter(contour) * Math.Abs(h);
        }
        // Letzte Schicht-Decke nicht vergessen
        surface += ComputeBaseArea(layers[^1].contour);

        return new VolumeEstimate(volume, surface, baseArea, EstimateMaterials(volume));
    }

    public VolumeEstimate EstimateFrustum(GardenElement topContour, GardenElement bottomContour, double heightMeters)
    {
        var areaTop = ComputeBaseArea(topContour);
        var areaBottom = ComputeBaseArea(bottomContour);
        // Trapez-Naehrung: V = (A_top + A_bottom) / 2 * h. Mathematisch exakter waere die
        // Prismatoid-Formel V = h/6 * (A_top + 4*A_mid + A_bottom), aber A_mid braeuchte
        // eine Zwischen-Kontur — die hat der User selten.
        var volume = (areaTop + areaBottom) / 2.0 * Math.Abs(heightMeters);
        var avgPerimeter = (ComputePerimeter(topContour) + ComputePerimeter(bottomContour)) / 2.0;
        var surface = areaTop + areaBottom + avgPerimeter * Math.Abs(heightMeters);
        return new VolumeEstimate(volume, surface, areaBottom, EstimateMaterials(volume));
    }

    private double ComputeBaseArea(GardenElement contour)
    {
        var pts = GetLocalPolygon(contour);
        return ShoelaceArea(pts);
    }

    private double ComputePerimeter(GardenElement contour)
    {
        var pts = GetLocalPolygon(contour);
        if (pts.Count < 2) return 0;
        double sum = 0;
        for (var i = 0; i < pts.Count; i++)
        {
            var j = (i + 1) % pts.Count;
            var dx = pts[j].x - pts[i].x;
            var dy = pts[j].y - pts[i].y;
            sum += Math.Sqrt(dx * dx + dy * dy);
        }
        return sum;
    }

    /// <summary>Local-Polygon in Metern relativ zum Schwerpunkt der Kontur. Schwerpunkt
    /// als Ref fuer GetLocalPoints — Area/Perimeter sind translation-invariant, daher
    /// reicht ein konsistenter Bezugspunkt.</summary>
    private List<(double x, double y)> GetLocalPolygon(GardenElement contour)
    {
        // v2-Format: WGS84-Punkte direkt aus PointsJson
        var wgs = _gardenPlan.ParsePointsWgs84(contour.PointsJson ?? string.Empty);
        if (wgs == null || wgs.Count == 0) return [];
        var refLat = wgs.Average(p => p.latitude);
        var refLon = wgs.Average(p => p.longitude);
        return _gardenPlan.GetLocalPoints(contour, refLat, refLon, _coordinateService);
    }

    /// <summary>Shoelace-Formel fuer Polygon-Area (Vorzeichen-unabhaengig). Public fuer
    /// Wiederverwendung in PDF-Bericht-Rendering und Tests.</summary>
    public static double ShoelaceArea(IReadOnlyList<(double x, double y)> points)
    {
        if (points.Count < 3) return 0;
        double sum = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var j = (i + 1) % points.Count;
            sum += points[i].x * points[j].y - points[j].x * points[i].y;
        }
        return Math.Abs(sum) / 2.0;
    }

    private static IReadOnlyList<VolumeMaterialEstimate> EstimateMaterials(double volumeM3)
    {
        if (volumeM3 <= 0) return [];
        var list = new List<VolumeMaterialEstimate>(MaterialCatalog.Length);
        foreach (var (name, density) in MaterialCatalog)
            list.Add(new VolumeMaterialEstimate(name, density, volumeM3 * density / 1000.0));
        return list;
    }
}
