using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für die Log-Ansicht (Bot-Aktivitäten, Fehler, Trade-Signale).
/// </summary>
public partial class LogViewModel : ObservableObject
{
    // Filter
    [ObservableProperty] private bool _showDebug;
    [ObservableProperty] private bool _showInfo = true;
    [ObservableProperty] private bool _showTrade = true;
    [ObservableProperty] private bool _showWarning = true;
    [ObservableProperty] private bool _showError = true;

    [ObservableProperty] private string _selectedCategory = "Alle";
    [ObservableProperty] private int _entryCount;
    [ObservableProperty] private string _emptyStateText = "Noch keine Log-Einträge. Starte den Bot oder einen Backtest.";

    public string[] Categories => new[] { "Alle", "Trade", "Scanner", "Risk", "Engine", "WebSocket", "Backtest" };

    public ObservableCollection<LogDisplayItem> LogEntries { get; } = new();

    public LogViewModel()
    {
    }

    [RelayCommand]
    private void Clear()
    {
        LogEntries.Clear();
        EntryCount = 0;
    }
}

/// <summary>
/// Einzelner Log-Eintrag für die Anzeige.
/// </summary>
public record LogDisplayItem(DateTime Timestamp, string Level, string Category, string Message)
{
    public string TimeText => Timestamp.ToString("HH:mm:ss");
    public string LevelColor => Level switch
    {
        "Error" => "#EF4444",
        "Warning" => "#F59E0B",
        "Trade" => "#10B981",
        "Debug" => "#6B7280",
        _ => "#E2E8F0"
    };
}
