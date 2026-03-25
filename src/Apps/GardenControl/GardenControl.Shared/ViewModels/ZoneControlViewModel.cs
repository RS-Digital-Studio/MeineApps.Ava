using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GardenControl.Core.DTOs;
using GardenControl.Core.Enums;
using GardenControl.Shared.Services;

namespace GardenControl.Shared.ViewModels;

/// <summary>
/// Manuelle Steuerung - Ventile und Pumpe einzeln schalten mit konfigurierbarer Dauer.
/// </summary>
public partial class ZoneControlViewModel : ObservableObject
{
    private readonly IConnectionService _connection;

    [ObservableProperty] private bool _isPumpActive;
    [ObservableProperty] private int _selectedDurationSeconds = 30;
    [ObservableProperty] private int _customDurationSeconds = 30;

    public ObservableCollection<ZoneDisplayItem> Zones { get; } = [];

    public ZoneControlViewModel(IConnectionService connection)
    {
        _connection = connection;
        _connection.SystemStatusReceived += OnStatusReceived;
    }

    private void OnStatusReceived(SystemStatusDto status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsPumpActive = status.PumpActive;

            foreach (var dto in status.Zones)
            {
                var existing = Zones.FirstOrDefault(z => z.ZoneId == dto.ZoneId);
                if (existing != null)
                    existing.Update(dto);
                else
                    Zones.Add(new ZoneDisplayItem(dto));
            }
        });
    }

    [RelayCommand]
    private void SetDuration(string secondsStr)
    {
        if (int.TryParse(secondsStr, out var seconds))
        {
            SelectedDurationSeconds = seconds;
            CustomDurationSeconds = seconds;
        }
    }

    [RelayCommand]
    private async Task ToggleWatering(int zoneId)
    {
        var zone = Zones.FirstOrDefault(z => z.ZoneId == zoneId);
        if (zone == null) return;

        if (zone.IsWatering)
            await _connection.StopWateringAsync(zoneId);
        else
            await _connection.StartWateringAsync(zoneId, SelectedDurationSeconds);
    }

    [RelayCommand]
    private async Task EmergencyStop()
    {
        await _connection.EmergencyStopAsync();
    }

    /// <summary>
    /// Alle Zonen nacheinander für die gewählte Dauer bewässern.
    /// Nützlich zum Testen der gesamten Anlage.
    /// </summary>
    [RelayCommand]
    private async Task TestAllZones()
    {
        var testDuration = Math.Min(SelectedDurationSeconds, 15); // Max 15s pro Zone beim Test

        foreach (var zone in Zones.Where(z => z.IsEnabled))
        {
            await _connection.StartWateringAsync(zone.ZoneId, testDuration);
            await Task.Delay(TimeSpan.FromSeconds(testDuration + 2)); // Warten + 2s Pause
        }
    }

    partial void OnCustomDurationSecondsChanged(int value)
    {
        SelectedDurationSeconds = value;
    }
}
