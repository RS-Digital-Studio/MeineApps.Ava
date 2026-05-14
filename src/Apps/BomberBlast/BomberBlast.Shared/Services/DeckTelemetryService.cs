using System.Text.Json;
using BomberBlast.Models.Entities;
using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.Logging;

namespace BomberBlast.Services;

/// <summary>
/// Lokale Counter pro BombType in Preferences (JSON).
/// Optional: Upload an Firebase via <see cref="IFirebaseService"/> in flachem
/// Key-Value-Format (Pfad <c>analytics/deck/{uid}</c>).
/// Cache wird bei <see cref="ICloudSaveService.CloudStateLoaded"/> automatisch invalidiert.
/// Saves werden debounced (1s Verzögerung) um UI-Thread-Blockaden bei vielen Bomb-Placements zu vermeiden.
/// </summary>
public sealed class DeckTelemetryService : IDeckTelemetryService, IDisposable
{
    private const string PreferencesKey = "deck_telemetry_v1";
    private const int SaveDebounceMs = 1000;
    private readonly IPreferencesService _preferences;
    private readonly IFirebaseService _firebase;
    private readonly ILogger<DeckTelemetryService> _logger;
    private readonly ICloudSaveService _cloudSave;
    private readonly Dictionary<BombType, DeckTelemetryEntry> _entries = new();
    private readonly object _sync = new();
    private CancellationTokenSource? _saveDebounce;
    private volatile bool _isDisposed;

    public DeckTelemetryService(
        IPreferencesService preferences,
        IFirebaseService firebase,
        ILogger<DeckTelemetryService> logger,
        ICloudSaveService cloudSave)
    {
        _preferences = preferences;
        _firebase = firebase;
        _logger = logger;
        _cloudSave = cloudSave;
        Load();

        // Cache invalidieren wenn Cloud-Pull neue Preferences setzt
        _cloudSave.CloudStateLoaded += OnCloudStateLoaded;
    }

    private void OnCloudStateLoaded(object? sender, EventArgs e)
    {
        lock (_sync)
        {
            _entries.Clear();
            Load();
        }
    }

    public void Dispose()
    {
        // Flag zuerst setzen — alle nachfolgenden Record*-Calls sind No-Ops.
        // Verhindert dass nach SaveImmediate noch ein ScheduleSave spawnt.
        _isDisposed = true;
        _cloudSave.CloudStateLoaded -= OnCloudStateLoaded;

        // Pending Save sofort ausführen (Datenverlust-Schutz beim App-Shutdown).
        // CTS wird NICHT explizit disposed — der GC gibt sie frei. Grund: Die
        // fire-and-forget Task-Lambda könnte nach CTS-Dispose auf token.IsCancellationRequested
        // zugreifen → ObjectDisposedException. Cancel reicht aus, um die Lambda zu stoppen.
        _saveDebounce?.Cancel();
        SaveImmediate();
    }

    public void RecordBombPlaced(BombType type)
    {
        if (_isDisposed) return;
        if (type == BombType.Normal) return;
        lock (_sync)
        {
            GetOrCreate(type).Used++;
            ScheduleSave();
        }
    }

    public void RecordLevelStartedWithBombs(IEnumerable<BombType> typesUsed)
    {
        if (_isDisposed) return;
        lock (_sync)
        {
            bool changed = false;
            foreach (var type in typesUsed)
            {
                if (type == BombType.Normal) continue;
                GetOrCreate(type).Plays++;
                changed = true;
            }
            if (changed) ScheduleSave();
        }
    }

    public void RecordLevelCompletedWithBombs(IEnumerable<BombType> typesUsed)
    {
        if (_isDisposed) return;
        lock (_sync)
        {
            bool changed = false;
            foreach (var type in typesUsed)
            {
                if (type == BombType.Normal) continue;
                GetOrCreate(type).Wins++;
                changed = true;
            }
            if (changed) ScheduleSave();
        }
    }

    public IReadOnlyDictionary<BombType, DeckTelemetryEntry> GetStats()
    {
        lock (_sync)
        {
            // Flache Kopie zurückgeben, damit der Caller nicht den internen Zustand modifiziert
            return _entries.ToDictionary(
                kv => kv.Key,
                kv => new DeckTelemetryEntry { Used = kv.Value.Used, Plays = kv.Value.Plays, Wins = kv.Value.Wins });
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _entries.Clear();
            ScheduleSave();
        }
    }

    public async Task FlushToRemoteAsync()
    {
        if (!_firebase.IsOnline || _firebase.Uid == null) return;

        Dictionary<string, object> payload;
        lock (_sync)
        {
            payload = _entries.ToDictionary(
                kv => kv.Key.ToString().ToLowerInvariant(),
                kv => (object)new { used = kv.Value.Used, plays = kv.Value.Plays, wins = kv.Value.Wins });
        }

        try
        {
            await _firebase.UpdateAsync($"analytics/deck/{_firebase.Uid}", payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DeckTelemetry Firebase-Upload fehlgeschlagen");
        }
    }

    private DeckTelemetryEntry GetOrCreate(BombType type)
    {
        if (!_entries.TryGetValue(type, out var entry))
        {
            entry = new DeckTelemetryEntry();
            _entries[type] = entry;
        }
        return entry;
    }

    private void Load()
    {
        try
        {
            var json = _preferences.Get(PreferencesKey, "");
            if (string.IsNullOrEmpty(json)) return;

            var dto = JsonSerializer.Deserialize<Dictionary<string, DeckTelemetryEntry>>(json);
            if (dto == null) return;

            foreach (var (typeName, entry) in dto)
            {
                if (Enum.TryParse<BombType>(typeName, out var type) && entry != null)
                    _entries[type] = entry;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DeckTelemetry Load fehlgeschlagen");
        }
    }

    /// <summary>
    /// Debouncing: Mehrere Record-Calls in kurzer Folge (z.B. Kettenreaktion mit
    /// 5+ Explosionen gleichzeitig) führen zu nur einem Save nach 1s Ruhe.
    /// Reduziert UI-Thread-Blockaden durch JsonSerializer + Preferences.Set.
    /// </summary>
    private void ScheduleSave()
    {
        // Alte CTS canceln (die fire-and-forget Task sieht den Cancel via
        // TaskCanceledException im Delay und beendet sich). NICHT disposen —
        // Task-Lambda könnte sonst auf disposed Token zugreifen → ObjectDisposedException.
        // GC gibt die CTS frei sobald kein Referenz mehr besteht.
        _saveDebounce?.Cancel();
        _saveDebounce = new CancellationTokenSource();
        var token = _saveDebounce.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SaveDebounceMs, token);
                if (!token.IsCancellationRequested)
                    SaveImmediate();
            }
            catch (TaskCanceledException)
            {
                // Neuer Save-Request kam vor Ablauf — OK
            }
            catch (ObjectDisposedException)
            {
                // Dispose hat CTS freigegeben während Task.Delay lief — OK, nichts tun
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DeckTelemetry Debounced-Save Fehler");
            }
        });
    }

    /// <summary>Synchrones Save — nutzt Lock. Wird vom Debounce-Timer UND Dispose aufgerufen.</summary>
    private void SaveImmediate()
    {
        try
        {
            string json;
            lock (_sync)
            {
                var dto = _entries.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
                json = JsonSerializer.Serialize(dto);
            }
            _preferences.Set(PreferencesKey, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DeckTelemetry Save fehlgeschlagen");
        }
    }
}
