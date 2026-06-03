using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Fokussierter Datenbank-Zugriff für Backup/Restore (ISP-Cut: entkoppelt den
/// BackupService vom großen <see cref="IDatabaseService"/>). Wird von
/// <c>DatabaseService</c> mitimplementiert; die vier geteilten Getter spiegeln
/// die gleichnamigen IDatabaseService-Member.
/// </summary>
public interface IBackupDataAccess
{
    // Geteilte Getter (auch in IDatabaseService), die das Backup mitliest/-schreibt
    Task<WorkSettings> GetSettingsAsync();
    Task SaveSettingsAsync(WorkSettings settings);
    Task<List<Project>> GetProjectsAsync(bool includeInactive = false);
    Task<List<Employer>> GetEmployersAsync(bool includeInactive = false);

    // Vollständige Tabellen-Reads (ausschließlich Backup)
    Task<List<WorkDay>> GetAllWorkDaysAsync();
    Task<List<TimeEntry>> GetAllTimeEntriesAsync();
    Task<List<PauseEntry>> GetAllPauseEntriesAsync();
    Task<List<VacationEntry>> GetAllVacationEntriesAsync();
    Task<List<VacationQuota>> GetAllVacationQuotasAsync();
    Task<List<ShiftPattern>> GetAllShiftPatternsAsync();
    Task<List<ShiftAssignment>> GetAllShiftAssignmentsAsync();

    // Restore
    /// <summary>
    /// Löscht alle Daten aus allen Tabellen (für sauberes Restore).
    /// Settings werden nicht gelöscht (werden überschrieben).
    /// </summary>
    Task ClearAllDataAsync();

    /// <summary>
    /// Batch-Insert aller Backup-Daten in einer Transaction (5-10x schneller als einzelne SaveAsync-Aufrufe).
    /// Daten werden direkt eingefügt (keine Upsert-Logik, DB muss vorher geleert sein).
    /// </summary>
    Task BulkRestoreAsync(
        List<WorkDay>? workDays,
        List<TimeEntry>? timeEntries,
        List<PauseEntry>? pauseEntries,
        List<VacationEntry>? vacationEntries,
        List<VacationQuota>? vacationQuotas,
        List<Project>? projects,
        List<Employer>? employers,
        List<ShiftPattern>? shiftPatterns,
        List<ShiftAssignment>? shiftAssignments);
}
