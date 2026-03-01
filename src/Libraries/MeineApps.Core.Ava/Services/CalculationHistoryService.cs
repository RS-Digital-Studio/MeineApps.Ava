using System.Text.Json;

namespace MeineApps.Core.Ava.Services;

/// <summary>
/// JSON-file-based calculation history service (thread-safe).
/// Stores the last 30 calculations per calculator type.
/// </summary>
public class CalculationHistoryService : ICalculationHistoryService
{
    private const string HistoryFolder = "calculation_history";
    private const int MaxItemsPerCalculator = 30;

    private readonly string _historyPath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public CalculationHistoryService()
    {
        _historyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MeineApps", HistoryFolder);
        Directory.CreateDirectory(_historyPath);
    }

    public async Task AddCalculationAsync(string calculatorId, string title, Dictionary<string, object> data)
    {
        await _semaphore.WaitAsync();
        try
        {
            var item = new CalculationHistoryItem
            {
                Id = Guid.NewGuid().ToString(),
                CalculatorId = calculatorId,
                Title = title,
                Data = data,
                CreatedAt = DateTime.UtcNow
            };

            var history = await GetHistoryInternalAsync(calculatorId, 100);
            history.Insert(0, item);

            if (history.Count > MaxItemsPerCalculator)
                history = history.Take(MaxItemsPerCalculator).ToList();

            await SaveHistoryInternalAsync(calculatorId, history);
        }
        catch (Exception ex)
        {
            // Fehler still ignorieren
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<CalculationHistoryItem>> GetHistoryAsync(string calculatorId, int maxItems = 10)
    {
        await _semaphore.WaitAsync();
        try
        {
            return await GetHistoryInternalAsync(calculatorId, maxItems);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<CalculationHistoryItem?> GetCalculationAsync(string id)
    {
        await _semaphore.WaitAsync();
        try
        {
            var files = Directory.GetFiles(_historyPath, "*.json");
            foreach (var file in files)
            {
                var json = await File.ReadAllTextAsync(file);
                var history = JsonSerializer.Deserialize<List<CalculationHistoryItem>>(json);
                var item = history?.FirstOrDefault(h => h.Id == id);
                if (item != null) return item;
            }
            return null;
        }
        catch (Exception ex)
        {
            // Fehler still ignorieren
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteCalculationAsync(string id)
    {
        await _semaphore.WaitAsync();
        try
        {
            var files = Directory.GetFiles(_historyPath, "*.json");
            foreach (var file in files)
            {
                var json = await File.ReadAllTextAsync(file);
                var history = JsonSerializer.Deserialize<List<CalculationHistoryItem>>(json);
                if (history == null) continue;

                var item = history.FirstOrDefault(h => h.Id == id);
                if (item != null)
                {
                    history.Remove(item);
                    var updatedJson = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(file, updatedJson);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            // Fehler still ignorieren
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearHistoryAsync(string calculatorId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var filePath = GetHistoryFilePath(calculatorId);
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch (Exception ex)
        {
            // Fehler still ignorieren
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task CleanupOldEntriesAsync(int olderThanDays = 90)
    {
        await _semaphore.WaitAsync();
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            var files = Directory.GetFiles(_historyPath, "*.json");

            foreach (var file in files)
            {
                var json = await File.ReadAllTextAsync(file);
                var history = JsonSerializer.Deserialize<List<CalculationHistoryItem>>(json);
                if (history == null) continue;

                var filtered = history.Where(h => h.CreatedAt > cutoffDate).ToList();
                if (filtered.Count != history.Count)
                {
                    var updatedJson = JsonSerializer.Serialize(filtered, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(file, updatedJson);
                }
            }
        }
        catch (Exception ex)
        {
            // Fehler still ignorieren
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<CalculationHistoryItem>> GetAllHistoryAsync(int maxItemsPerCalculator = 10)
    {
        await _semaphore.WaitAsync();
        try
        {
            var allItems = new List<CalculationHistoryItem>();
            if (!Directory.Exists(_historyPath)) return allItems;

            var files = Directory.GetFiles(_historyPath, "*.json");
            foreach (var file in files)
            {
                var json = await File.ReadAllTextAsync(file);
                var history = JsonSerializer.Deserialize<List<CalculationHistoryItem>>(json);
                if (history != null)
                    allItems.AddRange(history.Take(maxItemsPerCalculator));
            }
            return allItems.OrderByDescending(h => h.CreatedAt).ToList();
        }
        catch
        {
            return [];
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Interner Read - MUSS innerhalb des Semaphore-Locks aufgerufen werden
    /// </summary>
    private async Task<List<CalculationHistoryItem>> GetHistoryInternalAsync(string calculatorId, int maxItems)
    {
        try
        {
            var filePath = GetHistoryFilePath(calculatorId);
            if (!File.Exists(filePath))
                return [];

            var json = await File.ReadAllTextAsync(filePath);
            var history = JsonSerializer.Deserialize<List<CalculationHistoryItem>>(json) ?? [];
            return history.Take(maxItems).ToList();
        }
        catch (Exception ex)
        {
            // Fehler still ignorieren
            return [];
        }
    }

    /// <summary>
    /// Interner Write - MUSS innerhalb des Semaphore-Locks aufgerufen werden
    /// </summary>
    private async Task SaveHistoryInternalAsync(string calculatorId, List<CalculationHistoryItem> history)
    {
        var filePath = GetHistoryFilePath(calculatorId);
        var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    private string GetHistoryFilePath(string calculatorId)
        => Path.Combine(_historyPath, $"{calculatorId}.json");
}
