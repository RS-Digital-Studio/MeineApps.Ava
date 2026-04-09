using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>Haupt-ViewModel: Navigation (6 Tabs + Verbindung), Status-Bar, Back-Button, AR-Transfer</summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IBleService _bleService;
    private readonly IArTransferService _arTransferService;
    private readonly IMeasurementService _measurementService;
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
        IArTransferService arTransferService,
        IMeasurementService measurementService,
        ConnectViewModel connectVm,
        SurveyViewModel surveyVm,
        TerrainViewModel terrainVm,
        GardenPlanViewModel gardenPlanVm,
        MapViewModel mapVm,
        ProjectsViewModel projectsVm,
        SettingsViewModel settingsVm)
    {
        _bleService = bleService;
        _arTransferService = arTransferService;
        _measurementService = measurementService;
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
            FixStatusText = _bleService.CurrentState.FixStatusText;
        });
        _bleService.AccuracyUpdated += (h, _) => Dispatcher.UIThread.Post(() => HorizontalAccuracy = h);

        // Back-Button
        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);

        // Projekt-Events verdrahten
        ProjectsVm.ProjectSelected += async project =>
        {
            ProjectsVm.SelectedProject = project;
            GardenPlanVm.CurrentProjectId = project.Id;

            // Punkte in MeasurementService laden fuer TerrainView/MapView/GardenPlanView
            var projectService = App.Services.GetRequiredService<IProjectService>();
            var full = await projectService.GetProjectAsync(project.Id);
            if (full != null)
            {
                _measurementService.ClearPoints();
                foreach (var pt in full.Points)
                    _measurementService.AddPoint(pt);

                await GardenPlanVm.LoadElementsFromProjectAsync(project.Id);
            }

            Navigate("Survey");
        };

        // Export-Events: Dateipfad dem User mitteilen
        ProjectsVm.FileExportReady += path =>
            System.Diagnostics.Debug.WriteLine($"Export erstellt: {path}");

        // AR-Capture → Terrain-Transfer
        SurveyVm.ArCaptureCompleted += async result =>
        {
            try
            {
                // Projekt sicherstellen (automatisch erstellen wenn keins ausgewaehlt)
                var project = ProjectsVm.SelectedProject;
                if (project == null)
                {
                    project = await ProjectsVm.EnsureProjectExistsAsync();
                    if (project == null)
                    {
                        System.Diagnostics.Debug.WriteLine("AR-Transfer: Kein Projekt verfuegbar");
                        return;
                    }
                }

                var count = await _arTransferService.TransferToProjectAsync(result, project.Id);
                System.Diagnostics.Debug.WriteLine($"AR-Transfer: {count} Punkte uebertragen");

                // GardenPlan mit neuen Konturen aktualisieren
                await GardenPlanVm.LoadElementsFromProjectAsync(project.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AR-Transfer fehlgeschlagen: {ex.Message}");
            }
        };
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
