namespace WorkTimePro.Services;

/// <summary>
/// Service Interface for local JSON backup (Export/Import).
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Event for backup progress (0-100)
    /// </summary>
    event EventHandler<int>? ProgressChanged;

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
