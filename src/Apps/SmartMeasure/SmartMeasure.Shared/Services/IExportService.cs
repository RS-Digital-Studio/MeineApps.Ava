using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Export von Vermessungsdaten: CSV, GeoJSON, PDF</summary>
public interface IExportService
{
    /// <summary>Punkte als CSV exportieren (Lat, Lon, Alt, Label, Timestamp)</summary>
    string ExportToCsv(SurveyProject project);

    /// <summary>Punkte + Polygon als GeoJSON exportieren (fuer QGIS/Google Earth)</summary>
    string ExportToGeoJson(SurveyProject project);
}
