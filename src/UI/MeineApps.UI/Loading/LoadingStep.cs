namespace MeineApps.UI.Loading;

/// <summary>
/// Einzelner Ladeschritt mit Name, Gewichtung und Ausführungs-Funktion.
/// Die Gewichtung bestimmt den Anteil am Gesamtfortschritt.
/// </summary>
public class LoadingStep
{
    /// <summary>
    /// Interner Name (für Logging/Debugging)
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Lokalisierter Anzeige-Text (z.B. "Daten werden geladen...")
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gewichtung relativ zu anderen Steps (z.B. 20 = 20% bei Gesamtgewicht 100)
    /// </summary>
    public required int Weight { get; init; }

    /// <summary>
    /// Asynchrone Ausführungs-Funktion des Ladeschritts
    /// </summary>
    public required Func<Task> ExecuteAsync { get; init; }
}
