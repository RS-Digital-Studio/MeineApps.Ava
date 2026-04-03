using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>Haupt-ViewModel: Navigation (6 Tabs + Verbindung), Status-Bar, Back-Button</summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IBleService _bleService;
    private readonly BackPressHelper _backPressHelper = new();

    // Child-ViewModels
    public ConnectViewModel ConnectVm { get; }
    public SurveyViewModel SurveyVm { get; }
    public TerrainViewModel TerrainVm { get; }
    public GardenPlanViewModel GardenPlanVm { get; }
    public MapViewModel MapVm { get; }
    public ProjectsViewModel ProjectsVm { get; }
    public SettingsViewModel SettingsVm { get; }

    // Navigation (6 Haupttabs + Verbindung als Overlay/Sub)
    [ObservableProperty] private bool _isSurveyActive = true;
    [ObservableProperty] private bool _isTerrainActive;
    [ObservableProperty] private bool _isGardenActive;
    [ObservableProperty] private bool _isMapActive;
    [ObservableProperty] private bool _isProjectsActive;
    [ObservableProperty] private bool _isConnectActive;
    [ObservableProperty] private bool _isSettingsActive;

    // Status-Bar
    [ObservableProperty] private bool _isBleConnected;
    [ObservableProperty] private int _batteryLevel;
    [ObservableProperty] private int _fixQuality;
    [ObservableProperty] private string _fixStatusText = "KEIN FIX";
    [ObservableProperty] private int _satelliteCount;
    [ObservableProperty] private float _horizontalAccuracy;

    /// <summary>Double-Back-to-Exit Hinweis</summary>
    public event Action<string>? ExitHintRequested;

    public MainViewModel(
        IBleService bleService,
        ConnectViewModel connectVm,
        SurveyViewModel surveyVm,
        TerrainViewModel terrainVm,
        GardenPlanViewModel gardenPlanVm,
        MapViewModel mapVm,
        ProjectsViewModel projectsVm,
        SettingsViewModel settingsVm)
    {
        _bleService = bleService;
        ConnectVm = connectVm;
        SurveyVm = surveyVm;
        TerrainVm = terrainVm;
        GardenPlanVm = gardenPlanVm;
        MapVm = mapVm;
        ProjectsVm = projectsVm;
        SettingsVm = settingsVm;

        // BLE-Status Events (kommen vom Background-Thread, daher Dispatcher)
        _bleService.StateChanged += state => Dispatcher.UIThread.Post(() => OnStateChanged(state));
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
        _bleService.AccuracyUpdated += (h, _) => Dispatcher.UIThread.Post(() => HorizontalAccuracy = h);

        // Back-Button
        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void Navigate(string page)
    {
        IsSurveyActive = page == "Survey";
        IsTerrainActive = page == "Terrain";
        IsGardenActive = page == "Garden";
        IsMapActive = page == "Map";
        IsProjectsActive = page == "Projects";
        IsConnectActive = page == "Connect";
        IsSettingsActive = page == "Settings";
    }

    public bool HandleBackPressed()
    {
        // Wenn nicht auf Hauptseite → zurueck zu Survey
        if (!IsSurveyActive)
        {
            Navigate("Survey");
            return true;
        }

        // Double-Back-to-Exit
        return _backPressHelper.HandleDoubleBack("Nochmal drücken zum Beenden");
    }

    private void OnStateChanged(StickState state)
    {
        IsBleConnected = state.IsConnected;
        BatteryLevel = state.BatteryLevel;
        SatelliteCount = state.SatelliteCount;
        HorizontalAccuracy = state.HorizontalAccuracy;
    }
}
