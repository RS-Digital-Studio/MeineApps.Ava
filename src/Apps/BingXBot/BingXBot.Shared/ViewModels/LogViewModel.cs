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

    public string[] Categories => new[] { "Alle", "Trade", "Scanner", "Risk", "Engine", "WebSocket", "Backtest" };

    public ObservableCollection<LogDisplayItem> LogEntries { get; } = new();

    public LogViewModel()
    {
        LoadDemoLogs();
    }

    [RelayCommand]
    private void Clear()
    {
        LogEntries.Clear();
        EntryCount = 0;
    }

    private void LoadDemoLogs()
    {
        LogEntries.Add(new(DateTime.UtcNow.AddMinutes(-10), "Info", "Engine", "Bot gestartet im Paper-Modus"));
        LogEntries.Add(new(DateTime.UtcNow.AddMinutes(-9), "Trade", "Scanner", "BTC-USDT: Momentum-Setup gefunden (Score: 95.5)"));
        LogEntries.Add(new(DateTime.UtcNow.AddMinutes(-8), "Trade", "Trade", "BTC-USDT: Long 0.1 @ 50000"));
        LogEntries.Add(new(DateTime.UtcNow.AddMinutes(-7), "Info", "Risk", "BTC-USDT: Trade erlaubt (2% Position, 10x Leverage)"));
        LogEntries.Add(new(DateTime.UtcNow.AddMinutes(-5), "Warning", "WebSocket", "Verbindung unterbrochen, Reconnect..."));
        LogEntries.Add(new(DateTime.UtcNow.AddMinutes(-4), "Info", "WebSocket", "Reconnect erfolgreich"));
        LogEntries.Add(new(DateTime.UtcNow.AddMinutes(-2), "Trade", "Trade", "BTC-USDT: Position geschlossen, P&L: +120 USDT"));
        LogEntries.Add(new(DateTime.UtcNow.AddMinutes(-1), "Error", "Engine", "API-Fehler: Rate Limit erreicht"));
        EntryCount = LogEntries.Count;
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
