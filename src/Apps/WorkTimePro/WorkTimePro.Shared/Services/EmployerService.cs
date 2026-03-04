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
        var result = new Dictionary<Employer, double>();
        var employers = await GetEmployersAsync(true);
        var workDays = await _database.GetWorkDaysAsync(start, end);

        foreach (var employer in employers)
        {
            var employerDays = workDays.Where(w => w.EmployerId == employer.Id || (employer.IsDefault && w.EmployerId == null));
            var hours = employerDays.Sum(w => w.ActualWorkMinutes) / 60.0;
            if (hours > 0)
            {
                result[employer] = hours;
            }
        }

        return result;
    }

    public async Task<EmployerStatistics> GetEmployerStatisticsAsync(int employerId)
    {
        var employer = (await GetEmployersAsync(true)).FirstOrDefault(e => e.Id == employerId);
        if (employer == null)
            throw new ArgumentException("Employer not found", nameof(employerId));

        var now = DateTime.Today;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1);
        var yearStart = new DateTime(now.Year, 1, 1);

        var allWorkDays = await _database.GetWorkDaysAsync(yearStart, now);
        var employerDays = allWorkDays.Where(w => w.EmployerId == employerId || (employer.IsDefault && w.EmployerId == null)).ToList();

        var thisMonthDays = employerDays.Where(w => w.Date >= thisMonthStart).ToList();

        var stats = new EmployerStatistics
        {
            EmployerId = employerId,
            EmployerName = employer.Name,
            TotalHours = employerDays.Sum(w => w.ActualWorkMinutes) / 60.0,
            ThisMonthHours = thisMonthDays.Sum(w => w.ActualWorkMinutes) / 60.0,
            TargetHoursWeekly = employer.WeeklyHours,
            WorkDaysCount = employerDays.Count(w => w.ActualWorkMinutes > 0),
            AverageHoursPerDay = employerDays.Count > 0
                ? employerDays.Sum(w => w.ActualWorkMinutes) / 60.0 / employerDays.Count(w => w.ActualWorkMinutes > 0)
                : 0
        };

        return stats;
    }
}
