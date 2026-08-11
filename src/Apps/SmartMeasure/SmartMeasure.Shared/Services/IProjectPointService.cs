using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>
/// SQLite-Persistenz der MESSPUNKTE eines Projekts. Rollen-Schnitt aus dem früheren
/// <see cref="IProjectService"/> (siehe dessen Doku).
/// </summary>
public interface IProjectPointService
{
    /// <summary>Punkt zu einem Projekt hinzufuegen</summary>
    Task AddPointAsync(int projectId, SurveyPoint point);

    /// <summary>Alle Punkte eines Projekts laden</summary>
    Task<List<SurveyPoint>> GetPointsAsync(int projectId);
}
