using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Export von Vermessungsdaten: CSV, GeoJSON, DXF, KMZ, PDF</summary>
public interface IExportService
{
    /// <summary>Punkte als CSV exportieren (Lat, Lon, Alt, Label, Timestamp)</summary>
    string ExportToCsv(SurveyProject project);

    /// <summary>Punkte + Polygon als GeoJSON exportieren (fuer QGIS/Google Earth)</summary>
    string ExportToGeoJson(SurveyProject project);

    /// <summary>Projekt als DXF exportieren (AutoCAD/Allplan/Revit-kompatibel).
    /// POINT-Entities für Messpunkte, LWPOLYLINE für Gartenelemente, je Typ eigener Layer.
    /// Koordinaten in UTM-Metern mit positioniertem INSERTION-Block.</summary>
    string ExportToDxf(SurveyProject project);

    /// <summary>Projekt als KMZ (ZIP-gepackte KML) exportieren (Google Earth, Maps).
    /// Placemarks für Punkte mit Labels, LineString für Polygon-Umrandung.</summary>
    /// <returns>Pfad zur geschriebenen KMZ-Datei</returns>
    Task<string> ExportToKmzAsync(SurveyProject project, string outputDir);

    /// <summary>Projekt-Bericht als PDF exportieren (Projekt-Info, Punkt-Tabelle, Materialliste)</summary>
    /// <returns>Pfad zur generierten PDF-Datei</returns>
    Task<string> ExportPdfAsync(SurveyProject project, List<SurveyPoint> points,
        List<GardenElement> elements, TerrainMesh? mesh, string outputDir);
}
