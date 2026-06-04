using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SunSeeker.Shared.Models;
using SunSeeker.Shared.Services;

namespace SunSeeker.Shared.ViewModels;

/// <summary>
/// Uebersichts-Tab: zeigt live Standort, Sonnenstand, Sonnenzeiten, die empfohlene
/// Soll-Ausrichtung je Ziel/Panel und die Bifazial-Empfehlung je Untergrund.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private static readonly string[] CompassPoints =
        ["N", "NNO", "NO", "ONO", "O", "OSO", "SO", "SSO", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"];

    private readonly ILocationService _location;
    private readonly ISolarPositionService _solar;
    private readonly IAlignmentService _alignment;
    private readonly IBifacialService _bifacial;

    private DispatcherTimer? _timer;
    private bool _initialized;

    public DashboardViewModel(
        ILocationService location,
        ISolarPositionService solar,
        IAlignmentService alignment,
        IBifacialService bifacial)
    {
        _location = location;
        _solar = solar;
        _alignment = alignment;
        _bifacial = bifacial;

        Panels = new ObservableCollection<PanelProfile>(PanelProfile.All);
        Goals = new ObservableCollection<GoalOption>(
        [
            new(AlignmentGoal.NowMaximum, "Jetzt maximal (mobil)"),
            new(AlignmentGoal.TodayYield, "Heutiger Tagesertrag"),
            new(AlignmentGoal.AnnualYield, "Jahresertrag (fest)"),
            new(AlignmentGoal.WinterYield, "Winterbetrieb (steil)"),
        ]);
        Grounds = new ObservableCollection<GroundOption>(
            Enum.GetValues<GroundType>().Select(g => new GroundOption(g, g.DisplayName())).ToList());

        _selectedPanel = Panels[0];
        _selectedGoal = Goals[0];
        _selectedGround = Grounds[0];
    }

    public ObservableCollection<PanelProfile> Panels { get; }
    public ObservableCollection<GoalOption> Goals { get; }
    public ObservableCollection<GroundOption> Grounds { get; }

    [ObservableProperty] private PanelProfile _selectedPanel;
    [ObservableProperty] private GoalOption _selectedGoal;
    [ObservableProperty] private GroundOption _selectedGround;

    [ObservableProperty] private string _locationText = "Standort wird ermittelt...";
    [ObservableProperty] private string _clockText = "";
    [ObservableProperty] private string _sunAzimuthText = "—";
    [ObservableProperty] private string _sunElevationText = "—";
    [ObservableProperty] private bool _isDaylight;
    [ObservableProperty] private string _daylightText = "";
    [ObservableProperty] private string _sunriseText = "—";
    [ObservableProperty] private string _sunsetText = "—";
    [ObservableProperty] private string _solarNoonText = "—";
    [ObservableProperty] private string _targetAzimuthText = "—";
    [ObservableProperty] private string _targetTiltText = "—";
    [ObservableProperty] private string _kickstandText = "—";
    [ObservableProperty] private bool _showKickstand;
    [ObservableProperty] private string _recommendationExplanation = "";
    [ObservableProperty] private string _currentYieldText = "";
    [ObservableProperty] private bool _isBifacial;
    [ObservableProperty] private string _bifacialGainText = "—";
    [ObservableProperty] private string _tiltBonusText = "";
    [ObservableProperty] private string _albedoText = "";

    public ObservableCollection<string> BifacialTips { get; } = [];

    public Task InitializeAsync()
    {
        // GetCurrentAsync ist auf dem Mock synchron; auf Android liefert es die letzte
        // bekannte Position (Updates kommen via LocationChanged).
        if (_location.Current is { } l)
            LocationText = FormatLocation(l);

        _location.LocationChanged += (_, newLoc) =>
            Dispatcher.UIThread.Post(() =>
            {
                LocationText = FormatLocation(newLoc);
                Refresh();
            });

        _initialized = true;
        Refresh();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        return Task.CompletedTask;
    }

    partial void OnSelectedPanelChanged(PanelProfile value) => Refresh();
    partial void OnSelectedGoalChanged(GoalOption value) => Refresh();
    partial void OnSelectedGroundChanged(GroundOption value) => Refresh();

    private void Refresh()
    {
        if (!_initialized) return;

        var location = _location.Current ?? new GeoLocation(52.52, 13.405, 38);
        var nowUtc = DateTime.UtcNow;

        ClockText = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);

        var sun = _solar.GetPosition(location, nowUtc);
        IsDaylight = sun.IsDaylight;
        DaylightText = sun.IsDaylight ? "Sonne ueber dem Horizont" : "Sonne unter dem Horizont";
        SunAzimuthText = $"{sun.Azimuth:0}° {Compass(sun.Azimuth)}";
        SunElevationText = $"{sun.Elevation:0.0}°";

        var times = _solar.GetSunTimes(location, DateOnly.FromDateTime(nowUtc));
        SunriseText = times.PolarNight ? "—" : times.PolarDay ? "geht nicht unter" : FormatLocalTime(times.SunriseUtc);
        SunsetText = times.PolarDay ? "—" : times.PolarNight ? "geht nicht auf" : FormatLocalTime(times.SunsetUtc);
        SolarNoonText = FormatLocalTime(times.SolarNoonUtc);

        var panel = SelectedPanel;
        var rec = _alignment.GetRecommendation(location, nowUtc, SelectedGoal.Goal, panel);
        TargetAzimuthText = $"{rec.TargetAzimuth:0}° {Compass(rec.TargetAzimuth)}";
        TargetTiltText = $"{rec.TargetTilt:0}° Neigung";
        ShowKickstand = panel.HasFixedTilts;
        KickstandText = panel.HasFixedTilts ? $"Standwinkel {rec.RecommendedKickstandTilt:0}° waehlen" : "";
        RecommendationExplanation = rec.Explanation;

        if (sun.IsDaylight)
        {
            var state = _alignment.Evaluate(sun, rec.TargetAzimuth, rec.RecommendedKickstandTilt, rec);
            CurrentYieldText = $"{state.DirectGainFactor * 100:0}% der Direktstrahlung bei dieser Ausrichtung";
        }
        else
        {
            CurrentYieldText = "Kein direkter Ertrag (Sonne unter dem Horizont)";
        }

        var advice = _bifacial.GetAdvice(SelectedGround.Ground, panel);
        IsBifacial = panel.IsBifacial;
        AlbedoText = $"Albedo {advice.Albedo:0.00}";
        if (panel.IsBifacial)
        {
            BifacialGainText = $"+{advice.EstimatedGainLow * 100:0}–{advice.EstimatedGainHigh * 100:0}% Mehrertrag (Rueckseite)";
            TiltBonusText = advice.TiltBonusDegrees >= 1
                ? $"Tipp: bei diesem Untergrund ~{advice.TiltBonusDegrees:0}° steiler stellen"
                : "";
        }
        else
        {
            BifacialGainText = "Panel nicht bifazial";
            TiltBonusText = "";
        }

        BifacialTips.Clear();
        foreach (var tip in advice.Tips)
            BifacialTips.Add(tip);
    }

    private static string FormatLocation(GeoLocation l)
    {
        var ns = l.Latitude >= 0 ? "N" : "S";
        var ew = l.Longitude >= 0 ? "O" : "W";
        return $"{Math.Abs(l.Latitude):0.000}° {ns}, {Math.Abs(l.Longitude):0.000}° {ew}";
    }

    private static string FormatLocalTime(DateTime? utc)
        => utc is { } u ? u.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture) : "—";

    private static string Compass(double azimuth)
    {
        var idx = (int)Math.Round(SunMath.Normalize360(azimuth) / 22.5) % 16;
        return CompassPoints[idx];
    }
}

/// <summary>Auswahl-Eintrag fuer das Ausricht-Ziel (Enum + deutsches Label).</summary>
public sealed record GoalOption(AlignmentGoal Goal, string Label);

/// <summary>Auswahl-Eintrag fuer den Untergrund (Enum + deutsches Label).</summary>
public sealed record GroundOption(GroundType Ground, string Label);
