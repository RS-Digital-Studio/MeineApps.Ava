using System.Text.Json.Serialization;

namespace BomberBlast.Models.Firebase;

/// <summary>
/// Antwort des Firebase Token-Refresh-Endpunkts.
/// </summary>
public class FirebaseTokenResponse
{
    [JsonPropertyName("id_token")]
    public string IdToken { get; set; } = "";

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public string ExpiresIn { get; set; } = "3600";
}
