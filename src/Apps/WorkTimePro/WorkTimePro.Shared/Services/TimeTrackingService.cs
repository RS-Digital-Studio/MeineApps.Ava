using WorkTimePro.Helpers;
using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Service für Zeiterfassung (Check-in/out, Pausen).
/// WICHTIG: Arbeitszeiten (Check-in/out, Pausen) nutzen DateTime.Now (Ortszeit),
/// da alle Anzeigen (HH:mm) lokale Uhrzeiten erwarten.
/// Audit-Timestamps (CreatedAt/ModifiedAt) nutzen DateTime.UtcNow.
/// </summary>
public sealed class TimeTrackingService : ITimeTrackingService
{
    private readonly IDatabaseService _database;
    private readonly ICalculationService _calculation;
    private readonly SemaphoreSlim _statusLock = new(1, 1);

    // Cache für non-blocking GetCurrentSessionDuration().
    // Als long-Ticks gespeichert + Interlocked-Zugriff, da TimeSpan (8 Bytes)
    // auf 32-Bit-Plattformen nicht atomar gelesen/geschrieben wird.
    private long _cachedWorkTimeTicks;

    public TrackingStatus CurrentStatus { get; private set; } = TrackingStatus.Idle;

    public event EventHandler<TrackingStatus>? StatusChanged;

    public TimeTrackingService(IDatabaseService database, ICalculationService calculation)
    {
        _database = database;
        _calculation = calculation;
    }

    public async Task LoadStatusAsync()
    {
        // Gleiche Rückwärts-Reichweite wie GetActiveWorkDayAsync (3 Tage) — sonst meldet
        // der Status nach App-Neustart Idle, während ein CheckOut noch einen offenen
        // Alt-Tag finden würde (inkonsistenter Zustand zwischen Anzeige und Buchung).
        var activeDay = await GetActiveWorkDayAsync();
        var lastEntry = await _database.GetLastTimeEntryAsync(activeDay.Id);
        var activePause = await _database.GetActivePauseAsync(activeDay.Id);

        if (activePause != null)
        {
            CurrentStatus = TrackingStatus.OnBreak;
        }
        else if (lastEntry?.Type == EntryType.CheckIn)
        {
            CurrentStatus = TrackingStatus.Working;
        }
        else
        {
            CurrentStatus = TrackingStatus.Idle;
        }

        StatusChanged?.Invoke(this, CurrentStatus);
    }

    public async Task<TimeEntry> CheckInAsync(int? employerId = null, int? projectId = null, string? note = null)
    {
        await _statusLock.WaitAsync();
        try
        {
            // Validierung: Nicht einchecken wenn bereits aktiv
            if (CurrentStatus != TrackingStatus.Idle)
                throw new InvalidOperationException("Already checked in");

            var today = await _database.GetOrCreateWorkDayAsync(DateTime.Today);
            var now = DateTime.Now;

            // Duplikat-Erkennung: Gleicher CheckIn < 10s → ignorieren
            var lastEntry = await _database.GetLastTimeEntryAsync(today.Id);
            if (lastEntry?.Type == EntryType.CheckIn &&
                (now - lastEntry.Timestamp).TotalSeconds < 10)
                return lastEntry;

            var entry = new TimeEntry
            {
                WorkDayId = today.Id,
                EmployerId = employerId,
                ProjectId = projectId,
                Timestamp = now,
                Type = EntryType.CheckIn,
                Note = note
            };

            await _database.SaveTimeEntryAsync(entry);

            // Erster Check-in des Tages?
            if (today.FirstCheckIn == null)
            {
                today.FirstCheckIn = now;
                await _database.SaveWorkDayAsync(today);
            }

            CurrentStatus = TrackingStatus.Working;
            StatusChanged?.Invoke(this, CurrentStatus);

            return entry;
        }
        finally
        {
            _statusLock.Release();
        }
    }

    public async Task<TimeEntry> CheckOutAsync(string? note = null)
    {
        await _statusLock.WaitAsync();
        try
        {
            // Validierung
            if (CurrentStatus == TrackingStatus.Idle)
                throw new InvalidOperationException("Cannot check out when not checked in");

            // Aktiven WorkDay finden (berücksichtigt Mitternachts-Übergang)
            var targetDay = await GetActiveWorkDayAsync();
            var now = DateTime.Now;

            // Aktive Pause beenden falls vorhanden
            var activePause = await _database.GetActivePauseAsync(targetDay.Id);
            if (activePause != null)
            {
                activePause.EndTime = now;
                await _database.SavePauseEntryAsync(activePause);
            }

            var entry = new TimeEntry
            {
                WorkDayId = targetDay.Id,
                Timestamp = now,
                Type = EntryType.CheckOut,
                Note = note
            };

            await _database.SaveTimeEntryAsync(entry);

            // WorkDay aktualisieren
            targetDay.LastCheckOut = now;
            await _calculation.RecalculateWorkDayAsync(targetDay);

            CurrentStatus = TrackingStatus.Idle;
            StatusChanged?.Invoke(this, CurrentStatus);

            return entry;
        }
        finally
        {
            _statusLock.Release();
        }
    }

    public async Task<PauseEntry> StartPauseAsync(string? note = null)
    {
        await _statusLock.WaitAsync();
        try
        {
            // Validierung: Nur in Pause gehen wenn aktiv arbeitend
            if (CurrentStatus != TrackingStatus.Working)
                throw new InvalidOperationException("Can only pause while working");

            var targetDay = await GetActiveWorkDayAsync();

            // Bereits in Pause? → bestehende zurückgeben
            var existingPause = await _database.GetActivePauseAsync(targetDay.Id);
            if (existingPause != null)
                return existingPause;

            var pause = new PauseEntry
            {
                WorkDayId = targetDay.Id,
                StartTime = DateTime.Now,
                Type = PauseType.Manual,
                IsAutoPause = false,
                Note = note
            };

            await _database.SavePauseEntryAsync(pause);

            CurrentStatus = TrackingStatus.OnBreak;
            StatusChanged?.Invoke(this, CurrentStatus);

            return pause;
        }
        finally
        {
            _statusLock.Release();
        }
    }

    public async Task<PauseEntry> EndPauseAsync()
    {
        await _statusLock.WaitAsync();
        try
        {
            var targetDay = await GetActiveWorkDayAsync();
            var activePause = await _database.GetActivePauseAsync(targetDay.Id);

            if (activePause == null)
                throw new InvalidOperationException("No active pause");

            activePause.EndTime = DateTime.Now;
            await _database.SavePauseEntryAsync(activePause);

            // Pausenzeit aktualisieren (RecalculateWorkDayAsync ruft intern RecalculatePauseTimeAsync auf)
            await _calculation.RecalculateWorkDayAsync(targetDay);

            CurrentStatus = TrackingStatus.Working;
            StatusChanged?.Invoke(this, CurrentStatus);

            return activePause;
        }
        finally
        {
            _statusLock.Release();
        }
    }

    public async Task<WorkDay> GetTodayAsync()
    {
        return await _database.GetOrCreateWorkDayAsync(DateTime.Today);
    }

    /// <summary>
    /// Findet den WorkDay mit dem aktiven CheckIn.
    /// Berücksichtigt Mitternachts-Übergang bei Nachtarbeit (bis zu 3 Tage rückwärts).
    /// Vorher: bis zu 8 separate DB-Queries (4 Tage × 2). Jetzt: 1 Range-Query + 1 Last-Entry-Query.
    /// </summary>
    public async Task<WorkDay> GetActiveWorkDayAsync()
    {
        var today = await _database.GetOrCreateWorkDayAsync(DateTime.Today);
        var lastEntry = await _database.GetLastTimeEntryAsync(today.Id);
        if (lastEntry?.Type == EntryType.CheckIn)
            return today;

        // Mitternachts-Übergang: Alle Days der letzten 3 Tage in einem Roundtrip laden,
        // dann von neueste→älteste prüfen ob CheckIn ohne CheckOut offen ist.
        var pastDays = await _database.GetWorkDaysAsync(DateTime.Today.AddDays(-3), DateTime.Today.AddDays(-1));
        foreach (var pastDay in pastDays.OrderByDescending(d => d.Date))
        {
            var pastLast = await _database.GetLastTimeEntryAsync(pastDay.Id);
            if (pastLast?.Type == EntryType.CheckIn)
                return pastDay;
        }

        return today;
    }

    public async Task<TimeSpan> GetCurrentWorkTimeAsync()
    {
        // Aktiven Tag verwenden (3-Tage-Rückblick) — bei Nachtschicht über Mitternacht
        // liegt die offene Session auf dem Vortag; "heute" wäre leer und die
        // Live-Anzeige spränge auf 0:00 (Buchungspfade nutzen denselben Rückblick).
        var today = await GetActiveWorkDayAsync();
        var entries = await _database.GetTimeEntriesAsync(today.Id);
        var pauses = await _database.GetPauseEntriesAsync(today.Id);

        if (entries.Count == 0)
        {
            Interlocked.Exchange(ref _cachedWorkTimeTicks, 0);
            return TimeSpan.Zero;
        }

        var totalWork = TimeSpan.Zero;
        TimeEntry? lastCheckIn = null;

        foreach (var entry in entries.OrderBy(e => e.Timestamp))
        {
            if (entry.Type == EntryType.CheckIn)
            {
                lastCheckIn = entry;
            }
            else if (entry.Type == EntryType.CheckOut && lastCheckIn != null)
            {
                // DST-bewusst wie der Persistenzpfad (CalculateBruttoMinutes) — sonst
                // divergieren Live-Anzeige und gebuchter Wert um ±1h bei Zeitumstellung
                totalWork += DurationMath.RealElapsed(lastCheckIn.Timestamp, entry.Timestamp);
                lastCheckIn = null;
            }
        }

        // Noch eingecheckt? (auch während Pause die laufende Session zählen)
        if (lastCheckIn != null && CurrentStatus != TrackingStatus.Idle)
        {
            totalWork += DurationMath.RealElapsed(lastCheckIn.Timestamp, DateTime.Now);
        }

        // Pausen abziehen
        var totalPauses = pauses
            .Where(p => p.EndTime != null)
            .Sum(p => p.Duration.TotalMinutes);

        // Aktive Pause
        var activePause = pauses.FirstOrDefault(p => p.EndTime == null);
        if (activePause != null)
        {
            totalPauses += DurationMath.RealElapsedMinutes(activePause.StartTime, DateTime.Now);
        }

        var result = totalWork - TimeSpan.FromMinutes(totalPauses);
        // Negative Arbeitszeit verhindern (wenn Pausen > Arbeitszeit)
        if (result < TimeSpan.Zero)
            result = TimeSpan.Zero;
        Interlocked.Exchange(ref _cachedWorkTimeTicks, result.Ticks);
        return result;
    }

    public async Task<TimeSpan> GetCurrentPauseTimeAsync()
    {
        var today = await GetActiveWorkDayAsync();
        var pauses = await _database.GetPauseEntriesAsync(today.Id);

        var totalPauses = pauses
            .Where(p => !p.IsAutoPause && p.EndTime != null)
            .Sum(p => p.Duration.TotalMinutes);

        // Aktive Pause
        var activePause = pauses.FirstOrDefault(p => p.EndTime == null);
        if (activePause != null)
        {
            totalPauses += DurationMath.RealElapsedMinutes(activePause.StartTime, DateTime.Now);
        }

        return TimeSpan.FromMinutes(totalPauses);
    }

    public async Task<TimeSpan?> GetTimeUntilEndAsync()
    {
        if (CurrentStatus == TrackingStatus.Idle)
            return null;

        var today = await GetTodayAsync();
        var currentWork = await GetCurrentWorkTimeAsync();
        var targetWork = today.TargetWorkTime;

        if (currentWork >= targetWork)
            return TimeSpan.Zero;

        return targetWork - currentWork;
    }

    public async Task AddManualEntryAsync(DateTime timestamp, EntryType type, string? note = null)
    {
        var workDay = await _database.GetOrCreateWorkDayAsync(timestamp.Date);

        var entry = new TimeEntry
        {
            WorkDayId = workDay.Id,
            Timestamp = timestamp,
            Type = type,
            Note = note,
            IsManuallyEdited = true
        };

        await _database.SaveTimeEntryAsync(entry);

        // Recalculate work day
        await _calculation.RecalculateWorkDayAsync(workDay);
    }

    public async Task UpdateTimeEntryAsync(int entryId, DateTime newTimestamp, string? note = null)
    {
        // Optimized: Direct lookup by ID
        var entry = await _database.GetTimeEntryByIdAsync(entryId);

        if (entry != null)
        {
            if (!entry.IsManuallyEdited)
            {
                entry.OriginalTimestamp = entry.Timestamp;
            }
            entry.Timestamp = newTimestamp;
            entry.IsManuallyEdited = true;
            if (note != null)
                entry.Note = note;

            await _database.SaveTimeEntryAsync(entry);

            // Recalculate the work day for this entry
            var workDay = await _database.GetWorkDayAsync(entry.Timestamp.Date);
            if (workDay != null)
            {
                await _calculation.RecalculateWorkDayAsync(workDay);
            }
        }
    }

    public async Task UpdatePauseEntryAsync(int pauseId, DateTime newStart, DateTime newEnd)
    {
        // Pause im richtigen WorkDay suchen (nicht nur heute - für Bearbeitung vergangener Tage)
        WorkDay? targetDay = null;

        // Zuerst im WorkDay des Start-Datums suchen
        var dayForDate = await _database.GetOrCreateWorkDayAsync(newStart.Date);
        var pauses = await _database.GetPauseEntriesAsync(dayForDate.Id);
        var pause = pauses.FirstOrDefault(p => p.Id == pauseId);
        if (pause != null) targetDay = dayForDate;

        // Fallback: heute suchen
        if (pause == null)
        {
            var today = await GetTodayAsync();
            if (today.Id != dayForDate.Id)
            {
                pauses = await _database.GetPauseEntriesAsync(today.Id);
                pause = pauses.FirstOrDefault(p => p.Id == pauseId);
                if (pause != null) targetDay = today;
            }
        }

        // Fallback: gestern suchen (Mitternachts-Übergang)
        if (pause == null)
        {
            var yesterday = await _database.GetWorkDayAsync(newStart.Date.AddDays(-1));
            if (yesterday != null)
            {
                pauses = await _database.GetPauseEntriesAsync(yesterday.Id);
                pause = pauses.FirstOrDefault(p => p.Id == pauseId);
                if (pause != null) targetDay = yesterday;
            }
        }

        if (pause != null && targetDay != null)
        {
            pause.StartTime = newStart;
            pause.EndTime = newEnd;
            await _database.SavePauseEntryAsync(pause);
            await _calculation.RecalculateWorkDayAsync(targetDay);
        }
    }

    public async Task<TrackingStatus> GetCurrentStatusAsync()
    {
        await LoadStatusAsync();
        return CurrentStatus;
    }

    public TimeSpan GetCurrentSessionDuration()
    {
        if (CurrentStatus == TrackingStatus.Idle)
            return TimeSpan.Zero;

        // Non-blocking: Atomic Read aus dem Cache (updated by GetLiveDataSnapshotAsync)
        return new TimeSpan(Interlocked.Read(ref _cachedWorkTimeTicks));
    }

    /// <summary>
    /// Lädt alle Live-Daten in einem einzigen Snapshot.
    /// Ersetzt die separaten Aufrufe (GetCurrentWorkTimeAsync + GetCurrentPauseTimeAsync + GetTimeUntilEndAsync)
    /// die zusammen 5+ DB-Queries pro Sekunde verursachten → jetzt nur noch 3 Queries total.
    /// </summary>
    public async Task<LiveDataSnapshot> GetLiveDataSnapshotAsync()
    {
        // Aktiver Tag statt "heute": bei Nachtschicht über Mitternacht liegt die offene
        // Session auf dem Vortag — sonst zeigt der Live-Timer nach 00:00 plötzlich 0:00
        // (Buchungspfade CheckOut/Pause nutzen denselben 3-Tage-Rückblick).
        var today = await GetActiveWorkDayAsync();
        var entries = await _database.GetTimeEntriesAsync(today.Id);
        var pauses = await _database.GetPauseEntriesAsync(today.Id);

        // === Arbeitszeit berechnen (DB liefert bereits nach Timestamp sortiert) ===
        var totalWork = TimeSpan.Zero;
        TimeEntry? lastCheckIn = null;

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.Type == EntryType.CheckIn)
            {
                lastCheckIn = entry;
            }
            else if (entry.Type == EntryType.CheckOut && lastCheckIn != null)
            {
                // DST-bewusst wie der Persistenzpfad — sonst weicht die Live-Anzeige
                // bei Zeitumstellung um ±1h vom später gebuchten Wert ab
                totalWork += DurationMath.RealElapsed(lastCheckIn.Timestamp, entry.Timestamp);
                lastCheckIn = null;
            }
        }

        // Noch eingecheckt? (auch während Pause die laufende Session zählen)
        if (lastCheckIn != null && CurrentStatus != TrackingStatus.Idle)
        {
            totalWork += DurationMath.RealElapsed(lastCheckIn.Timestamp, DateTime.Now);
        }

        // ALLE Pausen abziehen (manuell + auto) für korrekte Netto-Arbeitszeit.
        // Die PauseTime-Anzeige weiter unten zeigt nur manuelle Pausen (ohne Auto-Pause),
        // weil Auto-Pause separat in der TodayView als Warnung angezeigt wird.
        var totalPauseMinutes = 0.0;
        var manualPauseMinutes = 0.0;
        PauseEntry? activePause = null;

        for (var i = 0; i < pauses.Count; i++)
        {
            var p = pauses[i];
            if (p.EndTime == null)
            {
                activePause = p;
                continue;
            }
            totalPauseMinutes += p.Duration.TotalMinutes;
            if (!p.IsAutoPause)
                manualPauseMinutes += p.Duration.TotalMinutes;
        }

        // Aktive Pause
        if (activePause != null)
        {
            var activeDuration = DurationMath.RealElapsedMinutes(activePause.StartTime, DateTime.Now);
            totalPauseMinutes += activeDuration;
            manualPauseMinutes += activeDuration;
        }

        var workTime = totalWork - TimeSpan.FromMinutes(totalPauseMinutes);
        if (workTime < TimeSpan.Zero)
            workTime = TimeSpan.Zero;
        Interlocked.Exchange(ref _cachedWorkTimeTicks, workTime.Ticks);

        var pauseTime = TimeSpan.FromMinutes(manualPauseMinutes);

        // === Restzeit berechnen (gleiche Logik wie GetTimeUntilEndAsync) ===
        TimeSpan? timeUntilEnd = null;
        if (CurrentStatus != TrackingStatus.Idle)
        {
            var remaining = today.TargetWorkTime - workTime;
            timeUntilEnd = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return new LiveDataSnapshot(workTime, pauseTime, timeUntilEnd, today);
    }
}
