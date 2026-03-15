using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BingXBot.ViewModels;

/// <summary>
/// Haupt-ViewModel mit Sidebar-Navigation und Status-Anzeige.
/// </summary>
public partial class MainViewModel : ObservableObject
{
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
    [ObservableProperty] private string _connectionStatus = "Getrennt";

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
