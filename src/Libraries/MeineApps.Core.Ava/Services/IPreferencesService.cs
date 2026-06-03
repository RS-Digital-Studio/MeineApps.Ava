namespace MeineApps.Core.Ava.Services;

/// <summary>
/// Service for storing and retrieving app preferences
/// </summary>
public interface IPreferencesService
{
    /// <summary>
    /// Get a preference value
    /// </summary>
    T Get<T>(string key, T defaultValue);

    /// <summary>
    /// Set a preference value
    /// </summary>
    void Set<T>(string key, T value);

    /// <summary>
    /// Check if a preference exists
    /// </summary>
    bool ContainsKey(string key);

    /// <summary>
    /// Remove a preference
    /// </summary>
    void Remove(string key);

    /// <summary>
    /// Clear all preferences
    /// </summary>
    void Clear();

    /// <summary>
    /// Pausiert das Schreiben auf die Festplatte. <see cref="Set{T}"/>/<see cref="Remove"/>
    /// aktualisieren weiterhin den In-Memory-Stand, aber es wird nicht persistiert, bis
    /// <see cref="ResumePersistence"/> aufgerufen wird. Gedacht für performance-kritische
    /// Phasen (z.B. ein laufendes Spiel), um Disk-I/O-bedingtes Ruckeln zu vermeiden.
    /// Mehrfachaufrufe sind unkritisch (idempotent).
    /// </summary>
    void SuspendPersistence();

    /// <summary>
    /// Setzt das Schreiben fort und persistiert aufgestaute Änderungen einmalig sofort.
    /// </summary>
    void ResumePersistence();

    /// <summary>
    /// Schreibt aufgestaute Änderungen sofort auf die Festplatte, ohne den Suspend-Zustand
    /// zu ändern. Für Lifecycle-Hooks (z.B. App geht in den Hintergrund) gedacht, damit bei
    /// aktivem Suspend kein Fortschritt verloren geht.
    /// </summary>
    void FlushPending();
}
