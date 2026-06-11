using System.Globalization;
using WorkTimePro.Helpers;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;

namespace WorkTimePro.Services;

/// <summary>
/// Service for calculations (work time, plus/minus, auto-pause)
/// </summary>
public sealed class CalculationService : ICalculationService
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
            workDay.UnroundedWorkMinutes = 0;
            workDay.FirstCheckIn = null;
            workDay.LastCheckOut = null;
            workDay.BalanceMinutes = -workDay.TargetWorkMinutes;
            await _database.SaveWorkDayAsync(workDay);
            return;
        }

        // Brutto-Arbeitszeit berechnen
        var (totalMinutes, firstCheckIn, lastCheckOut) = CalculateBruttoMinutes(entries);

        workDay.FirstCheckIn = firstCheckIn;
        workDay.LastCheckOut = lastCheckOut;

        // Pausen berechnen (Daten durchreichen statt erneut laden)
        RecalculatePauseTime(workDay, pauses);
        await ApplyAutoPauseAsync(workDay, entries, pauses, settings);

        // Netto-Arbeitszeit (Brutto - Pausen)
        var totalPauseMinutes = workDay.ManualPauseMinutes + workDay.AutoPauseMinutes;
        var netMinutes = Math.Max(0, totalMinutes - totalPauseMinutes);

        // Ungerundete Netto-Zeit als Grundlage für gesetzliche Prüfungen festhalten,
        // BEVOR die optionale Abrechnungs-Rundung greift (sonst verschiebt RoundingMinutes
        // den §3-ArbZG-6-Monats-Durchschnitt).
        workDay.UnroundedWorkMinutes = netMinutes;

        // Zeitrundung anwenden (falls konfiguriert)
        if (settings.RoundingMinutes > 0)
        {
            // Kaufmännisch runden (AwayFromZero) — Banker's Rounding (.5 → gerade) wäre
            // bei Arbeitszeit-Abrechnung überraschend (30,5 min → 30 statt 31).
            netMinutes = (int)(Math.Round((double)netMinutes / settings.RoundingMinutes, MidpointRounding.AwayFromZero) * settings.RoundingMinutes);
        }

        workDay.ActualWorkMinutes = netMinutes;

        // Saldo berechnen
        workDay.BalanceMinutes = workDay.ActualWorkMinutes - workDay.TargetWorkMinutes;

        await _database.SaveWorkDayAsync(workDay);
    }

    public async Task RecalculatePauseTimeAsync(WorkDay workDay)
    {
        // Daten EINMAL laden und durchreichen — vermeidet Doppel-Load
        // (vorher: pauses 2× geladen + entries/settings nochmal in ApplyAutoPauseAsync)
        var pauses = await _database.GetPauseEntriesAsync(workDay.Id);
        var entries = await _database.GetTimeEntriesAsync(workDay.Id);
        var settings = await _database.GetSettingsAsync();
        RecalculatePauseTime(workDay, pauses);
        await ApplyAutoPauseAsync(workDay, entries, pauses, settings);
    }

    /// <summary>
    /// Berechnet manuelle Pausenzeit aus bereits geladenen Daten (kein DB-Zugriff).
    /// </summary>
    private static void RecalculatePauseTime(WorkDay workDay, List<PauseEntry> pauses)
    {
        var manualMinutes = pauses
            .Where(p => !p.IsAutoPause && p.EndTime != null)
            .Sum(p => (int)Math.Round(p.Duration.TotalMinutes, MidpointRounding.AwayFromZero));

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
        var (bruttoMinutes, _, _) = CalculateBruttoMinutes(entries);

        // Laufende Arbeitszeit berücksichtigen (noch eingecheckt, kein CheckOut)
        var lastCheckIn = entries.LastOrDefault(e => e.Type == EntryType.CheckIn);
        var lastCheckOut = entries.LastOrDefault(e => e.Type == EntryType.CheckOut);
        if (lastCheckIn != null && (lastCheckOut == null || lastCheckIn.Timestamp > lastCheckOut.Timestamp))
        {
            bruttoMinutes += (int)Math.Round(DurationMath.RealElapsedMinutes(lastCheckIn.Timestamp, DateTime.Now), MidpointRounding.AwayFromZero);
        }

        // Gesetzlich vorgeschriebene Pause — §4 ArbZG bemisst die Pausenstaffel an der
        // ARBEITSZEIT (netto), nicht an der Brutto-Präsenz. Bereits erfasste manuelle
        // Pausen daher abziehen, sonst wird in Randfällen fälschlich eine Auto-Pause ergänzt.
        var netWorkMinutes = Math.Max(0, bruttoMinutes - workDay.ManualPauseMinutes);
        var requiredPauseMinutes = settings.GetRequiredPauseMinutes(netWorkMinutes);
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

        // Lookup-Dictionary statt O(n)-FirstOrDefault pro Tag: spart 30+ Compares pro Woche,
        // wird relevant bei Statistics-Year-Range mit 365 Tagen.
        var workDaysByDate = workDays.ToDictionary(d => d.Date.Date);

        // Process days
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            workDaysByDate.TryGetValue(date.Date, out var workDay);

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
            // Legacy-Migration läuft einmalig beim DB-Init (siehe DatabaseService.InitializeDatabaseAsync)

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

        // Lookup-Dictionary statt O(n)-FirstOrDefault pro Tag
        var workDaysByDate = workDays.ToDictionary(d => d.Date.Date);

        // Process days
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            workDaysByDate.TryGetValue(date.Date, out var workDay);

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
            // Legacy-Migration läuft einmalig beim DB-Init (siehe DatabaseService.InitializeDatabaseAsync)

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
        var settings = await _database.GetSettingsAsync();
        return await GetWeekProgressAsync(settings);
    }

    public async Task<double> GetWeekProgressAsync(WorkSettings settings)
    {
        // Direkte Berechnung statt volle Woche laden
        var today = DateTime.Today;
        var dayOfWeek = ((int)today.DayOfWeek + 6) % 7;
        var monday = today.AddDays(-dayOfWeek);
        var sunday = monday.AddDays(6);

        var workDays = await _database.GetWorkDaysAsync(monday, sunday);

        var actualMinutes = workDays.Sum(d => d.ActualWorkMinutes);
        var targetMinutes = 0;
        for (var d = monday; d <= sunday; d = d.AddDays(1))
        {
            if (settings.IsWorkDay(d.DayOfWeek))
                targetMinutes += settings.GetDailyMinutesForDay(d.DayOfWeek);
        }

        if (targetMinutes == 0)
            return 0;

        return Math.Min(100, (actualMinutes * 100.0) / targetMinutes);
    }

    public async Task<List<string>> CheckLegalComplianceAsync(WorkDay workDay)
    {
        var warnings = new List<string>();
        var settings = await _database.GetSettingsAsync();

        if (!settings.LegalComplianceEnabled)
            return warnings;

        // Gesetzliche Prüfungen auf der UNGERUNDETEN Netto-Zeit (RoundingMinutes ist eine
        // reine Abrechnungs-Rundung und darf die ArbZG-Bewertung nicht verschieben).
        var legalMinutes = workDay.UnroundedWorkMinutes;

        // Maximale Arbeitszeit (10h nach ArbZG)
        if (legalMinutes > settings.MaxDailyHours * 60)
        {
            warnings.Add(string.Format(AppStrings.WarningDailyWorkTimeExceeds, settings.MaxDailyHours));
        }

        // §3 ArbZG: Durchschnitt der WERKTÄGLICHEN Arbeitszeit über 6 Monate
        // (bzw. 24 Wochen) darf 8h nicht überschreiten. "Werktag" = Mo-Sa.
        // Vacation, Sick, Feiertage zählen als Werktag mit 0h Arbeitszeit
        // (Tage werden mitgezählt, gearbeitete Minuten = 0).
        // Sonntage werden NICHT mitgezählt (kein Werktag im Sinne des ArbZG).
        if (legalMinutes > 8 * 60)
        {
            var sixMonthsAgo = workDay.Date.AddMonths(-6);
            var recentDays = await _database.GetWorkDaysAsync(sixMonthsAgo, workDay.Date);
            var dayMap = recentDays.ToDictionary(d => d.Date.Date);

            long totalMinutes = 0;
            int weekdayCount = 0;
            for (var d = sixMonthsAgo.Date; d <= workDay.Date.Date; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Sunday) continue; // §3 ArbZG: Werktage Mo-Sa
                weekdayCount++;
                if (dayMap.TryGetValue(d, out var entry))
                    totalMinutes += entry.UnroundedWorkMinutes; // Bei Vacation/Sick = 0
            }

            // Mindest-Datengrundlage ~10 Wochen Werktage, sonst keine aussagekräftige Aussage
            if (weekdayCount >= 60)
            {
                var avgMinutes = (double)totalMinutes / weekdayCount;
                if (avgMinutes > 8 * 60)
                {
                    var avgHours = avgMinutes / 60.0;
                    warnings.Add(string.Format(
                        AppStrings.WarningSixMonthAvgExceeds ?? "§3 ArbZG: 6-Monats-Durchschnitt {0:F1}h/Tag übersteigt 8h-Grenze",
                        avgHours));
                }
            }
        }

        // Pausenregelung (§4 ArbZG — ebenfalls auf ungerundeter Arbeitszeit)
        if (legalMinutes > 6 * 60 && workDay.ManualPauseMinutes + workDay.AutoPauseMinutes < 30)
        {
            warnings.Add(AppStrings.WarningMinPause30);
        }

        if (legalMinutes > 9 * 60 && workDay.ManualPauseMinutes + workDay.AutoPauseMinutes < 45)
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

    /// <summary>
    /// Berechnet Brutto-Arbeitsminuten aus CheckIn/CheckOut-Paaren.
    /// Entries müssen nach Timestamp sortiert sein (DB liefert bereits sortiert).
    /// </summary>
    private static (int totalMinutes, DateTime? firstCheckIn, DateTime? lastCheckOut) CalculateBruttoMinutes(List<TimeEntry> entries)
    {
        var totalMinutes = 0;
        TimeEntry? lastCheckIn = null;
        DateTime? firstCheckIn = null;
        DateTime? lastCheckOut = null;

        foreach (var entry in entries)
        {
            if (entry.Type == EntryType.CheckIn)
            {
                lastCheckIn = entry;
                firstCheckIn ??= entry.Timestamp;
            }
            else if (entry.Type == EntryType.CheckOut && lastCheckIn != null)
            {
                // DST-bewusst: tatsächlich verstrichene Zeit (korrigiert Sommer-/Winterzeit-Sprung
                // bei über die Umstellung laufenden Schichten).
                totalMinutes += (int)Math.Round(DurationMath.RealElapsedMinutes(lastCheckIn.Timestamp, entry.Timestamp), MidpointRounding.AwayFromZero);
                lastCheckOut = entry.Timestamp;
                lastCheckIn = null;
            }
        }

        return (totalMinutes, firstCheckIn, lastCheckOut);
    }
}
