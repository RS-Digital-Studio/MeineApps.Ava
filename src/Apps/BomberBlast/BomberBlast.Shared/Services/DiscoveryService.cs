using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Persistiert entdeckte PowerUps/Mechaniken via IPreferencesService.
/// Speichert als kommaseparierter String.
/// </summary>
public sealed class DiscoveryService : IDiscoveryService
{
    private const string PREFS_KEY = "DiscoveredItems";
    private readonly IPreferencesService _preferences;
    private readonly HashSet<string> _discovered;

    public DiscoveryService(IPreferencesService preferences)
    {
        _preferences = preferences;

        // Gespeicherte Entdeckungen laden
        var saved = _preferences.Get(PREFS_KEY, "");
        _discovered = string.IsNullOrEmpty(saved)
            ? new HashSet<string>()
            : new HashSet<string>(saved.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    public bool IsDiscovered(string id) => _discovered.Contains(id);

    public void MarkDiscovered(string id)
    {
        if (_discovered.Add(id))
        {
            Save();
        }
    }

    public string? GetDiscoveryTitleKey(string id)
    {
        if (_discovered.Contains(id))
            return null;

        _discovered.Add(id);
        Save();

        // ID-Format: "powerup_kick" → "DiscoverKick", "mechanic_ice" → "DiscoverIce"
        return GetKeyFromId(id);
    }

    public string? GetDiscoveryDescKey(string id)
    {
        // Immer den Desc-Key zurückgeben (für Discovery-Overlay Beschreibung)
        return GetKeyFromId(id, "Desc");
    }

    /// <summary>
    /// Generiert RESX-Key aus Discovery-ID.
    /// "powerup_kick" → "DiscoverKick" (suffix=""), "DiscoverKickDesc" (suffix="Desc")
    /// </summary>
    private static string GetKeyFromId(string id, string suffix = "")
    {
        var parts = id.Split('_');
        if (parts.Length < 2) return "Discover" + id + suffix;
        var name = char.ToUpper(parts[1][0]) + parts[1][1..];
        return "Discover" + name + suffix;
    }

    private void Save()
    {
        _preferences.Set(PREFS_KEY, string.Join(",", _discovered));
    }
}
