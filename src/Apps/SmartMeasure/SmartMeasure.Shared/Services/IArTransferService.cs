using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Konvertiert AR-Erfassungsdaten in SurveyPoints und GardenElements und fuegt sie ins Projekt ein</summary>
public interface IArTransferService
{
    /// <summary>AR-Ergebnis in SurveyPoints konvertieren und ins Projekt einfuegen</summary>
    Task<int> TransferToProjectAsync(ArCaptureResult result, int projectId);

    /// <summary>AR-Punkte in SurveyPoints umrechnen (lokale Meter + GPS-Offset + Heading-Rotation → WGS84)</summary>
    List<SurveyPoint> ConvertToSurveyPoints(ArCaptureResult result, int projectId);

    /// <summary>AR-Konturen in GardenElements konvertieren (Polygon-Punkte in UTM-Meter)</summary>
    List<GardenElement> ConvertToGardenElements(ArCaptureResult result, int projectId);
}
