namespace HandwerkerRechner.Services;

/// <summary>
/// Verwaltet Favoriten-Rechner für den Schnellzugriff im HomeTab.
/// Persistiert über IPreferencesService.
/// </summary>
public interface IFavoritesService
{
    /// <summary>Aktuelle Favoriten (Rechner-Keys, z.B. "TileCalculatorPage")</summary>
    IReadOnlyList<string> Favorites { get; }

    /// <summary>Prüft ob ein Rechner als Favorit markiert ist</summary>
    bool IsFavorite(string calculatorKey);

    /// <summary>Favorit hinzufügen oder entfernen (Toggle)</summary>
    void Toggle(string calculatorKey);

    /// <summary>Wird ausgelöst wenn sich die Favoriten-Liste ändert</summary>
    event EventHandler? FavoritesChanged;
}
