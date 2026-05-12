using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Service for calculations (work time, plus/minus, auto-pause)
/// </summary>
public interface ICalculationService
{
    /// <summary>
    /// Recalculate work day (work time, pauses, balance)
    /// </summary>
    Task RecalculateWorkDayAsync(WorkDay workDay);

    /// <summary>
    /// Recalculate pause time (including auto-pause)
    /// </summary>
    Task RecalculatePauseTimeAsync(WorkDay workDay);

    /// <summary>
    /// Calculate and apply auto-pause if needed
    /// </summary>
    Task ApplyAutoPauseAsync(WorkDay workDay);

    /// <summary>
    /// Calculate week summary
    /// </summary>
    Task<WorkWeek> CalculateWeekAsync(DateTime dateInWeek);

    /// <summary>
    /// Calculate month summary
    /// </summary>
    Task<WorkMonth> CalculateMonthAsync(int year, int month);

    /// <summary>
    /// Calculate cumulative balance up to a date
    /// </summary>
    Task<int> GetCumulativeBalanceAsync(DateTime upToDate);

    /// <summary>
    /// Calculate week progress
    /// </summary>
    Task<double> GetWeekProgressAsync();

    /// <summary>
    /// Variante mit bereits geladenen Settings — vermeidet doppelten DB-Round-Trip
    /// wenn der Aufrufer die Settings sowieso schon hält.
    /// </summary>
    Task<double> GetWeekProgressAsync(WorkSettings settings);

    /// <summary>
    /// Check legal compliance (German labor law)
    /// </summary>
    Task<List<string>> CheckLegalComplianceAsync(WorkDay workDay);

    /// <summary>
    /// Calculate ISO 8601 week number
    /// </summary>
    int GetIsoWeekNumber(DateTime date);

    /// <summary>
    /// Get the first day of an ISO week
    /// </summary>
    DateTime GetFirstDayOfWeek(int year, int weekNumber);
}
