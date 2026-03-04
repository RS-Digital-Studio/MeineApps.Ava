using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Implementation of the project tracking service
/// </summary>
public sealed class ProjectService : IProjectService
{
    private readonly IDatabaseService _database;

    public ProjectService(IDatabaseService database)
    {
        _database = database;
    }

    public async Task<List<Project>> GetProjectsAsync(bool includeInactive = false)
    {
        return await _database.GetProjectsAsync(includeInactive);
    }

    public async Task<Project?> GetProjectAsync(int id)
    {
        return await _database.GetProjectAsync(id);
    }

    public async Task SaveProjectAsync(Project project)
    {
        await _database.SaveProjectAsync(project);
    }

    public async Task DeleteProjectAsync(int id)
    {
        await _database.DeleteProjectAsync(id);
    }

    public async Task<ProjectTimeEntry> AddProjectTimeAsync(int projectId, DateTime date, int minutes, string? note = null)
    {
        var entry = new ProjectTimeEntry
        {
            ProjectId = projectId,
            Date = date.Date,
            Minutes = minutes,
            Description = note
        };

        await _database.SaveProjectTimeEntryAsync(entry);
        return entry;
    }

    public async Task DeleteProjectTimeEntryAsync(int entryId)
    {
        await _database.DeleteProjectTimeEntryAsync(entryId);
    }

    public async Task<Dictionary<Project, double>> GetProjectHoursAsync(DateTime start, DateTime end)
    {
        var result = new Dictionary<Project, double>();
        var projects = await GetProjectsAsync(true);
        var hoursById = await _database.GetProjectHoursAsync(start, end);

        foreach (var project in projects)
        {
            if (hoursById.TryGetValue(project.Id, out var hours))
            {
                result[project] = hours;
            }
        }

        return result;
    }

    public async Task<List<ProjectTimeEntry>> GetProjectTimeEntriesAsync(int projectId, DateTime start, DateTime end)
    {
        return await _database.GetProjectTimeEntriesAsync(projectId, start, end);
    }

    public async Task<ProjectStatistics> GetProjectStatisticsAsync(int projectId)
    {
        var project = await GetProjectAsync(projectId);
        if (project == null)
            throw new ArgumentException("Project not found", nameof(projectId));

        var entries = await _database.GetProjectTimeEntriesAsync(projectId);

        var now = DateTime.Today;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        var stats = new ProjectStatistics
        {
            ProjectId = projectId,
            ProjectName = project.Name,
            TotalHours = entries.Sum(e => e.Minutes) / 60.0,
            ThisMonthHours = entries.Where(e => e.Date >= thisMonthStart).Sum(e => e.Minutes) / 60.0,
            LastMonthHours = entries.Where(e => e.Date >= lastMonthStart && e.Date < thisMonthStart).Sum(e => e.Minutes) / 60.0,
            EntryCount = entries.Count,
            FirstEntry = entries.OrderBy(e => e.Date).FirstOrDefault()?.Date,
            LastEntry = entries.OrderByDescending(e => e.Date).FirstOrDefault()?.Date
        };

        return stats;
    }
}
