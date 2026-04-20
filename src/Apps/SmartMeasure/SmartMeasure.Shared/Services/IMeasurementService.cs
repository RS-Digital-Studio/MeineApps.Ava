using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Verwaltet Messpunkte und berechnet Abstände/Flächen</summary>
public interface IMeasurementService
{
    /// <summary>Aktuelle Messpunkte der laufenden Session</summary>
    List<SurveyPoint> CurrentPoints { get; }

    /// <summary>Neuer einzelner Punkt wurde hinzugefügt (Live-Messung).</summary>
    event Action<SurveyPoint>? PointAdded;

    /// <summary>
    /// Die komplette Punkte-Liste hat sich geändert (Projekt-Load, Clear, Batch-Import).
    /// Listener sollten ihre Visualisierung EINMAL neu aufbauen statt pro Punkt zu reagieren.
    /// </summary>
    event Action? PointsReset;

    /// <summary>Punkt hinzufügen (feuert PointAdded).</summary>
    void AddPoint(SurveyPoint point);

    /// <summary>
    /// Alle aktuellen Punkte durch die übergebene Liste ersetzen. Feuert nur einmal
    /// <see cref="PointsReset"/> am Ende — verhindert N² Rebuild-Kaskaden beim Projekt-Laden.
    /// </summary>
    void ReplacePoints(IEnumerable<SurveyPoint> points);

    /// <summary>Alle Punkte löschen (feuert PointsReset).</summary>
    void ClearPoints();

    /// <summary>Abstand zwischen zwei Punkten in Metern (2D horizontal)</summary>
    double CalculateDistance2D(SurveyPoint a, SurveyPoint b);

    /// <summary>Abstand zwischen zwei Punkten in Metern (3D mit Höhe)</summary>
    double CalculateDistance3D(SurveyPoint a, SurveyPoint b);

    /// <summary>Fläche des Polygons in m² (Shoelace auf Convex-Hull, UTM-projiziert)</summary>
    double CalculateArea(List<SurveyPoint> points);

    /// <summary>Umfang des Polygons in m (Haversine)</summary>
    double CalculatePerimeter(List<SurveyPoint> points);
}
