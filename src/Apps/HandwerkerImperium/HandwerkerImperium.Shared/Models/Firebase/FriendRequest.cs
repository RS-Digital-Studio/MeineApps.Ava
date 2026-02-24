using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models.Firebase;

/// <summary>
/// Eine eingehende Freundschaftsanfrage in Firebase.
/// Pfad: friend_requests/{targetUid}/{fromUid}
/// </summary>
public class FriendRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("sentAt")]
    public string SentAt { get; set; } = "";
}

/// <summary>
/// Ein Freundes-Eintrag in Firebase (beidseitig gespeichert).
/// Pfad: friends/{uid}/{friendUid}
/// </summary>
public class FirebaseFriend
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("addedAt")]
    public string AddedAt { get; set; } = "";
}
