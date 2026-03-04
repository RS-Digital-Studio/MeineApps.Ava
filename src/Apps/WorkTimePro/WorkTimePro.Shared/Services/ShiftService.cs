using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Implementation of the shift schedule service
/// </summary>
public sealed class ShiftService : IShiftService
{
    private readonly IDatabaseService _database;

    public ShiftService(IDatabaseService database)
    {
        _database = database;
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
    }

    public async Task<bool> IsWithinShiftAsync(DateTime time)
    {
        var assignment = await GetShiftAssignmentAsync(time.Date);
        if (assignment?.ShiftPattern == null)
            return true; // No shift = always allowed

        var currentTime = TimeOnly.FromDateTime(time);
        var shiftStart = assignment.ShiftPattern.StartTime;
        var shiftEnd = assignment.ShiftPattern.EndTime;

        // Normal shift (e.g. 6:00-14:00)
        if (shiftStart < shiftEnd)
        {
            return currentTime >= shiftStart && currentTime <= shiftEnd;
        }
        // Night shift (e.g. 22:00-6:00)
        else
        {
            return currentTime >= shiftStart || currentTime <= shiftEnd;
        }
    }

    public async Task<int> CalculateTargetMinutesAsync(DateTime date)
    {
        var assignment = await GetShiftAssignmentAsync(date);
        if (assignment?.ShiftPattern == null)
        {
            // Fallback to default settings
            var settings = await _database.GetSettingsAsync();
            return settings.IsWorkDay(date.DayOfWeek) ? settings.DailyMinutes : 0;
        }

        // Shift-based calculation
        if (assignment.ShiftPattern.Type == ShiftType.Off)
            return 0;

        return (int)assignment.ShiftPattern.WorkDuration.TotalMinutes;
    }
}
