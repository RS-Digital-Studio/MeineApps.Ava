using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Implementation of the vacation management service
/// </summary>
public sealed class VacationService : IVacationService
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
        var entries = await GetVacationEntriesAsync(year);
        var (taken, planned) = await CalculateTakenAndPlannedAsync(entries);

        quota.TakenDays = taken;
        quota.PlannedDays = planned;

        // Verfall des Übertrags zum Stichtag (BUrlG: regulär 31.03.) — NUR in-memory für die
        // Anzeige, der persistierte Datensatz bleibt unangetastet. Nur der BIS ZUM STICHTAG
        // tatsächlich genutzte Übertrag bleibt erhalten — Urlaub nach dem Stichtag stammt
        // aus dem regulären Jahresanspruch und rettet den Übertrag nicht.
        var settings = await _database.GetSettingsAsync();
        if (quota.CarryOverDays > 0 && TryGetExpiredCarryOverDeadline(year, settings, out var deadline))
        {
            var takenUntilDeadline = await CalculateTakenUntilAsync(entries, deadline);
            quota.CarryOverDays = Math.Min(takenUntilDeadline, quota.CarryOverDays);
        }

        return quota;
    }

    /// <summary>
    /// Prüft, ob der Resturlaub-Übertrag für ein Jahr zum konfigurierten Stichtag verfallen ist,
    /// und liefert den Stichtag zurück.
    /// </summary>
    private static bool TryGetExpiredCarryOverDeadline(int year, WorkSettings settings, out DateTime deadline)
    {
        deadline = default;
        if (!settings.VacationCarryOverExpires)
            return false;

        // Stichtag-Datum robust bilden (ungültige Tag/Monat-Kombinationen abfangen)
        var month = Math.Clamp(settings.VacationCarryOverExpiryMonth, 1, 12);
        var maxDay = DateTime.DaysInMonth(year, month);
        var day = Math.Clamp(settings.VacationCarryOverExpiryDay, 1, maxDay);
        deadline = new DateTime(year, month, day);

        return DateTime.Today > deadline;
    }

    /// <summary>
    /// Zählt die bis einschließlich Stichtag genommenen Urlaubstage. Einträge, die komplett
    /// vor dem Stichtag liegen, nutzen die gespeicherten Tage; Einträge, die den Stichtag
    /// überspannen, werden bis zum Stichtag werktags-genau gezählt.
    /// </summary>
    private async Task<int> CalculateTakenUntilAsync(List<VacationEntry> entries, DateTime deadline)
    {
        var taken = 0;

        foreach (var e in entries.Where(e => e.Type == DayStatus.Vacation && e.StartDate.Date <= deadline))
        {
            if (e.EndDate.Date <= deadline)
            {
                taken += e.Days;
            }
            else
            {
                var days = await CalculateWorkDaysAsync(e.StartDate, deadline);
                taken += Math.Clamp(days, 0, Math.Max(0, e.Days));
            }
        }

        return taken;
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
        var (taken, planned) = await CalculateTakenAndPlannedAsync(entries);

        var stats = new VacationStatistics
        {
            Year = year,
            TotalDays = quota.TotalDays,
            CarryOverDays = quota.CarryOverDays,
            TakenDays = taken,
            PlannedDays = planned,
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

        // Übertrag NUR aus dem reinen Jahresanspruch des Vorjahres (TotalDays − TakenDays),
        // NICHT aus AvailableDays inkl. Vorjahres-Übertrag → verhindert Kompoundierung über
        // mehrere Jahre (sonst würde nie verfallender Rest endlos weitergeschleppt).
        var remaining = Math.Max(0, fromQuota.TotalDays - fromQuota.TakenDays);

        if (remaining <= 0)
            return 0;

        // Obergrenze anwenden (0 = unbegrenzt)
        var settings = await _database.GetSettingsAsync();
        if (settings.VacationMaxCarryOverDays > 0)
            remaining = Math.Min(remaining, settings.VacationMaxCarryOverDays);

        var toQuota = await GetQuotaAsync(toYear, employerId);
        toQuota.CarryOverDays = remaining;
        await SaveQuotaAsync(toQuota);

        return remaining;
    }

    #region Private Methods

    /// <summary>
    /// Berechnet genommene und geplante Urlaubstage aus Einträgen.
    /// Laufende Urlaube (StartDate &lt; heute UND EndDate >= heute) werden über die tatsächlichen
    /// Werktage zwischen Start und gestern aufgeteilt (statt über das Kalendertag-Verhältnis) —
    /// das ist wochenend-/feiertags-genau.
    /// </summary>
    private async Task<(int taken, int planned)> CalculateTakenAndPlannedAsync(List<VacationEntry> entries)
    {
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
            else if (e.Days > 0)
            {
                // Laufender Urlaub: bereits vergangene Werktage (bis einschließlich gestern) zählen
                var pastWorkDays = await CalculateWorkDaysAsync(e.StartDate, today.AddDays(-1));
                pastWorkDays = Math.Clamp(pastWorkDays, 0, e.Days);
                taken += pastWorkDays;
                planned += e.Days - pastWorkDays;
            }
        }

        return (taken, planned);
    }

    private async Task UpdateWorkDaysForVacationAsync(VacationEntry entry)
    {
        // Alle Tage im Zeitraum auf einmal laden (statt N einzelner Queries)
        var existingDays = await _database.GetWorkDaysAsync(entry.StartDate.Date, entry.EndDate.Date);
        var daysByDate = existingDays.ToDictionary(d => d.Date.Date);

        var current = entry.StartDate.Date;
        while (current <= entry.EndDate.Date)
        {
            WorkDay workDay;
            if (daysByDate.TryGetValue(current, out var existing))
            {
                workDay = existing;
            }
            else
            {
                workDay = await _database.GetOrCreateWorkDayAsync(current);
            }

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

        // Alle Tage im Zeitraum auf einmal laden (statt N einzelner Queries)
        var existingDays = await _database.GetWorkDaysAsync(entry.StartDate.Date, entry.EndDate.Date);
        var daysByDate = existingDays.ToDictionary(d => d.Date.Date);

        var current = entry.StartDate.Date;
        while (current <= entry.EndDate.Date)
        {
            WorkDay workDay;
            if (daysByDate.TryGetValue(current, out var existing))
            {
                workDay = existing;
            }
            else
            {
                workDay = await _database.GetOrCreateWorkDayAsync(current);
            }

            if (workDay.Status == entry.Type)
            {
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
