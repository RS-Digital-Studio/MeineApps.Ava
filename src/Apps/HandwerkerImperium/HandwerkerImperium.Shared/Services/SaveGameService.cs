using System.Text.Json;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Handles saving and loading game state to persistent storage.
/// Uses atomic writes (temp file + rename) and backup for crash safety.
/// </summary>
public sealed class SaveGameService : ISaveGameService
{
    private readonly IGameStateService _gameStateService;
    private readonly IPlayGamesService? _playGamesService;
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

    public SaveGameService(IGameStateService gameStateService, IPlayGamesService? playGamesService = null)
    {
        _gameStateService = gameStateService;
        _playGamesService = playGamesService;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task SaveAsync()
    {
        if (!await _ioLock.WaitAsync(TimeSpan.FromSeconds(30)))
        {
            System.Diagnostics.Debug.WriteLine("[HandwerkerImperium] SaveAsync: IO-Lock Timeout - Save übersprungen");
            return;
        }
        try
        {
            var state = _gameStateService.State;
            state.LastSavedAt = DateTime.UtcNow;

            // Serialisierung auf dem UI-Thread (Thread-Safety: GameLoop modifiziert State jede Sekunde,
            // concurrent Serialisierung auf Background-Thread koennte Collection-Mutation-Exceptions ausloesen)
            string json = JsonSerializer.Serialize(state, _jsonOptions);

            // Atomic write: write to temp, backup old, rename temp to final
            await File.WriteAllTextAsync(TempFilePath, json);

            if (File.Exists(SaveFilePath))
            {
                File.Copy(SaveFilePath, BackupFilePath, overwrite: true);
            }

            File.Move(TempFilePath, SaveFilePath, overwrite: true);

            // Cloud-Save parallel (fire-and-forget, blockiert lokales Save nie)
            if (_playGamesService?.IsSignedIn == true && state.CloudSaveEnabled)
            {
                _ = Task.Run(() => _playGamesService.SaveToCloudAsync(json, $"Level {state.PlayerLevel}"));
            }
        }
        catch
        {
            // Clean up temp file on failure
            try { if (File.Exists(TempFilePath)) File.Delete(TempFilePath); } catch { /* ignore */ }
            ErrorOccurred?.Invoke("Error", "SaveErrorMessage");
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<GameState?> LoadAsync()
    {
        if (!await _ioLock.WaitAsync(TimeSpan.FromSeconds(30)))
        {
            System.Diagnostics.Debug.WriteLine("[HandwerkerImperium] LoadAsync: IO-Lock Timeout - Load übersprungen");
            return null;
        }
        try
        {
            if (!SaveExists)
            {
                // Try loading from backup if main save is missing
                if (File.Exists(BackupFilePath))
                {
                    return await LoadFromFileAsync(BackupFilePath);
                }
                return null;
            }

            var state = await LoadFromFileAsync(SaveFilePath);
            if (state != null) return state;

            // Main save is corrupted, try backup
            if (File.Exists(BackupFilePath))
            {
                return await LoadFromFileAsync(BackupFilePath);
            }

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
                // v1 -> v2 Migration
                if (state.Version < 2)
                {
                    state = MigrateFromV1(state);
                }

                // v2 -> v3 Migration: Workshop Rebirth Stars
                if (state.Version < 3)
                {
                    state.WorkshopStars ??= new Dictionary<string, int>();
                    state.Version = 3;
                }

                SanitizeState(state);
                _gameStateService.Initialize(state);
            }

            return state;
        }
        catch
        {
            ErrorOccurred?.Invoke("Error", "LoadErrorMessage");
            return null;
        }
    }

    public async Task DeleteSaveAsync()
    {
        if (!await _ioLock.WaitAsync(TimeSpan.FromSeconds(30)))
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
        catch
        {
            ErrorOccurred?.Invoke("Error", "DeleteSaveErrorMessage");
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<string> ExportSaveAsync()
    {
        var state = _gameStateService.State;
        if (state == null) return string.Empty;

        // Serialisierung außerhalb des Locks (UI-Thread, kein Race)
        string json = JsonSerializer.Serialize(state, _jsonOptions);

        // Lock nur fuer potentielle zukuenftige IO-Erweiterungen (Export-Datei etc.)
        // Aktuell gibt ExportSaveAsync() nur den JSON-String zurueck,
        // der Lock verhindert aber Race mit AutoSave falls spaeter File-Write hinzukommt
        if (!await _ioLock.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false))
        {
            System.Diagnostics.Debug.WriteLine("[HandwerkerImperium] ExportSaveAsync: IO-Lock Timeout - Export übersprungen");
            return string.Empty;
        }
        try
        {
            return json;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<bool> ImportSaveAsync(string json)
    {
        try
        {
            var state = JsonSerializer.Deserialize<GameState>(json, _jsonOptions);
            if (state == null) return false;

            SanitizeState(state);
            _gameStateService.Initialize(state);
            await SaveAsync();
            return true;
        }
        catch
        {
            ErrorOccurred?.Invoke("Error", "ImportErrorMessage");
            return false;
        }
    }

    /// <summary>
    /// Korrigiert ungültige Werte im geladenen State.
    /// Repariert statt abzulehnen - so gehen keine Savegames verloren.
    /// </summary>
    private static void SanitizeState(GameState state)
    {
        // Basis-Werte mit Ober-Caps (Exploit-Schutz gegen Save-Editing)
        if (state.PlayerLevel < LevelThresholds.MinPlayerLevel) state.PlayerLevel = LevelThresholds.MinPlayerLevel;
        if (state.PlayerLevel > LevelThresholds.MaxPlayerLevel) state.PlayerLevel = LevelThresholds.MaxPlayerLevel;
        if (state.Money < 0) state.Money = 0;
        if (state.Money > 100_000_000_000m) state.Money = 100_000_000_000m;
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
        // Prestige-Shop-Items: Nur gültige IDs behalten (Exploit-Schutz)
        var validShopIds = PrestigeShop.GetAllItems().Select(i => i.Id).ToHashSet();
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
            }
        }

        // Reputation validieren
        state.Reputation ??= new CustomerReputation();
        state.Reputation.ReputationScore = Math.Clamp(state.Reputation.ReputationScore, 0, 100);

        // Listen: null-Safety (VOR der Iteration!)
        state.Buildings ??= [];
        state.Researches ??= [];
        state.AvailableOrders ??= [];

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
        // MasterTools: Nur gültige IDs behalten (Exploit-Schutz)
        var validToolIds = MasterTool.GetAllDefinitions().Select(t => t.Id).ToHashSet();
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
        state.SeenMiniGameTutorials ??= [];

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
        if (state.Ascension.AscensionLevel < 0) state.Ascension.AscensionLevel = 0;
        if (state.Ascension.AscensionPoints < 0) state.Ascension.AscensionPoints = 0;

        if (state.TotalWorkersTrained < 0) state.TotalWorkersTrained = 0;
        if (state.TotalItemsCrafted < 0) state.TotalItemsCrafted = 0;
        if (state.TotalTournamentsWon < 0) state.TotalTournamentsWon = 0;

        // Lieferant: Abgelaufene Lieferung entfernen
        if (state.PendingDelivery?.IsExpired == true)
            state.PendingDelivery = null;
        if (state.TotalDeliveriesClaimed < 0) state.TotalDeliveriesClaimed = 0;
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
