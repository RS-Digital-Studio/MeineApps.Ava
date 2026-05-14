using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Firebase;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Co-op-Auftraege zwischen zwei Gildenmitgliedern (v2.1.0, Sprint 3 Big Bet).
///
/// Phase-1-Status: Foundation — Service-Skelett + Firebase-Pfad-Struktur. Echtzeit-Sync,
/// Polling-Loop, HMAC-Signierung und UI sind „Phase B" in einer dedizierten Branch.
/// </summary>
public interface IGuildCoopOrderService
{
    /// <summary>Firebase-Pfad-Praefix fuer Co-op-Auftraege einer Gilde.</summary>
    static string GetFirebasePath(string guildId, string orderId)
        => $"guilds/{guildId}/coopOrders/{orderId}";

    /// <summary>
    /// Erstellt einen neuen Co-op-Auftrag und ladet einen Mitspieler ein.
    /// Status startet auf Pending mit 5min Annahme-Frist.
    /// FB-H04: <paramref name="miniGameType"/> ist verpflichtend — der Aufrufer waehlt das
    /// Mini-Game, sonst waere jeder Co-op-Auftrag immer Sawing.
    /// </summary>
    Task<CoopOrderState?> CreateInviteAsync(string invitedPlayerId, MiniGameType miniGameType);

    /// <summary>
    /// Eingeladener Spieler nimmt den Auftrag an. Status wechselt auf Active.
    /// </summary>
    Task<bool> AcceptAsync(string orderId);

    /// <summary>
    /// Eingeladener Spieler lehnt ab oder Timeout — Status wechselt auf Expired.
    /// </summary>
    Task<bool> DeclineAsync(string orderId);

    /// <summary>
    /// Spieler liefert seinen MiniGame-Score ab. Wenn beide Scores vorhanden sind,
    /// wechselt der Status auf Completed und der Reward wird ausgeschuettet.
    /// </summary>
    Task<bool> SubmitScoreAsync(string orderId, int score, bool isPlayer1);

    /// <summary>
    /// Liest den aktuellen State (fuer Polling).
    /// </summary>
    Task<CoopOrderState?> GetStateAsync(string orderId);

    /// <summary>
    /// Liefert offene Co-op-Auftraege fuer den aktuellen Spieler (eingeladen oder erstellt).
    /// </summary>
    Task<IReadOnlyList<CoopOrderState>> GetOpenForPlayerAsync();

    /// <summary>
    /// Feuert wenn sich ein Co-op-Auftrag-Status aendert (Polling-Update oder Push).
    /// </summary>
    event Action<CoopOrderState>? CoopOrderUpdated;
}
