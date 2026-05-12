using HandwerkerImperium.Models.Firebase;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// V7 (Phase 4 Ressourcen-Plan, Plan Section 3.9): Gilden-Mega-Projekte.
/// Mitglieder spenden Materialien aus ihrem Lager, das Mega-Projekt schreitet voran,
/// bei Abschluss erhalten alle Mitglieder einen permanenten Gilden-Bonus.
///
/// Firebase-Pfad: <c>guilds/{guildId}/megaProjects/active</c> (single-active per Gilde).
/// HMAC-signiert; PATCH-atomar; ClaimedGuildProjectIds gegen Doppel-Belohnung.
/// </summary>
public interface IGuildMegaProjectService
{
    /// <summary>Event: Mega-Projekt wurde aktualisiert (UI-Refresh-Trigger).</summary>
    event Action<GuildMegaProject>? ProjectUpdated;

    /// <summary>Event: Mega-Projekt wurde abgeschlossen (Celebration-Trigger).</summary>
    event Action<GuildMegaProject>? ProjectCompleted;

    /// <summary>Aktuelles aktives Mega-Projekt der Gilde (null wenn keines aktiv).</summary>
    Task<GuildMegaProject?> GetActiveProjectAsync();

    /// <summary>
    /// Startet ein neues Mega-Projekt (nur Co-Leader oder Leader-Rolle).
    /// Returnt false wenn bereits eines aktiv ist oder Rolle unzureichend.
    /// </summary>
    Task<bool> StartProjectAsync(GuildMegaProjectType type);

    /// <summary>
    /// Spendet Material aus dem Lager des aktuellen Spielers.
    /// Atomar via Firebase-PATCH + GameState-Update.
    /// Returnt true bei Erfolg, false wenn nicht genug Material oder kein aktives Projekt.
    /// </summary>
    Task<bool> DonateAsync(string productId, int count);

    /// <summary>
    /// Prueft serverseitig ob das Projekt fertig ist und claimed den Bonus
    /// fuer den aktuellen Spieler (idempotent via ClaimedGuildProjectIds).
    /// </summary>
    Task<bool> TryClaimRewardAsync();
}
