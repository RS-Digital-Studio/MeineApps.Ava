using System.Text.Json;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// Handles saving and loading game state to persistent storage.
/// Uses atomic writes (temp file + rename) and backup for crash safety.
/// </summary>
public sealed class SaveGameService : ISaveGameService, IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ioLock.Dispose();
    }

    private const int IoLockTimeoutSeconds = 30;

    private readonly IGameStateService _gameStateService;
    private readonly IGameIntegrityService _integrityService;
    private readonly IPlayGamesService? _playGamesService;
    // Firebase-basierter Cloud-Save — ersetzt den nicht-funktionalen Play-Games-Snapshots-Stub.
    private readonly ICloudSaveService? _cloudSaveService;
    // M-M08: Premium-Status-Quelle. SanitizeState setzt state.IsPremium nicht mehr blind auf
    // false, sondern liest IPurchaseService.IsPremium (kaufgesichert via Preference-Cache,
    // getrennt vom Save-File) — so sieht ein Premium-Spieler bei kaputtem Netz keine Werbung.
    private readonly IPurchaseService? _purchaseService;
    // v2.1.1 (Audit C-C05): Lock-frei via Interlocked — DateTime.UtcNow.Ticks des letzten
    // Cloud-Upload-Versuchs (0 = noch nie). CompareExchange stellt sicher, dass bei parallelen
    // Saves nur ein Thread den Upload-Slot pro Rate-Limit-Fenster gewinnt.
    private long _lastCloudUploadTicks;
    private static readonly long CloudUploadMinIntervalTicks = TimeSpan.FromMinutes(2).Ticks;
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    public event Action<string, string>? ErrorOccurred;
    private readonly string _saveFileName = "handwerker_imperium_save.json";
    private readonly string _backupFileName = "handwerker_imperium_save.bak";
    private readonly JsonSerializerOptions _jsonOptions;

    private static string AppDataDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HandwerkerImperium");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public string SaveFilePath => Path.Combine(AppDataDirectory, _saveFileName);
    private string BackupFilePath => Path.Combine(AppDataDirectory, _backupFileName);
    private string TempFilePath => SaveFilePath + ".tmp";
    public bool SaveExists => File.Exists(SaveFilePath);

    /// <summary>
    /// v2.1.1 (Audit H-H09): True, wenn der letzte <see cref="LoadAsync"/>-Aufruf zwar Save-Dateien
    /// vorfand, aber alle beschaedigt waren (Haupt- UND Backup-Datei). Der Aufrufer kann darauf einen
    /// Cloud-Recovery-Flow anbieten, statt den Spieler kommentarlos mit CreateNew() zu starten.
    /// </summary>
    public bool LastLoadFailedCorrupt { get; private set; }

    public SaveGameService(
        IGameStateService gameStateService,
        IGameIntegrityService integrityService,
        IPlayGamesService? playGamesService = null,
        ICloudSaveService? cloudSaveService = null,
        IPurchaseService? purchaseService = null)
    {
        _gameStateService = gameStateService;
        _integrityService = integrityService;
        _playGamesService = playGamesService;
        _cloudSaveService = cloudSaveService;
        _purchaseService = purchaseService;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task SaveAsync()
    {
        // H-H05: ConfigureAwait(false) durchgehend — erlaubt deadlock-freies synchrones
        // Warten in Android OnPause (PauseGameLoopAsync().GetAwaiter().GetResult()).
        // ErrorOccurred-Handler dispatcht selbst auf den UI-Thread (MainViewModel.cs).
        if (!await _ioLock.WaitAsync(TimeSpan.FromSeconds(IoLockTimeoutSeconds)).ConfigureAwait(false))
        {
            System.Diagnostics.Debug.WriteLine("[HandwerkerImperium] SaveAsync: IO-Lock Timeout - Save übersprungen");
            return;
        }
        try
        {
            await SaveInternalAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerImperium] SaveAsync Fehler: {ex.Message}");
            // Temp-Datei aufräumen
            try { if (File.Exists(TempFilePath)) File.Delete(TempFilePath); } catch { /* Aufräum-Fehler ignorieren */ }
            ErrorOccurred?.Invoke("Error", "SaveErrorMessage");
        }
        finally
        {
            _ioLock.Release();
        }
    }

    /// <summary>
    /// Lock-freie Save-Implementierung. Muss vom Aufrufer unter _ioLock gehalten werden.
    /// Wird von SaveAsync() und ImportSaveAsync() genutzt — letzteres haelt den Lock bereits fuer den
    /// gesamten Initialize+Save-Pfad (sonst kann GameLoop zwischen Initialize und Save ticken).
    /// </summary>
    private async Task SaveInternalAsync()
    {
        var state = _gameStateService.State;

        // v2.1.0/P-C01: Snapshot-Pattern — Lock-Hold-Zeit minimieren.
        // - Lock: nur LastSavedAt setzen + Signature berechnen + Serialize
        // - Off-Lock: File-IO, Cloud-Upload (alles auf ThreadPool)
        // - Der Background-Thread blockiert UI-Thread nur kurz waehrend Lock-Aequisition;
        //   GameLoop tickt NUR unter dem gleichen Lock und kann nicht mit Save kollidieren.
        // v2.1.1 (Audit P-C01): SerializeToUtf8Bytes statt Serialize(string) — spart die intermediate
        // UTF-16-String-Allokation UND die spaetere UTF-16→UTF-8-Konvertierung beim File-Write.
        // Reduziert Lock-Hold-Zeit + GC-Druck spuerbar bei grossen Late-Game-Saves. Ein echter
        // Deep-Clone-Snapshot ausserhalb des Locks waere fehleranfaellig (jedes neue GameState-
        // Feld muesste mitgepflegt werden → Daten-Loss-Risiko) — die Serialisierung IST der
        // konsistente Snapshot, sie nur effizienter zu machen ist der sichere Weg.
#if DEBUG
        var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
        var (jsonBytes, cloudLevel, cloudMetadata, cloudSaveEnabled) = await Task.Run(() =>
            _gameStateService.ExecuteWithLock(() =>
        {
            state.LastSavedAt = DateTime.UtcNow;
            _integrityService.ComputeSignature(state);
            var serialized = JsonSerializer.SerializeToUtf8Bytes(state, _jsonOptions);
            // Metadaten aus dem gleichen Lock-Snapshot — sonst kann GameLoop zwischen
            // Serialize und Metadata-Bau einen Level-Up oder Goldene-Schraube-Add ticken.
            var meta = new CloudSaveMetadata
            {
                PlayerLevel = state.PlayerLevel,
                Money = state.Money,
                GoldenScrews = state.GoldenScrews,
                PrestigePoints = state.Prestige.PrestigePoints,
                AscensionLevel = state.Ascension.AscensionLevel,
                SavedAtIso = state.LastSavedAt.ToString("O"),
                StateVersion = state.Version,
                AppVersion = typeof(SaveGameService).Assembly.GetName().Version?.ToString(3) ?? "unknown"
            };
            return (json: serialized,
                    cloudLevel: state.PlayerLevel,
                    metadata: meta,
                    cloudEnabled: state.Settings.CloudSaveEnabled);
        })).ConfigureAwait(false);
#if DEBUG
        sw.Stop();
        if (sw.ElapsedMilliseconds > 50)
            System.Diagnostics.Debug.WriteLine($"[SaveGameService] Slow save snapshot: {sw.ElapsedMilliseconds}ms");
#endif

        await File.WriteAllBytesAsync(TempFilePath, jsonBytes).ConfigureAwait(false);

        // v2.1.1 (Audit M-M13): Atomares Move statt Copy fuer das Backup. File.Move ist auf demselben Volume
        // atomar (Rename) — nach dem Move ist das Backup garantiert die vollstaendige alte Datei.
        // File.Copy konnte bei einem Crash mittendrin ein halb geschriebenes Backup hinterlassen.
        if (File.Exists(SaveFilePath))
        {
            File.Move(SaveFilePath, BackupFilePath, overwrite: true);
        }

        File.Move(TempFilePath, SaveFilePath, overwrite: true);

        // Cloud-Save (Firebase-REST) parallel (fire-and-forget, blockiert lokales Save nie).
        // Rate-Limit: Max. alle 2 Minuten uploaden damit Firebase-Kosten kontrolliert bleiben.
        // Der Save ist lokal immer konsistent — Cloud ist nur Backup.
        if (_cloudSaveService?.IsAvailable == true && cloudSaveEnabled)
        {
            // C-C05: Rate-Limit lock-frei. CompareExchange gewinnt den Upload-Slot nur,
            // wenn _lastCloudUploadTicks seit dem Lesen unveraendert blieb — verhindert
            // doppelten Cloud-Upload bei parallelen Saves.
            long nowTicks = DateTime.UtcNow.Ticks;
            long lastTicks = Interlocked.Read(ref _lastCloudUploadTicks);
            if (nowTicks - lastTicks >= CloudUploadMinIntervalTicks
                && Interlocked.CompareExchange(ref _lastCloudUploadTicks, nowTicks, lastTicks) == lastTicks)
            {
                var cloudSvc = _cloudSaveService;
                // Race-frei: JSON + Metadata sind bereits "frozen" (kein State-Zugriff im Background).
                // Vermeidet "JsonSerializer.Serialize auf Background-Thread → Collection-modified-Crash".
                // byte[] → string fuer den Cloud-Upload: einmalige Konvertierung ausserhalb des Locks.
                var jsonForCloud = System.Text.Encoding.UTF8.GetString(jsonBytes);
                var metadataForCloud = cloudMetadata;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var ok = await cloudSvc.UploadJsonAsync(jsonForCloud, metadataForCloud).ConfigureAwait(false);
                        if (ok)
                        {
                            // v2.1.1 (Audit P-H09): LastCloudSaveTime ist ein reiner Anzeige-Wert (SettingsView).
                            // Direktes Schreiben ohne State-Lock — eine DateTime-Zuweisung ist atomar
                            // genug, und der naechste regulaere Save ueberschreibt den Wert ohnehin.
                            // Spart eine unnoetige Lock-Uebernahme im Cloud-Upload-Hot-Path.
                            state.Settings.LastCloudSaveTime = DateTime.UtcNow;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandwerkerImperium] Cloud-Save Fehler: {ex.Message}");
                    }
                });
            }
        }
    }

    public async Task<GameState?> LoadAsync()
    {
        LastLoadFailedCorrupt = false;

        if (!await _ioLock.WaitAsync(TimeSpan.FromSeconds(IoLockTimeoutSeconds)).ConfigureAwait(false))
        {
            System.Diagnostics.Debug.WriteLine("[HandwerkerImperium] LoadAsync: IO-Lock Timeout - Load übersprungen");
            return null;
        }
        try
        {
            bool mainExists = SaveExists;
            bool backupExists = File.Exists(BackupFilePath);

            // Gar kein Spielstand vorhanden → legitimer neuer Spieler (kein Corrupt-Signal).
            if (!mainExists && !backupExists)
                return null;

            // Haupt-Datei zuerst versuchen.
            if (mainExists)
            {
                var state = await LoadFromFileAsync(SaveFilePath).ConfigureAwait(false);
                if (state != null) return state;
            }

            // Haupt-Datei fehlte oder war beschaedigt → Backup versuchen.
            if (backupExists)
            {
                var backupState = await LoadFromFileAsync(BackupFilePath).ConfigureAwait(false);
                if (backupState != null) return backupState;
            }

            // H-H09: Es GAB Save-Dateien, aber alle waren beschaedigt — Signal fuer den
            // Aufrufer, einen Cloud-Recovery-Flow anzubieten statt kommentarlos CreateNew().
            LastLoadFailedCorrupt = true;
            return null;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<GameState?> LoadFromFileAsync(string path)
    {
        try
        {
            string json = await File.ReadAllTextAsync(path);
            var state = JsonSerializer.Deserialize<GameState>(json, _jsonOptions);

            if (state != null)
            {
                state = MigrateState(state);
                SanitizeState(state);
                _gameStateService.Initialize(state);
            }

            return state;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerImperium] LoadFromFile Fehler ({path}): {ex.Message}");
            ErrorOccurred?.Invoke("Error", "LoadErrorMessage");
            return null;
        }
    }

    public async Task DeleteSaveAsync()
    {
        if (!await _ioLock.WaitAsync(TimeSpan.FromSeconds(IoLockTimeoutSeconds)))
        {
            System.Diagnostics.Debug.WriteLine("[HandwerkerImperium] DeleteSaveAsync: IO-Lock Timeout - Delete übersprungen");
            return;
        }
        try
        {
            if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
            if (File.Exists(BackupFilePath)) File.Delete(BackupFilePath);
            if (File.Exists(TempFilePath)) File.Delete(TempFilePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerImperium] DeleteSave Fehler: {ex.Message}");
            ErrorOccurred?.Invoke("Error", "DeleteSaveErrorMessage");
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public Task<string> ExportSaveAsync()
    {
        var state = _gameStateService.State;
        if (state == null) return Task.FromResult(string.Empty);

        // Kein IO-Lock nötig: kein File-IO, und Aufrufer ist UI-Thread (identisch mit GameLoop).
        // Cloud-Save (Zeile 83) liest nur den bereits geschriebenen File, kein Race.
        string json = JsonSerializer.Serialize(state, _jsonOptions);
        return Task.FromResult(json);
    }

    public async Task<bool> ImportSaveAsync(string json)
    {
        // Import+Save muessen atomar unter _ioLock laufen, sonst kann GameLoop zwischen
        // Initialize(state) und SaveAsync() ticken und den importierten State ueberschreiben.
        if (!await _ioLock.WaitAsync(TimeSpan.FromSeconds(IoLockTimeoutSeconds)))
        {
            System.Diagnostics.Debug.WriteLine("[HandwerkerImperium] ImportSaveAsync: IO-Lock Timeout - Import abgebrochen");
            return false;
        }
        try
        {
            var state = JsonSerializer.Deserialize<GameState>(json, _jsonOptions);
            if (state == null) return false;

            state = MigrateState(state);
            SanitizeState(state);
            _gameStateService.Initialize(state);
            await SaveInternalAsync();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerImperium] ImportSave Fehler: {ex.Message}");
            ErrorOccurred?.Invoke("Error", "ImportErrorMessage");
            return false;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    /// <summary>
    /// Korrigiert ungültige Werte im geladenen State.
    /// Repariert statt abzulehnen - so gehen keine Savegames verloren.
    /// M-M08: Nicht mehr static — liest <see cref="IPurchaseService"/> fuer den Premium-Status.
    /// </summary>
    private void SanitizeState(GameState state)
    {
        // Sub-Objekte (V5): null-Safety vor allen anderen Zugriffen
        state.Boosts ??= new BoostData();
        state.DailyProgress ??= new DailyProgressData();
        state.Cosmetics ??= new CosmeticData();

        // Basis-Werte mit Ober-Caps (Exploit-Schutz gegen Save-Editing)
        if (state.PlayerLevel < LevelThresholds.MinPlayerLevel) state.PlayerLevel = LevelThresholds.MinPlayerLevel;
        if (state.PlayerLevel > LevelThresholds.MaxPlayerLevel) state.PlayerLevel = LevelThresholds.MaxPlayerLevel;
        if (state.Money < 0) state.Money = 0;
        // v2.1.1 (Audit M-M07): Money-Cap dynamisch — Geld kann nie mehr sein als je verdient wurde (logisch
        // korrekt), mit 1e15-Floor fuer Late-Game-Spieler (Prestige x20 + Rush erreicht 100B
        // in ~50h). Der alte feste Cap (100B) hat valide Late-Game-Saves verschluckt.
        decimal moneyCap = Math.Max(1_000_000_000_000_000m, state.TotalMoneyEarned);
        if (state.Money > moneyCap) state.Money = moneyCap;
        if (state.CurrentXp < 0) state.CurrentXp = 0;
        if (state.GoldenScrews < 0) state.GoldenScrews = 0;
        if (state.GoldenScrews > 100_000) state.GoldenScrews = 100_000;
        if (state.TotalMoneyEarned < 0) state.TotalMoneyEarned = 0;
        if (state.CurrentRunMoney < 0) state.CurrentRunMoney = 0;
        if (state.TotalMoneySpent < 0) state.TotalMoneySpent = 0;

        // Workshops: mindestens ein Carpenter muss existieren
        state.Workshops ??= [];
        state.UnlockedWorkshopTypes ??= [];
        if (state.Workshops.Count == 0)
        {
            state.UnlockedWorkshopTypes.Add(WorkshopType.Carpenter);
            var carpenter = Workshop.Create(WorkshopType.Carpenter);
            carpenter.IsUnlocked = true;
            state.Workshops.Add(carpenter);
        }

        // Workshop-Levels: 1-1000
        foreach (var ws in state.Workshops)
        {
            if (ws.Level < 1) ws.Level = 1;
            if (ws.Level > Workshop.MaxLevel) ws.Level = Workshop.MaxLevel;
        }

        // Prestige: darf nicht null sein, Werte validieren
        state.Prestige ??= new PrestigeData();
        if (state.Prestige.PrestigePoints < 0) state.Prestige.PrestigePoints = 0;
        if (state.Prestige.BronzeCount < 0) state.Prestige.BronzeCount = 0;
        if (state.Prestige.SilverCount < 0) state.Prestige.SilverCount = 0;
        if (state.Prestige.GoldCount < 0) state.Prestige.GoldCount = 0;
        if (state.Prestige.PlatinCount < 0) state.Prestige.PlatinCount = 0;
        if (state.Prestige.DiamantCount < 0) state.Prestige.DiamantCount = 0;
        if (state.Prestige.MeisterCount < 0) state.Prestige.MeisterCount = 0;
        if (state.Prestige.LegendeCount < 0) state.Prestige.LegendeCount = 0;
        // PermanentMultiplier: Minimum 1.0 (kein Prestige), Maximum 20.0 (konsistent mit PrestigeService.MaxPermanentMultiplier)
        if (state.Prestige.PermanentMultiplier < 1.0m) state.Prestige.PermanentMultiplier = 1.0m;
        if (state.Prestige.PermanentMultiplier > 20.0m) state.Prestige.PermanentMultiplier = 20.0m;
        state.Prestige.PurchasedShopItems ??= [];
        // Prestige-Shop-Items: Nur gültige IDs behalten (Exploit-Schutz, statisches HashSet)
        var validShopIds = PrestigeShop.GetValidIds();
        state.Prestige.PurchasedShopItems.RemoveAll(id => !validShopIds.Contains(id));

        // Daily Reward Streak
        if (state.DailyRewardStreak < 0) state.DailyRewardStreak = 0;

        // Worker-Daten validieren
        foreach (var ws in state.Workshops)
        {
            // AdBonusWorkerSlots auf Cap begrenzen (Exploit-Schutz)
            if (ws.AdBonusWorkerSlots > Workshop.MaxAdBonusWorkerSlots)
                ws.AdBonusWorkerSlots = Workshop.MaxAdBonusWorkerSlots;

            ws.Workers ??= [];
            foreach (var worker in ws.Workers)
            {
                worker.Mood = Math.Clamp(worker.Mood, 0m, 100m);
                worker.Fatigue = Math.Clamp(worker.Fatigue, 0m, 100m);
                if (worker.ExperienceLevel < 1) worker.ExperienceLevel = 1;
                if (worker.ExperienceXp < 0) worker.ExperienceXp = 0;
                // AssignedWorkshop muss zum Workshop passen, in dem der Worker steckt
                if (worker.AssignedWorkshop != ws.Type)
                    worker.AssignedWorkshop = ws.Type;
                // Löhne auf aktuellen Tier-Wert korrigieren (Balance-Update Migration)
                worker.WagePerHour = worker.Tier.GetWagePerHour();
                // Effizienz auf gültigen Tier-Bereich clampen (Balance-Update Migration)
                var minEff = worker.Tier.GetMinEfficiency();
                var maxEff = worker.Tier.GetMaxEfficiency();
                if (worker.Efficiency < minEff || worker.Efficiency > maxEff)
                    worker.Efficiency = Math.Clamp(worker.Efficiency, minEff, maxEff);

                // V7 (Phase 4): Material-Affinity deterministisch fuer alte Saves zuweisen.
                if (worker.MaterialAffinity == MaterialAffinity.None && !string.IsNullOrEmpty(worker.Id))
                {
                    // 5 Achsen (1-5), deterministisch per WorkerId-Hash
                    int hash = Math.Abs(worker.Id.GetHashCode());
                    worker.MaterialAffinity = (MaterialAffinity)((hash % 5) + 1);
                }
            }
        }

        // Reputation validieren
        state.Reputation ??= new CustomerReputation();
        state.Reputation.ReputationScore = Math.Clamp(state.Reputation.ReputationScore, 0, 100);

        // Listen: null-Safety (VOR der Iteration!)
        state.Buildings ??= [];
        state.Researches ??= [];
        state.AvailableOrders ??= [];
        // v2.0.35 Feature A: Multi-Order-System — null-safe init + Konsistenz-Checks.
        state.ParallelOrdersByWorkshop ??= new Dictionary<WorkshopType, Order>();
        // Verwaiste Eintraege entfernen: Key ≠ Value.WorkshopType (Save-Editor-Schutz),
        // abgelaufene Auftraege, oder nicht-freigeschaltete Workshops.
        var invalidParallelKeys = new List<WorkshopType>();
        foreach (var kv in state.ParallelOrdersByWorkshop)
        {
            var order = kv.Value;
            if (order == null
                || order.WorkshopType != kv.Key
                || order.IsExpired
                || !state.UnlockedWorkshopTypes.Contains(kv.Key))
            {
                invalidParallelKeys.Add(kv.Key);
            }
        }
        foreach (var k in invalidParallelKeys)
            state.ParallelOrdersByWorkshop.Remove(k);
        // Exploit-Schutz gegen manipulierte Saves: hartes Cap auf MaxParallelOrders.
        while (state.ParallelOrdersByWorkshop.Count > GameBalanceConstants.MaxParallelOrders)
        {
            var firstKey = state.ParallelOrdersByWorkshop.Keys.First();
            state.ParallelOrdersByWorkshop.Remove(firstKey);
        }

        // Research-Tree aus Template synchronisieren (fehlende Nodes ergänzen, DurationTicks/Effect aktualisieren)
        SyncResearchTree(state);

        // Building-Levels validieren
        foreach (var building in state.Buildings)
        {
            if (building.Level < 0) building.Level = 0;
            if (building.Level > building.Type.GetMaxLevel())
                building.Level = building.Type.GetMaxLevel();
        }
        state.UnlockedAchievements ??= [];
        state.QuickJobs ??= [];
        state.EventHistory ??= [];
        state.DailyChallengeState ??= new DailyChallengeState();
        state.CollectedMasterTools ??= [];
        // MasterTools: Nur gültige IDs behalten (Exploit-Schutz, statisches HashSet)
        var validToolIds = MasterTool.GetValidIds();
        state.CollectedMasterTools.RemoveAll(id => !validToolIds.Contains(id));
        state.Tools ??= [];
        // Tool-Migration: Fehlende ToolTypes ergänzen (z.B. nach Update von 4 auf 8 Tools)
        var existingToolTypes = state.Tools.Select(t => t.Type).ToHashSet();
        foreach (var defaultTool in Tool.CreateDefaults())
        {
            if (!existingToolTypes.Contains(defaultTool.Type))
                state.Tools.Add(defaultTool);
        }
        state.ViewedStoryIds ??= [];
        state.Tutorial.SeenMiniGameTutorials ??= [];
        state.Tutorial.SeenHints ??= [];

        // Welle 1-8 Migrationen: Neue Properties null-safe initialisieren
        state.LuckySpin ??= new LuckySpinState();
        state.WeeklyMissionState ??= new WeeklyMissionState();
        state.EquipmentInventory ??= [];
        state.Managers ??= [];
        state.CraftingInventory ??= new Dictionary<string, int>();
        state.ActiveCraftingJobs ??= [];
        state.Friends ??= [];
        state.ClaimedLevelOffers ??= [];
        state.CompletedRecipeIds ??= [];
        state.PerfectMiniGameTypes ??= [];

        // V7 (Phase 1 Ressourcen-Plan): Warehouse-Felder
        state.ReservedInventory ??= new Dictionary<string, int>();
        state.AutoSellRules ??= new Dictionary<string, AutoSellRule>();
        state.HeirloomItems ??= [];
        if (state.WarehouseSlotCount < 20) state.WarehouseSlotCount = 20;
        if (state.WarehouseSlotCount > WarehouseService.MaxSlots) state.WarehouseSlotCount = WarehouseService.MaxSlots;
        if (state.WarehouseStackLimit < 50) state.WarehouseStackLimit = 50;
        if (state.WarehouseStackLimit > 9999) state.WarehouseStackLimit = 9999;

        // CraftingInventory: Stack-Limit hart durchsetzen (Save-Editor-Schutz)
        var inventoryKeys = state.CraftingInventory.Keys.ToList();
        foreach (var productId in inventoryKeys)
        {
            int count = state.CraftingInventory[productId];
            if (count <= 0)
            {
                state.CraftingInventory.Remove(productId);
                continue;
            }
            if (count > state.WarehouseStackLimit)
                state.CraftingInventory[productId] = state.WarehouseStackLimit;
        }

        // ReservedInventory darf nie groesser sein als CraftingInventory
        var reservedKeys = state.ReservedInventory.Keys.ToList();
        foreach (var productId in reservedKeys)
        {
            int reserved = state.ReservedInventory[productId];
            int available = state.CraftingInventory.GetValueOrDefault(productId, 0);
            if (reserved <= 0 || available <= 0)
            {
                state.ReservedInventory.Remove(productId);
                continue;
            }
            if (reserved > available)
                state.ReservedInventory[productId] = available;
        }

        // V7 (Phase 2): Orphan-Reservations erkennen — Reservierungen die zu keinem
        // aktiven Auftrag (ActiveOrder + ParallelOrdersByWorkshop) mit MaterialOfferAccepted
        // gehoeren. Solche Reservierungen werden freigegeben, damit der Spieler nichts blockiert hat.
        var expectedReservations = new Dictionary<string, int>();
        void AggregateReservations(Order? o)
        {
            if (o == null || !o.MaterialOfferAccepted || o.MaterialOffer == null) return;
            foreach (var (id, count) in o.MaterialOffer)
                expectedReservations[id] = expectedReservations.GetValueOrDefault(id, 0) + count;
        }
        AggregateReservations(state.ActiveOrder);
        foreach (var kv in state.ParallelOrdersByWorkshop)
            if (kv.Value != state.ActiveOrder) AggregateReservations(kv.Value);

        var orphanKeys = state.ReservedInventory.Keys.ToList();
        foreach (var productId in orphanKeys)
        {
            int reserved = state.ReservedInventory[productId];
            int expected = expectedReservations.GetValueOrDefault(productId, 0);
            if (reserved > expected)
            {
                if (expected <= 0)
                    state.ReservedInventory.Remove(productId);
                else
                    state.ReservedInventory[productId] = expected;
            }
        }

        // Workshop Rebirth Stars validieren (0-5 pro Workshop)
        state.WorkshopStars ??= new Dictionary<string, int>();
        var invalidStarKeys = state.WorkshopStars
            .Where(kv => kv.Value < 0 || kv.Value > 5)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in invalidStarKeys)
            state.WorkshopStars[key] = Math.Clamp(state.WorkshopStars[key], 0, 5);

        // Ascension-Daten validieren (reserviert für zukünftige Funktionalität, Save-Kompatibilität)
        state.Ascension ??= new AscensionData();
        state.Ascension.Perks ??= new Dictionary<string, int>();
        state.Ascension.PermanentHeirlooms ??= [];
        if (state.Ascension.AscensionLevel < 0) state.Ascension.AscensionLevel = 0;
        if (state.Ascension.AscensionPoints < 0) state.Ascension.AscensionPoints = 0;

        // V7 (Phase 4): HeirloomItems & PermanentHeirlooms validieren — nur Heirloom-faehige Produkt-IDs.
        var allProductsForHeirlooms = CraftingProduct.GetAllProducts();
        state.HeirloomItems.RemoveAll(id =>
            !allProductsForHeirlooms.TryGetValue(id, out var p) || !p.IsHeirloomEligible);
        // Imperium-Pass-Spieler bekommen +1 Slot (Plan Section 10.2). Sanitize wendet den
        // SaveGame-State an, aber Premium kann sich aendern → wir clampen auf den Pass-Cap,
        // das ist der hoechste; bei Prestige clamped der PrestigeService dann strenger.
        int heirloomCap = GameBalanceConstants.MaxHeirloomsPerRunPremium;
        if (state.HeirloomItems.Count > heirloomCap)
            state.HeirloomItems.RemoveRange(heirloomCap, state.HeirloomItems.Count - heirloomCap);
        state.Ascension.PermanentHeirlooms.RemoveAll(id =>
            !allProductsForHeirlooms.TryGetValue(id, out var p) || !p.IsHeirloomEligible);

        if (state.Statistics.TotalWorkersTrained < 0) state.Statistics.TotalWorkersTrained = 0;
        if (state.Statistics.TotalItemsCrafted < 0) state.Statistics.TotalItemsCrafted = 0;
        if (state.Statistics.TotalTournamentsWon < 0) state.Statistics.TotalTournamentsWon = 0;

        // v2.1.1 (Audit M-M08): Premium-Status aus IPurchaseService statt blind auf false. Der Wert kommt
        // aus dem kaufgesicherten Preference-Cache (is_premium/has_subscription/has_lifetime),
        // der getrennt vom Save-File liegt — Save-Editing kann ihn nicht manipulieren. So sieht
        // ein Premium-Spieler auch bei kaputtem Netz keine Werbung; RestorePurchasesAsync beim
        // App-Start ist dann nur noch eine Auffrischung.
        state.IsPremium = _purchaseService?.IsPremium ?? false;
        // BattlePass-Saison-Premium + Prestige-Pass haben eigene Restore-Pfade — bleiben false
        // (Exploit-Schutz gegen Save-Editing).
        if (state.BattlePass != null)
            state.BattlePass.IsPremium = false;
        state.IsPrestigePassActive = false;

        // Lieferant: Abgelaufene Lieferung entfernen
        if (state.PendingDelivery?.IsExpired == true)
            state.PendingDelivery = null;
        if (state.Statistics.TotalDeliveriesClaimed < 0) state.Statistics.TotalDeliveriesClaimed = 0;
    }

    /// <summary>
    /// Synchronisiert den Research-Tree aus dem Template.
    /// Aktualisiert DurationTicks, Effect, Cost, Prerequisites bei bestehenden Nodes,
    /// ergänzt fehlende Nodes (z.B. nach Update mit neuen Forschungen).
    /// Validiert ActiveResearchId-Konsistenz.
    /// </summary>
    private static void SyncResearchTree(GameState state)
    {
        var template = ResearchTree.CreateAll();
        var existingById = state.Researches.ToDictionary(r => r.Id, r => r);

        foreach (var tmpl in template)
        {
            if (existingById.TryGetValue(tmpl.Id, out var existing))
            {
                // Template-Daten aktualisieren (Balance-Updates, Bug-Fixes)
                existing.DurationTicks = tmpl.DurationTicks;
                existing.Effect = tmpl.Effect;
                existing.Cost = tmpl.Cost;
                existing.Prerequisites = tmpl.Prerequisites;
                existing.Branch = tmpl.Branch;
                existing.Level = tmpl.Level;
                existing.NameKey = tmpl.NameKey;
                existing.DescriptionKey = tmpl.DescriptionKey;
            }
            else
            {
                // Fehlende Node ergänzen (neuer Content nach Update)
                state.Researches.Add(tmpl);
            }
        }

        // ActiveResearchId-Konsistenz prüfen
        if (!string.IsNullOrEmpty(state.ActiveResearchId))
        {
            var active = state.Researches.FirstOrDefault(r => r.Id == state.ActiveResearchId);
            if (active == null)
            {
                // Referenzierte Forschung existiert nicht → zurücksetzen
                state.ActiveResearchId = null;
            }
            else if (!active.IsActive || active.StartedAt == null)
            {
                // Inkonsistenter Zustand: ActiveResearchId gesetzt aber Node nicht aktiv
                state.ActiveResearchId = null;
                active.IsActive = false;
                active.StartedAt = null;
            }
        }

        // Umgekehrt: Nodes die IsActive=true haben aber nicht ActiveResearchId sind → zurücksetzen
        foreach (var research in state.Researches)
        {
            if (research.IsActive && research.Id != state.ActiveResearchId)
            {
                research.IsActive = false;
                research.StartedAt = null;
            }
        }
    }

    /// <summary>
    /// Führt alle notwendigen Versionsmigrationen durch (v1→v2→v3→v5).
    /// Zentrale Methode für LoadFromFileAsync und ImportSaveAsync.
    /// </summary>
    // P0.1 AAA-Audit: internal damit Property-Based Tests die Migration durch alle Stufen pruefen koennen.
    internal static GameState MigrateState(GameState state)
    {
        if (state.Version < 2)
            state = MigrateFromV1(state);

        if (state.Version < 3)
        {
            state.WorkshopStars ??= new Dictionary<string, int>();
            state.Version = 3;
        }

        // V3/V4 → V5: Sub-Objekte für Boosts, DailyProgress, Cosmetics.
        // Die Legacy-Weiterleitungs-Properties in GameState leiten get/set an die
        // Sub-Objekte weiter. System.Text.Json deserialisiert die alten flachen Properties
        // direkt in die Sub-Objekte über diese Weiterleitungen. Daher ist keine
        // explizite Datenübernahme nötig - nur die Version hochsetzen.
        if (state.Version < 5)
        {
            // Null-Safety für die neuen Sub-Objekte (falls JSON sie nicht enthält)
            state.Boosts ??= new BoostData();
            state.DailyProgress ??= new DailyProgressData();
            state.Cosmetics ??= new CosmeticData();
            state.Version = 5;
        }

        // V5 → V6 (v2.0.35): Multi-Order-System. Bei alten Saves mit ActiveOrder
        // wird dieser in das neue ParallelOrdersByWorkshop-Dictionary migriert.
        //
        // v2.0.37 Audit-Fix K5: Audit hat „IsAccepted"-Flag in V5 angenommen — das gibt es
        // aber nicht. In V5 war ActiveOrder die einzige Quelle fuer „in Bearbeitung". Pause-
        // Mechanik (Order.PausedAt) wurde erst in V6 (Sprint 2) eingefuehrt. Damit ist die
        // urspruengliche Migration korrekt — kein Datenverlust-Pfad fuer V5-Saves.
        if (state.Version < 6)
        {
            state.ParallelOrdersByWorkshop ??= new Dictionary<WorkshopType, Order>();
            if (state.ActiveOrder != null
                && !state.ParallelOrdersByWorkshop.ContainsKey(state.ActiveOrder.WorkshopType))
            {
                state.ParallelOrdersByWorkshop[state.ActiveOrder.WorkshopType] = state.ActiveOrder;
            }
            state.Version = 6;
        }

        // V6 → V7 (Phase 1 Ressourcen-Plan): Lager-Slots, Stack-Limits, ReservedInventory.
        // Defaults: 20 Slots, Stack-Limit 50. Vorhandene CraftingInventory-Eintraege werden
        // ggf. auf das Stack-Limit gekuerzt; die Differenz wird als Geld gutgeschrieben
        // (1:1 zu BaseValue, damit kein wertvoller Bestand verloren geht).
        if (state.Version < 7)
        {
            if (state.WarehouseSlotCount <= 0) state.WarehouseSlotCount = 20;
            if (state.WarehouseStackLimit <= 0) state.WarehouseStackLimit = 50;
            state.ReservedInventory ??= new Dictionary<string, int>();
            state.AutoSellRules ??= new Dictionary<string, AutoSellRule>();
            state.HeirloomItems ??= [];

            // Stack-Truncation: alte Saves konnten unbegrenzt stapeln.
            // Ueberschuss wird zum BaseValue (kein Sell-Multiplier!) ausbezahlt damit der
            // Spieler nichts verliert — der konservative Pfad ist intentional, weil die
            // V6-Inventories oft Massen-Auto-Production gesammelt haben.
            var allProducts = CraftingProduct.GetAllProducts();
            decimal compensation = 0m;
            var keys = state.CraftingInventory.Keys.ToList();
            foreach (var productId in keys)
            {
                int count = state.CraftingInventory[productId];
                if (count <= state.WarehouseStackLimit) continue;

                int overflow = count - state.WarehouseStackLimit;
                state.CraftingInventory[productId] = state.WarehouseStackLimit;

                if (allProducts.TryGetValue(productId, out var product))
                    compensation += product.BaseValue * overflow;
            }
            if (compensation > 0)
                state.Money += compensation;

            state.Version = 7;
        }

        return state;
    }

    /// <summary>
    /// Migriert einen v1-Spielstand ins v2-Format.
    /// Konvertiert Worker-Daten, Prestige und initialisiert neue Collections.
    /// </summary>
    private static GameState MigrateFromV1(GameState old)
    {
        if (old.Version >= 2) return old;

        old.Version = 2;

        // Worker migrieren: alte Worker hatten feste 1.0 Effizienz
        foreach (var ws in old.Workshops)
        {
            ws.IsUnlocked = true;
            foreach (var worker in ws.Workers)
            {
                worker.Tier = WorkerTier.E;
                worker.Talent = 3;
                worker.Personality = WorkerPersonality.Steady;
                worker.Mood = 80m;
                worker.Fatigue = 0m;
                worker.ExperienceLevel = Math.Min(10, worker.SkillLevel);
                worker.WagePerHour = WorkerTier.E.GetWagePerHour();
                worker.AssignedWorkshop = ws.Type;
            }

            if (!old.UnlockedWorkshopTypes.Contains(ws.Type))
                old.UnlockedWorkshopTypes.Add(ws.Type);
        }

        // Prestige migrieren
        old.Prestige = new PrestigeData
        {
            BronzeCount = old.PrestigeLevel,
            PermanentMultiplier = old.PrestigeMultiplier,
            CurrentTier = old.PrestigeLevel > 0 ? PrestigeTier.Bronze : PrestigeTier.None
        };

        // Reputation initialisieren
        old.Reputation ??= new CustomerReputation();

        // Leere Collections initialisieren
        old.Buildings ??= [];
        old.EventHistory ??= [];

        // Research-Tree initialisieren
        if (old.Researches == null || old.Researches.Count == 0)
        {
            old.Researches = ResearchTree.CreateAll();
        }
        else
        {
            // Prerequisites aus der aktuellen ResearchTree-Definition synchronisieren
            // (damit Änderungen am Baum-Layout auch bei bestehenden Spielständen wirken)
            var template = ResearchTree.CreateAll();
            foreach (var tmpl in template)
            {
                var existing = old.Researches.FirstOrDefault(r => r.Id == tmpl.Id);
                if (existing != null)
                    existing.Prerequisites = tmpl.Prerequisites;
            }
        }

        return old;
    }
}
