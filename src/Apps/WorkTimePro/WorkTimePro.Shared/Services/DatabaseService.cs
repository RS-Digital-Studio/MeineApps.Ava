using SQLite;
using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// SQLite database service for WorkTime Pro
/// </summary>
public class DatabaseService : IDatabaseService
{
    private SQLiteAsyncConnection? _database;
    private readonly string _dbPath;
    private Task<SQLiteAsyncConnection>? _initTask;
    private readonly object _initLock = new();

    public DatabaseService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WorkTimePro");
        Directory.CreateDirectory(appDataDir);
        _dbPath = Path.Combine(appDataDir, "worktimepro.db3");
    }

    private Task<SQLiteAsyncConnection> GetDatabaseAsync()
    {
        // Alle Aufrufer warten auf denselben Task - keine Race-Condition möglich.
        // Bei Fehler wird der Task zurückgesetzt, damit ein Retry möglich ist.
        lock (_initLock)
        {
            if (_initTask != null && _initTask.IsFaulted)
                _initTask = null;
            _initTask ??= InitializeDatabaseAsync();
        }
        return _initTask!;
    }

    private async Task<SQLiteAsyncConnection> InitializeDatabaseAsync()
    {
        _database = new SQLiteAsyncConnection(_dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

        // Tabellen erstellen
        await _database.CreateTableAsync<WorkDay>();
        await _database.CreateTableAsync<TimeEntry>();
        await _database.CreateTableAsync<PauseEntry>();
        await _database.CreateTableAsync<WorkSettings>();
        await _database.CreateTableAsync<VacationEntry>();
        await _database.CreateTableAsync<VacationQuota>();
        await _database.CreateTableAsync<HolidayEntry>();
        await _database.CreateTableAsync<Project>();
        await _database.CreateTableAsync<ProjectTimeEntry>();
        await _database.CreateTableAsync<Employer>();
        await _database.CreateTableAsync<ShiftPattern>();
        await _database.CreateTableAsync<ShiftAssignment>();

        // Indizes für häufig abgefragte FK-Spalten (Tabellennamen müssen mit [Table]-Attribut übereinstimmen!)
        await _database.ExecuteAsync("CREATE UNIQUE INDEX IF NOT EXISTS idx_workday_date ON WorkDays(Date)");
        await _database.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_timeentry_workdayid ON TimeEntries(WorkDayId)");
        await _database.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_pauseentry_workdayid ON PauseEntries(WorkDayId)");
        await _database.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_vacation_year ON VacationEntries(Year)");
        await _database.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_shiftassignment_date ON ShiftAssignments(Date)");

        return _database;
    }

    public async Task InitializeAsync()
    {
        await GetDatabaseAsync();
    }

    // ==================== WorkDay ====================

    public async Task<WorkDay?> GetWorkDayAsync(DateTime date)
    {
        var db = await GetDatabaseAsync();
        var dateOnly = date.Date;
        return await db.Table<WorkDay>()
            .Where(w => w.Date == dateOnly)
            .FirstOrDefaultAsync();
    }

    public async Task<WorkDay> GetOrCreateWorkDayAsync(DateTime date)
    {
        var workDay = await GetWorkDayAsync(date);
        if (workDay != null)
            return workDay;

        var settings = await GetSettingsAsync();

        // Individuelle Stunden pro Tag berücksichtigen
        int targetMinutes = 0;
        if (settings.IsWorkDay(date.DayOfWeek))
        {
            var ourDay = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            var hoursForDay = settings.GetHoursForDay(ourDay);
            targetMinutes = (int)(hoursForDay * 60);
        }
        workDay = new WorkDay
        {
            Date = date.Date,
            Status = settings.IsWorkDay(date.DayOfWeek) ? DayStatus.WorkDay : DayStatus.Weekend,
            TargetWorkMinutes = targetMinutes,
            BalanceMinutes = -targetMinutes
        };

        // Check for holiday
        if (await IsHolidayAsync(date, settings.HolidayRegion))
        {
            workDay.Status = DayStatus.Holiday;
            workDay.TargetWorkMinutes = 0;
            workDay.BalanceMinutes = 0;
        }

        try
        {
            await SaveWorkDayAsync(workDay);
        }
        catch (SQLiteException)
        {
            // UNIQUE Constraint verletzt → paralleler Thread hat bereits eingefügt
            workDay = await GetWorkDayAsync(date);
            if (workDay != null)
                return workDay;
            throw; // Unerwarteter Fehler
        }
        return workDay;
    }

    public async Task<List<WorkDay>> GetWorkDaysAsync(DateTime startDate, DateTime endDate)
    {
        var db = await GetDatabaseAsync();
        var start = startDate.Date;
        var end = endDate.Date;
        return await db.Table<WorkDay>()
            .Where(w => w.Date >= start && w.Date <= end)
            .OrderBy(w => w.Date)
            .ToListAsync();
    }

    public async Task<int> SaveWorkDayAsync(WorkDay workDay)
    {
        var db = await GetDatabaseAsync();
        workDay.ModifiedAt = DateTime.UtcNow;

        if (workDay.Id == 0)
        {
            // Check if entry with same date already exists
            var existing = await db.Table<WorkDay>()
                .Where(w => w.Date == workDay.Date.Date)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                workDay.Id = existing.Id;
                workDay.CreatedAt = existing.CreatedAt;
                await db.UpdateAsync(workDay);
                return workDay.Id;
            }

            workDay.CreatedAt = DateTime.UtcNow;
            // InsertAsync gibt Row-Count zurück (immer 1), NICHT die Auto-Increment-ID.
            // sqlite-net setzt die ID direkt auf dem Objekt nach dem Insert.
            await db.InsertAsync(workDay);
            return workDay.Id;
        }
        else
        {
            await db.UpdateAsync(workDay);
            return workDay.Id;
        }
    }

    public async Task DeleteWorkDayAsync(int id)
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAsync<WorkDay>(id);
    }

    // ==================== TimeEntry ====================

    public async Task<List<TimeEntry>> GetTimeEntriesAsync(int workDayId)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<TimeEntry>()
            .Where(t => t.WorkDayId == workDayId)
            .OrderBy(t => t.Timestamp)
            .ToListAsync();
    }

    public async Task<Dictionary<int, List<TimeEntry>>> GetTimeEntriesForWorkDaysAsync(List<int> workDayIds)
    {
        if (workDayIds.Count == 0)
            return new Dictionary<int, List<TimeEntry>>();

        var db = await GetDatabaseAsync();
        // Alle TimeEntries für die gegebenen WorkDayIds in einer Query laden
        var allEntries = await db.Table<TimeEntry>()
            .Where(t => workDayIds.Contains(t.WorkDayId))
            .OrderBy(t => t.Timestamp)
            .ToListAsync();

        return allEntries
            .GroupBy(e => e.WorkDayId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public async Task<TimeEntry?> GetLastTimeEntryAsync(int workDayId)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<TimeEntry>()
            .Where(t => t.WorkDayId == workDayId)
            .OrderByDescending(t => t.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<TimeEntry?> GetTimeEntryByIdAsync(int id)
    {
        var db = await GetDatabaseAsync();
        return await db.GetAsync<TimeEntry>(id);
    }

    public async Task<int> SaveTimeEntryAsync(TimeEntry entry)
    {
        var db = await GetDatabaseAsync();
        if (entry.Id == 0)
        {
            entry.CreatedAt = DateTime.UtcNow;
            // sqlite-net setzt die ID direkt auf dem Objekt
            await db.InsertAsync(entry);
            return entry.Id;
        }
        else
        {
            await db.UpdateAsync(entry);
            return entry.Id;
        }
    }

    public async Task DeleteTimeEntryAsync(int id)
    {
        var db = await GetDatabaseAsync();
        var entry = await db.Table<TimeEntry>().Where(t => t.Id == id).FirstOrDefaultAsync();
        await db.DeleteAsync<TimeEntry>(id);

        // WorkDay neu berechnen um verwaiste PauseEntries zu bereinigen
        if (entry != null)
        {
            var workDay = await db.Table<WorkDay>().Where(w => w.Id == entry.WorkDayId).FirstOrDefaultAsync();
            if (workDay != null)
            {
                // Entries für Recalculation neu laden (ohne den gelöschten)
                var remainingEntries = await db.Table<TimeEntry>()
                    .Where(t => t.WorkDayId == workDay.Id)
                    .OrderBy(t => t.Timestamp)
                    .ToListAsync();

                if (remainingEntries.Count == 0)
                {
                    // Keine Entries mehr → Auto-Pause entfernen
                    var autoPauses = await db.Table<PauseEntry>()
                        .Where(p => p.WorkDayId == workDay.Id && p.IsAutoPause)
                        .ToListAsync();
                    foreach (var ap in autoPauses)
                        await db.DeleteAsync<PauseEntry>(ap.Id);
                }
            }
        }
    }

    // ==================== PauseEntry ====================

    public async Task<List<PauseEntry>> GetPauseEntriesAsync(int workDayId)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<PauseEntry>()
            .Where(p => p.WorkDayId == workDayId)
            .OrderBy(p => p.StartTime)
            .ToListAsync();
    }

    public async Task<PauseEntry?> GetActivePauseAsync(int workDayId)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<PauseEntry>()
            .Where(p => p.WorkDayId == workDayId && p.EndTime == null)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SavePauseEntryAsync(PauseEntry entry)
    {
        var db = await GetDatabaseAsync();
        if (entry.Id == 0)
        {
            entry.CreatedAt = DateTime.UtcNow;
            // sqlite-net setzt die ID direkt auf dem Objekt
            await db.InsertAsync(entry);
            return entry.Id;
        }
        else
        {
            await db.UpdateAsync(entry);
            return entry.Id;
        }
    }

    public async Task DeletePauseEntryAsync(int id)
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAsync<PauseEntry>(id);
    }

    // ==================== WorkSettings ====================

    public async Task<WorkSettings> GetSettingsAsync()
    {
        var db = await GetDatabaseAsync();
        var settings = await db.Table<WorkSettings>().FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new WorkSettings();
            await db.InsertAsync(settings);
        }
        return settings;
    }

    public async Task SaveSettingsAsync(WorkSettings settings)
    {
        var db = await GetDatabaseAsync();
        settings.ModifiedAt = DateTime.UtcNow;
        await db.UpdateAsync(settings);
    }

    // ==================== VacationEntry ====================

    public async Task<List<VacationEntry>> GetVacationsAsync(int year)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<VacationEntry>()
            .Where(v => v.Year == year)
            .OrderBy(v => v.StartDate)
            .ToListAsync();
    }

    public async Task<int> SaveVacationAsync(VacationEntry vacation)
    {
        var db = await GetDatabaseAsync();
        if (vacation.Id == 0)
        {
            vacation.CreatedAt = DateTime.UtcNow;
            // sqlite-net setzt die ID direkt auf dem Objekt
            await db.InsertAsync(vacation);
            return vacation.Id;
        }
        else
        {
            await db.UpdateAsync(vacation);
            return vacation.Id;
        }
    }

    public async Task DeleteVacationAsync(int id)
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAsync<VacationEntry>(id);
    }

    public async Task<List<VacationEntry>> GetVacationEntriesAsync(DateTime start, DateTime end)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<VacationEntry>()
            .Where(v => v.StartDate <= end && v.EndDate >= start)
            .OrderBy(v => v.StartDate)
            .ToListAsync();
    }

    public async Task<VacationEntry?> GetVacationEntryAsync(int id)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<VacationEntry>()
            .Where(v => v.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SaveVacationEntryAsync(VacationEntry entry)
    {
        return await SaveVacationAsync(entry);
    }

    public async Task DeleteVacationEntryAsync(int id)
    {
        await DeleteVacationAsync(id);
    }

    // ==================== VacationQuota ====================

    public async Task<VacationQuota?> GetVacationQuotaAsync(int year, int? employerId = null)
    {
        var db = await GetDatabaseAsync();
        if (employerId.HasValue)
        {
            return await db.Table<VacationQuota>()
                .Where(q => q.Year == year && q.EmployerId == employerId)
                .FirstOrDefaultAsync();
        }
        return await db.Table<VacationQuota>()
            .Where(q => q.Year == year && q.EmployerId == null)
            .FirstOrDefaultAsync();
    }

    public async Task SaveVacationQuotaAsync(VacationQuota quota)
    {
        var db = await GetDatabaseAsync();
        var existing = await GetVacationQuotaAsync(quota.Year);
        if (existing != null)
        {
            quota.Id = existing.Id;
            await db.UpdateAsync(quota);
        }
        else
        {
            await db.InsertAsync(quota);
        }
    }

    // ==================== HolidayEntry ====================

    public async Task<List<HolidayEntry>> GetHolidaysAsync(int year, string region)
    {
        var db = await GetDatabaseAsync();
        var holidays = await db.Table<HolidayEntry>()
            .Where(h => h.Year == year && h.Region == region)
            .OrderBy(h => h.Date)
            .ToListAsync();

        // Generate holidays if none exist
        if (holidays.Count == 0 && Enum.TryParse<GermanState>(region.Replace("DE-", ""), out var state))
        {
            holidays = GermanHolidays.GetHolidays(year, state);
            await SaveHolidaysAsync(holidays);
        }

        return holidays;
    }

    public async Task SaveHolidaysAsync(List<HolidayEntry> holidays)
    {
        var db = await GetDatabaseAsync();
        foreach (var holiday in holidays)
        {
            if (holiday.Id == 0)
                await db.InsertAsync(holiday);
            else
                await db.UpdateAsync(holiday);
        }
    }

    public async Task<bool> IsHolidayAsync(DateTime date, string region)
    {
        var db = await GetDatabaseAsync();
        var dateOnly = date.Date;
        var holiday = await db.Table<HolidayEntry>()
            .Where(h => h.Date == dateOnly && h.Region == region)
            .FirstOrDefaultAsync();
        return holiday != null;
    }

    // ==================== Project ====================

    public async Task<List<Project>> GetProjectsAsync(bool includeInactive = false)
    {
        var db = await GetDatabaseAsync();
        var query = db.Table<Project>();
        if (!includeInactive)
            query = query.Where(p => p.IsActive);
        return await query.OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<Project?> GetProjectAsync(int id)
    {
        var db = await GetDatabaseAsync();
        return await db.GetAsync<Project>(id);
    }

    public async Task<int> SaveProjectAsync(Project project)
    {
        var db = await GetDatabaseAsync();
        if (project.Id == 0)
        {
            project.CreatedAt = DateTime.UtcNow;
            // sqlite-net setzt die ID direkt auf dem Objekt
            await db.InsertAsync(project);
            return project.Id;
        }
        else
        {
            await db.UpdateAsync(project);
            return project.Id;
        }
    }

    public async Task DeleteProjectAsync(int id)
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAsync<Project>(id);
    }

    // ==================== ProjectTimeEntry ====================

    public async Task<List<ProjectTimeEntry>> GetProjectTimeEntriesAsync(int projectId)
    {
        var db = await GetDatabaseAsync();
        return await db.Table<ProjectTimeEntry>()
            .Where(p => p.ProjectId == projectId)
            .OrderByDescending(p => p.Date)
            .ToListAsync();
    }

    public async Task<List<ProjectTimeEntry>> GetProjectTimeEntriesAsync(int projectId, DateTime startDate, DateTime endDate)
    {
        var db = await GetDatabaseAsync();
        var start = startDate.Date;
        var end = endDate.Date;
        return await db.Table<ProjectTimeEntry>()
            .Where(p => p.ProjectId == projectId && p.Date >= start && p.Date <= end)
            .OrderBy(p => p.Date)
            .ToListAsync();
    }

    public async Task<int> SaveProjectTimeEntryAsync(ProjectTimeEntry entry)
    {
        var db = await GetDatabaseAsync();
        if (entry.Id == 0)
        {
            entry.CreatedAt = DateTime.UtcNow;
            // sqlite-net setzt die ID direkt auf dem Objekt
            await db.InsertAsync(entry);
            return entry.Id;
        }
        else
        {
            await db.UpdateAsync(entry);
            return entry.Id;
        }
    }

    public async Task DeleteProjectTimeEntryAsync(int id)
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAsync<ProjectTimeEntry>(id);
    }

    // ==================== Employer ====================

    public async Task<List<Employer>> GetEmployersAsync(bool includeInactive = false)
    {
        var db = await GetDatabaseAsync();
        var query = db.Table<Employer>();
        if (!includeInactive)
            query = query.Where(e => e.IsActive);
        return await query.OrderBy(e => e.Name).ToListAsync();
    }

    public async Task<Employer?> GetDefaultEmployerAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<Employer>()
            .Where(e => e.IsDefault && e.IsActive)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SaveEmployerAsync(Employer employer)
    {
        var db = await GetDatabaseAsync();
        if (employer.Id == 0)
        {
            employer.CreatedAt = DateTime.UtcNow;
            // sqlite-net setzt die ID direkt auf dem Objekt
            await db.InsertAsync(employer);
            return employer.Id;
        }
        else
        {
            await db.UpdateAsync(employer);
            return employer.Id;
        }
    }

    public async Task DeleteEmployerAsync(int id)
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAsync<Employer>(id);
    }

    public async Task SetDefaultEmployerAsync(int id)
    {
        var db = await GetDatabaseAsync();
        var employers = await db.Table<Employer>().ToListAsync();
        foreach (var employer in employers)
        {
            employer.IsDefault = employer.Id == id;
            await db.UpdateAsync(employer);
        }
    }

    // ==================== ShiftPattern ====================

    public async Task<List<ShiftPattern>> GetShiftPatternsAsync()
    {
        var db = await GetDatabaseAsync();
        var patterns = await db.Table<ShiftPattern>()
            .Where(s => s.IsActive)
            .OrderBy(s => s.StartTimeTicks)
            .ToListAsync();

        // Create default patterns if none exist
        if (patterns.Count == 0)
        {
            patterns = ShiftPattern.GetDefaultPatterns();
            foreach (var pattern in patterns)
            {
                pattern.CreatedAt = DateTime.UtcNow;
                await db.InsertAsync(pattern);
            }
        }

        return patterns;
    }

    public async Task<int> SaveShiftPatternAsync(ShiftPattern pattern)
    {
        var db = await GetDatabaseAsync();
        if (pattern.Id == 0)
        {
            pattern.CreatedAt = DateTime.UtcNow;
            // sqlite-net setzt die ID direkt auf dem Objekt
            await db.InsertAsync(pattern);
            return pattern.Id;
        }
        else
        {
            await db.UpdateAsync(pattern);
            return pattern.Id;
        }
    }

    public async Task DeleteShiftPatternAsync(int id)
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAsync<ShiftPattern>(id);
    }

    // ==================== ShiftAssignment ====================

    public async Task<List<ShiftAssignment>> GetShiftAssignmentsAsync(DateTime startDate, DateTime endDate)
    {
        var db = await GetDatabaseAsync();
        var start = startDate.Date;
        var end = endDate.Date;
        var assignments = await db.Table<ShiftAssignment>()
            .Where(s => s.Date >= start && s.Date <= end)
            .OrderBy(s => s.Date)
            .ToListAsync();

        // Load patterns
        var patterns = await GetShiftPatternsAsync();
        foreach (var assignment in assignments)
        {
            assignment.ShiftPattern = patterns.FirstOrDefault(p => p.Id == assignment.ShiftPatternId);
        }

        return assignments;
    }

    public async Task<ShiftAssignment?> GetShiftAssignmentAsync(DateTime date)
    {
        var db = await GetDatabaseAsync();
        var dateOnly = date.Date;
        var assignment = await db.Table<ShiftAssignment>()
            .Where(s => s.Date == dateOnly)
            .FirstOrDefaultAsync();

        if (assignment != null)
        {
            var patterns = await GetShiftPatternsAsync();
            assignment.ShiftPattern = patterns.FirstOrDefault(p => p.Id == assignment.ShiftPatternId);
        }

        return assignment;
    }

    public async Task<int> SaveShiftAssignmentAsync(ShiftAssignment assignment)
    {
        var db = await GetDatabaseAsync();
        if (assignment.Id == 0)
        {
            assignment.CreatedAt = DateTime.UtcNow;
            // sqlite-net setzt die ID direkt auf dem Objekt
            await db.InsertAsync(assignment);
            return assignment.Id;
        }
        else
        {
            await db.UpdateAsync(assignment);
            return assignment.Id;
        }
    }

    public async Task DeleteShiftAssignmentAsync(int id)
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAsync<ShiftAssignment>(id);
    }

    // ==================== Month lock ====================

    public async Task LockMonthAsync(int year, int month)
    {
        var db = await GetDatabaseAsync();
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var workDays = await GetWorkDaysAsync(startDate, endDate);
        foreach (var workDay in workDays)
        {
            workDay.IsLocked = true;
            await db.UpdateAsync(workDay);
        }
    }

    public async Task UnlockMonthAsync(int year, int month)
    {
        var db = await GetDatabaseAsync();
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var workDays = await GetWorkDaysAsync(startDate, endDate);
        foreach (var workDay in workDays)
        {
            workDay.IsLocked = false;
            await db.UpdateAsync(workDay);
        }
    }

    public async Task<bool> IsMonthLockedAsync(int year, int month)
    {
        var db = await GetDatabaseAsync();
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var lockedDay = await db.Table<WorkDay>()
            .Where(w => w.Date >= startDate && w.Date <= endDate && w.IsLocked)
            .FirstOrDefaultAsync();

        return lockedDay != null;
    }

    // ==================== Statistics queries ====================

    public async Task<DateTime?> GetFirstWorkDayDateAsync()
    {
        var db = await GetDatabaseAsync();
        var firstDay = await db.Table<WorkDay>()
            .Where(w => w.ActualWorkMinutes > 0)
            .OrderBy(w => w.Date)
            .FirstOrDefaultAsync();
        return firstDay?.Date;
    }

    public async Task<int> GetTotalWorkMinutesAsync(DateTime startDate, DateTime endDate)
    {
        var db = await GetDatabaseAsync();
        var start = startDate.Date;
        var end = endDate.Date;

        var workDays = await db.Table<WorkDay>()
            .Where(w => w.Date >= start && w.Date <= end)
            .ToListAsync();

        return workDays.Sum(w => w.ActualWorkMinutes);
    }

    public async Task<int> GetTotalOvertimeMinutesAsync(DateTime startDate, DateTime endDate)
    {
        var db = await GetDatabaseAsync();
        var start = startDate.Date;
        var end = endDate.Date;

        var workDays = await db.Table<WorkDay>()
            .Where(w => w.Date >= start && w.Date <= end)
            .ToListAsync();

        return workDays.Sum(w => w.BalanceMinutes);
    }

    public async Task<Dictionary<int, double>> GetProjectHoursAsync(DateTime startDate, DateTime endDate)
    {
        var db = await GetDatabaseAsync();
        var start = startDate.Date;
        var end = endDate.Date;

        var entries = await db.Table<ProjectTimeEntry>()
            .Where(p => p.Date >= start && p.Date <= end)
            .ToListAsync();

        return entries
            .GroupBy(e => e.ProjectId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Minutes) / 60.0);
    }

    // ==================== Achievement ====================

    public async Task CreateAchievementTableAsync()
    {
        var db = await GetDatabaseAsync();
        await db.CreateTableAsync<Achievement>();
        await db.ExecuteAsync("CREATE UNIQUE INDEX IF NOT EXISTS idx_achievement_key ON Achievements([Key])");
    }

    public async Task<List<Achievement>> GetAllAchievementsAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<Achievement>().ToListAsync();
    }

    public async Task SaveAchievementAsync(Achievement achievement)
    {
        var db = await GetDatabaseAsync();
        if (achievement.Id == 0)
        {
            // sqlite-net setzt die ID direkt auf dem Objekt
            await db.InsertAsync(achievement);
        }
        else
        {
            await db.UpdateAsync(achievement);
        }
    }

    // ==================== Clear (für Restore) ====================

    public async Task ClearAllDataAsync()
    {
        var db = await GetDatabaseAsync();
        // Reihenfolge beachten: FK-abhängige Tabellen zuerst
        await db.DeleteAllAsync<ProjectTimeEntry>();
        await db.DeleteAllAsync<TimeEntry>();
        await db.DeleteAllAsync<PauseEntry>();
        await db.DeleteAllAsync<ShiftAssignment>();
        await db.DeleteAllAsync<ShiftPattern>();
        await db.DeleteAllAsync<VacationEntry>();
        await db.DeleteAllAsync<VacationQuota>();
        await db.DeleteAllAsync<HolidayEntry>();
        await db.DeleteAllAsync<WorkDay>();
        await db.DeleteAllAsync<Project>();
        await db.DeleteAllAsync<Employer>();
        // Achievement-Tabelle existiert möglicherweise nicht bei älteren DBs
        try { await db.DeleteAllAsync<Achievement>(); }
        catch { /* Tabelle existiert noch nicht */ }
        // Settings werden NICHT gelöscht (werden beim Restore überschrieben)
    }

    // ==================== Backup methods ====================

    public async Task<List<WorkDay>> GetAllWorkDaysAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<WorkDay>().ToListAsync();
    }

    public async Task<List<TimeEntry>> GetAllTimeEntriesAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<TimeEntry>().ToListAsync();
    }

    public async Task<List<TimeEntry>> GetTimeEntriesAsync(DateTime date)
    {
        var db = await GetDatabaseAsync();
        var workDay = await GetWorkDayAsync(date);
        if (workDay == null)
            return new List<TimeEntry>();

        return await db.Table<TimeEntry>()
            .Where(e => e.WorkDayId == workDay.Id)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();
    }

    public async Task<List<PauseEntry>> GetAllPauseEntriesAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<PauseEntry>().ToListAsync();
    }

    public async Task<List<VacationEntry>> GetAllVacationEntriesAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<VacationEntry>().ToListAsync();
    }

    public async Task<List<VacationQuota>> GetAllVacationQuotasAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<VacationQuota>().ToListAsync();
    }
}
