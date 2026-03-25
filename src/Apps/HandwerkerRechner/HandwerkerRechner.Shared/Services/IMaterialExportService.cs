namespace HandwerkerRechner.Services;

/// <summary>
/// Service fuer den PDF-Export von Material-/Berechnungslisten.
/// </summary>
public interface IMaterialExportService
{
    /// <summary>
    /// Erstellt ein PDF mit Rechner-Typ, Eingaben und Ergebnissen.
    /// Gibt den Dateipfad des generierten PDFs zurueck.
    /// </summary>
    Task<string> ExportToPdfAsync(string calculatorType, Dictionary<string, string> inputs, Dictionary<string, string> results);

    /// <summary>
    /// Erstellt ein PDF fuer ein komplettes Projekt (Name + Berechnungsdaten).
    /// Gibt den Dateipfad des generierten PDFs zurueck.
    /// </summary>
    Task<string> ExportProjectToPdfAsync(string projectName, string calculatorType, Dictionary<string, string> inputs, Dictionary<string, string> results);

    /// <summary>
    /// Exportiert Berechnungsdaten als CSV (Excel-kompatibel, Semikolon-getrennt, UTF-8-BOM).
    /// Gibt den Dateipfad der generierten CSV-Datei zurueck.
    /// </summary>
    Task<string> ExportToCsvAsync(string calculatorType, Dictionary<string, string> inputs, Dictionary<string, string> results);
}
