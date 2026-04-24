using System.Net;
using BingXBot.ClientApi.Connection;
using BingXBot.ClientApi.Http;
using BingXBot.ClientApi.Pairing;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Exchange;
using BingXBot.Trading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für allgemeine Einstellungen (API-Keys, Verbindung, Log-Level).
/// Nutzt ISecureStorageService für verschlüsselte API-Key-Speicherung
/// und IExchangeClient für Verbindungstests.
/// Publiziert Aktionen über den BotEventBus an die Log-Ansicht.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly BotSettings _botSettings;
    private readonly ISecureStorageService? _secureStorage;
    private readonly IExchangeClient? _exchangeClient;
    private readonly BotEventBus _eventBus;
    private readonly ISettingsPersistenceService _settingsPersistence;

    /// <summary>Event wenn sich der API-Key-Status ändert (true = vorhanden, false = gelöscht).</summary>
    public event EventHandler<bool>? ApiKeysAvailableChanged;

    [ObservableProperty] private string _apiKey = "";
    [ObservableProperty] private string _apiSecret = "";
    [ObservableProperty] private bool _hasCredentials;
    private bool _isMasked; // Verhindert Speichern maskierter Keys
    [ObservableProperty] private string _connectionStatus = "Nicht verbunden";
    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private string _selectedLogLevel = "Info";

    public string[] LogLevels => new[] { "Debug", "Info", "Trade", "Warning", "Error" };

    // ==== Remote-Pairing zum Pi-Server ====
    private readonly ServerConnection _serverConnection;
    private readonly PairingClient _pairingClient;
    private string? _pendingPairingId;

    // Default-URL: leer. Der User tippt seine Pi-URL selbst ein (z.B. http://raspberrypi.local:5050
    // oder http://192.168.178.28:5050 oder http://100.116.108.33:5050 via Tailscale).
    // Nach erfolgreichem Pairing wird die URL aus dem persistierten Profil übernommen.
    [ObservableProperty] private string _serverUrl = "";
    [ObservableProperty] private string _pairingCode = "";
    [ObservableProperty] private string _pairingStatus = "Nicht verbunden";
    [ObservableProperty] private bool _serverPaired;
    [ObservableProperty] private bool _pairingInProgress;
    [ObservableProperty] private string _pairedDeviceName = Environment.MachineName;

    // Re-Entrancy-Guard für CompletePairingAsync: Verhindert dass der Button-Click mehrfach
    // parallele HTTP-POSTs feuert (Server zählt jeden Call als Fehlversuch — >5 = Session-Kill).
    // Wird in der View als IsEnabled-Binding am "Code bestätigen"-Button genutzt.
    [ObservableProperty] private bool _isCompletingPairing;

    /// <summary>Verfuegbare Theme-Optionen fuer den Picker in der View.</summary>
    public static IReadOnlyList<ThemePreference> AvailableThemes { get; } =
        new[] { ThemePreference.Dark, ThemePreference.Light, ThemePreference.System };

    [ObservableProperty] private ThemePreference _theme = ThemePreference.Dark;

    partial void OnThemeChanged(ThemePreference value)
    {
        _botSettings.ThemePreference = value;
        App.ApplyTheme(value);
        _ = _settingsPersistence.SaveAllAsync();
    }

    private readonly IAccountService? _accountService;

    public SettingsViewModel(
        BotSettings botSettings,
        BotEventBus eventBus,
        ServerConnection serverConnection,
        PairingClient pairingClient,
        ISettingsPersistenceService settingsPersistence,
        ISecureStorageService? secureStorage = null,
        IExchangeClient? exchangeClient = null,
        IAccountService? accountService = null)
    {
        _botSettings = botSettings;
        _eventBus = eventBus;
        _secureStorage = secureStorage;
        _exchangeClient = exchangeClient;
        _serverConnection = serverConnection;
        _pairingClient = pairingClient;
        _accountService = accountService;
        _settingsPersistence = settingsPersistence;
        _theme = botSettings.ThemePreference;

        // Gespeicherte Credentials laden
        _ = LoadCredentialsAsync();

        // Persisted Server-Profile pruefen
        UpdateServerStatus();
        _serverConnection.Changed += _ => Avalonia.Threading.Dispatcher.UIThread.Post(UpdateServerStatus);
    }

    private void UpdateServerStatus()
    {
        var profile = _serverConnection.Profile;
        ServerPaired = profile != null;
        if (profile != null)
        {
            ServerUrl = profile.BaseUrl;
            PairedDeviceName = profile.DeviceName;
            PairingStatus = $"Verbunden (gepairt {profile.PairedAtUtc:yyyy-MM-dd})";
        }
        else
        {
            PairingStatus = "Nicht verbunden";
        }
    }

    /// <summary>Schritt 1: Ruft /pair/init auf. Server zeigt dann den Code in seinem Terminal/Log an.</summary>
    [RelayCommand]
    private async Task InitiatePairingAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            PairingStatus = "Server-URL fehlt";
            return;
        }

        // Reset vor jedem Init — verhindert Stuck-State wenn Ping/Init scheitert
        // (PairingInProgress darf nur true sein wenn _pendingPairingId gesetzt ist).
        _pendingPairingId = null;
        PairingInProgress = false;
        PairingCode = "";

        PairingStatus = "Pruefe Server-Erreichbarkeit...";

        // Progressives Feedback bei Tailscale-Cold-Start (3-8 s Handshake) damit User nicht denkt
        // das UI haengt. Timer wird bei jedem erfolgreichen Schritt abgebrochen.
        using var progressCts = new CancellationTokenSource();
        var progressTask = RunPairingProgressHintAsync(
            afterSeconds: 5,
            hint: "Server antwortet langsam — Tailscale braucht bei Cold-Start oft 5-10 s, bitte warten...",
            ct: progressCts.Token);

        try
        {
            if (!await _pairingClient.PingHealthAsync(ServerUrl))
            {
                progressCts.Cancel();
                PairingStatus = "Server nicht erreichbar (Health-Check fehlgeschlagen). Server-URL pruefen (z.B. http://raspberrypi.local:5050 oder http://192.168.178.28:5050).";
                return;
            }

            var response = await _pairingClient.InitiateAsync(ServerUrl, PairedDeviceName);
            progressCts.Cancel();
            _pendingPairingId = response.PairingId;
            PairingInProgress = true;  // Erst nach erfolgreichem Init — jetzt ist Schritt 2 (Code-Eingabe) sinnvoll
            PairingStatus = $"Code vom Pi ablesen (gueltig {response.ExpiresInSeconds}s) und unten eingeben";
        }
        catch (Exception ex)
        {
            progressCts.Cancel();
            // Sauber zurueck in Schritt 1 — Buttons verhalten sich sonst wie "stuck" (CompleteButton sichtbar ohne PairingId).
            _pendingPairingId = null;
            PairingInProgress = false;
            PairingStatus = $"Fehler: {ex.Message}";
        }
        finally
        {
            // Task-Finalisierung (bei erfolgreichem Cancel wirft die Task OperationCanceledException — schlucken)
            try { await progressTask; } catch (OperationCanceledException) { }
        }
    }

    /// <summary>
    /// Zeigt nach <paramref name="afterSeconds"/> einen zusaetzlichen Hint im PairingStatus-Text,
    /// wenn der laufende Pairing-Call noch nicht abgeschlossen ist. Wird durch Cancellation abgebrochen.
    /// </summary>
    private async Task RunPairingProgressHintAsync(int afterSeconds, string hint, CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(afterSeconds), ct).ConfigureAwait(false);
            if (!ct.IsCancellationRequested)
                PairingStatus = hint;
        }
        catch (OperationCanceledException) { /* normal beim erfolgreichen Pairing */ }
    }

    /// <summary>
    /// Schritt 2: User hat den 6-stelligen Code vom Pi abgelesen und eingegeben.
    /// Re-Entrancy-Guard via <see cref="IsCompletingPairing"/> verhindert, dass Button-Double-Clicks
    /// mehrere parallele POSTs feuern (Server zählt jeden Call als Fehlversuch, >5 = Session-Kill).
    /// </summary>
    [RelayCommand]
    private async Task CompletePairingAsync()
    {
        // Re-Entrancy-Schutz: Wenn der Call bereits läuft, jeden weiteren Klick ignorieren.
        if (IsCompletingPairing) return;

        if (_pendingPairingId == null)
        {
            PairingStatus = "Bitte erst 'Pairing starten' klicken";
            return;
        }
        if (string.IsNullOrWhiteSpace(PairingCode))
        {
            PairingStatus = "Code fehlt";
            return;
        }

        IsCompletingPairing = true;
        try
        {
            await _pairingClient.CompleteAsync(ServerUrl, _pendingPairingId, PairingCode.Trim(), PairedDeviceName);
            _pendingPairingId = null;
            PairingCode = "";
            PairingInProgress = false;  // Erfolg: Schritt-2-Panel ausblenden
            PairingStatus = "Erfolgreich gepairt! App-Neustart startet den Remote-Modus.";
            _botSettings.UseRemoteMode = true;
            _botSettings.ServerUrl = ServerUrl;
            _ = _settingsPersistence.SaveAllAsync();
        }
        catch (ApiException api) when (api.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Server liefert seit v1.3.0 spezifische ErrorCodes — wir unterscheiden:
            //   invalid_code       → Tippfehler, Session lebt, User tippt einfach neu
            //   pairing_exhausted  → >5 Fehlversuche, Session ist serverseitig weg
            //   pairing_expired    → Code-Lifetime (5 min) abgelaufen
            //   pairing_unknown    → PairingId nicht bekannt (Server-Neustart o.ä.)
            // Die letzten drei erfordern zwingend Zurück in Schritt 1, sonst läuft der Client in eine
            // Endlosschleife aus 401 "Code falsch"-Meldungen.
            switch (api.ErrorCode)
            {
                case "invalid_code":
                    // Session bleibt offen — User tippt neu. Code-Feld leeren ist Feedback dass "jetzt
                    // neu tippen" der richtige Schritt ist.
                    PairingCode = "";
                    PairingStatus = "Code falsch. Bitte erneut eingeben.";
                    break;

                case "pairing_exhausted":
                case "pairing_expired":
                case "pairing_unknown":
                    // Session tot — zurück in Schritt 1.
                    _pendingPairingId = null;
                    PairingCode = "";
                    PairingInProgress = false;
                    PairingStatus = api.ErrorCode switch
                    {
                        "pairing_exhausted" => "Zu viele Fehlversuche. Bitte 'Pairing starten' erneut klicken.",
                        "pairing_expired"   => "Pairing-Code abgelaufen. Bitte 'Pairing starten' erneut klicken.",
                        _                   => "Pairing-Sitzung unbekannt (Server neu gestartet?). Bitte 'Pairing starten' erneut klicken."
                    };
                    break;

                default:
                    // Unerwarteter ErrorCode vom Server (z.B. alte Server-Version vor v1.3.0).
                    // Fallback-Verhalten wie früher: als Code-Fehler behandeln, Session stehen lassen.
                    PairingCode = "";
                    PairingStatus = $"Pairing fehlgeschlagen: {api.Message}";
                    break;
            }
        }
        catch (ApiException api)
        {
            // Andere HTTP-Fehler (z.B. 429 Rate-Limit, 500 Server-Fehler). Session bleibt offen,
            // da der Fehler nicht den Pairing-State betrifft.
            PairingStatus = api.StatusCode == (HttpStatusCode)429
                ? "Server-Rate-Limit erreicht. Bitte kurz warten und erneut versuchen."
                : $"Pairing fehlgeschlagen ({(int)api.StatusCode}): {api.Message}";
        }
        catch (Exception ex)
        {
            // Netz/Timeout/etc — PairingId bleibt stehen damit User retryen kann sobald Verbindung wieder da ist.
            PairingStatus = $"Pairing fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsCompletingPairing = false;
            UpdateServerStatus();
        }
    }

    /// <summary>Bricht den laufenden Pairing-Vorgang ab (falls Code verlegt oder Fehler).</summary>
    [RelayCommand]
    private async Task CancelPairingAsync()
    {
        if (_pendingPairingId == null) return;
        await _pairingClient.CancelAsync(ServerUrl, _pendingPairingId);
        _pendingPairingId = null;
        PairingCode = "";
        PairingInProgress = false;
        PairingStatus = "Pairing abgebrochen";
    }

    /// <summary>Trennt die Server-Verbindung — App faellt nach Neustart in den Standalone-Modus.</summary>
    [RelayCommand]
    private void DisconnectServer()
    {
        _serverConnection.Clear();
        _botSettings.UseRemoteMode = false;
        _botSettings.ServerUrl = null;
        _ = _settingsPersistence.SaveAllAsync();
        PairingStatus = "Getrennt — Neustart fuer Standalone-Modus erforderlich";
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
            System.Diagnostics.Debug.WriteLine($"Credentials laden fehlgeschlagen: {ex.Message}");
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

        // Unmaskierte Keys vor dem Maskieren merken (für Remote-Upload)
        var rawApiKey = ApiKey;
        var rawApiSecret = ApiSecret;

        if (_secureStorage != null)
        {
            try
            {
                await _secureStorage.SaveCredentialsAsync(rawApiKey, rawApiSecret);
                HasCredentials = true;
                ConnectionStatus = "Gespeichert";

                // Keys maskieren nach dem Speichern
                ApiKey = MaskKey(rawApiKey);
                ApiSecret = MaskKey(rawApiSecret);
                _isMasked = true;

                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                    "API-Credentials gespeichert"));

                ApiKeysAvailableChanged?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Speichern fehlgeschlagen: {ex.Message}";
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                    $"API-Credentials speichern fehlgeschlagen: {ex.Message}"));
            }
        }

        // Im Remote-Mode: Credentials zum Pi übertragen (wird dort AES-256 verschlüsselt gespeichert).
        // Ohne diesen Upload hat der Pi keine Keys und kann keine Live-Orders senden.
        // ServerConnection.IsPaired ist robuster als BotSettings.UseRemoteMode (kein Settings-Timing-Problem).
        if (_serverConnection.IsPaired && _accountService != null
            && !string.IsNullOrWhiteSpace(rawApiKey) && !string.IsNullOrWhiteSpace(rawApiSecret))
        {
            try
            {
                await _accountService.SetCredentialsAsync(new SetCredentialsRequest(rawApiKey, rawApiSecret));
                ConnectionStatus = "Gespeichert + zum Pi übertragen";
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                    "API-Credentials erfolgreich zum Pi-Server übertragen"));
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Lokal gespeichert, Upload zum Pi fehlgeschlagen: {ex.Message}";
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                    $"Credentials-Upload zum Pi fehlgeschlagen: {ex.Message}"));
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

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            "API-Credentials gelöscht"));

        ApiKeysAvailableChanged?.Invoke(this, false);
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

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            "Teste BingX-Verbindung..."));

        try
        {
            // Temporären BingXRestClient mit gespeicherten Keys erstellen und testen
            if (_secureStorage != null)
            {
                var creds = await _secureStorage.LoadCredentialsAsync();
                if (creds == null)
                {
                    ConnectionStatus = "API-Keys konnten nicht entschlüsselt werden";
                    return;
                }

                using var httpClient = new HttpClient();
                // 24.04.2026 Phase-4-Audit m5: RateLimiter ist IDisposable und haelt SemaphoreSlim
                // mit OS-Handle pro Kategorie — ohne `using` leakte jeder Connect-Test diese Handles.
                using var rateLimiter = new RateLimiter();
                var testClient = new BingXRestClient(creds.Value.ApiKey, creds.Value.ApiSecret,
                    httpClient, rateLimiter, NullLogger<BingXRestClient>.Instance);
                var account = await testClient.GetAccountInfoAsync();
                ConnectionStatus = $"Verbunden (Balance: {account.Balance:F2} USDT)";

                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                    $"BingX-Verbindung erfolgreich. Balance: {account.Balance:F2} USDT"));
            }
            else
            {
                ConnectionStatus = "Secure Storage nicht verfügbar";
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Verbindung fehlgeschlagen: {ex.Message}";

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                $"BingX-Verbindung fehlgeschlagen: {ex.Message}"));
        }
        finally
        {
            IsConnecting = false;
        }
    }
}
