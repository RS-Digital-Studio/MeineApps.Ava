using HandwerkerImperium.Models.Firebase;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Worker-Markt-Auktionen (v2.1.0, Sprint 3 Big Bet).
///
/// Phase-1-Status: Foundation — Service-Skelett + Firebase-Pfad. Echtzeit-Bidding,
/// 1s-Polling, NPC-Bots, 5min-Spawn-Cron, Refund-Logik sind „Phase B".
/// </summary>
public interface IWorkerAuctionService
{
    /// <summary>Firebase-Pfad-Praefix fuer Auktionen einer Gilde.</summary>
    static string GetFirebasePath(string guildId, string auctionId)
        => $"guilds/{guildId}/auctions/{auctionId}";

    /// <summary>Aktuell laufende Auktion (eine pro Gilde gleichzeitig). Null = keine.</summary>
    WorkerAuctionState? CurrentAuction { get; }

    /// <summary>
    /// Stellt ein Gebot ab. Mindest-Erhoehung: 10% des Hoechstgebots.
    /// 1s-Cooldown gegen Spam-Bidding. Spieler muss das Geld haben (locked bis Auktions-Ende).
    /// </summary>
    Task<bool> PlaceBidAsync(decimal amount);

    /// <summary>
    /// Liefert die laufende Auktion fuer Polling-Updates.
    /// </summary>
    Task<WorkerAuctionState?> RefreshAuctionAsync();

    /// <summary>
    /// v2.1.0: Spawnt eine neue Auktion (Master-Client-Pattern: nur Spieler mit kleinster
    /// PlayerId in der Gilde fuehrt das aus). Wird vom GuildTickService alle 5min gerufen.
    /// </summary>
    Task<bool> SpawnAuctionIfMasterAsync();

    /// <summary>
    /// v2.1.0: NPC-Bots bieten zufaellig gegen den Spieler. Wird vom Master-Client periodisch
    /// gerufen (typischerweise im Polling-Tick). In kleinen Gilden + Solo wird mindestens
    /// 1 NPC-Bot aktiv, in groesseren weniger.
    /// </summary>
    Task RunNpcBotTickAsync();

    /// <summary>Feuert bei Auktions-Updates (Polling oder Push).</summary>
    event Action<WorkerAuctionState>? AuctionUpdated;

    /// <summary>Feuert wenn die Auktion endet — mit Hoechstbieter-Info.</summary>
    event Action<WorkerAuctionState>? AuctionSettled;
}
