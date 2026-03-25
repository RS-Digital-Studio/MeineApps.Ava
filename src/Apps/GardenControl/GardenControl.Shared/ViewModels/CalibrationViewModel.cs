using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GardenControl.Core.DTOs;
using GardenControl.Shared.Services;

namespace GardenControl.Shared.ViewModels;

/// <summary>
/// Sensor-Kalibrierung - Trocken- und Nasswerte pro Zone setzen.
///
/// Anleitung:
/// 1. Sensor in trockene Erde stecken → "Trocken" kalibrieren
/// 2. Sensor in nasse Erde stecken → "Nass" kalibrieren
/// 3. Die Prozentwerte werden aus diesen Referenzwerten berechnet
/// </summary>
public partial class CalibrationViewModel : ObservableObject
{
    private readonly IApiService _api;
    private readonly IConnectionService _connection;

    [ObservableProperty] private string _statusMessage = string.Empty;

    public ObservableCollection<CalibrationItem> Items { get; } = [];

    public CalibrationViewModel(IApiService api, IConnectionService connection)
    {
        _api = api;
        _connection = connection;

        // Live-Werte anzeigen (SignalR-Callback kommt auf Hintergrund-Thread)
        _connection.SensorDataReceived += data =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var val in data.Values)
                {
                    var item = Items.FirstOrDefault(i => i.ZoneId == val.ZoneId);
                    if (item != null)
                    {
                        item.CurrentRawValue = val.RawValue;
                        item.CurrentMoisturePercent = val.MoisturePercent;
                    }
                }
            });
        };
    }

    [RelayCommand]
    private async Task LoadZones()
    {
        var zones = await _api.GetZonesAsync();
        Items.Clear();
        foreach (var zone in zones)
        {
            Items.Add(new CalibrationItem
            {
                ZoneId = zone.Id,
                Name = zone.Name,
                DryValue = zone.CalibrationDryValue,
                WetValue = zone.CalibrationWetValue,
                CalibratedAt = zone.CalibratedAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "Nie"
            });
        }
    }

    [RelayCommand]
    private async Task CalibrateDry(int zoneId)
    {
        StatusMessage = "Kalibriere Trocken-Wert...";
        var result = await _api.CalibrateAsync(zoneId, "dry");
        if (result != null)
        {
            var item = Items.FirstOrDefault(i => i.ZoneId == zoneId);
            if (item != null)
            {
                item.DryValue = result.CalibrationDryValue;
                item.CalibratedAt = result.CalibratedAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "Jetzt";
            }
            StatusMessage = $"Trocken-Wert für {result.Name}: {result.CalibrationDryValue}";
        }
        else
        {
            StatusMessage = "Kalibrierung fehlgeschlagen - Sensor erreichbar?";
        }
    }

    [RelayCommand]
    private async Task CalibrateWet(int zoneId)
    {
        StatusMessage = "Kalibriere Nass-Wert...";
        var result = await _api.CalibrateAsync(zoneId, "wet");
        if (result != null)
        {
            var item = Items.FirstOrDefault(i => i.ZoneId == zoneId);
            if (item != null)
            {
                item.WetValue = result.CalibrationWetValue;
                item.CalibratedAt = result.CalibratedAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "Jetzt";
            }
            StatusMessage = $"Nass-Wert für {result.Name}: {result.CalibrationWetValue}";
        }
        else
        {
            StatusMessage = "Kalibrierung fehlgeschlagen - Sensor erreichbar?";
        }
    }
}

public partial class CalibrationItem : ObservableObject
{
    public int ZoneId { get; set; }
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private int _dryValue;
    [ObservableProperty] private int _wetValue;
    [ObservableProperty] private int _currentRawValue;
    [ObservableProperty] private double _currentMoisturePercent;
    [ObservableProperty] private string _calibratedAt = "Nie";
}
