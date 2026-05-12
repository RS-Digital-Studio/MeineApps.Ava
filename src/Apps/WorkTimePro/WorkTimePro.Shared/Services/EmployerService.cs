using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Implementation of the employer service
/// </summary>
public sealed class EmployerService : IEmployerService
{
    private readonly IDatabaseService _database;

    public EmployerService(IDatabaseService database)
    {
        _database = database;
    }

    public async Task<List<Employer>> GetEmployersAsync(bool includeInactive = false)
    {
        var employers = await _database.GetEmployersAsync(includeInactive);

        // Create default employer if none exist
        if (employers.Count == 0)
        {
            var defaultEmployer = new Employer
            {
                Name = "Main Employer",
                WeeklyHours = 40,
                IsDefault = true,
                IsActive = true,
                Color = "#1565C0"
            };
            await _database.SaveEmployerAsync(defaultEmployer);
            employers.Add(defaultEmployer);
        }

        return employers;
    }

    public async Task<Employer?> GetDefaultEmployerAsync()
    {
        return await _database.GetDefaultEmployerAsync();
    }

    public async Task SaveEmployerAsync(Employer employer)
    {
        await _database.SaveEmployerAsync(employer);
    }

    public async Task DeleteEmployerAsync(int id)
    {
        await _database.DeleteEmployerAsync(id);
    }

    public async Task SetDefaultEmployerAsync(int id)
    {
        await _database.SetDefaultEmployerAsync(id);
    }

    public async Task<Dictionary<Employer, double>> GetEmployerHoursAsync(DateTime start, DateTime end)
    {
        var workDays = await _database.GetWorkDaysAsync(start, end);
        return await GetEmployerHoursAsync(workDays);
    }

    public async Task<Dictionary<Employer, double>> GetEmployerHoursAsync(IReadOnlyList<WorkDay> workDays)
    {
        var result = new Dictionary<Employer, double>();
        var employers = await GetEmployersAsync(true);

        foreach (var employer in employers)
        {
            double minutes = 0;
            for (var i = 0; i < workDays.Count; i++)
            {
                var w = workDays[i];
                if (w.EmployerId == employer.Id || (employer.IsDefault && w.EmployerId == null))
                    minutes += w.ActualWorkMinutes;
            }

            var hours = minutes / 60.0;
            if (hours > 0)
                result[employer] = hours;
        }

        return result;
    }

}
