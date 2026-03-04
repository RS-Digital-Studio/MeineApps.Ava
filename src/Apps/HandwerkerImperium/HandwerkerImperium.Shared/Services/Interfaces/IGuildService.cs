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

    /// <summary>Berechnet max. Gilden-Mitglieder (20 + Forschungs-Boni aus GuildMembership-Cache).</summary>
    int GetMaxMembers();

    // ── Einladungs-System ──

    /// <summary>Gibt den Einladungs-Code der aktuellen Gilde zurück (erstellt einen bei Bedarf).</summary>
    Task<string?> GetOrCreateInviteCodeAsync();

    /// <summary>Tritt einer Gilde per Einladungs-Code bei.</summary>
    Task<bool> JoinByInviteCodeAsync(string code);

    /// <summary>Lädt verfügbare Spieler ohne Gilde (max 50, nach Aktivität sortiert).</summary>
    Task<List<AvailablePlayerInfo>> BrowseAvailablePlayersAsync();

    /// <summary>Registriert den Spieler als verfügbar (wenn gildelos).</summary>
    Task RegisterAsAvailableAsync();

    /// <summary>Entfernt die Verfügbarkeits-Registrierung (wenn Gilde beigetreten).</summary>
    Task UnregisterAvailableAsync();

    // ── Einladungs-Inbox ──

    /// <summary>Sendet eine direkte Einladung an einen Spieler.</summary>
    Task<bool> SendInviteAsync(string targetUid);

    /// <summary>Lädt empfangene Einladungen für den aktuellen Spieler.</summary>
    Task<List<(string guildId, GuildInvitation invite)>> GetReceivedInvitesAsync();

    /// <summary>Nimmt eine Einladung an (beitritt Gilde, löscht alle anderen Einladungen).</summary>
    Task<bool> AcceptInviteAsync(string guildId);

    /// <summary>Lehnt eine einzelne Einladung ab.</summary>
    Task<bool> DeclineInviteAsync(string guildId);

    // ── Rollen-Management (3 Stufen: Leader/Officer/Member) ──

    /// <summary>Befördert ein Mitglied zum Offizier (nur Leader/Officer).</summary>
    Task<bool> PromoteToOfficerAsync(string targetUid);

    /// <summary>Degradiert einen Offizier zum normalen Mitglied (nur Leader).</summary>
    Task<bool> DemoteToMemberAsync(string targetUid);

    /// <summary>Entfernt ein Mitglied aus der Gilde (nur Leader/Officer).</summary>
    Task<bool> KickMemberAsync(string targetUid);

    /// <summary>Überträgt die Gildenleitung an ein anderes Mitglied (nur Leader).</summary>
    Task<bool> TransferLeadershipAsync(string targetUid);

    /// <summary>Aktualisiert den lastActive-Zeitstempel des aktuellen Spielers.</summary>
    Task UpdateLastActiveAsync();
}
