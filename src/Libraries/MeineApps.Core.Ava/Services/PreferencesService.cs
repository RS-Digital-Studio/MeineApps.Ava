using System.Text.Json;

namespace MeineApps.Core.Ava.Services;

/// <summary>
/// JSON file-based preferences service for cross-platform support.
/// Speichert debounced (500ms nach letztem Set) fuer bessere Performance.
/// </summary>
public sealed class PreferencesService : IPreferencesService, IDisposable
{
    private static readonly JsonSerializerOptions _jsonWriteOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private Dictionary<string, JsonElement> _preferences = new();
    private readonly object _lock = new();
    private Timer? _saveTimer;
    // Wenn true werden Disk-Writes ausgesetzt; aufgestaute Aenderungen merkt sich _pendingSave.
    private bool _suspended;
    private bool _pendingSave;

    public PreferencesService(string? appName = null)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, appName ?? "MeineApps");
        Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, "preferences.json");
        Load();
    }

    public T Get<T>(string key, T defaultValue)
    {
        lock (_lock)
        {
            if (!_preferences.TryGetValue(key, out var element))
                return defaultValue;

            try
            {
                return element.Deserialize<T>() ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
    }

    public void Set<T>(string key, T value)
    {
        lock (_lock)
        {
            var json = JsonSerializer.SerializeToElement(value);
            _preferences[key] = json;
        }
        ScheduleSave();
    }

    public bool ContainsKey(string key)
    {
        lock (_lock)
        {
            return _preferences.ContainsKey(key);
        }
    }

    public void Remove(string key)
    {
        lock (_lock)
        {
            if (_preferences.Remove(key))
                ScheduleSave();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _preferences.Clear();
        }
        ScheduleSave();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _preferences = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();
            }
        }
        catch
        {
            _preferences = new();
        }
    }

    /// <summary>
    /// Debounced Save: Setzt/resettet Timer auf 500ms nach letztem Aufruf.
    /// Verwendet Change() statt Dispose+New um Race Conditions zu vermeiden.
    /// </summary>
    private void ScheduleSave()
    {
        lock (_lock)
        {
            // Persistenz pausiert (z.B. laufendes Spiel): nur vormerken, nicht auf Disk schreiben.
            if (_suspended)
            {
                _pendingSave = true;
                return;
            }

            if (_saveTimer == null)
                _saveTimer = new Timer(_ => SaveNow(), null, 500, Timeout.Infinite);
            else
                _saveTimer.Change(500, Timeout.Infinite);
        }
    }

    public void SuspendPersistence()
    {
        lock (_lock)
        {
            _suspended = true;
        }
    }

    public void ResumePersistence()
    {
        bool flush;
        lock (_lock)
        {
            if (!_suspended) return;
            _suspended = false;
            flush = _pendingSave;
            _pendingSave = false;
        }
        // SaveNow() nimmt selbst _lock — daher ausserhalb des lock-Blocks aufrufen.
        if (flush) SaveNow();
    }

    public void FlushPending()
    {
        bool flush;
        lock (_lock)
        {
            flush = _pendingSave;
            _pendingSave = false;
        }
        if (flush) SaveNow();
    }

    /// <summary>
    /// Schreibt Preferences synchron auf Disk (wird vom Timer-Callback aufgerufen)
    /// </summary>
    private void SaveNow()
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_preferences, _jsonWriteOptions);
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // Fehler beim Speichern ignorieren
            }
        }
    }

    public void Dispose()
    {
        _saveTimer?.Dispose();
        SaveNow(); // Sicherstellen dass letzte Aenderungen gespeichert werden
    }
}
