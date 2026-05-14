using System.Text.Json;
using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.Logging;

namespace BomberBlast.Services;

/// <summary>
/// Persistiert Master-Mode-Status pro Level in Preferences (JSON-Dictionary Level→Stars).
/// IsActive-Toggle wird separat persistiert (user preference, überlebt App-Restart).
/// Cache wird bei <see cref="ICloudSaveService.CloudStateLoaded"/> automatisch invalidiert.
/// </summary>
public sealed class MasterModeService : IMasterModeService, IDisposable
{
    private const string StatusKey = "master_mode_status_v1";
    private const string ActiveKey = "master_mode_active";

    private readonly IPreferencesService _preferences;
    private readonly IProgressService _progressService;
    private readonly ILogger<MasterModeService> _logger;
    private readonly ICloudSaveService _cloudSave;
    private readonly Dictionary<int, int> _levelStars = new(); // level → stars (0-3)
    // Lock für _levelStars: OnCloudStateLoaded kann vom Background-Thread kommen
    // (CloudSaveService.ApplyCloudData läuft in Task-Continuation), während der
    // UI/Game-Thread RecordLevelCompleted/GetMasterStars aufruft. Ohne Lock würde
    // Dictionary-Mutation während Iteration CollectionWasModified werfen.
    private readonly object _sync = new();

    public MasterModeService(
        IPreferencesService preferences,
        IProgressService progressService,
        ILogger<MasterModeService> logger,
        ICloudSaveService cloudSave)
    {
        _preferences = preferences;
        _progressService = progressService;
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
            _levelStars.Clear();
            LoadInternal();
        }
    }

    public void Dispose()
    {
        _cloudSave.CloudStateLoaded -= OnCloudStateLoaded;
    }

    public bool IsUnlocked => _progressService.HighestCompletedLevel >= 100;

    public bool IsActive
    {
        // Sicherheits-Guard im Getter: Auch wenn die Preferences-Key einen hängen-
        // gebliebenen true-Wert hat (z.B. nach HighestCompletedLevel-Rollback durch
        // Corruption), liefert der Getter false solange !IsUnlocked. Verhindert
        // das Spielen im Master-Mode ohne vollen L100-Clear.
        get => IsUnlocked && _preferences.Get(ActiveKey, false);
        set
        {
            // Master Mode kann nur aktiviert werden wenn unlocked. Deaktivieren geht immer.
            if (value && !IsUnlocked)
            {
                _logger.LogWarning("MasterMode Aktivierung angefordert obwohl !IsUnlocked — ignoriert.");
                return;
            }
            _preferences.Set(ActiveKey, value);
        }
    }

    public int TotalMasterClears
    {
        get
        {
            lock (_sync)
            {
                int count = 0;
                foreach (var (_, stars) in _levelStars)
                    if (stars > 0) count++;
                return count;
            }
        }
    }

    public int TotalMaster3Stars
    {
        get
        {
            lock (_sync)
            {
                int count = 0;
                foreach (var (_, stars) in _levelStars)
                    if (stars >= 3) count++;
                return count;
            }
        }
    }

    public int GetMasterStars(int level)
    {
        lock (_sync)
        {
            return _levelStars.TryGetValue(level, out var stars) ? stars : 0;
        }
    }

    public bool RecordLevelCompleted(int level, int stars)
    {
        if (level < 1 || level > 100) return false;
        if (stars < 0) stars = 0;
        if (stars > 3) stars = 3;

        bool wasFirstClear;
        bool wasStarImprovement;
        lock (_sync)
        {
            int previous = _levelStars.TryGetValue(level, out var s) ? s : 0;
            wasFirstClear = previous == 0 && stars > 0;
            wasStarImprovement = stars > previous;

            if (wasStarImprovement)
            {
                _levelStars[level] = stars;
                SaveInternal();
            }
        }

        // Event ausserhalb des Locks feuern (Handler könnten Reentrance-Risiko bergen)
        MasterLevelCleared?.Invoke(this, new MasterLevelClearedEventArgs
        {
            Level = level,
            Stars = stars,
            WasFirstClear = wasFirstClear,
            WasStarImprovement = wasStarImprovement
        });

        return wasFirstClear || wasStarImprovement;
    }

    public void Reset()
    {
        lock (_sync)
        {
            _levelStars.Clear();
            SaveInternal();
        }
    }

    public event EventHandler<MasterLevelClearedEventArgs>? MasterLevelCleared;

    /// <summary>Ctor-Load. Nicht im Lock — vor dem Event-Subscribe aufgerufen.</summary>
    private void Load() => LoadInternal();

    /// <summary>Kern-Load. Muss innerhalb des Locks aufgerufen werden.</summary>
    private void LoadInternal()
    {
        try
        {
            var json = _preferences.Get(StatusKey, "");
            if (string.IsNullOrEmpty(json)) return;

            var dto = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            if (dto == null) return;

            foreach (var (levelStr, stars) in dto)
            {
                if (int.TryParse(levelStr, out var level) && level >= 1 && level <= 100)
                    _levelStars[level] = Math.Clamp(stars, 0, 3);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MasterMode Load fehlgeschlagen");
        }
    }

    /// <summary>Kern-Save. Muss innerhalb des Locks aufgerufen werden.</summary>
    private void SaveInternal()
    {
        try
        {
            var dto = _levelStars.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
            var json = JsonSerializer.Serialize(dto);
            _preferences.Set(StatusKey, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MasterMode Save fehlgeschlagen");
        }
    }
}
