namespace FitnessRechner.Services;

/// <summary>
/// Plattform-Service fuer nativen Barcode-Scan (Kamera).
/// Android: CameraX + ML Kit, Desktop: gibt null zurueck.
/// </summary>
public interface IBarcodeService
{
    /// <summary>
    /// Startet den nativen Barcode-Scanner.
    /// Gibt den erkannten Barcode-String zurueck, oder null wenn abgebrochen.
    /// </summary>
    Task<string?> ScanBarcodeAsync();
}
