using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>Live-Vermessung: Position, Punkt setzen, Labels, AR-Capture</summary>
public partial class SurveyViewModel : ViewModelBase
{
    private readonly IBleService _bleService;
    private readonly IMeasurementService _measurementService;
    private readonly IArCaptureService _arCaptureService;

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

    // Live-Kompass Renderer
    public Graphics.SurveyLiveRenderer CompassRenderer { get; } = new();

    // AR-Capture
    [ObservableProperty] private bool _isArAvailable;
    [ObservableProperty] private string _arStatusText = string.Empty;

    /// <summary>True wenn MockBleService aktiv ist (Desktop-Entwicklung).
    /// Im echten Betrieb drückt der User den Hardware-Knopf am Stab.</summary>
    public bool IsMockMode { get; }

    /// <summary>Warnung wenn Kompass-Kalibrierung während AR-Session schlecht wurde.</summary>
    [ObservableProperty] private string _magWarning = string.Empty;

    /// <summary>Kompass-Canvas muss neu gezeichnet werden</summary>
    public event Action? CompassInvalidateRequested;

    /// <summary>AR-Capture abgeschlossen - MainViewModel soll Transfer starten</summary>
    public event Action<Models.ArCaptureResult>? ArCaptureCompleted;

    public SurveyViewModel(IBleService bleService, IMeasurementService measurementService,
        IArCaptureService arCaptureService)
    {
        _bleService = bleService;
        _measurementService = measurementService;
        _arCaptureService = arCaptureService;
        IsMockMode = bleService is MockBleService;

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
            FixStatusText = _bleService.CurrentState.FixStatusText;

            // Bei Fix-Verlust: UI-Werte zurücksetzen damit User nicht mit Stale-Daten misst
            if (q == 0)
                ResetLivePositionUi();
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

            // Disconnect-UX: Bei Verbindungsabbruch alle Live-Werte zurücksetzen
            if (!state.IsConnected)
                ResetLivePositionUi();

            // Magnetometer-Warnung für AR-Qualität
            MagWarning = state.MagAccuracy < 2 && state.IsConnected
                ? "Kompass-Genauigkeit niedrig — bitte Stab 3x im Kreis drehen"
                : string.Empty;

            // Kompass-Renderer mit aktuellen Daten versorgen
            CompassRenderer.HorizontalAccuracy = state.HorizontalAccuracy;
            CompassRenderer.VerticalAccuracy = state.VerticalAccuracy;
            CompassRenderer.SatelliteCount = state.SatelliteCount;
            CompassRenderer.FixQuality = state.FixQuality;
            CompassRenderer.TiltAngle = state.TiltAngle;
            CompassInvalidateRequested?.Invoke();
        });

        // Punkte vom Stab empfangen (auch auf UI-Thread für ObservableCollection)
        _bleService.PointReceived += p => Dispatcher.UIThread.Post(() => OnPointReceived(p));
    }

    private void ResetLivePositionUi()
    {
        LatText = "—";
        LonText = "—";
        AltText = "—";
        HorizontalAccuracy = 0;
        VerticalAccuracy = 0;
        SatelliteCount = 0;
        TiltAngle = 0;
        FixStatusText = "KEIN FIX";
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

    /// <summary>AR-Kamera-Capture starten</summary>
    [RelayCommand]
    private async Task StartArCaptureAsync()
    {
        var available = await _arCaptureService.IsAvailableAsync();
        if (!available)
        {
            ArStatusText = "ARCore nicht verfuegbar";
            return;
        }

        ArStatusText = "AR-Kamera aktiv...";
        var result = await _arCaptureService.CaptureAsync();

        if (result != null && result.TotalPointCount > 0)
        {
            ArStatusText = $"{result.TotalPointCount} Punkte erfasst";
            ArCaptureCompleted?.Invoke(result);
        }
        else
        {
            ArStatusText = "AR-Capture abgebrochen";
        }
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
            FixStatus = StickState.GetFixStatusText(point.FixQuality)
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
