using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>SQLite-Persistenz fuer Projekte, Messpunkte und Gartenelemente</summary>
public interface IProjectService
{
    /// <summary>Alle Projekte laden (ohne Punkte/Elemente)</summary>
    Task<List<SurveyProject>> GetAllProjectsAsync();

    /// <summary>Projekt mit allen Punkten und Gartenelementen laden</summary>
    Task<SurveyProject?> GetProjectAsync(int id);

    /// <summary>Neues Projekt erstellen</summary>
    Task<SurveyProject> CreateProjectAsync(string name, string type = "Grundstueck");

    /// <summary>Projekt aktualisieren (Name, Notizen, berechnete Werte)</summary>
    Task UpdateProjectAsync(SurveyProject project);

    /// <summary>Projekt loeschen (inkl. Punkte und Elemente)</summary>
    Task DeleteProjectAsync(int id);

    /// <summary>Projekt duplizieren (als Planungsvariante)</summary>
    Task<SurveyProject> DuplicateProjectAsync(int id, string newName);

    /// <summary>Punkt zu einem Projekt hinzufuegen</summary>
    Task AddPointAsync(int projectId, SurveyPoint point);

    /// <summary>Alle Punkte eines Projekts laden</summary>
    Task<List<SurveyPoint>> GetPointsAsync(int projectId);

    /// <summary>Gartenelement zu einem Projekt hinzufuegen</summary>
    Task AddGardenElementAsync(int projectId, GardenElement element);

    /// <summary>Gartenelement aktualisieren</summary>
    Task UpdateGardenElementAsync(GardenElement element);

    /// <summary>Gartenelement loeschen</summary>
    Task DeleteGardenElementAsync(int id);

    /// <summary>Alle Gartenelemente eines Projekts laden</summary>
    Task<List<GardenElement>> GetGardenElementsAsync(int projectId);
}
