using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Implementation of the vacation management service
/// </summary>
public class VacationService : IVacationService
{
    private readonly IDatabaseService _database;
    private readonly IHolidayService _holidayService;

    public VacationService(IDatabaseService database, IHolidayService holidayService)
    {
        _database = database;
        _holidayService = holidayService;
    }

    public async Task<VacationQuota> GetQuotaAsync(int year, int? employerId = null)
    {
        var quota = await _database.GetVacationQuotaAsync(year, employerId);

        if (quota == null)
        {
            // Create new quota with default values
            quota = new VacationQuota
            {
                Year = year,
                TotalDays = 30,
                CarryOverDays = 0,
                EmployerId = employerId
            };
            await _database.SaveVacationQuotaAsync(quota);
        }

        // Genommene und geplante Urlaubstage berechnen
        // Laufende Urlaube (StartDate < heute UND EndDate >= heute) anteilig aufteilen
        var entries = await GetVacationEntriesAsync(year);
        var today = DateTime.Today;

        var taken = 0;
        var planned = 0;

        foreach (var e in entries.Where(e => e.Type == DayStatus.Vacation))
        {
            if (e.EndDate < today)
            {
                // Komplett vergangen
                taken += e.Days;
            }
            else if (e.StartDate >= today)
            {
                // Komplett in der Zukunft
                planned += e.Days;
            }
            else
            {
                // Laufender Urlaub: anteilig aufteilen
                var totalDays = (e.EndDate - e.StartDate).Days + 1;
                var pastDays = (today - e.StartDate).Days;
                if (totalDays > 0 && e.Days > 0)
                {
                    var pastRatio = (double)pastDays / totalDays;
                    var pastWorkDays = (int)Math.Round(e.Days * pastRatio);
                    taken += pastWorkDays;
                    planned += e.Days - pastWorkDays;
                }
            }
        }

        quota.TakenDays = taken;
        quota.PlannedDays = planned;

        return quota;
    }

    public async Task SaveQuotaAsync(VacationQuota quota)
    {
        await _database.SaveVacationQuotaAsync(quota);
    }

    public async Task<List<VacationEntry>> GetVacationEntriesAsync(int year)
    {
        var start = new DateTime(year, 1, 1);
        var end = new DateTime(year, 12, 31);
        return await GetVacationEntriesAsync(start, end);
    }

    public async Task<List<VacationEntry>> GetVacationEntriesAsync(DateTime start, DateTime end)
    {
        return await _database.GetVacationEntriesAsync(start, end);
    }

    public async Task SaveVacationEntryAsync(VacationEntry entry)
    {
        // Calculate work days if not set
        if (entry.Days <= 0)
        {
            entry.Days = await CalculateWorkDaysAsync(entry.StartDate, entry.EndDate);
        }

        await _database.SaveVacationEntryAsync(entry);

        // Update WorkDays for the period
        await UpdateWorkDaysForVacationAsync(entry);
    }

    public async Task DeleteVacationEntryAsync(int entryId)
    {
        var entry = await _database.GetVacationEntryAsync(entryId);
        if (entry != null)
        {
            await _database.DeleteVacationEntryAsync(entryId);

            // Reset WorkDays to default status
            await ResetWorkDaysForVacationAsync(entry);
        }
    }

    public async Task<VacationEntry?> GetVacationForDateAsync(DateTime date)
    {
        var entries = await GetVacationEntriesAsync(date, date);
        return entries.FirstOrDefault(e => e.StartDate <= date && e.EndDate >= date);
    }

    public async Task<int> CalculateWorkDaysAsync(DateTime start, DateTime end)
    {
        var settings = await _database.GetSettingsAsync();
        var workDaysArray = settings.WorkDaysArray;
        var holidays = await _holidayService.GetHolidaysAsync(start, end);

        int count = 0;
        var current = start.Date;

        while (current <= end.Date)
        {
            // Check weekday (1=Mon, 7=Sun)
            var ourDay = current.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)current.DayOfWeek;

            if (workDaysArray.Contains(ourDay))
            {
                // Check holiday
                if (!holidays.Any(h => h.Date == current))
                {
                    count++;
                }
            }

            current = current.AddDays(1);
        }

        return count;
    }

    public async Task<VacationStatistics> GetStatisticsAsync(int year, int? employerId = null)
    {
        var quota = await GetQuotaAsync(year, employerId);
        var entries = await GetVacationEntriesAsync(year);
        var today = DateTime.Today;

        // Genommene/geplante Urlaubstage korrekt berechnen (inkl. laufender Urlaube)
        var statsTaken = 0;
        var statsPlanned = 0;
        foreach (var e in entries.Where(e => e.Type == DayStatus.Vacation))
        {
            if (e.EndDate < today) statsTaken += e.Days;
            else if (e.StartDate >= today) statsPlanned += e.Days;
            else
            {
                var totalDays = (e.EndDate - e.StartDate).Days + 1;
                var pastDays = (today - e.StartDate).Days;
                if (totalDays > 0 && e.Days > 0)
                {
                    var pastWorkDays = (int)Math.Round(e.Days * ((double)pastDays / totalDays));
                    statsTaken += pastWorkDays;
                    statsPlanned += e.Days - pastWorkDays;
                }
            }
        }

        var stats = new VacationStatistics
        {
            Year = year,
            TotalDays = quota.TotalDays,
            CarryOverDays = quota.CarryOverDays,
            TakenDays = statsTaken,
            PlannedDays = statsPlanned,
            SickDays = entries
                .Where(e => e.Type == DayStatus.Sick)
                .Sum(e => e.Days),
            SpecialLeaveDays = entries
                .Where(e => e.Type == DayStatus.SpecialLeave)
                .Sum(e => e.Days)
        };

        return stats;
    }

    public async Task<int> CarryOverRemainingDaysAsync(int fromYear, int toYear, int? employerId = null)
    {
        var fromQuota = await GetQuotaAsync(fromYear, employerId);
        var remaining = fromQuota.RemainingDays;

        if (remaining <= 0)
            return 0;

        var toQuota = await GetQuotaAsync(toYear, employerId);
        toQuota.CarryOverDays = remaining;
        await SaveQuotaAsync(toQuota);

        return remaining;
    }

    #region Private Methods

    private async Task UpdateWorkDaysForVacationAsync(VacationEntry entry)
    {
        var current = entry.StartDate.Date;
        while (current <= entry.EndDate.Date)
        {
            var workDay = await _database.GetOrCreateWorkDayAsync(current);

            if (workDay.Status == DayStatus.WorkDay)
            {
                workDay.Status = entry.Type;
                workDay.Note = entry.Note;
                await _database.SaveWorkDayAsync(workDay);
            }

            current = current.AddDays(1);
        }
    }

    private async Task ResetWorkDaysForVacationAsync(VacationEntry entry)
    {
        var settings = await _database.GetSettingsAsync();
        var current = entry.StartDate.Date;

        while (current <= entry.EndDate.Date)
        {
            var workDay = await _database.GetOrCreateWorkDayAsync(current);

            if (workDay.Status == entry.Type)
            {
                // Reset to default status
                if (settings.IsWorkDay(current.DayOfWeek))
                {
                    workDay.Status = DayStatus.WorkDay;
                }
                else
                {
                    workDay.Status = DayStatus.Weekend;
                }
                workDay.Note = null;
                await _database.SaveWorkDayAsync(workDay);
            }

            current = current.AddDays(1);
        }
    }

    #endregion
}
