using System.Text.Json;
using FinanzRechner.Models;

namespace FinanzRechner.Services;

/// <summary>
/// Implementierung der benutzerdefinierten Kategorien mit lokalem JSON-Speicher.
/// </summary>
public sealed class CustomCategoryService : ICustomCategoryService, IDisposable
{
    private const string CategoriesFile = "custom_categories.json";
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<CustomCategory> _categories = [];
    private bool _isInitialized;

    public CustomCategoryService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FinanzRechner");
        Directory.CreateDirectory(appDataDir);
        _filePath = Path.Combine(appDataDir, CategoriesFile);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        await _semaphore.WaitAsync();
        try
        {
            if (_isInitialized) return;
            await LoadAsync();
            _isInitialized = true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<CustomCategory> CreateCategoryAsync(CustomCategory category)
    {
        await InitializeAsync();
        ValidateCategory(category);
        await _semaphore.WaitAsync();
        try
        {
            category.Id = Guid.NewGuid().ToString();
            if (category.SortOrder == 0)
                category.SortOrder = _categories.Count;
            _categories.Add(category);
            await SaveAsync();
            return category;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<bool> UpdateCategoryAsync(CustomCategory category)
    {
        await InitializeAsync();
        ValidateCategory(category);
        await _semaphore.WaitAsync();
        try
        {
            var existing = _categories.FirstOrDefault(c => c.Id == category.Id);
            if (existing == null) return false;

            existing.Name = category.Name;
            existing.Type = category.Type;
            existing.Icon = category.Icon;
            existing.ColorHex = category.ColorHex;
            existing.SortOrder = category.SortOrder;
            existing.IsActive = category.IsActive;

            await SaveAsync();
            return true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<bool> DeleteCategoryAsync(string id)
    {
        await InitializeAsync();
        await _semaphore.WaitAsync();
        try
        {
            var category = _categories.FirstOrDefault(c => c.Id == id);
            if (category == null) return false;
            // Deaktivieren statt löschen (Transaktionen referenzieren die ID)
            category.IsActive = false;
            await SaveAsync();
            return true;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<CustomCategory?> GetCategoryAsync(string id)
    {
        await InitializeAsync();
        return _categories.FirstOrDefault(c => c.Id == id);
    }

    public async Task<IReadOnlyList<CustomCategory>> GetAllCategoriesAsync()
    {
        await InitializeAsync();
        return _categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToList();
    }

    public async Task<IReadOnlyList<CustomCategory>> GetCategoriesByTypeAsync(TransactionType type)
    {
        await InitializeAsync();
        return _categories.Where(c => c.IsActive && c.Type == type).OrderBy(c => c.SortOrder).ToList();
    }

    public async Task<string?> GetCategoryNameAsync(string id)
    {
        await InitializeAsync();
        return _categories.FirstOrDefault(c => c.Id == id)?.Name;
    }

    public async Task<string> ExportToJsonAsync()
    {
        await InitializeAsync();
        return JsonSerializer.Serialize(_categories, _jsonOptions);
    }

    public async Task<int> ImportFromJsonAsync(string json, bool merge = false)
    {
        await InitializeAsync();
        var imported = JsonSerializer.Deserialize<List<CustomCategory>>(json) ?? [];
        await _semaphore.WaitAsync();
        try
        {
            if (!merge) _categories.Clear();
            var existingIds = _categories.Select(c => c.Id).ToHashSet();
            var count = 0;
            foreach (var category in imported)
            {
                if (merge && existingIds.Contains(category.Id)) continue;
                _categories.Add(category);
                count++;
            }
            await SaveAsync();
            return count;
        }
        finally { _semaphore.Release(); }
    }

    private async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath);
                _categories = JsonSerializer.Deserialize<List<CustomCategory>>(json) ?? [];
            }
        }
        catch { _categories = []; }
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_categories, _jsonOptions);
        var tempPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static void ValidateCategory(CustomCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (string.IsNullOrWhiteSpace(category.Name))
            throw new ArgumentException("Kategoriename darf nicht leer sein.");
        if (category.Name.Length > 30)
            throw new ArgumentException("Kategoriename darf maximal 30 Zeichen lang sein.");
    }

    public void Dispose() => _semaphore.Dispose();
}
