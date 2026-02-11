namespace MeineApps.CalcLib;

/// <summary>
/// Service für den Berechnungsverlauf (Session-basiert, keine Persistenz).
/// </summary>
public interface IHistoryService
{
    /// <summary>
    /// Alle Verlaufseinträge (neueste zuerst).
    /// </summary>
    IReadOnlyList<CalculationHistoryEntry> History { get; }

    /// <summary>
    /// Fügt einen neuen Eintrag zum Verlauf hinzu.
    /// </summary>
    void AddEntry(string expression, string result, double value);

    /// <summary>
    /// Lädt Einträge aus einer externen Quelle (z.B. Persistenz).
    /// Ersetzt den aktuellen Verlauf.
    /// </summary>
    void LoadEntries(IEnumerable<CalculationHistoryEntry> entries);

    /// <summary>
    /// Löscht den gesamten Verlauf.
    /// </summary>
    void Clear();

    /// <summary>
    /// Löscht einen einzelnen Eintrag aus dem Verlauf (optional).
    /// </summary>
    void DeleteEntry(CalculationHistoryEntry entry);

    /// <summary>
    /// Event wird ausgelöst, wenn sich der Verlauf ändert.
    /// </summary>
    event EventHandler? HistoryChanged;
}
