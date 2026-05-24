namespace SmartMeasure.Shared.Services;

/// <summary>
/// Plattform-übergreifender Zugriff auf App-spezifische Ordner und Dateien.
/// Abstrahiert Environment.SpecialFolder-Zugriffe, damit Android (Sandbox-Pfad)
/// und Desktop (APPDATA/~/.config) sauber getrennt sind.
/// </summary>
public interface IAppPaths
{
    /// <summary>Basis-Ordner für alle SmartMeasure-Dateien (plattformabhängig).</summary>
    string AppDataFolder { get; }

    /// <summary>Vollständiger Pfad zur SQLite-Datenbank.</summary>
    string DatabasePath { get; }

    /// <summary>Ordner für Export-Dateien (CSV, GeoJSON, OBJ).</summary>
    string ExportFolder { get; }

    /// <summary>Plan-Kap. 5.6: Ordner fuer AR-Capture-Fotos pro Punkt. JPEGs werden
    /// hier abgelegt; <see cref="Models.SurveyPoint.PhotoPath"/> speichert nur den
    /// Dateinamen relativ zu diesem Ordner.</summary>
    string PhotosFolder { get; }
}
