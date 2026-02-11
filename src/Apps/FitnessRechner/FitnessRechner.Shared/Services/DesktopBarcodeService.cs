namespace FitnessRechner.Services;

/// <summary>
/// Desktop-Fallback: Kein Kamera-Zugriff verfuegbar.
/// Gibt immer null zurueck → BarcodeScannerView zeigt manuelle Eingabe.
/// </summary>
public class DesktopBarcodeService : IBarcodeService
{
    public Task<string?> ScanBarcodeAsync() => Task.FromResult<string?>(null);
}
