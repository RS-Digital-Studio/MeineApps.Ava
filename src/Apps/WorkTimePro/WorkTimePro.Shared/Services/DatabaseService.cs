using SQLite;
using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// SQLite database service for WorkTime Pro
/// </summary>
public sealed class DatabaseService : IDatabaseService, IBackupDataAccess
{
    private SQLiteAsyncConnection? _database;
    private readonly string _dbPath;
    private Task<SQLiteAsyncConnection>? _initTask;
    private readonly object _initLock = new();

    // Settings-Cache: Settings ändern sich selten (User-Aktion in SettingsView),
    // werden aber pro Tab-Wechsel/Live-Tick mehrfach gelesen. Cache wird in
    // SaveSettingsAsync invalidiert und in GetSettingsAsync bei Miss befüllt.
    private WorkSettings? _settingsCache;
    private readonly object _settingsCacheLock = new();

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

        // WAL-Modus aktivieren: gleichzeitige Reads während Writes, weniger "SQLite busy"-Fehler
        // bei parallelen Pfaden (Auto-Pause-Recalculation + Note-Debounce + Live-Updates).
        try
        {
            await _database.ExecuteAsync("PRAGMA journal_mode=WAL");
        }
        catch (Exception ex)
        {
            // WAL ist nicht überall verfügbar (z.B. exotische Filesysteme) — kein Crash-Grund.
            System.Diagnostics.Debug.WriteLine($"DatabaseService: WAL-Aktivierung fehlgeschlagen: {ex.Message}");
        }

        // Tabellen erstellen
        await _database.CreateTableAsync<WorkDay>();
        await _database.CreateTableAsync<TimeEntry>();
        await _database.CreateTableAsync<PauseEntry>();
        await _database.CreateTableAsync<WorkSettings>();
        await _database.CreateTableAsync<VacationEntry>();
        await _database.CreateTableAsync<VacationQuota>();
        await _database.CreateTableAsync<HolidayEntry>();
        await _database.CreateTableAsync<Project>();
        await _database.CreateTableAsync<Employer>();
        await _database.CreateTableAsync<ShiftPattern>();
        await _database.CreateTableAsync<ShiftAssignment>();

        // Indizes für häufig abgefragte FK-Spalten (Tabellennamen müssen mit [Table]-Attribut übereinstimmen!)
        await _database.ExecuteAsync("CREATE UNIQUE INDEX IF NOT EXISTS idx_workday_date ON WorkDays(Date)");
        await _database.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_timeentry_workdayid ON TimeEntries(WorkDayId)");
        await _database.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_pauseentry_workdayid ON PauseEntries(WorkDayId)");
        await _database.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_vacation_year ON VacationEntries(Year)");
        await _database.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_shiftassignment_date ON ShiftAssignments(Date)");

        // Defense-in-depth gegen doppelte CheckIn/CheckOut-Entries (z.B. bei Multi-Device-Restore
        // oder Hintergrund-Restart). SemaphoreSlim schützt nur den aktuellen Prozess.
        // Try/Catch: bei bestehenden Duplikaten in alten DBs nicht crashen — Index-Erstellung
        // wird beim nächsten Start nach Cleanup erneut versucht.
        try
        {
            await _database.ExecuteAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_timeentry_workday_ts_type ON TimeEntries(WorkDayId, Timestamp, Type)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"DatabaseService: UNIQUE-Index auf TimeEntries konnte nicht erstellt werden (vermutlich Altdaten-Duplikate): {ex.Message}");
        }

        // One-Time-Legacy-Migration: Frühere Versionen hatten WorkDays mit Saldo=0 + Soll>0 + Ist=0
        // (vor der Negativ-Saldo-Berechnung). Hier einmalig korrigieren statt bei jeder
        // Wochen-/Monatsberechnung erneut zu prüfen.
        try
        {
            await _database.ExecuteAsync(
                "UPDATE WorkDays SET BalanceMinutes = -TargetWorkMinutes " +
                "WHERE ActualWorkMinutes = 0 AND BalanceMinutes = 0 AND TargetWorkMinutes > 0");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DatabaseService: Legacy-Migration fehlgeschlagen: {ex.Message}");
        }

        // Backfill: UnroundedWorkMinutes (neue Spalte) für Altdaten mit ActualWorkMinutes
        // initialisieren. Die ungerundete Original-Zeit ist für historische Tage nicht mehr
        // rekonstruierbar — ActualWorkMinutes ist die beste Näherung (= bisheriges Verhalten).
        try
        {
            await _database.ExecuteAsync(
                "UPDATE WorkDays SET UnroundedWorkMinutes = ActualWorkMinutes " +
                "WHERE UnroundedWorkMinutes = 0 AND ActualWorkMinutes <> 0");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DatabaseService: UnroundedWorkMinutes-Backfill fehlgeschlagen: {ex.Message}");
        }

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

        // Individuelle Stunden pro Tag berücksichtigen — über GetDailyMinutesForDay
        // (kaufmännisch gerundet), damit das persistierte Tages-Soll identisch mit der
        // Wochen-/Monats-Aggregation ist (Truncation lieferte z.B. bei 8,2h 491 statt 492).
        var isWorkday = settings.IsWorkDay(date.DayOfWeek);
        int targetMinutes = isWorkday ? settings.GetDailyMinutesForDay(date.DayOfWeek) : 0;
        var status = isWorkday ? DayStatus.WorkDay : DayStatus.Weekend;

        // Soll-Vorrang: Feiertag (0) > zugewiesene Schicht (Netto-Schichtdauer, Off=0) >
        // Wochentag-Soll aus den Settings. Eine Schicht macht auch einen sonst freien Tag
        // zum Arbeitstag (und eine Off-Schicht einen Wochentag frei).
        if (await IsHolidayAsync(date, settings.HolidayRegion))
        {
            status = DayStatus.Holiday;
            targetMinutes = 0;
        }
        else
        {
            var shiftMinutes = await GetShiftTargetMinutesAsync(date);
            if (shiftMinutes.HasValue)
            {
                targetMinutes = shiftMinutes.Value;
                status = DayStatus.WorkDay;
            }
        }

        workDay = new WorkDay
        {
            Date = date.Date,
            Status = status,
            TargetWorkMinutes = targetMinutes,
            BalanceMinutes = WorkDay.CalculateBalance(status, 0, targetMinutes)
        };

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
        // Kind-Zeilen mitlöschen (kein FK-Cascade in sqlite-net) — sonst verwaisen TimeEntries/
        // PauseEntries und blähen Backup-Export/Queries auf. Atomar in einer Transaction.
        await db.RunInTransactionAsync(conn =>
        {
            conn.Execute("DELETE FROM TimeEntries WHERE WorkDayId = ?", id);
            conn.Execute("DELETE FROM PauseEntries WHERE WorkDayId = ?", id);
            conn.Delete<WorkDay>(id);
        });
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

        // SQLite hat ein Limit von 999 Parametern → bei großen Listen in Batches aufteilen
        var allEntries = new List<TimeEntry>();
        foreach (var batch in workDayIds.Chunk(500))
        {
            var batchList = batch.ToList();
            var batchEntries = await db.Table<TimeEntry>()
                .Where(t => batchList.Contains(t.WorkDayId))
                .OrderBy(t => t.Timestamp)
                .ToListAsync();
            allEntries.AddRange(batchEntries);
        }

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
        // PK-Lookup ist schneller als Table+Where; FindAsync gibt null bei nicht-gefunden
        var entry = await db.FindAsync<TimeEntry>(id);
        if (entry == null) return;

        // Delete + ggf. AutoPause-Cleanup in einer Transaction (atomar, ein Roundtrip)
        await db.RunInTransactionAsync(conn =>
        {
            conn.Delete<TimeEntry>(id);

            // Wenn keine Entries mehr für diesen WorkDay existieren → Auto-Pause entfernen
            var remainingCount = conn.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM TimeEntries WHERE WorkDayId = ?", entry.WorkDayId);

            if (remainingCount == 0)
            {
                conn.Execute(
                    "DELETE FROM PauseEntries WHERE WorkDayId = ? AND IsAutoPause = 1", entry.WorkDayId);
            }
        });
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
        // Cache-Hit: keine DB-Query nötig
        WorkSettings? cached;
        lock (_settingsCacheLock) cached = _settingsCache;
        if (cached != null) return cached;

        var db = await GetDatabaseAsync();
        var settings = await db.Table<WorkSettings>().FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new WorkSettings();
            await db.InsertAsync(settings);
        }
        lock (_settingsCacheLock) _settingsCache = settings;
        return settings;
    }

    public async Task SaveSettingsAsync(WorkSettings settings)
    {
        var db = await GetDatabaseAsync();
        settings.ModifiedAt = DateTime.UtcNow;
        await db.UpdateAsync(settings);
        // Cache invalidieren / aktualisieren: die Referenz des gespeicherten Objekts ist
        // der neueste Stand — Cache merkt sich die gleiche Instanz.
        lock (_settingsCacheLock) _settingsCache = settings;
    }

    // ==================== VacationEntry ====================

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

    public async Task DeleteVacationEntryAsync(int id)
    {
        var db = await GetDatabaseAsync();
        await db.DeleteAsync<VacationEntry>(id);
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
        var existing = await GetVacationQuotaAsync(quota.Year, quota.EmployerId);
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

    // Hinweis: Feiertage werden ausschließlich über HolidayCalculator (In-Memory) berechnet.
    // Die frühere DB-gestützte Persistenz (GetHolidaysAsync(year,region)/SaveHolidaysAsync)
    // wurde nie befüllt und ist entfernt — IsHolidayAsync rechnet jetzt direkt.

    /// <summary>
    /// Tages-Soll aus einer zugewiesenen Schicht: Netto-Schichtdauer (Start−Ende − Pause),
    /// 0 bei einer Off-Schicht, oder null wenn dem Tag keine Schicht zugewiesen ist (dann gilt
    /// das Wochentag-Soll aus den Settings). Interner DB-Lookup → kein DI-Zyklus mit ShiftService.
    /// </summary>
    private async Task<int?> GetShiftTargetMinutesAsync(DateTime date)
    {
        var assignment = await GetShiftAssignmentAsync(date);
        if (assignment?.ShiftPattern == null) return null;
        if (assignment.ShiftPattern.Type == ShiftType.Off) return 0;
        return (int)assignment.ShiftPattern.WorkDuration.TotalMinutes;
    }

    public Task<bool> IsHolidayAsync(DateTime date, string region)
    {
        // In-Memory-Berechnung (HolidayCalculator) statt Lookup in der nie befüllten
        // Holidays-Tabelle — so greift die Feiertags-Auto-Erkennung in GetOrCreateWorkDayAsync
        // tatsächlich. Kein DI-Zyklus, da HolidayCalculator abhängigkeitsfrei ist.
        return Task.FromResult(HolidayCalculator.IsHoliday(date, region));
    }

    public async Task<int> SyncHolidaysAsync(DateTime startDate, DateTime endDate)
    {
        var settings = await GetSettingsAsync();
        var workDays = await GetWorkDaysAsync(startDate, endDate);
        var changed = 0;

        foreach (var wd in workDays)
        {
            // Tage mit erfasster Arbeit und manuell gesetzte Abwesenheiten (Urlaub/Krank/…)
            // nie automatisch umstatusen — nur die automatischen Status WorkDay/Weekend/Holiday.
            if (wd.ActualWorkMinutes > 0) continue;
            if (wd.Status is not (DayStatus.WorkDay or DayStatus.Weekend or DayStatus.Holiday)) continue;

            var isHoliday = HolidayCalculator.IsHoliday(wd.Date, settings.HolidayRegion);

            if (isHoliday && wd.Status != DayStatus.Holiday)
            {
                wd.Status = DayStatus.Holiday;
                wd.TargetWorkMinutes = 0;
                wd.BalanceMinutes = 0;
                await SaveWorkDayAsync(wd);
                changed++;
            }
            else if (!isHoliday && wd.Status == DayStatus.Holiday)
            {
                // Region-Wechsel: war Feiertag, ist hier keiner mehr → regulärer Status zurück.
                var isWork = settings.IsWorkDay(wd.Date.DayOfWeek);
                wd.Status = isWork ? DayStatus.WorkDay : DayStatus.Weekend;
                wd.TargetWorkMinutes = isWork ? settings.GetDailyMinutesForDay(wd.Date.DayOfWeek) : 0;
                wd.BalanceMinutes = WorkDay.CalculateBalance(wd.Status, 0, wd.TargetWorkMinutes);
                await SaveWorkDayAsync(wd);
                changed++;
            }
        }

        return changed;
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
        // 2 SQL-Statements statt N+1 (alle laden + einzeln updaten)
        var db = await GetDatabaseAsync();
        await db.ExecuteAsync("UPDATE Employers SET IsDefault = 0");
        await db.ExecuteAsync("UPDATE Employers SET IsDefault = 1 WHERE Id = ?", id);
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

    public async Task<List<ShiftAssignment>> GetAllShiftAssignmentsAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<ShiftAssignment>().ToListAsync();
    }

    // Für Backup: ALLE Muster (auch inaktive), ohne Default-Anlage-Seiteneffekt von
    // GetShiftPatternsAsync — sonst fehlen FK-Ziele der ShiftAssignments beim Restore.
    public async Task<List<ShiftPattern>> GetAllShiftPatternsAsync()
    {
        var db = await GetDatabaseAsync();
        return await db.Table<ShiftPattern>().ToListAsync();
    }

    // ==================== Month lock ====================

    public async Task LockMonthAsync(int year, int month)
    {
        // 1 SQL-Statement statt N+1 (alle laden + einzeln updaten)
        var db = await GetDatabaseAsync();
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        await db.ExecuteAsync(
            "UPDATE WorkDays SET IsLocked = 1 WHERE Date >= ? AND Date <= ?",
            startDate, endDate);
    }

    public async Task UnlockMonthAsync(int year, int month)
    {
        // 1 SQL-Statement statt N+1 (alle laden + einzeln updaten)
        var db = await GetDatabaseAsync();
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        await db.ExecuteAsync(
            "UPDATE WorkDays SET IsLocked = 0 WHERE Date >= ? AND Date <= ?",
            startDate, endDate);
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
        // SQL-Aggregat statt komplette Rows materialisieren: vermeidet Reflection-Mapping pro Row.
        // Bei 365-Tages-Sicht (Statistics/Year) Faktor-10-Speedup gegenüber ToListAsync().Sum().
        var db = await GetDatabaseAsync();
        return await db.ExecuteScalarAsync<int>(
            "SELECT COALESCE(SUM(ActualWorkMinutes), 0) FROM WorkDays WHERE Date BETWEEN ? AND ?",
            startDate.Date, endDate.Date);
    }

    public async Task<int> GetTotalOvertimeMinutesAsync(DateTime startDate, DateTime endDate)
    {
        var db = await GetDatabaseAsync();
        return await db.ExecuteScalarAsync<int>(
            "SELECT COALESCE(SUM(BalanceMinutes), 0) FROM WorkDays WHERE Date BETWEEN ? AND ?",
            startDate.Date, endDate.Date);
    }

    // ==================== Clear (für Restore) ====================

    public async Task ClearAllDataAsync()
    {
        // Settings-Cache invalidieren — wird beim nächsten GetSettings neu aus DB geladen,
        // damit ein Restore mit neuen Settings sauber durchschlägt.
        lock (_settingsCacheLock) _settingsCache = null;

        var db = await GetDatabaseAsync();
        // Alle Delete-Calls in einer Transaction: atomar (Crash mittendrin = kein halb-leerer Zustand)
        // Reihenfolge: FK-abhängige Tabellen zuerst, danach Stammdaten
        await db.RunInTransactionAsync(conn =>
        {
            conn.DeleteAll<TimeEntry>();
            conn.DeleteAll<PauseEntry>();
            conn.DeleteAll<ShiftAssignment>();
            conn.DeleteAll<ShiftPattern>();
            conn.DeleteAll<VacationEntry>();
            conn.DeleteAll<VacationQuota>();
            conn.DeleteAll<HolidayEntry>();
            conn.DeleteAll<WorkDay>();
            conn.DeleteAll<Project>();
            conn.DeleteAll<Employer>();
            // Settings werden NICHT gelöscht (werden beim Restore überschrieben)
        });
    }

    // ==================== Bulk Restore (Batch-Transaction) ====================

    public async Task BulkRestoreAsync(
        List<WorkDay>? workDays,
        List<TimeEntry>? timeEntries,
        List<PauseEntry>? pauseEntries,
        List<VacationEntry>? vacationEntries,
        List<VacationQuota>? vacationQuotas,
        List<Project>? projects,
        List<Employer>? employers,
        List<ShiftPattern>? shiftPatterns,
        List<ShiftAssignment>? shiftAssignments)
    {
        var db = await GetDatabaseAsync();

        // Alles in einer Transaction: 1 Roundtrip statt 1000+ einzelne
        // Reihenfolge: Stammdaten zuerst (Employer, Project), dann abhängige Tabellen
        await db.RunInTransactionAsync(conn =>
        {
            if (employers != null)
                foreach (var item in employers)
                    conn.Insert(item);

            if (projects != null)
                foreach (var item in projects)
                    conn.Insert(item);

            if (workDays != null)
                foreach (var item in workDays)
                    conn.Insert(item);

            if (timeEntries != null)
                foreach (var item in timeEntries)
                    conn.Insert(item);

            if (pauseEntries != null)
                foreach (var item in pauseEntries)
                    conn.Insert(item);

            if (vacationEntries != null)
                foreach (var item in vacationEntries)
                    conn.Insert(item);

            if (vacationQuotas != null)
                foreach (var item in vacationQuotas)
                    conn.Insert(item);

            if (shiftPatterns != null)
                foreach (var item in shiftPatterns)
                    conn.Insert(item);

            // ShiftAssignments NACH ShiftPatterns (FK auf ShiftPatternId) und Employers
            if (shiftAssignments != null)
                foreach (var item in shiftAssignments)
                    conn.Insert(item);
        });
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
