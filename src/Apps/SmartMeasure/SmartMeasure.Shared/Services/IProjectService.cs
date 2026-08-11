using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>
/// SQLite-Persistenz der PROJEKTE selbst (Anlegen, Laden, Umbenennen, Duplizieren, Löschen).
///
/// Punkte und Gartenelemente liegen bewusst in eigenen Rollen-Interfaces
/// (<see cref="IProjectPointService"/>, <see cref="IProjectElementService"/>): vorher war alles
/// in einem 12-Methoden-Interface, und jeder Konsument hing an allem — der Gartenplan-Tab
/// bekam Lösch-Rechte für ganze Projekte, obwohl er nur Elemente schreibt.
/// Implementiert werden alle drei von derselben <see cref="ProjectService"/>-Instanz
/// (eine SQLite-Verbindung, siehe DI-Registrierung in App.axaml.cs).
/// </summary>
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
}
