using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GardenControl.Core.DTOs;
using GardenControl.Core.Enums;
using GardenControl.Shared.Services;

namespace GardenControl.Shared.ViewModels;

/// <summary>
/// Dashboard - Übersicht aller Zonen mit Live-Sensorwerten.
/// Zeigt Feuchtigkeitsbalken, Systemstatus und Schnellaktionen.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IConnectionService _connection;

    [ObservableProperty] private string _systemMode = "Manual";
    [ObservableProperty] private bool _isPumpActive;
    [ObservableProperty] private string _uptimeText = "--:--:--";
    [ObservableProperty] private string _serverTime = "--:--";

    // Wetter
    [ObservableProperty] private bool _hasWeatherData;
    [ObservableProperty] private string _weatherDescription = "";
    [ObservableProperty] private double _temperature;
    [ObservableProperty] private int _humidity;
    [ObservableProperty] private bool _weatherPaused;
    [ObservableProperty] private string _weatherPauseReason = "";

    public ObservableCollection<ZoneDisplayItem> Zones { get; } = [];

    public DashboardViewModel(IConnectionService connection)
    {
        _connection = connection;

        // Echtzeit-Updates empfangen (SignalR-Callbacks kommen auf Hintergrund-Thread)
        _connection.SystemStatusReceived += OnStatusReceived;
        _connection.SensorDataReceived += OnSensorDataReceived;
    }

    private void OnStatusReceived(SystemStatusDto status)
    {
        // SignalR-Callback kommt auf Hintergrund-Thread - UI-Zugriff nur über Dispatcher
        Dispatcher.UIThread.Post(() =>
        {
            SystemMode = status.Mode.ToString();
            IsPumpActive = status.PumpActive;
            UptimeText = $"{status.Uptime:dd\\.hh\\:mm\\:ss}";
            ServerTime = status.ServerTimeUtc.ToLocalTime().ToString("HH:mm:ss");

            // Wetter aktualisieren
            HasWeatherData = status.Weather != null;
            if (status.Weather != null)
            {
                WeatherDescription = status.Weather.Description;
                Temperature = status.Weather.TemperatureCelsius;
                Humidity = status.Weather.HumidityPercent;
            }
            WeatherPaused = status.WeatherPaused;
            WeatherPauseReason = status.WeatherPauseReason ?? "";

            // Zonen aktualisieren
            foreach (var zoneDto in status.Zones)
            {
                var existing = Zones.FirstOrDefault(z => z.ZoneId == zoneDto.ZoneId);
                if (existing != null)
                {
                    existing.Update(zoneDto);
                }
                else
                {
                    Zones.Add(new ZoneDisplayItem(zoneDto));
                }
            }
        });
    }

    private void OnSensorDataReceived(SensorDataDto data)
    {
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var value in data.Values)
            {
                var zone = Zones.FirstOrDefault(z => z.ZoneId == value.ZoneId);
                zone?.UpdateMoisture(value.MoisturePercent, value.RawValue);
            }
        });
    }

    [RelayCommand]
    private async Task EmergencyStop()
    {
        await _connection.EmergencyStopAsync();
    }

    [RelayCommand]
    private async Task SetMode(string mode)
    {
        await _connection.SetModeAsync(mode);
    }

    [RelayCommand]
    private async Task QuickWater(int zoneId)
    {
        await _connection.StartWateringAsync(zoneId);
    }

    [RelayCommand]
    private async Task StopWatering(int zoneId)
    {
        await _connection.StopWateringAsync(zoneId);
    }
}

/// <summary>
/// Display-Modell für eine Zone im Dashboard
/// </summary>
public partial class ZoneDisplayItem : ObservableObject
{
    [ObservableProperty] private int _zoneId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private double _moisturePercent;
    [ObservableProperty] private int _rawAdcValue;
    [ObservableProperty] private int _thresholdPercent;
    [ObservableProperty] private string _stateText = "Bereit";
    [ObservableProperty] private string _stateColor = "#4CAF50";
    [ObservableProperty] private bool _isWatering;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _lastWatered = "Nie";
    [ObservableProperty] private int? _remainingSeconds;
    [ObservableProperty] private string _moistureColor = "#4CAF50";

    public ZoneDisplayItem() { }

    public ZoneDisplayItem(ZoneStatusDto dto)
    {
        Update(dto);
    }

    public void Update(ZoneStatusDto dto)
    {
        ZoneId = dto.ZoneId;
        Name = dto.Name;
        MoisturePercent = dto.MoisturePercent;
        RawAdcValue = dto.RawAdcValue;
        ThresholdPercent = dto.ThresholdPercent;
        IsEnabled = dto.IsEnabled;
        IsWatering = dto.State == ZoneState.Watering;
        RemainingSeconds = dto.RemainingWateringSeconds;
        LastWatered = dto.LastWateredUtc?.ToLocalTime().ToString("dd.MM HH:mm") ?? "Nie";

        // Zustandstext und -farbe
        (StateText, StateColor) = dto.State switch
        {
            ZoneState.Watering => ($"Bewässert ({dto.RemainingWateringSeconds}s)", "#2196F3"),
            ZoneState.Cooldown => ("Abkühlphase", "#FF9800"),
            ZoneState.Error => ("Fehler", "#F44336"),
            _ => dto.SensorStatus == SensorStatus.Disconnected
                ? ("Sensor getrennt", "#9E9E9E")
                : ("Bereit", "#4CAF50")
        };

        UpdateMoistureColor();
    }

    public void UpdateMoisture(double percent, int rawValue)
    {
        MoisturePercent = percent;
        RawAdcValue = rawValue;
        UpdateMoistureColor();
    }

    private void UpdateMoistureColor()
    {
        MoistureColor = MoisturePercent switch
        {
            < 25 => "#F44336",  // Rot - kritisch trocken
            < 40 => "#FF9800",  // Orange - zu trocken
            < 70 => "#4CAF50",  // Grün - optimal
            _ => "#2196F3"      // Blau - sehr nass
        };
    }
}
