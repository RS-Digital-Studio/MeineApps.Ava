using System.Text.Json;
using MeineApps.Core.Ava.Services;
using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Implementation of the cloud backup service
/// Supports Google Drive and OneDrive
/// </summary>
public class BackupService : IBackupService
{
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

    public BackupService(IDatabaseService database, IPreferencesService preferences, IFileShareService fileShareService)
    {
        _database = database;
        _preferences = preferences;
        _fileShareService = fileShareService;
        _backupFolder = "WorkTimeProBackups";
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(BackupDirectory);
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
        if (!string.IsNullOrEmpty(lastBackupStr) && DateTime.TryParse(lastBackupStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastBackup))
        {
            LastBackupDate = lastBackup;
        }

        var lastSyncStr = _preferences.Get(PREFERENCES_LAST_SYNC, string.Empty);
        if (!string.IsNullOrEmpty(lastSyncStr) && DateTime.TryParse(lastSyncStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastSync))
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

    public async Task<bool> SignInWithGoogleAsync()
    {
        try
        {
            ProgressChanged?.Invoke(this, 10);

            // Benötigt Google Sign-In mit Google.Apis.Auth (noch nicht integriert)
            // Platzhalter für UI-Tests
            await Task.Delay(1000);

            ProgressChanged?.Invoke(this, 50);

            CurrentProvider = CloudProvider.GoogleDrive;
            IsAuthenticated = true;
            UserEmail = "user@gmail.com"; // Placeholder

            ProgressChanged?.Invoke(this, 100);

            SaveSettings();
            AuthStatusChanged?.Invoke(this, true);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackupService.SignInWithGoogle Fehler: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SignInWithMicrosoftAsync()
    {
        try
        {
            ProgressChanged?.Invoke(this, 10);

            // Benötigt Microsoft Sign-In mit MSAL (noch nicht integriert)
            await Task.Delay(1000);

            ProgressChanged?.Invoke(this, 50);

            CurrentProvider = CloudProvider.OneDrive;
            IsAuthenticated = true;
            UserEmail = "user@outlook.com"; // Placeholder

            ProgressChanged?.Invoke(this, 100);

            SaveSettings();
            AuthStatusChanged?.Invoke(this, true);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackupService.SignInWithMicrosoft Fehler: {ex.Message}");
            return false;
        }
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
            var backupData = await CreateBackupDataAsync();

            ProgressChanged?.Invoke(this, 40);

            // Serialize to JSON
            var json = JsonSerializer.Serialize(backupData, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var fileName = $"worktime_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";

            ProgressChanged?.Invoke(this, 60);

            // Benötigt Google Drive API / Microsoft Graph API für Cloud-Upload
            // var fileId = await UploadToCloudAsync(bytes, fileName);

            // Save locally as fallback
            var localPath = Path.Combine(CacheDirectory, fileName);
            await File.WriteAllBytesAsync(localPath, bytes);

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
        var settings = await _database.GetSettingsAsync();
        var workDays = await _database.GetAllWorkDaysAsync();
        var timeEntries = await _database.GetAllTimeEntriesAsync();
        var pauseEntries = await _database.GetAllPauseEntriesAsync();
        var vacationEntries = await _database.GetAllVacationEntriesAsync();
        var vacationQuotas = await _database.GetAllVacationQuotasAsync();
        var projects = await _database.GetProjectsAsync(true);
        var employers = await _database.GetEmployersAsync(true);
        var shiftPatterns = await _database.GetShiftPatternsAsync();
        var achievements = await _database.GetAllAchievementsAsync();

        return new BackupData
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
            Achievements = achievements
        };
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

            var json = await File.ReadAllTextAsync(localPath);
            var backupData = JsonSerializer.Deserialize<BackupData>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (backupData == null)
            {
                return false;
            }

            ProgressChanged?.Invoke(this, 40);

            // Sicherheits-Backup der aktuellen Daten VOR dem Restore
            var safetyBackup = await CreateBackupDataAsync();
            ProgressChanged?.Invoke(this, 50);

            try
            {
                // Restore durchführen
                await RestoreDataAsync(backupData);
            }
            catch (Exception restoreEx)
            {
                // Restore fehlgeschlagen → Sicherheits-Backup wiederherstellen
                System.Diagnostics.Debug.WriteLine($"BackupService.Restore fehlgeschlagen, stelle Sicherheits-Backup wieder her: {restoreEx.Message}");
                try
                {
                    await RestoreDataAsync(safetyBackup);
                }
                catch (Exception rollbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"BackupService.Rollback fehlgeschlagen: {rollbackEx.Message}");
                }
                throw; // Ursprünglichen Fehler weitergeben
            }

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
        await _database.ClearAllDataAsync();

        if (data.Settings != null)
        {
            await _database.SaveSettingsAsync(data.Settings);
        }

        if (data.WorkDays != null)
        {
            foreach (var item in data.WorkDays)
            {
                await _database.SaveWorkDayAsync(item);
            }
        }

        if (data.TimeEntries != null)
        {
            foreach (var item in data.TimeEntries)
            {
                await _database.SaveTimeEntryAsync(item);
            }
        }

        if (data.PauseEntries != null)
        {
            foreach (var item in data.PauseEntries)
            {
                await _database.SavePauseEntryAsync(item);
            }
        }

        if (data.VacationEntries != null)
        {
            foreach (var item in data.VacationEntries)
            {
                await _database.SaveVacationEntryAsync(item);
            }
        }

        if (data.VacationQuotas != null)
        {
            foreach (var item in data.VacationQuotas)
            {
                await _database.SaveVacationQuotaAsync(item);
            }
        }

        if (data.Projects != null)
        {
            foreach (var item in data.Projects)
            {
                await _database.SaveProjectAsync(item);
            }
        }

        if (data.Employers != null)
        {
            foreach (var item in data.Employers)
            {
                await _database.SaveEmployerAsync(item);
            }
        }

        if (data.ShiftPatterns != null)
        {
            foreach (var item in data.ShiftPatterns)
            {
                await _database.SaveShiftPatternAsync(item);
            }
        }

        if (data.Achievements != null)
        {
            // Achievement-Tabelle sicherstellen (fuer aeltere Backups ohne Tabelle)
            await _database.CreateAchievementTableAsync();
            foreach (var item in data.Achievements)
            {
                await _database.SaveAchievementAsync(item);
            }
        }
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

            var backupData = await CreateBackupDataAsync();

            ProgressChanged?.Invoke(this, 50);

            var json = JsonSerializer.Serialize(backupData, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var fileName = $"worktime_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var localPath = Path.Combine(BackupDirectory, fileName);

            await File.WriteAllBytesAsync(localPath, bytes);

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
        var result = await CreateLocalBackupAsync();

        if (!result.Success || string.IsNullOrEmpty(result.FileName))
            return result;

        try
        {
            var filePath = Path.Combine(BackupDirectory, result.FileName);
            await _fileShareService.ShareFileAsync(filePath, "WorkTimePro Backup", "application/json");
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

            var json = await File.ReadAllTextAsync(filePath);
            var backupData = JsonSerializer.Deserialize<BackupData>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (backupData == null)
                return false;

            ProgressChanged?.Invoke(this, 30);

            // Sicherheits-Backup der aktuellen Daten VOR dem Import
            var safetyBackup = await CreateBackupDataAsync();
            ProgressChanged?.Invoke(this, 50);

            try
            {
                await RestoreDataAsync(backupData);
            }
            catch (Exception restoreEx)
            {
                // Import fehlgeschlagen → Sicherheits-Backup wiederherstellen
                System.Diagnostics.Debug.WriteLine($"BackupService.Import fehlgeschlagen, stelle Sicherheits-Backup wieder her: {restoreEx.Message}");
                try
                {
                    await RestoreDataAsync(safetyBackup);
                }
                catch (Exception rollbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"BackupService.Rollback fehlgeschlagen: {rollbackEx.Message}");
                }
                return false;
            }

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
        var backups = new List<BackupInfo>();

        try
        {
            if (!Directory.Exists(BackupDirectory))
                return Task.FromResult(backups);

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

        return Task.FromResult(backups);
    }

    // === Auto-Sync ===

    public async Task SetAutoSyncEnabledAsync(bool enabled)
    {
        IsAutoSyncEnabled = enabled;
        SaveSettings();

        if (enabled && IsAuthenticated)
        {
            await SyncNowAsync();
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
            await Task.Delay(500);

            ProgressChanged?.Invoke(this, 50);

            // Create backup as simple sync variant
            var backupResult = await CreateBackupAsync();

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
    public List<Achievement>? Achievements { get; set; }
}
