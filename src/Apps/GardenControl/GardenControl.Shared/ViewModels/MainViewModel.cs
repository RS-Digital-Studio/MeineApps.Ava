using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GardenControl.Core;
using GardenControl.Core.DTOs;
using GardenControl.Shared.Services;
using MeineApps.Core.Ava.Async;
using MeineApps.Core.Ava.Services;
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
    private readonly IPreferencesService _prefs;
    private readonly IAppLifecycleService _lifecycle;
    private readonly BackPressHelper _backPressHelper = new();
    // Aktuelles Server-Secret (persistiert, in beide Services injiziert vor Connect).
    private string _serverSecret;

    /// <summary>Wird ausgeloest um einen Exit-Hinweis anzuzeigen (Toast "Nochmal druecken zum Beenden").</summary>
    public event Action<string>? ExitHintRequested;

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
        IPreferencesService prefs,
        IAppLifecycleService lifecycle,
        DashboardViewModel dashboard,
        ZoneControlViewModel zoneControl,
        ScheduleViewModel schedule,
        CalibrationViewModel calibration,
        HistoryViewModel history,
        SettingsViewModel settings)
    {
        _connection = connection;
        _api = api;
        _prefs = prefs;
        _lifecycle = lifecycle;
        // Persistiertes Server-Secret laden (Default-Dev-Secret falls noch nie gesetzt).
        _serverSecret = _prefs.Get(GardenAuth.ClientSecretPreferenceKey, GardenAuth.DefaultDevSecret);

        Dashboard = dashboard;
        ZoneControl = zoneControl;
        Schedule = schedule;
        Calibration = calibration;
        History = history;
        Settings = settings;

        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);

        // Verbindungsstatus tracken (SignalR-Callback kommt auf Hintergrund-Thread)
        _connection.ConnectionChanged += OnConnectionChanged;

        // API-Fehler global anzeigen
        _api.ErrorOccurred += OnApiErrorOccurred;

        // Settings-ViewModel kann URL + Secret ändern
        Settings.ServerUrlChanged += OnSettingsServerUrlChanged;
        Settings.ServerSecretChanged += OnSettingsServerSecretChanged;

        // App-Pause → SignalR trennen (kein Hintergrund-Empfang, spart Radio-Wakeups);
        // App-Resume → neu verbinden (der erste Push aktualisiert die Live-Werte sofort).
        _lifecycle.Paused += OnAppPaused;
        _lifecycle.Resumed += OnAppResumed;
    }

    /// <summary>
    /// App ging in den Hintergrund: SignalR-Hub trennen. GardenControl ist reine Fernsteuerung/
    /// Monitor — der Pi bewässert autonom 24/7 weiter, im Hintergrund braucht die App keine
    /// offene Verbindung. Fire-and-forget (kein Blockieren im Lifecycle-Handler), Fehler werden
    /// geloggt. Reconnect-Konfiguration (WithAutomaticReconnect) bleibt davon unberührt.
    /// </summary>
    private void OnAppPaused() =>
        _connection.DisconnectAsync().Forget();

    /// <summary>
    /// App kam in den Vordergrund: neu verbinden. Über <see cref="ConnectAsync"/>, damit Secret +
    /// Server-URL erneut injiziert werden und der Status korrekt in der UI erscheint. Fire-and-forget.
    /// </summary>
    private void OnAppResumed() =>
        ConnectAsync().Forget();

    /// <summary>Verbindungsstatus-Update vom SignalR-Client (kommt auf Hintergrund-Thread).</summary>
    private void OnConnectionChanged(bool connected)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsConnected = connected;
            ConnectionStatus = connected ? "Verbunden" : "Verbindung verloren...";
            if (connected) ClearError();
        });
    }

    /// <summary>Globale Anzeige eines API-Fehlers (kommt synchron aus dem ApiService-catch).</summary>
    private void OnApiErrorOccurred(string error) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => ShowError(error));

    /// <summary>Reagiert auf eine im SettingsViewModel geänderte Server-URL mit Reconnect.</summary>
    private async void OnSettingsServerUrlChanged(string url)
    {
        ServerUrl = url;
        await ConnectAsync();
    }

    /// <summary>Reagiert auf ein im SettingsViewModel geändertes Server-Secret mit Reconnect.</summary>
    private async void OnSettingsServerSecretChanged(string secret)
    {
        _serverSecret = secret;
        await ConnectAsync();
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
            // Secret in beide Services injizieren BEVOR verbunden wird (REST-Header + SignalR-Auth).
            _api.SetSecret(_serverSecret);
            _connection.SetSecret(_serverSecret);
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

    /// <summary>
    /// Behandelt die Android-Zurueck-Taste. Gibt true zurueck wenn konsumiert (App bleibt offen),
    /// false wenn die App geschlossen werden darf (Double-Back-to-Exit).
    /// Reihenfolge: Error-Banner schliessen → Tab zum Dashboard zurueck → Double-Back-to-Exit.
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. Fehler-Overlay schliessen
        if (HasError)
        {
            ClearError();
            return true;
        }

        // 2. Nicht auf Dashboard → zurueck zum Dashboard
        if (CurrentPage != "Dashboard")
        {
            CurrentPage = "Dashboard";
            return true;
        }

        // 3. Auf Dashboard: Double-Back-to-Exit
        return _backPressHelper.HandleDoubleBack("Erneut drücken zum Beenden");
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

    /// <summary>
    /// Sauberes Cleanup: eigene Service-Event-Abos lösen, Child-ViewModels disposen
    /// (melden ihre ConnectionService-Abos ab) und die SignalR-Verbindung trennen.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Eigene Subscriptions abmelden
        _connection.ConnectionChanged -= OnConnectionChanged;
        _api.ErrorOccurred -= OnApiErrorOccurred;
        Settings.ServerUrlChanged -= OnSettingsServerUrlChanged;
        Settings.ServerSecretChanged -= OnSettingsServerSecretChanged;
        _lifecycle.Paused -= OnAppPaused;
        _lifecycle.Resumed -= OnAppResumed;

        // Child-ViewModels abmelden (HistoryViewModel abonniert keine Events → kein IDisposable)
        Dashboard.Dispose();
        ZoneControl.Dispose();
        Schedule.Dispose();
        Calibration.Dispose();
        Settings.Dispose();

        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
