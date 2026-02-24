using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models.Firebase;

/// <summary>
/// Ein Geschenk in Firebase.
/// Pfad: gifts/{recipientUid}/{giftId}
/// </summary>
public class FirebaseGift
{
    [JsonPropertyName("fromUid")]
    public string FromUid { get; set; } = "";

    [JsonPropertyName("fromName")]
    public string FromName { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "golden_screws";

    [JsonPropertyName("amount")]
    public int Amount { get; set; } = 3;

    [JsonPropertyName("sentAt")]
    public string SentAt { get; set; } = "";

    [JsonPropertyName("claimed")]
    public bool Claimed { get; set; }
}
