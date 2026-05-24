using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using SmartMeasure.Shared.Graphics;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>
/// Absteckung: geplanten Punkt im Feld finden. Zeigt live Pfeil + Distanz zum Ziel.
///
/// Arbeitsablauf:
/// 1. User lädt Ziele aus aktuellem Projekt (Messpunkte oder Garten-Kontur-Knoten).
/// 2. User wählt ein Ziel aus der Liste.
/// 3. Während User zum Ziel läuft, updated die App Distanz + Bearing pro BLE-Position.
/// 4. Bei &lt; 10cm: Haptic + Audio-Feedback, Pfeil wird grün, "Markiert"-Button.
///
/// Bearing-Berechnung: Initial-Bearing-Formel auf Kugel (Haversine-Kontext).
/// Heading-Quelle: Primär Bewegungsrichtung (aus letzten 2 Positionen wenn Bewegung &gt; 30cm),
/// Fallback auf 0° (dann zeigt Pfeil absolut zum Nord).
/// </summary>
public partial class StakeoutViewModel : ViewModelBase
{
    private readonly IBleService _bleService;
    private readonly IProjectService _projectService;
    private readonly IGardenPlanService _gardenPlanService;

    // Live-Position
    private double _currentLat;
    private double _currentLon;
    private double _currentAlt;
    private bool _hasPosition;

    // Bewegungs-Heading (aus letzten Positionen berechnet)
    private double _lastLat;
    private double _lastLon;
    private bool _hasLastPosition;
    private double _movementHeadingDeg;

    /// <summary>Aktive Projekt-ID (wird vom MainViewModel gesetzt beim Projekt-Wechsel)</summary>
    public int CurrentProjectId { get; set; }

    public ObservableCollection<StakeoutTarget> Targets { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveTarget))]
    private StakeoutTarget? _activeTarget;

    public bool HasActiveTarget => ActiveTarget != null;

    [ObservableProperty] private double _distanceMeters = double.PositiveInfinity;
    [ObservableProperty] private double _bearingDeg;
    [ObservableProperty] private double _altitudeDeltaMeters;
    [ObservableProperty] private string _statusText = "Projekt laden für Absteckung";

    public StakeoutRenderer Renderer { get; } = new();

    /// <summary>Canvas muss neu gezeichnet werden</summary>
    public event Action? InvalidateRequested;

    /// <summary>Haptic-Feedback anfordern (Android übersetzt zu Vibration)</summary>
    public event Action? TargetNearRequested;

    private bool _targetNearTriggered;

    private readonly IArCaptureService _arCaptureService;

    public StakeoutViewModel(
        IBleService bleService,
        IProjectService projectService,
        IGardenPlanService gardenPlanService,
        IArCaptureService arCaptureService)
    {
        _bleService = bleService;
        _projectService = projectService;
        _gardenPlanService = gardenPlanService;
        _arCaptureService = arCaptureService;

        _bleService.PositionUpdated += (lat, lon, alt) =>
            Dispatcher.UIThread.Post(() => OnPositionUpdate(lat, lon, alt));
    }

    /// <summary>Plan-Kap. 5.9: AR-Stakeout starten. Aktuelle Targets werden an die
    /// ArCaptureActivity uebergeben, dort kann der User per Toolbar-Button auf den
    /// "Absteck"-Modus umschalten und den Pfeil zum naechsten Ziel sehen. Erreichte
    /// Targets werden direkt im StakeoutTarget-Objekt (ObservableObject) markiert —
    /// die Liste im Stakeout-Tab updated sich live mit.</summary>
    [RelayCommand]
    public async Task StartArStakeoutAsync()
    {
        if (Targets.Count == 0)
        {
            StatusText = "Erst Ziele laden, dann AR starten";
            return;
        }
        if (!await _arCaptureService.IsAvailableAsync())
        {
            StatusText = "ARCore nicht verfuegbar";
            return;
        }

        // Snapshot reichen — die Activity-Logik mutiert Target.IsReached direkt auf den
        // Listen-Eintraegen, sodass das UI im Stakeout-Tab gleich aktualisiert ist.
        _arCaptureService.SetStakeoutTargets(Targets.ToList());
        try
        {
            await _arCaptureService.CaptureAsync();
        }
        finally
        {
            // Targets-Bruecke zuruecksetzen — die naechste AR-Session soll nicht
            // ueberraschend wieder Stakeout-Ziele zeigen.
            _arCaptureService.SetStakeoutTargets(null);
        }
    }

    /// <summary>Ziele aus aktuellem Projekt laden: Messpunkte + Garten-Kontur-Knoten.</summary>
    [RelayCommand]
    public async Task LoadTargetsAsync()
    {
        Targets.Clear();
        if (CurrentProjectId <= 0)
        {
            StatusText = "Kein Projekt ausgewählt — zuerst in Projekte wählen";
            return;
        }

        var project = await _projectService.GetProjectAsync(CurrentProjectId);
        if (project == null)
        {
            StatusText = "Projekt nicht gefunden";
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Targets.Clear();

            // Messpunkte als Ziele
            var pointIdx = 1;
            foreach (var p in project.Points)
            {
                Targets.Add(new StakeoutTarget
                {
                    Label = !string.IsNullOrWhiteSpace(p.Label) ? p.Label : $"P{pointIdx}",
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Altitude = p.Altitude,
                    Source = StakeoutTargetSource.SurveyPoint,
                });
                pointIdx++;
            }

            // Garten-Kontur-Knoten als Ziele (jeder Vertex eines Wegs/Zauns ist ein potentieller Absteckpunkt)
            foreach (var el in project.GardenElements)
            {
                var wgs = _gardenPlanService.ParsePointsWgs84(el.PointsJson);
                if (wgs == null || wgs.Count == 0) continue;

                for (var i = 0; i < wgs.Count; i++)
                {
                    var (lat, lon) = wgs[i];
                    Targets.Add(new StakeoutTarget
                    {
                        Label = $"{el.ElementType} {i + 1}",
                        Latitude = lat,
                        Longitude = lon,
                        Altitude = el.TargetAltitude, // für Terrassen relevant, sonst 0
                        Source = StakeoutTargetSource.GardenElement,
                    });
                }
            }

            StatusText = Targets.Count == 0
                ? "Keine Ziele — Messpunkte oder Gartenelemente erfassen"
                : $"{Targets.Count} Ziele geladen — wähle eins aus";
        });
    }

    [RelayCommand]
    private void SelectTarget(StakeoutTarget? target)
    {
        ActiveTarget = target;
        _targetNearTriggered = false;
        if (target != null)
        {
            StatusText = $"Ziel: {target.Label}";
            target.BestDistance = double.PositiveInfinity; // Session-Reset
            UpdateDistanceAndBearing();
        }
        else
        {
            StatusText = "Kein Ziel ausgewählt";
        }
    }

    [RelayCommand]
    private void MarkReached()
    {
        if (ActiveTarget == null) return;
        ActiveTarget.IsReached = true;
        StatusText = $"✓ {ActiveTarget.Label} markiert";
        // Nächstes unmarkiertes Ziel automatisch wählen
        var next = Targets.FirstOrDefault(t => !t.IsReached && t != ActiveTarget);
        if (next != null) SelectTarget(next);
    }

    private void OnPositionUpdate(double lat, double lon, double alt)
    {
        // Bewegungs-Heading aus letzten 2 Positionen — nur wenn Bewegung > 30cm (sonst GPS-Noise)
        if (_hasLastPosition)
        {
            var dLat = lat - _lastLat;
            var dLon = lon - _lastLon;
            const double metersPerDegLat = 111320.0;
            var metersPerDegLon = 111320.0 * Math.Cos(lat * Math.PI / 180.0);
            var deltaNorth = dLat * metersPerDegLat;
            var deltaEast = dLon * metersPerDegLon;
            var movedDist = Math.Sqrt(deltaNorth * deltaNorth + deltaEast * deltaEast);

            if (movedDist > 0.3)
            {
                // atan2: 0° = Nord, 90° = Ost
                var heading = Math.Atan2(deltaEast, deltaNorth) * 180.0 / Math.PI;
                if (heading < 0) heading += 360.0;
                _movementHeadingDeg = heading;
                _lastLat = lat;
                _lastLon = lon;
            }
        }
        else
        {
            _lastLat = lat;
            _lastLon = lon;
            _hasLastPosition = true;
        }

        _currentLat = lat;
        _currentLon = lon;
        _currentAlt = alt;
        _hasPosition = true;

        UpdateDistanceAndBearing();
    }

    private void UpdateDistanceAndBearing()
    {
        if (!_hasPosition || ActiveTarget == null)
        {
            Renderer.HasTarget = false;
            InvalidateRequested?.Invoke();
            return;
        }

        var (dist, bearing) = ComputeDistanceAndBearing(
            _currentLat, _currentLon,
            ActiveTarget.Latitude, ActiveTarget.Longitude);

        DistanceMeters = dist;
        BearingDeg = bearing;
        AltitudeDeltaMeters = ActiveTarget.Altitude - _currentAlt;

        if (dist < ActiveTarget.BestDistance)
            ActiveTarget.BestDistance = dist;

        // Haptic-Feedback wenn Ziel erreicht (<10cm) — einmalig pro Session
        if (dist < 0.1 && !_targetNearTriggered)
        {
            _targetNearTriggered = true;
            TargetNearRequested?.Invoke();
            StatusText = $"✓ Ziel erreicht: {ActiveTarget.Label} — [Markieren] drücken";
        }
        else if (dist >= 0.2 && _targetNearTriggered)
        {
            // Hysterese: erst bei 20cm wieder zurücksetzen
            _targetNearTriggered = false;
        }

        Renderer.HasTarget = true;
        Renderer.TargetLabel = ActiveTarget.Label;
        Renderer.DistanceMeters = dist;
        Renderer.BearingDeg = bearing;
        Renderer.HeadingDeg = _movementHeadingDeg;
        Renderer.AltitudeDeltaMeters = AltitudeDeltaMeters;

        InvalidateRequested?.Invoke();
    }

    /// <summary>Haversine-Distanz + Initial-Bearing zwischen zwei WGS84-Koordinaten.</summary>
    private static (double distanceMeters, double bearingDeg) ComputeDistanceAndBearing(
        double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadius = 6371000.0;
        var phi1 = lat1 * Math.PI / 180.0;
        var phi2 = lat2 * Math.PI / 180.0;
        var dPhi = (lat2 - lat1) * Math.PI / 180.0;
        var dLambda = (lon2 - lon1) * Math.PI / 180.0;

        // Distanz (Haversine)
        var a = Math.Sin(dPhi / 2) * Math.Sin(dPhi / 2) +
                Math.Cos(phi1) * Math.Cos(phi2) *
                Math.Sin(dLambda / 2) * Math.Sin(dLambda / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var distance = earthRadius * c;

        // Initial-Bearing: 0° = Nord, 90° = Ost
        var y = Math.Sin(dLambda) * Math.Cos(phi2);
        var x = Math.Cos(phi1) * Math.Sin(phi2) -
                Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(dLambda);
        var bearing = Math.Atan2(y, x) * 180.0 / Math.PI;
        if (bearing < 0) bearing += 360.0;

        return (distance, bearing);
    }
}
