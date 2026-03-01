namespace MeineApps.Core.Ava.Services;

/// <summary>
/// Service for calculation history (last N calculations per calculator)
/// </summary>
public interface ICalculationHistoryService
{
    Task AddCalculationAsync(string calculatorId, string title, Dictionary<string, object> data);
    Task<List<CalculationHistoryItem>> GetHistoryAsync(string calculatorId, int maxItems = 10);
    Task<CalculationHistoryItem?> GetCalculationAsync(string id);
    Task DeleteCalculationAsync(string id);
    Task ClearHistoryAsync(string calculatorId);
    Task CleanupOldEntriesAsync(int olderThanDays = 90);

    /// <summary>
    /// Lädt History-Einträge aus ALLEN Rechnern, maxItemsPerCalculator pro Rechner.
    /// Ergebnis sortiert nach CreatedAt absteigend.
    /// </summary>
    Task<List<CalculationHistoryItem>> GetAllHistoryAsync(int maxItemsPerCalculator = 10);
}

/// <summary>
/// A single calculation history entry
/// </summary>
public class CalculationHistoryItem
{
    public string Id { get; set; } = string.Empty;
    public string CalculatorId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime CreatedAt { get; set; }

    public string DisplayDate => CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
}
