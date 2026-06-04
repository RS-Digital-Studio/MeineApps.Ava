using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SunSeeker.Shared.Graphics;
using SunSeeker.Shared.Models;
using SunSeeker.Shared.Services;

namespace SunSeeker.Shared.ViewModels;

/// <summary>
/// Live-Ausricht-Tab. Liest Geraete-Azimut + Neigung (Heading-Sensor), berechnet den aktuellen
/// Einfallswinkel zur Sonne und die Abweichung von der Soll-Ausrichtung und gibt konkrete
/// Dreh-/Neigungs-Anweisungen. Konvention: Das Handy flach mit dem Bildschirm an die
/// Panel-Vorderseite (zur Sonne) halten — dann entspricht die Display-Normale der Panel-Normale.
///
/// Die Sensoren werden nur bei aktivem Tab betrieben (<see cref="Activate"/>/<see cref="Deactivate"/>)
/// — spart Akku.
/// </summary>
public partial class AlignViewModel : ObservableObject, IDisposable
{
    private static readonly string[] CompassPoints =
        ["N", "NNO", "NO", "ONO", "O", "OSO", "SO", "SSO", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"];

    private readonly ILocationService _location;
    private readonly IHeadingService _heading;
    private readonly ISolarPositionService _solar;
    private readonly IAlignmentService _alignment;

    private DispatcherTimer? _timer;
    private bool _active;
    private HeadingReading _lastHeading;
    private AlignmentRecommendation _recommendation;

    public AlignViewModel(
        ILocationService location, IHeadingService heading,
        ISolarPositionService solar, IAlignmentService alignment)
    {
        _location = location;
        _heading = heading;
        _solar = solar;
        _alignment = alignment;

        Panels = new ObservableCollection<PanelProfile>(PanelProfile.All);
        Goals = new ObservableCollection<GoalOption>(
        [
            new(AlignmentGoal.NowMaximum, "Jetzt maximal (mobil)"),
            new(AlignmentGoal.TodayYield, "Heutiger Tagesertrag"),
            new(AlignmentGoal.AnnualYield, "Jahresertrag (fest)"),
            new(AlignmentGoal.WinterYield, "Winterbetrieb (steil)"),
        ]);
        _selectedPanel = Panels[0];
        _selectedGoal = Goals[0];

        _heading.Changed += OnHeadingChanged;
        _location.LocationChanged += OnLocationChanged;
    }

    /// <summary>Wird vom Code-Behind abonniert, um den SkiaSharp-Canvas neu zu zeichnen.</summary>
    public event Action? InvalidateRequested;

    public SunCompassRenderer Renderer { get; } = new();

    public ObservableCollection<PanelProfile> Panels { get; }
    public ObservableCollection<GoalOption> Goals { get; }

    [ObservableProperty] private PanelProfile _selectedPanel;
    [ObservableProperty] private GoalOption _selectedGoal;

    [ObservableProperty] private string _azimuthGuidance = "Panel zur Sonne ausrichten";
    [ObservableProperty] private string _tiltGuidance = "";
    [ObservableProperty] private string _panelAzimuthText = "—";
    [ObservableProperty] private string _panelTiltText = "—";
    [ObservableProperty] private string _targetAzimuthText = "—";
    [ObservableProperty] private string _targetTiltText = "—";
    [ObservableProperty] private string _gainText = "";
    [ObservableProperty] private string _qualityText = "";
    [ObservableProperty] private string _sunInfoText = "";
    [ObservableProperty] private bool _showCalibrationWarning;
    [ObservableProperty] private string _kickstandHint = "";
    [ObservableProperty] private bool _showKickstandHint;

    /// <summary>Tab wird sichtbar: Sensoren starten, Live-Update-Timer fuer den Sonnenstand starten.</summary>
    public void Activate()
    {
        if (_active) return;
        _active = true;

        if (_location.Current is { } loc)
            _heading.SetLocation(loc);

        _heading.Start();
        _location.Start();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Recompute();
        _timer.Start();

        Recompute();
    }

    /// <summary>Tab wird verlassen: Sensoren stoppen (Akku sparen).</summary>
    public void Deactivate()
    {
        if (!_active) return;
        _active = false;
        _timer?.Stop();
        _timer = null;
        _heading.Stop();
    }

    partial void OnSelectedPanelChanged(PanelProfile value) => Recompute();
    partial void OnSelectedGoalChanged(GoalOption value) => Recompute();

    private void OnHeadingChanged(object? sender, HeadingReading reading)
        => Dispatcher.UIThread.Post(() => { _lastHeading = reading; Recompute(); });

    private void OnLocationChanged(object? sender, GeoLocation location)
        => Dispatcher.UIThread.Post(() => { _heading.SetLocation(location); Recompute(); });

    private void Recompute()
    {
        if (!_active) return;

        var location = _location.Current ?? new GeoLocation(52.52, 13.405, 38);
        var nowUtc = DateTime.UtcNow;

        var sun = _solar.GetPosition(location, nowUtc);
        _recommendation = _alignment.GetRecommendation(location, nowUtc, SelectedGoal.Goal, SelectedPanel);

        // Konvention: Display-Normale = Panel-Normale (Handy an die Vorderseite).
        var panelAzimuth = _lastHeading.DeviceAzimuth;
        var panelTilt = _lastHeading.Tilt;
        var reliable = _lastHeading.AzimuthReliable;

        var state = _alignment.Evaluate(sun, panelAzimuth, panelTilt, _recommendation);

        // Renderer fuettern
        Renderer.PanelAzimuth = panelAzimuth;
        Renderer.TargetAzimuth = _recommendation.TargetAzimuth;
        Renderer.SunAzimuth = sun.Azimuth;
        Renderer.SunElevation = sun.Elevation;
        Renderer.PanelTilt = panelTilt;
        Renderer.TargetTilt = _recommendation.RecommendedKickstandTilt;
        Renderer.AzimuthReliable = reliable;
        Renderer.IsDaylight = sun.IsDaylight;
        Renderer.Quality = state.Quality;

        // Text-Anzeigen
        PanelAzimuthText = reliable ? $"{panelAzimuth:0}° {Compass(panelAzimuth)}" : "— neigen —";
        PanelTiltText = $"{panelTilt:0}°";
        TargetAzimuthText = $"{_recommendation.TargetAzimuth:0}° {Compass(_recommendation.TargetAzimuth)}";
        TargetTiltText = $"{_recommendation.RecommendedKickstandTilt:0}°";

        ShowCalibrationWarning = !reliable || _lastHeading.Accuracy <= HeadingAccuracy.Low;

        AzimuthGuidance = BuildAzimuthGuidance(state.AzimuthError, reliable);
        TiltGuidance = BuildTiltGuidance(panelTilt - _recommendation.RecommendedKickstandTilt);

        GainText = sun.IsDaylight
            ? $"{state.DirectGainFactor * 100:0}% der Direktstrahlung"
            : "Sonne unter dem Horizont";
        QualityText = QualityLabel(state.Quality, reliable);
        SunInfoText = $"Sonne: {sun.Azimuth:0}° {Compass(sun.Azimuth)} · {sun.Elevation:0}° hoch";

        ShowKickstandHint = SelectedPanel.HasFixedTilts;
        KickstandHint = SelectedPanel.HasFixedTilts
            ? $"Standwinkel {_recommendation.RecommendedKickstandTilt:0}° am Panel einstellen"
            : "";

        InvalidateRequested?.Invoke();
    }

    private static string BuildAzimuthGuidance(double azimuthError, bool reliable)
    {
        if (!reliable)
            return "Handy flach an die Panel-Flaeche halten";
        var abs = Math.Abs(azimuthError);
        if (abs < 3) return "Richtung passt";
        // azimuthError = panel - soll. Positiv -> Panel zu weit oestlich -> nach Westen/links drehen.
        var direction = azimuthError > 0 ? "nach Westen (links)" : "nach Osten (rechts)";
        return $"{abs:0}° {direction} drehen";
    }

    private static string BuildTiltGuidance(double tiltError)
    {
        var abs = Math.Abs(tiltError);
        if (abs < 3) return "Neigung passt";
        return tiltError > 0 ? $"{abs:0}° flacher stellen" : $"{abs:0}° steiler stellen";
    }

    private static string QualityLabel(AlignmentQuality quality, bool reliable) => !reliable
        ? "Ausrichtung wird gemessen"
        : quality switch
        {
            AlignmentQuality.Excellent => "Perfekt ausgerichtet",
            AlignmentQuality.Good => "Gut ausgerichtet",
            AlignmentQuality.Fair => "Noch justieren",
            _ => "Schlecht ausgerichtet",
        };

    private static string Compass(double azimuth)
    {
        var idx = (int)Math.Round(SunMath.Normalize360(azimuth) / 22.5) % 16;
        return CompassPoints[idx];
    }

    public void Dispose()
    {
        Deactivate();
        _heading.Changed -= OnHeadingChanged;
        _location.LocationChanged -= OnLocationChanged;
        Renderer.Dispose();
    }
}
