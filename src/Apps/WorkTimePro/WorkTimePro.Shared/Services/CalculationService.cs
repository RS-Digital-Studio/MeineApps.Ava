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
        // Alle Daten einmal laden und durchreichen (statt mehrfacher DB-Queries)
        var entries = await _database.GetTimeEntriesAsync(workDay.Id);
        var pauses = await _database.GetPauseEntriesAsync(workDay.Id);
        var settings = await _database.GetSettingsAsync();

        if (entries.Count == 0)
        {
            workDay.ActualWorkMinutes = 0;
            workDay.FirstCheckIn = null;
            workDay.LastCheckOut = null;
            workDay.BalanceMinutes = -workDay.TargetWorkMinutes;
            await _database.SaveWorkDayAsync(workDay);
            return;
        }

        // Brutto-Arbeitszeit berechnen
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

        // Pausen berechnen (Daten durchreichen statt erneut laden)
        RecalculatePauseTime(workDay, pauses);
        await ApplyAutoPauseAsync(workDay, entries, pauses, settings);

        // Netto-Arbeitszeit (Brutto - Pausen)
        var totalPauseMinutes = workDay.ManualPauseMinutes + workDay.AutoPauseMinutes;
        var netMinutes = Math.Max(0, totalMinutes - totalPauseMinutes);

        // Zeitrundung anwenden (falls konfiguriert)
        if (settings.RoundingMinutes > 0)
        {
            netMinutes = (int)(Math.Round((double)netMinutes / settings.RoundingMinutes) * settings.RoundingMinutes);
        }

        workDay.ActualWorkMinutes = netMinutes;

        // Saldo berechnen
        workDay.BalanceMinutes = workDay.ActualWorkMinutes - workDay.TargetWorkMinutes;

        await _database.SaveWorkDayAsync(workDay);
    }

    public async Task RecalculatePauseTimeAsync(WorkDay workDay)
    {
        var pauses = await _database.GetPauseEntriesAsync(workDay.Id);
        RecalculatePauseTime(workDay, pauses);
        await ApplyAutoPauseAsync(workDay);
    }

    /// <summary>
    /// Berechnet manuelle Pausenzeit aus bereits geladenen Daten (kein DB-Zugriff).
    /// </summary>
    private static void RecalculatePauseTime(WorkDay workDay, List<PauseEntry> pauses)
    {
        var manualMinutes = pauses
            .Where(p => !p.IsAutoPause && p.EndTime != null)
            .Sum(p => (int)p.Duration.TotalMinutes);

        workDay.ManualPauseMinutes = manualMinutes;
    }

    public async Task ApplyAutoPauseAsync(WorkDay workDay)
    {
        var settings = await _database.GetSettingsAsync();
        var entries = await _database.GetTimeEntriesAsync(workDay.Id);
        var pauses = await _database.GetPauseEntriesAsync(workDay.Id);
        await ApplyAutoPauseAsync(workDay, entries, pauses, settings);
    }

    /// <summary>
    /// Auto-Pause anwenden mit bereits geladenen Daten (kein DB-Zugriff für Entries/Settings/Pauses).
    /// Reduziert DB-Queries von 7 auf 0 wenn aus RecalculateWorkDayAsync aufgerufen.
    /// </summary>
    private async Task ApplyAutoPauseAsync(WorkDay workDay, List<TimeEntry> entries, List<PauseEntry> pauses, WorkSettings settings)
    {
        if (!settings.AutoPauseEnabled)
        {
            workDay.AutoPauseMinutes = 0;
            return;
        }

        // Brutto-Arbeitszeit berechnen (ohne Pausen)
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

        // Laufende Arbeitszeit berücksichtigen (noch eingecheckt, kein CheckOut)
        if (lastCheckIn != null)
        {
            bruttoMinutes += (int)(DateTime.Now - lastCheckIn.Timestamp).TotalMinutes;
        }

        // Gesetzlich vorgeschriebene Pause
        var requiredPauseMinutes = settings.GetRequiredPauseMinutes(bruttoMinutes);
        var difference = requiredPauseMinutes - workDay.ManualPauseMinutes;
        var existingAutoPause = pauses.FirstOrDefault(p => p.IsAutoPause);

        if (difference > 0)
        {
            workDay.AutoPauseMinutes = difference;

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
            else if (existingAutoPause != null && workDay.LastCheckOut != null)
            {
                existingAutoPause.StartTime = workDay.LastCheckOut.Value.AddMinutes(-difference);
                existingAutoPause.EndTime = workDay.LastCheckOut.Value;
                await _database.SavePauseEntryAsync(existingAutoPause);
            }
        }
        else
        {
            workDay.AutoPauseMinutes = 0;

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

        // Wochen-Soll berechnen: individuelle Tagesstunden berücksichtigen
        var weekTargetMinutes = 0;
        for (var d = firstDay; d <= lastDay; d = d.AddDays(1))
        {
            if (settings.IsWorkDay(d.DayOfWeek))
                weekTargetMinutes += settings.GetDailyMinutesForDay(d.DayOfWeek);
        }

        var week = new WorkWeek
        {
            WeekNumber = weekNumber,
            Year = year,
            StartDate = DateOnly.FromDateTime(firstDay),
            EndDate = DateOnly.FromDateTime(lastDay),
            TargetWorkMinutes = weekTargetMinutes
        };

        // Process days
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            var workDay = workDays.FirstOrDefault(d => d.Date.Date == date.Date);

            if (workDay == null)
            {
                var targetMinutes = settings.IsWorkDay(date.DayOfWeek) ? settings.GetDailyMinutesForDay(date.DayOfWeek) : 0;
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

        // Monats-Soll berechnen: individuelle Tagesstunden berücksichtigen
        var monthTargetMinutes = 0;
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            if (settings.IsWorkDay(date.DayOfWeek))
            {
                workMonth.TargetWorkDays++;
                monthTargetMinutes += settings.GetDailyMinutesForDay(date.DayOfWeek);
            }
        }

        workMonth.TargetWorkMinutes = monthTargetMinutes;

        // Process days
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            var workDay = workDays.FirstOrDefault(d => d.Date.Date == date.Date);

            if (workDay == null)
            {
                var targetMinutes = settings.IsWorkDay(date.DayOfWeek) ? settings.GetDailyMinutesForDay(date.DayOfWeek) : 0;
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
        // Ersten WorkDay als Startdatum nutzen statt hardcoded 2020
        var firstWorkDay = await _database.GetFirstWorkDayDateAsync();
        var startDate = firstWorkDay ?? upToDate.AddYears(-1);
        return await _database.GetTotalOvertimeMinutesAsync(startDate, upToDate);
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
