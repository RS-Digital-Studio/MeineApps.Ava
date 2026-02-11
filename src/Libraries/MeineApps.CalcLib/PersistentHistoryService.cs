using System.Text.Json;

namespace MeineApps.CalcLib;

/// <summary>
/// Persistente Implementation des History-Services mit JSON-Speicherung.
/// </summary>
public class PersistentHistoryService : IHistoryService
{
    private readonly List<CalculationHistoryEntry> _history = new();
    private const int MAX_ENTRIES = 100;
    private const string HISTORY_FILE = "calculator_history.json";

    private readonly string _filePath;

    public IReadOnlyList<CalculationHistoryEntry> History => _history.AsReadOnly();

    public event EventHandler? HistoryChanged;

    public PersistentHistoryService(string appDataDirectory)
    {
        _filePath = Path.Combine(appDataDirectory, HISTORY_FILE);
        LoadHistory();
    }

    public void AddEntry(string expression, string result, double value)
    {
        if (string.IsNullOrWhiteSpace(expression) || string.IsNullOrWhiteSpace(result))
        {
            return;
        }

        var entry = new CalculationHistoryEntry(
            Expression: expression,
            Result: result,
            ResultValue: value,
            Timestamp: DateTime.Now
        );

        // Neueste Einträge am Anfang
        _history.Insert(0, entry);

        // Begrenze die Anzahl der Einträge
        if (_history.Count > MAX_ENTRIES)
        {
            _history.RemoveAt(_history.Count - 1);
        }

        SaveHistory();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void LoadEntries(IEnumerable<CalculationHistoryEntry> entries)
    {
        _history.Clear();
        foreach (var entry in entries)
        {
            _history.Add(entry);
            if (_history.Count >= MAX_ENTRIES) break;
        }
        SaveHistory();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _history.Clear();
        SaveHistory();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Löscht einen einzelnen Eintrag aus dem Verlauf.
    /// </summary>
    public void DeleteEntry(CalculationHistoryEntry entry)
    {
        if (_history.Remove(entry))
        {
            SaveHistory();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void LoadHistory()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var entries = JsonSerializer.Deserialize<List<CalculationHistoryEntry>>(json);

                if (entries != null)
                {
                    _history.Clear();
                    _history.AddRange(entries);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PersistentHistoryService: Fehler beim Laden: {ex.Message}");
        }
    }

    private void SaveHistory()
    {
        try
        {
            var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PersistentHistoryService: Fehler beim Speichern: {ex.Message}");
        }
    }
}
