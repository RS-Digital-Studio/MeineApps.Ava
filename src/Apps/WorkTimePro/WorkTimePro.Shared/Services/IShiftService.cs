using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Service for shift schedule management (Premium feature)
/// </summary>
public interface IShiftService
{
    /// <summary>
    /// Get all shift patterns
    /// </summary>
    Task<List<ShiftPattern>> GetShiftPatternsAsync();

    /// <summary>
    /// Save shift pattern
    /// </summary>
    Task SaveShiftPatternAsync(ShiftPattern pattern);

    /// <summary>
    /// Delete shift pattern
    /// </summary>
    Task DeleteShiftPatternAsync(int id);

    /// <summary>
    /// Get shift assignment for a date
    /// </summary>
    Task<ShiftAssignment?> GetShiftAssignmentAsync(DateTime date);

    /// <summary>
    /// Get shift assignments for a period
    /// </summary>
    Task<List<ShiftAssignment>> GetShiftAssignmentsAsync(DateTime start, DateTime end);

    /// <summary>
    /// Assign shift to a date
    /// </summary>
    Task AssignShiftAsync(DateTime date, int shiftPatternId);

    /// <summary>
    /// Generate week schedule (repeating pattern)
    /// </summary>
    Task GenerateWeekScheduleAsync(DateTime weekStart, List<int?> shiftPatternIds);

    /// <summary>
    /// Remove shift assignment
    /// </summary>
    Task RemoveShiftAssignmentAsync(DateTime date);
}
