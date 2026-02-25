namespace BomberBlast.Services;

/// <summary>
/// Firebase REST API Client für Realtime Database + Anonymous Auth.
/// Plattformübergreifend (Android + Desktop) via HttpClient.
/// </summary>
public interface IFirebaseService : IDisposable
{
    /// <summary>Firebase User-ID (nach Authentifizierung).</summary>
    string? Uid { get; }

    /// <summary>Ob der Service online erreichbar ist.</summary>
    bool IsOnline { get; }

    /// <summary>Stellt sicher dass der User authentifiziert ist (Anonymous Auth).</summary>
    Task EnsureAuthenticatedAsync();

    /// <summary>GET-Request an Firebase Realtime Database.</summary>
    Task<T?> GetAsync<T>(string path) where T : class;

    /// <summary>PUT-Request (Überschreiben).</summary>
    Task SetAsync<T>(string path, T data);

    /// <summary>PATCH-Request (Teilweise aktualisieren).</summary>
    Task UpdateAsync(string path, Dictionary<string, object> updates);

    /// <summary>POST-Request (Neuen Eintrag erstellen, gibt Key zurück).</summary>
    Task<string?> PushAsync<T>(string path, T data);

    /// <summary>DELETE-Request.</summary>
    Task DeleteAsync(string path);
}
