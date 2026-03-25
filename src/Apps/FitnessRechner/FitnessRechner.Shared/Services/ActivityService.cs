using System.Text.Json;
using FitnessRechner.Models;

namespace FitnessRechner.Services;

/// <summary>
/// JSON-basierter Aktivitäts-Tracking-Service.
/// Speichert alle Aktivitäten in einer JSON-Datei im AppData-Verzeichnis.
/// Thread-Safe durch SemaphoreSlim.
/// </summary>
public sealed class ActivityService : IActivityService, IDisposable
{
    private bool _disposed;
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };
    private const string ACTIVITY_FILE = "activity_log.json";
    private static readonly TimeSpan BackupInterval = TimeSpan.FromMinutes(1);
    private readonly string _filePath;
    private List<ActivityEntry> _entries = [];
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _isLoaded;
    private DateTime _lastBackupTime = DateTime.MinValue;

    public event Action? ActivityAdded;

    public ActivityService()
    {
        _filePath = Path.Combine(GetDataDirectory(), ACTIVITY_FILE);
    }

    public async Task<ActivityEntry> AddActivityAsync(ActivityEntry entry)
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

        ActivityAdded?.Invoke();
        return entry;
    }

    public async Task<IReadOnlyList<ActivityEntry>> GetActivitiesAsync(DateTime date)
    {
        await EnsureLoadedAsync();

        await _writeLock.WaitAsync();
        try
        {
            return _entries
                .Where(e => e.Date.Date == date.Date)
                .OrderByDescending(e => e.Date)
                .ToList();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<ActivityEntry>> GetActivitiesInRangeAsync(DateTime start, DateTime end)
    {
        await EnsureLoadedAsync();

        await _writeLock.WaitAsync();
        try
        {
            return _entries
                .Where(e => e.Date.Date >= start.Date && e.Date.Date <= end.Date)
                .OrderByDescending(e => e.Date)
                .ToList();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> DeleteActivityAsync(string id)
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

    public async Task<double> GetTodayBurnedCaloriesAsync()
    {
        await EnsureLoadedAsync();

        await _writeLock.WaitAsync();
        try
        {
            return _entries
                .Where(e => e.Date.Date == DateTime.Today)
                .Sum(e => e.CaloriesBurned);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public double CalculateCalories(double metValue, double weightKg, int durationMinutes)
        => ActivityDatabase.CalculateCalories(metValue, weightKg, durationMinutes);

    // =====================================================================
    // Persistenz (analog zu TrackingService)
    // =====================================================================

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
            _entries = JsonSerializer.Deserialize<List<ActivityEntry>>(json) ?? [];
        }
        catch (Exception)
        {
            // Backup versuchen
            var backupPath = _filePath + ".backup";
            if (File.Exists(backupPath))
            {
                try
                {
                    var backupJson = await File.ReadAllTextAsync(backupPath);
                    _entries = JsonSerializer.Deserialize<List<ActivityEntry>>(backupJson) ?? [];
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
        var tempFilePath = _filePath + ".tmp";
        try
        {
            // 1. In Temp-Datei schreiben
            var json = JsonSerializer.Serialize(_entries, s_jsonOptions);
            await File.WriteAllTextAsync(tempFilePath, json);

            // 2. Backup erstellen (max. alle 1 Minute)
            if (File.Exists(_filePath) && DateTime.UtcNow - _lastBackupTime > BackupInterval)
            {
                var backupPath = _filePath + ".backup";
                File.Copy(_filePath, backupPath, overwrite: true);
                _lastBackupTime = DateTime.UtcNow;
            }

            // 3. Atomic Move: temp → final
            File.Move(tempFilePath, _filePath, overwrite: true);
        }
        catch
        {
            // Aufräumen bei Fehler
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
