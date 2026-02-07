using System.Text.Json;

namespace MeineApps.Core.Ava.Services;

/// <summary>
/// JSON file-based preferences service for cross-platform support.
/// Speichert debounced (500ms nach letztem Set) fuer bessere Performance.
/// </summary>
public class PreferencesService : IPreferencesService, IDisposable
{
    private static readonly JsonSerializerOptions _jsonWriteOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private Dictionary<string, JsonElement> _preferences = new();
    private readonly object _lock = new();
    private Timer? _saveTimer;

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
    /// Debounced Save: Setzt/resettet Timer auf 500ms nach letztem Aufruf
    /// </summary>
    private void ScheduleSave()
    {
        _saveTimer?.Dispose();
        _saveTimer = new Timer(_ => SaveNow(), null, 500, Timeout.Infinite);
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
