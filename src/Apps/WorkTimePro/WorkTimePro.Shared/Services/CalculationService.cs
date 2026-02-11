using System.Globalization;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;

namespace WorkTimePro.Services;

/// <summary>
/// Service for calculations (work time, plus/minus, auto-pause)
/// </summary>
public class CalculationService : ICalculationService
{
    private readonly IDatabaseService _database;

    public CalculationService(IDatabaseService database)
    {
        _database = database;
    }

    public async Task RecalculateWorkDayAsync(WorkDay workDay)
    {
        var entries = await _database.GetTimeEntriesAsync(workDay.Id);

        if (entries.Count == 0)
        {
            workDay.ActualWorkMinutes = 0;
            workDay.FirstCheckIn = null;
            workDay.LastCheckOut = null;
            workDay.BalanceMinutes = -workDay.TargetWorkMinutes;
            await _database.SaveWorkDayAsync(workDay);
            return;
        }

        // Calculate work time
        var totalMinutes = 0;
        TimeEntry? lastCheckIn = null;
        DateTime? firstCheckIn = null;
        DateTime? lastCheckOut = null;

        foreach (var entry in entries.OrderBy(e => e.Timestamp))
        {
            if (entry.Type == EntryType.CheckIn)
            {
                lastCheckIn = entry;
                firstCheckIn ??= entry.Timestamp;
            }
            else if (entry.Type == EntryType.CheckOut && lastCheckIn != null)
            {
                totalMinutes += (int)(entry.Timestamp - lastCheckIn.Timestamp).TotalMinutes;
                lastCheckOut = entry.Timestamp;
                lastCheckIn = null;
            }
        }

        workDay.FirstCheckIn = firstCheckIn;
        workDay.LastCheckOut = lastCheckOut;

        // Calculate pauses
        await RecalculatePauseTimeAsync(workDay);

        // Net work time (gross - pauses)
        var totalPauseMinutes = workDay.ManualPauseMinutes + workDay.AutoPauseMinutes;
        workDay.ActualWorkMinutes = Math.Max(0, totalMinutes - totalPauseMinutes);

        // Calculate balance
        workDay.BalanceMinutes = workDay.ActualWorkMinutes - workDay.TargetWorkMinutes;

        await _database.SaveWorkDayAsync(workDay);
    }

    public async Task RecalculatePauseTimeAsync(WorkDay workDay)
    {
        var pauses = await _database.GetPauseEntriesAsync(workDay.Id);

        // Sum manual pauses
        var manualMinutes = pauses
            .Where(p => !p.IsAutoPause && p.EndTime != null)
            .Sum(p => (int)p.Duration.TotalMinutes);

        workDay.ManualPauseMinutes = manualMinutes;

        // Check and apply auto-pause
        await ApplyAutoPauseAsync(workDay);
    }

    public async Task ApplyAutoPauseAsync(WorkDay workDay)
    {
        var settings = await _database.GetSettingsAsync();

        if (!settings.AutoPauseEnabled)
        {
            workDay.AutoPauseMinutes = 0;
            return;
        }

        // Calculate gross work time (without pauses)
        var entries = await _database.GetTimeEntriesAsync(workDay.Id);
        var bruttoMinutes = 0;
        TimeEntry? lastCheckIn = null;

        foreach (var entry in entries.OrderBy(e => e.Timestamp))
        {
            if (entry.Type == EntryType.CheckIn)
                lastCheckIn = entry;
            else if (entry.Type == EntryType.CheckOut && lastCheckIn != null)
            {
                bruttoMinutes += (int)(entry.Timestamp - lastCheckIn.Timestamp).TotalMinutes;
                lastCheckIn = null;
            }
        }

        // Legally required pause
        var requiredPauseMinutes = settings.GetRequiredPauseMinutes(bruttoMinutes);

        // Difference to manual pauses
        var difference = requiredPauseMinutes - workDay.ManualPauseMinutes;

        if (difference > 0)
        {
            workDay.AutoPauseMinutes = difference;

            // Create PauseEntry for display
            var existingAutoPause = (await _database.GetPauseEntriesAsync(workDay.Id))
                .FirstOrDefault(p => p.IsAutoPause);

            if (existingAutoPause == null && workDay.LastCheckOut != null)
            {
                var autoPause = new PauseEntry
                {
                    WorkDayId = workDay.Id,
                    StartTime = workDay.LastCheckOut.Value.AddMinutes(-difference),
                    EndTime = workDay.LastCheckOut.Value,
                    Type = PauseType.Auto,
                    IsAutoPause = true,
                    Note = AppStrings.AutoPauseLegal
                };
                await _database.SavePauseEntryAsync(autoPause);
            }
            else if (existingAutoPause != null)
            {
                if (workDay.LastCheckOut != null)
                {
                    existingAutoPause.StartTime = workDay.LastCheckOut.Value.AddMinutes(-difference);
                    existingAutoPause.EndTime = workDay.LastCheckOut.Value;
                    await _database.SavePauseEntryAsync(existingAutoPause);
                }
            }
        }
        else
        {
            workDay.AutoPauseMinutes = 0;

            // Remove existing auto-pause
            var existingAutoPause = (await _database.GetPauseEntriesAsync(workDay.Id))
                .FirstOrDefault(p => p.IsAutoPause);

            if (existingAutoPause != null)
            {
                await _database.DeletePauseEntryAsync(existingAutoPause.Id);
            }
        }
    }

    public async Task<WorkWeek> CalculateWeekAsync(DateTime dateInWeek)
    {
        var weekNumber = GetIsoWeekNumber(dateInWeek);
        var year = dateInWeek.Year;

        // Correction for year change
        if (weekNumber == 1 && dateInWeek.Month == 12)
            year++;
        else if (weekNumber >= 52 && dateInWeek.Month == 1)
            year--;

        var firstDay = GetFirstDayOfWeek(year, weekNumber);
        var lastDay = firstDay.AddDays(6);

        var settings = await _database.GetSettingsAsync();
        var workDays = await _database.GetWorkDaysAsync(firstDay, lastDay);

        var week = new WorkWeek
        {
            WeekNumber = weekNumber,
            Year = year,
            StartDate = DateOnly.FromDateTime(firstDay),
            EndDate = DateOnly.FromDateTime(lastDay),
            TargetWorkMinutes = settings.WeeklyMinutes
        };

        // Process days
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            var workDay = workDays.FirstOrDefault(d => d.Date.Date == date.Date);

            if (workDay == null)
            {
                var targetMinutes = settings.IsWorkDay(date.DayOfWeek) ? settings.DailyMinutes : 0;
                workDay = new WorkDay
                {
                    Date = date,
                    Status = settings.IsWorkDay(date.DayOfWeek) ? DayStatus.WorkDay : DayStatus.Weekend,
                    TargetWorkMinutes = targetMinutes,
                    ActualWorkMinutes = 0,
                    BalanceMinutes = -targetMinutes
                };
            }
            else
            {
                if (workDay.ActualWorkMinutes == 0 && workDay.BalanceMinutes == 0 && workDay.TargetWorkMinutes > 0)
                {
                    workDay.BalanceMinutes = -workDay.TargetWorkMinutes;
                }
            }

            week.Days.Add(workDay);

            // Statistics
            week.ActualWorkMinutes += workDay.ActualWorkMinutes;
            week.TotalPauseMinutes += workDay.ManualPauseMinutes + workDay.AutoPauseMinutes;

            if (workDay.ActualWorkMinutes > 0)
                week.WorkedDays++;

            switch (workDay.Status)
            {
                case DayStatus.Vacation:
                    week.VacationDays++;
                    break;
                case DayStatus.Sick:
                    week.SickDays++;
                    break;
                case DayStatus.Holiday:
                    week.HolidayDays++;
                    break;
            }
        }

        week.BalanceMinutes = week.ActualWorkMinutes - week.TargetWorkMinutes;

        return week;
    }

    public async Task<WorkMonth> CalculateMonthAsync(int year, int month)
    {
        var firstDay = new DateTime(year, month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);

        var settings = await _database.GetSettingsAsync();
        var workDays = await _database.GetWorkDaysAsync(firstDay, lastDay);

        var workMonth = new WorkMonth
        {
            Month = month,
            Year = year
        };

        // Calculate target work days
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            if (settings.IsWorkDay(date.DayOfWeek))
                workMonth.TargetWorkDays++;
        }

        workMonth.TargetWorkMinutes = workMonth.TargetWorkDays * settings.DailyMinutes;

        // Process days
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            var workDay = workDays.FirstOrDefault(d => d.Date.Date == date.Date);

            if (workDay == null)
            {
                var targetMinutes = settings.IsWorkDay(date.DayOfWeek) ? settings.DailyMinutes : 0;
                workDay = new WorkDay
                {
                    Date = date,
                    Status = settings.IsWorkDay(date.DayOfWeek) ? DayStatus.WorkDay : DayStatus.Weekend,
                    TargetWorkMinutes = targetMinutes,
                    ActualWorkMinutes = 0,
                    BalanceMinutes = -targetMinutes
                };
            }
            else
            {
                if (workDay.ActualWorkMinutes == 0 && workDay.BalanceMinutes == 0 && workDay.TargetWorkMinutes > 0)
                {
                    workDay.BalanceMinutes = -workDay.TargetWorkMinutes;
                }
            }

            workMonth.Days.Add(workDay);

            // Statistics
            workMonth.ActualWorkMinutes += workDay.ActualWorkMinutes;
            workMonth.TotalPauseMinutes += workDay.ManualPauseMinutes + workDay.AutoPauseMinutes;

            if (workDay.ActualWorkMinutes > 0)
                workMonth.WorkedDays++;

            switch (workDay.Status)
            {
                case DayStatus.Vacation:
                    workMonth.VacationDays++;
                    break;
                case DayStatus.Sick:
                    workMonth.SickDays++;
                    break;
                case DayStatus.Holiday:
                    workMonth.HolidayDays++;
                    break;
                case DayStatus.HomeOffice:
                    workMonth.HomeOfficeDays++;
                    break;
            }

            workMonth.IsLocked = workMonth.IsLocked || workDay.IsLocked;
        }

        workMonth.BalanceMinutes = workMonth.ActualWorkMinutes - workMonth.TargetWorkMinutes;

        // Calculate cumulative balance
        workMonth.CumulativeBalanceMinutes = await GetCumulativeBalanceAsync(lastDay);

        return workMonth;
    }

    public async Task<int> GetCumulativeBalanceAsync(DateTime upToDate)
    {
        return await _database.GetTotalOvertimeMinutesAsync(new DateTime(2020, 1, 1), upToDate);
    }

    public async Task<double> GetWeekProgressAsync()
    {
        var week = await CalculateWeekAsync(DateTime.Today);

        if (week.TargetWorkMinutes == 0)
            return 0;

        return Math.Min(100, (week.ActualWorkMinutes * 100.0) / week.TargetWorkMinutes);
    }

    public async Task<List<string>> CheckLegalComplianceAsync(WorkDay workDay)
    {
        var warnings = new List<string>();
        var settings = await _database.GetSettingsAsync();

        if (!settings.LegalComplianceEnabled)
            return warnings;

        // Maximale Arbeitszeit (10h nach ArbZG)
        if (workDay.ActualWorkMinutes > settings.MaxDailyHours * 60)
        {
            warnings.Add(string.Format(AppStrings.WarningDailyWorkTimeExceeds, settings.MaxDailyHours));
        }

        // Pausenregelung
        if (workDay.ActualWorkMinutes > 6 * 60 && workDay.ManualPauseMinutes + workDay.AutoPauseMinutes < 30)
        {
            warnings.Add(AppStrings.WarningMinPause30);
        }

        if (workDay.ActualWorkMinutes > 9 * 60 && workDay.ManualPauseMinutes + workDay.AutoPauseMinutes < 45)
        {
            warnings.Add(AppStrings.WarningMinPause45);
        }

        // Ruhezeit (11h zwischen Schichten)
        var yesterday = await _database.GetWorkDayAsync(workDay.Date.AddDays(-1));
        if (yesterday?.LastCheckOut != null && workDay.FirstCheckIn != null)
        {
            var restTime = workDay.FirstCheckIn.Value - yesterday.LastCheckOut.Value;
            if (restTime.TotalHours < settings.MinRestHours)
            {
                warnings.Add(string.Format(AppStrings.WarningRestTimeBelowMin, settings.MinRestHours));
            }
        }

        return warnings;
    }

    public int GetIsoWeekNumber(DateTime date)
    {
        var cal = CultureInfo.InvariantCulture.Calendar;
        var day = cal.GetDayOfWeek(date);

        // ISO 8601: Week starts on Monday
        if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
        {
            date = date.AddDays(3);
        }

        return cal.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }

    public DateTime GetFirstDayOfWeek(int year, int weekNumber)
    {
        // January 4th is always in week 1
        var jan4 = new DateTime(year, 1, 4);
        var daysOffset = DayOfWeek.Monday - jan4.DayOfWeek;

        var firstMonday = jan4.AddDays(daysOffset);
        var firstWeekDay = firstMonday.AddDays((weekNumber - 1) * 7);

        return firstWeekDay;
    }
}
