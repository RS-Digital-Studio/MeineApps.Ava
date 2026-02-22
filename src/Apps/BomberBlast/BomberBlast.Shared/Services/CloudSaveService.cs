using System.Globalization;
using System.Text.Json;
using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Cloud Save Koordination: Sammelt alle Persistenz-Keys, baut JSON,
/// nutzt IPlayGamesService für Upload/Download, Konflikt-Resolution.
///
/// Local-First: Spiel funktioniert immer offline.
/// Sync-Punkte: App-Start (Pull), Level-Complete/Kauf/Karten-Drop (Push mit Debounce).
/// </summary>
public class CloudSaveService : ICloudSaveService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private const string EnabledKey = "CloudSaveEnabled";
    private const string LastSyncKey = "CloudSaveLastSync";
    private const int DebounceMs = 5000; // 5 Sekunden Debounce für Push

    /// <summary>
    /// Alle Persistenz-Keys die synchronisiert werden sollen.
    /// Reihenfolge: Kritische Daten zuerst (Fortschritt, Währungen).
    /// </summary>
    private static readonly string[] SyncKeys =
    [
        // Kern-Fortschritt
        "GameProgress",
        "CoinData",
        "GemData",
        "PlayerUpgrades",

        // Achievements & Statistiken
        "Achievements",
        "HighScores",

        // Kosmetik (aktive + besessene)
        "PlayerSkin",
        "EnemySkinSet",
        "BombSkin",
        "ExplosionSkin",
        "OwnedPlayerSkins",
        "OwnedBombSkins",
        "OwnedExplosionSkins",
        "ActiveTrail",
        "OwnedTrails",
        "ActiveVictory",
        "OwnedVictories",
        "ActiveFrame",
        "OwnedFrames",

        // Tägliche/Wöchentliche Events
        "DailyRewardData",
        "DailyChallengeData",
        "DailyMissionData",
        "WeeklyChallengeData",
        "LuckySpinData",

        // Feature-Expansion Daten
        "CardCollection",
        "DungeonRunData",
        "DungeonStatsData",
        "BattlePassData",
        "CollectionData",
        "LeagueData",
        "LeagueStatsData",

        // Entdeckungen & Einstellungen
        "DiscoveredItems",
        "visual_style",
        "TutorialCompleted"
    ];

    private readonly IPreferencesService _preferences;
    private readonly IPlayGamesService _playGames;
    private readonly IProgressService _progressService;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ICardService _cardService;

    private CancellationTokenSource? _debounceCts;
    private bool _isSyncing;

    public bool IsEnabled => _preferences.Get(EnabledKey, false);
    public bool IsSyncing => _isSyncing;
    public string? LastSyncTimeUtc => _preferences.Get<string?>(LastSyncKey, null);

    public event EventHandler? SyncStatusChanged;

    public CloudSaveService(
        IPreferencesService preferences,
        IPlayGamesService playGames,
        IProgressService progressService,
        ICoinService coinService,
        IGemService gemService,
        ICardService cardService)
    {
        _preferences = preferences;
        _playGames = playGames;
        _progressService = progressService;
        _coinService = coinService;
        _gemService = gemService;
        _cardService = cardService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AKTIVIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    public void SetEnabled(bool enabled)
    {
        _preferences.Set(EnabledKey, enabled);
        SyncStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CLOUD → LOKAL (Pull)
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<bool> TryLoadFromCloudAsync()
    {
        if (!IsEnabled || !_playGames.IsSignedIn)
            return false;

        try
        {
            SetSyncing(true);

            var cloudJson = await _playGames.LoadCloudSaveAsync();
            if (string.IsNullOrEmpty(cloudJson))
                return false;

            var cloudData = JsonSerializer.Deserialize<CloudSaveData>(cloudJson, JsonOptions);
            if (cloudData == null)
                return false;

            // Lokalen Stand bauen
            var localData = BuildCloudSaveData();

            // Besseren Stand wählen
            var best = CloudSaveData.ChooseBest(localData, cloudData);

            if (best == cloudData)
            {
                // Cloud-Stand auf lokale Preferences anwenden
                ApplyCloudData(cloudData);
                UpdateLastSyncTime();
                return true;
            }

            // Lokaler Stand ist besser → nichts ändern, aber hochladen
            await UploadDataAsync(localData);
            UpdateLastSyncTime();
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CloudSave Load Fehler: {ex.Message}");
            return false;
        }
        finally
        {
            SetSyncing(false);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LOKAL → CLOUD (Push mit Debounce)
    // ═══════════════════════════════════════════════════════════════════════

    public async Task SchedulePushAsync()
    {
        if (!IsEnabled || !_playGames.IsSignedIn)
            return;

        // Vorherigen Debounce-Timer abbrechen
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(DebounceMs, token);

            if (token.IsCancellationRequested)
                return;

            await ForceUploadAsync();
        }
        catch (TaskCanceledException)
        {
            // Debounce wurde abgebrochen → neuer Push kommt
        }
    }

    public async Task ForceUploadAsync()
    {
        if (!IsEnabled || !_playGames.IsSignedIn)
            return;

        try
        {
            SetSyncing(true);
            var data = BuildCloudSaveData();
            await UploadDataAsync(data);
            UpdateLastSyncTime();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CloudSave Upload Fehler: {ex.Message}");
        }
        finally
        {
            SetSyncing(false);
        }
    }

    public async Task<bool> ForceDownloadAsync()
    {
        if (!IsEnabled || !_playGames.IsSignedIn)
            return false;

        try
        {
            SetSyncing(true);

            var cloudJson = await _playGames.LoadCloudSaveAsync();
            if (string.IsNullOrEmpty(cloudJson))
                return false;

            var cloudData = JsonSerializer.Deserialize<CloudSaveData>(cloudJson, JsonOptions);
            if (cloudData == null)
                return false;

            // Cloud-Stand erzwingen (ohne Vergleich)
            ApplyCloudData(cloudData);
            UpdateLastSyncTime();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CloudSave ForceDownload Fehler: {ex.Message}");
            return false;
        }
        finally
        {
            SetSyncing(false);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DATEN SAMMELN
    // ═══════════════════════════════════════════════════════════════════════

    private CloudSaveData BuildCloudSaveData()
    {
        var data = new CloudSaveData
        {
            Version = 1,
            TimestampUtc = DateTime.UtcNow.ToString("O"),
            TotalStars = _progressService.GetTotalStars(),
            CoinBalance = _coinService.Balance,
            GemBalance = _gemService.Balance,
            TotalCards = _cardService.OwnedCards.Count
        };

        // Alle Sync-Keys aus Preferences sammeln
        foreach (var key in SyncKeys)
        {
            var value = _preferences.Get(key, "");
            if (!string.IsNullOrEmpty(value))
            {
                data.Keys[key] = value;
            }
        }

        return data;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DATEN ANWENDEN
    // ═══════════════════════════════════════════════════════════════════════

    private void ApplyCloudData(CloudSaveData cloudData)
    {
        foreach (var kvp in cloudData.Keys)
        {
            _preferences.Set(kvp.Key, kvp.Value);
        }

        // Hinweis: Services müssen ihre Daten beim nächsten Zugriff neu laden.
        // Da alle Services im Konstruktor aus IPreferencesService laden und
        // Singleton sind, brauchen wir einen App-Neustart für volle Konsistenz.
        // Alternative: DataRefreshed-Event auf allen Services (wird bei Bedarf ergänzt).
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UPLOAD
    // ═══════════════════════════════════════════════════════════════════════

    private async Task UploadDataAsync(CloudSaveData data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await _playGames.SaveToCloudAsync(json);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateLastSyncTime()
    {
        _preferences.Set(LastSyncKey, DateTime.UtcNow.ToString("O"));
        SyncStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetSyncing(bool syncing)
    {
        _isSyncing = syncing;
        SyncStatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
