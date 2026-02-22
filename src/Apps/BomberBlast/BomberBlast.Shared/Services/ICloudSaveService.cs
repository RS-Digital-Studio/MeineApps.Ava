namespace BomberBlast.Services;

/// <summary>
/// Cloud Save Abstraktion.
/// Android: CloudSaveService (Google Play Games Snapshots oder Drive API).
/// Desktop: NullCloudSaveService (no-op).
///
/// Local-First: Spiel funktioniert immer offline, Cloud ist optional.
/// Sync-Strategie: Pull bei App-Start, Push nach wichtigen Aktionen.
/// Konflikt-Resolution: Spieler-freundlichster Stand gewinnt (mehr Sterne/Coins/Karten).
/// </summary>
public interface ICloudSaveService
{
    /// <summary>Ob Cloud Save verfügbar und aktiviert ist</summary>
    bool IsEnabled { get; }

    /// <summary>Ob gerade ein Sync läuft</summary>
    bool IsSyncing { get; }

    /// <summary>Letzter erfolgreicher Sync-Zeitpunkt (UTC, ISO 8601)</summary>
    string? LastSyncTimeUtc { get; }

    /// <summary>Wird gefeuert wenn sich der Sync-Status ändert</summary>
    event EventHandler? SyncStatusChanged;

    /// <summary>
    /// Cloud-Stand laden und mit lokalem Stand vergleichen.
    /// Besserer Stand wird angewendet.
    /// Aufgerufen bei App-Start.
    /// </summary>
    /// <returns>true wenn Cloud-Daten geladen und angewendet wurden</returns>
    Task<bool> TryLoadFromCloudAsync();

    /// <summary>
    /// Lokalen Stand in die Cloud pushen (mit Debounce).
    /// Sammelt alle IPreferencesService-Keys und lädt hoch.
    /// </summary>
    Task SchedulePushAsync();

    /// <summary>
    /// Sofortiger Upload ohne Debounce.
    /// Für manuelle Sync-Buttons in den Einstellungen.
    /// </summary>
    Task ForceUploadAsync();

    /// <summary>
    /// Cloud-Stand erzwingen (überschreibt lokalen Stand).
    /// Für manuelle "Cloud-Stand laden" in den Einstellungen.
    /// </summary>
    Task<bool> ForceDownloadAsync();

    /// <summary>Cloud Save aktivieren/deaktivieren</summary>
    void SetEnabled(bool enabled);
}
