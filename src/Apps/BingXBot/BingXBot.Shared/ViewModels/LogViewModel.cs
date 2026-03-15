using BingXBot.Core.Models;
using BingXBot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für die Log-Ansicht (Bot-Aktivitäten, Fehler, Trade-Signale).
/// Empfängt echte Log-Einträge über den BotEventBus.
/// </summary>
public partial class LogViewModel : ObservableObject
{
    private readonly BotEventBus _eventBus;
    private readonly List<LogDisplayItem> _allLogs = new();

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

    public LogViewModel(BotEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.LogEmitted += OnLogEmitted;
    }

    private void OnLogEmitted(object? sender, LogEntry entry)
    {
        var item = new LogDisplayItem(entry.Timestamp, entry.Level.ToString(), entry.Category, entry.Message);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _allLogs.Add(item);

            // Ringpuffer: max 1000 Einträge
            if (_allLogs.Count > 1000)
                _allLogs.RemoveAt(0);

            // Nur anzeigen wenn Filter passt
            if (PassesFilter(item))
            {
                LogEntries.Add(item);
                if (LogEntries.Count > 1000)
                    LogEntries.RemoveAt(0);
            }

            EntryCount = LogEntries.Count;
        });
    }

    /// <summary>
    /// Prüft ob ein Log-Eintrag die aktuellen Filter-Kriterien erfüllt.
    /// </summary>
    private bool PassesFilter(LogDisplayItem item)
    {
        // Level-Filter
        var levelOk = item.Level switch
        {
            "Debug" => ShowDebug,
            "Info" => ShowInfo,
            "Trade" => ShowTrade,
            "Warning" => ShowWarning,
            "Error" => ShowError,
            _ => true
        };
        if (!levelOk) return false;

        // Kategorie-Filter
        if (SelectedCategory != "Alle" && item.Category != SelectedCategory)
            return false;

        return true;
    }

    /// <summary>
    /// Baut die angezeigte Liste neu auf basierend auf aktuellen Filter-Einstellungen.
    /// </summary>
    private void ApplyFilter()
    {
        LogEntries.Clear();
        foreach (var log in _allLogs)
        {
            if (PassesFilter(log))
                LogEntries.Add(log);
        }
        EntryCount = LogEntries.Count;
    }

    // Filter-Änderungen lösen ApplyFilter() aus
    partial void OnShowDebugChanged(bool value) => ApplyFilter();
    partial void OnShowInfoChanged(bool value) => ApplyFilter();
    partial void OnShowTradeChanged(bool value) => ApplyFilter();
    partial void OnShowWarningChanged(bool value) => ApplyFilter();
    partial void OnShowErrorChanged(bool value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void Clear()
    {
        _allLogs.Clear();
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
