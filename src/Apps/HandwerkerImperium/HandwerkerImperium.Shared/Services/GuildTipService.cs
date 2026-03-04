using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// Einfacher Preferences-basierter Service für 8 Gilden-First-Time-Tipps.
/// Beim ersten Benutzen eines Gilden-Features wird ein kurzer Tipp angezeigt.
/// Kein Firebase nötig - rein lokal.
/// 8 Kontexte: joined, research, war, boss, hall, officer, season_end, chat
/// </summary>
public sealed class GuildTipService : IGuildTipService
{
    private readonly IPreferencesService _preferences;
    private readonly ILocalizationService _localization;

    private const string PrefPrefix = "guild_tip_seen_";

    public GuildTipService(
        IPreferencesService preferences,
        ILocalizationService localization)
    {
        _preferences = preferences;
        _localization = localization;
    }

    /// <summary>
    /// Gibt den lokalisierten Tipp-Text für einen Kontext zurück.
    /// Gibt null zurück wenn der Tipp bereits gesehen wurde.
    /// RESX-Key: GuildTip_{context} (z.B. GuildTip_joined, GuildTip_research)
    /// </summary>
    public string? GetTipForContext(string context)
    {
        if (string.IsNullOrEmpty(context)) return null;
        if (_preferences.Get(PrefPrefix + context, false)) return null;

        return _localization.GetString($"GuildTip_{context}");
    }

    /// <summary>
    /// Markiert einen Tipp als gesehen. Wird beim Anzeigen/Dismissing aufgerufen.
    /// </summary>
    public void MarkTipSeen(string context)
    {
        if (string.IsNullOrEmpty(context)) return;
        _preferences.Set(PrefPrefix + context, true);
    }

    /// <summary>
    /// Prüft ob ein Tipp für den Kontext noch nicht gesehen wurde.
    /// </summary>
    public bool HasUnseenTip(string context)
    {
        if (string.IsNullOrEmpty(context)) return false;
        return !_preferences.Get(PrefPrefix + context, false);
    }
}
