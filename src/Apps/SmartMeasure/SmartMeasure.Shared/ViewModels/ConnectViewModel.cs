using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>BLE-Verbindung + NTRIP-Konfiguration</summary>
public partial class ConnectViewModel : ObservableObject
{
    private readonly IBleService _bleService;

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
            FixStatusText = q switch
            {
                4 => "RTK FIX",
                5 => "RTK FLOAT",
                2 => "DGPS",
                1 => "GPS",
                _ => "KEIN FIX"
            };
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
        catch (Exception)
        {
            ConnectionStatus = "Verbindung fehlgeschlagen";
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _bleService.DisconnectAsync();
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

        await _bleService.ConfigureNtripAsync(config);
    }

    [RelayCommand]
    private async Task SendWiFiConfigAsync()
    {
        if (string.IsNullOrWhiteSpace(WifiSsid)) return;
        await _bleService.ConfigureWiFiAsync(WifiSsid, WifiPassword);
    }

    [RelayCommand]
    private async Task UpdateStabHeightAsync()
    {
        await _bleService.SetStabHeightAsync(StabHeight);
    }

    [RelayCommand]
    private async Task CalibrateImuAsync()
    {
        await _bleService.CalibrateImuAsync();
    }
}
