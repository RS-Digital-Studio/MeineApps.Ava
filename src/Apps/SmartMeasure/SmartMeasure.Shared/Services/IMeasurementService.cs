using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Verwaltet Messpunkte und berechnet Abstände/Flächen</summary>
public interface IMeasurementService
{
    /// <summary>Aktuelle Messpunkte der laufenden Session</summary>
    List<SurveyPoint> CurrentPoints { get; }

    /// <summary>Neuer Punkt wurde hinzugefuegt</summary>
    event Action<SurveyPoint>? PointAdded;

    /// <summary>Punkt hinzufuegen</summary>
    void AddPoint(SurveyPoint point);

    /// <summary>Alle Punkte loeschen</summary>
    void ClearPoints();

    /// <summary>Abstand zwischen zwei Punkten in Metern (2D horizontal)</summary>
    double CalculateDistance2D(SurveyPoint a, SurveyPoint b);

    /// <summary>Abstand zwischen zwei Punkten in Metern (3D mit Hoehe)</summary>
    double CalculateDistance3D(SurveyPoint a, SurveyPoint b);

    /// <summary>Flaeche des Polygons in m² (Shoelace-Formel, 2D)</summary>
    double CalculateArea(List<SurveyPoint> points);

    /// <summary>Umfang des Polygons in m</summary>
    double CalculatePerimeter(List<SurveyPoint> points);
}
