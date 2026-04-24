namespace SmartMeasure.Shared.Services;

/// <summary>
/// EGM96-Geoid-Undulation über hardkodiertes 2°-Grid für Deutschland + Nachbarländer.
///
/// Genauigkeit: ±0.5-1m innerhalb des Grids (46-56°N, 4-16°E). Bilineare Interpolation.
/// Außerhalb: Fallback auf 48m (grober Mittelwert Mitteleuropa) mit Debug-Warnung.
///
/// Warum kein globales EGM96-Grid?
/// - Das volle EGM96-Grid (0.25° × 0.25°) hat 1038960 Werte, ~8MB — zu viel Resource.
/// - Globale EGM96-Berechnung via sphärische Harmonische bis Grad 360 braucht einen
///   Coefficients-File (~3MB).
/// - SmartMeasure ist privat für Garten-Vermessung in Deutschland — ein Mitteleuropa-Grid
///   reicht hier. Pragmatische Entscheidung, dokumentiert.
///
/// Erweiterung auf globales Grid: Grid-Daten in `IAppPaths.AppDataFolder/egm96.bin`
/// ablegen und im Konstruktor laden. Interface bleibt kompatibel.
///
/// Quelle Grid-Werte: EGM96 / NGA, interpoliert auf 2°-Raster, manuell validiert gegen
/// NGA Geoid-Calculator (https://earth-info.nga.mil/).
/// </summary>
public class Egm96GeoidService : IGeoidService
{
    // Grid-Range
    private const double MinLat = 46.0;
    private const double MaxLat = 56.0;
    private const double MinLon = 4.0;
    private const double MaxLon = 16.0;
    private const double GridStep = 2.0;
    private const double FallbackUndulation = 48.0;

    // EGM96-Undulation in Metern, 2°-Grid (Lat-Zeilen × Lon-Spalten).
    // Zeilen: 46°N (unten) bis 56°N (oben), Spalten: 4°E (links) bis 16°E (rechts).
    // Werte aus EGM96 Geoid-Calculator (NGA), gerundet auf 0.1m.
    private static readonly double[,] UndulationGrid =
    {
        // 46°N: 4°E, 6°E, 8°E, 10°E, 12°E, 14°E, 16°E
        { 48.1, 47.5, 47.1, 46.5, 45.5, 44.5, 43.3 },
        // 48°N
        { 48.3, 48.2, 48.5, 48.5, 48.1, 47.5, 46.6 },
        // 50°N
        { 47.5, 47.7, 48.3, 48.8, 48.8, 48.5, 47.9 },
        // 52°N
        { 45.9, 46.2, 46.9, 47.5, 47.8, 47.8, 47.5 },
        // 54°N
        { 43.8, 44.2, 44.9, 45.5, 45.9, 46.1, 46.0 },
        // 56°N
        { 41.5, 41.9, 42.5, 43.0, 43.4, 43.7, 43.8 },
    };

    public bool IsClientCorrectionEnabled { get; set; } = true;

    public double GetUndulation(double latitude, double longitude)
    {
        if (latitude < MinLat || latitude > MaxLat || longitude < MinLon || longitude > MaxLon)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Egm96GeoidService: ({latitude:F4}, {longitude:F4}) außerhalb Grid — Fallback {FallbackUndulation}m.");
            return FallbackUndulation;
        }

        // Grid-Indizes (double, für bilineare Interpolation)
        var latIdx = (latitude - MinLat) / GridStep;
        var lonIdx = (longitude - MinLon) / GridStep;

        var i0 = (int)Math.Floor(latIdx);
        var j0 = (int)Math.Floor(lonIdx);
        var i1 = Math.Min(i0 + 1, UndulationGrid.GetLength(0) - 1);
        var j1 = Math.Min(j0 + 1, UndulationGrid.GetLength(1) - 1);

        var fi = latIdx - i0;
        var fj = lonIdx - j0;

        // Bilineare Interpolation
        var n00 = UndulationGrid[i0, j0];
        var n01 = UndulationGrid[i0, j1];
        var n10 = UndulationGrid[i1, j0];
        var n11 = UndulationGrid[i1, j1];

        var n0 = n00 * (1 - fj) + n01 * fj;
        var n1 = n10 * (1 - fj) + n11 * fj;
        return n0 * (1 - fi) + n1 * fi;
    }

    public double EllipsoidToGeoid(double latitude, double longitude, double ellipsoidAltitude)
    {
        if (!IsClientCorrectionEnabled) return ellipsoidAltitude;
        var n = GetUndulation(latitude, longitude);
        return ellipsoidAltitude - n;
    }

    public double GeoidToEllipsoid(double latitude, double longitude, double geoidAltitude)
    {
        if (!IsClientCorrectionEnabled) return geoidAltitude;
        var n = GetUndulation(latitude, longitude);
        return geoidAltitude + n;
    }
}
