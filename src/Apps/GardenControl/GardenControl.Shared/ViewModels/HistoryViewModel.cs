using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GardenControl.Core.Models;
using GardenControl.Shared.Controls;
using GardenControl.Shared.Services;

namespace GardenControl.Shared.ViewModels;

/// <summary>
/// Verlaufsdaten - Feuchtigkeitsverlauf-Chart, Bewässerungsereignisse und Statistiken.
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly IApiService _api;
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    [ObservableProperty] private string _selectedPeriod = "24h";
    [ObservableProperty] private int? _selectedZoneId;
    [ObservableProperty] private bool _isLoading;

    // Chart-Daten (für MoistureChartControl)
    [ObservableProperty] private List<ChartDataPoint>? _chartDataPoints;
    [ObservableProperty] private int _chartThreshold = 40;
    [ObservableProperty] private string _chartTitle = "Feuchtigkeitsverlauf (24h)";

    // Statistiken
    [ObservableProperty] private int _totalWateringCount;
    [ObservableProperty] private int _totalWateringDurationSeconds;
    [ObservableProperty] private double _avgMoistureAtStart;
    [ObservableProperty] private int _readingCount;

    public ObservableCollection<SensorReading> Readings { get; } = [];
    public ObservableCollection<IrrigationEvent> Events { get; } = [];

    public HistoryViewModel(IApiService api)
    {
        _api = api;
    }

    [RelayCommand]
    private async Task LoadData()
    {
        // Debouncing: Nur ein Request gleichzeitig
        if (!await _loadSemaphore.WaitAsync(0)) return;

        try
        {
            IsLoading = true;

            var (from, to) = GetTimeRange();

            // Messwerte laden
            var readings = await _api.GetReadingsAsync(SelectedZoneId, from, to);
            Readings.Clear();
            foreach (var r in readings)
                Readings.Add(r);

            // Chart-Daten aufbereiten
            ChartDataPoints = readings
                .OrderBy(r => r.TimestampUtc)
                .Select(r => new ChartDataPoint
                {
                    TimestampUtc = r.TimestampUtc,
                    MoisturePercent = r.MoisturePercent,
                    WasWatering = r.WasWatering
                })
                .ToList();

            ChartTitle = $"Feuchtigkeitsverlauf ({SelectedPeriod})" +
                         (SelectedZoneId.HasValue ? $" - Zone {SelectedZoneId}" : "");

            // Bewässerungsereignisse laden
            var events = await _api.GetEventsAsync(SelectedZoneId, from, to);
            Events.Clear();
            foreach (var e in events)
                Events.Add(e);

            // Statistiken berechnen
            TotalWateringCount = events.Count;
            TotalWateringDurationSeconds = events.Sum(e => e.ActualDurationSeconds);
            AvgMoistureAtStart = events.Count > 0 ? Math.Round(events.Average(e => e.MoistureAtStart), 1) : 0;
            ReadingCount = readings.Count;

            // Schwellenwert für Chart (von erster Zone)
            var zones = await _api.GetZonesAsync();
            if (SelectedZoneId.HasValue)
            {
                var zone = zones.FirstOrDefault(z => z.Id == SelectedZoneId);
                if (zone != null) ChartThreshold = zone.ThresholdPercent;
            }
            else if (zones.Count > 0)
            {
                ChartThreshold = zones[0].ThresholdPercent;
            }
        }
        finally
        {
            IsLoading = false;
            _loadSemaphore.Release();
        }
    }

    [RelayCommand]
    private void SetPeriod(string period)
    {
        SelectedPeriod = period;
        _ = LoadData();
    }

    [RelayCommand]
    private void FilterZone(string zoneIdStr)
    {
        SelectedZoneId = int.TryParse(zoneIdStr, out var id) ? id : null;
        _ = LoadData();
    }

    /// <summary>Formatierte Bewässerungsdauer (z.B. "4 Min 30 Sek")</summary>
    public string TotalWateringDurationFormatted => TotalWateringDurationSeconds switch
    {
        < 60 => $"{TotalWateringDurationSeconds} Sek",
        < 3600 => $"{TotalWateringDurationSeconds / 60} Min {TotalWateringDurationSeconds % 60} Sek",
        _ => $"{TotalWateringDurationSeconds / 3600} Std {(TotalWateringDurationSeconds % 3600) / 60} Min"
    };

    partial void OnTotalWateringDurationSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(TotalWateringDurationFormatted));
    }

    private (DateTime from, DateTime to) GetTimeRange()
    {
        var to = DateTime.UtcNow;
        var from = SelectedPeriod switch
        {
            "1h" => to.AddHours(-1),
            "6h" => to.AddHours(-6),
            "24h" => to.AddHours(-24),
            "7d" => to.AddDays(-7),
            "30d" => to.AddDays(-30),
            _ => to.AddHours(-24)
        };
        return (from, to);
    }
}
