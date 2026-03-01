using System.Text.Json;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Handles saving and loading game state to persistent storage.
/// Uses atomic writes (temp file + rename) and backup for crash safety.
/// </summary>
public class SaveGameService : ISaveGameService
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
        await _ioLock.WaitAsync();
        try
        {
            var state = _gameStateService.State;
            state.LastSavedAt = DateTime.UtcNow;

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
        await _ioLock.WaitAsync();
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
                    state = GameState.MigrateFromV1(state);
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
        await _ioLock.WaitAsync();
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
        await _ioLock.WaitAsync();
        try
        {
            var state = _gameStateService.State;
            return JsonSerializer.Serialize(state, _jsonOptions);
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
        if (state.PlayerLevel < 1) state.PlayerLevel = 1;
        if (state.PlayerLevel > 1500) state.PlayerLevel = 1500;
        if (state.Money < 0) state.Money = 0;
        if (state.Money > 100_000_000_000m) state.Money = 100_000_000_000m;
        if (state.CurrentXp < 0) state.CurrentXp = 0;
        if (state.GoldenScrews < 0) state.GoldenScrews = 0;
        if (state.GoldenScrews > 100_000) state.GoldenScrews = 100_000;
        if (state.TotalMoneyEarned < 0) state.TotalMoneyEarned = 0;
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
        // PermanentMultiplier: Minimum 1.0 (kein Prestige), Maximum 200.0
        if (state.Prestige.PermanentMultiplier < 1.0m) state.Prestige.PermanentMultiplier = 1.0m;
        if (state.Prestige.PermanentMultiplier > 200.0m) state.Prestige.PermanentMultiplier = 200.0m;
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

        // Ascension-Daten validieren
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
}
