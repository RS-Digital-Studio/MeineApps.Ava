using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>AR-Kamera-Erfassung (plattform-spezifisch: Android = ARCore, Desktop = Mock)</summary>
public interface IArCaptureService
{
    /// <summary>AR-Capture-Session starten (oeffnet native Activity, kehrt mit Ergebnis zurueck)</summary>
    Task<ArCaptureResult?> CaptureAsync();

    /// <summary>Ist ARCore auf diesem Geraet verfuegbar?</summary>
    Task<bool> IsAvailableAsync();
}
