using HandwerkerRechner.Models;

namespace HandwerkerRechner.Services;

/// <summary>
/// Service for project storage and management
/// </summary>
public interface IProjectService
{
    /// <summary>Wird ausgelöst, wenn das Speichern fehlschlägt (z.B. Speicher voll/Schreibschutz) — die UI zeigt einen Hinweis statt stillem Datenverlust.</summary>
    event Action? SaveFailed;

    Task SaveProjectAsync(Project project);
    Task<List<Project>> LoadAllProjectsAsync();
    Task<Project?> LoadProjectAsync(string projectId);
    Task DeleteProjectAsync(string projectId);
    Task<List<Project>> LoadProjectsByTypeAsync(CalculatorType calculatorType);
}
