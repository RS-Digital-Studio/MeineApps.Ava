using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GardenControl.Core.DTOs;
using GardenControl.Shared.Services;
using MeineApps.Core.Ava.ViewModels;

namespace GardenControl.Shared.ViewModels;

/// <summary>
/// Haupt-ViewModel mit Tab-Navigation, Verbindungsmanagement und Fehlerbehandlung.
/// Implementiert IAsyncDisposable für sauberes Cleanup.
/// </summary>
public partial class MainViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly IConnectionService _connection;
    private readonly IApiService _api;

    // Child-ViewModels
    public DashboardViewModel Dashboard { get; }
    public ZoneControlViewModel ZoneControl { get; }
    public ScheduleViewModel Schedule { get; }
    public CalibrationViewModel Calibration { get; }
    public HistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty] private string _currentPage = "Dashboard";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _connectionStatus = "Nicht verbunden";
    [ObservableProperty] private string _serverUrl = "http://192.168.178.56:5000";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _hasError;

    public MainViewModel(
        IConnectionService connection,
        IApiService api,
        DashboardViewModel dashboard,
        ZoneControlViewModel zoneControl,
        ScheduleViewModel schedule,
        CalibrationViewModel calibration,
        HistoryViewModel history,
        SettingsViewModel settings)
    {
        _connection = connection;
        _api = api;

        Dashboard = dashboard;
        ZoneControl = zoneControl;
        Schedule = schedule;
        Calibration = calibration;
        History = history;
        Settings = settings;

        // Verbindungsstatus tracken (SignalR-Callback kommt auf Hintergrund-Thread)
        _connection.ConnectionChanged += connected =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsConnected = connected;
                ConnectionStatus = connected ? "Verbunden" : "Verbindung verloren...";
                if (connected) ClearError();
            });
        };

        // API-Fehler global anzeigen
        _api.ErrorOccurred += error =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ShowError(error));
        };

        // Settings-ViewModel kann URL ändern
        Settings.ServerUrlChanged += async url =>
        {
            ServerUrl = url;
            await ConnectAsync();
        };
    }

    /// <summary>Beim App-Start verbinden</summary>
    public async Task InitializeAsync()
    {
        await ConnectAsync();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            ConnectionStatus = "Verbinde...";
            ClearError();
            _api.SetServerUrl(ServerUrl);
            await _connection.ConnectAsync(ServerUrl);
            ConnectionStatus = "Verbunden";
        }
        catch (HttpRequestException ex)
        {
            ConnectionStatus = "Server nicht erreichbar";
            ShowError($"Verbindung fehlgeschlagen: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            ConnectionStatus = "Zeitüberschreitung";
            ShowError("Server antwortet nicht (Timeout)");
        }
        catch (Exception ex)
        {
            ConnectionStatus = "Verbindung fehlgeschlagen";
            ShowError($"Fehler: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Navigate(string page)
    {
        CurrentPage = page;
    }

    [RelayCommand]
    private void ClearError()
    {
        ErrorMessage = "";
        HasError = false;
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }

    // --- Tab-Aktiv-Properties für UI-Binding ---
    public bool IsDashboardActive => CurrentPage == "Dashboard";
    public bool IsZoneControlActive => CurrentPage == "ZoneControl";
    public bool IsScheduleActive => CurrentPage == "Schedule";
    public bool IsCalibrationActive => CurrentPage == "Calibration";
    public bool IsHistoryActive => CurrentPage == "History";
    public bool IsSettingsActive => CurrentPage == "Settings";

    partial void OnCurrentPageChanged(string value)
    {
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsZoneControlActive));
        OnPropertyChanged(nameof(IsScheduleActive));
        OnPropertyChanged(nameof(IsCalibrationActive));
        OnPropertyChanged(nameof(IsHistoryActive));
        OnPropertyChanged(nameof(IsSettingsActive));
    }

    /// <summary>Sauberes Cleanup: SignalR-Verbindung trennen</summary>
    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
