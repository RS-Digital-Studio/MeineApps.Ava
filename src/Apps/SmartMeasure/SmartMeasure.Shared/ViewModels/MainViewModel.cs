using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>Haupt-ViewModel: Navigation (6 Tabs), Back-Button, AR-Transfer, Export-Banner</summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IArTransferService _arTransferService;
    private readonly IMeasurementService _measurementService;
    private readonly IProjectService _projectService;
    private readonly BackPressHelper _backPressHelper = new();

    // Child-ViewModels
    public SurveyViewModel SurveyVm { get; }
    public TerrainViewModel TerrainVm { get; }
    public GardenPlanViewModel GardenPlanVm { get; }
    public MapViewModel MapVm { get; }
    public ProjectsViewModel ProjectsVm { get; }
    public SettingsViewModel SettingsVm { get; }

    // Navigation (6 Haupttabs)
    [ObservableProperty] private bool _isSurveyActive = true;
    [ObservableProperty] private bool _isTerrainActive;
    [ObservableProperty] private bool _isGardenActive;
    [ObservableProperty] private bool _isMapActive;
    [ObservableProperty] private bool _isProjectsActive;
    [ObservableProperty] private bool _isSettingsActive;

    // Export-Share-Banner: Nach erfolgreichem Export sichtbar, bietet Teilen/Öffnen an
    [ObservableProperty] private string? _lastExportPath;
    [ObservableProperty] private string _lastExportFileName = string.Empty;
    [ObservableProperty] private bool _isExportBannerVisible;

    /// <summary>Double-Back-to-Exit Hinweis</summary>
    public event Action<string>? ExitHintRequested;

    /// <summary>Fehler-/Status-Nachricht anzeigen (Android: Toast, Desktop: Log)</summary>
    public event Action<string, string>? MessageRequested;

    public MainViewModel(
        IArTransferService arTransferService,
        IMeasurementService measurementService,
        IProjectService projectService,
        SurveyViewModel surveyVm,
        TerrainViewModel terrainVm,
        GardenPlanViewModel gardenPlanVm,
        MapViewModel mapVm,
        ProjectsViewModel projectsVm,
        SettingsViewModel settingsVm)
    {
        _arTransferService = arTransferService;
        _measurementService = measurementService;
        _projectService = projectService;
        SurveyVm = surveyVm;
        TerrainVm = terrainVm;
        GardenPlanVm = gardenPlanVm;
        MapVm = mapVm;
        ProjectsVm = projectsVm;
        SettingsVm = settingsVm;

        // Back-Button
        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);

        // Projekt-Events verdrahten
        ProjectsVm.ProjectSelected += async project =>
        {
            try
            {
                ProjectsVm.SelectedProject = project;
                GardenPlanVm.CurrentProjectId = project.Id;

                // Punkte in MeasurementService laden für TerrainView/MapView/GardenPlanView.
                // ReplacePoints feuert nur EIN PointsReset-Event — TerrainViewModel rechnet
                // das Delaunay-Mesh einmal statt N-mal pro Punkt.
                var full = await _projectService.GetProjectAsync(project.Id);
                if (full != null)
                {
                    _measurementService.ReplacePoints(full.Points);
                    await GardenPlanVm.LoadElementsFromProjectAsync(project.Id);
                }

                Navigate("Survey");
            }
            catch (Exception ex)
            {
                MessageRequested?.Invoke("Projekt konnte nicht geladen werden", ex.Message);
            }
        };

        // Export-Events: Banner einblenden mit Teilen/Öffnen-Optionen
        ProjectsVm.FileExportReady += path =>
        {
            LastExportPath = path;
            LastExportFileName = System.IO.Path.GetFileName(path);
            IsExportBannerVisible = true;
            MessageRequested?.Invoke("Export erstellt", LastExportFileName);
        };

        ProjectsVm.ExportFailed += reason =>
            MessageRequested?.Invoke("Export fehlgeschlagen", reason);

        // GardenPlan-Fehler (z.B. Zeichnung ohne Referenz verworfen)
        GardenPlanVm.MessageRequested += msg =>
            MessageRequested?.Invoke("Gartenplan", msg);

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
                        MessageRequested?.Invoke("AR-Transfer", "Kein Projekt verfügbar");
                        return;
                    }
                }

                var count = await _arTransferService.TransferToProjectAsync(result, project.Id);
                MessageRequested?.Invoke("AR-Capture", $"{count} Punkte übertragen");

                // GardenPlan mit neuen Konturen aktualisieren. CurrentProjectId MUSS gesetzt
                // werden — sonst persistiert manuelles Nachzeichnen im Garten-Tab nicht
                // (FinishDrawingAsync prueft CurrentProjectId > 0) → stiller Datenverlust.
                GardenPlanVm.CurrentProjectId = project.Id;
                await GardenPlanVm.LoadElementsFromProjectAsync(project.Id);
            }
            catch (Exception ex)
            {
                MessageRequested?.Invoke("AR-Transfer fehlgeschlagen", ex.Message);
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

    /// <summary>Letzten Export teilen (Share-Sheet via UriLauncher).</summary>
    [RelayCommand]
    private void ShareLastExport()
    {
        if (string.IsNullOrWhiteSpace(LastExportPath)) return;
        var mime = GetMimeTypeFromPath(LastExportPath);
        MeineApps.Core.Ava.Services.UriLauncher.ShareFile(LastExportPath, mime,
            $"SmartMeasure Export: {LastExportFileName}");
    }

    /// <summary>Letzten Export mit Standard-App öffnen (PDF-Reader, Google Earth für KMZ, etc.).</summary>
    [RelayCommand]
    private void OpenLastExport()
    {
        if (string.IsNullOrWhiteSpace(LastExportPath)) return;
        var mime = GetMimeTypeFromPath(LastExportPath);
        MeineApps.Core.Ava.Services.UriLauncher.OpenFile(LastExportPath, mime);
    }

    [RelayCommand]
    private void DismissExportBanner()
    {
        IsExportBannerVisible = false;
    }

    /// <summary>MIME-Type aus Dateiendung ableiten — wichtig damit Android die richtigen
    /// Share-Ziele anbietet (z.B. KMZ → Google Earth, DXF → CAD-Apps).</summary>
    private static string GetMimeTypeFromPath(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".csv" => "text/csv",
            ".geojson" => "application/geo+json",
            ".json" => "application/json",
            ".kmz" => "application/vnd.google-earth.kmz",
            ".kml" => "application/vnd.google-earth.kml+xml",
            ".dxf" => "application/dxf",
            ".obj" => "model/obj",
            ".mtl" => "model/mtl",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}
