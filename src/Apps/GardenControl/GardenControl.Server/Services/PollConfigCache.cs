namespace GardenControl.Server.Services;

/// <summary>
/// Hält die im Poll-Zyklus benötigten Konfigurationswerte (Abfrageintervall,
/// Verlaufs-Aufbewahrung) im Speicher, damit der SensorPollingWorker sie nicht bei JEDER
/// Iteration frisch aus SQLite lesen muss (vermeidet eine SD-Karten-Query pro Tick).
///
/// Befüllung: einmal beim Worker-Start aus der DB. Invalidierung: der PUT /api/config-Handler
/// schreibt den geänderten Wert hier ein. Die Werte ändern sich ausschließlich über diesen
/// Endpoint, daher ist der Cache stets konsistent mit der DB.
///
/// Als DI-Singleton registriert (kein statischer Zustand) — testbar und ohne Service-Locator.
/// </summary>
public sealed class PollConfigCache
{
    // Defaults entsprechen den DB-Standardwerten (DatabaseService.InitializeAsync) und greifen
    // nur, falls der Wert vor der Initialisierung gelesen wird.
    private volatile int _pollIntervalSeconds = 30;
    private volatile int _historyRetentionDays = 30;

    /// <summary>Sensor-Abfrageintervall in Sekunden.</summary>
    public int PollIntervalSeconds
    {
        get => _pollIntervalSeconds;
        set => _pollIntervalSeconds = value;
    }

    /// <summary>Maximale Verlaufsdaten-Aufbewahrung in Tagen.</summary>
    public int HistoryRetentionDays
    {
        get => _historyRetentionDays;
        set => _historyRetentionDays = value;
    }
}
