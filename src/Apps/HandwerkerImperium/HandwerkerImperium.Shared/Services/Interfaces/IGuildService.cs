using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Service für Multiplayer-Gilden via Firebase Realtime Database.
/// Echte Spieler können Gilden erstellen, beitreten und gemeinsam an Wochenzielen arbeiten.
/// </summary>
public interface IGuildService
{
    /// <summary>Feuert wenn sich der Gilden-Zustand ändert.</summary>
    event Action? GuildUpdated;

    /// <summary>Spielername für Firebase (gesetzt beim ersten Gilden-Besuch).</summary>
    string? PlayerName { get; }

    /// <summary>Ob der Service online erreichbar ist.</summary>
    bool IsOnline { get; }

    /// <summary>Initialisiert Auth und prüft ob Spieler in einer Gilde ist.</summary>
    Task InitializeAsync();

    /// <summary>Lädt verfügbare Gilden zum Beitreten.</summary>
    Task<List<GuildListItem>> BrowseGuildsAsync();

    /// <summary>Erstellt eine neue Gilde.</summary>
    Task<bool> CreateGuildAsync(string name, string icon, string color);

    /// <summary>Tritt einer bestehenden Gilde bei.</summary>
    Task<bool> JoinGuildAsync(string guildId);

    /// <summary>Verlässt die aktuelle Gilde.</summary>
    Task<bool> LeaveGuildAsync();

    /// <summary>Leistet einen Geldbeitrag zum Wochenziel.</summary>
    Task<bool> ContributeAsync(decimal amount);

    /// <summary>Lädt aktuelle Gilden-Details (mit Mitgliedern) von Firebase.</summary>
    Task<GuildDetailData?> RefreshGuildDetailsAsync();

    /// <summary>Gibt den aktuellen IncomeBonus aus dem lokalen Cache (kein Firebase-Request).</summary>
    decimal GetIncomeBonus();

    /// <summary>Setzt den Spielernamen (wird in Preferences gespeichert).</summary>
    void SetPlayerName(string name);

    // ── Gilden-Forschung ──

    /// <summary>Lädt alle Gilden-Forschungen mit aktuellem Fortschritt von Firebase.</summary>
    Task<List<GuildResearchDisplay>> GetGuildResearchAsync();

    /// <summary>Leistet einen Geldbeitrag zu einer bestimmten Forschung.</summary>
    Task<bool> ContributeToResearchAsync(string researchId, long amount);

    /// <summary>Gibt die gecachten Forschungs-Effekte zurück (kein Firebase-Request).</summary>
    GuildResearchEffects GetResearchEffects();

    /// <summary>Berechnet max. Gilden-Mitglieder (20 + Forschungs-Boni).</summary>
    int GetMaxMembers();
}
