using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using SunSeeker.Shared.Graphics;
using SunSeeker.Shared.Models;
using SunSeeker.Shared.Services;

namespace SunSeeker.Shared.ViewModels;

/// <summary>
/// Übersichts-Tab: zeigt live Standort, Sonnenstand, Sonnenzeiten, die empfohlene
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
    private readonly ILocalizationService _loc;

    private DispatcherTimer? _timer;
    private bool _initialized;
    private DateOnly _dayArcDate;

    /// <summary>Wird vom Code-Behind abonniert, um das Sonnenbahn-Diagramm neu zu zeichnen.</summary>
    public event Action? SunPathInvalidateRequested;

    public SunPathRenderer SunPathRenderer { get; } = new();

    /// <summary>True, wenn das AR-Sonnenbahn-Overlay verfügbar ist (Android-Kamera-Activity verdrahtet).</summary>
    public bool IsSunArAvailable { get; } = App.LaunchSunAr is not null;

    /// <summary>Öffnet das AR-Sonnenbahn-Overlay (native Kamera-Activity, nur Android).</summary>
    [RelayCommand]
    private void OpenSunAr() => App.LaunchSunAr?.Invoke();

    public DashboardViewModel(
        ILocationService location,
        ISolarPositionService solar,
        IAlignmentService alignment,
        IBifacialService bifacial,
        ILocalizationService loc)
    {
        _location = location;
        _solar = solar;
        _alignment = alignment;
        _bifacial = bifacial;
        _loc = loc;

        Panels = new ObservableCollection<PanelProfile>(PanelProfile.All);
        Goals = new ObservableCollection<GoalOption>(
        [
            new(AlignmentGoal.NowMaximum, loc.GetString("GoalNowMaximum")),
            new(AlignmentGoal.TodayYield, loc.GetString("GoalTodayYield")),
            new(AlignmentGoal.SeasonYield, loc.GetString("GoalSeasonYield")),
            new(AlignmentGoal.AnnualYield, loc.GetString("GoalAnnualYield")),
            new(AlignmentGoal.WinterYield, loc.GetString("GoalWinterYield")),
        ]);
        Grounds = new ObservableCollection<GroundOption>(
            Enum.GetValues<GroundType>().Select(g => new GroundOption(g, loc.GetString(g.LocKey()))).ToList());

        _selectedPanel = Panels[0];
        _selectedGoal = Goals[0];
        _selectedGround = Grounds[0];
        _locationText = loc.GetString("LocationPending");
    }

    public ObservableCollection<PanelProfile> Panels { get; }
    public ObservableCollection<GoalOption> Goals { get; }
    public ObservableCollection<GroundOption> Grounds { get; }

    [ObservableProperty] private PanelProfile _selectedPanel;
    [ObservableProperty] private GoalOption _selectedGoal;
    [ObservableProperty] private GroundOption _selectedGround;

    [ObservableProperty] private string _locationText = "";
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
                _dayArcDate = default; // Sonnenbahn für den neuen Standort neu berechnen
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
        DaylightText = _loc.GetString(sun.IsDaylight ? "SunAboveHorizon" : "SunBelowHorizon");
        SunAzimuthText = $"{sun.Azimuth:0}° {Compass(sun.Azimuth)}";
        SunElevationText = $"{sun.Elevation:0.0}°";

        // Sonnenbahn-Diagramm: die Tagesbahn nur bei Datums-/Standortwechsel neu berechnen.
        var today = DateOnly.FromDateTime(nowUtc);
        if (today != _dayArcDate)
        {
            _dayArcDate = today;
            SunPathRenderer.DayArc = _solar.GetDayArc(location, today, 10);
        }
        SunPathRenderer.CurrentAzimuth = sun.Azimuth;
        SunPathRenderer.CurrentElevation = sun.Elevation;
        SunPathRenderer.IsDaylight = sun.IsDaylight;
        SunPathInvalidateRequested?.Invoke();

        var times = _solar.GetSunTimes(location, DateOnly.FromDateTime(nowUtc));
        SunriseText = times.PolarNight ? "—" : times.PolarDay ? _loc.GetString("SunNeverSets") : FormatLocalTime(times.SunriseUtc);
        SunsetText = times.PolarDay ? "—" : times.PolarNight ? _loc.GetString("SunNeverRises") : FormatLocalTime(times.SunsetUtc);
        SolarNoonText = FormatLocalTime(times.SolarNoonUtc);

        var panel = SelectedPanel;
        var rec = _alignment.GetRecommendation(location, nowUtc, SelectedGoal.Goal, panel);
        TargetAzimuthText = $"{rec.TargetAzimuth:0}° {Compass(rec.TargetAzimuth)}";
        TargetTiltText = string.Format(_loc.GetString("TargetTiltFormat"), Num(rec.TargetTilt));
        ShowKickstand = panel.HasFixedTilts;
        KickstandText = panel.HasFixedTilts
            ? string.Format(_loc.GetString("KickstandSelect"), Num(rec.RecommendedKickstandTilt))
            : "";
        RecommendationExplanation = _loc.GetString(ExplanationKey(SelectedGoal.Goal, sun.IsDaylight));

        if (sun.IsDaylight)
        {
            var state = _alignment.Evaluate(sun, rec.TargetAzimuth, rec.RecommendedKickstandTilt, rec);
            CurrentYieldText = string.Format(_loc.GetString("YieldDirect"), Num(state.DirectGainFactor * 100));
        }
        else
        {
            CurrentYieldText = _loc.GetString("NoDirectYield");
        }

        var advice = _bifacial.GetAdvice(SelectedGround.Ground, panel);
        IsBifacial = panel.IsBifacial;
        AlbedoText = string.Format(_loc.GetString("AlbedoLabel"), advice.Albedo.ToString("0.00", CultureInfo.CurrentCulture));
        if (panel.IsBifacial)
        {
            BifacialGainText = string.Format(_loc.GetString("BifacialGain"),
                Num(advice.EstimatedGainLow * 100), Num(advice.EstimatedGainHigh * 100));
            TiltBonusText = advice.TiltBonusDegrees >= 1
                ? string.Format(_loc.GetString("TiltBonusTip"), Num(advice.TiltBonusDegrees))
                : "";
        }
        else
        {
            BifacialGainText = _loc.GetString("NotBifacial");
            TiltBonusText = "";
        }

        BifacialTips.Clear();
        foreach (var tipKey in advice.Tips)
            BifacialTips.Add(_loc.GetString(tipKey));
    }

    private static string ExplanationKey(AlignmentGoal goal, bool isDaylight) => goal switch
    {
        AlignmentGoal.NowMaximum => isDaylight ? "ExplNowMaximum" : "ExplNowNight",
        AlignmentGoal.TodayYield => "ExplTodayYield",
        AlignmentGoal.SeasonYield => "ExplSeasonYield",
        AlignmentGoal.WinterYield => "ExplWinterYield",
        _ => "ExplAnnualYield",
    };

    private static string Num(double value) => value.ToString("0", CultureInfo.CurrentCulture);

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

/// <summary>Auswahl-Eintrag für das Ausricht-Ziel (Enum + deutsches Label).</summary>
public sealed record GoalOption(AlignmentGoal Goal, string Label);

/// <summary>Auswahl-Eintrag für den Untergrund (Enum + deutsches Label).</summary>
public sealed record GroundOption(GroundType Ground, string Label);
