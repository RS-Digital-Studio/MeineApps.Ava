using System.Text.Json;
using HandwerkerImperium.Models;
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

            _gameStateService.Initialize(state);
            await SaveAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
