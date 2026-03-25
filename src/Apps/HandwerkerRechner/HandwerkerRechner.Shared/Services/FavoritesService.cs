using MeineApps.Core.Ava.Services;

namespace HandwerkerRechner.Services;

/// <summary>
/// Verwaltet Favoriten-Rechner. Persistiert als kommagetrennte Liste in Preferences.
/// </summary>
public sealed class FavoritesService : IFavoritesService
{
    private const string PreferencesKey = "FavoriteCalculators";
    private readonly IPreferencesService _preferences;
    private readonly List<string> _favorites = [];

    public event EventHandler? FavoritesChanged;

    public IReadOnlyList<string> Favorites => _favorites.AsReadOnly();

    public FavoritesService(IPreferencesService preferences)
    {
        _preferences = preferences;
        Load();
    }

    public bool IsFavorite(string calculatorKey) => _favorites.Contains(calculatorKey);

    public void Toggle(string calculatorKey)
    {
        if (_favorites.Contains(calculatorKey))
            _favorites.Remove(calculatorKey);
        else
            _favorites.Add(calculatorKey);

        Save();
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Load()
    {
        var stored = _preferences.Get(PreferencesKey, string.Empty);
        if (string.IsNullOrEmpty(stored)) return;

        _favorites.Clear();
        foreach (var key in stored.Split(',', StringSplitOptions.RemoveEmptyEntries))
            _favorites.Add(key.Trim());
    }

    private void Save()
    {
        var value = string.Join(",", _favorites);
        _preferences.Set(PreferencesKey, value);
    }
}
