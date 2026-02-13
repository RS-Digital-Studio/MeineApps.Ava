using ZeitManager.Models;

namespace ZeitManager.Services;

public class ShiftScheduleService : IShiftScheduleService
{
    private readonly IDatabaseService _database;

    public ShiftScheduleService(IDatabaseService database)
    {
        _database = database;
    }

    public Task<ShiftSchedule?> GetActiveScheduleAsync() => _database.GetActiveScheduleAsync();

    public Task<List<ShiftSchedule>> GetAllSchedulesAsync() => _database.GetShiftSchedulesAsync();

    public async Task SaveScheduleAsync(ShiftSchedule schedule)
    {
        await _database.SaveShiftScheduleAsync(schedule);
    }

    public async Task DeleteScheduleAsync(ShiftSchedule schedule)
    {
        await _database.DeleteShiftScheduleAsync(schedule);
    }

    public async Task ActivateScheduleAsync(ShiftSchedule schedule)
    {
        // Deactivate all others first
        var all = await _database.GetShiftSchedulesAsync();
        foreach (var s in all.Where(s => s.IsActive))
        {
            s.IsActive = false;
            await _database.SaveShiftScheduleAsync(s);
        }

        schedule.IsActive = true;
        await _database.SaveShiftScheduleAsync(schedule);
    }

    public async Task DeactivateScheduleAsync(ShiftSchedule schedule)
    {
        schedule.IsActive = false;
        await _database.SaveShiftScheduleAsync(schedule);
    }

    public Task<List<ShiftException>> GetExceptionsAsync(int scheduleId) =>
        _database.GetShiftExceptionsAsync(scheduleId);

    public Task SaveExceptionAsync(ShiftException exception) =>
        _database.SaveShiftExceptionAsync(exception);

    public Task DeleteExceptionAsync(ShiftException exception) =>
        _database.DeleteShiftExceptionAsync(exception);

    public ShiftType GetShiftForDate(ShiftSchedule schedule, DateOnly date)
    {
        return GetShiftForDate(schedule, date, []);
    }

    public ShiftType GetShiftForDate(ShiftSchedule schedule, DateOnly date, List<ShiftException> exceptions)
    {
        // Check exceptions first
        var exception = exceptions.FirstOrDefault(e => e.DateValue == date);
        if (exception != null)
        {
            return exception.NewShiftType ?? ShiftType.Free;
        }

        var startDate = schedule.StartDateValue;
        var daysSinceStart = date.DayNumber - startDate.DayNumber;
        // 36400 ist durch 7 (15-Schicht) UND 10 (21-Schicht) teilbar → korrekte Zyklusberechnung
        if (daysSinceStart < 0) daysSinceStart += 36400;

        return schedule.PatternType switch
        {
            ShiftPatternType.FifteenShift => GetFifteenShift(daysSinceStart, schedule.ShiftGroupNumber),
            ShiftPatternType.TwentyOneShift => GetTwentyOneShift(daysSinceStart, schedule.ShiftGroupNumber),
            _ => ShiftType.Free
        };
    }

    /// <summary>
    /// 15-shift pattern: Mon-Fri, 3 groups
    /// Week pattern per group: E E L L N N - - -  (9 days, but only Mon-Fri active)
    /// Simplified: 3 groups, 5 weekdays, rotating early/late/night weekly
    /// </summary>
    private static ShiftType GetFifteenShift(int daysSinceStart, int group)
    {
        // 15-shift: 3 weeks cycle, 3 groups
        var weekNumber = daysSinceStart / 7;
        var dayOfWeek = daysSinceStart % 7;

        // Only Mon-Fri (0-4 = Mon-Fri in the pattern)
        if (dayOfWeek >= 5) return ShiftType.Free;

        var cycleWeek = (weekNumber + (group - 1)) % 3;
        return cycleWeek switch
        {
            0 => ShiftType.Early,
            1 => ShiftType.Late,
            2 => ShiftType.Night,
            _ => ShiftType.Free
        };
    }

    /// <summary>
    /// 21-shift pattern: 24/7, 5 groups
    /// Pattern per group: E E L L N N - - - - (10 days cycle)
    /// Groups offset by 2 days
    /// </summary>
    private static ShiftType GetTwentyOneShift(int daysSinceStart, int group)
    {
        var cycleLength = 10; // 2E + 2L + 2N + 4Free
        var groupOffset = (group - 1) * 2;
        var dayInCycle = (daysSinceStart + groupOffset) % cycleLength;

        return dayInCycle switch
        {
            0 or 1 => ShiftType.Early,
            2 or 3 => ShiftType.Late,
            4 or 5 => ShiftType.Night,
            _ => ShiftType.Free
        };
    }
}
