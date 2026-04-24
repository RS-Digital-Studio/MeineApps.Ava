using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>BLE-Verbindung + NTRIP-Konfiguration. Fehler werden über StatusChanged sichtbar.
///
/// Persistenz: Alle NTRIP/WiFi/Stab-Felder werden via <see cref="IPreferencesService"/>
/// gespeichert. Neustart-Safe — Credentials und Server-Config überleben App-Reinstall nicht,
/// aber App-Neustart schon.
///
/// Validation: <see cref="SendNtripConfigAsync"/> prüft Port ∈ [1, 65535] und verbietet
/// Doppelpunkte in Mountpoint — sonst zerstört er das ESP32-Protokoll
/// `NTRIP:server:port/mountpoint:user:pass` (Format-String).</summary>
public partial class ConnectViewModel : ViewModelBase
{
    private readonly IBleService _bleService;
    private readonly IPreferencesService _preferences;

    /// <summary>Fehler/Statusmeldung die als Toast angezeigt werden soll.</summary>
    public event Action<string>? MessageRequested;

    // Preferences-Keys
    private const string KeyNtripServer = "ntrip.server";
    private const string KeyNtripPort = "ntrip.port";
    private const string KeyNtripMountpoint = "ntrip.mountpoint";
    private const string KeyNtripUser = "ntrip.user";
    private const string KeyNtripPassword = "ntrip.password";
    private const string KeyIsOwnBase = "ntrip.isownbase";
    private const string KeyWifiSsid = "wifi.ssid";
    private const string KeyWifiPassword = "wifi.password";
    private const string KeyStabHeight = "stab.height";

    private bool _isLoadingFromPrefs;

    // BLE
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _connectionStatus = "Nicht verbunden";
    public ObservableCollection<BleDevice> FoundDevices { get; } = [];

    // NTRIP
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendNtripConfigCommand))]
    private string _ntripServer = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendNtripConfigCommand))]
    private int _ntripPort = 2101;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendNtripConfigCommand))]
    private string _ntripMountpoint = string.Empty;

    [ObservableProperty] private string _ntripUser = string.Empty;
    [ObservableProperty] private string _ntripPassword = string.Empty;
    [ObservableProperty] private bool _isOwnBase = true;
    [ObservableProperty] private int _ntripStatus;

    // WiFi
    [ObservableProperty] private string _wifiSsid = string.Empty;
    [ObservableProperty] private string _wifiPassword = string.Empty;

    // Stab
    [ObservableProperty] private float _stabHeight = 1.5f;

    // Fix
    [ObservableProperty] private int _fixQuality;
    [ObservableProperty] private string _fixStatusText = "KEIN FIX";

    public ConnectViewModel(IBleService bleService, IPreferencesService preferences)
    {
        _bleService = bleService;
        _preferences = preferences;

        LoadPreferences();

        // BLE-Events kommen vom Background-Thread, daher Dispatcher
        _bleService.StateChanged += state => Dispatcher.UIThread.Post(() =>
        {
            IsConnected = state.IsConnected;
            ConnectionStatus = state.IsConnected ? "Verbunden" : "Nicht verbunden";
            NtripStatus = state.NtripStatus;
        });

        _bleService.FixQualityChanged += q => Dispatcher.UIThread.Post(() =>
        {
            FixQuality = q;
            FixStatusText = _bleService.CurrentState.FixStatusText;
        });
    }

    /// <summary>Prefs beim Start laden — ohne partial-Setter auszulösen (sonst Endlos-Schleife).</summary>
    private void LoadPreferences()
    {
        _isLoadingFromPrefs = true;
        try
        {
            NtripServer = _preferences.Get(KeyNtripServer, string.Empty);
            NtripPort = _preferences.Get(KeyNtripPort, 2101);
            NtripMountpoint = _preferences.Get(KeyNtripMountpoint, string.Empty);
            NtripUser = _preferences.Get(KeyNtripUser, string.Empty);
            NtripPassword = _preferences.Get(KeyNtripPassword, string.Empty);
            IsOwnBase = _preferences.Get(KeyIsOwnBase, true);
            WifiSsid = _preferences.Get(KeyWifiSsid, string.Empty);
            WifiPassword = _preferences.Get(KeyWifiPassword, string.Empty);
            StabHeight = _preferences.Get(KeyStabHeight, 1.5f);
        }
        finally
        {
            _isLoadingFromPrefs = false;
        }
    }

    partial void OnNtripServerChanged(string value)
    {
        if (!_isLoadingFromPrefs) _preferences.Set(KeyNtripServer, value);
    }

    partial void OnNtripPortChanged(int value)
    {
        if (!_isLoadingFromPrefs) _preferences.Set(KeyNtripPort, value);
    }

    partial void OnNtripMountpointChanged(string value)
    {
        if (!_isLoadingFromPrefs) _preferences.Set(KeyNtripMountpoint, value);
    }

    partial void OnNtripUserChanged(string value)
    {
        if (!_isLoadingFromPrefs) _preferences.Set(KeyNtripUser, value);
    }

    partial void OnNtripPasswordChanged(string value)
    {
        if (!_isLoadingFromPrefs) _preferences.Set(KeyNtripPassword, value);
    }

    partial void OnIsOwnBaseChanged(bool value)
    {
        if (!_isLoadingFromPrefs) _preferences.Set(KeyIsOwnBase, value);
    }

    partial void OnWifiSsidChanged(string value)
    {
        if (!_isLoadingFromPrefs) _preferences.Set(KeyWifiSsid, value);
    }

    partial void OnWifiPasswordChanged(string value)
    {
        if (!_isLoadingFromPrefs) _preferences.Set(KeyWifiPassword, value);
    }

    partial void OnStabHeightChanged(float value)
    {
        if (!_isLoadingFromPrefs) _preferences.Set(KeyStabHeight, value);
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsScanning = true;
        FoundDevices.Clear();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var devices = await _bleService.ScanAsync(cts.Token);
            foreach (var d in devices)
                FoundDevices.Add(d);

            if (devices.Count == 0)
                MessageRequested?.Invoke("Keine Geräte gefunden — Bluetooth aktiv und Stab in Reichweite?");
        }
        catch (Exception ex)
        {
            // Bluetooth off, Permission denied, etc. — User soll einen Hinweis sehen
            ConnectionStatus = "Scan fehlgeschlagen";
            MessageRequested?.Invoke($"BLE-Scan fehlgeschlagen: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task ConnectToDeviceAsync(BleDevice? device)
    {
        if (device == null) return;

        ConnectionStatus = "Verbinde...";
        try
        {
            await _bleService.ConnectAsync(device);
            await _bleService.SetStabHeightAsync(StabHeight);
        }
        catch (Exception ex)
        {
            ConnectionStatus = "Verbindung fehlgeschlagen";
            MessageRequested?.Invoke($"Verbindung zu {device.Name} fehlgeschlagen: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        try
        {
            await _bleService.DisconnectAsync();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke($"Trennen fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>Validation für NTRIP-Command: verhindert dass Müll ans ESP32 geht
    /// (leerer Server, Port außerhalb Range, Mountpoint mit ':' was das Protokoll zerstören würde).</summary>
    private bool CanSendNtripConfig()
    {
        if (string.IsNullOrWhiteSpace(NtripServer)) return false;
        if (NtripPort < 1 || NtripPort > 65535) return false;
        if (NtripMountpoint.Contains(':')) return false;
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanSendNtripConfig))]
    private async Task SendNtripConfigAsync()
    {
        // Explizite User-feedback wenn doch noch invalide Werte durchrutschen
        // (z.B. direktes Command-Invoke statt Button-Click)
        if (!CanSendNtripConfig())
        {
            MessageRequested?.Invoke("NTRIP-Konfig ungültig: Server leer, Port außerhalb 1-65535, oder ':' im Mountpoint");
            return;
        }

        var config = new NtripConfig
        {
            Server = NtripServer.Trim(),
            Port = NtripPort,
            Mountpoint = NtripMountpoint.Trim(),
            Username = NtripUser,
            Password = NtripPassword,
            IsOwnBase = IsOwnBase
        };

        try
        {
            await _bleService.ConfigureNtripAsync(config);
            MessageRequested?.Invoke("NTRIP-Konfiguration gesendet");
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke($"NTRIP-Konfig fehlgeschlagen: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SendWiFiConfigAsync()
    {
        if (string.IsNullOrWhiteSpace(WifiSsid)) return;
        try
        {
            await _bleService.ConfigureWiFiAsync(WifiSsid, WifiPassword);
            MessageRequested?.Invoke("WLAN-Konfiguration gesendet");
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke($"WLAN-Konfig fehlgeschlagen: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task UpdateStabHeightAsync()
    {
        try
        {
            await _bleService.SetStabHeightAsync(StabHeight);
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke($"Stabhöhe-Update fehlgeschlagen: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CalibrateImuAsync()
    {
        try
        {
            await _bleService.CalibrateImuAsync();
            MessageRequested?.Invoke("Kalibrierung gestartet — Stab langsam 3x im Kreis drehen");
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke($"IMU-Kalibrierung fehlgeschlagen: {ex.Message}");
        }
    }
}
