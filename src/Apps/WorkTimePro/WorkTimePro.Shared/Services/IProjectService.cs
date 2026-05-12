using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Service for project tracking (Premium feature)
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// Get all active projects
    /// </summary>
    Task<List<Project>> GetProjectsAsync(bool includeInactive = false);

    /// <summary>
    /// Get project by ID
    /// </summary>
    Task<Project?> GetProjectAsync(int id);

    /// <summary>
    /// Save project
    /// </summary>
    Task SaveProjectAsync(Project project);

    /// <summary>
    /// Delete project
    /// </summary>
    Task DeleteProjectAsync(int id);

    /// <summary>
    /// Hours per project for a period (aggregiert aus TimeEntry CheckIn/CheckOut-Paaren).
    /// </summary>
    Task<Dictionary<Project, double>> GetProjectHoursAsync(DateTime start, DateTime end);

    /// <summary>
    /// Variante für StatisticsViewModel: arbeitet mit bereits geladenen WorkDays.
    /// </summary>
    Task<Dictionary<Project, double>> GetProjectHoursAsync(IReadOnlyList<WorkDay> workDays);
}
