namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Firebase REST API Client für Realtime Database + Anonymous Auth.
/// Plattformübergreifend (Android + Desktop) via HttpClient.
/// </summary>
public interface IFirebaseService : IDisposable
{
    /// <summary>Firebase User-ID (nur für Authentifizierung, NICHT als Spieler-Identität verwenden).</summary>
    string? Uid { get; }

    /// <summary>Stabile Spieler-ID (GUID), überlebt Firebase-Account-Wechsel. Für alle Daten-Pfade verwenden.</summary>
    string? PlayerId { get; }

    /// <summary>Ob der Service online erreichbar ist.</summary>
    bool IsOnline { get; }

    /// <summary>
    /// Initialisiert die stabile PlayerId aus Preferences oder GameState-Backup.
    /// Generiert neue GUID falls keine vorhanden.
    /// </summary>
    void InitializePlayerId(string? gameStateBackup);

    /// <summary>Stellt sicher dass der User authentifiziert ist (Anonymous Auth).</summary>
    Task EnsureAuthenticatedAsync();

    /// <summary>GET-Request an Firebase Realtime Database.</summary>
    Task<T?> GetAsync<T>(string path) where T : class;

    /// <summary>PUT-Request (Überschreiben). Gibt true bei Erfolg zurück, false bei Fehler.</summary>
    Task<bool> SetAsync<T>(string path, T data);

    /// <summary>PATCH-Request (Teilweise aktualisieren). Gibt true bei Erfolg zurück, false bei Fehler.</summary>
    Task<bool> UpdateAsync(string path, Dictionary<string, object> updates);

    /// <summary>POST-Request (Neuen Eintrag erstellen, gibt Key zurück).</summary>
    Task<string?> PushAsync<T>(string path, T data);

    /// <summary>DELETE-Request. Gibt true bei Erfolg zurück, false bei Fehler.</summary>
    Task<bool> DeleteAsync(string path);

    /// <summary>GET mit Query-Parametern (orderBy, limitToLast etc.).</summary>
    Task<string?> QueryAsync(string path, string queryParams);
}
