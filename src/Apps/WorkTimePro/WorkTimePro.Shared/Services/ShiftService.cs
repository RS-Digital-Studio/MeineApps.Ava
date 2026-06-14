using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Implementation of the shift schedule service
/// </summary>
public sealed class ShiftService : IShiftService
{
    private readonly IDatabaseService _database;
    private readonly ICalculationService _calculation;

    public ShiftService(IDatabaseService database, ICalculationService calculation)
    {
        _database = database;
        _calculation = calculation;
    }

    public async Task<List<ShiftPattern>> GetShiftPatternsAsync()
    {
        return await _database.GetShiftPatternsAsync();
    }

    public async Task SaveShiftPatternAsync(ShiftPattern pattern)
    {
        await _database.SaveShiftPatternAsync(pattern);
    }

    public async Task DeleteShiftPatternAsync(int id)
    {
        await _database.DeleteShiftPatternAsync(id);
    }

    public async Task<ShiftAssignment?> GetShiftAssignmentAsync(DateTime date)
    {
        return await _database.GetShiftAssignmentAsync(date);
    }

    public async Task<List<ShiftAssignment>> GetShiftAssignmentsAsync(DateTime start, DateTime end)
    {
        return await _database.GetShiftAssignmentsAsync(start, end);
    }

    public async Task AssignShiftAsync(DateTime date, int shiftPatternId)
    {
        var existing = await GetShiftAssignmentAsync(date);
        if (existing != null)
        {
            existing.ShiftPatternId = shiftPatternId;
            await _database.SaveShiftAssignmentAsync(existing);
        }
        else
        {
            var assignment = new ShiftAssignment
            {
                Date = date.Date,
                ShiftPatternId = shiftPatternId
            };
            await _database.SaveShiftAssignmentAsync(assignment);
        }

        await RefreshWorkDayTargetAsync(date);
    }

    public async Task GenerateWeekScheduleAsync(DateTime weekStart, List<int?> shiftPatternIds)
    {
        if (shiftPatternIds.Count != 7)
            throw new ArgumentException("Exactly 7 shift pattern IDs (for Mon-Sun) must be provided");

        for (int i = 0; i < 7; i++)
        {
            var date = weekStart.AddDays(i);
            var patternId = shiftPatternIds[i];

            if (patternId.HasValue)
            {
                await AssignShiftAsync(date, patternId.Value);
            }
            else
            {
                await RemoveShiftAssignmentAsync(date);
            }
        }
    }

    public async Task RemoveShiftAssignmentAsync(DateTime date)
    {
        var existing = await GetShiftAssignmentAsync(date);
        if (existing != null)
        {
            await _database.DeleteShiftAssignmentAsync(existing.Id);
        }

        await RefreshWorkDayTargetAsync(date);
    }

    /// <summary>
    /// Aktualisiert das Tages-Soll eines bereits angelegten WorkDays, wenn sich seine
    /// Schichtzuweisung geändert hat. Vorrang: manuelle Abwesenheit (Urlaub/Krank/…) und
    /// Feiertag bleiben unangetastet; sonst überschreibt eine zugewiesene Schicht das Soll
    /// (Off=0, macht einen sonst freien Tag zum Arbeitstag), ohne Zuweisung gilt wieder das
    /// Wochentag-Soll. Existiert noch kein WorkDay, setzt GetOrCreateWorkDayAsync das Soll
    /// bei der Anlage korrekt — dann ist hier nichts zu tun.
    /// </summary>
    private async Task RefreshWorkDayTargetAsync(DateTime date)
    {
        var workDay = await _database.GetWorkDayAsync(date);
        if (workDay == null) return;
        if (workDay.Status is not (DayStatus.WorkDay or DayStatus.Weekend)) return;

        var settings = await _database.GetSettingsAsync();
        if (await _database.IsHolidayAsync(date, settings.HolidayRegion)) return;

        var assignment = await GetShiftAssignmentAsync(date);
        if (assignment?.ShiftPattern != null)
        {
            workDay.TargetWorkMinutes = assignment.ShiftPattern.Type == ShiftType.Off
                ? 0
                : (int)assignment.ShiftPattern.WorkDuration.TotalMinutes;
            workDay.Status = DayStatus.WorkDay;
        }
        else
        {
            var isWork = settings.IsWorkDay(date.DayOfWeek);
            workDay.Status = isWork ? DayStatus.WorkDay : DayStatus.Weekend;
            workDay.TargetWorkMinutes = isWork ? settings.GetDailyMinutesForDay(date.DayOfWeek) : 0;
        }

        await _calculation.RecalculateWorkDayAsync(workDay);
    }
}
