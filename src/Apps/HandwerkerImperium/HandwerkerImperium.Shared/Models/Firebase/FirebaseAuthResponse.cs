using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models.Firebase;

/// <summary>
/// Antwort der Firebase Anonymous Auth (signUp-Endpunkt).
/// </summary>
public class FirebaseAuthResponse
{
    [JsonPropertyName("idToken")]
    public string IdToken { get; set; } = "";

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("localId")]
    public string LocalId { get; set; } = "";

    [JsonPropertyName("expiresIn")]
    public string ExpiresIn { get; set; } = "3600";
}
