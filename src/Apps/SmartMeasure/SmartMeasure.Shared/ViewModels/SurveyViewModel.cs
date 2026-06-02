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
    private readonly IHardwareModeService _hardwareMode;

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

    // Live-Projekt-Statistik (gilt fuer AR- UND RTK-Punkte) — gibt dem AR-Nutzer ein
    // motivierendes, sichtbares Mess-Ergebnis direkt auf dem Start-Screen.
    [ObservableProperty] private int _pointCount;
    [ObservableProperty] private bool _hasPoints;
    [ObservableProperty] private string _areaText = "—";
    [ObservableProperty] private string _perimeterText = "—";

    // Live-Kompass Renderer
    public Graphics.SurveyLiveRenderer CompassRenderer { get; } = new();

    // AR-Capture
    [ObservableProperty] private bool _isArAvailable = true;
    [ObservableProperty] private string _arStatusText = string.Empty;
    [ObservableProperty] private bool _isArBusy;

    /// <summary>Adaptiver Betriebsmodus: true = RTK-Hardware-Ansicht (Kompass, Position,
    /// Genauigkeit, PUNKT-Button). false = reiner AR-Modus → grosser AR-Hero-CTA statt
    /// der leeren Hardware-Karten. Gespeist vom <see cref="IHardwareModeService"/>.</summary>
    [ObservableProperty] private bool _showRtkUi;

    /// <summary>Aktuell ein RTK-Stab verbunden? Steuert ob der PUNKT-Button bedienbar ist.</summary>
    [ObservableProperty] private bool _isBleConnected;

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
        IArCaptureService arCaptureService, IHardwareModeService hardwareMode)
    {
        _bleService = bleService;
        _measurementService = measurementService;
        _arCaptureService = arCaptureService;
        _hardwareMode = hardwareMode;
        IsMockMode = bleService is MockBleService;

        // Adaptiver Modus: initial + auf Aenderungen reagieren (Changed kommt vom BLE-Thread).
        ShowRtkUi = _hardwareMode.ShowRtkUi;
        IsBleConnected = _hardwareMode.IsConnected;
        _hardwareMode.Changed += () => Dispatcher.UIThread.Post(() =>
        {
            ShowRtkUi = _hardwareMode.ShowRtkUi;
            IsBleConnected = _hardwareMode.IsConnected;
        });

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

        // Listen-/Statistik-Pflege zentral ueber den MeasurementService — so erscheinen
        // AR-Punkte (via ArTransferService) genauso in der Liste wie RTK-Stab-Punkte.
        _measurementService.PointAdded += p => Dispatcher.UIThread.Post(() => OnMeasurementPointAdded(p));
        _measurementService.PointsReset += () => Dispatcher.UIThread.Post(RebuildPointList);
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
        if (!IsBleConnected)
        {
            // Frueher tat dieser Button ohne verbundenen Stab stumm nichts. Jetzt klare
            // Ansage statt totes Steuerelement. (Im AR-Modus ist der Button ohnehin ausgeblendet.)
            ArStatusText = "Kein RTK-Stab verbunden. Nutze die AR-Kamera zum Vermessen.";
            return;
        }

        if (_bleService is MockBleService mock)
            mock.SimulatePointTrigger();
        // Bei echtem BLE: Stab sendet den Punkt per Hardware-Knopf.
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
        if (IsArBusy) return; // Doppel-Tap-Schutz auf den grossen Hero-CTA
        IsArBusy = true;
        try
        {
            var available = await _arCaptureService.IsAvailableAsync();
            IsArAvailable = available;
            if (!available)
            {
                ArStatusText = "AR-Kamera (ARCore) ist auf diesem Geraet nicht verfuegbar.";
                return;
            }

            // Plan-Kap. 5.2: Bestehende Projekt-Punkte als Site-Marker in die AR-Session
            // mitnehmen — neue Erfassungen landen dann im selben Koordinatensystem.
            var sitePoints = _measurementService.CurrentPoints;
            _arCaptureService.SetSitePoints(sitePoints.Count > 0 ? [.. sitePoints] : null);

            ArStatusText = "AR-Kamera aktiv...";
            ArCaptureResult? result;
            try
            {
                result = await _arCaptureService.CaptureAsync();
            }
            finally
            {
                // Site-Points-Bruecke zuruecksetzen — naechster Aufruf soll keine veralteten
                // Punkte erben.
                _arCaptureService.SetSitePoints(null);
            }

            if (result != null && result.TotalPointCount > 0)
            {
                ArStatusText = $"{result.TotalPointCount} Punkte erfasst";
                ArCaptureCompleted?.Invoke(result);
                return;
            }

            // Plan Kap. 4.3: Statt pauschal "abgebrochen" den Status differenzieren — User
            // soll erkennen ob er selbst geschlossen hat oder ein Fehler vorlag.
            ArStatusText = _arCaptureService.LastCompletionStatus switch
            {
                ArCaptureCompletionStatus.UserCancelled => "AR-Capture abgebrochen",
                ArCaptureCompletionStatus.Error         => _arCaptureService.LastError ?? "AR-Fehler",
                ArCaptureCompletionStatus.Success       => "Keine Punkte erfasst",
                _                                        => "AR-Capture abgebrochen",
            };
        }
        finally
        {
            IsArBusy = false;
        }
    }

    /// <summary>Alle Punkte loeschen. ClearPoints feuert PointsReset → RebuildPointList
    /// raeumt Liste + Statistik konsistent auf.</summary>
    [RelayCommand]
    private void ClearPoints() => _measurementService.ClearPoints();

    // ===== Debug-Commands (nur im Mock-Modus sichtbar) =====
    // Simulieren Edge-Cases die sonst nur im echten Feld auftreten.

    [RelayCommand]
    private void DebugCycleFix()
    {
        if (_bleService is MockBleService mock) mock.CycleFixDegradation();
    }

    [RelayCommand]
    private void DebugPacketLoss()
    {
        if (_bleService is MockBleService mock) mock.SimulatePacketLoss(5);
    }

    [RelayCommand]
    private void DebugBatteryDrain()
    {
        if (_bleService is MockBleService mock) mock.SimulateBatteryDrain();
    }

    [RelayCommand]
    private void DebugMagLoss()
    {
        if (_bleService is MockBleService mock) mock.SimulateMagLoss();
    }

    [RelayCommand]
    private void DebugSpuriousDisconnect()
    {
        if (_bleService is MockBleService mock) mock.SimulateSpuriousDisconnect();
    }

    private void OnPointReceived(SurveyPoint point)
    {
        // Label aus dem Eingabefeld zuweisen, dann zentral ueber den MeasurementService
        // hinzufuegen. Listen-/Statistik-Pflege passiert in OnMeasurementPointAdded — gilt
        // damit gleichermassen fuer BLE-Stab- und AR-Punkte.
        if (!string.IsNullOrWhiteSpace(PointLabel))
        {
            point.Label = PointLabel;
            PointLabel = string.Empty;
        }

        _measurementService.AddPoint(point);
    }

    /// <summary>Ein Punkt wurde dem MeasurementService hinzugefuegt (BLE-Stab ODER AR-Transfer).
    /// Aktualisiert Abstand, Anzeige-Liste und Projekt-Statistik.</summary>
    private void OnMeasurementPointAdded(SurveyPoint point)
    {
        var points = _measurementService.CurrentPoints;
        if (points.Count >= 2)
        {
            var dist = _measurementService.CalculateDistance2D(points[^2], points[^1]);
            DistanceToLast = $"{dist:F2} m";
        }

        RecentPoints.Insert(0, SurveyPointDisplay.FromPoint(point, points.Count, DistanceToLast));
        UpdateStats();
    }

    /// <summary>Komplette Liste neu aufbauen (Projekt-Load, Clear). Neueste Punkte oben.</summary>
    private void RebuildPointList()
    {
        RecentPoints.Clear();
        var points = _measurementService.CurrentPoints;
        for (var i = 0; i < points.Count; i++)
        {
            var dist = i >= 1
                ? $"{_measurementService.CalculateDistance2D(points[i - 1], points[i]):F2} m"
                : "—";
            RecentPoints.Insert(0, SurveyPointDisplay.FromPoint(points[i], i + 1, dist));
        }

        DistanceToLast = points.Count >= 2
            ? $"{_measurementService.CalculateDistance2D(points[^2], points[^1]):F2} m"
            : "—";
        UpdateStats();
    }

    /// <summary>Projekt-Statistik (Punktzahl, Flaeche, Umfang) neu berechnen.
    /// Fuer den AR-Nutzer das sichtbare Ergebnis seiner Messung.</summary>
    private void UpdateStats()
    {
        var points = _measurementService.CurrentPoints;
        PointCount = points.Count;
        HasPoints = points.Count > 0;

        if (points.Count >= 3)
        {
            var area = _measurementService.CalculateArea(points);
            AreaText = area >= 10000 ? $"{area / 10000.0:F2} ha" : $"{area:F1} m²";
            PerimeterText = $"{_measurementService.CalculatePerimeter(points):F1} m";
        }
        else
        {
            AreaText = "—";
            PerimeterText = "—";
        }
    }
}

/// <summary>Anzeige-Objekt fuer die Punkte-Liste (AR- und RTK-Punkte einheitlich).</summary>
public class SurveyPointDisplay
{
    /// <summary>AR-erfasste Punkte tragen diesen FixQuality-Wert (siehe ArTransferService).</summary>
    private const int ArFixQuality = 10;

    public int Number { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Altitude { get; set; } = string.Empty;
    public string Accuracy { get; set; } = string.Empty;
    public string Distance { get; set; } = string.Empty;

    /// <summary>Qualitaets-Text: AR-Punkt → Konfidenz ("AR · 85%"), RTK-Punkt → Fix-Status.</summary>
    public string Quality { get; set; } = string.Empty;

    /// <summary>Hex-Farbe des Qualitaets-Texts (matcht die Confidence-/RTK-Tokens der AppPalette).
    /// Im XAML via StringToColorBrushConverter gebunden — Display-Objekt sieht keine Theme-Resources.</summary>
    public string QualityColor { get; set; } = "#8899AA";

    /// <summary>True bei AR-Punkten (FixQuality == 10).</summary>
    public bool IsAr { get; set; }

    /// <summary>Baut ein Anzeige-Objekt aus einem SurveyPoint — unterscheidet AR (Konfidenz)
    /// von RTK (Fix-Status) fuer ehrliche Genauigkeits-Kommunikation.</summary>
    public static SurveyPointDisplay FromPoint(SurveyPoint p, int number, string distance)
    {
        var isAr = p.FixQuality == ArFixQuality;
        string quality, color;

        if (isAr)
        {
            var pct = (int)MathF.Round(Math.Clamp(p.Confidence, 0f, 1f) * 100f);
            quality = $"AR · {pct}%";
            color = p.Confidence >= 0.75f ? "#4CAF50"   // ConfidenceHigh
                  : p.Confidence >= 0.50f ? "#FFC107"   // ConfidenceMid
                  : "#FF7043";                          // ConfidenceLow
        }
        else
        {
            quality = StickState.GetFixStatusText(p.FixQuality);
            color = p.FixQuality >= 4 ? "#4CAF50"       // RtkFix
                  : p.FixQuality >= 1 ? "#FFC107"       // RtkFloat/DGPS
                  : "#EF5350";                          // NoFix
        }

        return new SurveyPointDisplay
        {
            Number = number,
            Label = string.IsNullOrWhiteSpace(p.Label) ? $"P{number}" : p.Label,
            Altitude = $"{p.Altitude:F2} m",
            Accuracy = $"±{p.HorizontalAccuracy:F1} cm",
            Distance = distance,
            Quality = quality,
            QualityColor = color,
            IsAr = isAr,
        };
    }
}
