using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Cloud-Save ueber Firebase Realtime-DB (plattformuebergreifend, REST-basiert).
/// Ersetzt den nicht-funktionalen Play-Games-v2-Stub.
///
/// Struktur in Firebase:
/// <code>
/// cloud_saves/{playerId}/
///   data:      &lt;JSON-State&gt;       (HMAC-signiert via IGameIntegrityService)
///   level:     int                   (fuer UI/Konflikt-Anzeige)
///   money:     decimal               (fuer UI/Konflikt-Anzeige)
///   savedAt:   ISO-8601 UTC          (Newest-Wins-Entscheidung)
///   version:   int                   (GameState.Version)
///   appVersion:string                (Client-App-Version)
/// </code>
///
/// Die Strategie beim App-Start:
/// <list type="number">
///   <item>Lokalen Spielstand laden.</item>
///   <item>Cloud-Metadaten abrufen (nur Header, nicht den gesamten State).</item>
///   <item>Wenn Cloud &gt; Lokal (hoeheres Level oder neueres savedAt): Konflikt-Dialog anzeigen.</item>
///   <item>Sonst lokal weiter, beim naechsten Save wird auch Cloud aktualisiert.</item>
/// </list>
/// </summary>
public interface ICloudSaveService
{
    /// <summary>
    /// Ob der Service nutzbar ist (Firebase online + Cloud-Save-Toggle an).
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Laedt nur die Metadaten des Cloud-Spielstands (ohne den kompletten State).
    /// Null zurueck wenn kein Cloud-Stand existiert oder Firebase offline ist.
    /// </summary>
    Task<CloudSaveMetadata?> GetMetadataAsync();

    /// <summary>
    /// Laedt den kompletten Cloud-Spielstand. Prueft HMAC-Signatur. Null bei Fehler.
    /// </summary>
    Task<GameState?> DownloadAsync();

    /// <summary>
    /// Schiebt den aktuellen State in die Cloud. Uebernimmt HMAC-Signierung.
    /// Ist idempotent — mehrfacher Aufruf schadet nicht.
    /// </summary>
    Task<bool> UploadAsync(GameState state);

    /// <summary>
    /// Loescht den Cloud-Save (z.B. nach "Fortschritt zuruecksetzen").
    /// </summary>
    Task<bool> DeleteAsync();
}
