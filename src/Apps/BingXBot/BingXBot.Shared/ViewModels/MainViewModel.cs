using BingXBot.Core.Enums;
using BingXBot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;

namespace BingXBot.ViewModels;

/// <summary>
/// Haupt-ViewModel mit Sidebar-Navigation und Status-Anzeige.
/// Empfängt Bot-Status-Updates über den BotEventBus.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly BotEventBus _eventBus;

    [ObservableProperty] private string _currentPage = "Dashboard";
    [ObservableProperty] private bool _isDashboardActive = true;
    [ObservableProperty] private bool _isScannerActive;
    [ObservableProperty] private bool _isStrategyActive;
    [ObservableProperty] private bool _isBacktestActive;
    [ObservableProperty] private bool _isTradeHistoryActive;
    [ObservableProperty] private bool _isRiskSettingsActive;
    [ObservableProperty] private bool _isLogActive;
    [ObservableProperty] private bool _isSettingsActive;
    [ObservableProperty] private string _botStatus = "Gestoppt";
    [ObservableProperty] private string _tradingMode = "Paper";
    [ObservableProperty] private string _connectionStatus = "Marktdaten verfügbar";
    [ObservableProperty] private bool _isConnected = true;

    /// <summary>Farbe des Verbindungs-Indikators (grün=verbunden, rot=getrennt).</summary>
    public string ConnectionDotColor => IsConnected ? "#10B981" : "#EF4444";

    /// <summary>Farbe des Bot-Status-Texts (grün=läuft, gelb=pausiert, rot=fehler, grau=gestoppt).</summary>
    public string BotStatusColor => BotStatus switch
    {
        "Läuft" => "#10B981",
        "Startet..." => "#3B82F6",
        "Pausiert" => "#F59E0B",
        "Notfall-Stop" or "Fehler" => "#EF4444",
        _ => "#94A3B8"
    };

    partial void OnIsConnectedChanged(bool value) => OnPropertyChanged(nameof(ConnectionDotColor));
    partial void OnBotStatusChanged(string value) => OnPropertyChanged(nameof(BotStatusColor));

    public MainViewModel(BotEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.BotStateChanged += OnBotStateChanged;
    }

    private void OnBotStateChanged(object? sender, BotState state)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            BotStatus = state switch
            {
                BotState.Running => "Läuft",
                BotState.Paused => "Pausiert",
                BotState.Stopped => "Gestoppt",
                BotState.Starting => "Startet...",
                BotState.EmergencyStop => "Notfall-Stop",
                BotState.Error => "Fehler",
                _ => state.ToString()
            };

            IsConnected = state is BotState.Running or BotState.Paused or BotState.Starting;
        });
    }

    /// <summary>
    /// Navigation zu einer Seite. Parameter ist immer string (XAML CommandParameter).
    /// </summary>
    [RelayCommand]
    private void NavigateTo(string page)
    {
        CurrentPage = page;
        IsDashboardActive = page == "Dashboard";
        IsScannerActive = page == "Scanner";
        IsStrategyActive = page == "Strategie";
        IsBacktestActive = page == "Backtest";
        IsTradeHistoryActive = page == "TradeHistory";
        IsRiskSettingsActive = page == "RiskSettings";
        IsLogActive = page == "Log";
        IsSettingsActive = page == "Settings";
    }
}
