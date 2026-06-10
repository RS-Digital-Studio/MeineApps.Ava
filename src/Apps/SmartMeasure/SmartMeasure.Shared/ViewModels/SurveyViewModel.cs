using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>Live-Vermessung: AR-Capture starten, Punkte anzeigen, Projekt-Statistik</summary>
public partial class SurveyViewModel : ViewModelBase
{
    private readonly IMeasurementService _measurementService;
    private readonly IArCaptureService _arCaptureService;

    // Letzte Punkte
    public ObservableCollection<SurveyPointDisplay> RecentPoints { get; } = [];

    // Abstand zum letzten Punkt
    [ObservableProperty] private string _distanceToLast = "—";

    // Live-Projekt-Statistik — gibt dem AR-Nutzer ein motivierendes, sichtbares
    // Mess-Ergebnis direkt auf dem Start-Screen.
    [ObservableProperty] private int _pointCount;
    [ObservableProperty] private bool _hasPoints;
    [ObservableProperty] private string _areaText = "—";
    [ObservableProperty] private string _perimeterText = "—";

    // AR-Capture
    [ObservableProperty] private bool _isArAvailable = true;
    [ObservableProperty] private string _arStatusText = string.Empty;
    [ObservableProperty] private bool _isArBusy;

    /// <summary>AR-Capture abgeschlossen - MainViewModel soll Transfer starten</summary>
    public event Action<Models.ArCaptureResult>? ArCaptureCompleted;

    public SurveyViewModel(IMeasurementService measurementService, IArCaptureService arCaptureService)
    {
        _measurementService = measurementService;
        _arCaptureService = arCaptureService;

        // Listen-/Statistik-Pflege zentral ueber den MeasurementService — AR-Punkte
        // (via ArTransferService) erscheinen damit direkt in Liste + Statistik.
        _measurementService.PointAdded += p => Dispatcher.UIThread.Post(() => OnMeasurementPointAdded(p));
        _measurementService.PointsReset += () => Dispatcher.UIThread.Post(RebuildPointList);
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

            // Bestehende Projekt-Punkte in die AR-Session mitnehmen — als graue Earth-Site-Marker
            // (Plan-Kap. 5.2, nur bei aktivem VPS sichtbar) UND als Geo-unabhaengige Vorlade-Punkte
            // ("alles noch da", Lage relativ — sichtbar auch im reinen AR-Modus). Beide aus
            // derselben Quelle; Vorlade-Punkte gehen nicht ins Ergebnis zurueck.
            var sitePoints = _measurementService.CurrentPoints;
            IReadOnlyList<SurveyPoint>? snapshot = sitePoints.Count > 0 ? [.. sitePoints] : null;
            _arCaptureService.SetSitePoints(snapshot);
            _arCaptureService.SetPreloadPoints(snapshot);

            ArStatusText = "AR-Kamera aktiv...";
            ArCaptureResult? result;
            try
            {
                result = await _arCaptureService.CaptureAsync();
            }
            finally
            {
                // Bruecken zuruecksetzen — naechster Aufruf soll keine veralteten Punkte erben.
                _arCaptureService.SetSitePoints(null);
                _arCaptureService.SetPreloadPoints(null);
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

    /// <summary>Ein Punkt wurde dem MeasurementService hinzugefuegt (AR-Transfer).
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

/// <summary>Anzeige-Objekt fuer die Punkte-Liste.</summary>
public class SurveyPointDisplay
{
    public int Number { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Altitude { get; set; } = string.Empty;
    public string Accuracy { get; set; } = string.Empty;
    public string Distance { get; set; } = string.Empty;

    /// <summary>Qualitaets-Text: ARCore-Konfidenz ("AR · 85%").</summary>
    public string Quality { get; set; } = string.Empty;

    /// <summary>Hex-Farbe des Qualitaets-Texts (matcht die Confidence-Tokens der AppPalette).
    /// Im XAML via StringToColorBrushConverter gebunden — Display-Objekt sieht keine Theme-Resources.</summary>
    public string QualityColor { get; set; } = "#8899AA";

    /// <summary>Baut ein Anzeige-Objekt aus einem SurveyPoint — die ARCore-Konfidenz
    /// (Hit-Quality, Streuung, Stabilitaet) als ehrliche Genauigkeits-Kommunikation.</summary>
    public static SurveyPointDisplay FromPoint(SurveyPoint p, int number, string distance)
    {
        var pct = (int)MathF.Round(Math.Clamp(p.Confidence, 0f, 1f) * 100f);
        var color = p.Confidence >= 0.75f ? "#4CAF50"   // ConfidenceHigh
                  : p.Confidence >= 0.50f ? "#FFC107"   // ConfidenceMid
                  : "#FF7043";                          // ConfidenceLow

        return new SurveyPointDisplay
        {
            Number = number,
            Label = string.IsNullOrWhiteSpace(p.Label) ? $"P{number}" : p.Label,
            Altitude = $"{p.Altitude:F2} m",
            Accuracy = $"±{p.HorizontalAccuracy:F1} cm",
            Distance = distance,
            Quality = $"AR · {pct}%",
            QualityColor = color,
        };
    }
}
