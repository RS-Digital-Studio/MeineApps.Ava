using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>
/// SQLite-Persistenz der GARTENELEMENTE eines Projekts. Rollen-Schnitt aus dem früheren
/// <see cref="IProjectService"/> (siehe dessen Doku).
///
/// Nicht verwechseln mit <see cref="IGardenPlanService"/>: der rechnet und serialisiert
/// (Fläche, Länge, PointsJson v1/v2, Projektion), speichert aber nichts.
/// </summary>
public interface IProjectElementService
{
    /// <summary>Gartenelement zu einem Projekt hinzufuegen</summary>
    Task AddGardenElementAsync(int projectId, GardenElement element);

    /// <summary>Gartenelement aktualisieren</summary>
    Task UpdateGardenElementAsync(GardenElement element);

    /// <summary>Gartenelement loeschen</summary>
    Task DeleteGardenElementAsync(int id);

    /// <summary>Alle Gartenelemente eines Projekts laden</summary>
    Task<List<GardenElement>> GetGardenElementsAsync(int projectId);
}
