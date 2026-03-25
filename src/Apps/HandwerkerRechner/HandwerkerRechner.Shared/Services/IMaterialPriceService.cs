using HandwerkerRechner.Models;

namespace HandwerkerRechner.Services;

/// <summary>
/// Verwaltet Materialpreise mit regionalen Durchschnittspreisen und benutzerdefinierten Überschreibungen.
/// Preise werden in allen Rechnern für Kostenschätzungen verwendet.
/// </summary>
public interface IMaterialPriceService
{
    /// <summary>Einzelnen Preis nach Key abrufen</summary>
    MaterialPrice? GetPrice(string key);

    /// <summary>Alle Preise abrufen</summary>
    List<MaterialPrice> GetAllPrices();

    /// <summary>Preise nach Kategorie filtern</summary>
    List<MaterialPrice> GetPricesByCategory(string category);

    /// <summary>Benutzerdefinierten Preis setzen</summary>
    Task SetCustomPriceAsync(string key, double price);

    /// <summary>Einzelnen Preis auf Standard zurücksetzen</summary>
    Task ResetToDefaultAsync(string key);

    /// <summary>Alle Preise auf Standard zurücksetzen</summary>
    Task ResetAllToDefaultAsync();
}
