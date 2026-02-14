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
    private readonly SemaphoreSlim _ioLock = new(1, 1);
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

    public SaveGameService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
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
        }
        catch
        {
            // Clean up temp file on failure
            try { if (File.Exists(TempFilePath)) File.Delete(TempFilePath); } catch { /* ignore */ }
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
            // Ignore delete errors
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public Task<string> ExportSaveAsync()
    {
        var state = _gameStateService.State;
        return Task.FromResult(JsonSerializer.Serialize(state, _jsonOptions));
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
            return false;
        }
    }

    /// <summary>
    /// Korrigiert ungültige Werte im geladenen State.
    /// Repariert statt abzulehnen - so gehen keine Savegames verloren.
    /// </summary>
    private static void SanitizeState(GameState state)
    {
        // Basis-Werte
        if (state.PlayerLevel < 1) state.PlayerLevel = 1;
        if (state.Money < 0) state.Money = 0;
        if (state.CurrentXp < 0) state.CurrentXp = 0;
        if (state.GoldenScrews < 0) state.GoldenScrews = 0;
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
        // PermanentMultiplier: Minimum 1.0 (kein Prestige), Maximum 20.0
        if (state.Prestige.PermanentMultiplier < 1.0m) state.Prestige.PermanentMultiplier = 1.0m;
        if (state.Prestige.PermanentMultiplier > 20.0m) state.Prestige.PermanentMultiplier = 20.0m;
        state.Prestige.PurchasedShopItems ??= [];

        // Daily Reward Streak
        if (state.DailyRewardStreak < 0) state.DailyRewardStreak = 0;

        // Worker-Daten validieren
        foreach (var ws in state.Workshops)
        {
            foreach (var worker in ws.Workers)
            {
                worker.Mood = Math.Clamp(worker.Mood, 0m, 100m);
                worker.Fatigue = Math.Clamp(worker.Fatigue, 0m, 100m);
                if (worker.ExperienceLevel < 0) worker.ExperienceLevel = 0;
                if (worker.ExperienceXp < 0) worker.ExperienceXp = 0;
                // AssignedWorkshop muss zum Workshop passen, in dem der Worker steckt
                if (worker.AssignedWorkshop != ws.Type)
                    worker.AssignedWorkshop = ws.Type;
            }
        }

        // Reputation validieren
        state.Reputation ??= new CustomerReputation();
        state.Reputation.ReputationScore = Math.Clamp(state.Reputation.ReputationScore, 0, 100);

        // Building-Levels validieren
        foreach (var building in state.Buildings)
        {
            if (building.Level < 0) building.Level = 0;
            if (building.Level > building.Type.GetMaxLevel())
                building.Level = building.Type.GetMaxLevel();
        }

        // Listen: null-Safety
        state.AvailableOrders ??= [];
        state.Buildings ??= [];
        state.Researches ??= [];
        state.UnlockedAchievements ??= [];
        state.QuickJobs ??= [];
        state.EventHistory ??= [];
        state.DailyChallengeState ??= new DailyChallengeState();
        state.CollectedMasterTools ??= [];
        state.Tools ??= [];

        // Lieferant: Abgelaufene Lieferung entfernen
        if (state.PendingDelivery?.IsExpired == true)
            state.PendingDelivery = null;
        if (state.TotalDeliveriesClaimed < 0) state.TotalDeliveriesClaimed = 0;
    }
}
