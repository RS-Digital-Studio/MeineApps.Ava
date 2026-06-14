using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GardenControl.Core;
using GardenControl.Shared.Services;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;

namespace GardenControl.Shared.ViewModels;

/// <summary>
/// Einstellungen - Server-IP, Server-Secret, Verbindungstest, Daten-Export.
/// Implementiert IDisposable, um das ConnectionService-Event-Abo sauber abzumelden.
/// </summary>
public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IApiService _api;
    private readonly IConnectionService _connection;
    private readonly IPreferencesService _prefs;

    [ObservableProperty] private string _serverUrl = "http://192.168.178.56:5000";
    [ObservableProperty] private string _serverSecret = GardenAuth.DefaultDevSecret;
    [ObservableProperty] private string _connectionTestResult = string.Empty;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _serverInfo = string.Empty;

    /// <summary>Event wenn sich die Server-URL ändert</summary>
    public event Action<string>? ServerUrlChanged;

    /// <summary>Event wenn sich das Server-Secret ändert (→ MainViewModel setzt + reconnectet).</summary>
    public event Action<string>? ServerSecretChanged;

    public SettingsViewModel(IApiService api, IConnectionService connection, IPreferencesService prefs)
    {
        _api = api;
        _connection = connection;
        _prefs = prefs;
        // Persistiertes Secret laden (Default-Dev-Secret, falls noch nie gesetzt).
        _serverSecret = _prefs.Get(GardenAuth.ClientSecretPreferenceKey, GardenAuth.DefaultDevSecret);
        _connection.ConnectionChanged += OnConnectionChanged;
    }

    private void OnConnectionChanged(bool connected) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => IsConnected = connected);

    [RelayCommand]
    private async Task TestConnection()
    {
        ConnectionTestResult = "Teste Verbindung...";
        _api.SetServerUrl(ServerUrl);
        // Aktuelles (ggf. noch nicht gespeichertes) Secret-Eingabefeld mittesten.
        _api.SetSecret(ServerSecret);

        var success = await _api.TestConnectionAsync();
        ConnectionTestResult = success
            ? "Verbindung erfolgreich!"
            : "Verbindung fehlgeschlagen. IP und Port prüfen.";
    }

    [RelayCommand]
    private void SaveServerUrl()
    {
        ServerUrlChanged?.Invoke(ServerUrl);
    }

    [RelayCommand]
    private void SaveServerSecret()
    {
        _prefs.Set(GardenAuth.ClientSecretPreferenceKey, ServerSecret);
        ServerSecretChanged?.Invoke(ServerSecret);
    }

    [RelayCommand]
    private async Task RefreshServerInfo()
    {
        var config = await _api.GetConfigAsync();
        if (config.Count > 0)
        {
            var lines = config.Select(kv => $"{kv.Key}: {kv.Value}");
            ServerInfo = string.Join("\n", lines);
        }
        else
        {
            ServerInfo = "Keine Konfiguration abrufbar";
        }
    }

    /// <summary>Meldet das im Konstruktor abonnierte ConnectionService-Event wieder ab.</summary>
    public void Dispose()
    {
        _connection.ConnectionChanged -= OnConnectionChanged;
        GC.SuppressFinalize(this);
    }
}
