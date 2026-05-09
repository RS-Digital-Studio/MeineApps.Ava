using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Einladungs-Subsystem fuer Gilden: 6-stellige Invite-Codes,
/// Spieler-Browser fuer "verfuegbare Spieler", Direkt-Einladungs-Inbox.
///
/// Architektur-Hinweis: Beitreten/Annehmen-Operationen delegieren an <see cref="IGuildService.JoinGuildAsync"/>,
/// damit nur ein Ort den globalen Beitritts-Lock und die Integritaets-Checks haelt.
/// Der InviteService haelt einen eigenen Lock fuer Code-Generierung
/// (verhindert konkurrente Code-Kollisionsschleifen und doppelte Codes pro Gilde).
/// </summary>
public interface IGuildInviteService
{
    // ── Invite-Codes (6-stellig, bidirektionales Mapping) ──

    /// <summary>
    /// Gibt den Einladungs-Code der aktuellen Gilde zurueck.
    /// Erstellt einen 6-stelligen Code bei Bedarf und speichert ihn in Firebase.
    /// Pfade: /guild_invite_codes/{guildId} → Code, /invite_code_to_guild/{code} → GuildId
    /// </summary>
    Task<string?> GetOrCreateInviteCodeAsync();

    /// <summary>
    /// Tritt einer Gilde per 6-stelligem Einladungs-Code bei.
    /// Sucht den Code im /invite_code_to_guild/-Pfad und ruft <see cref="IGuildService.JoinGuildAsync"/> auf.
    /// </summary>
    Task<bool> JoinByInviteCodeAsync(string code);

    // ── "Verfuegbare Spieler"-Browser (gildelose Spieler) ──

    /// <summary>
    /// Laedt verfuegbare Spieler ohne Gilde (max 50, nach Aktivitaet sortiert).
    /// Pfad: /available_players/{uid} → { name, level, lastActive }
    /// </summary>
    Task<List<AvailablePlayerInfo>> BrowseAvailablePlayersAsync();

    /// <summary>
    /// Registriert den Spieler als verfuegbar fuer Einladungen (wenn gildelos).
    /// Wird automatisch aufgerufen wenn der Spieler keine Gilde hat.
    /// </summary>
    Task RegisterAsAvailableAsync();

    /// <summary>
    /// Entfernt die Verfuegbarkeits-Registrierung (wird bei Gilden-Beitritt aufgerufen).
    /// </summary>
    Task UnregisterAvailableAsync();

    // ── Einladungs-Inbox (Direkt-Einladungen pro Spieler) ──

    /// <summary>Sendet eine direkte Einladung an einen Spieler. Max 10 Einladungen pro Empfaenger.</summary>
    Task<bool> SendInviteAsync(string targetUid);

    /// <summary>Laedt empfangene Einladungen fuer den aktuellen Spieler (sortiert: neueste zuerst).</summary>
    Task<List<(string guildId, GuildInvitation invite)>> GetReceivedInvitesAsync();

    /// <summary>Nimmt eine Einladung an (Beitritt zur Gilde, loescht alle anderen Einladungen).</summary>
    Task<bool> AcceptInviteAsync(string guildId);

    /// <summary>Lehnt eine einzelne Einladung ab.</summary>
    Task<bool> DeclineInviteAsync(string guildId);
}
