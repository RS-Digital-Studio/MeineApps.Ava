using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>BLE-Verbindung + NTRIP-Konfiguration. Fehler werden über StatusChanged sichtbar.</summary>
public partial class ConnectViewModel : ViewModelBase
{
    private readonly IBleService _bleService;

    /// <summary>Fehler/Statusmeldung die als Toast angezeigt werden soll.</summary>
    public event Action<string>? MessageRequested;

    // BLE
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _connectionStatus = "Nicht verbunden";
    public ObservableCollection<BleDevice> FoundDevices { get; } = [];

    // NTRIP
    [ObservableProperty] private string _ntripServer = string.Empty;
    [ObservableProperty] private int _ntripPort = 2101;
    [ObservableProperty] private string _ntripMountpoint = string.Empty;
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

    public ConnectViewModel(IBleService bleService)
    {
        _bleService = bleService;

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

    [RelayCommand]
    private async Task SendNtripConfigAsync()
    {
        var config = new NtripConfig
        {
            Server = NtripServer,
            Port = NtripPort,
            Mountpoint = NtripMountpoint,
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
