using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Service for SQLite database operations
/// </summary>
public interface IDatabaseService
{
    // === Initialization ===
    Task InitializeAsync();

    // === WorkDay ===
    Task<WorkDay?> GetWorkDayAsync(DateTime date);
    Task<WorkDay> GetOrCreateWorkDayAsync(DateTime date);
    Task<List<WorkDay>> GetWorkDaysAsync(DateTime startDate, DateTime endDate);
    Task<int> SaveWorkDayAsync(WorkDay workDay);
    Task DeleteWorkDayAsync(int id);

    // === TimeEntry ===
    Task<List<TimeEntry>> GetTimeEntriesAsync(int workDayId);
    Task<Dictionary<int, List<TimeEntry>>> GetTimeEntriesForWorkDaysAsync(List<int> workDayIds);
    Task<TimeEntry?> GetTimeEntryByIdAsync(int id);
    Task<TimeEntry?> GetLastTimeEntryAsync(int workDayId);
    Task<int> SaveTimeEntryAsync(TimeEntry entry);
    Task DeleteTimeEntryAsync(int id);

    // === PauseEntry ===
    Task<List<PauseEntry>> GetPauseEntriesAsync(int workDayId);
    Task<PauseEntry?> GetActivePauseAsync(int workDayId);
    Task<int> SavePauseEntryAsync(PauseEntry entry);
    Task DeletePauseEntryAsync(int id);

    // === WorkSettings ===
    Task<WorkSettings> GetSettingsAsync();
    Task SaveSettingsAsync(WorkSettings settings);

    // === VacationEntry ===
    Task<List<VacationEntry>> GetVacationsAsync(int year);
    Task<List<VacationEntry>> GetVacationEntriesAsync(DateTime start, DateTime end);
    Task<VacationEntry?> GetVacationEntryAsync(int id);
    Task<int> SaveVacationAsync(VacationEntry vacation);
    Task<int> SaveVacationEntryAsync(VacationEntry entry);
    Task DeleteVacationAsync(int id);
    Task DeleteVacationEntryAsync(int id);

    // === VacationQuota ===
    Task<VacationQuota?> GetVacationQuotaAsync(int year, int? employerId = null);
    Task SaveVacationQuotaAsync(VacationQuota quota);

    // === HolidayEntry ===
    Task<List<HolidayEntry>> GetHolidaysAsync(int year, string region);
    Task SaveHolidaysAsync(List<HolidayEntry> holidays);
    Task<bool> IsHolidayAsync(DateTime date, string region);

    // === Project ===
    Task<List<Project>> GetProjectsAsync(bool includeInactive = false);
    Task<Project?> GetProjectAsync(int id);
    Task<int> SaveProjectAsync(Project project);
    Task DeleteProjectAsync(int id);

    // === ProjectTimeEntry ===
    Task<List<ProjectTimeEntry>> GetProjectTimeEntriesAsync(int projectId);
    Task<List<ProjectTimeEntry>> GetProjectTimeEntriesAsync(int projectId, DateTime startDate, DateTime endDate);
    Task<int> SaveProjectTimeEntryAsync(ProjectTimeEntry entry);
    Task DeleteProjectTimeEntryAsync(int id);

    // === Employer ===
    Task<List<Employer>> GetEmployersAsync(bool includeInactive = false);
    Task<Employer?> GetDefaultEmployerAsync();
    Task<int> SaveEmployerAsync(Employer employer);
    Task DeleteEmployerAsync(int id);
    Task SetDefaultEmployerAsync(int id);

    // === ShiftPattern ===
    Task<List<ShiftPattern>> GetShiftPatternsAsync();
    Task<int> SaveShiftPatternAsync(ShiftPattern pattern);
    Task DeleteShiftPatternAsync(int id);

    // === ShiftAssignment ===
    Task<List<ShiftAssignment>> GetShiftAssignmentsAsync(DateTime startDate, DateTime endDate);
    Task<ShiftAssignment?> GetShiftAssignmentAsync(DateTime date);
    Task<int> SaveShiftAssignmentAsync(ShiftAssignment assignment);
    Task DeleteShiftAssignmentAsync(int id);

    // === Month lock ===
    Task LockMonthAsync(int year, int month);
    Task UnlockMonthAsync(int year, int month);
    Task<bool> IsMonthLockedAsync(int year, int month);

    // === Statistics queries ===
    Task<DateTime?> GetFirstWorkDayDateAsync();
    Task<int> GetTotalWorkMinutesAsync(DateTime startDate, DateTime endDate);
    Task<int> GetTotalOvertimeMinutesAsync(DateTime startDate, DateTime endDate);
    Task<Dictionary<int, double>> GetProjectHoursAsync(DateTime startDate, DateTime endDate);

    // === Achievement ===
    Task CreateAchievementTableAsync();
    Task<List<Achievement>> GetAllAchievementsAsync();
    Task SaveAchievementAsync(Achievement achievement);

    // === Clear (für Restore) ===
    /// <summary>
    /// Löscht alle Daten aus allen Tabellen (für sauberes Restore).
    /// Settings werden nicht gelöscht (werden überschrieben).
    /// </summary>
    Task ClearAllDataAsync();

    // === Backup methods ===
    Task<List<WorkDay>> GetAllWorkDaysAsync();
    Task<List<TimeEntry>> GetAllTimeEntriesAsync();
    Task<List<TimeEntry>> GetTimeEntriesAsync(DateTime date);
    Task<List<PauseEntry>> GetAllPauseEntriesAsync();
    Task<List<VacationEntry>> GetAllVacationEntriesAsync();
    Task<List<VacationQuota>> GetAllVacationQuotasAsync();
}
