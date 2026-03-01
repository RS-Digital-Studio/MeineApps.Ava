namespace WorkTimePro.Services;

/// <summary>
/// Service Interface for cloud backup (Premium feature)
/// Supports Google Drive and OneDrive
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Current cloud provider
    /// </summary>
    Models.CloudProvider CurrentProvider { get; }

    /// <summary>
    /// Is the user authenticated?
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Email of the authenticated user
    /// </summary>
    string? UserEmail { get; }

    /// <summary>
    /// Date of the last backup
    /// </summary>
    DateTime? LastBackupDate { get; }

    /// <summary>
    /// Date of the last sync
    /// </summary>
    DateTime? LastSyncDate { get; }

    /// <summary>
    /// Event when auth status changes
    /// </summary>
    event EventHandler<bool>? AuthStatusChanged;

    /// <summary>
    /// Event for backup progress (0-100)
    /// </summary>
    event EventHandler<int>? ProgressChanged;

    // === Authentication ===

    /// <summary>
    /// Sign in with Google Drive
    /// </summary>
    Task<bool> SignInWithGoogleAsync();

    /// <summary>
    /// Sign in with OneDrive
    /// </summary>
    Task<bool> SignInWithMicrosoftAsync();

    /// <summary>
    /// Sign out
    /// </summary>
    Task SignOutAsync();

    // === Backup ===

    /// <summary>
    /// Create and upload backup
    /// </summary>
    Task<BackupResult> CreateBackupAsync();

    /// <summary>
    /// List available backups
    /// </summary>
    Task<List<BackupInfo>> GetAvailableBackupsAsync();

    /// <summary>
    /// Restore a backup
    /// </summary>
    Task<bool> RestoreBackupAsync(string backupId);

    /// <summary>
    /// Delete a backup
    /// </summary>
    Task<bool> DeleteBackupAsync(string backupId);

    // === Lokaler Export/Import (ohne Cloud-Auth) ===

    /// <summary>
    /// Erstellt ein lokales Backup und gibt den Dateipfad zurück.
    /// Benötigt KEINE Cloud-Authentifizierung.
    /// </summary>
    Task<BackupResult> CreateLocalBackupAsync();

    /// <summary>
    /// Erstellt ein lokales Backup und teilt es über das Share-Sheet (Android) bzw. öffnet es (Desktop).
    /// Benötigt KEINE Cloud-Authentifizierung.
    /// </summary>
    Task<BackupResult> ExportBackupAsync();

    /// <summary>
    /// Importiert ein Backup aus einer JSON-Datei.
    /// Benötigt KEINE Cloud-Authentifizierung.
    /// </summary>
    Task<bool> ImportBackupFromFileAsync(string filePath);

    /// <summary>
    /// Listet alle lokalen Backups (ohne Cloud-Auth).
    /// </summary>
    Task<List<BackupInfo>> GetLocalBackupsAsync();

    // === Auto-Sync ===

    /// <summary>
    /// Enable/disable auto-sync
    /// </summary>
    Task SetAutoSyncEnabledAsync(bool enabled);

    /// <summary>
    /// Is auto-sync enabled?
    /// </summary>
    bool IsAutoSyncEnabled { get; }

    /// <summary>
    /// Sync now manually
    /// </summary>
    Task<SyncResult> SyncNowAsync();
}

/// <summary>
/// Result of a backup operation
/// </summary>
public class BackupResult
{
    public bool Success { get; set; }
    public string? BackupId { get; set; }
    public string? FileName { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Information about a backup
/// </summary>
public class BackupInfo
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public int WorkDaysCount { get; set; }

    /// <summary>
    /// Formatted size (e.g. "2.5 MB")
    /// </summary>
    public string SizeDisplay
    {
        get
        {
            if (SizeBytes < 1024)
                return $"{SizeBytes} B";
            if (SizeBytes < 1024 * 1024)
                return $"{SizeBytes / 1024.0:F1} KB";
            return $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
        }
    }

    /// <summary>
    /// Formatted date
    /// </summary>
    public string DateDisplay => CreatedAt.ToString("dd.MM.yyyy HH:mm");
}

/// <summary>
/// Result of a sync operation
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; }
    public int UploadedItems { get; set; }
    public int DownloadedItems { get; set; }
    public int ConflictsResolved { get; set; }
    public string? ErrorMessage { get; set; }
    public SyncDirection Direction { get; set; }
}

/// <summary>
/// Sync direction
/// </summary>
public enum SyncDirection
{
    /// <summary>Bidirectional (default)</summary>
    Bidirectional = 0,

    /// <summary>Upload only</summary>
    UploadOnly = 1,

    /// <summary>Download only</summary>
    DownloadOnly = 2
}
