using System.Globalization;
using System.Text.Json;
using BomberBlast.Models;
using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.Logging;

namespace BomberBlast.Services;

/// <summary>
/// Cloud Save Koordination: Sammelt alle Persistenz-Keys, baut JSON,
/// nutzt IPlayGamesService für Upload/Download, Konflikt-Resolution.
///
/// Local-First: Spiel funktioniert immer offline.
/// Sync-Punkte: App-Start (Pull), Level-Complete/Kauf/Karten-Drop (Push mit Debounce).
/// </summary>
public sealed class CloudSaveService : ICloudSaveService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private const string EnabledKey = "CloudSaveEnabled";
    // Audit L06: LastSyncKey ist BEWUSST nicht in SyncKeys[] — er ist ein lokales
    // Telemetrie-Feld ("wann zuletzt synced?") und darf nicht via Cloud-Pull ueberschrieben werden.
    // Sonst wuerde der "Pulling from Cloud"-Vorgang den eigenen LastSync-Stempel mit dem Cloud-Stempel
    // ersetzen → Settings-Anzeige zeigt falsches Datum nach Erstlogin auf neuem Geraet.
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
        "TutorialCompleted",

        // v2.0.35: Master Mode + Deck-Balancing-Telemetrie
        "master_mode_status_v1",   // Per-Level Master-Sterne
        "master_mode_active",       // User-Toggle
        "deck_telemetry_v1",        // Used/Plays/Wins pro BombType

        // v2.0.46: Accessibility + Performance + DSGVO-Consent (Schema V3)
        "Accessibility_ColorblindMode",
        "Accessibility_HighContrast",
        "Accessibility_UiScale",
        "Accessibility_Subtitles",
        "TargetFrameRate",
        "AnalyticsConsent",

        // Phase 23 (M5) — First-Time-Purchase-Bonus muss über Geräte-Wechsel synchen,
        // sonst kann ein Spieler den Bonus durch App-Reinstall mehrfach einlösen (Exploit-Schutz).
        "FirstPurchaseClaimed",

        // Phase 24 (O3-O5) — Retention-Tracking. FirstWin/FtueSkin müssen gegen
        // App-Reinstall-Re-Trigger geschuetzt sein. FirstSession ist der D1/D7-Anker.
        "Retention_FirstWin",
        "Retention_FtueSkin",
        "Retention_FirstSessionUtc",
        "Retention_LastSessionUtc",
        "Retention_ComebackLastClaim",

        // Phase 25b (Compliance) — Privacy-Center-Toggles. Sync verhindert dass
        // ein Spieler nach Geräte-Wechsel ohne explizite Re-Zustimmung wieder auf Defaults landet.
        "Privacy_PersonalizedAds",
        "Privacy_PushNotifications",
        "Privacy_ChildSafeMode",

        // Phase 23b (M1+M2) — Premium-Pass-Plus + VIP-Subscription.
        // Müssen über Geräte-Wechsel synchen damit der Spieler den gekauften Tier nicht verliert.
        "BattlePassPlus_Active",
        "BattlePassPlus_Season",
        "Vip_ExpiresAtUtc",
        "Vip_DailyClaimedDate"
    ];

    /// <summary>
    /// Wird nach erfolgreichem Cloud-Pull gefeuert (wenn ApplyCloudData Preferences aktualisiert hat).
    /// Services mit internem Cache (MasterModeService, DeckTelemetryService etc.) müssen ihren
    /// Cache invalidieren, da Preferences-Keys jetzt neue Werte haben können.
    /// </summary>
    public event EventHandler? CloudStateLoaded;

    private readonly IPreferencesService _preferences;
    private readonly IPlayGamesService _playGames;
    private readonly IProgressService _progressService;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ICardService _cardService;
    private readonly ILogger<CloudSaveService> _logger;

    private CancellationTokenSource? _debounceCts;
    private bool _isSyncing;

    // v2.0.60 (B-E5): Sync-Semaphore — verhindert Push/Force-Race zwischen SchedulePushAsync,
    // ForceUploadAsync und ForceDownloadAsync. Vorher konnten zwei parallele Sync-Operationen
    // den Cloud-State in inkonsistenten Zustand bringen (z.B. Pull während Push).
    private readonly SemaphoreSlim _syncSemaphore = new(1, 1);

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
        ICardService cardService,
        ILogger<CloudSaveService> logger)
    {
        _preferences = preferences;
        _playGames = playGames;
        _progressService = progressService;
        _coinService = coinService;
        _gemService = gemService;
        _cardService = cardService;
        _logger = logger;
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

        // #34: Pull über dasselbe Semaphore serialisieren wie ForceUpload — verhindert Push/Pull-Race
        // (Pull baut intern via MergeBest + UploadDataAsync auch hoch). Die internen Aufrufer
        // (SchedulePushAsync/ForceUploadAsync bei Corruption) rufen diese Methode VOR ihrem eigenen
        // WaitAsync auf, und UploadDataAsync nimmt das Semaphore nicht → keine Re-Entrancy/kein Deadlock.
        await _syncSemaphore.WaitAsync();
        try
        {
            SetSyncing(true);

            var cloudJson = await _playGames.LoadCloudSaveAsync();
            if (string.IsNullOrEmpty(cloudJson))
                return false;

            var cloudData = JsonSerializer.Deserialize<CloudSaveData>(cloudJson, JsonOptions);
            if (cloudData == null)
                return false;

            // v2.0.44: Schema-Migration auf aktuelle Version anheben + Validation.
            if (!CloudSaveSchemaMigrator.TryMigrateAndValidate(cloudData, out var migrationError))
            {
                _logger.LogWarning("CloudSave: Migration/Validation fehlgeschlagen — Cloud-Daten verworfen. Grund: {Reason}", migrationError);
                return false;
            }

            // Persistenz-Korruption: Wenn ein Service beim Laden einen JSON-Parse-Fehler hatte,
            // ist der lokale State nicht vertrauenswuerdig. Cloud-Pull ERZWINGEN, um zu verhindern,
            // dass BuildCloudSaveData() einen leeren Local-State in die Cloud pusht (Data-Loss).
            if (PersistenceHealth.WasCorruptionDetected)
            {
                _logger.LogWarning("CloudSave: Lokale Corruption erkannt → Cloud-Pull wird erzwungen.");
                ApplyCloudData(cloudData);
                UpdateLastSyncTime();
                PersistenceHealth.ClearCorruptionFlag();
                return true;
            }

            // Lokalen Stand bauen
            var localData = BuildCloudSaveData();

            // v2.0.60 (B-D15): Per-Field-Merging — Maxima von Progress-Werten behalten.
            // Verhindert Data-Loss-Szenarien wie "Local 500 Stars + 50.000 Coins, Cloud 499 Stars
            // + 5.000 Coins" → ChooseBest wählte Cloud (1 Star mehr) und ließ 45.000 Coins verfallen.
            // MergeBest behält max(Stars, Coins, Gems, Cards) aus beiden Snapshots.
            var merged = CloudSaveData.MergeBest(localData, cloudData);

            // Apply merged data lokal (Maxima kommen entweder von local oder cloud — egal welcher).
            ApplyCloudData(merged);
            // Upload merged-Snapshot zurück damit Cloud-Side den merged Stand kennt.
            await UploadDataAsync(merged);
            UpdateLastSyncTime();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CloudSave Load Fehler");
            return false;
        }
        finally
        {
            SetSyncing(false);
            _syncSemaphore.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LOKAL → CLOUD (Push mit Debounce)
    // ═══════════════════════════════════════════════════════════════════════

    public async Task SchedulePushAsync()
    {
        if (!IsEnabled || !_playGames.IsSignedIn)
            return;

        // Push-seitige Corruption-Pruefung: Wenn lokale Daten als korrupt erkannt wurden,
        // KEIN Push (wuerde leeren Local-State in Cloud schieben → Data-Loss auf allen Geraeten).
        // Stattdessen: Ersten Pull erzwingen damit Cloud-State lokale Corruption ueberschreibt.
        if (PersistenceHealth.WasCorruptionDetected)
        {
            _logger.LogWarning("CloudSave: Push blockiert wegen Corruption-Flag → erzwinge Pull stattdessen.");
            await TryLoadFromCloudAsync();
            return;
        }

        // Vorherigen Debounce-Timer abbrechen
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
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

        // Gleiche Corruption-Schutz wie in SchedulePushAsync: auch direkter Upload blockiert
        // wenn Local-State nicht vertrauenswuerdig ist.
        if (PersistenceHealth.WasCorruptionDetected)
        {
            _logger.LogWarning("CloudSave: ForceUpload blockiert wegen Corruption-Flag → erzwinge Pull stattdessen.");
            await TryLoadFromCloudAsync();
            return;
        }

        // v2.0.60 (B-E5): Semaphore serialisiert Push/Pull/Force-Operationen — kein Race.
        await _syncSemaphore.WaitAsync();
        try
        {
            SetSyncing(true);
            var data = BuildCloudSaveData();
            await UploadDataAsync(data);
            UpdateLastSyncTime();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CloudSave Upload Fehler");
        }
        finally
        {
            SetSyncing(false);
            _syncSemaphore.Release();
        }
    }

    /// <summary>
    /// DSGVO Art. 17: Cloud-Save überschreiben mit leerem Snapshot (effective Delete).
    /// Wenn der User später eine neue Identität anlegt, wird der erste Pull ein leeres Objekt
    /// zurückgeben → CloudSaveData.ChooseBest entscheidet sich automatisch für lokale Daten.
    /// Best-Effort: Bei Offline/Permission still abgebrochen.
    /// </summary>
    public async Task DeleteCloudSaveAsync()
    {
        if (!_playGames.IsSignedIn) return;

        try
        {
            // Leeren Snapshot uploaden (überschreibt existierenden Snapshot)
            // v2.0.55 — Phase 15 P1-Fix: CurrentSchemaVersion statt hardcoded 1
            // (sonst triggert Re-Login eine V1→V3-Migration mit Default-Auffüllung)
            var emptyData = new CloudSaveData
            {
                Version = CloudSaveSchemaMigrator.CurrentSchemaVersion,
                TimestampUtc = DateTime.UtcNow.ToString("O"),
                TotalStars = 0,
                CoinBalance = 0,
                GemBalance = 0,
                TotalCards = 0,
                Keys = new Dictionary<string, string>()
            };
            var emptyJson = JsonSerializer.Serialize(emptyData, JsonOptions);
            await _playGames.SaveToCloudAsync(emptyJson);
            _logger.LogInformation("CloudSave: DSGVO Account-Löschung — Snapshot mit leeren Daten überschrieben.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CloudSave DeleteCloudSaveAsync Fehler");
        }
    }

    public async Task<bool> ForceDownloadAsync()
    {
        if (!IsEnabled || !_playGames.IsSignedIn)
            return false;

        // #34: Über dasselbe Semaphore serialisieren wie Push/Pull (kein paralleler Cloud-Zugriff).
        await _syncSemaphore.WaitAsync();
        try
        {
            SetSyncing(true);

            var cloudJson = await _playGames.LoadCloudSaveAsync();
            if (string.IsNullOrEmpty(cloudJson))
                return false;

            var cloudData = JsonSerializer.Deserialize<CloudSaveData>(cloudJson, JsonOptions);
            if (cloudData == null)
                return false;

            // v2.0.44: Schema-Migration auf aktuelle Version anheben + Validation.
            if (!CloudSaveSchemaMigrator.TryMigrateAndValidate(cloudData, out var migrationError))
            {
                _logger.LogWarning("CloudSave ForceDownload: Migration/Validation fehlgeschlagen. Grund: {Reason}", migrationError);
                return false;
            }

            // Cloud-Stand erzwingen (ohne Vergleich)
            ApplyCloudData(cloudData);
            UpdateLastSyncTime();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CloudSave ForceDownload Fehler");
            return false;
        }
        finally
        {
            SetSyncing(false);
            _syncSemaphore.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DATEN SAMMELN
    // ═══════════════════════════════════════════════════════════════════════

    private CloudSaveData BuildCloudSaveData()
    {
        var data = new CloudSaveData
        {
            Version = CloudSaveSchemaMigrator.CurrentSchemaVersion,
            TimestampUtc = DateTime.UtcNow.ToString("O"),
            TotalStars = _progressService.GetTotalStars(),
            CoinBalance = _coinService.Balance,
            GemBalance = _gemService.Balance,
            TotalCards = _cardService.OwnedCards.Count
        };

        // Alle Sync-Keys aus Preferences sammeln — auch leere Werte explizit aufnehmen,
        // damit ApplyCloudData lokale Sync-Keys zuruecksetzen kann (Cherry-Pick-Schutz, Audit C04).
        // Sonst koennten Mischzustaende entstehen: Premium-Skin auf Gerät A + Coins auf Gerät B
        // = beide Werte bleiben separat lokal stehen.
        foreach (var key in SyncKeys)
        {
            data.Keys[key] = _preferences.Get(key, "");
        }

        return data;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DATEN ANWENDEN
    // ═══════════════════════════════════════════════════════════════════════

    private void ApplyCloudData(CloudSaveData cloudData)
    {
        // SyncKeys[] zuerst lokal "leeren" — verhindert Mischzustaende wenn ein Cloud-Key
        // fehlt, lokal aber noch ein alter Wert steht (Audit C04). Nur Keys aus SyncKeys
        // werden gereset; andere Preferences-Keys bleiben unangetastet.
        foreach (var key in SyncKeys)
        {
            if (!cloudData.Keys.ContainsKey(key))
                _preferences.Set(key, "");
        }

        foreach (var kvp in cloudData.Keys)
        {
            _preferences.Set(kvp.Key, kvp.Value);
        }

        // Services mit internem Cache (MasterModeService, DeckTelemetryService, ...)
        // abonnieren dieses Event und rufen ihren Load() auf, damit Cache nicht stale bleibt.
        // Services ohne Cache (lesen direkt aus Preferences) ignorieren das Event.
        //
        // Dispatcher.UIThread.Post: ApplyCloudData läuft typischerweise auf einer
        // Task-Continuation nach dem Cloud-Pull — Subscriber (z.B. ViewModel-Updates)
        // erwarten aber UI-Thread. Marshal hier einmal zentral statt in jedem Handler.
        Avalonia.Threading.Dispatcher.UIThread.Post(() => CloudStateLoaded?.Invoke(this, EventArgs.Empty));
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

    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
    }
}
