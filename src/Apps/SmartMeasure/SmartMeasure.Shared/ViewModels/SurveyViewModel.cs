using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>Live-Vermessung: Position, Punkt setzen, Labels</summary>
public partial class SurveyViewModel : ObservableObject
{
    private readonly IBleService _bleService;
    private readonly IMeasurementService _measurementService;

    // Live-Position
    [ObservableProperty] private double _latitude;
    [ObservableProperty] private double _longitude;
    [ObservableProperty] private double _altitude;
    [ObservableProperty] private string _latText = "—";
    [ObservableProperty] private string _lonText = "—";
    [ObservableProperty] private string _altText = "—";

    // Fix-Status
    [ObservableProperty] private int _fixQuality;
    [ObservableProperty] private string _fixStatusText = "KEIN FIX";
    [ObservableProperty] private float _horizontalAccuracy;
    [ObservableProperty] private float _verticalAccuracy;
    [ObservableProperty] private int _satelliteCount;
    [ObservableProperty] private float _tiltAngle;

    // Punkt-Label
    [ObservableProperty] private string _pointLabel = string.Empty;

    // Letzte Punkte
    public ObservableCollection<SurveyPointDisplay> RecentPoints { get; } = [];

    // Abstand zum letzten Punkt
    [ObservableProperty] private string _distanceToLast = "—";

    public SurveyViewModel(IBleService bleService, IMeasurementService measurementService)
    {
        _bleService = bleService;
        _measurementService = measurementService;

        // BLE-Events kommen vom Background-Thread, daher Dispatcher
        _bleService.PositionUpdated += (lat, lon, alt) => Dispatcher.UIThread.Post(() =>
        {
            Latitude = lat;
            Longitude = lon;
            Altitude = alt;
            LatText = lat.ToString("F8");
            LonText = lon.ToString("F8");
            AltText = $"{alt:F2} m";
        });

        _bleService.FixQualityChanged += q => Dispatcher.UIThread.Post(() =>
        {
            FixQuality = q;
            FixStatusText = q switch
            {
                4 => "RTK FIX",
                5 => "FLOAT",
                2 => "DGPS",
                1 => "GPS",
                _ => "KEIN FIX"
            };
        });

        _bleService.AccuracyUpdated += (h, v) => Dispatcher.UIThread.Post(() =>
        {
            HorizontalAccuracy = h;
            VerticalAccuracy = v;
        });

        _bleService.StateChanged += state => Dispatcher.UIThread.Post(() =>
        {
            SatelliteCount = state.SatelliteCount;
            TiltAngle = state.TiltAngle;
        });

        // Punkte vom Stab empfangen (auch auf UI-Thread fuer ObservableCollection)
        _bleService.PointReceived += p => Dispatcher.UIThread.Post(() => OnPointReceived(p));
    }

    /// <summary>Punkt manuell setzen (per App-Button, nicht per Stab-Knopf)</summary>
    [RelayCommand]
    private void SetPoint()
    {
        if (_bleService is MockBleService mock)
        {
            // Im Mock-Modus: Punkt simulieren
            mock.SimulatePointTrigger();
        }
        // Bei echtem BLE: Stab sendet den Punkt per Hardware-Knopf
    }

    /// <summary>Schnell-Label setzen</summary>
    [RelayCommand]
    private void SetQuickLabel(string label)
    {
        PointLabel = label;
    }

    /// <summary>Alle Punkte loeschen</summary>
    [RelayCommand]
    private void ClearPoints()
    {
        _measurementService.ClearPoints();
        RecentPoints.Clear();
        DistanceToLast = "—";
    }

    private void OnPointReceived(SurveyPoint point)
    {
        // Label zuweisen wenn gesetzt
        if (!string.IsNullOrWhiteSpace(PointLabel))
        {
            point.Label = PointLabel;
            PointLabel = string.Empty; // Label nach Verwendung zuruecksetzen
        }

        _measurementService.AddPoint(point);

        // Abstand zum vorherigen Punkt
        var points = _measurementService.CurrentPoints;
        if (points.Count >= 2)
        {
            var dist = _measurementService.CalculateDistance2D(points[^2], points[^1]);
            DistanceToLast = $"{dist:F2} m";
        }

        // In die Anzeige-Liste
        RecentPoints.Insert(0, new SurveyPointDisplay
        {
            Number = points.Count,
            Label = point.Label ?? $"P{points.Count}",
            Altitude = $"{point.Altitude:F2} m",
            Accuracy = $"±{point.HorizontalAccuracy:F1} cm",
            Distance = DistanceToLast,
            FixStatus = point.FixQuality == 4 ? "FIX" : "FLOAT"
        });
    }
}

/// <summary>Anzeige-Objekt fuer die Punkte-Liste</summary>
public class SurveyPointDisplay
{
    public int Number { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Altitude { get; set; } = string.Empty;
    public string Accuracy { get; set; } = string.Empty;
    public string Distance { get; set; } = string.Empty;
    public string FixStatus { get; set; } = string.Empty;
}
