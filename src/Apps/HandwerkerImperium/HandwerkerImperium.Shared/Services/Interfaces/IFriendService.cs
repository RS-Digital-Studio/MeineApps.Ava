using HandwerkerImperium.Models.Firebase;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Service für Freunde: simulierte Freunde (offline) + echte Firebase-Freunde (online).
/// Max. 50 echte Freunde. Tägliche Geschenke, Freundschafts-Level.
/// </summary>
public interface IFriendService
{
    /// <summary>Feuert wenn sich der Freunde-Zustand ändert.</summary>
    event Action? FriendsUpdated;

    /// <summary>Initialisiert Freundes-Liste (simulierte Freunde als Basis).</summary>
    void Initialize();

    /// <summary>Generiert tägliche Geschenke von simulierten Freunden.</summary>
    void GenerateDailyGifts();

    /// <summary>Nimmt ein Geschenk von einem simulierten Freund an.</summary>
    void ClaimGift(string friendId);

    /// <summary>Sendet ein Geschenk an einen simulierten Freund (kostet 1 Goldschraube).</summary>
    void SendGift(string friendId);

    // --- Firebase-basierte echte Freunde ---

    /// <summary>Freundschaftsanfrage an einen Spieler senden (über UID aus Gildenmitgliederliste).</summary>
    Task SendFriendRequestAsync(string targetUid, string targetName);

    /// <summary>Eingehende Freundschaftsanfragen laden.</summary>
    Task<List<FriendRequestDisplay>> GetPendingRequestsAsync();

    /// <summary>Freundschaftsanfrage annehmen (beidseitige Freundschaft in Firebase).</summary>
    Task AcceptRequestAsync(string fromUid);

    /// <summary>Freundschaftsanfrage ablehnen.</summary>
    Task DeclineRequestAsync(string fromUid);

    /// <summary>Echte Freunde-Liste aus Firebase laden.</summary>
    Task<List<FriendDisplay>> GetRealFriendsAsync();

    /// <summary>Freund entfernen (beidseitig).</summary>
    Task RemoveFriendAsync(string friendUid);
}

/// <summary>
/// Anzeige-Daten für eine Freundschaftsanfrage.
/// </summary>
public class FriendRequestDisplay
{
    public string Uid { get; set; } = "";
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public string SentAt { get; set; } = "";
}

/// <summary>
/// Anzeige-Daten für einen echten Freund.
/// </summary>
public class FriendDisplay
{
    public string Uid { get; set; } = "";
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public string AddedAt { get; set; } = "";
}
