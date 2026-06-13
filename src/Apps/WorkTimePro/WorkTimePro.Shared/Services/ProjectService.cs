using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Implementation of the project tracking service.
/// Project-Stunden werden aus TimeEntry.ProjectId aggregiert (CheckIn/CheckOut-Paare),
/// nicht aus einer separaten ProjectTimeEntry-Tabelle (entfällt seit v2.0.7).
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

    public async Task<Dictionary<Project, double>> GetProjectHoursAsync(DateTime start, DateTime end)
    {
        var workDays = await _database.GetWorkDaysAsync(start, end);
        return await GetProjectHoursAsync(workDays);
    }

    public async Task<Dictionary<Project, double>> GetProjectHoursAsync(IReadOnlyList<WorkDay> workDays)
    {
        var result = new Dictionary<Project, double>();
        if (workDays.Count == 0) return result;

        var projects = await GetProjectsAsync(true);
        var projectMap = projects.ToDictionary(p => p.Id);

        // TimeEntries für alle WorkDays in einer Batch-Query laden (kein N+1)
        var workDayIds = workDays.Select(w => w.Id).ToList();
        var entriesByDay = await _database.GetTimeEntriesForWorkDaysAsync(workDayIds);

        var minutesByProject = new Dictionary<int, double>();

        // Pro WorkDay: CheckIn/CheckOut-Paare auflösen, Dauer dem Projekt der CheckIn-Buchung zuordnen
        foreach (var (_, entries) in entriesByDay)
        {
            // Bereits nach Timestamp sortiert (DB-Index/OrderBy in GetTimeEntriesForWorkDaysAsync)
            TimeEntry? lastCheckIn = null;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == EntryType.CheckIn)
                {
                    lastCheckIn = e;
                }
                else if (e.Type == EntryType.CheckOut && lastCheckIn != null)
                {
                    if (lastCheckIn.ProjectId.HasValue)
                    {
                        // DST-bewusst wie alle anderen Dauer-Berechnungen
                        var minutes = Helpers.DurationMath.RealElapsedMinutes(lastCheckIn.Timestamp, e.Timestamp);
                        if (minutes > 0)
                        {
                            var pid = lastCheckIn.ProjectId.Value;
                            if (!minutesByProject.TryAdd(pid, minutes))
                                minutesByProject[pid] += minutes;
                        }
                    }
                    lastCheckIn = null;
                }
            }
        }

        foreach (var (pid, minutes) in minutesByProject)
        {
            if (projectMap.TryGetValue(pid, out var project) && minutes > 0)
                result[project] = minutes / 60.0;
        }

        return result;
    }
}
