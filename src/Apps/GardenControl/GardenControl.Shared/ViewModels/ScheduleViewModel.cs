using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GardenControl.Core.DTOs;
using GardenControl.Core.Models;
using GardenControl.Shared.Services;

namespace GardenControl.Shared.ViewModels;

/// <summary>
/// Automatik-Konfiguration - Schwellenwerte, Zeitpläne und System-Einstellungen.
/// </summary>
public partial class ScheduleViewModel : ObservableObject
{
    private readonly IApiService _api;
    private readonly IConnectionService _connection;

    [ObservableProperty] private string _currentMode = "Manual";
    [ObservableProperty] private int _pollIntervalSeconds = 30;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasData;

    public ObservableCollection<ZoneConfigItem> ZoneConfigs { get; } = [];
    public ObservableCollection<ScheduleItem> Schedules { get; } = [];

    public ScheduleViewModel(IApiService api, IConnectionService connection)
    {
        _api = api;
        _connection = connection;
        _connection.SystemStatusReceived += status =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => CurrentMode = status.Mode.ToString());
    }

    [RelayCommand]
    private async Task LoadConfig()
    {
        var zones = await _api.GetZonesAsync();
        var config = await _api.GetConfigAsync();

        ZoneConfigs.Clear();
        foreach (var zone in zones)
            ZoneConfigs.Add(new ZoneConfigItem(zone));

        if (config.TryGetValue("poll_interval_seconds", out var interval))
            PollIntervalSeconds = int.TryParse(interval, out var i) ? i : 30;

        if (config.TryGetValue("system_mode", out var mode))
            CurrentMode = mode;

        // Zeitpläne laden
        await LoadSchedulesAsync();

        HasData = ZoneConfigs.Count > 0;
    }

    private async Task LoadSchedulesAsync()
    {
        Schedules.Clear();
        var zones = await _api.GetZonesAsync();
        var schedules = await _api.GetSchedulesAsync();
        foreach (var s in schedules)
        {
            var zoneName = zones.FirstOrDefault(z => z.Id == s.ZoneId)?.Name ?? $"Zone {s.ZoneId}";
            Schedules.Add(new ScheduleItem
            {
                Id = s.Id,
                ZoneId = s.ZoneId,
                ZoneName = zoneName,
                Hour = s.Hour,
                Minute = s.Minute,
                DurationSeconds = s.DurationSeconds,
                IsEnabled = s.IsEnabled,
                DaysDescription = s.DaysDescription
            });
        }
    }

    [RelayCommand]
    private async Task SaveZoneConfig(ZoneConfigItem item)
    {
        var config = new ZoneConfigDto
        {
            ZoneId = item.Id,
            Name = item.Name,
            ThresholdPercent = item.ThresholdPercent,
            WateringDurationSeconds = item.WateringDurationSeconds,
            CooldownSeconds = item.CooldownSeconds,
            IsEnabled = item.IsEnabled
        };

        var result = await _api.UpdateZoneAsync(config);
        StatusMessage = result != null ? $"{item.Name} gespeichert" : "Fehler beim Speichern";
    }

    [RelayCommand]
    private async Task SetMode(string mode)
    {
        await _connection.SetModeAsync(mode);
        CurrentMode = mode;
        StatusMessage = $"Modus auf '{mode}' gesetzt";
    }

    [RelayCommand]
    private async Task SavePollInterval()
    {
        await _api.UpdateConfigAsync(new SystemConfigDto { PollIntervalSeconds = PollIntervalSeconds });
        StatusMessage = $"Abfrageintervall auf {PollIntervalSeconds}s gesetzt";
    }
}

/// <summary>
/// Editierbares Zone-Konfig-Item für die UI
/// </summary>
public partial class ZoneConfigItem : ObservableObject
{
    public int Id { get; set; }
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private int _thresholdPercent = 40;
    [ObservableProperty] private int _wateringDurationSeconds = 30;
    [ObservableProperty] private int _cooldownSeconds = 300;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private int _sensorChannel;

    public ZoneConfigItem() { }

    public ZoneConfigItem(Zone zone)
    {
        Id = zone.Id;
        Name = zone.Name;
        ThresholdPercent = zone.ThresholdPercent;
        WateringDurationSeconds = zone.WateringDurationSeconds;
        CooldownSeconds = zone.CooldownSeconds;
        IsEnabled = zone.IsEnabled;
        SensorChannel = zone.SensorChannel;
    }
}

/// <summary>
/// Zeitplan-Item für die UI
/// </summary>
public partial class ScheduleItem : ObservableObject
{
    public int Id { get; set; }
    public int ZoneId { get; set; }
    [ObservableProperty] private string _zoneName = "";
    [ObservableProperty] private int _hour = 7;
    [ObservableProperty] private int _minute;
    [ObservableProperty] private int _durationSeconds = 60;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private string _daysDescription = "Mo-Fr";
}
