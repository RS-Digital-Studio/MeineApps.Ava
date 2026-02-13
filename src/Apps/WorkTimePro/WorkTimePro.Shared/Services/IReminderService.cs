namespace WorkTimePro.Services;

/// <summary>
/// Service für WorkTimePro-spezifische Erinnerungen (Morgen, Abend, Pause, Überstunden, Wochenzusammenfassung)
/// </summary>
public interface IReminderService : IDisposable
{
    /// <summary>
    /// Initialisiert Reminder basierend auf aktuellem Status und Settings.
    /// Wird beim App-Start aufgerufen.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Plant alle Reminder neu (nach Settings-Änderung).
    /// Cancelt bestehende und plant basierend auf neuen Settings.
    /// </summary>
    Task RescheduleAsync();
}
