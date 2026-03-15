using BingXBot.Core.Configuration;
using BingXBot.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für allgemeine Einstellungen (API-Keys, Verbindung, Log-Level).
/// Nutzt ISecureStorageService für verschlüsselte API-Key-Speicherung
/// und IExchangeClient für Verbindungstests.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly BotSettings _botSettings;
    private readonly ISecureStorageService? _secureStorage;
    private readonly IExchangeClient? _exchangeClient;

    [ObservableProperty] private string _apiKey = "";
    [ObservableProperty] private string _apiSecret = "";
    [ObservableProperty] private bool _hasCredentials;
    private bool _isMasked; // Verhindert Speichern maskierter Keys
    [ObservableProperty] private string _connectionStatus = "Nicht verbunden";
    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private string _selectedLogLevel = "Info";

    public string[] LogLevels => new[] { "Debug", "Info", "Trade", "Warning", "Error" };

    public SettingsViewModel(
        BotSettings botSettings,
        ISecureStorageService? secureStorage = null,
        IExchangeClient? exchangeClient = null)
    {
        _botSettings = botSettings;
        _secureStorage = secureStorage;
        _exchangeClient = exchangeClient;

        // Gespeicherte Credentials laden
        _ = LoadCredentialsAsync();
    }

    private async Task LoadCredentialsAsync()
    {
        if (_secureStorage == null) return;

        try
        {
            HasCredentials = _secureStorage.HasCredentials;
            if (HasCredentials)
            {
                var creds = await _secureStorage.LoadCredentialsAsync();
                if (creds.HasValue)
                {
                    // API-Key maskiert anzeigen (nur letzte 4 Zeichen)
                    ApiKey = MaskKey(creds.Value.ApiKey);
                    ApiSecret = MaskKey(creds.Value.ApiSecret);
                    _isMasked = true;
                    ConnectionStatus = "Gespeichert";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Credentials laden fehlgeschlagen: {ex}");
            ConnectionStatus = "Laden fehlgeschlagen";
        }
    }

    private static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 4) return key;
        return new string('*', key.Length - 4) + key[^4..];
    }

    [RelayCommand]
    private async Task SaveCredentials()
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(ApiSecret))
        {
            ConnectionStatus = "Nicht konfiguriert";
            HasCredentials = false;
            return;
        }

        // Maskierte Keys nicht ueberschreiben - Benutzer muss neue Keys eingeben
        if (_isMasked && ApiKey.Contains('*'))
        {
            ConnectionStatus = "Neue Keys eingeben zum Aktualisieren";
            return;
        }

        if (_secureStorage != null)
        {
            try
            {
                await _secureStorage.SaveCredentialsAsync(ApiKey, ApiSecret);
                HasCredentials = true;
                ConnectionStatus = "Gespeichert";

                // Keys maskieren nach dem Speichern
                ApiKey = MaskKey(ApiKey);
                ApiSecret = MaskKey(ApiSecret);
                _isMasked = true;
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Speichern fehlgeschlagen: {ex.Message}";
            }
        }
        else
        {
            // Kein SecureStorage verfügbar, nur Status setzen
            HasCredentials = !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);
            ConnectionStatus = HasCredentials ? "Gespeichert (unverschlüsselt)" : "Nicht konfiguriert";
        }
    }

    [RelayCommand]
    private async Task DeleteCredentials()
    {
        if (_secureStorage != null)
        {
            try
            {
                await _secureStorage.DeleteCredentialsAsync();
            }
            catch (Exception ex)
            {
                // Lokaler State wird trotzdem zurückgesetzt
                System.Diagnostics.Debug.WriteLine($"Credentials löschen fehlgeschlagen: {ex.Message}");
            }
        }

        ApiKey = "";
        ApiSecret = "";
        HasCredentials = false;
        ConnectionStatus = "Gelöscht";
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        if (!HasCredentials)
        {
            ConnectionStatus = "Keine API-Keys konfiguriert";
            return;
        }

        IsConnecting = true;
        ConnectionStatus = "Verbinde...";

        try
        {
            if (_exchangeClient != null)
            {
                // Echten Verbindungstest: Account-Info abrufen
                var account = await _exchangeClient.GetAccountInfoAsync();
                ConnectionStatus = $"Verbunden (Balance: {account.Balance:F2} USDT)";
            }
            else
            {
                // Kein Exchange-Client verfügbar
                await Task.Delay(500);
                ConnectionStatus = "Exchange-Client nicht konfiguriert (Demo-Modus)";
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Verbindung fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }
}
