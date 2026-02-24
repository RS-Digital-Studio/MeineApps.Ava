namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Service für Firebase-basiertes Geschenke-System.
/// Täglich 1 Geschenk an einen echten Freund senden (3 Goldschrauben + Bonus).
/// </summary>
public interface IGiftService
{
    /// <summary>Geschenk an einen echten Freund senden (1x/Tag).</summary>
    Task<bool> SendGiftAsync(string friendUid, string friendName);

    /// <summary>Eingehende Geschenke laden.</summary>
    Task<List<GiftDisplay>> GetPendingGiftsAsync();

    /// <summary>Geschenk einlösen (Goldschrauben gutschreiben).</summary>
    Task<bool> ClaimGiftAsync(string giftId);

    /// <summary>Alle Geschenke auf einmal einlösen.</summary>
    Task<int> ClaimAllGiftsAsync();

    /// <summary>Ob heute bereits ein Geschenk gesendet wurde.</summary>
    bool HasSentGiftToday { get; }
}

/// <summary>
/// Anzeige-Daten für ein empfangenes Geschenk.
/// </summary>
public class GiftDisplay
{
    public string GiftId { get; set; } = "";
    public string FromName { get; set; } = "";
    public string Type { get; set; } = "golden_screws";
    public int Amount { get; set; }
    public string SentAt { get; set; } = "";
    public bool Claimed { get; set; }
}
