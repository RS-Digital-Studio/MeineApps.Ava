namespace MeineApps.CalcLib;

/// <summary>
/// Implementation des History-Services.
/// Speichert Berechnungen nur für die aktuelle Session (keine Persistenz).
/// </summary>
public class HistoryService : IHistoryService
{
    private readonly List<CalculationHistoryEntry> _history = new();
    private const int MAX_ENTRIES = 100;

    public IReadOnlyList<CalculationHistoryEntry> History => _history.AsReadOnly();

    public event EventHandler? HistoryChanged;

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
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _history.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteEntry(CalculationHistoryEntry entry)
    {
        if (_history.Remove(entry))
        {
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
