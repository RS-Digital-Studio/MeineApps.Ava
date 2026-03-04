using System.Text.Json;
using FitnessRechner.Models;

namespace FitnessRechner.Services;

/// <summary>
/// JSON-based tracking service
/// </summary>
public sealed class TrackingService : ITrackingService, IDisposable
{
    private bool _disposed;
    private const string TRACKING_FILE = "tracking.json";
    private const int DEFAULT_STATS_DAYS = 30;
    private static readonly TimeSpan BackupInterval = TimeSpan.FromMinutes(1);
    private readonly string _filePath;
    private List<TrackingEntry> _entries = [];
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _isLoaded = false;
    private DateTime _lastBackupTime = DateTime.MinValue;

    public event Action? EntryAdded;

    public TrackingService()
    {
        _filePath = Path.Combine(GetDataDirectory(), TRACKING_FILE);
    }

    public async Task<TrackingEntry> AddEntryAsync(TrackingEntry entry)
    {
        await EnsureLoadedAsync();

        await _writeLock.WaitAsync();
        try
        {
            entry.Id = Guid.NewGuid().ToString();
            _entries.Add(entry);
            await SaveEntriesAsync();
        }
        finally
        {
            _writeLock.Release();
        }

        EntryAdded?.Invoke();
        return entry;
    }

    public async Task<bool> UpdateEntryAsync(TrackingEntry entry)
    {
        await EnsureLoadedAsync();

        await _writeLock.WaitAsync();
        try
        {
            var existing = _entries.FirstOrDefault(e => e.Id == entry.Id);
            if (existing == null) return false;

            existing.Date = entry.Date;
            existing.Type = entry.Type;
            existing.Value = entry.Value;
            existing.Note = entry.Note;

            await SaveEntriesAsync();
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> DeleteEntryAsync(string id)
    {
        await EnsureLoadedAsync();

        await _writeLock.WaitAsync();
        try
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry == null) return false;

            _entries.Remove(entry);
            await SaveEntriesAsync();
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<TrackingEntry>> GetEntriesAsync(TrackingType type, int limit = DEFAULT_STATS_DAYS)
    {
        await EnsureLoadedAsync();

        await _writeLock.WaitAsync();
        try
        {
            return _entries
                .Where(e => e.Type == type)
                .OrderByDescending(e => e.Date)
                .Take(limit)
                .ToList();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<TrackingEntry>> GetEntriesAsync(TrackingType type, DateTime startDate, DateTime endDate)
    {
        await EnsureLoadedAsync();

        await _writeLock.WaitAsync();
        try
        {
            return _entries
                .Where(e => e.Type == type && e.Date.Date >= startDate.Date && e.Date.Date <= endDate.Date)
                .OrderByDescending(e => e.Date)
                .ToList();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<TrackingEntry?> GetLatestEntryAsync(TrackingType type)
    {
        await EnsureLoadedAsync();

        await _writeLock.WaitAsync();
        try
        {
            return _entries
                .Where(e => e.Type == type)
                .OrderByDescending(e => e.Date)
                .FirstOrDefault();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<TrackingStats?> GetStatsAsync(TrackingType type, int days = DEFAULT_STATS_DAYS)
    {
        await EnsureLoadedAsync();

        await _writeLock.WaitAsync();
        try
        {
            var cutoffDate = DateTime.Today.AddDays(-days);
            var typeEntries = _entries
                .Where(e => e.Type == type && e.Date.Date >= cutoffDate.Date)
                .OrderByDescending(e => e.Date)
                .ToList();

            if (typeEntries.Count == 0)
                return null;

            var current = typeEntries.First().Value;
            var values = typeEntries.Select(e => e.Value).ToList();

            // Trend: Differenz zwischen letztem und vorletztem Eintrag
            double trend = 0;
            if (typeEntries.Count >= 2)
            {
                trend = typeEntries[0].Value - typeEntries[1].Value;
            }

            return new TrackingStats(
                type,
                current,
                values.Average(),
                values.Min(),
                values.Max(),
                trend,
                typeEntries.Count);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task ClearAllAsync()
    {
        await _writeLock.WaitAsync();
        try
        {
            _entries.Clear();
            await SaveEntriesAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task EnsureLoadedAsync()
    {
        if (_isLoaded) return;

        await _loadLock.WaitAsync();
        try
        {
            if (_isLoaded) return;

            if (File.Exists(_filePath))
            {
                await LoadEntriesAsync();
            }

            _isLoaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task LoadEntriesAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            _entries = JsonSerializer.Deserialize<List<TrackingEntry>>(json) ?? [];
        }
        catch (Exception)
        {
            // Try to restore from backup
            var backupPath = _filePath + ".backup";
            if (File.Exists(backupPath))
            {
                try
                {
                    var backupJson = await File.ReadAllTextAsync(backupPath);
                    _entries = JsonSerializer.Deserialize<List<TrackingEntry>>(backupJson) ?? [];
                }
                catch
                {
                    _entries = [];
                }
            }
            else
            {
                _entries = [];
            }
        }
    }

    private async Task SaveEntriesAsync()
    {
        // Atomic file operations with temp file and backup
        var tempFilePath = _filePath + ".tmp";
        try
        {
            // 1. Write to temp file
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(tempFilePath, json);

            // 2. Backup erstellen (max. alle 1 Minute, vermeidet unnötige Disk-IO bei Quick-Add)
            if (File.Exists(_filePath) && DateTime.UtcNow - _lastBackupTime > BackupInterval)
            {
                var backupPath = _filePath + ".backup";
                File.Copy(_filePath, backupPath, overwrite: true);
                _lastBackupTime = DateTime.UtcNow;
            }

            // 3. Atomic move: temp -> final
            File.Move(tempFilePath, _filePath, overwrite: true);
        }
        catch
        {
            // Cleanup on error
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _loadLock.Dispose();
        _writeLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static string GetDataDirectory()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FitnessRechner");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
