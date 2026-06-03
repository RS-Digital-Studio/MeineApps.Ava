using System.Text.Json;
using MeineApps.Core.Ava.Services;
using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Implementation of the cloud backup service
/// Supports Google Drive and OneDrive
/// </summary>
public sealed class BackupService : IBackupService
{
    // Gecachte JSON-Optionen (vermeidet Neuanlage bei jedem Serialize/Deserialize)
    private static readonly JsonSerializerOptions s_jsonWriteOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static readonly JsonSerializerOptions s_jsonWriteIndentedOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static readonly JsonSerializerOptions s_jsonReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDatabaseService _database;
    private readonly IPreferencesService _preferences;
    private readonly IFileShareService _fileShareService;
    private readonly string _backupFolder;

    /// <summary>
    /// Backup folder name for cloud storage (Google Drive/OneDrive)
    /// </summary>
    public string BackupFolder => _backupFolder;
    private const string PREFERENCES_LAST_BACKUP = "backup_last_date";
    private const string PREFERENCES_LAST_SYNC = "backup_last_sync";
    private const string PREFERENCES_AUTO_SYNC = "backup_auto_sync";
    private const string PREFERENCES_PROVIDER = "backup_provider";
    private const string PREFERENCES_USER_EMAIL = "backup_user_email";

    private static string CacheDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WorkTimePro", "Cache");

    private static string BackupDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WorkTimePro", "Backups");

    private static string AppDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WorkTimePro");

    private static string SafetyDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WorkTimePro", "Safety");

    /// <summary>
    /// Dateiname-Muster für Disk-Safety-Backups (vor Restore/Import angelegt, nach Erfolg gelöscht).
    /// Beim nächsten Start prüft <see cref="TryRecoverPendingSafetyAsync"/> auf zurückgelassene Dateien.
    /// </summary>
    private const string SAFETY_FILE_PREFIX = "safety_";
    private const string SAFETY_FILE_EXT = ".json";
    private const int SAFETY_RETENTION_COUNT = 3;

    public BackupService(IDatabaseService database, IPreferencesService preferences, IFileShareService fileShareService)
    {
        _database = database;
        _preferences = preferences;
        _fileShareService = fileShareService;
        _backupFolder = "WorkTimeProBackups";
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(SafetyDirectory);
        LoadSettings();
    }

    // === Properties ===

    public CloudProvider CurrentProvider { get; private set; } = CloudProvider.None;
    public bool IsAuthenticated { get; private set; }
    public string? UserEmail { get; private set; }
    public DateTime? LastBackupDate { get; private set; }
    public DateTime? LastSyncDate { get; private set; }
    public bool IsAutoSyncEnabled { get; private set; }

    public event EventHandler<bool>? AuthStatusChanged;
    public event EventHandler<int>? ProgressChanged;

    // === Initialization ===

    private void LoadSettings()
    {
        var lastBackupStr = _preferences.Get(PREFERENCES_LAST_BACKUP, string.Empty);
        if (!string.IsNullOrEmpty(lastBackupStr) && DateTime.TryParse(lastBackupStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var lastBackup))
        {
            LastBackupDate = lastBackup;
        }

        var lastSyncStr = _preferences.Get(PREFERENCES_LAST_SYNC, string.Empty);
        if (!string.IsNullOrEmpty(lastSyncStr) && DateTime.TryParse(lastSyncStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var lastSync))
        {
            LastSyncDate = lastSync;
        }

        IsAutoSyncEnabled = _preferences.Get(PREFERENCES_AUTO_SYNC, false);
        UserEmail = _preferences.Get(PREFERENCES_USER_EMAIL, string.Empty);

        var providerInt = _preferences.Get(PREFERENCES_PROVIDER, 0);
        CurrentProvider = (CloudProvider)providerInt;

        IsAuthenticated = !string.IsNullOrEmpty(UserEmail) && CurrentProvider != CloudProvider.None;
    }

    private void SaveSettings()
    {
        if (LastBackupDate.HasValue)
            _preferences.Set(PREFERENCES_LAST_BACKUP, LastBackupDate.Value.ToString("O"));

        if (LastSyncDate.HasValue)
            _preferences.Set(PREFERENCES_LAST_SYNC, LastSyncDate.Value.ToString("O"));

        _preferences.Set(PREFERENCES_AUTO_SYNC, IsAutoSyncEnabled);
        _preferences.Set(PREFERENCES_PROVIDER, (int)CurrentProvider);
        _preferences.Set(PREFERENCES_USER_EMAIL, UserEmail ?? string.Empty);
    }

    // === Authentication ===

    public Task<bool> SignInWithGoogleAsync()
    {
        // Cloud-Auth ist nicht integriert (Google.Apis.Auth fehlt). Solange das so ist,
        // muss SignIn ehrlich false zurückgeben — sonst zeigt die UI "angemeldet" an
        // und ein nachfolgendes CreateBackupAsync schlägt mit "Not authenticated" fehl.
        System.Diagnostics.Debug.WriteLine("BackupService.SignInWithGoogle: Cloud-Auth nicht integriert");
        return Task.FromResult(false);
    }

    public Task<bool> SignInWithMicrosoftAsync()
    {
        // Cloud-Auth ist nicht integriert (MSAL fehlt). Siehe SignInWithGoogleAsync.
        System.Diagnostics.Debug.WriteLine("BackupService.SignInWithMicrosoft: Cloud-Auth nicht integriert");
        return Task.FromResult(false);
    }

    public async Task SignOutAsync()
    {
        try
        {
            CurrentProvider = CloudProvider.None;
            IsAuthenticated = false;
            UserEmail = null;

            SaveSettings();
            AuthStatusChanged?.Invoke(this, false);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackupService.SignOut Fehler: {ex.Message}");
        }
    }

    // === Backup ===

    public async Task<BackupResult> CreateBackupAsync()
    {
        var result = new BackupResult { Timestamp = DateTime.UtcNow };

        try
        {
            if (!IsAuthenticated)
            {
                result.ErrorMessage = "Not authenticated";
                return result;
            }

            ProgressChanged?.Invoke(this, 10);

            // Collect backup data
            var backupData = await CreateBackupDataAsync().ConfigureAwait(false);

            ProgressChanged?.Invoke(this, 40);

            // Serialize to JSON
            var json = JsonSerializer.Serialize(backupData, s_jsonWriteOptions);

            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var fileName = $"worktime_backup_{DateTime.UtcNow:yyyyMMddTHHmmssZ}.json";

            ProgressChanged?.Invoke(this, 60);

            // Benötigt Google Drive API / Microsoft Graph API für Cloud-Upload
            // var fileId = await UploadToCloudAsync(bytes, fileName);

            // Save locally as fallback
            var localPath = Path.Combine(CacheDirectory, fileName);
            await File.WriteAllBytesAsync(localPath, bytes).ConfigureAwait(false);

            ProgressChanged?.Invoke(this, 90);

            result.Success = true;
            result.BackupId = Guid.NewGuid().ToString();
            result.FileName = fileName;
            result.FileSizeBytes = bytes.Length;

            LastBackupDate = DateTime.UtcNow;
            SaveSettings();

            ProgressChanged?.Invoke(this, 100);

            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private async Task<BackupData> CreateBackupDataAsync()
    {
        var settings = await _database.GetSettingsAsync().ConfigureAwait(false);
        var workDays = await _database.GetAllWorkDaysAsync().ConfigureAwait(false);
        var timeEntries = await _database.GetAllTimeEntriesAsync().ConfigureAwait(false);
        var pauseEntries = await _database.GetAllPauseEntriesAsync().ConfigureAwait(false);
        var vacationEntries = await _database.GetAllVacationEntriesAsync().ConfigureAwait(false);
        var vacationQuotas = await _database.GetAllVacationQuotasAsync().ConfigureAwait(false);
        var projects = await _database.GetProjectsAsync(true).ConfigureAwait(false);
        var employers = await _database.GetEmployersAsync(true).ConfigureAwait(false);
        // GetAllShiftPatternsAsync statt GetShiftPatternsAsync: liefert auch inaktive Muster
        // (FK-Ziele der ShiftAssignments) und vermeidet die Default-Anlage als Seiteneffekt.
        var shiftPatterns = await _database.GetAllShiftPatternsAsync().ConfigureAwait(false);
        var shiftAssignments = await _database.GetAllShiftAssignmentsAsync().ConfigureAwait(false);

        var data = new BackupData
        {
            Version = "1.0",
            CreatedAt = DateTime.UtcNow,
            DeviceName = Environment.MachineName,
            AppVersion = GetAppVersion(),
            Settings = settings,
            WorkDays = workDays,
            TimeEntries = timeEntries,
            PauseEntries = pauseEntries,
            VacationEntries = vacationEntries,
            VacationQuotas = vacationQuotas,
            Projects = projects,
            Employers = employers,
            ShiftPatterns = shiftPatterns,
            ShiftAssignments = shiftAssignments
        };

        // WICHTIG: Deep-Clone via JSON-Roundtrip damit das Backup vollständig vom DB-Tracking
        // entkoppelt ist. Ohne Clone würde sqlite-net beim Restore die Id der Settings/Entities
        // auf dem Original-Objekt mutieren — beim Rollback wäre der Sicherheits-Backup verfälscht.
        return DeepCloneViaJson(data);
    }

    /// <summary>
    /// Deep-Clone via JSON-Roundtrip. Entkoppelt das Objekt von eventuellem
    /// ORM-Tracking und verhindert Cross-Mutation zwischen Original- und Sicherungs-Backup.
    /// </summary>
    private static T DeepCloneViaJson<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, s_jsonWriteOptions);
        return JsonSerializer.Deserialize<T>(json, s_jsonReadOptions)!;
    }

    public async Task<List<BackupInfo>> GetAvailableBackupsAsync()
    {
        var backups = new List<BackupInfo>();

        try
        {
            if (!IsAuthenticated)
                return backups;

            // Benötigt Google Drive API / Microsoft Graph API für Cloud-Backups

            // Local backups as fallback
            var localFiles = Directory.GetFiles(CacheDirectory, "worktime_backup_*.json");

            foreach (var file in localFiles.OrderByDescending(f => f))
            {
                var fileInfo = new FileInfo(file);
                backups.Add(new BackupInfo
                {
                    Id = Path.GetFileNameWithoutExtension(file),
                    FileName = fileInfo.Name,
                    CreatedAt = fileInfo.CreationTime,
                    SizeBytes = fileInfo.Length,
                    DeviceName = Environment.MachineName,
                    AppVersion = GetAppVersion()
                });
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Backups: {ex.Message}");
        }

        return backups;
    }

    public async Task<bool> RestoreBackupAsync(string backupId)
    {
        try
        {
            ProgressChanged?.Invoke(this, 10);

            // Benötigt Google Drive API / Microsoft Graph API für Cloud-Download

            // Local file as fallback
            var localPath = Path.Combine(CacheDirectory, $"{backupId}.json");
            if (!File.Exists(localPath))
            {
                return false;
            }

            ProgressChanged?.Invoke(this, 30);

            var json = await File.ReadAllTextAsync(localPath).ConfigureAwait(false);
            var backupData = JsonSerializer.Deserialize<BackupData>(json, s_jsonReadOptions);

            if (backupData == null)
            {
                return false;
            }

            ProgressChanged?.Invoke(this, 40);

            // Sicherheits-Backup der aktuellen Daten VOR dem Restore — RAM + Disk
            // (Disk übersteht App-Kill mitten im ClearAllDataAsync; Recovery beim Start)
            var safetyBackup = await CreateBackupDataAsync().ConfigureAwait(false);
            var safetyPath = await WriteSafetyBackupAsync(safetyBackup).ConfigureAwait(false);
            ProgressChanged?.Invoke(this, 50);

            try
            {
                // Restore durchführen
                await RestoreDataAsync(backupData).ConfigureAwait(false);
            }
            catch (Exception restoreEx)
            {
                // Restore fehlgeschlagen → Sicherheits-Backup wiederherstellen
                System.Diagnostics.Debug.WriteLine($"BackupService.Restore fehlgeschlagen, stelle Sicherheits-Backup wieder her: {restoreEx.Message}");
                try
                {
                    await RestoreDataAsync(safetyBackup).ConfigureAwait(false);
                }
                catch (Exception rollbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"BackupService.Rollback fehlgeschlagen: {rollbackEx.Message}");
                }
                throw; // Ursprünglichen Fehler weitergeben
            }

            // Restore erfolgreich → Safety-Datei aufräumen (alte Generationen behalten)
            TryDeleteSafetyFile(safetyPath);
            TrimOldSafetyFiles();

            ProgressChanged?.Invoke(this, 100);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackupService.RestoreBackup Fehler: {ex.Message}");
            return false;
        }
    }

    private async Task RestoreDataAsync(BackupData data)
    {
        // Alle bestehenden Daten löschen für sauberes Restore (keine Mischung alt+neu)
        await _database.ClearAllDataAsync().ConfigureAwait(false);

        // Settings separat (Upsert-Logik, nicht Batch-fähig)
        if (data.Settings != null)
        {
            await _database.SaveSettingsAsync(data.Settings).ConfigureAwait(false);
        }

        // Alle anderen Daten in einer Transaction (5-10x schneller als einzelne Inserts)
        await _database.BulkRestoreAsync(
            data.WorkDays,
            data.TimeEntries,
            data.PauseEntries,
            data.VacationEntries,
            data.VacationQuotas,
            data.Projects,
            data.Employers,
            data.ShiftPatterns,
            data.ShiftAssignments).ConfigureAwait(false);
    }

    public async Task<bool> DeleteBackupAsync(string backupId)
    {
        try
        {
            // Benötigt Google Drive API / Microsoft Graph API für Cloud-Löschung

            // Delete locally
            var localPath = Path.Combine(CacheDirectory, $"{backupId}.json");
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
            }

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackupService.DeleteBackup Fehler: {ex.Message}");
            return false;
        }
    }

    // === Lokaler Export/Import (ohne Cloud-Auth) ===

    public async Task<BackupResult> CreateLocalBackupAsync()
    {
        var result = new BackupResult { Timestamp = DateTime.UtcNow };

        try
        {
            ProgressChanged?.Invoke(this, 10);

            var backupData = await CreateBackupDataAsync().ConfigureAwait(false);

            ProgressChanged?.Invoke(this, 50);

            var json = JsonSerializer.Serialize(backupData, s_jsonWriteIndentedOptions);

            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var fileName = $"worktime_backup_{DateTime.UtcNow:yyyyMMddTHHmmssZ}.json";
            var localPath = Path.Combine(BackupDirectory, fileName);

            await File.WriteAllBytesAsync(localPath, bytes).ConfigureAwait(false);

            ProgressChanged?.Invoke(this, 90);

            result.Success = true;
            result.BackupId = Path.GetFileNameWithoutExtension(fileName);
            result.FileName = fileName;
            result.FileSizeBytes = bytes.Length;

            LastBackupDate = DateTime.UtcNow;
            SaveSettings();

            ProgressChanged?.Invoke(this, 100);

            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackupService.CreateLocalBackup Fehler: {ex.Message}");
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public async Task<BackupResult> ExportBackupAsync()
    {
        // Erstelle lokales Backup und teile es über Share-Sheet
        var result = await CreateLocalBackupAsync().ConfigureAwait(false);

        if (!result.Success || string.IsNullOrEmpty(result.FileName))
            return result;

        try
        {
            var filePath = Path.Combine(BackupDirectory, result.FileName);
            await _fileShareService.ShareFileAsync(filePath, "WorkTimePro Backup", "application/json").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackupService.ExportBackup Share-Fehler: {ex.Message}");
            // Backup wurde erstellt, nur Share fehlgeschlagen - trotzdem Success
        }

        return result;
    }

    public async Task<bool> ImportBackupFromFileAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            ProgressChanged?.Invoke(this, 10);

            // Größenlimit VOR dem Einlesen prüfen (verhindert OOM bei riesigen Dateien)
            var fileSize = new FileInfo(filePath).Length;
            if (fileSize > 50 * 1024 * 1024)
                return false;

            var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);

            var backupData = JsonSerializer.Deserialize<BackupData>(json, s_jsonReadOptions);

            if (backupData == null)
                return false;

            // Schema-Validierung: Pflichtfelder und Anzahl-Limits
            if (backupData.WorkDays?.Count > 50_000 ||
                backupData.TimeEntries?.Count > 200_000 ||
                backupData.PauseEntries?.Count > 200_000 ||
                backupData.VacationEntries?.Count > 10_000 ||
                backupData.ShiftAssignments?.Count > 50_000)
                return false;

            // FK-Integrität prüfen: TimeEntry.WorkDayId muss in WorkDays existieren
            if (backupData.WorkDays != null && backupData.TimeEntries != null)
            {
                var workDayIds = new HashSet<int>(backupData.WorkDays.Select(w => w.Id));
                if (backupData.TimeEntries.Any(t => !workDayIds.Contains(t.WorkDayId)))
                    return false;
                if (backupData.PauseEntries != null &&
                    backupData.PauseEntries.Any(p => !workDayIds.Contains(p.WorkDayId)))
                    return false;
            }

            // FK-Integrität: ShiftAssignment.ShiftPatternId muss in ShiftPatterns existieren
            if (backupData.ShiftAssignments != null && backupData.ShiftAssignments.Count > 0)
            {
                var patternIds = new HashSet<int>(
                    backupData.ShiftPatterns?.Select(p => p.Id) ?? Enumerable.Empty<int>());
                if (backupData.ShiftAssignments.Any(a => !patternIds.Contains(a.ShiftPatternId)))
                    return false;
            }

            ProgressChanged?.Invoke(this, 30);

            // Sicherheits-Backup der aktuellen Daten VOR dem Import — RAM + Disk
            var safetyBackup = await CreateBackupDataAsync().ConfigureAwait(false);
            var safetyPath = await WriteSafetyBackupAsync(safetyBackup).ConfigureAwait(false);
            ProgressChanged?.Invoke(this, 50);

            try
            {
                await RestoreDataAsync(backupData).ConfigureAwait(false);
            }
            catch (Exception restoreEx)
            {
                // Import fehlgeschlagen -> Sicherheits-Backup wiederherstellen
                System.Diagnostics.Debug.WriteLine($"BackupService.Import fehlgeschlagen, stelle Sicherheits-Backup wieder her: {restoreEx.Message}");
                try
                {
                    await RestoreDataAsync(safetyBackup).ConfigureAwait(false);
                }
                catch (Exception rollbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"BackupService.Rollback fehlgeschlagen: {rollbackEx.Message}");
                }
                return false;
            }

            // Import erfolgreich → Safety-Datei aufräumen
            TryDeleteSafetyFile(safetyPath);
            TrimOldSafetyFiles();

            ProgressChanged?.Invoke(this, 100);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackupService.ImportBackup Fehler: {ex.Message}");
            return false;
        }
    }

    public Task<List<BackupInfo>> GetLocalBackupsAsync()
    {
        // I/O auf Worker-Thread: Verhindert UI-Thread-Block wenn Aufrufer auf UI-Thread läuft
        // (Directory.GetFiles + N×FileInfo sind synchroner Disk-Zugriff).
        return Task.Run(() =>
        {
            var backups = new List<BackupInfo>();
            try
            {
                if (!Directory.Exists(BackupDirectory))
                    return backups;

                var localFiles = Directory.GetFiles(BackupDirectory, "worktime_backup_*.json");

                foreach (var file in localFiles.OrderByDescending(f => f))
                {
                    var fileInfo = new FileInfo(file);
                    backups.Add(new BackupInfo
                    {
                        Id = Path.GetFileNameWithoutExtension(file),
                        FileName = fileInfo.Name,
                        CreatedAt = fileInfo.CreationTime,
                        SizeBytes = fileInfo.Length,
                        DeviceName = Environment.MachineName,
                        AppVersion = GetAppVersion()
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BackupService.GetLocalBackups Fehler: {ex.Message}");
            }
            return backups;
        });
    }

    // === Auto-Sync ===

    public async Task SetAutoSyncEnabledAsync(bool enabled)
    {
        IsAutoSyncEnabled = enabled;
        SaveSettings();

        if (enabled && IsAuthenticated)
        {
            await SyncNowAsync().ConfigureAwait(false);
        }
    }

    public async Task<SyncResult> SyncNowAsync()
    {
        var result = new SyncResult { Timestamp = DateTime.UtcNow };

        try
        {
            if (!IsAuthenticated)
            {
                result.ErrorMessage = "Not authenticated";
                return result;
            }

            ProgressChanged?.Invoke(this, 10);

            // Echte Sync-Logik benötigt Cloud-API (aktuell Backup als Sync-Variante)
            await Task.Delay(500).ConfigureAwait(false);

            ProgressChanged?.Invoke(this, 50);

            // Create backup as simple sync variant
            var backupResult = await CreateBackupAsync().ConfigureAwait(false);

            ProgressChanged?.Invoke(this, 100);

            result.Success = backupResult.Success;
            result.Direction = SyncDirection.UploadOnly;
            result.UploadedItems = 1;

            LastSyncDate = DateTime.UtcNow;
            SaveSettings();

            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private static string GetAppVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "2.0.0";
    }

    // === Disk-Safety-Backup (überlebt Prozess-Kill mitten im Restore) ===

    /// <summary>
    /// Schreibt das übergebene Backup als zusätzliche Safety-Datei auf Disk.
    /// Wird VOR dem Clear/Restore aufgerufen — bei Crash bleibt die Datei liegen
    /// und kann beim nächsten Start via <see cref="TryRecoverPendingSafetyAsync"/>
    /// wiederhergestellt werden.
    /// </summary>
    /// <returns>Vollständiger Dateipfad oder null bei I/O-Fehler.</returns>
    private static async Task<string?> WriteSafetyBackupAsync(BackupData data)
    {
        try
        {
            Directory.CreateDirectory(SafetyDirectory);
            var fileName = $"{SAFETY_FILE_PREFIX}{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}{SAFETY_FILE_EXT}";
            var path = Path.Combine(SafetyDirectory, fileName);
            var json = JsonSerializer.Serialize(data, s_jsonWriteOptions);
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
            return path;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackupService.WriteSafetyBackup Fehler: {ex.Message}");
            return null;
        }
    }

    private static void TryDeleteSafetyFile(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackupService.TryDeleteSafetyFile Fehler: {ex.Message}");
        }
    }

    /// <summary>
    /// Hält maximal <see cref="SAFETY_RETENTION_COUNT"/> der jüngsten Safety-Backups als
    /// Vorhaltung (falls eine spätere Recovery doch noch gewünscht ist).
    /// </summary>
    private static void TrimOldSafetyFiles()
    {
        try
        {
            if (!Directory.Exists(SafetyDirectory)) return;
            var files = Directory.GetFiles(SafetyDirectory, $"{SAFETY_FILE_PREFIX}*{SAFETY_FILE_EXT}")
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .Skip(SAFETY_RETENTION_COUNT)
                .ToList();
            foreach (var f in files)
            {
                try { File.Delete(f); }
                catch { /* nicht kritisch */ }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackupService.TrimOldSafetyFiles Fehler: {ex.Message}");
        }
    }

    /// <summary>
    /// Prüft beim App-Start auf zurückgelassene Safety-Backups. Wenn vorhanden, liefert
    /// die Info zur jüngsten Datei zurück — Aufrufer (UI) kann dem User einen Recovery-
    /// Dialog anbieten. Wenn der User ablehnt, kann <see cref="DismissPendingSafetyAsync"/>
    /// die Datei löschen.
    /// </summary>
    public Task<BackupInfo?> GetPendingSafetyBackupAsync()
    {
        try
        {
            if (!Directory.Exists(SafetyDirectory))
                return Task.FromResult<BackupInfo?>(null);

            var newest = Directory.GetFiles(SafetyDirectory, $"{SAFETY_FILE_PREFIX}*{SAFETY_FILE_EXT}")
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .FirstOrDefault();

            if (newest == null)
                return Task.FromResult<BackupInfo?>(null);

            var fi = new FileInfo(newest);
            return Task.FromResult<BackupInfo?>(new BackupInfo
            {
                Id = Path.GetFileNameWithoutExtension(newest),
                FileName = fi.Name,
                CreatedAt = fi.CreationTime,
                SizeBytes = fi.Length,
                DeviceName = Environment.MachineName,
                AppVersion = GetAppVersion()
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackupService.GetPendingSafetyBackup Fehler: {ex.Message}");
            return Task.FromResult<BackupInfo?>(null);
        }
    }

    /// <summary>
    /// Stellt das zuletzt geschriebene Safety-Backup wieder her. Liefert true, wenn
    /// die DB nach dem Aufruf wieder dem Stand vor dem letzten Restore entspricht.
    /// </summary>
    public async Task<bool> RecoverPendingSafetyAsync()
    {
        try
        {
            var pending = await GetPendingSafetyBackupAsync().ConfigureAwait(false);
            if (pending == null)
                return false;

            var path = Path.Combine(SafetyDirectory, pending.FileName);
            if (!File.Exists(path))
                return false;

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<BackupData>(json, s_jsonReadOptions);
            if (data == null)
                return false;

            await RestoreDataAsync(data).ConfigureAwait(false);
            TryDeleteSafetyFile(path);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackupService.RecoverPendingSafety Fehler: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// User lehnt Recovery ab → Safety-Datei löschen, damit der Dialog beim nächsten Start nicht erneut erscheint.
    /// </summary>
    public Task DismissPendingSafetyAsync()
    {
        try
        {
            if (!Directory.Exists(SafetyDirectory))
                return Task.CompletedTask;
            foreach (var f in Directory.GetFiles(SafetyDirectory, $"{SAFETY_FILE_PREFIX}*{SAFETY_FILE_EXT}"))
            {
                try { File.Delete(f); }
                catch { /* nicht kritisch */ }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackupService.DismissPendingSafety Fehler: {ex.Message}");
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Container for backup data
/// </summary>
public class BackupData
{
    public string Version { get; set; } = "1.0";
    public DateTime CreatedAt { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;

    public WorkSettings? Settings { get; set; }
    public List<WorkDay>? WorkDays { get; set; }
    public List<TimeEntry>? TimeEntries { get; set; }
    public List<PauseEntry>? PauseEntries { get; set; }
    public List<VacationEntry>? VacationEntries { get; set; }
    public List<VacationQuota>? VacationQuotas { get; set; }
    public List<Project>? Projects { get; set; }
    public List<Employer>? Employers { get; set; }
    public List<ShiftPattern>? ShiftPatterns { get; set; }
    public List<ShiftAssignment>? ShiftAssignments { get; set; }
}
