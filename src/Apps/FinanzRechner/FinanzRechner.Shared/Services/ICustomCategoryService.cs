using FinanzRechner.Models;

namespace FinanzRechner.Services;

/// <summary>
/// Service für benutzerdefinierte Kategorien.
/// </summary>
public interface ICustomCategoryService
{
    Task InitializeAsync();

    Task<CustomCategory> CreateCategoryAsync(CustomCategory category);
    Task<bool> UpdateCategoryAsync(CustomCategory category);
    Task<bool> DeleteCategoryAsync(string id);
    Task<CustomCategory?> GetCategoryAsync(string id);
    Task<IReadOnlyList<CustomCategory>> GetAllCategoriesAsync();

    /// <summary>Alle Kategorien eines Typs (Ausgaben oder Einnahmen).</summary>
    Task<IReadOnlyList<CustomCategory>> GetCategoriesByTypeAsync(TransactionType type);

    /// <summary>Name einer Custom-Kategorie anhand der ID holen.</summary>
    Task<string?> GetCategoryNameAsync(string id);

    // Daten-Export/Import
    Task<string> ExportToJsonAsync();
    Task<int> ImportFromJsonAsync(string json, bool merge = false);
}
