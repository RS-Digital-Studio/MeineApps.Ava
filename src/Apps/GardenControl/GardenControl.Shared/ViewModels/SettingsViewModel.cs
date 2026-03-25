using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GardenControl.Shared.Services;

namespace GardenControl.Shared.ViewModels;

/// <summary>
/// Einstellungen - Server-IP, Verbindungstest, Daten-Export.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IApiService _api;
    private readonly IConnectionService _connection;

    [ObservableProperty] private string _serverUrl = "http://192.168.178.56:5000";
    [ObservableProperty] private string _connectionTestResult = string.Empty;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _serverInfo = string.Empty;

    /// <summary>Event wenn sich die Server-URL ändert</summary>
    public event Action<string>? ServerUrlChanged;

    public SettingsViewModel(IApiService api, IConnectionService connection)
    {
        _api = api;
        _connection = connection;
        _connection.ConnectionChanged += connected =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsConnected = connected);
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        ConnectionTestResult = "Teste Verbindung...";
        _api.SetServerUrl(ServerUrl);

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
}
